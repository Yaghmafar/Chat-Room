﻿using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ChatRoomApp
{
    public partial class MainWindow : Window
    {
        private ClientWebSocket _webSocket;
        private string _username;
        private ObservableCollection<string> _messages;

        public MainWindow()
        {
            InitializeComponent();
            _messages = new ObservableCollection<string>();
            MessagesList.ItemsSource = _messages;
            ShowUsernameDialog();
        }

        private void ShowUsernameDialog()
        {
            var dialog = new Window
            {
                Title = "نام کاربری",
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            var button = new Button { Content = "تأیید" };

            button.Click += (s, e) => {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    _username = textBox.Text;
                    dialog.Close();
                    ConnectToWebSocket();
                }
            };

            panel.Children.Add(new TextBlock { Text = "نام کاربری خود را وارد کنید:" });
            panel.Children.Add(textBox);
            panel.Children.Add(button);

            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private async void ConnectToWebSocket()
        {
            try 
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri("ws://localhost:8080/ws"), CancellationToken.None);

                // ارسال نام کاربری
                await SendUsernameAsync();

                // شروع دریافت پیام
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}");
            }
        }

        private async Task SendUsernameAsync()
        {
            var usernameMessage = new 
            {
                type = "username",
                username = _username
            };
            var jsonMessage = JsonConvert.SerializeObject(usernameMessage);
            var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(messageBytes), 
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None
            );
        }

        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text;
            if (!string.IsNullOrWhiteSpace(message))
            {
                var chatMessage = new 
                {
                    type = "chat",
                    username = _username,
                    content = message
                };
                var jsonMessage = JsonConvert.SerializeObject(chatMessage);
                var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(messageBytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None
                );
                MessageTextBox.Clear();
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 4];
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Dispatcher.Invoke(() => {
                        var msgObject = JsonConvert.DeserializeObject<dynamic>(message);

                        if (msgObject.type == "chat")
                        {
                            _messages.Add($"{msgObject.username}: {msgObject.content}");
                        }
                    });
                }
            }
        }

        private void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is string message)
            {
                // بررسی اینکه پیام متعلق به کاربر جاری باشد
                if (message.StartsWith($"{_username}:"))
                {
                    _messages.Remove(message);

                    // ارسال درخواست حذف به سرور (اختیاری)
                    var deleteMessage = new
                    {
                        type = "delete",
                        username = _username,
                        content = message
                    };
                    var jsonMessage = JsonConvert.SerializeObject(deleteMessage);
                    var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);

                    _ = _webSocket.SendAsync(
                        new ArraySegment<byte>(messageBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
                else
                {
                    MessageBox.Show("شما فقط می‌توانید پیام‌های خودتان را حذف کنید.");
                }
            }
        }
    }
}