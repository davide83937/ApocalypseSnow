package main

import (
	"testing"
	"time"
)

// Esegue la sequenza di input nel normale scheduler del server.
func processShotInputSequence(t *testing.T, events ...PlayerEvent) *GameRoom {
	t.Helper()
	room := syncTestRoom()
	room.matchTick = 40
	for _, event := range events {
		room.firstPlayerConnection.EventChannel <- event
	}
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, time.Now())
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	return room
}

// Prepara un MsgShot con identita e carica date.
func shotAt(tick, charge uint32) PlayerEvent {
	return PlayerEvent{Type: EventShot, Shot: ShotEvent{
		SeqN:     tick,
		Charge:   charge,
		Position: Position{X: 300, Y: 200},
	}}
}

// Controlla che sia nato solo il nuovo tiro, con la carica giusta.
func requireShotFromNewCharge(t *testing.T, room *GameRoom) {
	t.Helper()
	if len(room.activeShots) != 1 {
		t.Fatalf("shots=%d, want 1; pending=%+v", len(room.activeShots), room.firstPlayer.PendingShot)
	}
	shot := room.activeShots[0]
	if shot.PressedTick != 6 || shot.ReleasedTick != 9 || shot.ClientShotTick != 9 || shot.AuthCharge != 3 {
		t.Fatalf("old charge reused: press=%d release=%d keyTick=%d charge=%d", shot.PressedTick, shot.ReleasedTick, shot.ClientShotTick, shot.AuthCharge)
	}
	if room.firstPlayer.PendingShot.Active || room.firstPlayer.PendingShot.ShotPacket != nil {
		t.Fatalf("pending shot not cleared: %+v", room.firstPlayer.PendingShot)
	}
}

// Ricaricare mentre si carica il tiro deve annullare il vecchio PendingShot.
func TestReloadCancelsChargingShotAndNextShotStartsFresh(t *testing.T) {
	room := processShotInputSequence(t,
		syncInput(1, ShootLeft),
		syncInput(2, ShootLeft),
		syncInput(3, Reload),
		syncInput(4, Reload),
		syncInput(5, 0),
		syncInput(6, ShootLeft),
		syncInput(7, ShootLeft),
		syncInput(8, ShootLeft),
		shotAt(9, 100),
		syncInput(9, 0),
	)
	requireShotFromNewCharge(t, room)
}

// Ricaricare dopo il rilascio deve cancellare anche l'attesa del MsgShot.
func TestReloadCancelsShotWaitingForPacket(t *testing.T) {
	room := processShotInputSequence(t,
		syncInput(1, ShootLeft),
		syncInput(2, ShootLeft),
		syncInput(3, 0),
		syncInput(4, Reload),
		syncInput(5, 0),
		syncInput(6, ShootLeft),
		syncInput(7, ShootLeft),
		syncInput(8, ShootLeft),
		shotAt(9, 100),
		syncInput(9, 0),
	)
	requireShotFromNewCharge(t, room)
}

// Un pacchetto con tick sbagliato non deve consumare la carica attuale.
func TestShotPacketCannotUseAnotherReleaseTick(t *testing.T) {
	room := processShotInputSequence(t,
		syncInput(1, ShootLeft),
		syncInput(2, ShootLeft),
		syncInput(3, 0),
		shotAt(9, 100), // Non corrisponde al rilascio al tick 3.
	)
	if len(room.activeShots) != 0 {
		t.Fatalf("mismatched shot created: %+v", room.activeShots[0])
	}
	if !room.firstPlayer.PendingShot.Active || !room.firstPlayer.PendingShot.WaitingShotPacket {
		t.Fatalf("waiting charge prematurely invalidated: %+v", room.firstPlayer.PendingShot)
	}

	// Il vero pacchetto del tick 3 deve ancora poter completare la carica.
	room.firstPlayerConnection.EventChannel <- shotAt(3, 67)
	room.drainEventChannelToPending(room.firstPlayerConnection, &room.firstPlayer, &room.firstPlayerPendingEvents, time.Now())
	room.applyMaturePendingEvents(&room.firstPlayer, &room.firstPlayerPendingEvents, room.secondPlayerConnection, MoveDt)
	if len(room.activeShots) != 1 || room.activeShots[0].ClientShotTick != 3 {
		t.Fatalf("valid packet not accepted after mismatched one: %+v", room.activeShots)
	}
}

// Un tiro normale con MsgShot precedente allo stato di rilascio resta valido.
func TestShotPacketBeforeReleaseStateStillAccepted(t *testing.T) {
	room := processShotInputSequence(t,
		syncInput(1, ShootLeft),
		syncInput(2, ShootLeft),
		shotAt(3, 67),
		syncInput(3, 0),
	)
	if len(room.activeShots) != 1 {
		t.Fatalf("valid shot was rejected, got %d", len(room.activeShots))
	}
	shot := room.activeShots[0]
	if shot.PressedTick != 1 || shot.ReleasedTick != 3 || shot.ClientShotTick != 3 {
		t.Fatalf("wrong valid shot: %+v", shot)
	}
}

// Ricaricare dopo un tiro valido non deve annullare la palla gia creata.
func TestReloadAfterValidShotDoesNotUndoIt(t *testing.T) {
	room := processShotInputSequence(t,
		syncInput(1, ShootLeft),
		syncInput(2, ShootLeft),
		shotAt(3, 67),
		syncInput(3, 0),
		syncInput(4, Reload),
		syncInput(5, 0),
		syncInput(6, ShootLeft),
		syncInput(7, ShootLeft),
		syncInput(8, ShootLeft),
		shotAt(9, 100),
		syncInput(9, 0),
	)
	if len(room.activeShots) != 2 {
		t.Fatalf("valid shots lost after reload, got %d", len(room.activeShots))
	}
	if room.activeShots[0].ClientShotTick != 3 || room.activeShots[1].ClientShotTick != 9 ||
		room.activeShots[1].PressedTick != 6 {
		t.Fatalf("wrong shots after reload: %+v", room.activeShots)
	}
}

// Annullare il tiro destro con Reload deve lasciare libera la FSM.
func TestReloadCancelsRightShot(t *testing.T) {
	room := processShotInputSequence(t,
		syncInput(1, ShootRight),
		syncInput(2, ShootRight),
		syncInput(3, Reload),
		syncInput(4, Reload),
		syncInput(5, 0),
		syncInput(6, ShootRight),
		syncInput(7, ShootRight),
		syncInput(8, ShootRight),
		shotAt(9, 100),
		syncInput(9, 0),
	)
	requireShotFromNewCharge(t, room)
	if room.activeShots[0].Type != ShotTypeRight {
		t.Fatalf("expected right shot, got %d", room.activeShots[0].Type)
	}
}
