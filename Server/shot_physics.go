package main

/*
#cgo CFLAGS: -I../Shared
#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll
#include "shot_library.h"
*/
import "C"

// I parametri fisici del tiro, calcolati dalla DLL a partire da dati generici.
type nativeShotParameters struct {
	startX, startY float32
	vx, vy         float32
	vz, gravity    float32
}

func nativeComputeShotParameters(origin Position, direction Position,
	speed, angleDegrees, gravity float32) nativeShotParameters {
	shot := C.ComputeShotParameters(
		C.Vector2{x: C.float(origin.X), y: C.float(origin.Y)},
		C.Vector2{x: C.float(direction.X), y: C.float(direction.Y)},
		C.float(speed), C.float(angleDegrees), C.float(gravity))
	return nativeShotParameters{
		float32(shot.start.x), float32(shot.start.y),
		float32(shot.groundVelocity.x), float32(shot.groundVelocity.y),
		float32(shot.verticalSpeed), float32(shot.gravity),
	}
}

func nativeShotFlightDuration(verticalSpeed, gravity float32) float32 {
	return float32(C.ShotFlightDuration(C.float(verticalSpeed), C.float(gravity)))
}

// La DLL usa la stessa parabola e le stesse proiezioni del client.
func nativeSampleShot(startX, startY, vx, vy, vz, gravity, seconds float32) (
	screenX, screenY, shadowX, shadowY, height float32,
) {
	var dx, dy, z C.float
	C.calculate_parabolic_motion(C.float(seconds), C.float(vx), C.float(vy),
		C.float(vz), C.float(gravity), &dx, &dy, &z)
	start := C.Vector2{x: C.float(startX), y: C.float(startY)}
	ball := C.calculateScreenPosition(start, dx, dy, z)
	shadow := C.calculateScreenPosition(start, dx, dy, 0)
	return float32(ball.x), float32(ball.y), float32(shadow.x), float32(shadow.y), float32(z)
}
