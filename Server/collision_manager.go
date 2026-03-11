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
	collisionTagPenguin    = "penguin"
	collisionTagPenguinRed = "penguinRed"
	collisionTagObstacle   = "obstacle"

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
		cm.collisionRecordOuts = append(cm.collisionRecordOuts,
			NewCollisionRecordOut(
				C.GoString((*C.char)(unsafe.Pointer(&dataOut[i].myTag[0]))),
				C.GoString((*C.char)(unsafe.Pointer(&dataOut[i].otherTag[0]))),
				int32(dataOut[i]._type),
			),
		)
	}
}

func (cm *CollisionManager) Results() []CollisionRecordOut {
	return cm.collisionRecordOuts
}

func playerCollisionTag(playerID uint32) string {
	if playerID == 2 {
		return collisionTagPenguinRed
	}
	return collisionTagPenguin
}

func (cm *CollisionManager) CheckPlayerCollisionAt(playerTag string, x, y float32, obstacles []Position) bool {
	cm.collisionRecordIns = cm.collisionRecordIns[:0]

	// Replica 1:1 della costruzione del collider del client in Penguin.Update().
	posCollX := x + penguinHalfTextureFractionWidth
	posCollY := y + penguinHalfTextureFractionHeight
	cm.AddObject(playerTag, posCollX, posCollY, penguinTextureFractionWidth, penguinHalfTextureFractionHeight)

	// Replica 1:1 della costruzione del collider del client in Obstacle.LoadContent().
	for _, obs := range obstacles {
		posCollX := obs.X + obstacleHalfTextureFractionWidth
		posCollY := obs.Y + obstacleHalfTextureFractionHeight
		cm.AddObject(
			collisionTagObstacle,
			posCollX,
			posCollY+obstacleColliderYOffset,
			obstacleHalfTextureFractionWidth+obstacleColliderExtraWidth,
			obstacleHalfTextureFractionHeight,
		)
	}

	cm.SendToCpp()

	for _, result := range cm.collisionRecordOuts {
		if result.Type != 0 && (result.MyTag == playerTag || result.OtherTag == playerTag) {
			return true
		}
	}

	return false
}

func writeCollisionTag(dst *C.char, tag string) {
	cTag := C.CString(tag)
	defer C.free(unsafe.Pointer(cTag))

	C.memset(unsafe.Pointer(dst), 0, 16)
	C.strncpy(dst, cTag, 15)
}
