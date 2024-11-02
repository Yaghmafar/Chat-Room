using System;
using System.Windows.Forms;
using WebSocketSharp;

namespace SimpleChatApp
{
    public partial class Form1 : Form
    {
        private WebSocket ws;
        private string username = "Anonymous";

        public Form1()
        {
            InitializeComponent();
            SetupForm();
            ConnectToServer();
        }

        private void SetupForm()
        {
            // فرم اصلی
            this.Text = "Simple Chat";
            this.Size = new System.Drawing.Size(500, 400);

            // تکست باکس چت
            txtChat = new TextBox();
            txtChat.Multiline = true;
            txtChat.ReadOnly = true;
            txtChat.ScrollBars = ScrollBars.Vertical;
            txtChat.Dock = DockStyle.Top;
            txtChat.Height = 300;
            this.Controls.Add(txtChat);

            // پنل پایین
            Panel bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 40;
            this.Controls.Add(bottomPanel);

            // تکست باکس پیام
            txtMessage = new TextBox();
            txtMessage.Width = 380;
            txtMessage.Location = new System.Drawing.Point(10, 10);
            bottomPanel.Controls.Add(txtMessage);

            // دکمه ارسال
            btnSend = new Button();
            btnSend.Text = "Send";
            btnSend.Width = 80;
            btnSend.Location = new System.Drawing.Point(400, 10);
            btnSend.Click += BtnSend_Click;
            bottomPanel.Controls.Add(btnSend);

            // رویداد کلید Enter
            txtMessage.KeyPress += TxtMessage_KeyPress;
        }

        private void ConnectToServer()
        {
            try
            {
                ws = new WebSocket("ws://localhost:8080/ws");

                ws.OnMessage += (sender, e) =>
                {
                    this.Invoke(new Action(() =>
                    {
                        txtChat.AppendText(e.Data + Environment.NewLine);
                    }));
                };

                ws.OnClose += (sender, e) =>
                {
                    this.Invoke(new Action(() =>
                    {
                        txtChat.AppendText("Disconnected from server" + Environment.NewLine);
                        btnSend.Enabled = false;
                    }));
                };

                ws.OnError += (sender, e) =>
                {
                    this.Invoke(new Action(() =>
                    {
                        txtChat.AppendText("Error: " + e.Message + Environment.NewLine);
                    }));
                };

                ws.Connect();
                btnSend.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection error: " + ex.Message);
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text))
                return;

            if (ws?.ReadyState == WebSocketState.Open)
            {
                ws.Send(txtMessage.Text);
                txtMessage.Clear();
            }
            else
            {
                MessageBox.Show("Not connected to server!");
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void TxtMessage_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (ws != null && ws.ReadyState == WebSocketState.Open)
            {
                ws.Close();
            }
        }

        // متغیرهای کنترل‌ها
        private TextBox txtChat;
        private TextBox txtMessage;
        private Button btnSend;
    }
}