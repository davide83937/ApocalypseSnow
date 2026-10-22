package main

import (
	"encoding/binary"
	"log"
	"math"
)

// recordEggDelivery conteggia un uovo una sola volta, usando le coordinate
// e lo stato del giocatore gia' verificati dal server.
// TODO (autorita' uova): il server NON ricostruisce ancora quale uovo il
// giocatore possiede e NON conta il tempo di deposito. Oggi il client invia
// MsgEggDelivered dopo 1 secondo di PuttingEgg sulla propria piattaforma
// (con reset locale a 1.2 secondi); il server verifica solo lo stato corrente.
// In futuro: possesso per eggID autoritativo e 30 tick consecutivi di
// PuttingEgg presso la propria base, azzerati su rilascio/uscita/HIT.
func (gameRoom *GameRoom) recordEggDelivery(player *Player, eggID int32) bool {
	if gameRoom.matchFinished || gameRoom.totalEggs == 0 ||
		eggID < 0 || int(eggID) >= gameRoom.totalEggs ||
		gameRoom.deliveredEggs[eggID] || player.ID < 1 || player.ID > 2 {
		return false
	}

	// Serve una consegna in corso, non un generico pacchetto con un ID.
	if player.LastMask&(WithEgg|PuttingEgg) != (WithEgg|PuttingEgg) ||
		gameRoom.isServerFrozen(player) {
		log.Printf("[EGG DELIVERY REJECT] player=%d egg=%d state=%d frozen=%t",
			player.ID, eggID, player.LastMask, gameRoom.isServerFrozen(player))
		return false
	}

	// La base usa uno sprite 140x140: sovrapposizione con la stessa hitbox
	// del pinguino usata dal server (centro X+48,Y+64; dimensioni 96x64).
	const platformSize float32 = 140
	home := gameRoom.basePositions[player.ID-1]
	playerCenterX := player.X + penguinHalfTextureFractionWidth
	playerCenterY := player.Y + penguinHalfTextureFractionHeight
	if float32(math.Abs(float64(playerCenterX-home.X))) >=
		(float32(penguinTextureFractionWidth)+platformSize)/2 ||
		float32(math.Abs(float64(playerCenterY-home.Y))) >=
			(float32(penguinHalfTextureFractionHeight)+platformSize)/2 {
		log.Printf("[EGG DELIVERY REJECT] player=%d egg=%d not at home", player.ID, eggID)
		return false
	}

	gameRoom.deliveredEggs[eggID] = true
	gameRoom.scores[player.ID-1]++
	var winnerID uint32
	if len(gameRoom.deliveredEggs) == gameRoom.totalEggs {
		gameRoom.matchFinished = true
		winnerID = 1
		if gameRoom.scores[1] > gameRoom.scores[0] {
			winnerID = 2
		} else if gameRoom.scores[1] == gameRoom.scores[0] {
			winnerID = 3 // pareggio (possibile solo con numero pari di uova)
		}
	}

	log.Printf("[EGG DELIVERY] player=%d egg=%d score=%d-%d remaining=%d winner=%d",
		player.ID, eggID, gameRoom.scores[0], gameRoom.scores[1],
		gameRoom.totalEggs-len(gameRoom.deliveredEggs), winnerID)
	gameRoom.sendMatchStatus(winnerID)
	return true
}

// Un unico formato per i punteggi intermedi e il risultato finale.
// winnerID=0: partita in corso; 1/2: vincitore; 3: pareggio.
func (gameRoom *GameRoom) sendMatchStatus(winnerID uint32) {
	packet := make([]byte, 17)
	packet[0] = MsgMatchStatus
	binary.LittleEndian.PutUint32(packet[1:5], gameRoom.scores[0])
	binary.LittleEndian.PutUint32(packet[5:9], gameRoom.scores[1])
	binary.LittleEndian.PutUint32(packet[9:13], uint32(gameRoom.totalEggs-len(gameRoom.deliveredEggs)))
	binary.LittleEndian.PutUint32(packet[13:17], winnerID)
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
}
