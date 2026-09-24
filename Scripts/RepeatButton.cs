using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Nút nổi cạnh entry: bấm là chạy lại lệnh cuối. Giữ **dòng lệnh** chứ không giữ tham chiếu
    /// node — nhờ vậy chạy lại đúng tham số đã chạy và sống qua lần mở app sau.
    public class RepeatButton : MonoBehaviour
    {
        private const string ENABLED_KEY = "DebugHub.RepeatButton";

        /// Vùng icon ở đầu nút và phần chừa sau nhãn — khớp với layout đã bake trong prefab.
        private const float ICON_WIDTH = 92f;
        private const float LABEL_PADDING = 28f;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private FloatingBubble bubble;

        public bool Enabled
        {
            get => PlayerPrefs.GetInt(ENABLED_KEY) == 1;
            set
            {
                PlayerPrefs.SetInt(ENABLED_KEY, value ? 1 : 0);
                Refresh();
            }
        }

        public void Refresh()
        {
            var line = DebugRegistry.LastCommand;

            // Bật mà chưa có lệnh nào thì ẩn hẳn: một nút rỗng bấm ra lỗi thì tệ hơn là không có nút.
            // Và luôn tôn trọng Visible: LastCommandChanged bắn bất kể hub đang ẩn hay hiện, không check
            // thì một lệnh chạy qua console trong lúc hub đang ẩn có chủ đích sẽ tự bật nút lên.
            var visible = DebugHub.Visible && Enabled && !string.IsNullOrEmpty(line);
            if (!visible)
            {
                gameObject.SetActive(false);
                return;
            }

            // Bật trước rồi mới đo: TMP chỉ nạp font mặc định ở OnEnable, đo preferredWidth lúc còn tắt là
            // không có font. Cùng một frame nên không hiện ra ở vị trí cũ.
            gameObject.SetActive(true);
            Present(line);
        }

        /// Nhãn là tên lá của lệnh sẽ chạy: một nút một chạm chạy lại thứ không nhìn thấy là gì thì không
        /// ai dám bấm.
        internal void Present(string line)
        {
            var path = line.Split(' ')[0];
            var dot = path.LastIndexOf('.');
            label.text = dot >= 0 ? path.Substring(dot + 1) : path;

            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(ICON_WIDTH + label.preferredWidth + LABEL_PADDING, rect.sizeDelta.y);
            PositionNextToBubble();
        }

        /// DebugHub gọi, không phải Awake: object lưu inactive trong prefab và chỉ bật khi đã có lệnh, nên
        /// Awake có thể không bao giờ chạy — bật `hub.repeat` lúc chưa có lệnh là nút không bao giờ hiện.
        internal void Initialize()
        {
            button.onClick.AddListener(Run);
            bubble.DragStateChanged += OnBubbleDragStateChanged;
            DebugRegistry.LastCommandChanged += Refresh;
            Refresh();
        }

        /// Cặp với Initialize, cũng do DebugHub gọi: OnDestroy không chạy trên object chưa từng active.
        internal void Release()
        {
            DebugRegistry.LastCommandChanged -= Refresh;
            bubble.DragStateChanged -= OnBubbleDragStateChanged;
        }

        /// Bubble chỉ dời lúc trượt về mép sau khi thả (lúc kéo thì nút này ẩn) — đứng yên thì không đụng
        /// RectTransform, đụng là canvas dựng lại batch mỗi frame.
        private void LateUpdate()
        {
            if (!bubble.Settled) PositionNextToBubble();
        }

        private void OnBubbleDragStateChanged(bool dragging)
        {
            // Kéo bubble phải đọc như một vật dưới ngón tay: nút phụ biến mất, thả xong hiện lại cạnh nó.
            if (dragging) gameObject.SetActive(false);
            else Refresh();
        }

        private void PositionNextToBubble()
        {
            var rect = (RectTransform)transform;
            var bubbleRect = (RectTransform)bubble.transform;
            var gapX = bubbleRect.rect.width * 0.5f + rect.rect.width * 0.5f + 12f;
            var offset = bubble.DockedEdge == FloatingBubble.Edge.Left ? gapX : -gapX;
            rect.anchoredPosition = bubbleRect.anchoredPosition + new Vector2(offset, 0f);
        }

        private void Run() => DebugHub.Repeat(DebugRegistry.LastCommand);
    }
}
