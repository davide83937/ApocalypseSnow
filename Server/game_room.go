package main

import (
	"encoding/binary"
	"log"
	"math"
	"math/rand"
	"time"
)

type GameRoom struct {
	firstPlayerConnection  *PlayerConnection
	secondPlayerConnection *PlayerConnection

	firstPlayer  Player
	secondPlayer Player

	// Runtime netcode della room
	firstPlayerPendingInputs  []InputState
	secondPlayerPendingInputs []InputState

	tickRateHz uint32
	matchTick  uint32
}

func NewGameRoom(firstPlayerConnection, secondPlayerConnection *PlayerConnection) *GameRoom {
	return &GameRoom{
		firstPlayerConnection:  firstPlayerConnection,
		secondPlayerConnection: secondPlayerConnection,
		tickRateHz:             uint32(MoveHz),

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

		firstPlayerPendingInputs:  make([]InputState, 0, 64),
		secondPlayerPendingInputs: make([]InputState, 0, 64),
	}
}

func (gameRoom *GameRoom) Run() {
	gameRoom.matchTick = 0

	gameRoom.SendJoinAcknowledgements()
	gameRoom.CreateInitialEggs(5)

	tickDuration := time.Second / time.Duration(gameRoom.tickRateHz)
	ticker := time.NewTicker(tickDuration)
	defer ticker.Stop()

	// 1. Inizializziamo l'accumulatore per prevenire i freeze del server
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
			// 2. Calcoliamo quanto tempo reale è passato
			now := time.Now()
			accumulator += now.Sub(lastTime)
			lastTime = now

			// 3. Il "Catch-up loop": se c'è stato lag, recupera i tick persi all'istante!
			for accumulator >= tickDuration {
				gameRoom.Tick(now)
				accumulator -= tickDuration
			}
		}
	}
}

// Tick nuovo:
// - matchTick++
// - drena gli input nuovi dentro la pending queue della room
// - applica dalla pending tutti gli input con SeqN <= matchTick
// - gli input con SeqN > matchTick restano pending
// - scarta solo vecchi/duplicati (SeqN <= LastSeqN)
func (gameRoom *GameRoom) Tick(now time.Time) { // <-- Aggiunto now come parametro
	gameRoom.matchTick++
	deltaTime := float32(1.0 / float32(gameRoom.tickRateHz))

	drain1, drain2 := 0, 0
	inserted1, inserted2 := 0, 0
	applied1, applied2 := 0, 0
	future1, future2 := 0, 0
	droppedOld1, droppedOld2 := 0, 0

	// =======================
	// PLAYER 1
	// =======================
	drain1, inserted1, droppedOld1 = gameRoom.drainInputChannelToPending(
		gameRoom.firstPlayerConnection,
		&gameRoom.firstPlayer,
		&gameRoom.firstPlayerPendingInputs,
		now,
	)

	applied1, future1 = gameRoom.applyMaturePendingInputs(
		&gameRoom.firstPlayer,
		&gameRoom.firstPlayerPendingInputs,
		deltaTime,
	)

	// =======================
	// PLAYER 2
	// =======================
	drain2, inserted2, droppedOld2 = gameRoom.drainInputChannelToPending(
		gameRoom.secondPlayerConnection,
		&gameRoom.secondPlayer,
		&gameRoom.secondPlayerPendingInputs,
		now,
	)

	applied2, future2 = gameRoom.applyMaturePendingInputs(
		&gameRoom.secondPlayer,
		&gameRoom.secondPlayerPendingInputs,
		deltaTime,
	)

	// =======================
	// BROADCAST STATO
	// =======================
	gameRoom.sendAuthStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.firstPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.secondPlayer)

	gameRoom.sendAuthStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.secondPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.firstPlayer)

	// =======================
	// LOG 1 VOLTA AL SECONDO
	// =======================
	if gameRoom.matchTick%gameRoom.tickRateHz == 0 {
		age1 := int64(-1)
		if !gameRoom.firstPlayer.LastRx.IsZero() {
			age1 = now.Sub(gameRoom.firstPlayer.LastRx).Milliseconds()
		}
		age2 := int64(-1)
		if !gameRoom.secondPlayer.LastRx.IsZero() {
			age2 = now.Sub(gameRoom.secondPlayer.LastRx).Milliseconds()
		}

		log.Printf(
			"[TICK %d] "+
				"P1 drain=%d inserted=%d applied=%d pending=%d future=%d droppedOld=%d ageMs=%d lastSeq=%d mask=%d pos=(%.0f,%.0f) | "+
				"P2 drain=%d inserted=%d applied=%d pending=%d future=%d droppedOld=%d ageMs=%d lastSeq=%d mask=%d pos=(%.0f,%.0f)",
			gameRoom.matchTick,
			drain1, inserted1, applied1, len(gameRoom.firstPlayerPendingInputs), future1, droppedOld1, age1, gameRoom.firstPlayer.LastSeqN, gameRoom.firstPlayer.LastMask, gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y,
			drain2, inserted2, applied2, len(gameRoom.secondPlayerPendingInputs), future2, droppedOld2, age2, gameRoom.secondPlayer.LastSeqN, gameRoom.secondPlayer.LastMask, gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y,
		)
	}

	// =======================
	// FORWARD SHOTS
	// =======================
