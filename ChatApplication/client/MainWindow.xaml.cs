using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Windows.Controls; // اضافه شد برای ScrollViewer
using Websocket.Client;

namespace WPFChatClient;

public partial class MainWindow : Window
{
    private WebsocketClient? _client;
    private readonly ObservableCollection<ChatMessage> _messages;
    private string _username = string.Empty;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public MainWindow()
    {
    InitializeComponent();
    _messages = new ObservableCollection<ChatMessage>();
    g = _messages;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_client?.IsRunning == true)
            {
                await DisconnectAsync();
                return;
            }

            await ConnectAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطا در اتصال: {ex.Message}");
        }
    }

    private async Task ConnectAsync()
    {
        _username = UsernameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(_username))
        {
            MessageBox.Show("لطفا نام کاربری را وارد کنید");
            return;
        }

        var url = new Uri($"ws://localhost:8080/ws?username={Uri.EscapeDataString(_username)}");
        _client = new WebsocketClient(url)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(30)
        };

        _client.DisconnectionHappened.Subscribe(info =>
        {
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"اتصال قطع شد: {info.Type}");
                UpdateConnectionStatus(false);
            });
        });

        _client.ReconnectionHappened.Subscribe(info =>
        {
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"اتصال مجدد برقرار شد: {info.Type}");
                UpdateConnectionStatus(true);
            });
        });

        _client.MessageReceived.Subscribe(msg =>
        {
            if (string.IsNullOrEmpty(msg.Text)) return;

            Dispatcher.Invoke(() =>
            {
                var parts = msg.Text.Split(':', 2);
                var chatMessage = new ChatMessage
                {
                    Username = parts[0].Trim(),
                    Message = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    Background = DetermineMessageBackground(parts[0].Trim())
                };
                _messages.Add(chatMessage);

                // Auto scroll to bottom - با روش ساده‌تر
                if (MessagesPanel.Items.Count > 0)
                {
                    var scrollViewer = FindChild<ScrollViewer>(MessagesPanel);
                    scrollViewer?.ScrollToBottom();
                }
            });
        });

        await _client.Start();
        UpdateConnectionStatus(true);
        AddSystemMessage("به چت متصل شدید");
    }

    private async Task DisconnectAsync()
    {
        if (_client == null) return;

        await _client.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "قطع اتصال توسط کاربر");
        _client.Dispose();
        _client = null;
        UpdateConnectionStatus(false);
        AddSystemMessage("از چت خارج شدید");
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        ConnectButton.Content = isConnected ? "قطع اتصال" : "اتصال";
        UsernameTextBox.IsEnabled = !isConnected;
        MessageTextBox.IsEnabled = isConnected;
    }

    private Brush DetermineMessageBackground(string username)
    {
        if (username == _username)
            return new SolidColorBrush(Color.FromRgb(220, 248, 198)); // سبز روشن برای پیام‌های خودمان
        return new SolidColorBrush(Color.FromRgb(240, 240, 240)); // خاکستری روشن برای بقیه
    }

    private void AddSystemMessage(string message)
    {
        _messages.Add(new ChatMessage
        {
            Username = "سیستم",
            Message = message,
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)) // نارنجی روشن برای پیام‌های سیستم
        });
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessageAsync();
    }

    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        if (_client?.IsRunning != true)
        {
            MessageBox.Show("لطفا ابتدا به سرور متصل شوید");
            return;
        }

        var message = MessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        try
        {
            await _sendLock.WaitAsync();
            try
            {
                await Task.Run(() => _client.Send(message));
                MessageTextBox.Clear();
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطا در ارسال پیام: {ex.Message}");
        }
    }

    // متد جدید و ساده‌تر برای پیدا کردن کنترل‌ها
    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T found)
                return found;

            var result = FindChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }
}

public class ChatMessage
{
    public required string Username { get; init; }
    public required string Message { get; init; }
    public required string Time { get; init; }
    public required Brush Background { get; init; }
}