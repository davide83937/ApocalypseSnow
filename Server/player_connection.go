package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"io"
	"math"
	"net"
	"sync"
)

type PlayerConnection struct {
	networkConnection net.Conn

	// EventChannel preserva l'ordine globale degli eventi letti dal TCP.
	// MsgState e MsgShot restano nello stesso ordine in cui arrivano sulla connessione.
	EventChannel chan PlayerEvent

	OutgoingPacketChannel chan []byte
	DisconnectChannel     chan struct{}

	closeOnce    sync.Once
	bufferReader *bufio.Reader
}

func NewPlayerConnection(networkConnection net.Conn) *PlayerConnection {
	return &PlayerConnection{
		networkConnection: networkConnection,

		// buffer unico per tutti gli eventi del player
		EventChannel: make(chan PlayerEvent, 1024),

		OutgoingPacketChannel: make(chan []byte, 256),
		DisconnectChannel:     make(chan struct{}),

		bufferReader: bufio.NewReader(networkConnection),
	}
}

func (playerConnection *PlayerConnection) TryEnqueueOutgoingPacket(packet []byte, mustSend bool) {
	select {
	case playerConnection.OutgoingPacketChannel <- packet:
		return
	default:
		// se è "mustSend" e la coda è piena, chiudiamo: meglio killare che desyncare
		if mustSend {
			playerConnection.CloseConnection()
		}
	}
}

func (playerConnection *PlayerConnection) StartWritePump() {
	defer playerConnection.CloseConnection()

	for {
		select {
		case <-playerConnection.DisconnectChannel:
			return

		case outgoingPacket := <-playerConnection.OutgoingPacketChannel:
			if outgoingPacket == nil {
				return
			}
			if err := writeAll(playerConnection.networkConnection, outgoingPacket); err != nil {
				return
			}
		}
	}
}

func (playerConnection *PlayerConnection) StartReadPump() {
	defer playerConnection.CloseConnection()

	messageTypeBuffer := make([]byte, 1)
	joinPayloadBuffer := make([]byte, 8)
	statePayloadBuffer := make([]byte, 8)
	shotPayloadBuffer := make([]byte, 16)

	for {
		select {
		case <-playerConnection.DisconnectChannel:
			return
		default:
		}

		if _, err := io.ReadFull(playerConnection.bufferReader, messageTypeBuffer); err != nil {
			return
		}

		switch messageTypeBuffer[0] {
		case MsgJoin:
			if _, err := io.ReadFull(playerConnection.bufferReader, joinPayloadBuffer); err != nil {
				return
			}

		case MsgState:
			if _, err := io.ReadFull(playerConnection.bufferReader, statePayloadBuffer); err != nil {
				return
			}

			inputMask := int32(binary.LittleEndian.Uint32(statePayloadBuffer[0:4]))
			inputMask = sanitizeMask(inputMask)
			sequenceNumber := binary.LittleEndian.Uint32(statePayloadBuffer[4:8])

			inputState := InputState{
				Mask: inputMask,
				SeqN: sequenceNumber,
			}

			event := PlayerEvent{
				Type:  EventInput,
				Input: inputState,
			}

			select {
			case playerConnection.EventChannel <- event:
			case <-playerConnection.DisconnectChannel:
				return
			}

		case MsgShot:
			if _, err := io.ReadFull(playerConnection.bufferReader, shotPayloadBuffer); err != nil {
				return
			}

			shotTick := binary.LittleEndian.Uint32(shotPayloadBuffer[0:4])
			targetX := math.Float32frombits(binary.LittleEndian.Uint32(shotPayloadBuffer[4:8]))
			targetY := math.Float32frombits(binary.LittleEndian.Uint32(shotPayloadBuffer[8:12]))
			chargeValue := binary.LittleEndian.Uint32(shotPayloadBuffer[12:16])

			shotEvent := ShotEvent{
				Position: Position{
					X: targetX,
					Y: targetY,
				},
				Charge: chargeValue,
				SeqN:   shotTick,
			}

			event := PlayerEvent{
				Type: EventShot,
				Shot: shotEvent,
			}

			select {
			case playerConnection.EventChannel <- event:
			case <-playerConnection.DisconnectChannel:
				return
			}

		default:
			fmt.Printf("[WARN] unknown type=%d from %v\n",
				messageTypeBuffer[0],
				playerConnection.networkConnection.RemoteAddr())
			return
		}
	}
}

func writeAll(networkConnection net.Conn, buffer []byte) error {
	totalBytesWritten := 0
	for totalBytesWritten < len(buffer) {
		bytesWritten, err := networkConnection.Write(buffer[totalBytesWritten:])
		if err != nil {
			return err
		}
		if bytesWritten == 0 {
			return io.ErrUnexpectedEOF
		}
		totalBytesWritten += bytesWritten
	}
	return nil
}

func (playerConnection *PlayerConnection) CloseConnection() {
	playerConnection.closeOnce.Do(func() {
		close(playerConnection.DisconnectChannel)
		_ = playerConnection.networkConnection.Close()
	})
}

func sanitizeMask(m int32) int32 {
	// Pulizia direzioni opposte
	if (m&Left) != 0 && (m&Right) != 0 {
		m &^= (Left | Right)
	}
	if (m&Up) != 0 && (m&Down) != 0 {
		m &^= (Up | Down)
	}

	// Validazione logica: se non hai l'uovo (WithEgg), non puoi inviare PuttingEgg
	if (m&WithEgg) == 0 && (m&PuttingEgg) != 0 {
		m &^= PuttingEgg
	}

	// Se stai raccogliendo (TakingEgg), non puoi stare già portando un uovo
	if (m&WithEgg) != 0 && (m&TakingEgg) != 0 {
		m &^= TakingEgg
	}

	return m
}
