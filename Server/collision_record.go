package main

type CollisionRecordIn struct {
	Tag    string
	X      float32
	Y      float32
	Width  int32
	Height int32
}

func NewCollisionRecordIn(tag string, x, y float32, width, height int32) CollisionRecordIn {
	return CollisionRecordIn{
		Tag:    tag,
		X:      x,
		Y:      y,
		Width:  width,
		Height: height,
	}
}

type CollisionRecordOut struct {
	MyTag    string
	OtherTag string
	Type     int32
}

func NewCollisionRecordOut(myTag, otherTag string, collisionType int32) CollisionRecordOut {
	return CollisionRecordOut{
		MyTag:    myTag,
		OtherTag: otherTag,
		Type:     collisionType,
	}
}
