using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bảng màu của package. Trước đây mỗi trang info của game tự khai lại bộ này.
    public static class Palette
    {
        public const string GOOD = "#5FD068";
        public const string WARN = "#E8B04B";
        public const string BAD = "#E5484D";
        public const string DIM = "#A7B6CA";

        internal const string FAVORITE = "#F4C96B";
        internal const string MUTED = "#91A4BC";

        public static string Wrap(string text, TextStyle style) => style switch
        {
            TextStyle.Note => $"<size=85%><color={DIM}>{text}</color></size>",
            TextStyle.Good => $"<color={GOOD}>{text}</color>",
            TextStyle.Warn => $"<color={WARN}>{text}</color>",
            TextStyle.Bad => $"<color={BAD}>{text}</color>",
            _ => text,
        };

        internal static Color ToColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
