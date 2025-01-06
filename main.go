package main

import (
	"log"
	"net/http"
	"sync"

	"github.com/gin-gonic/gin"
	"github.com/gorilla/websocket"
)

type Client struct {
	Conn     *websocket.Conn
	Username string
}

type Message struct {
	Type      string `json:"type"`
	Username  string `json:"username"`
	Content   string `json:"content"`
	ImageData string `json:"imageData,omitempty"`
	FileName  string `json:"filename,omitempty"`
	FileData  string `json:"fileData,omitempty"`
}

var (
	clients     = make(map[*Client]bool)
	mutex       = &sync.Mutex{}
	chatHistory = []Message{}
)

var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool {
		// بررسی امنیتی برای محدود کردن Origin
		return true
	},
}

func handleConnections(w http.ResponseWriter, r *http.Request) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("Upgrade error: %v", err)
		return
	}

	client := &Client{Conn: conn}

	mutex.Lock()
	clients[client] = true
	mutex.Unlock()

	// ارسال تاریخچه چت
	sendChatHistory(client)

	defer func() {
		mutex.Lock()
		delete(clients, client)
		mutex.Unlock()
		conn.Close()
		broadcastUserList()
	}()

	for {
		var msg Message
		err := conn.ReadJSON(&msg)
		if err != nil {
			log.Printf("ReadJSON error: %v", err)
			break
		}

		handleMessage(client, msg)
	}
}

func sendChatHistory(client *Client) {
	mutex.Lock()
	defer mutex.Unlock()

	for _, msg := range chatHistory {
		err := client.Conn.WriteJSON(msg)
		if err != nil {
			log.Printf("Error sending chat history: %v", err)
			break
		}
	}
}

func handleMessage(client *Client, msg Message) {
	switch msg.Type {
	case "username":
		client.Username = msg.Username
		broadcastUserList()
	case "chat", "image", "file":
		msg.Username = client.Username
		saveMessage(msg)
		broadcastMessage(msg)
	}
}

func saveMessage(msg Message) {
	mutex.Lock()
	defer mutex.Unlock()

	chatHistory = append(chatHistory, msg)
	if len(chatHistory) > 100 {
		chatHistory = chatHistory[len(chatHistory)-100:]
	}
}

func broadcastUserList() {
	usernames := make([]string, 0)
	mutex.Lock()
	for client := range clients {
		if client.Username != "" {
			usernames = append(usernames, client.Username)
		}
	}
	mutex.Unlock()

	userListMsg := Message{
		Type:     "userlist",
		Content:  "",
		Username: "",
	}

	for client := range clients {
		err := client.Conn.WriteJSON(userListMsg)
		if err != nil {
			log.Printf("Error sending user list: %v", err)
		}
	}
}

func broadcastMessage(msg Message) {
	mutex.Lock()
	defer mutex.Unlock()

	for client := range clients {
		err := client.Conn.WriteJSON(msg)
		if err != nil {
			log.Printf("Error broadcasting message: %v", err)
			client.Conn.Close()
			delete(clients, client)
		}
	}
}

func main() {
	r := gin.Default()
	r.GET("/ws", func(c *gin.Context) {
		handleConnections(c.Writer, c.Request)
	})

	if err := r.Run(":8080"); err != nil {
		log.Fatalf("Server failed to start: %v", err)
	}
}
