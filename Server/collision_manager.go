package main

/*
#cgo CFLAGS: -I../Shared
#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll
#include "library.h"
#include <stdlib.h>
#include <string.h>
*/
import "C"

import "unsafe"

const (
	collisionTagPenguin1 = "penguin1"
	collisionTagPenguin2 = "penguin2"
	collisionTagObstacle = "obstacle"

	// Replica server-side della geometria usata dal client.
	// Riferimenti:
	// - ApocalypseSnow/Penguin.cs
	// - ApocalypseSnow/Obstacle.cs
	penguinTextureWidth          = 288
	penguinTextureHeight         = 384
	penguinTextureFractionWidth  = penguinTextureWidth / 3  // 96
	penguinTextureFractionHeight = penguinTextureHeight / 3 // 128

	penguinHalfTextureFractionWidth  = penguinTextureFractionWidth / 2  // 48
	penguinHalfTextureFractionHeight = penguinTextureFractionHeight / 2 // 64

	obstacleTextureWidth          = 500
	obstacleTextureHeight         = 400
	obstacleTextureFractionWidth  = obstacleTextureWidth / 2  // 250
	obstacleTextureFractionHeight = obstacleTextureHeight / 2 // 200

	obstacleHalfTextureFractionWidth  = obstacleTextureFractionWidth / 2  // 125
	obstacleHalfTextureFractionHeight = obstacleTextureFractionHeight / 2 // 100
	obstacleColliderYOffset           = 25
	obstacleColliderExtraWidth        = 60
)

type CollisionManager struct {
	collisionRecordIns  []CollisionRecordIn
	collisionRecordOuts []CollisionRecordOut
}

func NewCollisionManager() *CollisionManager {
	return &CollisionManager{
		collisionRecordIns:  make([]CollisionRecordIn, 0),
		collisionRecordOuts: make([]CollisionRecordOut, 0),
	}
}

func (cm *CollisionManager) AddObject(tag string, x, y float32, width, height int32) {
	cm.collisionRecordIns = append(cm.collisionRecordIns,
		NewCollisionRecordIn(tag, x, y, width, height))
}

func (cm *CollisionManager) ModifyObject(tag string, x, y float32, width, height int32) {
	for i := range cm.collisionRecordIns {
		if cm.collisionRecordIns[i].Tag == tag {
			cm.collisionRecordIns[i].X = x
			cm.collisionRecordIns[i].Y = y
			cm.collisionRecordIns[i].Width = width
			cm.collisionRecordIns[i].Height = height
			return
		}
	}
}

func (cm *CollisionManager) RemoveObject(tag string) {
	filtered := cm.collisionRecordIns[:0]
	for _, record := range cm.collisionRecordIns {
		if record.Tag != tag {
			filtered = append(filtered, record)
		}
	}
	cm.collisionRecordIns = filtered
}

func (cm *CollisionManager) SendToCpp() {
	count := len(cm.collisionRecordIns)
	if count < 2 {
		cm.collisionRecordOuts = cm.collisionRecordOuts[:0]
		return
	}

	dataIn := make([]C.struct_CollisionDataIn, count)

	for i, record := range cm.collisionRecordIns {
		writeCollisionTag((*C.char)(unsafe.Pointer(&dataIn[i].tag[0])), record.Tag)
		dataIn[i].x = C.float(record.X)
		dataIn[i].y = C.float(record.Y)
		dataIn[i].width = C.int(record.Width)
		dataIn[i].height = C.int(record.Height)
	}

	resCount := count * (count - 1) / 2
	dataOut := make([]C.struct_CollisionDataOut, resCount)

	C.check_collisions(&dataIn[0], &dataOut[0], C.int(count))

	cm.collisionRecordOuts = cm.collisionRecordOuts[:0]
	for i := 0; i < resCount; i++ {
		record := NewCollisionRecordOut(
			C.GoString((*C.char)(unsafe.Pointer(&dataOut[i].myTag[0]))),
			C.GoString((*C.char)(unsafe.Pointer(&dataOut[i].otherTag[0]))),
			int32(dataOut[i]._type),
		)
		record.Penetration = float32(dataOut[i].penetration)
		cm.collisionRecordOuts = append(cm.collisionRecordOuts, record)
	}
}

func (cm *CollisionManager) Results() []CollisionRecordOut {
	return cm.collisionRecordOuts
}

func playerCollisionTag(playerID uint32) string {
	if playerID == 2 {
		return collisionTagPenguin2
	}
	return collisionTagPenguin1
}

// Risolve l'intersezione della posizione appena calcolata con gli ostacoli fissi.
// Stessa funzione C++ e stessa regola del client, senza alterare la scena registrata.
func (cm *CollisionManager) ResolvePlayerObstacleOverlaps(playerTag string, x, y float32) (float32, float32) {
	cx := x + penguinHalfTextureFractionWidth
	cy := y + penguinHalfTextureFractionHeight

	for pass := 0; pass < 8; pass++ {
		moved := false

		for _, obstacle := range cm.collisionRecordIns {
			if obstacle.Tag != collisionTagObstacle && !isObstacleTag(obstacle.Tag) {
				continue
			}

			var input [2]C.struct_CollisionDataIn
			var result [1]C.struct_CollisionDataOut

			writeCollisionTag((*C.char)(unsafe.Pointer(&input[0].tag[0])), playerTag)
			input[0].x, input[0].y = C.float(cx), C.float(cy)
			input[0].width = C.int(penguinTextureFractionWidth)
			input[0].height = C.int(penguinHalfTextureFractionHeight)

			writeCollisionTag((*C.char)(unsafe.Pointer(&input[1].tag[0])), obstacle.Tag)
			input[1].x, input[1].y = C.float(obstacle.X), C.float(obstacle.Y)
			input[1].width, input[1].height = C.int(obstacle.Width), C.int(obstacle.Height)

			C.check_collisions(&input[0], &result[0], 2)

			if result[0]._type == 0 || result[0].penetration <= 0 {
				continue
			}

			correction := float32(result[0].penetration)
			if C.GoString((*C.char)(unsafe.Pointer(&result[0].myTag[0]))) != playerTag {
				correction = -correction
			}

			switch result[0]._type {
			case 1:
				cy += correction
			case 2:
				cy -= correction
			case 3:
				cx -= correction
			case 4:
				cx += correction
			}
			moved = true
		}

		if !moved {
			break
		}
	}

	return cx - penguinHalfTextureFractionWidth, cy - penguinHalfTextureFractionHeight
}

func writeCollisionTag(dst *C.char, tag string) {
	cTag := C.CString(tag)
	defer C.free(unsafe.Pointer(cTag))

	C.memset(unsafe.Pointer(dst), 0, 16)
	C.strncpy(dst, cTag, 15)
}
