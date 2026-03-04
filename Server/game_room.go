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
	// Contatore tick (solo per debug/log).
	tickNo uint64
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
	// JoinAck (blocca il client se manca)
	gameRoom.SendJoinAcknowledgements()

	// Parità col vecchio old_main.go: 5 uova subito
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

// Tick (server authoritative, tick-based):
// - drena gli input arrivati (anche in burst) e tiene SOLO l'ultimo (latest-wins)
// - fa SEMPRE 1 step fisico per tick usando l'ultima mask ricevuta
// - se non arriva input da troppo, stoppa il player (anti "ghiaccio")
func (gameRoom *GameRoom) Tick() {
	gameRoom.tickNo++
	deltaTime := float32(1.0 / float32(gameRoom.tickRateHz))
	now := time.Now()
	staleTimeout := time.Duration(inputStaleTimeoutMs) * time.Millisecond

	// helper: drena fino a maxDrainInputsPerTick e ritorna l'ultimo letto + quanti drenati
	drainLatest := func(ch <-chan InputState) (InputState, bool, int) {
		var last InputState
		has := false
		count := 0
		for n := 0; n < maxDrainInputsPerTick; n++ {
			select {
			case s := <-ch:
				last = s
				has = true
				count++
			default:
				return last, has, count
			}
		}
		return last, has, count
	}

	// =======================
	// PLAYER 1: latest-wins + 1 step per tick
	// =======================
	in1, ok1, drain1 := drainLatest(gameRoom.firstPlayerConnection.InputChannel)
	if ok1 {
		gameRoom.firstPlayer.RecvMask = in1.Mask
		gameRoom.firstPlayer.RecvSeqN = in1.SeqN
		gameRoom.firstPlayer.LastRx = now
	}
	mask1 := gameRoom.firstPlayer.RecvMask
	if gameRoom.firstPlayer.LastRx.IsZero() || now.Sub(gameRoom.firstPlayer.LastRx) > staleTimeout {
		mask1 = 0
	}
	gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y = stepFromStateDLL(
		gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y, mask1, deltaTime)
	gameRoom.firstPlayer.LastMask = mask1
	gameRoom.firstPlayer.LastSeqN = gameRoom.firstPlayer.RecvSeqN

	// =======================
	// PLAYER 2: latest-wins + 1 step per tick
	// =======================
	in2, ok2, drain2 := drainLatest(gameRoom.secondPlayerConnection.InputChannel)
	if ok2 {
		gameRoom.secondPlayer.RecvMask = in2.Mask
		gameRoom.secondPlayer.RecvSeqN = in2.SeqN
		gameRoom.secondPlayer.LastRx = now
	}
	mask2 := gameRoom.secondPlayer.RecvMask
	if gameRoom.secondPlayer.LastRx.IsZero() || now.Sub(gameRoom.secondPlayer.LastRx) > staleTimeout {
		mask2 = 0
	}
	gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y = stepFromStateDLL(
		gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y, mask2, deltaTime)
	gameRoom.secondPlayer.LastMask = mask2
	gameRoom.secondPlayer.LastSeqN = gameRoom.secondPlayer.RecvSeqN

	// =======================
	// BROADCAST STATO
	// =======================
	gameRoom.sendAuthStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.firstPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.secondPlayer)

	gameRoom.sendAuthStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.secondPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.firstPlayer)

	// =======================
	// DEBUG LOG (1 volta al secondo)
	// =======================
	if gameRoom.tickNo%uint64(gameRoom.tickRateHz) == 0 {
		age1 := int64(-1)
		if !gameRoom.firstPlayer.LastRx.IsZero() {
			age1 = now.Sub(gameRoom.firstPlayer.LastRx).Milliseconds()
		}
		age2 := int64(-1)
		if !gameRoom.secondPlayer.LastRx.IsZero() {
			age2 = now.Sub(gameRoom.secondPlayer.LastRx).Milliseconds()
		}

		log.Printf("[TICK] P1 drain=%d ok=%v ageMs=%d recvSeq=%d usedMask=%d pos=(%.0f,%.0f) | P2 drain=%d ok=%v ageMs=%d recvSeq=%d usedMask=%d pos=(%.0f,%.0f)",
			drain1, ok1, age1, gameRoom.firstPlayer.RecvSeqN, gameRoom.firstPlayer.LastMask, gameRoom.firstPlayer.X, gameRoom.firstPlayer.Y,
			drain2, ok2, age2, gameRoom.secondPlayer.RecvSeqN, gameRoom.secondPlayer.LastMask, gameRoom.secondPlayer.X, gameRoom.secondPlayer.Y,
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
	// clamp charge
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
