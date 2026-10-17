package main

import (
	"encoding/binary"
	"math"
	"testing"
)

func checkRemoteShotPayload(t *testing.T, payload []byte, ownerID, shotTick uint32, shotType ShotType) {
	t.Helper()
	if len(payload) != 22 {
		t.Fatalf("MsgRemoteShot length=%d, want 22", len(payload))
	}
	if payload[0] != MsgRemoteShot {
		t.Fatalf("message type=%d, want %d", payload[0], MsgRemoteShot)
	}
	if got := ShotType(payload[13]); got != shotType {
		t.Fatalf("shot type=%d, want %d", got, shotType)
	}
	if got := binary.LittleEndian.Uint32(payload[14:18]); got != ownerID {
		t.Fatalf("owner=%d, want %d", got, ownerID)
	}
	if got := binary.LittleEndian.Uint32(payload[18:22]); got != shotTick {
		t.Fatalf("shot tick=%d, want %d", got, shotTick)
	}
}

func TestRemoteShotBroadcastRetainsClientShotIdentity(t *testing.T) {
	room := syncTestRoom()
	shot := Shot{
		ShotID: 100, OwnerPlayerID: 2, ClientShotTick: 33,
		TargetX: 412.5, TargetY: 13.25, AuthCharge: 18,
		Type: ShotTypeRight,
	}
	room.forwardShotEventToOpponent(room.firstPlayerConnection, shot)
	select {
	case payload := <-room.firstPlayerConnection.OutgoingPacketChannel:
		checkRemoteShotPayload(t, payload, 2, 33, ShotTypeRight)
		if got := math.Float32frombits(binary.LittleEndian.Uint32(payload[1:5])); got != 412.5 {
			t.Fatalf("target x=%v", got)
		}
		if got := math.Float32frombits(binary.LittleEndian.Uint32(payload[5:9])); got != 13.25 {
			t.Fatalf("target y=%v", got)
		}
		if got := binary.LittleEndian.Uint32(payload[9:13]); got != 600 {
			t.Fatalf("charge=%d, want 600ms", got)
		}
	default:
		t.Fatal("missing MsgRemoteShot")
	}
}

func TestShotIdentitySeparatesPlayersWithSameClientTick(t *testing.T) {
	room := syncTestRoom()
	room.forwardShotEventToOpponent(room.firstPlayerConnection,
		Shot{ShotID: 1, OwnerPlayerID: 1, ClientShotTick: 33, AuthCharge: 3})
	room.forwardShotEventToOpponent(room.firstPlayerConnection,
		Shot{ShotID: 2, OwnerPlayerID: 2, ClientShotTick: 33, AuthCharge: 3})
	a := <-room.firstPlayerConnection.OutgoingPacketChannel
	b := <-room.firstPlayerConnection.OutgoingPacketChannel
	checkRemoteShotPayload(t, a, 1, 33, ShotTypeLeft)
	checkRemoteShotPayload(t, b, 2, 33, ShotTypeLeft)
}
