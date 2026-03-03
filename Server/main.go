package main

import (
	"log"
	"math/rand"
	"net"
	"time"
)

func main() {
	rand.Seed(time.Now().UnixNano())

	listener, listenError := net.Listen("tcp", ListenAddr)
	if listenError != nil {
		log.Fatal(listenError)
	}
	defer listener.Close()

	matchmaker := NewMatchmaker()
	go matchmaker.Run()

	log.Println("New Version!")
	log.Println("Server listening on", ListenAddr)

	for {
		networkConnection, acceptError := listener.Accept()
		if acceptError != nil {
			log.Println("accept:", acceptError)
			continue
		}

		log.Println("[ACCEPT]", networkConnection.RemoteAddr())

		if tcpConnection, ok := networkConnection.(*net.TCPConn); ok {
			_ = tcpConnection.SetNoDelay(true)
		}

		playerConnection := NewPlayerConnection(networkConnection)

		go playerConnection.StartReadPump()
		go playerConnection.StartWritePump()

		select {
		case <-playerConnection.DisconnectChannel:
			continue
		default:
			matchmaker.JoinChannel <- playerConnection
		}
	}
}
