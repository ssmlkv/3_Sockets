using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Sockets.Chat;

namespace Sockets
{
    internal sealed class ChatClient : IDisposable
    {
        private readonly object connectionLock = new object();
        private readonly object sendLock = new object();
        private TcpClient client;
        private NetworkStream stream;
        private volatile bool stopped;
        private volatile bool connected;

        public event Action<string> StatusChanged;
        public event Action<string> Connected;
        public event Action<ChatMessage> MessageReceived;
        public event Action<string> Disconnected;

        public void Connect(string nickname)
        {
            Thread worker = new Thread(delegate() { ConnectAndReceive(nickname); });
            worker.IsBackground = true;
            worker.Start();
        }

        private void ConnectAndReceive(string nickname)
        {
            string reason = "Соединение с сервером закрыто.";
            try
            {
                SetStatus("Поиск сервера в локальной сети…");
                IPEndPoint endpoint = ServerDiscovery.FindServer(delegate { return stopped; });
                if (endpoint == null || stopped) return;
                SetStatus("Подключение к " + endpoint + "…");
                TcpClient socket = new TcpClient(AddressFamily.InterNetwork);
                lock (connectionLock)
                {
                    if (stopped) { socket.Close(); return; }
                    client = socket;
                }
                IAsyncResult pending = socket.BeginConnect(endpoint.Address, endpoint.Port, null, null);
                using (WaitHandle wait = pending.AsyncWaitHandle)
                {
                    if (!wait.WaitOne(4000, false))
                        throw new TimeoutException("Сервер найден, но TCP-подключение не установлено. Проверьте TCP-порт 1010.");
                    socket.EndConnect(pending);
                }
                socket.NoDelay = true;
                socket.SendTimeout = 2000;
                socket.ReceiveTimeout = 5000;
                NetworkStream network = socket.GetStream();
                ChatProtocol.Write(network, MessageType.Join, nickname);
                ChatMessage welcome = ChatProtocol.Read(network);
                if (welcome == null) throw new IOException("Сервер закрыл соединение при входе.");
                if (welcome.Type == MessageType.Error) throw new IOException(welcome.Text);
                if (welcome.Type != MessageType.Welcome || welcome.Text != nickname)
                    throw new InvalidDataException("Сервер прислал неверный ответ при входе.");
                socket.ReceiveTimeout = 0;
                lock (connectionLock)
                {
                    if (stopped) return;
                    stream = network;
                    connected = true;
                }
                Action<string> onConnected = Connected;
                if (onConnected != null) onConnected(endpoint.ToString());
                while (!stopped)
                {
                    ChatMessage message = ChatProtocol.Read(network);
                    if (message == null) break;
                    if (message.Type != MessageType.Chat && message.Type != MessageType.Notice && message.Type != MessageType.Error)
                        throw new InvalidDataException("Неожиданный тип сообщения сервера.");
                    Action<ChatMessage> handler = MessageReceived;
                    if (handler != null) handler(message);
                }
            }
            catch (InvalidDataException ex) { reason = ex.Message; }
            catch (IOException ex) { reason = ex.Message; }
            catch (SocketException ex) { reason = "Ошибка сети: " + ex.Message; }
            catch (TimeoutException ex) { reason = ex.Message; }
            catch (ObjectDisposedException) { }
            finally
            {
                bool notify = !stopped;
                Dispose();
                Action<string> handler = Disconnected;
                if (notify && handler != null) handler(reason);
            }
        }

        public void Send(string text)
        {
            if (!ChatProtocol.IsValidMessage(text))
                throw new ArgumentException("Введите сообщение длиной до 2000 символов.");
            NetworkStream network;
            lock (connectionLock)
            {
                if (!connected || stopped) throw new IOException("Нет подключения к серверу.");
                network = stream;
            }
            lock (sendLock) ChatProtocol.Write(network, MessageType.Chat, text);
        }

        private void SetStatus(string text)
        {
            Action<string> handler = StatusChanged;
            if (handler != null) handler(text);
        }

        public void Dispose()
        {
            lock (connectionLock)
            {
                stopped = true;
                connected = false;
                if (client != null) client.Close();
            }
        }
    }
}
