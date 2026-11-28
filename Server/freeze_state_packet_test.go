package main

import (
    "encoding/binary"
    "testing"
)

func TestAuthoritativeFreezeStatePackets(t *testing.T) {
    room := &GameRoom{matchTick: 100}
    player := Player{
        ID: 1, LastSeqN: 12, LastMask: Freezing,
        Position: Position{X: 120, Y: 80},
        FreezeStartTick: 100, FreezeEndTick: 190,
    }
    conn := &PlayerConnection{OutgoingPacketChannel: make(chan []byte, 4)}

    room.sendAuthStateToPlayer(conn, player)
    auth := <-conn.OutgoingPacketChannel
    if len(auth) != 17 || auth[0] != MsgAuthState ||
        binary.LittleEndian.Uint32(auth[13:17]) != 90 {
        t.Fatalf("auth freeze packet invalid: %v", auth)
    }

    room.sendRemoteStateToPlayer(conn, player)
    remote := <-conn.OutgoingPacketChannel
    if len(remote) != 17 || remote[0] != MsgRemoteState ||
        binary.LittleEndian.Uint32(remote[13:17]) != 90 ||
        binary.LittleEndian.Uint32(remote[9:13]) != uint32(Freezing) {
        t.Fatalf("remote freeze packet invalid: %v", remote)
    }

    room.matchTick = 145
    if remaining := room.remainingFreezeTicks(player); remaining != 45 {
        t.Fatalf("halfway freeze remaining=%d; want 45", remaining)
    }
    room.matchTick = 190
    if remaining := room.remainingFreezeTicks(player); remaining != 0 {
        t.Fatalf("expired freeze remaining=%d; want 0", remaining)
    }
}
