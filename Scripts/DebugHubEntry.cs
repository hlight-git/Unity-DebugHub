using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Nút nổi mở hub: một ô đếm log mới chưa xem — Log / Cảnh báo / Lỗi — như nút nổi của IngameDebugConsole.
    public class DebugHubEntry : MonoBehaviour
    {
        /// Số 0 mờ đi để số khác 0 nổi lên; vẫn hiện để ô không đổi hình.
        private const float ZERO_ALPHA = 0.45f;

        [SerializeField] private Button button;
        [SerializeField] private FloatingBubble floatingBubble;

        [Tooltip("Thứ tự theo LogGroup: Log, Cảnh báo, Lỗi.")]
        [SerializeField] private CanvasGroup[] countGroups = new CanvasGroup[3];
        [SerializeField] private TMP_Text[] countLabels = new TMP_Text[3];

        public event Action Clicked;

        public bool Activating
        {
            get => button.gameObject.activeInHierarchy;
            set
            {
                if (value == button.gameObject.activeInHierarchy) return;
                button.gameObject.SetActive(value);
            }
        }

        /// Log Unity mới chưa xem theo loại, từ lần mở trang log gần nhất (spec ① §5.1). Quá 99 thì 99+.
        public void SetCounts(long logs, long warnings, long errors)
        {
            SetCount(LogGroup.Log, logs);
            SetCount(LogGroup.Warning, warnings);
            SetCount(LogGroup.Error, errors);
        }

        private void SetCount(LogGroup group, long value)
        {
            countLabels[(int)group].text = LogText.Badge(value);
            countGroups[(int)group].alpha = value > 0 ? 1f : ZERO_ALPHA;
        }

        private void Awake()
        {
            button.onClick.AddListener(() =>
            {
                if (!floatingBubble.SuppressClick) Clicked?.Invoke();
            });
            floatingBubble.DragStateChanged += value => button.enabled = !value;
            // Kéo vào X là tester cố ý tắt bong bóng: nhớ cho phiên sau.
            floatingBubble.Dismissed += () =>
            {
                DebugHub.Visible = false;
                DebugHub.RememberEntry(false);
            };
        }
    }
}
