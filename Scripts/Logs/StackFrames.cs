using System;
using System.Collections.Generic;
using System.Text;

namespace Hlight.Debug.Hub
{
    /// Đọc stack trace của Unity. Hai dạng:
    /// log `Ns.Type:Method (Args) (at Assets/X.cs:12)`, exception `Ns.Type.Method (Args) (at Assets/X.cs:12)`.
    /// IL2CPP release (mặc định) chỉ có tên method, không có `(at …)`.
    internal static class StackFrames
    {
        internal readonly struct Frame
        {
            public readonly string Qualified;   // "Harvest.Gameplay.ItemPiece:OnPointerUp"
            public readonly string Method;      // "ItemPiece.OnPointerUp (PointerEventData)" — kiểu đối số đã rút gọn, để hiển thị
            public readonly string Location;    // "ItemPiece.cs:212" hoặc ""
            public readonly bool Game;          // false = engine/thư viện/lớp log → tô mờ
            public readonly string Caller;      // "ItemPiece.OnPointerUp:212"

            public Frame(string qualified, string method, string location, bool game, string caller)
            {
                Qualified = qualified;
                Method = method;
                Location = location;
                Game = game;
                Caller = caller;
            }
        }

        /// Frame của chính lớp log (Debug.Log, lớp bọc com.hlight.logging): không phải "nơi gọi". Log, LogTag,
        /// LogAlways nằm ở global namespace.
        private static readonly string[] LoggingPrefixes =
        {
            "UnityEngine.Debug", "UnityEngine.Logger", "Hlight.Logging.",
            "Log:", "Log.", "LogTag:", "LogTag.", "LogAlways:", "LogAlways.",
        };

        private static readonly string[] EnginePrefixes = { "UnityEngine.", "UnityEditor.", "System.", "Unity.", "Mono.", "TMPro." };

        internal static List<Frame> Split(string stack)
        {
            var frames = new List<Frame>();
            if (string.IsNullOrEmpty(stack)) return frames;
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0) frames.Add(Parse(line));
            }
            return frames;
        }

        /// Frame đầu tiên thuộc code game (spec §5.2). Stack không có frame game nào (log từ engine thuần)
        /// thì lấy frame đầu tiên không thuộc lớp log. null = không có stack.
        internal static string Caller(string stack)
        {
            var frames = Split(stack);
            foreach (var frame in frames)
            {
                if (frame.Game) return frame.Caller;
            }
            foreach (var frame in frames)
            {
                if (!StartsWithAny(frame.Qualified, LoggingPrefixes)) return frame.Caller;
            }
            return null;
        }

        private static Frame Parse(string line)
        {
            var head = line;
            var location = string.Empty;
            var at = line.LastIndexOf("(at ", StringComparison.Ordinal);
            if (at >= 0 && line.EndsWith(")", StringComparison.Ordinal))
            {
                var path = line.Substring(at + 4, line.Length - at - 5);
                var slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
                location = slash >= 0 ? path.Substring(slash + 1) : path;
                head = line.Substring(0, at).TrimEnd();
            }

            var paren = head.IndexOf('(');
            var qualified = (paren >= 0 ? head.Substring(0, paren) : head).Trim();
            var arguments = paren >= 0 ? ShortArguments(head.Substring(paren).Trim()) : string.Empty;
            var name = ShortName(qualified);

            var colon = location.LastIndexOf(':');
            var caller = colon >= 0 ? $"{name}:{location.Substring(colon + 1)}" : name;
            var game = !StartsWithAny(qualified, LoggingPrefixes) && !StartsWithAny(qualified, EnginePrefixes);
            return new Frame(qualified, $"{name} {arguments}".TrimEnd(), location, game, caller);
        }

        /// "(UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.BaseEventData)" →
        /// "(IPointerUpHandler,\u200BBaseEventData)". Tên kiểu đầy đủ là một cụm không khoảng trắng, TMP ngắt giữa chữ;
        /// chỉ giữ đoạn cuối và chừa chỗ ngắt sau mỗi dấu phẩy. Chỉ để hiển thị — Qualified/Caller/Copy dùng chuỗi gốc.
        private static string ShortArguments(string arguments)
        {
            var builder = new StringBuilder(arguments.Length);
            var segment = 0;
            foreach (var c in arguments)
            {
                // Gặp dấu chấm thì bỏ đoạn vừa chép: còn lại luôn là đoạn cuối của tên có chấm.
                if (c == '.')
                {
                    builder.Length = segment;
                    continue;
                }
                builder.Append(c);
                if (char.IsLetterOrDigit(c) || c == '_' || c == '`') continue;
                if (c == ',') builder.Append('\u200B');
                segment = builder.Length;
            }
            return builder.ToString();
        }

        /// "Harvest.Gameplay.ItemPiece:OnPointerUp" / "Harvest.Gameplay.ItemPiece.OnPointerUp" → "ItemPiece.OnPointerUp".
        private static string ShortName(string qualified)
        {
            var split = qualified.LastIndexOf(':');
            if (split < 0) split = qualified.LastIndexOf('.');
            if (split < 0) return qualified;
            var type = qualified.Substring(0, split);
            var method = qualified.Substring(split + 1);
            var dot = type.LastIndexOf('.');
            return (dot >= 0 ? type.Substring(dot + 1) : type) + "." + method;
        }

        private static bool StartsWithAny(string text, string[] prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (text.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
