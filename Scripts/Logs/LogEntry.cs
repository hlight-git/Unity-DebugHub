using System;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    internal enum LogKind { Log, Command }

    /// Native cho spec ② (logcat) / ③ (OSLogStore): cùng ring, ghép theo Time.
    internal enum LogSource { Unity, Native }

    internal readonly struct LogEntry
    {
        public readonly long Seq;
        public readonly DateTime Time;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string Stack;
        public readonly LogKind Kind;
        public readonly LogSource Source;

        public LogEntry(long seq, DateTime time, LogType type, string message, string stack, LogKind kind, LogSource source)
        {
            Seq = seq;
            Time = time;
            Type = type;
            Message = message;
            Stack = stack;
            Kind = kind;
            Source = source;
        }

        public bool IsError => Type == LogType.Error || Type == LogType.Exception || Type == LogType.Assert;

        /// Byte giữ trong RAM: chuỗi .NET là UTF-16, cộng 64 cho chính entry — log rỗng cũng phải tốn chỗ,
        /// không thì ring nở vô hạn.
        public int Cost => 64 + ((Message?.Length ?? 0) + (Stack?.Length ?? 0)) * 2;
    }
}
