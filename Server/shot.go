package main

type ShotType uint8

const (
	ShotTypeLeft ShotType = iota
	ShotTypeRight
)

type Shot struct {
	ShotID         uint32
	ClientShotTick uint32
	OwnerPlayerID  uint32

	TargetX float32
	TargetY float32

	LocalCharge  uint32
	PressedTick  uint32
	ReleasedTick uint32
	AuthCharge   uint32

	Type ShotType

	// Runtime server-side
	StartX    float32
	StartY    float32
	SpawnTick uint32
	IsAlive   bool

	GroundVelX         float32
	GroundVelY         float32
	StartVerticalSpeed float32
	Gravity            float32
	FlightDuration     float32
}
