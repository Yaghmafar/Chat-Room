package main

import (
	"fmt"
	"log"
	"net/http"
	"sync"

	"github.com/gorilla/websocket"
)

type Client struct {
	conn     *websocket.Conn
	username string
}

type ChatServer struct {
	clients    map[*Client]bool
	broadcast  chan []byte
	register   chan *Client
	unregister chan *Client
	mutex      sync.Mutex
}

var upgrader = websocket.Upgrader{
	ReadBufferSize:  1024,
	WriteBufferSize: 1024,
	CheckOrigin: func(r *http.Request) bool {
		return true
	},
}

func newChatServer() *ChatServer {
	return &ChatServer{
		clients:    make(map[*Client]bool),
		broadcast:  make(chan []byte),
		register:   make(chan *Client),
		unregister: make(chan *Client),
	}
}

func (s *ChatServer) run() {
	for {
		select {
		case client := <-s.register:
			s.mutex.Lock()
			s.clients[client] = true
			s.mutex.Unlock()
			log.Printf("User %s joined the chat", client.username)
			s.broadcast <- []byte(fmt.Sprintf("User %s joined the chat", client.username))

		case client := <-s.unregister:
			s.mutex.Lock()
			if _, ok := s.clients[client]; ok {
				delete(s.clients, client)
				client.conn.Close()
				log.Printf("User %s left the chat", client.username)
			}
			s.mutex.Unlock()
			s.broadcast <- []byte(fmt.Sprintf("User %s left the chat", client.username))

		case message := <-s.broadcast:
			s.mutex.Lock()
			for client := range s.clients {
				err := client.conn.WriteMessage(websocket.TextMessage, message)
				if err != nil {
					log.Printf("Error broadcasting to client %s: %v", client.username, err)
					client.conn.Close()
					delete(s.clients, client)
				} else {
					log.Printf("Message sent to %s: %s", client.username, string(message))
				}
			}
			s.mutex.Unlock()
		}
	}
}

func (s *ChatServer) handleWebSocket(w http.ResponseWriter, r *http.Request) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("Error upgrading connection: %v", err)
		return
	}

	username := r.URL.Query().Get("username")
	if username == "" {
		username = "Anonymous"
	}

	client := &Client{
		conn:     conn,
		username: username,
	}

	log.Printf("New connection: %s", client.username)
	s.register <- client

	defer func() {
		log.Printf("Connection closed: %s", client.username)
		s.unregister <- client
	}()

	for {
		_, message, err := client.conn.ReadMessage()
		if err != nil {
			log.Printf("Error reading message from %s: %v", client.username, err)
			break
		}

		formattedMessage := fmt.Sprintf("%s: %s", client.username, string(message))
		log.Printf("Received message from %s: %s", client.username, formattedMessage)
		s.broadcast <- []byte(formattedMessage)
	}
}

func main() {
	server := newChatServer()
	go server.run()

	http.HandleFunc("/ws", server.handleWebSocket)

	log.Println("Chat server starting on :8080...")
	if err := http.ListenAndServe(":8080", nil); err != nil {
		log.Fatal("ListenAndServe error:", err)
	}
}
