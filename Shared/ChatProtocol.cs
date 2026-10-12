using System;
using System.IO;
using System.Text;

namespace Sockets.Chat
{
    internal enum MessageType : byte
    {
        Join = 1, Welcome = 2, Chat = 3, Notice = 4, Error = 5
    }

    internal sealed class ChatMessage
    {
        public readonly MessageType Type;
        public readonly string Text;

        public ChatMessage(MessageType type, string text)
        {
            Type = type;
            Text = text;
        }
    }

    internal static class ChatProtocol
    {
        public const int Port = 1010;
        public const int MaxNicknameLength = 24;
        public const int MaxMessageLength = 2000;
        public const int MaxFrameBytes = 16384;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static bool IsValidNickname(string nickname)
        {
            if (String.IsNullOrEmpty(nickname) || nickname != nickname.Trim() ||
                nickname.Length > MaxNicknameLength)
                return false;
            foreach (char c in nickname)
                if (!Char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    return false;
            return true;
        }

        public static bool IsValidMessage(string text)
        {
            if (String.IsNullOrEmpty(text) || text.Trim().Length == 0 || text.Length > MaxMessageLength)
                return false;
            foreach (char c in text)
                if (Char.IsControl(c))
                    return false;
            return true;
        }

        // TCP не сохраняет границы Write: длина (4 байта, big endian), тип, UTF-8.
        public static void Write(Stream stream, MessageType type, string text)
        {
            byte[] body = Utf8.GetBytes(text);
            int length = body.Length + 1;
            if (length > MaxFrameBytes || !IsKnownType(type))
                throw new InvalidDataException("Недопустимый размер или тип сообщения.");
            byte[] frame = new byte[length + 4];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            frame[4] = (byte)type;
            Buffer.BlockCopy(body, 0, frame, 5, body.Length);
            stream.Write(frame, 0, frame.Length);
        }

        public static ChatMessage Read(Stream stream)
        {
            byte[] header = new byte[4];
            int first = stream.ReadByte();
            if (first < 0)
                return null; // Штатное отключение между сообщениями.
            header[0] = (byte)first;
            ReadExactly(stream, header, 1, 3);
            int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            if (length < 1 || length > MaxFrameBytes)
                throw new InvalidDataException("Недопустимая длина сообщения.");
            byte[] body = new byte[length];
            ReadExactly(stream, body, 0, length);
            MessageType type = (MessageType)body[0];
            if (!IsKnownType(type))
                throw new InvalidDataException("Неизвестный тип сообщения.");
            try
            {
                return new ChatMessage(type, Utf8.GetString(body, 1, length - 1));
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException("Повреждённый текст UTF-8.", ex);
            }
        }

        private static bool IsKnownType(MessageType type)
        {
            return type >= MessageType.Join && type <= MessageType.Error;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read == 0)
                    throw new EndOfStreamException("Соединение оборвалось внутри сообщения.");
                offset += read;
                count -= read;
            }
        }
    }
}
