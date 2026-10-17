package main

import (
	"encoding/binary"
	"math"
	"testing"
)

// checkShotResolution controlla tutti i campi dell'esito ricevuto.
func checkShotResolution(t *testing.T, packet []byte, owner, clientTick uint32, outcome ShotOutcome, victim, serverTick uint32) {
	t.Helper()
	if len(packet) != 34 || packet[0] != MsgShotResolved {
		t.Fatalf("invalid resolution packet (len=%d bytes=%v)", len(packet), packet)
	}
	if v := binary.LittleEndian.Uint32(packet[1:5]); v != owner {
		t.Fatalf("owner=%d want %d", v, owner)
	}
	if v := binary.LittleEndian.Uint32(packet[5:9]); v != clientTick {
		t.Fatalf("shot tick=%d want %d", v, clientTick)
	}
	if ShotOutcome(packet[9]) != outcome {
		t.Fatalf("outcome=%d want %d", packet[9], outcome)
	}
	if v := binary.LittleEndian.Uint32(packet[10:14]); v != victim {
		t.Fatalf("victim=%d want %d", v, victim)
	}
	if v := binary.LittleEndian.Uint32(packet[14:18]); v != serverTick {
		t.Fatalf("server tick=%d want %d", v, serverTick)
	}
	if outcome != ShotOutcomePenguin && binary.LittleEndian.Uint32(packet[18:22]) != 0 {
		t.Fatal("non-hit outcome contains a victim ACK")
	}
}

// assertNoResolution verifica che non siano partiti altri esiti.
func assertNoResolution(t *testing.T, room *GameRoom) {
	t.Helper()
	if n := len(room.firstPlayerConnection.OutgoingPacketChannel); n != 0 {
		t.Fatalf("unexpected player A packet count=%d", n)
	}
	if n := len(room.secondPlayerConnection.OutgoingPacketChannel); n != 0 {
		t.Fatalf("unexpected player B packet count=%d", n)
	}
}

// Prova tutti gli esiti e verifica che arrivino una sola volta.
func TestShotResolutionEveryTerminalOutcomeBroadcastOnce(t *testing.T) {
	cases := []struct {
		name    string
		outcome ShotOutcome
		victim  uint32
	}{
		{"expired", ShotOutcomeExpired, 0},
		{"obstacle", ShotOutcomeObstacle, 0},
		{"penguin", ShotOutcomePenguin, 2},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			room := syncTestRoom()
			room.matchTick = 123
			shot := Shot{ShotID: 17, OwnerPlayerID: 1, ClientShotTick: 33, IsAlive: true}
			room.collisionManager.AddObject(shotCollisionTag(shot.ShotID), 0, 0, shotColliderWidth, shotColliderHeight)

			room.resolveShot(&shot, c.outcome, c.victim)
			if shot.IsAlive {
				t.Fatal("resolved shot is still alive")
			}
			for _, conn := range []*PlayerConnection{room.firstPlayerConnection, room.secondPlayerConnection} {
				if n := len(conn.OutgoingPacketChannel); n != 1 {
					t.Fatalf("resolution count=%d, want 1", n)
				}
				checkShotResolution(t, <-conn.OutgoingPacketChannel, 1, 33, c.outcome, c.victim, 123)
			}
			// Duplicate C++ collision pairs / subsequent ticks must not emit another resolution.
			room.resolveShot(&shot, c.outcome, c.victim)
			assertNoResolution(t, room)
		})
	}
}

// Verifica l'esito quando una palla termina il volo.
func TestExpiredShotEmitsResolutionFromPhysicsTick(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 20
	room.activeShots = append(room.activeShots, Shot{
		ShotID: 2, OwnerPlayerID: 2, ClientShotTick: 88,
		SpawnTick: 1, FlightDuration: 0.2, IsAlive: true,
	})
	room.updateShotColliders()
	if room.activeShots[0].IsAlive {
		t.Fatal("expired ball was not resolved")
	}
	checkShotResolution(t, <-room.firstPlayerConnection.OutgoingPacketChannel, 2, 88, ShotOutcomeExpired, 0, 20)
	checkShotResolution(t, <-room.secondPlayerConnection.OutgoingPacketChannel, 2, 88, ShotOutcomeExpired, 0, 20)
	room.updateShotColliders()
	assertNoResolution(t, room)
}

// Verifica la vittima corretta e ignora collisioni duplicate.
func TestCollisionOutcomeChoosesPlayerAndIgnoresLaterPairs(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 40
	room.activeShots = append(room.activeShots, Shot{ShotID: 9, OwnerPlayerID: 1, ClientShotTick: 14, IsAlive: true})
	room.collisionManager.collisionRecordOuts = []CollisionRecordOut{
		NewCollisionRecordOut("s9", "penguin1", 1), // self-hit is ignored
		NewCollisionRecordOut("s9", "penguin2", 1), // real hit
		NewCollisionRecordOut("s9", "o0", 1),       // duplicate pair after destruction
	}
	room.resolveCollisionResults()
	if room.activeShots[0].IsAlive {
		t.Fatal("penguin hit did not end the ball")
	}
	checkShotResolution(t, <-room.firstPlayerConnection.OutgoingPacketChannel, 1, 14, ShotOutcomePenguin, 2, 40)
	checkShotResolution(t, <-room.secondPlayerConnection.OutgoingPacketChannel, 1, 14, ShotOutcomePenguin, 2, 40)
	assertNoResolution(t, room)
}

