package main

import (
	"bufio"
	"encoding/binary"
	"fmt"
	"io"
	"net"
	"sync"
)

type PlayerConnection struct {
	networkConnection net.Conn

	InputChannel chan InputState
	ShotChannel  chan ShotEvent

	OutgoingPacketChannel chan []byte
	DisconnectChannel     chan struct{}

	closeOnce    sync.Once
	bufferReader *bufio.Reader
}

func NewPlayerConnection(networkConnection net.Conn) *PlayerConnection {
	return &PlayerConnection{
		networkConnection:     networkConnection,
		InputChannel:          make(chan InputState, 128),
		ShotChannel:           make(chan ShotEvent, 128),
		OutgoingPacketChannel: make(chan []byte, 128),
		DisconnectChannel:     make(chan struct{}),
		bufferReader:          bufio.NewReader(networkConnection),
	}
}

func (playerConnection *PlayerConnection) TryEnqueueOutgoingPacket(packet []byte, mustSend bool) {
	select {
	case playerConnection.OutgoingPacketChannel <- packet:
		return
	default:
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
	shotPayloadBuffer := make([]byte, 12)

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

			inputState := InputState{Mask: inputMask, SeqN: sequenceNumber}

			// latest-wins
			select {
			case playerConnection.InputChannel <- inputState:
			default:
				select {
				case <-playerConnection.InputChannel:
				default:
				}
				select {
				case playerConnection.InputChannel <- inputState:
				default:
				}
			}

		case MsgShot:
			if _, err := io.ReadFull(playerConnection.bufferReader, shotPayloadBuffer); err != nil {
				return
			}

			targetX := int32(binary.LittleEndian.Uint32(shotPayloadBuffer[0:4]))
			targetY := int32(binary.LittleEndian.Uint32(shotPayloadBuffer[4:8]))
			chargeValue := int32(binary.LittleEndian.Uint32(shotPayloadBuffer[8:12]))

			shotEvent := ShotEvent{X: targetX, Y: targetY, Charge: chargeValue}

			// latest-wins
			select {
			case playerConnection.ShotChannel <- shotEvent:
			default:
				select {
				case <-playerConnection.ShotChannel:
				default:
				}
				select {
				case playerConnection.ShotChannel <- shotEvent:
				default:
				}
			}

		default:
			fmt.Printf("[WARN] unknown type=%d from %v\n", messageTypeBuffer[0], playerConnection.networkConnection.RemoteAddr())
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
