package main

import "math"

type CollisionRecordIn struct {
	Tag    string
	X      float32
	Y      float32
	Width  int32
	Height int32
	MinZ   float32
	MaxZ   float32
}

func NewCollisionRecordIn(tag string, x, y float32, width, height int32, zBounds ...float32) CollisionRecordIn {
	minZ, maxZ := float32(0), float32(math.MaxFloat32)
	if len(zBounds) == 2 {
		minZ, maxZ = zBounds[0], zBounds[1]
	} else if len(zBounds) != 0 {
		panic("NewCollisionRecordIn requires both minZ and maxZ")
	}
	return CollisionRecordIn{
		Tag:    tag,
		X:      x,
		Y:      y,
		Width:  width,
		Height: height,
		MinZ:   minZ,
		MaxZ:   maxZ,
	}
}

type CollisionRecordOut struct {
	MyTag       string
	OtherTag    string
	Type        int32
	Penetration float32
}

func NewCollisionRecordOut(myTag, otherTag string, collisionType int32) CollisionRecordOut {
	return CollisionRecordOut{
		MyTag:    myTag,
		OtherTag: otherTag,
		Type:     collisionType,
	}
}
