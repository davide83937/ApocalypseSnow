package main

import (
	"math"
	"testing"
	"time"
)

func testTickRoom() *GameRoom {
	a := &PlayerConnection{
		EventChannel:          make(chan PlayerEvent, 128),
		OutgoingPacketChannel: make(chan []byte, 128),
	}
	b := &PlayerConnection{
		EventChannel:          make(chan PlayerEvent, 128),
		OutgoingPacketChannel: make(chan []byte, 128),
	}
	return NewGameRoom(a, b)
}

func inputAt(seq uint32, mask int32) PlayerEvent {
	return PlayerEvent{Type: EventInput, Input: InputState{SeqN: seq, Mask: mask}}
}

func closeEnough(t *testing.T, got, want float32) {
	t.Helper()
	if math.Abs(float64(got-want)) > 0.03 {
		t.Fatalf("got %.4f, want %.4f", got, want)
	}
}

func TestInputSequenceIndependentFromRoomClock(t *testing.T) {
	for _, roomTick := range []uint32{1, 1000} {
		room := testTickRoom()
		room.matchTick = roomTick
		now := time.Now()
		room.firstPlayerConnection.EventChannel <- inputAt(5000, Right)
		room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
		applied, future := room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
		if applied != 1 || future != 0 || room.firstPlayer.LastSeqN != 5000 {
			t.Fatalf("tick=%d applied=%d pending=%d seq=%d", roomTick, applied, future, room.firstPlayer.LastSeqN)
		}
		closeEnough(t, room.firstPlayer.X, 100+MoveSpeed*MoveDt)
	}
}

func TestBurstAdvancesOneInputAndACKPerServerTick(t *testing.T) {
	room := testTickRoom()
	now := time.Now()
	for seq := uint32(1); seq <= 8; seq++ {
		room.firstPlayerConnection.EventChannel <- inputAt(seq, Right)
	}
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)

	for expected := uint32(1); expected <= 8; expected++ {
		applied, future := room.applyMaturePendingEvents(
			&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
		if applied != 1 || future != int(8-expected) || room.firstPlayer.LastSeqN != expected {
			t.Fatalf("tick=%d applied=%d pending=%d ack=%d", expected, applied, future, room.firstPlayer.LastSeqN)
		}
		closeEnough(t, room.firstPlayer.X, 100+float32(expected)*MoveSpeed*MoveDt)
	}

	applied, future := room.applyMaturePendingEvents(
		&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	if applied != 0 || future != 0 {
		t.Fatalf("unexpected simulated input: applied=%d pending=%d", applied, future)
	}
	closeEnough(t, room.firstPlayer.X, 100+8*MoveSpeed*MoveDt)
}

func TestQueuedInputSequenceIsNotRewoundByAnACK(t *testing.T) {
	room := testTickRoom()
	now := time.Now()
	room.firstPlayerConnection.EventChannel <- inputAt(1, Right)
	room.firstPlayerConnection.EventChannel <- inputAt(2, Right)
	room.firstPlayerConnection.EventChannel <- inputAt(3, Right)

	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)

	if room.firstPlayer.LastSeqN != 1 || room.firstPlayer.RecvSeqN != 3 {
		t.Fatalf("processed=%d received=%d", room.firstPlayer.LastSeqN, room.firstPlayer.RecvSeqN)
	}

	// Il duplicate SeqN=3 non deve inserirsi due volte nel backlog.
	room.firstPlayerConnection.EventChannel <- inputAt(3, Right)
	_, _, dropped := room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
	if dropped != 1 || len(room.firstPlayerPendingEvents) != 2 {
		t.Fatalf("duplicate dropped=%d backlog=%d", dropped, len(room.firstPlayerPendingEvents))
	}
}

func TestDuplicateDoesNotRefreshInputTimeout(t *testing.T) {
	room := testTickRoom()
	at := time.Now()
	room.firstPlayerConnection.EventChannel <- inputAt(1, Right)
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, at)
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)

	room.firstPlayerConnection.EventChannel <- inputAt(1, Right)
	_, _, dropped := room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, at.Add(time.Second))
	if dropped != 1 {
		t.Fatalf("duplicate not discarded: %d", dropped)
	}
	if room.firstPlayer.LastRx != at {
		t.Fatal("duplicate refreshed LastRx")
	}
}

func TestShotPacketBeforeReleaseStatePreservesFSM(t *testing.T) {
	room := testTickRoom()
	now := time.Now()
	a := room.firstPlayerConnection.EventChannel
	a <- inputAt(1, ShootLeft)
	a <- inputAt(2, ShootLeft)
	// This is the real TCP order if Penguin.Shot runs before SendState in the same tick.
	a <- PlayerEvent{Type: EventShot, Shot: ShotEvent{
		SeqN: 3, Position: Position{X: 300, Y: 200}, Charge: 67,
	}}
	a <- inputAt(3, 0)

	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
	for i := 0; i < 3; i++ {
		room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	}

	if len(room.activeShots) != 1 {
		t.Fatalf("expected exactly one server shot, got %d", len(room.activeShots))
	}
	shot := room.activeShots[0]
	if shot.PressedTick != 1 || shot.ReleasedTick != 3 || shot.AuthCharge != 2 {
		t.Fatalf("bad server-side charge: %+v", shot)
	}
	if room.firstPlayer.LastSeqN != 3 {
		t.Fatalf("ACK should include release input, got %d", room.firstPlayer.LastSeqN)
	}
	if len(room.secondPlayerConnection.OutgoingPacketChannel) != 1 {
		t.Fatal("remote shot not forwarded")
	}
}
