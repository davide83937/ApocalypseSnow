package main

/*
#cgo CFLAGS: -I../Shared
#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll
#include "library.h"
*/
import "C"

func PhysicsBuildInfo() string {
	return C.GoString(C.PhysicsBuildInfo())
}

func uniformRectilinearMotion(position *float32, velocity, deltaTime float32) {
	cPosition := C.float(*position)
	C.uniform_rectilinear_motion(&cPosition, C.float(velocity), C.float(deltaTime))
	*position = float32(cPosition)
}

func normalizeVelocity(velocityX, velocityY *float32) {
	cVelocityX := C.float(*velocityX)
	cVelocityY := C.float(*velocityY)

	C.normalizeVelocity(&cVelocityX, &cVelocityY)

	*velocityX = float32(cVelocityX)
	*velocityY = float32(cVelocityY)
}
