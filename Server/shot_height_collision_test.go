package main

import "testing"

// La DLL deve rilevare solo le intersezioni XY, senza filtrare per quota.
func TestNativeXYCollisionIgnoresShotHeight(t *testing.T) {
	cases := []float32{0, 29.9, 40, 59.9, 60, 65, 70, 89.9, 90, 90.1}
	for _, z := range cases {
		cm := NewCollisionManager()
		cm.AddObject("s1", 100, 100, 20, 25, z, z)
		cm.AddObject("penguin2", 100, 100, 96, 64, 0, penguinCollisionHeight)
		cm.AddObject("o0", 100, 100, 185, 100, 0, obstacleCollisionHeight)
		cm.SendToCpp()
		p, o := false, false
		for _, c := range cm.Results() {
			if c.Type <= 0 || (c.MyTag != "s1" && c.OtherTag != "s1") {
				continue
			}
			if c.MyTag == "penguin2" || c.OtherTag == "penguin2" {
				p = true
			}
			if c.MyTag == "o0" || c.OtherTag == "o0" {
				o = true
			}
		}
		if !p || !o {
			t.Errorf("z=%v: native XY collision with penguin=%t obstacle=%t, want both true", z, p, o)
		}
	}
}

// Il server riceve eventi XY e verifica Z prima di emettere un esito autoritativo.
func TestShotHeightFiltersAuthoritativeCollision(t *testing.T) {
	cases := []struct {
		name    string
		z       float32
		target  string
		outcome ShotOutcome
	}{
		{"penguin at z20", 20, "penguin2", ShotOutcomePenguin},
		{"penguin at z40", 40, "penguin2", ShotOutcomePenguin},
		{"penguin missed at z70", 70, "penguin2", 0},
		{"obstacle hit at z55", 55, "o0", ShotOutcomeObstacle},
		{"obstacle hit at z65", 65, "o0", ShotOutcomeObstacle},
		{"obstacle hit at z89", 89, "o0", ShotOutcomeObstacle},
		{"obstacle cleared at z90", 90, "o0", 0},
		{"obstacle hit at z20", 20, "o0", ShotOutcomeObstacle},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			room := syncTestRoom()
			room.matchTick = 40
			room.activeShots = []Shot{{
				ShotID: 9, OwnerPlayerID: 1,
				ClientShotTick: 14, IsAlive: true, CurrentHeight: tc.z,
			}}
			room.collisionManager = NewCollisionManager()
			room.collisionManager.AddObject("s9", 100, 100, 20, 25, tc.z, tc.z)
			// Il server decide l'esito con lo stesso limite usato nella partita.
			zMax := obstacleCollisionHeight
			if tc.target == "penguin2" {
				zMax = penguinCollisionHeight
			}
			room.collisionManager.AddObject(tc.target, 100, 100, 96, 64, 0, zMax)
			room.collisionManager.SendToCpp()
			room.resolveCollisionResults()
			if tc.outcome == 0 {
				if !room.activeShots[0].IsAlive {
					t.Fatal("shot stopped despite being above the target")
				}
				assertNoResolution(t, room)
			} else {
				if room.activeShots[0].IsAlive {
					t.Fatal("valid shot collision was ignored")
				}
				victim := uint32(0)
				if tc.outcome == ShotOutcomePenguin {
					victim = 2
				}
				checkShotResolution(t, <-room.firstPlayerConnection.OutgoingPacketChannel,
					1, 14, tc.outcome, victim, 40)
				checkShotResolution(t, <-room.secondPlayerConnection.OutgoingPacketChannel,
					1, 14, tc.outcome, victim, 40)
			}
		})
	}
}

// Test integrato: la collisione sul piano XY arriva dalla DLL C++,
// il filtro Z deve scegliere il bersaglio corretto nello stesso tick.
func TestNativeXYCollisionCombinedWithShotHeight(t *testing.T) {
	cases := []struct {
		z    float32
		want ShotOutcome
	}{
		{20, ShotOutcomePenguin},  // Entrambi sovrapposti: colpisce il pinguino.
		{55, ShotOutcomePenguin},  // Colpisce ancora il pinguino e anche il muro.
		{65, ShotOutcomeObstacle}, // Supera il pinguino, ma non il muro.
		{89.9, ShotOutcomeObstacle},
		{90, 0}, // Supera sia il pinguino sia il muro.
	}
	for _, tc := range cases {
		room := syncTestRoom()
		room.matchTick = 60
		room.collisionManager = NewCollisionManager()
		room.activeShots = []Shot{{
			ShotID: 9, OwnerPlayerID: 1, ClientShotTick: 7,
			IsAlive: true, CurrentHeight: tc.z,
		}}
		room.collisionManager.AddObject("penguin2", 100, 100, 96, 64, 0, penguinCollisionHeight)
		room.collisionManager.AddObject("o0", 100, 100, 100, 80, 0, obstacleCollisionHeight)
		room.collisionManager.AddObject("s9", 100, 100, shotColliderWidth, shotColliderHeight, tc.z, tc.z)
		room.collisionManager.SendToCpp()
		room.resolveCollisionResults()
		if tc.want == 0 {
			if !room.activeShots[0].IsAlive {
				t.Fatalf("z=%v: shot should pass above both targets", tc.z)
			}
			assertNoResolution(t, room)
			continue
		}
		if room.activeShots[0].IsAlive {
			t.Fatalf("z=%v: expected outcome=%d", tc.z, tc.want)
		}
		victim := uint32(0)
		if tc.want == ShotOutcomePenguin {
			victim = 2
		}
		checkShotResolution(t, <-room.firstPlayerConnection.OutgoingPacketChannel,
			1, 7, tc.want, victim, 60)
		checkShotResolution(t, <-room.secondPlayerConnection.OutgoingPacketChannel,
			1, 7, tc.want, victim, 60)
	}
}

// Verifica i collider della partita vera: non basta che i test costruiscano
// un muro alto 90 se NewGameRoom ne registra ancora uno alto 60.
func TestGameRoomRegistersDifferentCollisionHeights(t *testing.T) {
	room := syncTestRoom()
	foundPenguins, foundObstacles := 0, 0
	for _, record := range room.collisionManager.collisionRecordIns {
		if isPenguinTag(record.Tag) {
			foundPenguins++
			if record.MinZ != 0 || record.MaxZ != penguinCollisionHeight {
				t.Errorf("penguin %s: Z=[%v,%v], want [0,%v]", record.Tag, record.MinZ, record.MaxZ, penguinCollisionHeight)
			}
		}
		if isObstacleTag(record.Tag) {
			foundObstacles++
			if record.MinZ != 0 || record.MaxZ != obstacleCollisionHeight {
				t.Errorf("obstacle %s: Z=[%v,%v], want [0,%v]", record.Tag, record.MinZ, record.MaxZ, obstacleCollisionHeight)
			}
		}
	}
	if foundPenguins != 2 || foundObstacles != len(room.Obstacles) {
		t.Errorf("registered %d penguins and %d obstacles, want 2 and %d", foundPenguins, foundObstacles, len(room.Obstacles))
	}
}
