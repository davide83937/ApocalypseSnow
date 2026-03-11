package main

import (
	"encoding/binary"
	"fmt"
	"log"
	"math"
	"math/rand"
	"time"
)

// GameRoom rappresenta una partita 1v1 lato server.
// Il server è autoritativo su:
// - movimento
// - collisioni
// - gestione degli shot
// - broadcast degli stati ai due client
type GameRoom struct {
	firstPlayerConnection  *PlayerConnection
	secondPlayerConnection *PlayerConnection

	firstPlayer  Player
	secondPlayer Player

	// Code di eventi pendenti per ciascun player.
	// Qui accumuliamo input e shot packet finché non diventano "maturi"
	// rispetto al matchTick del server.
	firstPlayerPendingEvents  []PlayerEvent
	secondPlayerPendingEvents []PlayerEvent

	// Ostacoli statici presenti nella mappa.
	Obstacles []Position

	// Gestore collisioni server-side.
	collisionManager *CollisionManager

	// Shot realmente creati lato server.
	activeShots []Shot
	nextShotID  uint32

	// Clock logico della stanza.
	tickRateHz uint32
	matchTick  uint32
}

// NewGameRoom crea una nuova stanza di gioco con:
// - due player
// - due connection
// - ostacoli iniziali
// - collision manager
func NewGameRoom(firstPlayerConnection, secondPlayerConnection *PlayerConnection) *GameRoom {
	return &GameRoom{
		firstPlayerConnection:     firstPlayerConnection,
		secondPlayerConnection:    secondPlayerConnection,
		tickRateHz:                uint32(MoveHz),
		firstPlayerPendingEvents:  make([]PlayerEvent, 0, 64),
		secondPlayerPendingEvents: make([]PlayerEvent, 0, 64),

		activeShots:      make([]Shot, 0, 64),
		nextShotID:       1,
		collisionManager: NewCollisionManager(),

		Obstacles: []Position{
			{X: 100, Y: 100},
			{X: 500, Y: 200},
		},

		firstPlayer: Player{
			ID:       1,
			Position: Position{X: 100, Y: 300},
			LastMask: 0,
			LastSeqN: 0,
			RecvMask: 0,
			RecvSeqN: 0,
			LastRx:   time.Time{},
		},
		secondPlayer: Player{
			ID:       2,
			Position: Position{X: 550, Y: 25},
			LastMask: 0,
			LastSeqN: 0,
			RecvMask: 0,
			RecvSeqN: 0,
			LastRx:   time.Time{},
		},
	}
}

// Run è il loop principale della room.
// Mantiene un ticker fisso e accumula il tempo reale per garantire
// che i Tick() vengano eseguiti con il passo logico corretto.
func (gameRoom *GameRoom) Run() {
	gameRoom.matchTick = 0

	gameRoom.SendJoinAcknowledgements()
	gameRoom.CreateInitialEggs(5)
	gameRoom.sendSpawnObstacleToBothPlayers()

	tickDuration := time.Second / time.Duration(gameRoom.tickRateHz)
	ticker := time.NewTicker(tickDuration)
	defer ticker.Stop()

	lastTime := time.Now()
	var accumulator time.Duration

	for {
		select {
		case <-gameRoom.firstPlayerConnection.DisconnectChannel:
			log.Println("GameRoom: il giocatore 1 si è disconnesso. Chiudo la stanza.")
			gameRoom.secondPlayerConnection.CloseConnection()
			return

		case <-gameRoom.secondPlayerConnection.DisconnectChannel:
			log.Println("GameRoom: il giocatore 2 si è disconnesso. Chiudo la stanza.")
			gameRoom.firstPlayerConnection.CloseConnection()
			return

		case <-ticker.C:
			now := time.Now()
			elapsed := now.Sub(lastTime)
			lastTime = now

			accumulator += elapsed

			for accumulator >= tickDuration {
				gameRoom.Tick()
				accumulator -= tickDuration
			}
		}
	}
}