drainFirstPlayerShots:
	for {
		select {
		case shotEvent := <-gameRoom.firstPlayerConnection.ShotChannel:
			gameRoom.forwardShotEventToOpponent(gameRoom.secondPlayerConnection, shotEvent)
		default:
			break drainFirstPlayerShots
		}
	}

drainSecondPlayerShots:
	for {
		select {
		case shotEvent := <-gameRoom.secondPlayerConnection.ShotChannel:
			gameRoom.forwardShotEventToOpponent(gameRoom.firstPlayerConnection, shotEvent)
		default:
			break drainSecondPlayerShots
		}
	}
}

func (gameRoom *GameRoom) drainInputChannelToPending(
	playerConnection *PlayerConnection,
	player *Player,
	pending *[]InputState,
	now time.Time,
) (drain int, inserted int, droppedOld int) {
	for n := 0; n < maxDrainInputsPerTick; n++ {
		select {
		case inputState := <-playerConnection.InputChannel:
			drain++
			player.LastRx = now
			player.RecvMask = inputState.Mask
			player.RecvSeqN = inputState.SeqN

			// vecchio o duplicato rispetto a ciò che abbiamo già applicato
			if inputState.SeqN <= player.LastSeqN {
				droppedOld++
				continue
			}

			// evita duplicati già in pending
			alreadyQueued := false
			for i := range *pending {
				if (*pending)[i].SeqN == inputState.SeqN {
					alreadyQueued = true
					break
				}
			}
			if alreadyQueued {
				continue
			}

			*pending = append(*pending, inputState)
			inserted++

		default:
			return
		}
	}
	return
}

func (gameRoom *GameRoom) applyMaturePendingInputs(
	player *Player,
	pending *[]InputState,
	deltaTime float32,
) (applied int, futureCount int) {
	consumeCount := 0

	for consumeCount < len(*pending) {
		inputState := (*pending)[consumeCount]

		// se è vecchio rispetto all'ultimo applicato, buttalo
		if inputState.SeqN <= player.LastSeqN {
			consumeCount++
			continue
		}

		// se è ancora nel futuro, ci fermiamo: i successivi saranno futuri pure loro
		if inputState.SeqN > gameRoom.matchTick {
			break
		}

		player.X, player.Y = stepFromStateDLL(
			player.X, player.Y, inputState.Mask, deltaTime,
		)

		player.LastMask = inputState.Mask
		player.LastSeqN = inputState.SeqN
		player.RecvMask = inputState.Mask
		player.RecvSeqN = inputState.SeqN
		applied++
		consumeCount++
	}

	if consumeCount > 0 {
		remaining := (*pending)[consumeCount:]
		copy((*pending), remaining)
		*pending = (*pending)[:len(remaining)]
	}

	futureCount = len(*pending)
	return
}

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

func (gameRoom *GameRoom) CreateInitialEggs(eggCount int) {
	for eggId := 0; eggId < eggCount; eggId++ {
		eggX := float32(250 + rand.Intn(250))
		eggY := float32(rand.Intn(400))
		gameRoom.sendSpawnEggToBothPlayers(int32(eggId), eggX, eggY)
	}
}

func (gameRoom *GameRoom) sendSpawnEggToBothPlayers(eggId int32, eggX, eggY float32) {
	packet := make([]byte, 13)
	packet[0] = MsgSpawnEgg
	binary.LittleEndian.PutUint32(packet[1:5], uint32(eggId))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(eggX))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(eggY))

	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
}

func (gameRoom *GameRoom) sendAuthStateToPlayer(targetConn *PlayerConnection, player Player) {
	packet := make([]byte, 13)
	packet[0] = MsgAuthState
	binary.LittleEndian.PutUint32(packet[1:5], player.LastSeqN)
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(player.X))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(player.Y))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

func (gameRoom *GameRoom) sendRemoteStateToPlayer(targetConn *PlayerConnection, opponent Player) {
	packet := make([]byte, 13)
	packet[0] = MsgRemoteState
	binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(opponent.X))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(opponent.Y))
	binary.LittleEndian.PutUint32(packet[9:13], uint32(opponent.LastMask))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

func (gameRoom *GameRoom) forwardShotEventToOpponent(targetConn *PlayerConnection, shot ShotEvent) {
	charge := shot.Charge
	if charge < 0 {
		charge = 0
	}
	if charge > chargeCap {
		charge = chargeCap
	}

	packet := make([]byte, 13)
	packet[0] = MsgRemoteShot
	binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(shot.X))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(shot.Y))
	binary.LittleEndian.PutUint32(packet[9:13], uint32(charge))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}
