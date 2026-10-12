using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Sockets.Chat
{
    internal static class ServerDiscovery
    {
        private const string RequestPrefix = "SOCKETS_CHAT_DISCOVER_V1|";
        private const string ResponsePrefix = "SOCKETS_CHAT_SERVER_V1|";

        public static byte[] GetResponse(byte[] request)
        {
            string text = Encoding.ASCII.GetString(request);
            if (!text.StartsWith(RequestPrefix, StringComparison.Ordinal))
                return null;
            string nonce = text.Substring(RequestPrefix.Length);
            if (nonce.Length != 32)
                return null;
            foreach (char c in nonce)
                if (!Uri.IsHexDigit(c))
                    return null;
            return Encoding.ASCII.GetBytes(ResponsePrefix + nonce);
        }

        public static IPEndPoint FindServer(Func<bool> cancelled)
        {
            string nonce = Guid.NewGuid().ToString("N");
            byte[] request = Encoding.ASCII.GetBytes(RequestPrefix + nonce);
            string expected = ResponsePrefix + nonce;
            List<IPAddress> targets = GetBroadcastAddresses();
            using (UdpClient udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
            {
                udp.EnableBroadcast = true;
                udp.Client.ReceiveTimeout = 200;
                Stopwatch elapsed = Stopwatch.StartNew();
                long nextSend = 0;
                while (!cancelled() && elapsed.ElapsedMilliseconds < 4000)
                {
                    if (elapsed.ElapsedMilliseconds >= nextSend)
                    {
                        foreach (IPAddress address in targets)
                        {
                            try { udp.Send(request, request.Length, new IPEndPoint(address, ChatProtocol.Port)); }
                            catch (SocketException) { /* Другие интерфейсы и loopback ещё могут работать. */ }
                        }
                        nextSend = elapsed.ElapsedMilliseconds + 1000;
                    }
                    try
                    {
                        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        byte[] response = udp.Receive(ref sender);
                        if (sender.Port == ChatProtocol.Port && Encoding.ASCII.GetString(response) == expected)
                            return new IPEndPoint(sender.Address, ChatProtocol.Port);
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut &&
                            ex.SocketErrorCode != SocketError.WouldBlock &&
                            ex.SocketErrorCode != SocketError.ConnectionReset)
                            throw;
                    }
                }
            }
            if (cancelled())
                return null;
            throw new TimeoutException("Сервер не найден. Запустите сервер и проверьте локальную сеть и доступ к UDP-порту 1010.");
        }

        private static List<IPAddress> GetBroadcastAddresses()
        {
            List<IPAddress> result = new List<IPAddress>();
            result.Add(IPAddress.Broadcast);
            // Направленный broadcast помогает при нескольких сетевых адаптерах.
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (UnicastIPAddressInformation info in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork || info.IPv4Mask == null)
                            continue;
                        byte[] address = info.Address.GetAddressBytes();
                        byte[] mask = info.IPv4Mask.GetAddressBytes();
                        for (int i = 0; i < address.Length; i++)
                            address[i] = (byte)(address[i] | (mask[i] ^ 255));
                        IPAddress broadcast = new IPAddress(address);
                        if (!result.Contains(broadcast))
                            result.Add(broadcast);
                    }
                }
            }
            catch (NetworkInformationException) { }
            // Для запуска на одной машине, в том числе без активной ЛВС.
            result.Add(IPAddress.Loopback);
            return result;
        }
    }
}