// Tick esegue un singolo passo logico del gioco:
// 1. drena i messaggi dai due player
// 2. applica gli eventi maturi
// 3. manda a ciascun player:
//    - il proprio stato autoritativo
//    - lo stato remoto dell’avversario
func (gameRoom *GameRoom) Tick() {
	gameRoom.matchTick++
	deltaTime := float32(1.0 / float32(gameRoom.tickRateHz))
	now := time.Now()

	// PLAYER 1
	gameRoom.drainEventChannelToPending(
		gameRoom.firstPlayerConnection,
		&gameRoom.firstPlayer,
		&gameRoom.firstPlayerPendingEvents,
		now,
	)

	gameRoom.applyMaturePendingEvents(
		&gameRoom.firstPlayer,
		&gameRoom.firstPlayerPendingEvents,
		gameRoom.secondPlayerConnection,
		deltaTime,
	)

	// PLAYER 2
	gameRoom.drainEventChannelToPending(
		gameRoom.secondPlayerConnection,
		&gameRoom.secondPlayer,
		&gameRoom.secondPlayerPendingEvents,
		now,
	)

	gameRoom.applyMaturePendingEvents(
		&gameRoom.secondPlayer,
		&gameRoom.secondPlayerPendingEvents,
		gameRoom.firstPlayerConnection,
		deltaTime,
	)

	// Broadcast stato
	gameRoom.sendAuthStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.firstPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.secondPlayer)

	gameRoom.sendAuthStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.secondPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.firstPlayer)
}

// drainEventChannelToPending sposta eventi dalla channel di rete alla coda pending del player.
// Gli input troppo vecchi vengono scartati subito.
func (gameRoom *GameRoom) drainEventChannelToPending(
	playerConnection *PlayerConnection,
	player *Player,
	pending *[]PlayerEvent,
	now time.Time,
) (drain int, inserted int, droppedOld int) {
	for n := 0; n < maxDrainInputsPerTick; n++ {
		select {
		case event := <-playerConnection.EventChannel:
			drain++

			if event.Type == EventInput {
				player.LastRx = now
				player.RecvMask = event.Input.Mask
				player.RecvSeqN = event.Input.SeqN

				// Se l'input è più vecchio o uguale all'ultimo già processato,
				// lo scartiamo direttamente.
				if event.Input.SeqN <= player.LastSeqN {
					droppedOld++
					continue
				}
			}

			*pending = append(*pending, event)
			inserted++

		default:
			return
		}
	}
	return
}

// updatePendingShot implementa la FSM del tiro lato server.
// È pensata per essere speculare alla FSM lato client.
//
// Stati logici:
// - Idle            => !ps.Active && !ps.WaitingNeutral
// - ChargingLeft    => ps.Active && ps.Mode == ChargeLeft
// - ChargingRight   => ps.Active && ps.Mode == ChargeRight
// - WaitingNeutral  => ps.WaitingNeutral == true
//
// Regole:
// - first press wins
// - mentre è attiva una carica, l'altro tasto viene ignorato
// - quando il tasto lockato viene rilasciato, entriamo in WaitingShotPacket
// - dopo la creazione dello shot, andremo in WaitingNeutral
func (gameRoom *GameRoom) updatePendingShot(player *Player, mask int32, seqN uint32) {
	leftNow := (mask & ShootLeft) != 0
	rightNow := (mask & ShootRight) != 0

	leftPrev := (player.LastMask & ShootLeft) != 0
	rightPrev := (player.LastMask & ShootRight) != 0

	leftJustPressed := leftNow && !leftPrev
	rightJustPressed := rightNow && !rightPrev

	ps := &player.PendingShot

	// =======================================================
	// STATO: WaitingNeutral
	// =======================================================
	// Dopo aver chiuso uno shot, il server non accetta una nuova carica
	// finché entrambi i tasti non tornano rilasciati.
	if ps.WaitingNeutral {
		if !leftNow && !rightNow {
			ps.WaitingNeutral = false
		}
		return
	}

	// =======================================================
	// STATO: Idle
	// =======================================================
	// Nessuna carica attiva: first press wins.
	if !ps.Active {
		if leftJustPressed {
			ps.Active = true
			ps.Mode = ChargeLeft
			ps.PressTick = seqN
			ps.ReleaseTick = 0
			ps.WaitingShotPacket = false
			ps.ShotPacket = nil

			log.Printf("[SHOT PRESS] owner=%d mode=LEFT pressedTick=%d", player.ID, seqN)
			return
		}

		if rightJustPressed {
			ps.Active = true
			ps.Mode = ChargeRight
			ps.PressTick = seqN
			ps.ReleaseTick = 0
			ps.WaitingShotPacket = false
			ps.ShotPacket = nil

			log.Printf("[SHOT PRESS] owner=%d mode=RIGHT pressedTick=%d", player.ID, seqN)
			return
		}

		return
	}

	// =======================================================
	// STATO: WaitingShotPacket
	// =======================================================
	// Abbiamo già visto il rilascio del tasto lockato e stiamo aspettando
	// che arrivi il MsgShot vero e proprio.
	if ps.WaitingShotPacket {
		return
	}

	// =======================================================
	// STATO: ChargingLeft / ChargingRight
	// =======================================================
	switch ps.Mode {
	case ChargeLeft:
		// Finché il sinistro resta premuto, continuiamo la carica.
		// Il destro è ignorato.
		if !leftNow {
			ps.ReleaseTick = seqN
			ps.WaitingShotPacket = true
			log.Printf("[SHOT RELEASE] owner=%d mode=LEFT releasedTick=%d", player.ID, seqN)
		}

	case ChargeRight:
		// Finché il destro resta premuto, continuiamo la carica.
		// Il sinistro è ignorato.
		if !rightNow {
			ps.ReleaseTick = seqN
			ps.WaitingShotPacket = true
			log.Printf("[SHOT RELEASE] owner=%d mode=RIGHT releasedTick=%d", player.ID, seqN)
		}
	}
}

