using System.Globalization;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Trang chi tiết một log (spec ① §5.4): đọc lỗi — nội dung đầy đủ, stack từng frame (frame game sáng,
    /// engine mờ), một nút chính Copy, Trước/Sau đi qua các log của bộ lọc hiện tại.
    internal static class LogDetailPage
    {
        private const string ENGINE_FRAME = "#6F7F96";

        /// Mỗi frame là một row (~0,9 ms) dựng lại ở mỗi Trước/Sau: stack 200 frame là 180 ms một lần bấm. Lỗi đọc
        /// được ở vài chục frame đầu; Copy vẫn lấy đủ.
        internal const int MAX_FRAMES = 60;

        internal static DebugPage For(LogModel model, LogItem item)
        {
            model.SelectedSeq = item.Entry.Seq;
            var type = item.Entry.Type;
            // Log thường để màu tiêu đề mặc định: màu mờ ở tiêu đề đọc như trang bị tắt.
            var title = type == LogType.Log
                ? LogText.TypeLabel(type)
                : $"<color={LogText.ColorOf(type)}>{LogText.TypeLabel(type)}</color>";
            return new DebugPage(title, panel =>
            {
                var cut = item.Plain.Length > LogText.DETAIL_CHARS ? "… (Copy để lấy đủ)" : string.Empty;
                panel.AddButton(LogText.Render(item.Rich, null, LogText.DETAIL_CHARS) + cut,
                    () => Copy(panel, item.Plain, "nội dung"));

                if (item.Entry.Source == LogSource.Unity) AddFrames(panel, item.Entry.Stack);
                else AddLines(panel, item.Entry.Stack);

                var previous = model.Neighbour(item, -1);
                var next = model.Neighbour(item, 1);
                panel.ShowBar(
                    "‹ Trước", previous == null ? null : () => panel.Replace(For(model, previous)),
                    "Copy", () => Copy(panel, LogModel.Describe(item), "log"),
                    "Sau ›", next == null ? null : () => panel.Replace(For(model, next)));
            }, searchable: false, subtitle: Subtitle(item), fixedHeight: true);
        }

        /// Dòng phụ dưới tiêu đề bị cắt ở 40 ký tự (TailOf, cắt từ đầu): giờ + ×N.NNN.NNN + giờ cuối tới giây
        /// vừa 40 ký tự tới hàng triệu lần lặp, nên không cắt mất giờ đầu. Giờ cuối đặt InvariantCulture
        /// để dấu `:` không phụ thuộc máy.
        private static string Subtitle(LogItem item) => item.Repeat > 1
            ? $"{item.Time} – ×{LogText.Count(item.Repeat)}, cuối {item.Last.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}"
            : item.Time;

        private static void AddFrames(DebugHubPanel panel, string stack)
        {
            panel.AddText($"<color={Palette.DIM}>Stack trace</color>");
            var frames = StackFrames.Split(stack);
            if (frames.Count == 0) panel.AddText($"<color={Palette.MUTED}>Không có stack trace.</color>");
            for (var i = 0; i < frames.Count && i < MAX_FRAMES; i++) panel.AddText(Describe(frames[i]));
            if (frames.Count > MAX_FRAMES)
                panel.AddText($"<color={Palette.DIM}>… còn {LogText.Count(frames.Count - MAX_FRAMES)} frame – Copy để lấy đủ</color>");
        }

        /// Log native: các dòng sau dòng đầu (stack Java, các lần ghi cùng mili giây) là chữ thường, không phải frame C# —
        /// qua StackFrames thì `SDK v12.3.0` thành `3.0`. Hiện nguyên văn.
        private static void AddLines(DebugHubPanel panel, string stack)
        {
            if (string.IsNullOrEmpty(stack)) return;
            var lines = stack.Split('\n');
            panel.AddText($"<color={Palette.DIM}>Dòng tiếp theo</color>");
            for (var i = 0; i < lines.Length && i < MAX_FRAMES; i++) panel.AddText(LogText.Escape(lines[i]));
            if (lines.Length > MAX_FRAMES)
                panel.AddText($"<color={Palette.DIM}>… còn {LogText.Count(lines.Length - MAX_FRAMES)} dòng – Copy để lấy đủ</color>");
        }

        private static string Describe(StackFrames.Frame frame)
        {
            if (!frame.Game) return $"<color={ENGINE_FRAME}>{LogText.Escape(frame.Method)}</color>";
            if (string.IsNullOrEmpty(frame.Location)) return LogText.Escape(frame.Method);
            return $"{LogText.Escape(frame.Method)}\n<size=85%><color={Palette.MUTED}>{LogText.Escape(frame.Location)}</color></size>";
        }

        private static void Copy(DebugHubPanel panel, string text, string what)
        {
            GUIUtility.systemCopyBuffer = text;
            panel.ShowResult($"đã copy {what}", false);
        }
    }
}
