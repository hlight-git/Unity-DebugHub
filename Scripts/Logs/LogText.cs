using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Chữ của trang log. Chỉ dùng ký tự ngoài chữ cái mà font game chắc có (`› ‹ … – — ×`).
    internal static class LogText
    {
        internal const int ROW_CHARS = 300;
        internal const int DETAIL_CHARS = 4000;
        private const string MARK = "<mark=#E8B04B66>";

        /// Dữ liệu game qua TMP: `<noparse>`, và mọi `</` trong chính dữ liệu bị chèn zero-width space.
        /// TMP đóng tag theo tên viết hoa, dừng ở `>`, `=` hoặc khoảng trắng nên `</NoParse>`, `</noparse >`,
        /// `</noparse=x>` đều đóng được noparse: phá cả `</` thì không biến thể hoa/thường hay khoảng trắng nào đóng nổi.
        internal static string Escape(string text) =>
            "<noparse>" + (text ?? string.Empty).Replace("</", "<\u200B/") + "</noparse>";

        /// Escape + tô các đoạn khớp (không phân biệt hoa thường). Tag mark nằm ngoài noparse nên vẫn có tác dụng.
        internal static string Highlight(string text, string query)
        {
            text ??= string.Empty;
            if (string.IsNullOrEmpty(query)) return Escape(text);

            var builder = new StringBuilder(text.Length + 64);
            var start = 0;
            while (true)
            {
                var hit = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                if (hit < 0) break;
                if (hit > start) builder.Append(Escape(text.Substring(start, hit - start)));
                builder.Append(MARK).Append(Escape(text.Substring(hit, query.Length))).Append("</mark>");
                start = hit + query.Length;
            }
            if (start < text.Length) builder.Append(Escape(text.Substring(start)));
            return builder.ToString();
        }

        /// Log đã tách tag rich text: chữ nhìn thấy (tìm, Copy, cắt độ dài đều trên chuỗi này) và tag TMP chèn
        /// trước ký tự thứ At của nó.
        internal sealed class Rich
        {
            public readonly string Plain;
            public readonly (int At, string Tag)[] Tags;

            public Rich(string plain, (int, string)[] tags)
            {
                Plain = plain;
                Tags = tags;
            }
        }

        private static readonly (int, string)[] NoTags = Array.Empty<(int, string)>();

        /// Đúng bộ tag Unity console vẽ được. Các tag khác (`<link>`, `<sprite>`, `List<int>`…) console hiện
        /// nguyên chữ, ở đây cũng vậy.
        private static readonly Regex ConsoleTag =
            new(@"<(/?)(b|i|color|size|material|quad)\b([^<>]*)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// Hiện rich text như Unity console: b/i/color giữ, size/material/quad bỏ (hàng log cao cố định). Log không
        /// có `<` (gần như mọi log) không qua regex và giữ nguyên chuỗi.
        internal static Rich Parse(string text)
        {
            text ??= string.Empty;
            if (text.IndexOf('<') < 0) return new Rich(text, NoTags);

            StringBuilder plain = null;
            List<(int, string)> tags = null;
            var last = 0;
            foreach (Match match in ConsoleTag.Matches(text))
            {
                var tag = TmpTagOf(match);
                if (tag == null) continue;
                plain ??= new StringBuilder(text.Length);
                plain.Append(text, last, match.Index - last);
                last = match.Index + match.Length;
                if (tag.Length > 0) (tags ??= new List<(int, string)>()).Add((plain.Length, tag));
            }
            if (plain == null) return new Rich(text, NoTags);
            plain.Append(text, last, text.Length - last);
            return new Rich(plain.ToString(), tags?.ToArray() ?? NoTags);
        }

        /// null = không phải tag console, để nguyên là chữ; "" = tag console nhưng bỏ.
        private static string TmpTagOf(Match match)
        {
            var closing = match.Groups[1].Length > 0;
            var name = match.Groups[2].Value.ToLowerInvariant();
            var rest = match.Groups[3].Value;
            switch (name)
            {
                case "b":
                case "i":
                    return rest.Length > 0 ? null : closing ? $"</{name}>" : $"<{name}>";
                case "color":
                    if (closing) return rest.Length > 0 ? null : "</color>";
                    // TMP không biết tên màu kiểu Unity (lime, aqua…): đổi ra mã.
                    return rest.StartsWith("=", StringComparison.Ordinal) &&
                           ColorUtility.TryParseHtmlString(rest.Substring(1).Trim().Trim('"', '\''), out var color)
                        ? $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>"
                        : null;
                default:
                    return string.Empty;
            }
        }

        /// Chuỗi TMP cho tối đa <paramref name="limit"/> ký tự nhìn thấy (hàng log chỉ hiện 2 dòng: đưa cả chuỗi
        /// 50 KB vào TMP là dựng mesh cho 50 KB rồi mới cắt). Có query thì tô các đoạn khớp, kể cả đoạn vắt qua tag.
        internal static string Render(Rich rich, string query, int limit)
        {
            var plain = rich.Plain;
            var end = Math.Min(plain.Length, limit);
            if (rich.Tags.Length == 0) return Highlight(plain.Substring(0, end), query);

            var tags = rich.Tags;
            var searching = !string.IsNullOrEmpty(query);
            var builder = new StringBuilder(end + 32 * tags.Length);
            int pos = 0, t = 0, markEnd = -1;
            var hit = Find(plain, query, 0, end, searching);
            while (true)
            {
                while (t < tags.Length && tags[t].At <= pos) builder.Append(tags[t++].Tag);
                if (markEnd == pos)
                {
                    builder.Append("</mark>");
                    markEnd = -1;
                    hit = Find(plain, query, pos, end, searching);
                }
                if (markEnd < 0 && hit == pos)
                {
                    builder.Append(MARK);
                    markEnd = pos + query.Length;
                }
                if (pos >= end) break;

                var stop = end;
                if (t < tags.Length && tags[t].At < stop) stop = tags[t].At;
                if (markEnd >= 0 && markEnd < stop) stop = markEnd;
                if (markEnd < 0 && hit >= 0 && hit < stop) stop = hit;
                builder.Append(Escape(plain.Substring(pos, stop - pos)));
                pos = stop;
            }
            return builder.ToString();
        }

        /// Chỉ đoạn khớp nằm trọn trong phần hiện: đoạn bị cắt ngang ở cuối không tô.
        private static int Find(string plain, string query, int from, int end, bool searching) =>
            searching && from < end ? plain.IndexOf(query, from, end - from, StringComparison.OrdinalIgnoreCase) : -1;

        internal static string Time(DateTime time) => time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

        internal static string TypeLabel(LogType type) => type switch
        {
            LogType.Log => "Log",
            LogType.Warning => "Cảnh báo",
            _ => "Lỗi",
        };

        internal static string ColorOf(LogType type) => type switch
        {
            LogType.Log => Palette.DIM,
            LogType.Warning => Palette.WARN,
            _ => Palette.BAD,
        };

        /// 1204 → "1.204". Tự định dạng, không dựa vào dữ liệu culture vi-VN (IL2CPP có thể không mang theo).
        internal static string Count(long n)
        {
            var digits = n.ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= 3) return digits;
            var builder = new StringBuilder(digits.Length + digits.Length / 3);
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) builder.Append('.');
                builder.Append(digits[i]);
            }
            return builder.ToString();
        }

        /// Chấm đỏ: chỗ nhỏ, quá 99 thì "99+".
        internal static string Badge(long n) => n > 99 ? "99+" : n.ToString(CultureInfo.InvariantCulture);
    }
}
