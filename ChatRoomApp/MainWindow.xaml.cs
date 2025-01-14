using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using ClosedXML.Excel;
using System.IO;


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
                else
                {
                    MessageBox.Show("نام کاربری نمی‌تواند خالی باشد.");
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

                await SendUsernameAsync();
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در اتصال: {ex.Message}");
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
                if (message.Length > 500)
                {
                    MessageBox.Show("پیام بیش از حد طولانی است. حداکثر طول ۵۰۰ کاراکتر است.");
                    return;
                }

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
private void MessageTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (e.Key == System.Windows.Input.Key.Enter)
    {
        SendMessage_Click(sender, e);
    }
}
    private void ScrollToBottom()
{
    if (MessagesList.Items.Count > 0)
    {
        var lastItem = MessagesList.Items[MessagesList.Items.Count - 1];
        MessagesList.ScrollIntoView(lastItem);
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

            Dispatcher.Invoke(() =>
            {
                var msgObject = JsonConvert.DeserializeObject<dynamic>(message);

                if (msgObject.type == "chat")
                {
                    _messages.Add($"{msgObject.username}: {msgObject.content}");
                }
                else if (msgObject.type == "system")
                {
                    _messages.Add($"[System]: {msgObject.content}");
                }
                else if (msgObject.type == "userlist")
                {
                    var usernames = msgObject.content.ToString().Split(',');
                    OnlineUsersList.Items.Clear();
                    foreach (var username in usernames)
                    {
                        if (!string.IsNullOrEmpty(username))
                        {
                            OnlineUsersList.Items.Add(username);
                        }
                    }
                }
                else if (msgObject.type == "delete")
                {
                    // حذف پیام از لیست
                    var messageToRemove = $"{msgObject.username}: {msgObject.content}";
                    _messages.Remove(messageToRemove);
                }

                ScrollToBottom(); // اسکرول به پایین
            });
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            Dispatcher.Invoke(() => {
                MessageBox.Show("ارتباط با سرور قطع شد.");
            });
            break;
        }
    }
}

private void ExportToExcel_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            // ایجاد ورک‌بوک جدید
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Chat Messages");

            // افزودن هدر به اکسل
            worksheet.Cell(1, 1).Value = "نام کاربری";
            worksheet.Cell(1, 2).Value = "پیام";

            // افزودن پیام‌ها به اکسل
            for (int i = 0; i < _messages.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = _messages[i].Split(':')[0]; // نام کاربری
                worksheet.Cell(i + 2, 2).Value = _messages[i].Substring(_messages[i].IndexOf(':') + 1).Trim(); // محتوا
            }

            // ذخیره کردن فایل
            workbook.SaveAs(saveFileDialog.FileName);
            MessageBox.Show("چت با موفقیت به فایل اکسل صادر شد.");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"خطا در ذخیره‌سازی: {ex.Message}");
    }
}

 private async void DeleteMessage_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button button && button.DataContext is string message)
    {
        if (message.StartsWith($"{_username}:"))
        {
            _messages.Remove(message);

            // فقط محتوای پیام را ارسال کنید
            var messageContent = message.Substring(message.IndexOf(':') + 1).Trim();

            var deleteMessage = new
            {
                type = "delete",
                username = _username,
                content = messageContent // ارسال فقط محتوا
            };
            var jsonMessage = JsonConvert.SerializeObject(deleteMessage);
            var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);

            await _webSocket.SendAsync(
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