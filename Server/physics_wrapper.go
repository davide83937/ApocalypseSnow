package main

/*
#cgo CFLAGS: -I../Shared
#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll
#include "library.h"
*/
import "C"

const (
	MoveHz    float32 = 30.0
	MoveDt    float32 = 1.0 / MoveHz
	MoveSpeed float32 = 200.0
)

func stepFromStateDLL(x, y float32, mask int32, dt float32) (float32, float32) {
	if (mask&Reload) != 0 || (mask&Freezing) != 0 || (mask&TakingEgg) != 0 || (mask&PuttingEgg) != 0 {
		return x, y
	}

	var velocityX C.float = 0
	var velocityY C.float = 0

	if (mask & Up) != 0 {
		velocityY -= 1
	}
	if (mask & Down) != 0 {
		velocityY += 1
	}
	if (mask & Left) != 0 {
		velocityX -= 1
	}
	if (mask & Right) != 0 {
		velocityX += 1
	}

	if velocityX != 0 || velocityY != 0 {
		C.normalizeVelocity(&velocityX, &velocityY)
	}

	velocityX *= C.float(MoveSpeed)
	velocityY *= C.float(MoveSpeed)

	cx := C.float(x)
	cy := C.float(y)

	C.uniform_rectilinear_motion(&cx, velocityX, C.float(dt))
	C.uniform_rectilinear_motion(&cy, velocityY, C.float(dt))

	return float32(cx), float32(cy)
}
