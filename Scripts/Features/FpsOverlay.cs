using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Ô FPS nhỏ ở góc trên trái (trong safe area), bật bằng `view.fps`. Đứng riêng khỏi hub: vẫn hiện khi panel đóng,
    /// bong bóng ẩn hay `hub.hide` — tester bật nó là để nhìn trong lúc chơi; muốn màn hình sạch thì tắt lệnh.
    ///
    /// Canvas riêng, không có GraphicRaycaster: không bao giờ chặn chạm của game. Dựng bằng code lúc bật lần đầu, nên
    /// máy chưa từng bật (mọi người chơi) không có object nào.
    internal sealed class FpsOverlay : MonoBehaviour
    {
        private const string ENABLED_KEY = "DebugHub.FpsOverlay";

        /// Dưới canvas của hub (32767): panel mở thì che ô này, không chen giữa các row.
        private const int SORTING_ORDER = 32766;

        /// Chỉ con số FPS mang màu, phần còn lại trắng: chữ màu trên nền mờ khó đọc. Một chuỗi định dạng mỗi mức để
        /// SetText khỏi ghép chuỗi mỗi lần.
        private const string GOOD_FORMAT = "<color=" + Palette.GOOD + ">{0}</color> fps   {1:0} ms";
        private const string WARN_FORMAT = "<color=" + Palette.WARN + ">{0}</color> fps   {1:0} ms";
        private const string BAD_FORMAT = "<color=" + Palette.BAD + ">{0}</color> fps   {1:0} ms";

        private static FpsOverlay instance;

        private readonly FpsMeter meter = new();
        private RectTransform safeArea;
        private TMP_Text label;
        private Rect appliedSafeArea;

        /// Lưu qua phiên như `hub.repeat`: đo hiệu năng thường kéo dài nhiều lần mở app.
        internal static bool Enabled
        {
            get => PlayerPrefs.GetInt(ENABLED_KEY) == 1;
            set
            {
                PlayerPrefs.SetInt(ENABLED_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
                Refresh();
            }
        }

        /// DebugHub gọi lúc khởi động khi máy đã mở khoá: lần trước bật thì hiện lại ngay.
        internal static void Refresh()
        {
            // EditMode test không dựng UI thật.
            if (!Application.isPlaying) return;
            if (Enabled && !instance) instance = Create();
            if (instance) instance.gameObject.SetActive(Enabled);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static FpsOverlay Create()
        {
            var root = new GameObject("DebugHub FPS", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            // Cùng thước với prefab hub: chữ ô này to bằng chữ trong panel trên mọi màn hình.
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safeArea.SetParent(root.transform, false);

            var box = new GameObject("Box", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(safeArea, false);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
            box.anchoredPosition = new Vector2(12f, -12f);
            var background = box.gameObject.AddComponent<Image>();
            // Gần đặc: ô nằm trên trời / cây sáng của game, nền mờ thì chữ chìm.
            background.color = new Color(0.05f, 0.07f, 0.1f, 0.85f);
            background.raycastTarget = false;
            var layout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(box, false);
            text.fontSize = 40f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = "— fps";

            // Font giống chữ trong panel. Chỉ có số và chữ Latin nên font nào cũng vẽ được; chỉ để cùng một kiểu.
            HubFont.Apply(root);

            var overlay = root.AddComponent<FpsOverlay>();
            overlay.safeArea = safeArea;
            overlay.label = text;
            return overlay;
        }

        private void Update()
        {
            // Đổi xoay màn hình / notch thì dời theo; so Rect mỗi frame rẻ hơn dựng lại layout.
            var area = Screen.safeArea;
            if (area != appliedSafeArea) ApplySafeArea(area);

            // Giờ thật: time.scale = 0 hay tua nhanh không làm sai số khung hình.
            if (!meter.Add(Time.unscaledDeltaTime)) return;
            // SetText với tham số số không cấp phát chuỗi: ô này chạy suốt phiên đo hiệu năng.
            label.SetText(FormatOf(Grade(meter.Fps, TargetFps())), meter.Fps, meter.WorstMs);
        }

        private void ApplySafeArea(Rect area)
        {
            appliedSafeArea = area;
            var size = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            safeArea.anchorMin = area.min / size;
            safeArea.anchorMax = area.max / size;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        private static string FormatOf(TextStyle grade) => grade switch
        {
            TextStyle.Good => GOOD_FORMAT,
            TextStyle.Warn => WARN_FORMAT,
            _ => BAD_FORMAT,
        };

        /// Editor bỏ qua vSyncCount: Game view mặc định không chờ vSync nên targetFrameRate quyết, vSyncCount của
        /// quality desktop làm ô tô vàng dù game chạy đúng FPS đã đặt.
        private static int TargetFps() => TargetFps(Application.targetFrameRate,
            Application.isEditor ? 0 : QualitySettings.vSyncCount,
            Application.isMobilePlatform, Screen.currentResolution.refreshRateRatio.value);

        /// FPS game nhắm tới, để tô màu. Unity: vSyncCount > 0 thắng targetFrameRate (trừ mobile, nơi vSync luôn bật và
        /// targetFrameRate quyết); targetFrameRate ≤ 0 thì mobile mặc định 30, máy khác chạy theo tần số màn hình.
        internal static int TargetFps(int targetFrameRate, int vSyncCount, bool mobile, double refreshRate)
        {
            var refresh = refreshRate > 0 ? Mathf.RoundToInt((float)refreshRate) : 60;
            if (!mobile && vSyncCount > 0) return Mathf.Max(1, refresh / vSyncCount);
            if (targetFrameRate > 0) return mobile ? Mathf.Min(targetFrameRate, refresh) : targetFrameRate;
            return mobile ? 30 : refresh;
        }

        /// Xanh khi đạt ≥ 90% FPS nhắm tới, vàng ≥ 60%, dưới nữa đỏ. So số nguyên: 60 * 0.6f ra 36.000002, 36 fps thành đỏ.
        internal static TextStyle Grade(int fps, int target)
        {
            if (fps * 10 >= target * 9) return TextStyle.Good;
            if (fps * 10 >= target * 6) return TextStyle.Warn;
            return TextStyle.Bad;
        }
    }

    /// Đếm khung hình theo cửa sổ <see cref="Window"/> giây: FPS trung bình và khung chậm nhất trong cửa sổ — trung bình
    /// che mất giật, khung chậm nhất mới cho thấy giật.
    internal sealed class FpsMeter
    {
        internal const float Window = 0.5f;

        private float elapsed;
        private int frames;
        private float worst;

        internal int Fps { get; private set; }
        internal float WorstMs { get; private set; }

        /// Thêm một khung; true khi vừa xong một cửa sổ (Fps, WorstMs vừa đổi).
        internal bool Add(float deltaTime)
        {
            elapsed += deltaTime;
            frames++;
            if (deltaTime > worst) worst = deltaTime;
            if (elapsed < Window) return false;

            Fps = Mathf.RoundToInt(frames / elapsed);
            WorstMs = worst * 1000f;
            elapsed = 0f;
            frames = 0;
            worst = 0f;
            return true;
        }
    }
}
