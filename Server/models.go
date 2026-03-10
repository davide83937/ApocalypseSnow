package main

import "time"

type Position struct {
	X float32
	Y float32
}

type ChargeMode int

const (
	ChargeNone ChargeMode = iota
	ChargeLeft
	ChargeRight
)

type PendingShot struct {
	Active            bool
	Mode              ChargeMode
	PressTick         uint32
	ReleaseTick       uint32
	WaitingShotPacket bool
	WaitingNeutral    bool
	ShotPacket        *ShotEvent
}

type Player struct {
	ID uint32
	Position

	// Ultimo seq ACKato al client (MsgAuthState)
	LastSeqN uint32

	// Mask effettivamente usata nello step (serve anche per RemoteState)
	LastMask int32

	// Ultimo input ricevuto dalla rete
	RecvMask int32
	RecvSeqN uint32

	// Timestamp ultimo MsgState ricevuto
	LastRx time.Time

	// Stato autoritativo della carica tiro
	PendingShot PendingShot
}

type InputState struct {
	SeqN uint32
	Mask int32
}

type ShotEvent struct {
	SeqN uint32
	Position
	Charge uint32
}

type PlayerEventType int

const (
	EventInput PlayerEventType = iota
	EventShot
)

type PlayerEvent struct {
	Type  PlayerEventType
	Input InputState
	Shot  ShotEvent
}

func (event PlayerEvent) SeqN() uint32 {
	switch event.Type {
	case EventInput:
		return event.Input.SeqN
	case EventShot:
		return event.Shot.SeqN
	default:
		return 0
	}
}
