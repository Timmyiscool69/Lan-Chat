using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;

namespace cool  // ← Change this to match your actual project name if different
{
    public partial class Form1 : Form
    {
        private const int DiscoveryPort = 55001;
        private const int MessagePort   = 55002;

        private UdpClient? udpClient;
        private TcpListener? tcpListener;

        private string username = Environment.UserName;
        private bool showNotifications = true;
        private readonly object peersLock = new object();
        private readonly HashSet<IPEndPoint> knownPeers = new HashSet<IPEndPoint>();
        private readonly List<IPEndPoint> localEndpoints = new List<IPEndPoint>();

        private TextBox? txtChat;
        private TextBox? txtInput;
        private Button? btnSend;
        private Button? btnSettings;

        public Form1()
        {
            InitializeComponent();

            // Form appearance
            this.Text = "LAN Chat";
            this.Size = new Size(520, 680);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 42);
            this.Font = new Font("Segoe UI", 10);

            // Top panel with settings button
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(35, 35, 48),
                Padding = new Padding(8, 6, 8, 6)
            };

            btnSettings = new Button
            {
                Text = "⚙",
                Width = 34,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.FromArgb(200, 220, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = System.Windows.Forms.Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += BtnSettings_Click;
            btnSettings.MouseEnter += (s, e) => btnSettings.BackColor = Color.FromArgb(60, 60, 70);
            btnSettings.MouseLeave += (s, e) => btnSettings.BackColor = Color.FromArgb(45, 45, 55);
            topPanel.Controls.Add(btnSettings);

            // Chat history display
            txtChat = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 38, 50),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(12),
                Font = new Font("Segoe UI", 10.5f)
            };

