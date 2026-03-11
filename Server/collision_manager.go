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

func (cm *CollisionManager) CheckPlayerCollisionAt(x, y float32, obstacles []Position) bool {
	cm.collisionRecordIns = cm.collisionRecordIns[:0]

	cm.AddObject("P", x+48, y+64, 96, 128)

	for _, obs := range obstacles {
		cm.AddObject("O", obs.X+125, obs.Y+125, 200, 80)
	}

	cm.SendToCpp()

	for _, result := range cm.collisionRecordOuts {
		if result.Type != 0 && (result.MyTag == "P" || result.OtherTag == "P") {
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
