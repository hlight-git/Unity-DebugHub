using System;
using System.Threading.Tasks;

namespace Hlight.Debug.Hub
{
    /// Một kênh gửi (spec ④ §3): khoá "đang gửi", gọi Send, báo kết quả.
    ///
    /// Start chạy trong Invoke của row Gửi, tức trong DebugRegistry.Capture: log in ra ở đây thành dòng kết quả, ném
    /// ở đây thì row coi là chạy hỏng và panel không đóng — đúng cái cần khi Send validate hỏng ngay (QA sửa ô rồi
    /// gửi lại). Task xong sau đó thì panel đã đóng, chỉ còn dòng kết quả qua `show`.
    internal sealed class SendFlow
    {
        private readonly string doneText;
        private readonly Action<string, bool> show;

        internal SendFlow(string doneText, Action<string, bool> show)
        {
            this.doneText = doneText;
            this.show = show;
        }

        internal bool Busy { get; private set; }

        /// false = kênh đang bận, lần bấm này không gửi gì.
        internal bool Start(Func<Task<string>> send)
        {
            if (Busy)
            {
                UnityEngine.Debug.Log("Đang gửi lần trước…");
                return false;
            }

            var task = send() ?? throw new InvalidOperationException("Send trả về null.");
            if (task.IsCompleted)
            {
                UnityEngine.Debug.Log(Result(task));
                return true;
            }

            Busy = true;
            UnityEngine.Debug.Log("Đang gửi…");
            DebugHub.Watch(task, () => Finish(task));
            return true;
        }

        internal void Finish(Task<string> task)
        {
            Busy = false;
            string text;
            try { text = Result(task); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                show(exception.Message, true);
                return;
            }
            UnityEngine.Debug.Log(text);
            show(text, false);
        }

        /// GetResult ném đúng exception gốc (không bọc AggregateException), kể cả TaskCanceledException.
        private string Result(Task<string> task)
        {
            var result = task.GetAwaiter().GetResult();
            return string.IsNullOrEmpty(result) ? doneText : result;
        }
    }
}
