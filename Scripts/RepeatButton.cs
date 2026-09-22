using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Nút nổi cạnh entry: bấm là chạy lại lệnh cuối. Giữ **dòng lệnh** chứ không giữ tham chiếu
    /// node — nhờ vậy chạy lại đúng tham số đã chạy, sống qua lần mở app sau, và dùng lại nguyên
    /// DebugHub.Execute thay vì đẻ đường chạy thứ hai.
    public class RepeatButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        private const string ENABLED_KEY = "DebugHub.RepeatButton";

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
            gameObject.SetActive(Enabled && !string.IsNullOrEmpty(line));
            if (!gameObject.activeSelf) return;

            var path = line.Split(' ')[0];
            var dot = path.LastIndexOf('.');
            label.text = "↻ " + (dot >= 0 ? path.Substring(dot + 1) : path);
        }

        private void Awake()
        {
            button.onClick.AddListener(Run);
            DebugRegistry.LastCommandChanged += Refresh;
            Refresh();
        }

        private void OnDestroy() => DebugRegistry.LastCommandChanged -= Refresh;

        /// Lệnh có Confirms() thì mở panel tới trang xác nhận chứ không chạy thẳng: một nút nổi
        /// một chạm chạy thẳng `save.wipe` là tai nạn chờ sẵn.
        private void Run()
        {
            var line = DebugRegistry.LastCommand;
            if (DebugHub.NeedsConfirm(line))
            {
                DebugHub.OpenConfirm(line);
                return;
            }

            if (DebugHub.Execute(line, out var message)) return;

            DebugHub.Report(message, true);
            Refresh();       // lệnh đã mất đăng ký thì bản lưu cũng lỗi thời
        }
    }
}
