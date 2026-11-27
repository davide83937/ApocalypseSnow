package main

import "math"

// Geometria e bilanciamento di ApocalypseSnow, non della libreria fisica.
// Da mantenere coerenti con ApocalypseSnow/ShotGameplay.cs.
// I fixture di traiettoria C# e Go controllano che restino equivalenti.
const (
	shotHandOffsetX          float32 = 48
	shotHandOffsetY          float32 = 18
	shotBallSpawnDistance    float32 = 22
	shotPlanePerspectiveY    float32 = 0.70
	shotMaxChargeSeconds     float32 = float32(chargeCap) / 1000
	shotMaxInitialSpeed      float32 = 1900
	shotGravity              float32 = 3800
	shotMinDirectionLengthSq float32 = 0.0001
)

func buildShotParameters(penguinPosition, target Position, chargeSeconds float32, shotType ShotType) nativeShotParameters {
	handX := penguinPosition.X + shotHandOffsetX
	handY := penguinPosition.Y + shotHandOffsetY
	screenDeltaX := target.X - handX
	screenDeltaY := target.Y - handY

	screenLenSq := screenDeltaX*screenDeltaX + screenDeltaY*screenDeltaY
	screenDirX, screenDirY := float32(1), float32(0)
	if screenLenSq > shotMinDirectionLengthSq {
		invLen := 1 / float32(math.Sqrt(float64(screenLenSq)))
		screenDirX = screenDeltaX * invLen
		screenDirY = screenDeltaY * invLen
	}

	origin := Position{
		X: handX + screenDirX*shotBallSpawnDistance,
		Y: handY + screenDirY*shotBallSpawnDistance,
	}
	direction := Position{X: screenDeltaX, Y: screenDeltaY / shotPlanePerspectiveY}

	fraction := chargeSeconds / shotMaxChargeSeconds
	if fraction < 0 {
		fraction = 0
	} else if fraction > 1 {
		fraction = 1
	}
	speed := float32(math.Sqrt(float64(fraction))) * shotMaxInitialSpeed
	angleDegrees := float32(30)
	if shotType == ShotTypeRight {
		angleDegrees = 60
	}
	return nativeComputeShotParameters(origin, direction, speed, angleDegrees, shotGravity)
}