// applyMaturePendingEvents applica tutti gli eventi con seq <= matchTick.
// Qui avvengono:
// - movimento autoritativo
// - collisioni autoritative
// - avanzamento FSM shot
// - creazione shot quando release + packet shot sono entrambi presenti
func (gameRoom *GameRoom) applyMaturePendingEvents(
	player *Player,
	pending *[]PlayerEvent,
	opponentConn *PlayerConnection,
	deltaTime float32,
) (applied int, futureCount int) {
	consumeCount := 0

	for consumeCount < len(*pending) {
		event := (*pending)[consumeCount]

		// Gli eventi futuri restano in coda.
		if event.SeqN() > gameRoom.matchTick {
			break
		}

		switch event.Type {
		case EventInput:
			inputState := event.Input

			// Protezione ulteriore contro input vecchi.
			if inputState.SeqN <= player.LastSeqN {
				consumeCount++
				continue
			}

			// Aggiorniamo prima la FSM del tiro.
			gameRoom.updatePendingShot(player, inputState.Mask, inputState.SeqN)

			// Movimento autoritativo.
			oldX, oldY := player.X, player.Y
			newX, newY := StepFromState(player.X, player.Y, inputState.Mask, deltaTime)

			// Collisione autoritativa contro gli ostacoli.
			if gameRoom.collisionManager.CheckPlayerCollisionAt(newX, newY, gameRoom.Obstacles) {
				player.X, player.Y = oldX, oldY
			} else {
				player.X, player.Y = newX, newY
			}

			// Aggiorniamo il last processed input del player.
			player.LastMask = inputState.Mask
			player.LastSeqN = inputState.SeqN
			player.RecvMask = inputState.Mask
			player.RecvSeqN = inputState.SeqN
			applied++

			// Se avevamo già visto il release e lo shot packet era già arrivato,
			// possiamo creare lo shot adesso.
			ps := &player.PendingShot
			if ps.Active && ps.WaitingShotPacket && ps.ShotPacket != nil {
				shot := gameRoom.createShotFromEvent(player, *ps.ShotPacket)
				if shot.ShotID != 0 {
					gameRoom.forwardShotEventToOpponent(opponentConn, shot)
				}
				ps.ShotPacket = nil
			}

		case EventShot:
			shotEvent := event.Shot
			ps := &player.PendingShot

			// Se c'è una carica attiva, memorizziamo il packet shot.
			// Lo shot reale verrà creato quando avremo anche il release.
			if ps.Active {
				copied := shotEvent
				ps.ShotPacket = &copied
			}

			// Se il release è già stato visto, possiamo creare subito lo shot.
			if ps.Active && ps.WaitingShotPacket && ps.ShotPacket != nil {
				shot := gameRoom.createShotFromEvent(player, *ps.ShotPacket)
				if shot.ShotID != 0 {
					gameRoom.forwardShotEventToOpponent(opponentConn, shot)
				}
				ps.ShotPacket = nil
			}

			applied++
		}

		consumeCount++
	}

	// Rimuoviamo dalla coda tutti gli eventi consumati.
	if consumeCount > 0 {
		remaining := (*pending)[consumeCount:]
		copy((*pending), remaining)
		*pending = (*pending)[:len(remaining)]
	}

	futureCount = len(*pending)
	return
}