// Verifica l'esito di una palla che urta un ostacolo.
func TestObstacleCollisionResolvesByShotKey(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 66
	room.activeShots = append(room.activeShots, Shot{ShotID: 8, OwnerPlayerID: 2, ClientShotTick: 33, IsAlive: true})
	room.collisionManager.collisionRecordOuts = []CollisionRecordOut{
		NewCollisionRecordOut("o0", "s8", 1),
	}
	room.resolveCollisionResults()
	checkShotResolution(t, <-room.firstPlayerConnection.OutgoingPacketChannel, 2, 33, ShotOutcomeObstacle, 0, 66)
	checkShotResolution(t, <-room.secondPlayerConnection.OutgoingPacketChannel, 2, 33, ShotOutcomeObstacle, 0, 66)
}

// Un HIT blocca gli input 41..130, ma il 131 torna a muovere il pinguino.
func TestAuthoritativeFreezeBlocksNinetyInputSequences(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 200
	victim := &room.secondPlayer
	victim.LastSeqN = 40
	startX, startY := victim.X, victim.Y
	shot := Shot{ShotID: 23, OwnerPlayerID: 1, ClientShotTick: 11, IsAlive: true}
	room.resolveShot(&shot, ShotOutcomePenguin, 2)
	if victim.FreezeEndSeq != 130 {
		t.Fatalf("freeze end=%d, want 130", victim.FreezeEndSeq)
	}
	packet := <-room.secondPlayerConnection.OutgoingPacketChannel
	checkShotResolution(t, packet, 1, 11, ShotOutcomePenguin, 2, 200)
	if ack := binary.LittleEndian.Uint32(packet[18:22]); ack != 40 {
		t.Fatalf("hit ACK=%d, want 40", ack)
	}
	if x := math.Float32frombits(binary.LittleEndian.Uint32(packet[22:26])); x != startX {
		t.Fatalf("hit X=%f, want %f", x, startX)
	}
	if y := math.Float32frombits(binary.LittleEndian.Uint32(packet[26:30])); y != startY {
		t.Fatalf("hit Y=%f, want %f", y, startY)
	}
	for seq := uint32(41); seq <= 130; seq++ {
		room.secondPlayerPendingEvents = append(room.secondPlayerPendingEvents, syncInput(seq, Right))
	}
	room.applyMaturePendingEvents(victim, &room.secondPlayerPendingEvents, room.firstPlayerConnection, MoveDt)
	if victim.LastSeqN != 130 || victim.X != startX || victim.Y != startY || victim.LastMask&Freezing == 0 {
		t.Fatalf("frozen player moved: %+v", victim)
	}
	room.secondPlayerPendingEvents = append(room.secondPlayerPendingEvents, syncInput(131, Right|Freezing))
	room.applyMaturePendingEvents(victim, &room.secondPlayerPendingEvents, room.firstPlayerConnection, MoveDt)
	if victim.LastSeqN != 131 || victim.LastMask&Freezing != 0 || victim.X <= startX {
		t.Fatalf("player did not move at 131: %+v", victim)
	}
}

// Verifica che un secondo HIT estenda il blocco senza abbreviare il precedente.
func TestRepeatedHitExtendsFreezeDeadline(t *testing.T) {
	room := syncTestRoom()
	room.secondPlayer.LastSeqN = 40
	first := Shot{ShotID: 1, OwnerPlayerID: 1, ClientShotTick: 1, IsAlive: true}
	room.resolveShot(&first, ShotOutcomePenguin, 2)
	if room.secondPlayer.FreezeEndSeq != 130 {
		t.Fatal("first freeze deadline mismatch")
	}
	room.secondPlayer.LastSeqN = 70
	second := Shot{ShotID: 2, OwnerPlayerID: 1, ClientShotTick: 2, IsAlive: true}
	room.resolveShot(&second, ShotOutcomePenguin, 2)
	if room.secondPlayer.FreezeEndSeq != 160 {
		t.Fatal("second hit did not extend freeze")
	}
}

// Verifica che una palla non colpisca il suo tiratore.
func TestSelfCollisionDoesNotResolveShot(t *testing.T) {
	room := syncTestRoom()
	room.activeShots = append(room.activeShots, Shot{ShotID: 5, OwnerPlayerID: 2, ClientShotTick: 15, IsAlive: true})
	room.collisionManager.collisionRecordOuts = []CollisionRecordOut{
		NewCollisionRecordOut("penguin2", "s5", 1),
	}
	room.resolveCollisionResults()
	if !room.activeShots[0].IsAlive {
		t.Fatal("shot killed on self-collision")
	}
	assertNoResolution(t, room)
}
