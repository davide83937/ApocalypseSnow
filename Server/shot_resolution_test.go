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

// Il server usa 90 tick DI SERVER, mai le sequenze locali di B.
func TestAuthoritativeFreezeBlocksNinetyServerTicks(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 200
	victim := &room.secondPlayer
	victim.LastSeqN = 40
	startX, startY := victim.X, victim.Y
	shot := Shot{ShotID: 23, OwnerPlayerID: 1, ClientShotTick: 11, IsAlive: true}
	room.resolveShot(&shot, ShotOutcomePenguin, 2)
	if victim.FreezeStartTick != 200 || victim.FreezeEndTick != 290 {
		t.Fatalf("freeze interval=[%d,%d) want [200,290)", victim.FreezeStartTick, victim.FreezeEndTick)
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

	// Il freeze dura 90 tick server, indipendentemente da LastSeqN.
	for tick := uint32(200); tick < 290; tick++ {
		room.matchTick = tick
		if !room.isServerFrozen(victim) {
			t.Fatalf("server unfroze prematurely at tick %d", tick)
		}
	}
	room.matchTick = 289
	for seq := uint32(41); seq <= 130; seq++ {
		room.secondPlayerPendingEvents = append(room.secondPlayerPendingEvents, syncInput(seq, Right))
	}
	room.applyMaturePendingEvents(victim, &room.secondPlayerPendingEvents, room.firstPlayerConnection, MoveDt)
	if victim.LastSeqN != 130 || victim.X != startX || victim.Y != startY || victim.LastMask&Freezing == 0 {
		t.Fatalf("frozen player moved: %+v", victim)
	}
	room.matchTick = 290
	if room.isServerFrozen(victim) {
		t.Fatal("server still frozen at tick 290")
	}
	room.secondPlayerPendingEvents = append(room.secondPlayerPendingEvents, syncInput(131, Right|Freezing))
	room.applyMaturePendingEvents(victim, &room.secondPlayerPendingEvents, room.firstPlayerConnection, MoveDt)
	if victim.LastSeqN != 131 || victim.LastMask&Freezing != 0 || victim.X <= startX {
		t.Fatalf("player did not move after expiry: %+v", victim)
	}
}

// Due HIT sono intervalli sovrapposti sulla timeline del server.
func TestRepeatedHitExtendsFreezeDeadline(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 200
	room.secondPlayer.LastSeqN = 40
	first := Shot{ShotID: 1, OwnerPlayerID: 1, ClientShotTick: 1, IsAlive: true}
	room.resolveShot(&first, ShotOutcomePenguin, 2)
	if room.secondPlayer.FreezeEndTick != 290 {
		t.Fatal("first freeze deadline mismatch")
	}
	room.matchTick = 240
	room.secondPlayer.LastSeqN = 70
	second := Shot{ShotID: 2, OwnerPlayerID: 1, ClientShotTick: 2, IsAlive: true}
	room.resolveShot(&second, ShotOutcomePenguin, 2)
	if room.secondPlayer.FreezeEndTick != 330 {
		t.Fatal("second hit did not extend freeze on server timeline")
	}
	room.matchTick = 329
	if !room.isServerFrozen(&room.secondPlayer) {
		t.Fatal("server ended overlapping freezes too soon")
	}
	room.matchTick = 330
	if room.isServerFrozen(&room.secondPlayer) {
		t.Fatal("server froze after deadline")
	}
}

// Dieci HIT ravvicinati non si sommano: scadenza = ultimo impatto + 90 tick.
func TestTenHitsUseLastServerDeadline(t *testing.T) {
	room := syncTestRoom()
	victim := &room.secondPlayer
	for i := uint32(0); i < 10; i++ {
		room.matchTick = 200 + 10*i
		victim.LastSeqN = 40 + 10*i
		shot := Shot{ShotID: 100 + i, OwnerPlayerID: 1,
			ClientShotTick: 500 + i, IsAlive: true}
		room.resolveShot(&shot, ShotOutcomePenguin, 2)
		if victim.FreezeEndTick != room.matchTick+90 {
			t.Fatalf("hit %d: freeze ends at %d, want %d",
				i, victim.FreezeEndTick, room.matchTick+90)
		}
	}
	room.matchTick = 379
	if !room.isServerFrozen(victim) {
		t.Fatal("unfroze before last hit deadline")
	}
	room.matchTick = 380
	if room.isServerFrozen(victim) {
		t.Fatal("freeze stacked past final deadline")
	}
	if len(room.firstPlayerConnection.OutgoingPacketChannel) != 10 ||
		len(room.secondPlayerConnection.OutgoingPacketChannel) != 10 {
		t.Fatal("each confirmed hit must be delivered once to both players")
	}
}

// Misura la durata calcolata dalla fisica REALE di Go, a parita' di carica
// e tipologia di tiro rispetto al client A. L'output e' comparato da
// Tests/compare_shot_flight.ps1 con la misura indipendente in C#.
func TestShotFlightComparisonFixtures(t *testing.T) {
	room := syncTestRoom()
	for _, chargeMs := range []uint32{50, 100, 250, 500, 750, 1000, 1250, 1500} {
		for _, shotType := range []ShotType{ShotTypeLeft, ShotTypeRight} {
			name := "left"
			if shotType == ShotTypeRight {
				name = "right"
			}
			event := ShotEvent{Position: Position{X: 300, Y: 200}}
			_, _, _, _, speedZ, gravity, duration := room.buildServerShotRuntime(
				&room.firstPlayer, event, shotType, chargeMs)
			if duration <= 0 || math.Abs(float64(duration-2*speedZ/gravity)) > 0.00001 {
				t.Fatalf("bad runtime physics chargeMs=%d shotType=%d duration=%f", chargeMs, shotType, duration)
			}
			ticks := shotLandingTicks(duration, room.tickRateHz)
			t.Logf("FLIGHT_GO|%s-%d|%.9f|%d", name, chargeMs, duration, ticks)
		}
	}
}

// Confronto punto-per-punto della stessa traiettoria fisica C# / Go.
// Go esegue buildServerShotRuntime + computeShotScreenAndHeight (produzione).
func TestShotTrajectoryParityFixtures(t *testing.T) {
	room := syncTestRoom()
	if gameWorldWidth != 800 || gameWorldHeight != 480 ||
		shotColliderWidth != 20 || shotColliderHeight != 25 {
		t.Fatalf("incorrect shared geometry: world %.0fx%.0f ball %dx%d",
			gameWorldWidth, gameWorldHeight, shotColliderWidth, shotColliderHeight)
	}
	origins := []Position{{X: 100, Y: 300}, {X: 550, Y: 25}, {X: 314, Y: 74}}
	targets := []Position{{X: 700, Y: 120}, {X: 120, Y: 430}, {X: 400, Y: 240}}
	for i, origin := range origins {
		room.firstPlayer.Position = origin
		for _, charge := range []uint32{250, 700, 1500} {
			for _, kind := range []ShotType{ShotTypeLeft, ShotTypeRight} {
				name := "left"
				if kind == ShotTypeRight {
					name = "right"
				}
				event := ShotEvent{Position: targets[i]}
				sx, sy, gx, gy, vz, g, duration := room.buildServerShotRuntime(
					&room.firstPlayer, event, kind, charge)
				shot := Shot{ShotID: 7, OwnerPlayerID: 1, ClientShotTick: 11,
					StartX: sx, StartY: sy, GroundVelX: gx, GroundVelY: gy,
					StartVerticalSpeed: vz, Gravity: g, FlightDuration: duration,
					SpawnTick: 100, IsAlive: true}
				landing := shotLandingTicks(duration, room.tickRateHz)
				for _, tick := range []uint32{1, 3, 5, 7, 10, 15, 22} {
					if tick >= landing {
						continue
					}
					room.matchTick = 100 + tick
					x, y, shx, shy, z, expired := room.computeShotScreenAndHeight(&shot)
					if expired {
						t.Fatalf("unexpected expiry at tick %d", tick)
					}
					t.Logf("PATH_GO|p%d-%s-%d-%d|%.6f|%.6f|%.6f|%.6f|%.6f",
						i, name, charge, tick, x, y, shx, shy, z)
				}
			}
		}
	}
}

// Il caso 500ms/destra divergeva di un tick per floating-point:
// C# 0.50000000s -> 15 tick; Go 0.50000006s -> 16 tick.
func TestRightShot500msExpiresAtSameFifteenthTickAsClient(t *testing.T) {
	room := syncTestRoom()
	_, _, _, _, _, _, duration := room.buildServerShotRuntime(
		&room.firstPlayer, ShotEvent{Position: Position{X: 300, Y: 200}}, ShotTypeRight, 500)
	if shotLandingTicks(duration, room.tickRateHz) != 15 {
		t.Fatalf("landing ticks=%d, want 15", shotLandingTicks(duration, room.tickRateHz))
	}
	room.activeShots = append(room.activeShots, Shot{
		ShotID: 44, OwnerPlayerID: 1, ClientShotTick: 42,
		SpawnTick: 100, FlightDuration: duration, IsAlive: true,
	})
	room.collisionManager.AddObject("s44", 0, 0, shotColliderWidth, shotColliderHeight)
	room.matchTick = 114
	room.updateShotColliders()
	if !room.activeShots[0].IsAlive {
		t.Fatal("shot expired before 15 ticks")
	}
	room.matchTick = 115
	room.updateShotColliders()
	if room.activeShots[0].IsAlive {
		t.Fatal("shot still alive after exactly 15 server ticks")
	}
	packet := <-room.firstPlayerConnection.OutgoingPacketChannel
	checkShotResolution(t, packet, 1, 42, ShotOutcomeExpired, 0, 115)
	if got := binary.LittleEndian.Uint32(packet[30:34]); got != 15 {
		t.Fatalf("flight ticks=%d, want 15", got)
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
