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
	Type     string `json:"type"`
	Username string `json:"username"`
	Content  string `json:"content"`
	ImageData string `json:"imageData,omitempty"`
	FileName string `json:"filename,omitempty"`
	FileData string `json:"fileData,omitempty"`
}

var (
	clients = make(map[*Client]bool)
	mutex   = &sync.Mutex{}
	chatHistory = []Message{}
)

var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool {
		return true
	},
}

func handleConnections(w http.ResponseWriter, r *http.Request) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Println(err)
		return
	}

	client := &Client{Conn: conn}

	mutex.Lock()
	clients[client] = true
	mutex.Unlock()

	// ارسال تاریخچه چت به کلاینت جدید
	for _, msg := range chatHistory {
		err := client.Conn.WriteJSON(msg)
		if err != nil {
			log.Printf("Error sending chat history: %v", err)
		}
	}

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
			log.Println(err)
			break
		}

		switch msg.Type {
		case "username":
			client.Username = msg.Username
			broadcastUserList()
		case "chat", "image", "file":
			msg.Username = client.Username
			broadcastMessage(msg)
			// ذخیره پیام در تاریخچه
			chatHistory = append(chatHistory, msg)
			// محدود کردن تاریخچه به 100 پیام آخر
			if len(chatHistory) > 100 {
				chatHistory = chatHistory[len(chatHistory)-100:]
			}
		}
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
			log.Printf("error sending user list: %v", err)
		}
	}
}

func broadcastMessage(msg Message) {
	for client := range clients {
		err := client.Conn.WriteJSON(msg)
		if err != nil {
			log.Printf("error: %v", err)
		}
	}
}

func main() {
	r := gin.Default()
	r.GET("/ws", func(c *gin.Context) {
		handleConnections(c.Writer, c.Request)
	})

	r.Run(":8080")
}