// SendJoinAcknowledgements manda a ciascun client:
// - il proprio ID
// - la propria posizione iniziale
// - la posizione dell'altro player
// - il tick rate della stanza
func (gameRoom *GameRoom) SendJoinAcknowledgements() {
	packetP1 := make([]byte, 25)
	packetP1[0] = MsgJoinAck
	binary.LittleEndian.PutUint32(packetP1[1:5], gameRoom.firstPlayer.ID)
	binary.LittleEndian.PutUint32(packetP1[5:9], math.Float32bits(gameRoom.firstPlayer.X))
	binary.LittleEndian.PutUint32(packetP1[9:13], math.Float32bits(gameRoom.firstPlayer.Y))
	binary.LittleEndian.PutUint32(packetP1[13:17], math.Float32bits(gameRoom.secondPlayer.X))
	binary.LittleEndian.PutUint32(packetP1[17:21], math.Float32bits(gameRoom.secondPlayer.Y))
	binary.LittleEndian.PutUint32(packetP1[21:25], gameRoom.tickRateHz)
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packetP1, true)

	packetP2 := make([]byte, 25)
	packetP2[0] = MsgJoinAck
	binary.LittleEndian.PutUint32(packetP2[1:5], gameRoom.secondPlayer.ID)
	binary.LittleEndian.PutUint32(packetP2[5:9], math.Float32bits(gameRoom.secondPlayer.X))
	binary.LittleEndian.PutUint32(packetP2[9:13], math.Float32bits(gameRoom.secondPlayer.Y))
	binary.LittleEndian.PutUint32(packetP2[13:17], math.Float32bits(gameRoom.firstPlayer.X))
	binary.LittleEndian.PutUint32(packetP2[17:21], math.Float32bits(gameRoom.firstPlayer.Y))
	binary.LittleEndian.PutUint32(packetP2[21:25], gameRoom.tickRateHz)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packetP2, true)
}

// CreateInitialEggs genera un certo numero di uova iniziali
// e le manda a entrambi i player.
func (gameRoom *GameRoom) CreateInitialEggs(eggCount int) {
	for eggId := 0; eggId < eggCount; eggId++ {
		eggX := float32(250 + rand.Intn(250))
		eggY := float32(rand.Intn(400))
		gameRoom.sendSpawnEggToBothPlayers(int32(eggId), eggX, eggY)
	}
}

// sendSpawnEggToBothPlayers manda un pacchetto spawn egg a entrambi i client.
func (gameRoom *GameRoom) sendSpawnEggToBothPlayers(eggId int32, eggX, eggY float32) {
	packet := make([]byte, 13)
	packet[0] = MsgSpawnEgg
	binary.LittleEndian.PutUint32(packet[1:5], uint32(eggId))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(eggX))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(eggY))
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
}

// sendSpawnObstacleToBothPlayers manda a entrambi i client gli ostacoli iniziali.
func (gameRoom *GameRoom) sendSpawnObstacleToBothPlayers() {
	for i := 0; i < len(gameRoom.Obstacles); i++ {
		packet := make([]byte, 13)
		packet[0] = MsgSpawnObstacle

		fmt.Printf("Coordinate: %+v\n", gameRoom.Obstacles[i])

		binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(gameRoom.Obstacles[i].X))
		binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(gameRoom.Obstacles[i].Y))

		gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
		gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	}
}

// sendAuthStateToPlayer manda a un client il proprio stato autoritativo:
// - ack seq
// - posizione vera lato server
func (gameRoom *GameRoom) sendAuthStateToPlayer(targetConn *PlayerConnection, player Player) {
	packet := make([]byte, 13)
	packet[0] = MsgAuthState
	binary.LittleEndian.PutUint32(packet[1:5], player.LastSeqN)
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(player.X))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(player.Y))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

