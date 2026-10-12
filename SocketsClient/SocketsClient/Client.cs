using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Sockets.Chat;

namespace Sockets
{
    public partial class frmMain : Form
    {
        private ChatClient client;
        private bool closing;

        public frmMain()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
                SetDisconnected("Отключено.");
                AppendMessage("Вы отключились от беседы.");
                return;
            }
            string nickname = tbNickname.Text.Trim();
            if (!ChatProtocol.IsValidNickname(nickname))
            {
                MessageBox.Show(this, "Введите ник: от 1 до 24 букв, цифр или символов _ - .", "Ник пользователя",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                tbNickname.Focus();
                return;
            }
            tbNickname.Text = nickname;
            tbNickname.Enabled = false;
            btnConnect.Text = "Отменить";
            lblStatus.Text = "Поиск сервера…";
            ChatClient session = new ChatClient();
            client = session;
            session.StatusChanged += delegate(string text) { Post(session, delegate { lblStatus.Text = text; }); };
            session.Connected += delegate(string address)
            {
                Post(session, delegate
                {
                    Text = "Клиент     " + nickname;
                    btnConnect.Text = "Отключиться";
                    btnSend.Enabled = true;
                    tbMessage.Enabled = true;
                    lblStatus.Text = "Сервер: " + address;
                    AppendMessage("Вы вошли в беседу как " + nickname + ".");
                    tbMessage.Focus();
                });
            };
            session.MessageReceived += delegate(ChatMessage message)
            {
                Post(session, delegate
                {
                    AppendMessage(message.Type == MessageType.Error ? "Ошибка: " + message.Text : message.Text);
                });
            };
            session.Disconnected += delegate(string reason)
            {
                Post(session, delegate
                {
                    client = null;
                    SetDisconnected(reason);
                    AppendMessage(reason);
                });
            };
            session.Connect(nickname);
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (client == null || !btnSend.Enabled) return;
            string text = tbMessage.Text;
            if (!ChatProtocol.IsValidMessage(text))
            {
                MessageBox.Show(this, "Введите сообщение от 1 до 2000 символов без управляющих знаков.", "Сообщение",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ChatClient session = client;
            btnSend.Enabled = false;
            // Даже при недоступном сервере ожидание записи не блокирует окно.
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try { session.Send(text); }
                catch (IOException ex) { error = ex.Message; }
                catch (SocketException ex) { error = ex.Message; }
                catch (ObjectDisposedException) { error = "Соединение закрыто."; }
                catch (ArgumentException ex) { error = ex.Message; }
                Post(session, delegate
                {
                    if (error != null)
                    {
                        session.Dispose();
                        client = null;
                        SetDisconnected("Сообщение не отправлено.");
                        AppendMessage("Сообщение не отправлено: " + error);
                        return;
                    }
                    if (tbMessage.Text == text) tbMessage.Clear();
                    btnSend.Enabled = true;
                    tbMessage.Focus();
                });
            });
        }

        private void tbMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnSend_Click(sender, EventArgs.Empty);
            }
        }

        private void SetDisconnected(string status)
        {
            Text = "Клиент";
            btnConnect.Text = "Подключиться";
            tbNickname.Enabled = true;
            btnSend.Enabled = false;
            tbMessage.Enabled = false;
            lblStatus.Text = status;
        }

        private void Post(ChatClient session, MethodInvoker action)
        {
            if (closing || IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!closing && client == session) action();
                });
            }
            catch (InvalidOperationException) { }
        }

        private void AppendMessage(string text)
        {
            if (rtbMessages.TextLength > 100000) rtbMessages.Clear();
            rtbMessages.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
            rtbMessages.SelectionStart = rtbMessages.TextLength;
            rtbMessages.ScrollToCaret();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            closing = true;
            if (client != null) client.Dispose();
        }
    }
}
