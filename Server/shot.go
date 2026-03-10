package main

type ShotType uint8

const (
	ShotTypeLeft ShotType = iota
	ShotTypeRight
)

type Shot struct {
	ShotID        uint32
	OwnerPlayerID uint32

	TargetX float32
	TargetY float32

	LocalCharge  uint32
	PressedTick  uint32
	ReleasedTick uint32
	AuthCharge   uint32

	Type ShotType

	/*FlightTime float32
	MaxHeight  float32

	CurrentX float32
	CurrentY float32
	CurrentZ float32

	IsPredicted     bool
	IsAuthoritative bool
	IsAlive         bool*/
}
