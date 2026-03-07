package main

/*
#cgo CFLAGS: -I../Shared
#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll
#include "library.h"
#include <stdlib.h>  // Necessario per C.free
#include <string.h>  // Necessario per C.strcpy
*/
import "C"
import "unsafe"

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

// physics_wrapper.go

// Definiamo il metodo qui, così può accedere a "C"
func (gameRoom *GameRoom) checkCollisionAt(x, y float32) bool {
	if len(gameRoom.Obstacles) == 0 {
		return false
	}

	count := 1 + len(gameRoom.Obstacles)
	dataIn := make([]C.struct_CollisionDataIn, count)

	// Setup Player (Tag "P")
	cTagP := C.CString("P")
	defer C.free(unsafe.Pointer(cTagP))
	// Usiamo strcpy per copiare nel buffer char[16] della struct
	C.strcpy((*C.char)(unsafe.Pointer(&dataIn[0].tag[0])), cTagP)
	dataIn[0].x = C.float(x + 48)
	dataIn[0].y = C.float(y + 64)
	dataIn[0].width = 96
	dataIn[0].height = 128

	// Setup Ostacoli (Tag "O")
	cTagO := C.CString("O")
	defer C.free(unsafe.Pointer(cTagO))
	for i, obs := range gameRoom.Obstacles {
		idx := i + 1
		C.strcpy((*C.char)(unsafe.Pointer(&dataIn[idx].tag[0])), cTagO)
		dataIn[idx].x = C.float(obs.X + 125)
		dataIn[idx].y = C.float(obs.Y + 125)
		dataIn[idx].width = 200
		dataIn[idx].height = 80
	}

	resCount := count * (count - 1) / 2
	dataOut := make([]C.struct_CollisionDataOut, resCount)

	// Chiamata alla DLL C++
	C.check_collisions(&dataIn[0], &dataOut[0], C.int(count))

	// Analisi dei risultati
	for i := 0; i < resCount; i++ {
		// IL CAMPO 'type' DIVENTA '_type'
		if dataOut[i]._type != 0 {
			// Convertiamo i tag C in stringhe Go per il confronto
			myTag := C.GoString((*C.char)(unsafe.Pointer(&dataOut[i].myTag[0])))
			otherTag := C.GoString((*C.char)(unsafe.Pointer(&dataOut[i].otherTag[0])))

			// Se la collisione coinvolge il player ("P")
			if myTag == "P" || otherTag == "P" {
				return true
			}
		}
	}
	return false
}
