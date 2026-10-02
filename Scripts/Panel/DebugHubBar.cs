using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Footer ghim đáy window, ngoài vùng cuộn: nút chính ở giữa, hai nút phụ hai bên.
    /// Trang chi tiết log dùng cho Trước / Copy / Sau. Một instance duy nhất trong prefab (DebugHubPanel.ShowBar).
    public class DebugHubBar : MonoBehaviour
    {
        [SerializeField] private Button previous;
        [SerializeField] private TMP_Text previousLabel;
        [SerializeField] private CanvasGroup previousGroup;
        [SerializeField] private Button primary;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button next;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private CanvasGroup nextGroup;

        internal void Set(string previousText, Action onPrevious, string primaryText, Action onPrimary,
            string nextText, Action onNext)
        {
            Side(previous, previousLabel, previousGroup, previousText, onPrevious);
            Side(next, nextLabel, nextGroup, nextText, onNext);
            primaryLabel.text = primaryText;
            primary.onClick.RemoveAllListeners();
            primary.onClick.AddListener(() => onPrimary?.Invoke());
        }

        internal void Release()
        {
            previous.onClick.RemoveAllListeners();
            primary.onClick.RemoveAllListeners();
            next.onClick.RemoveAllListeners();
        }

        /// Không có log kế bên thì nút mờ đi chứ không biến mất: nút chính giữ nguyên chỗ, ngón tay khỏi hụt.
        private static void Side(Button button, TMP_Text label, CanvasGroup group, string text, Action action)
        {
            label.text = text;
            button.interactable = action != null;
            group.alpha = action != null ? 1f : 0.35f;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }
    }
}
