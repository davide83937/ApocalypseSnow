package main

import (
	"math"
	"testing"
	"time"
)

// Questi test non simulano un nuovo scheduler: invocano il mature pending
// originale ed esaminano ACK, posizioni e sequenze relative dei due client.
func syncTestRoom() *GameRoom {
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

func syncInput(seq uint32, mask int32) PlayerEvent {
	return PlayerEvent{Type: EventInput, Input: InputState{SeqN: seq, Mask: mask}}
}

func syncClose(t *testing.T, got, expected float32) {
	t.Helper()
	if math.Abs(float64(got-expected)) > 0.05 {
		t.Fatalf("position %.4f, expected %.4f", got, expected)
	}
}

func TestOriginalMaturePendingRecoversTenPacketsInSameServerTick(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 30
	start := room.firstPlayer.X
	now := time.Now()

	for seq := uint32(1); seq <= 10; seq++ {
		room.firstPlayerConnection.EventChannel <- syncInput(seq, Right)
	}

	drain, inserted, dropped := room.drainEventChannelToPending(
		room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
	if drain != 10 || inserted != 10 || dropped != 0 {
		t.Fatalf("drain=%d inserted=%d dropped=%d", drain, inserted, dropped)
	}
	applied, future := room.applyMaturePendingEvents(
		&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	if applied != 10 || future != 0 || room.firstPlayer.LastSeqN != 10 {
		t.Fatalf("applied=%d pending=%d ACK=%d", applied, future, room.firstPlayer.LastSeqN)
	}
	syncClose(t, room.firstPlayer.X, start+10*MoveSpeed*MoveDt)
}

func TestOriginalMaturePendingAllowsConstantClientOffset(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 40
	now := time.Now()
	ax0, bx0 := room.firstPlayer.X, room.secondPlayer.X
	// Allo stesso istante, A=14, B=17 e server=40.
	for seq := uint32(1); seq <= 14; seq++ {
		room.firstPlayerConnection.EventChannel <- syncInput(seq, Right)
	}
	for seq := uint32(1); seq <= 17; seq++ {
		room.secondPlayerConnection.EventChannel <- syncInput(seq, Right)
	}
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer,
		&room.firstPlayerPendingEvents, now)
	room.drainEventChannelToPending(room.secondPlayerConnection, &room.secondPlayer,
		&room.secondPlayerPendingEvents, now)
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	room.applyMaturePendingEvents(&room.secondPlayer, &room.secondPlayerPendingEvents, room.firstPlayerConnection, MoveDt)

	if room.firstPlayer.LastSeqN != 14 || room.secondPlayer.LastSeqN != 17 {
		t.Fatalf("initial ACK: A=%d B=%d", room.firstPlayer.LastSeqN, room.secondPlayer.LastSeqN)
	}
	syncClose(t, room.firstPlayer.X, ax0+14*MoveSpeed*MoveDt)
	syncClose(t, room.secondPlayer.X, bx0+17*MoveSpeed*MoveDt)

	// Dopo altri 6 tick di entrambi, lo sfasamento resta 3.
	room.matchTick += 6
	for seq := uint32(15); seq <= 20; seq++ {
		room.firstPlayerConnection.EventChannel <- syncInput(seq, Right)
	}
	for seq := uint32(18); seq <= 23; seq++ {
		room.secondPlayerConnection.EventChannel <- syncInput(seq, Right)
	}
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer,
		&room.firstPlayerPendingEvents, now.Add(200*time.Millisecond))
	room.drainEventChannelToPending(room.secondPlayerConnection, &room.secondPlayer,
		&room.secondPlayerPendingEvents, now.Add(200*time.Millisecond))
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	room.applyMaturePendingEvents(&room.secondPlayer, &room.secondPlayerPendingEvents, room.firstPlayerConnection, MoveDt)
	if room.firstPlayer.LastSeqN != 20 || room.secondPlayer.LastSeqN != 23 ||
		room.secondPlayer.LastSeqN-room.firstPlayer.LastSeqN != 3 {
		t.Fatalf("offset drift: A=%d B=%d", room.firstPlayer.LastSeqN, room.secondPlayer.LastSeqN)
	}
	syncClose(t, room.firstPlayer.X, ax0+20*MoveSpeed*MoveDt)
	syncClose(t, room.secondPlayer.X, bx0+23*MoveSpeed*MoveDt)
}

func TestOriginalMaturePendingKeepsFutureEventsPending(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 4
	now := time.Now()
	for seq := uint32(1); seq <= 7; seq++ {
		room.firstPlayerConnection.EventChannel <- syncInput(seq, Right)
	}
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
	applied, future := room.applyMaturePendingEvents(
		&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	if applied != 4 || future != 3 || room.firstPlayer.LastSeqN != 4 {
		t.Fatalf("at server tick 4: applied=%d future=%d ACK=%d", applied, future, room.firstPlayer.LastSeqN)
	}
	room.matchTick = 7
	applied, future = room.applyMaturePendingEvents(
		&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	if applied != 3 || future != 0 || room.firstPlayer.LastSeqN != 7 {
		t.Fatalf("at server tick 7: applied=%d future=%d ACK=%d", applied, future, room.firstPlayer.LastSeqN)
	}
}

func TestOriginalMaturePendingPreservesShotBeforeReleaseState(t *testing.T) {
	room := syncTestRoom()
	room.matchTick = 20
	now := time.Now()
	ch := room.firstPlayerConnection.EventChannel
	ch <- syncInput(1, ShootLeft)
	ch <- syncInput(2, ShootLeft)
	ch <- PlayerEvent{Type: EventShot, Shot: ShotEvent{
		SeqN: 3, Position: Position{X: 300, Y: 200}, Charge: 67,
	}}
	ch <- syncInput(3, 0)
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, now)
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	if len(room.activeShots) != 1 {
		t.Fatalf("expected one shot, got %d", len(room.activeShots))
	}
	if shot := room.activeShots[0]; shot.PressedTick != 1 || shot.ReleasedTick != 3 || shot.AuthCharge != 2 || shot.ClientShotTick != 3 || shot.OwnerPlayerID != 1 {
		t.Fatalf("wrong shot reconstruction: %+v", shot)
	}
	select {
	case pkt := <-room.secondPlayerConnection.OutgoingPacketChannel:
		checkRemoteShotPayload(t, pkt, 1, 3, ShotTypeLeft)
	default:
		t.Fatal("server did not forward the authoritative shot identity")
	}
	if room.firstPlayer.LastSeqN != 3 {
		t.Fatalf("ACK did not reach release: %d", room.firstPlayer.LastSeqN)
	}
}
