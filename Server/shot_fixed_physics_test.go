package main

import (
	"math"
	"testing"
)

// I parametri di gioco devono restare coerenti a prescindere dalla posizione.
func TestGameShotGravityAndMaxSpeedAreFixed(t *testing.T) {
	positions := []Position{{X: 100, Y: 300}, {X: 550, Y: 25}, {X: 314, Y: 74}}
	target := Position{X: 700, Y: 120}
	for _, shotType := range []ShotType{ShotTypeLeft, ShotTypeRight} {
		for _, pos := range positions {
			launch := buildShotParameters(pos, target, 1.5, shotType)
			if launch.gravity != 3800 {
				t.Fatalf("position %+v type %d: unexpected gravity %f", pos, shotType, launch.gravity)
			}
			speed := math.Sqrt(float64(launch.vx*launch.vx + launch.vy*launch.vy + launch.vz*launch.vz))
			if math.Abs(speed-1900) > 0.001 {
				t.Fatalf("position %+v type %d: max speed %.6f != 1900", pos, shotType, speed)
			}
			duration := nativeShotFlightDuration(launch.vz, launch.gravity)
			expected := float32(0.5)
			if shotType == ShotTypeRight {
				expected = float32(math.Sqrt(3) / 2)
			}
			if math.Abs(float64(duration-expected)) > 0.00001 {
				t.Fatalf("position %+v type %d: flight duration %f != %f", pos, shotType, duration, expected)
			}
		}
	}
}

// Il calcolo nativo non conosce né la mano né le regole del gioco:
// origine, velocita', angolo e gravita' sono tutti dati forniti dal chiamante.
func TestNativeShotParametersAcceptArbitraryPhysicalInputs(t *testing.T) {
	origin := Position{X: 19, Y: -6}
	direction := Position{X: 0, Y: 7}
	shot := nativeComputeShotParameters(origin, direction, 100, 0, 700)
	if shot.startX != origin.X || shot.startY != origin.Y {
		t.Fatalf("native code modified origin: got (%v, %v)", shot.startX, shot.startY)
	}
	if math.Abs(float64(shot.vx)) > 0.0001 ||
		math.Abs(float64(shot.vy)-100) > 0.0001 ||
		math.Abs(float64(shot.vz)) > 0.0001 || shot.gravity != 700 {
		t.Fatalf("native shot parameters mismatch: %+v", shot)
	}

	vertical := nativeComputeShotParameters(origin, Position{X: 1}, 100, 90, 12)
	if math.Abs(float64(vertical.vz)-100) > 0.0001 || vertical.gravity != 12 {
		t.Fatalf("native shot did not respect caller's angle/gravity: %+v", vertical)
	}
}

// Coordinate di riferimento della traiettoria C# prima del refactoring.
// Previene divergenze tra la geometria del tiro in Go e quella in C#.
func TestGoShotGameplayMatchesClientReferenceTrajectories(t *testing.T) {
	cases := []struct {
		name                                  string
		player, target                        Position
		charge                                float32
		shotType                              ShotType
		tick                                  float32
		screenX, screenY, shadowX, shadowY, z float32
	}{
		{"left-250", Position{X: 100, Y: 300}, Position{X: 700, Y: 120}, 0.250, ShotTypeLeft, 1, 188.635880, 299.638214, 188.635880, 303.424072, 10.816751},
		{"right-1500", Position{X: 100, Y: 300}, Position{X: 700, Y: 120}, 1.500, ShotTypeRight, 3, 253.254425, 229.304993, 253.254425, 280.245697, 145.544846},
		{"right-700", Position{X: 550, Y: 25}, Position{X: 120, Y: 430}, 0.700, ShotTypeRight, 5, 510.159302, 67.020203, 510.159302, 114.117867, 134.564743},
		{"right-250", Position{X: 314, Y: 74}, Position{X: 400, Y: 240}, 0.250, ShotTypeRight, 3, 374.331818, 123.167892, 374.331818, 140.029190, 48.175148},
		{"left-1500", Position{X: 314, Y: 74}, Position{X: 400, Y: 240}, 1.500, ShotTypeLeft, 1, 377.173584, 140.752686, 377.173584, 151.097137, 29.555557},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			shot := buildShotParameters(tc.player, tc.target, tc.charge, tc.shotType)
			x, y, sx, sy, z := nativeSampleShot(shot.startX, shot.startY,
				shot.vx, shot.vy, shot.vz, shot.gravity, tc.tick/30)
			got := []float32{x, y, sx, sy, z}
			want := []float32{tc.screenX, tc.screenY, tc.shadowX, tc.shadowY, tc.z}
			for i := range got {
				if math.Abs(float64(got[i]-want[i])) > 0.001 {
					t.Fatalf("C# trajectory differs, index %d got %.6f want %.6f", i, got[i], want[i])
				}
			}
		})
	}
}