// sendRemoteStateToPlayer manda a un client lo stato remoto dell’avversario.
func (gameRoom *GameRoom) sendRemoteStateToPlayer(targetConn *PlayerConnection, opponent Player) {
	packet := make([]byte, 13)
	packet[0] = MsgRemoteState
	binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(opponent.X))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(opponent.Y))
	binary.LittleEndian.PutUint32(packet[9:13], uint32(opponent.LastMask))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

// forwardShotEventToOpponent manda al client remoto l'evento shot da renderizzare.
// Per ora usa il charge ricavato da AuthCharge tick-based.
func (gameRoom *GameRoom) forwardShotEventToOpponent(targetConn *PlayerConnection, shot Shot) {
	chargeMs := uint32(float64(shot.AuthCharge) * (1000.0 / float64(gameRoom.tickRateHz)))
	if chargeMs > chargeCap {
		chargeMs = chargeCap
	}

	packet := make([]byte, 14)
	packet[0] = MsgRemoteShot
	binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(shot.TargetX))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(shot.TargetY))
	binary.LittleEndian.PutUint32(packet[9:13], chargeMs)
	packet[13] = byte(shot.Type)

	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

// createShotFromEvent crea lo shot vero e proprio quando il server ha:
// - press tick
// - release tick
// - packet shot con target/charge client
//
// Dopo la creazione resetta lo stato PendingShot e porta il player in WaitingNeutral.
func (gameRoom *GameRoom) createShotFromEvent(player *Player, shotEvent ShotEvent) Shot {
	ps := &player.PendingShot

	if !ps.Active {
		log.Printf("[SHOT] owner=%d arrivato MsgShot ma nessuna carica attiva", player.ID)
		return Shot{}
	}

	if !ps.WaitingShotPacket {
		log.Printf("[SHOT] owner=%d arrivato MsgShot ma la carica non è stata ancora rilasciata", player.ID)
		return Shot{}
	}

	if ps.ReleaseTick < ps.PressTick {
		log.Printf("[SHOT] owner=%d releaseTick < pressTick", player.ID)
		return Shot{}
	}

	authCharge := ps.ReleaseTick - ps.PressTick

	shotType := ShotTypeLeft
	if ps.Mode == ChargeRight {
		shotType = ShotTypeRight
	}

	shot := Shot{
		ShotID:        gameRoom.nextShotID,
		OwnerPlayerID: player.ID,
		TargetX:       shotEvent.X,
		TargetY:       shotEvent.Y,
		LocalCharge:   shotEvent.Charge,
		PressedTick:   ps.PressTick,
		ReleasedTick:  ps.ReleaseTick,
		AuthCharge:    authCharge,
		Type:          shotType,
	}

	gameRoom.nextShotID++
	gameRoom.activeShots = append(gameRoom.activeShots, shot)

	authChargeMs := uint32(float64(shot.AuthCharge) * (1000.0 / float64(gameRoom.tickRateHz)))
	deltaMs := int64(shot.LocalCharge) - int64(authChargeMs)

	// Integrazione dell'errore di quantizzazione.
	// Qui sto mantenendo la tua logica attuale.
	if deltaMs <= 34 {
		authChargeMs = uint32(int64(authChargeMs) + deltaMs)
	}

	// Cap finale.
	if authChargeMs > chargeCap {
		authChargeMs = chargeCap
	}

	log.Printf(
		"[SHOT CMP] owner=%d shotId=%d type=%d press=%d release=%d localChargeMs=%d authChargeTicks=%d authChargeMs=%d deltaMs=%d target=(%.1f, %.1f)",
		shot.OwnerPlayerID,
		shot.ShotID,
		shot.Type,
		shot.PressedTick,
		shot.ReleasedTick,
		shot.LocalCharge,
		shot.AuthCharge,
		authChargeMs,
		deltaMs,
		shot.TargetX,
		shot.TargetY,
	)

	// Reset FSM shot dopo la creazione.
	ps.Active = false
	ps.Mode = ChargeNone
	ps.PressTick = 0
	ps.ReleaseTick = 0
	ps.WaitingShotPacket = false
	ps.WaitingNeutral = true
	ps.ShotPacket = nil

	return shot
}
