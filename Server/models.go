// models.go
package main

import "time"

type Position struct {
	X float32
	Y float32
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

	// Timestamp ultimo MsgState ricevuto (timeout anti ghiaccio)
	LastRx time.Time
}

type InputState struct {
	Mask int32
	SeqN uint32
}

type ShotEvent struct {
	Position
	Charge float32
}