            // Input panel
            var inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(35, 35, 48)
            };

            txtInput = new TextBox
            {
                Location = new Point(12, 12),
                Size = new Size(360, 32),
                BackColor = Color.FromArgb(48, 48, 62),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11)
            };
            txtInput!.KeyDown += TxtInput_KeyDown;

            btnSend = new Button
            {
                Text = "Send",
                Location = new Point(380, 12),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnSend!.Click += BtnSend_Click;

            inputPanel.Controls.Add(txtInput);
            inputPanel.Controls.Add(btnSend);

            // Add controls in order: top, bottom, then fill so docking layouts immediately
            SuspendLayout();
            this.Controls.Add(topPanel);
            this.Controls.Add(inputPanel);
            this.Controls.Add(txtChat);
            ResumeLayout();

            this.Text = $"LAN Chat – {username}";
            // Defer initial welcome message until the form is shown so it renders correctly
            this.Shown += Form1_Shown;
            StartNetworking();
        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
            AppendChatLine($"Welcome, {username}! Searching for others on the local network...");
            // ensure chat control updates immediately
            txtChat?.Refresh();
            // Add a short startup "user" message to force the chat area to render
            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                AppendChatLine("[TestUser] Hello — testing initial message display.");
            });
        }

        private void StartNetworking()
        {
            try
            {
                // Cache local endpoints to avoid sending to self
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork ||
                        ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        localEndpoints.Add(new IPEndPoint(ip, MessagePort));
                    }
                }
            }
            catch { }

            udpClient = new UdpClient(DiscoveryPort);
            tcpListener = new TcpListener(IPAddress.Any, MessagePort);

            udpClient.EnableBroadcast = true;

            _ = Task.Run(AnnouncePresenceAsync);
            _ = Task.Run(ReceiveDiscoveryAsync);
            _ = Task.Run(ReceiveMessagesAsync);
        }

        private async Task AnnouncePresenceAsync()
        {
            var ping = Encoding.UTF8.GetBytes("LANCHAT_PING");
            var broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

            while (true)
            {
                try { await udpClient!.SendAsync(ping, ping.Length, broadcastEp); }
                catch { }
                await Task.Delay(7000);
            }
        }

        private async Task ReceiveDiscoveryAsync()
        {
            while (true)
            {
                try
                {
                    var result = await udpClient!.ReceiveAsync();
                    var msg = Encoding.UTF8.GetString(result.Buffer);

                    if (msg == "LANCHAT_PING")
                    {
                        lock (peersLock)
                            knownPeers.Add(result.RemoteEndPoint);
                    }
                }
                catch { await Task.Delay(50); }
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            tcpListener!.Start();

            while (true)
            {
                try
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await using var stream = client.GetStream();
                            var buffer = new byte[8192];
                            int read;

                            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                            {
                                var text = Encoding.UTF8.GetString(buffer, 0, read);
                                AppendChatLine(text);
                                ShowToast("New message", text);
                            }
                        }
                        catch { }
                        finally { client.Dispose(); }
                    });
                }
                catch { await Task.Delay(100); }
            }
        }

        private async void BtnSend_Click(object? sender, EventArgs e) => await SendMessage();

        private async void TxtInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var text = txtInput!.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            var fullMessage = $"[{username}] {text}";

            lock (peersLock)
            {
                foreach (var peer in knownPeers)
                {
                    if (localEndpoints.Exists(le => le.Address.Equals(peer.Address)))
                        continue;

                    _ = SendToPeerAsync(peer, fullMessage);
                }
            }

            AppendChatLine(fullMessage);
            ShowToast("Message sent", $"[{username}] {text}");
            txtInput.Clear();
        }

        private async Task SendToPeerAsync(IPEndPoint peer, string message)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(peer.Address, MessagePort);
                await using var stream = tcp.GetStream();
                var bytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(bytes);
            }
            catch { }
        }

        private void AppendChatLine(string line)
        {
            if (InvokeRequired)
            {
                Invoke(() => AppendChatLine(line));
                return;
            }

            txtChat!.AppendText(line + Environment.NewLine);
            txtChat!.SelectionStart = txtChat!.Text.Length;
            txtChat!.ScrollToCaret();
        }

        private void ShowToast(string title, string body)
        {
            if (!showNotifications)
                return;

            try
            {
                // Escape text for XML
                var escapedTitle = System.Security.SecurityElement.Escape(title);
                var escapedBody = System.Security.SecurityElement.Escape(body);

                // PowerShell command to show Windows Toast Notification
                var psCommand = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
[Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null

$APP_ID = 'Lan Chat'
$template = @'
<toast>
  <visual>
    <binding template='ToastText02'>
      <text id='1'>{escapedTitle}</text>
      <text id='2'>{escapedBody}</text>
    </binding>
  </visual>
</toast>
'@

$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = New-Object Windows.UI.Notifications.ToastNotification $xml
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($APP_ID).Show($toast);
";

                // Run PowerShell command silently
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    proc?.WaitForExit(5000);
                }
            }
            catch { /* Toast failed, message already in chat */ }
        }

        private bool IsValidUsername(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            name = name.Trim();

            // Check length: at least 3 and less than 20
            if (name.Length < 3 || name.Length >= 20)
                return false;

            // Only allow alphanumeric and spaces
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ')
                    return false;
            }

            return true;
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            // Create settings form
            var settingsForm = new Form
            {
                Text = "Settings",
                Size = new Size(350, 250),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10)
            };

            // Notification checkbox
            var chkNotifications = new CheckBox
            {
                Text = "Enable Windows Notifications",
                Location = new Point(20, 20),
                Size = new Size(300, 30),
                Checked = showNotifications,
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10)
            };
            settingsForm.Controls.Add(chkNotifications);

            // Name label
            var lblName = new Label
            {
                Text = "Change Name (3-19 chars, letters/numbers/spaces only):",
                Location = new Point(20, 70),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9)
            };
            settingsForm.Controls.Add(lblName);

            // Name textbox
            var txtName = new TextBox
            {
                Location = new Point(20, 105),
                Size = new Size(300, 32),
                Text = username,
                BackColor = Color.FromArgb(48, 48, 62),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            settingsForm.Controls.Add(txtName);

            // Error label
            var lblError = new Label
            {
                Location = new Point(20, 140),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9),
                Visible = false
            };
            settingsForm.Controls.Add(lblError);

            // OK button
            var btnOK = new Button
            {
                Text = "OK",
                Location = new Point(170, 175),
                Size = new Size(75, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += (s, e) =>
            {
                string newName = txtName.Text.Trim();
                if (!IsValidUsername(newName))
                {
                    lblError.Text = "Invalid name! Use 3-19 chars, letters/numbers/spaces only.";
                    lblError.Visible = true;
                    return;
                }

                showNotifications = chkNotifications.Checked;
                if (newName != username)
                {
                    username = newName;
                    this.Text = $"LAN Chat – {username}";
                    AppendChatLine($"Name changed to: {username}");
                }

                settingsForm.DialogResult = DialogResult.OK;
                settingsForm.Close();
            };
            settingsForm.Controls.Add(btnOK);

            // Cancel button
            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(255, 175),
                Size = new Size(75, 32),
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => settingsForm.Close();
            settingsForm.Controls.Add(btnCancel);

            settingsForm.ShowDialog(this);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}