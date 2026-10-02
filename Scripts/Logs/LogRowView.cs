using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Một hàng của trang log. LogView giữ ~15 cái và gán lại khi cuộn — Bind chỉ gán chuỗi đã tính sẵn ở
    /// LogItem, không dựng gì mới.
    public class LogRowView : MonoBehaviour
    {
        /// Đặc, không dùng alpha: project ở linear color space nên uGUI trộn alpha trong linear, 8% đỏ ra màu
        /// nâu đỏ đậm gấp mấy lần mockup. Đây là 8% #E5484D trên nền panel #171F2C trộn theo sRGB.
        private static readonly Color ErrorTint = Palette.ToColor("#28222F");
        private static readonly Color ErrorText = new(1f, 0.79f, 0.80f);
        private static readonly Color Mint = new(0.60f, 0.87f, 0.83f);
        private static readonly Color Muted = Palette.ToColor(Palette.MUTED);
        private static readonly Color Dim = Palette.ToColor(Palette.DIM);
        private static readonly Color Warn = Palette.ToColor(Palette.WARN);
        private static readonly Color Bad = Palette.ToColor(Palette.BAD);

        [SerializeField] internal Button button;
        [SerializeField] private Image background;
        [SerializeField] private GameObject selected;
        [SerializeField] private GameObject entryGroup;
        [SerializeField] private DebugHubIcon icon;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private GameObject repeatGroup;
        [SerializeField] private TMP_Text repeat;
        [SerializeField] private GameObject markerGroup;
        [SerializeField] private GameObject markerLeft;
        [SerializeField] private GameObject markerRight;
        [SerializeField] private TMP_Text marker;

        /// Hàng mô hình đang hiện ở ô này, cùng Version và trạng thái chọn lúc gán: trùng hết thì khỏi gán lại.
        internal int Index = -1;
        internal int Version = -1;
        internal bool Selected;

        internal void BindEntry(LogItem item, string query, bool isSelected, bool showRepeat)
        {
            entryGroup.SetActive(true);
            markerGroup.SetActive(false);
            var error = item.Group == LogGroup.Error;
            background.color = error ? ErrorTint : Color.clear;
            icon.symbol = item.Group == LogGroup.Error ? DebugHubIcon.Symbol.Error
                : item.Group == LogGroup.Warning ? DebugHubIcon.Symbol.Warning
                : DebugHubIcon.Symbol.Info;
            icon.color = error ? Bad : item.Group == LogGroup.Warning ? Warn : Dim;
            message.color = error ? ErrorText : Color.white;
            // Không tìm: gán chuỗi đã cache ở LogItem, cuộn không cấp phát. Đang tìm thì tô cả nơi gọi —
            // log khớp chỉ nhờ nơi gọi mà không tô gì thì đọc như lọc sai.
            if (query.Length == 0)
            {
                message.text = item.EscapedPreview;
                meta.text = item.Meta;
            }
            else
            {
                message.text = LogText.Render(item.Rich, query, LogText.ROW_CHARS);
                meta.text = item.Caller.Length == 0 ? item.Time : $"{item.Time} – {LogText.Highlight(item.Caller, query)}";
            }
            var repeated = showRepeat && item.Repeat > 1;
            repeatGroup.SetActive(repeated);
            if (repeated) repeat.text = "×" + LogText.Count(item.Repeat);
            selected.SetActive(isSelected);
        }

        /// Vạch command (mint, có hai đường kẻ) hoặc dòng ghi chú đầu danh sách (xám, không kẻ).
        /// <paramref name="line"/> đã escape sẵn — vạch command lấy LogItem.MarkerLine đã cache.
        internal void BindMarker(string line, bool command, bool isSelected)
        {
            entryGroup.SetActive(false);
            markerGroup.SetActive(true);
            background.color = Color.clear;
            markerLeft.SetActive(command);
            markerRight.SetActive(command);
            marker.color = command ? Mint : Muted;
            marker.text = line;
            selected.SetActive(isSelected);
        }
    }
}
