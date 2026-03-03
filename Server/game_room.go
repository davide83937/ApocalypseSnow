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

	tickRateHz int
}

func NewGameRoom(firstPlayerConnection, secondPlayerConnection *PlayerConnection) *GameRoom {
	return &GameRoom{
		firstPlayerConnection:  firstPlayerConnection,
		secondPlayerConnection: secondPlayerConnection,
		tickRateHz:             int(MoveHz),

		// Spawn identici al vecchio old_main.go
		firstPlayer: Player{
			ID:       1,
			Position: Position{X: 100, Y: 300},
			LastMask: 0,
			LastSeqN: 0,
		},
		secondPlayer: Player{
			ID:       2,
			Position: Position{X: 550, Y: 25},
			LastMask: 0,
			LastSeqN: 0,
		},
	}
}

func (gameRoom *GameRoom) Run() {
	// JoinAck (blocca il client se manca)
	gameRoom.SendJoinAcknowledgements()

	// Parità col vecchio server: 5 uova subito
	gameRoom.CreateInitialEggs(5)

	ticker := time.NewTicker(time.Second / time.Duration(gameRoom.tickRateHz))
	defer ticker.Stop()

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
			gameRoom.Tick()
		}
	}
}

// Tick compatibile con old_main:
// - processa fino a maxCatchupPerTick input per player, facendo uno step fisico per input
// - se non arriva input, fa 1 step con l'ultima mask (dead reckoning)
func (gameRoom *GameRoom) Tick() {
	deltaTime := float32(1.0 / float32(gameRoom.tickRateHz))

	// =======================
	// PLAYER 1: catch-up bounded
	// =======================
	processedFramesFirstPlayer := 0
drainFirstPlayerInputs:
	for {
		select {
		case inputState := <-gameRoom.firstPlayerConnection.InputChannel:
			gameRoom.firstPlayer.LastMask = inputState.Mask
			gameRoom.firstPlayer.LastSeqN = inputState.SeqN

			gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y = stepFromStateDLL(
				gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y, gameRoom.firstPlayer.LastMask, deltaTime)

			processedFramesFirstPlayer++
			if processedFramesFirstPlayer >= maxCatchupPerTick {
				break drainFirstPlayerInputs
			}
		default:
			break drainFirstPlayerInputs
		}
	}

	if processedFramesFirstPlayer == 0 {
		gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y = stepFromStateDLL(
			gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y, gameRoom.firstPlayer.LastMask, deltaTime)
	}

	// =======================
	// PLAYER 2: catch-up bounded
	// =======================
	processedFramesSecondPlayer := 0
drainSecondPlayerInputs:
	for {
		select {
		case inputState := <-gameRoom.secondPlayerConnection.InputChannel:
			gameRoom.secondPlayer.LastMask = inputState.Mask
			gameRoom.secondPlayer.LastSeqN = inputState.SeqN

			gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y = stepFromStateDLL(
				gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y, gameRoom.secondPlayer.LastMask, deltaTime)

			processedFramesSecondPlayer++
			if processedFramesSecondPlayer >= maxCatchupPerTick {
				break drainSecondPlayerInputs
			}
		default:
			break drainSecondPlayerInputs
		}
	}

	if processedFramesSecondPlayer == 0 {
		gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y = stepFromStateDLL(
			gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y, gameRoom.secondPlayer.LastMask, deltaTime)
	}

	// =======================
	// BROADCAST STATO
	// =======================
	gameRoom.sendAuthStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.firstPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.secondPlayer)

	gameRoom.sendAuthStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.secondPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.firstPlayer)

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

func (gameRoom *GameRoom) SendJoinAcknowledgements() {
	// P1 JoinAck
	packetP1 := make([]byte, 21)
	packetP1[0] = MsgJoinAck
	binary.LittleEndian.PutUint32(packetP1[1:5], gameRoom.firstPlayer.ID)
	binary.LittleEndian.PutUint32(packetP1[5:9], math.Float32bits(gameRoom.firstPlayer.X))
	binary.LittleEndian.PutUint32(packetP1[9:13], math.Float32bits(gameRoom.firstPlayer.Y))
	binary.LittleEndian.PutUint32(packetP1[13:17], math.Float32bits(gameRoom.secondPlayer.X))
	binary.LittleEndian.PutUint32(packetP1[17:21], math.Float32bits(gameRoom.secondPlayer.Y))
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packetP1, true)

	// P2 JoinAck
	packetP2 := make([]byte, 21)
	packetP2[0] = MsgJoinAck
	binary.LittleEndian.PutUint32(packetP2[1:5], gameRoom.secondPlayer.ID)
	binary.LittleEndian.PutUint32(packetP2[5:9], math.Float32bits(gameRoom.secondPlayer.X))
	binary.LittleEndian.PutUint32(packetP2[9:13], math.Float32bits(gameRoom.secondPlayer.Y))
	binary.LittleEndian.PutUint32(packetP2[13:17], math.Float32bits(gameRoom.firstPlayer.X))
	binary.LittleEndian.PutUint32(packetP2[17:21], math.Float32bits(gameRoom.firstPlayer.Y))
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packetP2, true)
}

// Parità old_main: genera N uova subito e le invia a entrambi
func (gameRoom *GameRoom) CreateInitialEggs(eggCount int) {
	for eggId := 0; eggId < eggCount; eggId++ {
		eggX := float32(250 + rand.Intn(250)) // 250..499
		eggY := float32(rand.Intn(400))       // 0..399
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

	// Parità old_main: rounding su AuthState self
	roundedX := float32(math.Round(float64(player.X)))
	roundedY := float32(math.Round(float64(player.Y)))

	binary.LittleEndian.PutUint32(packet[1:5], player.LastSeqN)
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(roundedX))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(roundedY))

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
	// Parità old_main: clamp charge
	charge := shot.Charge
	if charge < 0 {
		charge = 0
	}
	if charge > chargeCap {
		charge = chargeCap
	}

	packet := make([]byte, 13)
	packet[0] = MsgRemoteShot
	binary.LittleEndian.PutUint32(packet[1:5], uint32(shot.X))
	binary.LittleEndian.PutUint32(packet[5:9], uint32(shot.Y))
	binary.LittleEndian.PutUint32(packet[9:13], uint32(charge))

	targetConn.TryEnqueueOutgoingPacket(packet, false)
}
