package main

import "log"

type Matchmaker struct {
	// JoinChannel riceve le nuove connessioni dei giocatori in attesa di un match.
	JoinChannel chan *PlayerConnection
}

func NewMatchmaker() *Matchmaker {
	return &Matchmaker{
		JoinChannel: make(chan *PlayerConnection, 64),
	}
}

// Run gira in una goroutine dedicata e accoppia i giocatori a due a due.
// Quando forma una coppia, crea una GameRoom e avvia il suo loop (tick loop) in background.
//
// Nota: gestiamo anche il caso in cui un player si disconnette mentre aspetta il secondo.
func (matchmaker *Matchmaker) Run() {
	log.Println("Matchmaker avviato: in attesa di giocatori...")

	for {
		firstPlayerConnection := matchmaker.waitForNextAliveConnection()
		if firstPlayerConnection == nil {
			// JoinChannel chiuso => termina
			log.Println("Matchmaker: JoinChannel chiuso, termino.")
			return
		}
		log.Println("Matchmaker: giocatore 1 entrato. In attesa di un avversario...")

		secondPlayerConnection := matchmaker.waitForNextAliveConnectionWhileWaiting(firstPlayerConnection)
		if secondPlayerConnection == nil {
			// Il primo si è disconnesso durante l'attesa del secondo: ripartiamo
			log.Println("Matchmaker: giocatore 1 disconnesso mentre aspettava. Riparto.")
			continue
		}
		log.Println("Matchmaker: giocatore 2 entrato. Avvio la partita...")

		gameRoom := NewGameRoom(firstPlayerConnection, secondPlayerConnection)
		go gameRoom.Run()
	}
}

// waitForNextAliveConnection attende una connessione valida e non già chiusa.
// Ritorna nil se JoinChannel viene chiuso.
func (matchmaker *Matchmaker) waitForNextAliveConnection() *PlayerConnection {
	for {
		playerConnection, ok := <-matchmaker.JoinChannel
		if !ok {
			return nil
		}
		if playerConnection == nil {
			continue
		}

		select {
		case <-playerConnection.DisconnectChannel:
			// già chiusa => scarto e continuo
			continue
		default:
			return playerConnection
		}
	}
}

// waitForNextAliveConnectionWhileWaiting aspetta il secondo player,
// ma se nel frattempo il primo si disconnette, ritorna nil.
func (matchmaker *Matchmaker) waitForNextAliveConnectionWhileWaiting(firstPlayerConnection *PlayerConnection) *PlayerConnection {
	for {
		select {
		case <-firstPlayerConnection.DisconnectChannel:
			return nil

		case secondPlayerConnection, ok := <-matchmaker.JoinChannel:
			if !ok {
				// se si chiude il canale, chiudiamo anche il primo per consistenza
				firstPlayerConnection.CloseConnection()
				return nil
			}
			if secondPlayerConnection == nil {
				continue
			}

			select {
			case <-secondPlayerConnection.DisconnectChannel:
				// già chiusa => scarto e continuo
				continue
			default:
				return secondPlayerConnection
			}
		}
	}
}
