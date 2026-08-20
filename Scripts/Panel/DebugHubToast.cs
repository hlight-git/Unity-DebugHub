using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Dòng kết quả của command, nổi ở đáy màn hình.
    ///
    /// Nằm ngoài panel (con của root hub, không phải của Panel) vì command mặc định đóng panel sau khi
    /// chạy: để trong panel thì đóng xong là không thấy kết quả. Bấm vào nó = mở console log window,
    /// nơi có toàn văn kèm stack trace — nên ở đây chỉ cần cắt ngắn, không cần page "Result" riêng.
    public class DebugHubToast : MonoBehaviour
    {
        private static readonly Color ErrorColor = new Color(1f, 0.42f, 0.42f);

        [SerializeField] private Text label;
        [SerializeField] private Button button;
        [Tooltip("Tự ẩn sau bao nhiêu giây.")]
        [SerializeField] private float duration = 6f;
        [Tooltip("Dài hơn thì cắt; toàn văn xem ở console.")]
        [SerializeField] private int characterLimit = 240;

        public event Action Clicked;

        private Color normalColor;
        private Coroutine hide;

        private void Awake()
        {
            normalColor = label.color;
            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Show(string text, bool error)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }

            // SetActive trước khi chạm field: prefab để object này tắt nên Awake chỉ chạy ở đây,
            // và StartCoroutine cũng chỉ chạy được khi object đang bật.
            gameObject.SetActive(true);

            if (characterLimit > 0 && text.Length > characterLimit) text = text.Substring(0, characterLimit) + "…";
            label.text = text;
            label.color = error ? ErrorColor : normalColor;

            if (hide != null) StopCoroutine(hide);
            hide = null;

            // EditMode test không tick player loop nên coroutine không bao giờ resume sau yield.
            if (Application.isPlaying && duration > 0f) hide = StartCoroutine(HideAfterDuration());
        }

        public void Hide()
        {
            if (hide != null) StopCoroutine(hide);
            hide = null;
            gameObject.SetActive(false);
        }

        private IEnumerator HideAfterDuration()
        {
            // Realtime: cheat set time scale = 0 thì WaitForSeconds không bao giờ xong.
            yield return new WaitForSecondsRealtime(duration);
            hide = null;
            gameObject.SetActive(false);
        }
    }
}
