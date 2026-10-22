package main

import (
	"encoding/binary"
	"testing"
)

// Stessa stanza usata dai test di rete: i pacchetti restano nelle code,
// senza alcun socket reale.
func matchStatusTestRoom() *GameRoom {
	room := syncTestRoom()
	room.totalEggs = 5
	return room
}

func expectMatchStatus(t *testing.T, room *GameRoom, p1, p2, remaining, winner uint32) {
	t.Helper()
	for _, conn := range []*PlayerConnection{room.firstPlayerConnection, room.secondPlayerConnection} {
		select {
		case packet := <-conn.OutgoingPacketChannel:
			if len(packet) != 17 || packet[0] != MsgMatchStatus ||
				binary.LittleEndian.Uint32(packet[1:5]) != p1 ||
				binary.LittleEndian.Uint32(packet[5:9]) != p2 ||
				binary.LittleEndian.Uint32(packet[9:13]) != remaining ||
				binary.LittleEndian.Uint32(packet[13:17]) != winner {
				t.Fatalf("invalid match status: %v, expected score %d-%d remaining %d winner %d",
					packet, p1, p2, remaining, winner)
			}
		default:
			t.Fatal("match status not delivered to both clients")
		}
	}
}

func TestEggDeliveryCountsOnlyOnceAndFinishesAtFive(t *testing.T) {
	room := matchStatusTestRoom()
	a, b := &room.firstPlayer, &room.secondPlayer
	a.LastMask = WithEgg | PuttingEgg
	b.LastMask = WithEgg | PuttingEgg

	for _, tc := range []struct {
		owner                     *Player
		egg                       int32
		p1, p2, remaining, winner uint32
	}{
		{a, 0, 1, 0, 4, 0},
		{b, 1, 1, 1, 3, 0},
		{a, 2, 2, 1, 2, 0},
		{b, 3, 2, 2, 1, 0},
		{a, 4, 3, 2, 0, 1},
	} {
		if !room.recordEggDelivery(tc.owner, tc.egg) {
			t.Fatalf("legitimate delivery rejected for egg %d", tc.egg)
		}
		expectMatchStatus(t, room, tc.p1, tc.p2, tc.remaining, tc.winner)
		if room.recordEggDelivery(tc.owner, tc.egg) {
			t.Fatal("duplicate egg counted")
		}
	}
	if !room.matchFinished || len(room.deliveredEggs) != 5 {
		t.Fatal("five deliveries must finish match exactly once")
	}
	if room.recordEggDelivery(b, 0) {
		t.Fatal("delivery after finished match must be rejected")
	}
}

func TestEggDeliveryRejectsInvalidIDStateAndPlatform(t *testing.T) {
	room := matchStatusTestRoom()
	p := &room.firstPlayer
	p.LastMask = WithEgg | PuttingEgg
	for _, id := range []int32{-1, 5, 100} {
		if room.recordEggDelivery(p, id) {
			t.Fatalf("invalid egg %d accepted", id)
		}
	}
	p.LastMask = WithEgg
	if room.recordEggDelivery(p, 0) {
		t.Fatal("delivery without PuttingEgg accepted")
	}
	p.LastMask = WithEgg | PuttingEgg
	p.X = 600
	if room.recordEggDelivery(p, 0) {
		t.Fatal("delivery outside home accepted")
	}
	p.X = room.basePositions[0].X
	p.FreezeStartTick = 1
	p.FreezeEndTick = 40
	room.matchTick = 20
	if room.recordEggDelivery(p, 0) {
		t.Fatal("delivery while server-frozen accepted")
	}
	if len(room.firstPlayerConnection.OutgoingPacketChannel) > 0 || len(room.secondPlayerConnection.OutgoingPacketChannel) > 0 {
		t.Fatal("rejected delivery broadcast a status")
	}
}

func TestEggDeliveryEventRespectsServerQueue(t *testing.T) {
	room := matchStatusTestRoom()
	room.matchTick = 20
	a := &room.firstPlayer
	room.firstPlayerConnection.EventChannel <- PlayerEvent{Type: EventInput,
		Input: InputState{SeqN: 1, Mask: WithEgg | PuttingEgg}}
	room.firstPlayerConnection.EventChannel <- PlayerEvent{Type: EventEggDelivered, EggID: 2}
	room.drainEventChannelToPending(room.firstPlayerConnection, a,
		&room.firstPlayerPendingEvents, a.LastRx)
	room.applyMaturePendingEvents(a, &room.firstPlayerPendingEvents,
		room.secondPlayerConnection, MoveDt)
	expectMatchStatus(t, room, 1, 0, 4, 0)
}
