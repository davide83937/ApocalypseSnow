// models.go
package main

// Position rappresenta una coordinata fisica bidimensionale nel mondo di gioco.
// Usando l'embedding, ci permette di raggruppare logicamente le variabili
// senza perdere l'accesso diretto (es. player.X invece di player.Pos.X).
type Position struct {
	X float32
	Y float32
}

type Player struct {
	// Ogni player è identificato univocamente da un ID assegnato dal server.
	ID uint32

	// Coordinate della posizione corrente (autoritativa) nel mondo di gioco.
	// Grazie all'embedding anonimo, accediamo direttamente con player.X e player.Y
	Position

	// Ultimo numero di sequenza (seq) del messaggio di input (MsgState) processato dal server
	// per questo player. Viene rimandato al client come Ack nel MsgAuthState per reconciliation.
	LastSeqN uint32

	// Ultima input mask applicata (usata per il Server-Side Dead Reckoning in caso di jitter).
	LastMask int32
}

type InputState struct {
	// Bitmask dei comandi (Up/Down/Left/Right/Shoot/Reload/...).
	Mask int32

	// Numero di sequenza monotono assegnato dal client per ordinare/acknowledge gli input.
	SeqN uint32
}

type ShotEvent struct {
	// Coordinate del punto di mira (target) indicate dal client.
	// L'origine dello sparo viene determinata dal server usando la posizione del player.
	// NOTA: Rimangono int32 per rispettare il wire-format binario col Client C#.
	// NOTISSIMA: se vogliamo usare la strut anche qui dobbiamo uniformare questo X-Y: tutti int o tutti float?!??!?! OVUNQUE!
	Position

	// Parametro di potenza/charge.
	// NOTISSIMA: ANCHE CHARGE SECONDO ME E' FLOAT
	Charge float32
}
