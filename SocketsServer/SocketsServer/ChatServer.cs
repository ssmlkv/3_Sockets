using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Sockets.Chat;

namespace Sockets
{
    // Сетевая часть не зависит от формы: её можно проверить настоящими TCP-клиентами.
    internal sealed class ChatServer : IDisposable
    {
        private sealed class Peer
        {
            public readonly TcpClient Client;
            public readonly NetworkStream Stream;
            public readonly object SendLock = new object();
            public Thread Worker;
            public string Nickname;

            public Peer(TcpClient client)
            {
                Client = client;
                Stream = client.GetStream();
            }
        }

        private readonly object clientsLock = new object();
        private readonly object broadcastLock = new object();
        private readonly List<Peer> clients = new List<Peer>();
        private TcpListener listener;
        private UdpClient discovery;
        private Thread acceptThread;
        private Thread discoveryThread;
        private volatile bool running;

        public event Action<string> MessageReceived;
        public event Action<string[]> ClientsChanged;

        public void Start()
        {
            if (listener != null)
                throw new InvalidOperationException("Этот экземпляр сервера уже запускался.");
            try
            {
                listener = new TcpListener(IPAddress.Any, ChatProtocol.Port);
                listener.ExclusiveAddressUse = true;
                listener.Start();
                discovery = new UdpClient(AddressFamily.InterNetwork);
                discovery.ExclusiveAddressUse = true;
                // Конечное ожидание позволяет остановить UDP-поток и на ОС,
                // где Close не прерывает блокирующий Receive немедленно.
                discovery.Client.ReceiveTimeout = 200;
                discovery.Client.Bind(new IPEndPoint(IPAddress.Any, ChatProtocol.Port));
                running = true;
                acceptThread = new Thread(AcceptClients);
                discoveryThread = new Thread(AnswerDiscovery);
                acceptThread.IsBackground = true;
                discoveryThread.IsBackground = true;
                acceptThread.Start();
                discoveryThread.Start();
                Log("Сервер запущен. TCP / UDP: " + ChatProtocol.Port + ".");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void AcceptClients()
        {
            while (running)
            {
                try
                {
                    TcpClient socket = listener.AcceptTcpClient();
                    socket.NoDelay = true;
                    socket.SendTimeout = 2000;
                    socket.ReceiveTimeout = 5000; // Не держим незарегистрированные подключения бесконечно.
                    Peer peer = new Peer(socket);
                    lock (clientsLock)
                    {
                        if (!running || clients.Count >= 64)
                        {
                            socket.Close();
                            continue;
                        }
                        clients.Add(peer);
                        peer.Worker = new Thread(ReadMessages);
                        peer.Worker.IsBackground = true;
                        peer.Worker.Start(peer);
                    }
                }
                catch (SocketException ex)
                {
                    if (running) Log("Ошибка приёма подключения: " + ex.Message);
                }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void AnswerDiscovery()
        {
            while (running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] request = discovery.Receive(ref sender);
                    byte[] response = ServerDiscovery.GetResponse(request);
                    if (response != null)
                        discovery.Send(response, response.Length, sender);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut || ex.SocketErrorCode == SocketError.WouldBlock)
                        continue;
                    if (running) Log("Ошибка UDP: " + ex.Message);
                }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void ReadMessages(object state)
        {
            Peer peer = (Peer)state;
            try
            {
                ChatMessage join = ChatProtocol.Read(peer.Stream);
                if (join == null) return;
                if (join.Type != MessageType.Join || !ChatProtocol.IsValidNickname(join.Text))
                {
                    Send(peer, MessageType.Error, "Ник: 1–24 буквы, цифры или символы _ - .");
                    return;
                }
                lock (broadcastLock)
                {
                    bool duplicate = false;
                    lock (clientsLock)
                    {
                        foreach (Peer other in clients)
                            if (String.Equals(other.Nickname, join.Text, StringComparison.OrdinalIgnoreCase))
                                duplicate = true;
                    }
                    if (duplicate)
                    {
                        Send(peer, MessageType.Error, "Этот ник уже занят. Выберите другой.");
                        return;
                    }
                    if (!running || !Send(peer, MessageType.Welcome, join.Text)) return;
                    lock (clientsLock) peer.Nickname = join.Text;
                    peer.Client.ReceiveTimeout = 0;
                    Broadcast(MessageType.Notice, peer.Nickname + " присоединился к беседе.");
                    NotifyClients();
                }
                while (running)
                {
                    ChatMessage message = ChatProtocol.Read(peer.Stream);
                    if (message == null) break;
                    if (message.Type != MessageType.Chat || !ChatProtocol.IsValidMessage(message.Text))
                    {
                        Send(peer, MessageType.Error, "Сообщение должно содержать от 1 до 2000 символов без управляющих знаков.");
                        continue;
                    }
                    // Автор определяется по зарегистрированному сокету, а не по тексту клиента.
                    lock (broadcastLock)
                        Broadcast(MessageType.Chat, peer.Nickname + " >> " + message.Text);
                }
            }
            catch (InvalidDataException) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                peer.Client.Close();
                lock (broadcastLock)
                {
                    bool removed;
                    lock (clientsLock) removed = clients.Remove(peer);
                    if (removed && peer.Nickname != null && running)
                    {
                        Broadcast(MessageType.Notice, peer.Nickname + " покинул беседу.");
                        NotifyClients();
                    }
                }
            }
        }

        // Только один поток пишет кадр в конкретный NetworkStream в каждый момент.
        private bool Send(Peer peer, MessageType type, string text)
        {
            try
            {
                lock (peer.SendLock) ChatProtocol.Write(peer.Stream, type, text);
                return true;
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            peer.Client.Close();
            return false;
        }

        // broadcastLock сохраняет одинаковую последовательность рассылки для всех клиентов.
        private void Broadcast(MessageType type, string text)
        {
            Peer[] snapshot;
            lock (clientsLock) snapshot = clients.ToArray();
            foreach (Peer peer in snapshot)
                if (peer.Nickname != null) Send(peer, type, text);
            Log(text);
        }

        private void NotifyClients()
        {
            List<string> names = new List<string>();
            lock (clientsLock)
                foreach (Peer peer in clients)
                    if (peer.Nickname != null) names.Add(peer.Nickname);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            Action<string[]> handler = ClientsChanged;
            if (handler != null) handler(names.ToArray());
        }

        private void Log(string text)
        {
            Action<string> handler = MessageReceived;
            if (handler != null) handler(text);
        }

        public void Dispose()
        {
            running = false;
            if (listener != null) listener.Stop();
            if (discovery != null) discovery.Close();
            Peer[] snapshot;
            lock (clientsLock)
            {
                snapshot = clients.ToArray();
                clients.Clear();
            }
            // Close разблокирует Accept/Receive/Read. Thread.Abort не требуется.
            foreach (Peer peer in snapshot) peer.Client.Close();
            Join(acceptThread);
            Join(discoveryThread);
            foreach (Peer peer in snapshot) Join(peer.Worker);
        }

        private static void Join(Thread thread)
        {
            if (thread != null && thread != Thread.CurrentThread && thread.IsAlive)
                thread.Join(500);
        }
    }
}
