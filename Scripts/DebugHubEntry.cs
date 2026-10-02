using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class DebugHubEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private FloatingBubble floatingBubble;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeCount;

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

        /// Số lỗi ghi được mà chưa mở trang log xem (spec ① §5.1). 0 = ẩn.
        public long Badge
        {
            set
            {
                badge.SetActive(value > 0);
                if (value > 0) badgeCount.text = LogText.Badge(value);
            }
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
