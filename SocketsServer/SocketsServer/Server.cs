using System;
using System.Net.Sockets;
using System.Windows.Forms;
using Sockets.Chat;

namespace Sockets
{
    public partial class frmMain : Form
    {
        private ChatServer server;
        private bool closing;

        public frmMain()
        {
            InitializeComponent();
            Text += "     TCP / UDP : " + ChatProtocol.Port;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            server = new ChatServer();
            server.MessageReceived += delegate(string text) { Post(delegate { AppendMessage(text); }); };
            server.ClientsChanged += delegate(string[] names)
            {
                Post(delegate
                {
                    lbClients.BeginUpdate();
                    lbClients.Items.Clear();
                    lbClients.Items.AddRange(names);
                    lbClients.EndUpdate();
                    gbClients.Text = "Участники (" + names.Length + ")";
                });
            };
            try { server.Start(); }
            catch (SocketException ex)
            {
                string text = ex.SocketErrorCode == SocketError.AddressAlreadyInUse ?
                    "Порт 1010 уже занят. На этом компьютере уже работает сервер или другое приложение с тем же портом." :
                    "Не удалось запустить сервер: " + ex.Message;
                AppendMessage(text);
                Text = "Сервер — не запущен";
                MessageBox.Show(this, text, "Запуск сервера", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Post(MethodInvoker action)
        {
            if (closing || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((MethodInvoker)delegate { if (!closing) action(); }); }
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
            if (server != null) server.Dispose();
        }
    }
}
