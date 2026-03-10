package main

import "C"
import (
	"encoding/binary"
	"fmt"
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
	firstPlayerPendingEvents  []PlayerEvent
	secondPlayerPendingEvents []PlayerEvent

	Obstacles []Position

	// Shot lato server:
	// - activeShots contiene gli shot creati davvero
	// - nextShotID genera ID progressivi
	activeShots []Shot
	nextShotID  uint32

	tickRateHz uint32
	matchTick  uint32
}

func NewGameRoom(firstPlayerConnection, secondPlayerConnection *PlayerConnection) *GameRoom {
	return &GameRoom{
		firstPlayerConnection:     firstPlayerConnection,
		secondPlayerConnection:    secondPlayerConnection,
		tickRateHz:                uint32(MoveHz),
		firstPlayerPendingEvents:  make([]PlayerEvent, 0, 64),
		secondPlayerPendingEvents: make([]PlayerEvent, 0, 64),

		activeShots: make([]Shot, 0, 64),
		nextShotID:  1,

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

	// BROADCAST STATO
	gameRoom.sendAuthStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.firstPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.secondPlayer)

	gameRoom.sendAuthStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.secondPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.firstPlayer)
}

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

func (gameRoom *GameRoom) updatePendingShot(player *Player, mask int32, seqN uint32) {
	leftNow := (mask & ShootLeft) != 0
	rightNow := (mask & ShootRight) != 0

	leftPrev := (player.LastMask & ShootLeft) != 0
	rightPrev := (player.LastMask & ShootRight) != 0

	leftRise := leftNow && !leftPrev
	rightRise := rightNow && !rightPrev

	leftFall := !leftNow && leftPrev
	rightFall := !rightNow && rightPrev

	ps := &player.PendingShot

	if ps.WaitingNeutral {
		if !leftNow && !rightNow {
			ps.WaitingNeutral = false
		}
		return
	}

	if !ps.Active {
		if leftRise && !rightRise {
			ps.Active = true
			ps.Mode = ChargeLeft
			ps.PressTick = seqN
			ps.ReleaseTick = 0
			ps.WaitingShotPacket = false
			ps.ShotPacket = nil
			log.Printf("[SHOT PRESS] owner=%d mode=LEFT pressedTick=%d", player.ID, seqN)
		} else if rightRise && !leftRise {
			ps.Active = true
			ps.Mode = ChargeRight
			ps.PressTick = seqN
			ps.ReleaseTick = 0
			ps.WaitingShotPacket = false
			ps.ShotPacket = nil
			log.Printf("[SHOT PRESS] owner=%d mode=RIGHT pressedTick=%d", player.ID, seqN)
		}
		return
	}

	if ps.WaitingShotPacket {
		return
	}

	switch ps.Mode {
	case ChargeLeft:
		if leftFall {
			ps.ReleaseTick = seqN
			ps.WaitingShotPacket = true
			log.Printf("[SHOT RELEASE] owner=%d mode=LEFT releasedTick=%d", player.ID, seqN)
		}

	case ChargeRight:
		if rightFall {
			ps.ReleaseTick = seqN
			ps.WaitingShotPacket = true
			log.Printf("[SHOT RELEASE] owner=%d mode=RIGHT releasedTick=%d", player.ID, seqN)
		}
	}
}

func (gameRoom *GameRoom) applyMaturePendingEvents(
	player *Player,
	pending *[]PlayerEvent,
	opponentConn *PlayerConnection,
	deltaTime float32,
) (applied int, futureCount int) {
	consumeCount := 0

	for consumeCount < len(*pending) {
		event := (*pending)[consumeCount]

		if event.SeqN() > gameRoom.matchTick {
			break
		}

		switch event.Type {
		case EventInput:
			inputState := event.Input

			if inputState.SeqN <= player.LastSeqN {
				consumeCount++
				continue
			}

			gameRoom.updatePendingShot(player, inputState.Mask, inputState.SeqN)

			oldX, oldY := player.X, player.Y
			newX, newY := stepFromStateDLL(player.X, player.Y, inputState.Mask, deltaTime)

			if gameRoom.checkCollisionAt(newX, newY) {
				player.X, player.Y = oldX, oldY
			} else {
				player.X, player.Y = newX, newY
			}

			player.LastMask = inputState.Mask
			player.LastSeqN = inputState.SeqN
			player.RecvMask = inputState.Mask
			player.RecvSeqN = inputState.SeqN
			applied++

			// Se release già visto e lo shot packet era arrivato prima, crea ora lo shot
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

			// Memorizza il packet shot se c'è una carica attiva
			if ps.Active {
				copied := shotEvent
				ps.ShotPacket = &copied
			}

			// Se il release era già arrivato, crea subito lo shot
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

func (gameRoom *GameRoom) sendSpawnObstacleToBothPlayers() {
	for i := 0; i < 2; i++ {
		packet := make([]byte, 13)
		packet[0] = MsgSpawnObstacle
		fmt.Printf("Coordinate: %+v\n", gameRoom.Obstacles[i])
		binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(gameRoom.Obstacles[i].X))
		binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(gameRoom.Obstacles[i].Y))

		gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
		gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	}
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

	//integriamo l'errore di quantizzazione
	if deltaMs <= 34 {
		authChargeMs = uint32(int64(authChargeMs) + deltaMs)
	}
	
	//infine cappiamo al massimo
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

	ps.Active = false
	ps.Mode = ChargeNone
	ps.PressTick = 0
	ps.ReleaseTick = 0
	ps.WaitingShotPacket = false
	ps.WaitingNeutral = true
	ps.ShotPacket = nil

	return shot
}
