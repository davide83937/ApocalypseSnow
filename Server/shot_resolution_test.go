package main

import (
	"encoding/binary"
	"testing"
)

// checkShotResolution controlla tutti i campi dell'esito ricevuto.
func checkShotResolution(t *testing.T, packet []byte, owner, clientTick uint32, outcome ShotOutcome, victim, serverTick uint32) {
	t.Helper()
	if len(packet) != 18 || packet[0] != MsgShotResolved {
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
