using System;
using System.Globalization;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Threading;
#endif

namespace Hlight.Debug.Hub
{
    /// Log hệ thống của chính tiến trình trên iOS (spec ③): OSLogStore phạm vi tiến trình hiện tại (iOS 15+) — NSLog,
    /// os_log của SDK (AppLovin, Firebase…), lỗi mạng. Không stream được như logcat nên đọc theo lượt mỗi POLL_MS. Chỉ
    /// đọc khi LogRecorder đang ghi: máy người chơi không đọc.
    ///
    /// SDK link tĩnh ghi log từ chính UnityFramework như Unity, nên không phân biệt theo sender được: plugin cho Unity
    /// ghi log vào subsystem riêng (DebugHub_MarkUnityLogs) và đánh dấu entry đó là nguồn `U`.
    internal static class OsLog
    {
        private const int POLL_MS = 2000;
        private const char FIELD = '\u001f';
        private const char RECORD = '\u001e';
        private const char UNITY = 'U';

        /// Một lượt của plugin: `giây unix ␟ mức ␟ nguồn ␟ sender ␟ nội dung ␞` mỗi entry, hoặc `!lý do` khi không mở được
        /// store. Sender (AppLovinSDK, CFNetwork, UnityFramework…) là tag. Trả về giờ của entry mới nhất để lượt sau đọc
        /// tiếp từ đó.
        internal static double Read(string batch, bool skipUnity, NativeLogEmit emit, double after)
        {
            if (string.IsNullOrEmpty(batch)) return after;
            if (Failed(batch))
            {
                emit(DateTime.Now, LogType.Warning, "DebugHub", "Không đọc được log iOS: " + batch.Substring(1), null);
                return after;
            }

            var last = after;
            foreach (var record in batch.Split(RECORD))
            {
                var fields = record.Split(new[] { FIELD }, 5);
                if (fields.Length < 5 || fields[1].Length == 0 || fields[2].Length == 0 ||
                    !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) continue;
                if (seconds <= after) continue;
                last = Math.Max(last, seconds);

                // Log Unity: callback đã gắn thì đi đường đó (message + stack chuẩn hơn), không thì lấy và tách dòng đầu.
                var unity = fields[2][0] == UNITY;
                if (unity && skipUnity) continue;
                var text = fields[4];
                string message = text, stack = null;
                var newline = unity ? text.IndexOf('\n') : -1;
                if (newline >= 0)
                {
                    message = text.Substring(0, newline);
                    stack = text.Substring(newline + 1).TrimEnd();
                    if (stack.Length == 0) stack = null;
                }
                var time = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000)).LocalDateTime;
                emit(time, TypeOf(fields[1][0]), unity ? null : fields[3], message, stack);
            }
            return last;
        }

        internal static bool Failed(string batch) => !string.IsNullOrEmpty(batch) && batch[0] == '!';

        /// os_log không có mức cảnh báo: debug/info/notice là Log, error/fault là Lỗi.
        internal static LogType TypeOf(char level) => level == 'E' || level == 'F' ? LogType.Error : LogType.Log;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DebugHub_MarkUnityLogs();

        [DllImport("__Internal")]
        private static extern string DebugHub_ReadLog(double afterUnixSeconds);

        /// Mọi máy, kể cả người chơi: log Unity vẫn ra os_log y như Trampoline, chỉ đổi subsystem. Đánh dấu từ sớm để
        /// lượt đọc đầu khi mở khoá giữa phiên cũng nhận ra log Unity; chỉ vài dòng khởi tạo engine trước đó còn mang
        /// tag UnityFramework.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void MarkUnityLogs() => DebugHub_MarkUnityLogs();

        /// Gọi trên main thread, TRƯỚC khi gắn callback Unity (LogRecorder.Start): đọc mọi thứ từ lúc tiến trình khởi động
        /// rồi đọc tiếp theo lượt trên thread nền.
        /// ponytail: lượt đầu đồng bộ — mở khoá sau một phiên dài có thể khựng; đo trên máy, chuyển async nếu đáng kể.
        internal static void Start()
        {
            try
            {
                var batch = DebugHub_ReadLog(0);
                var last = Read(batch, false, LogRecorder.ReceiveNative, 0);
                // Store hỏng thì lượt nào cũng hỏng: Read đã báo một lần, không đọc tiếp.
                if (Failed(batch)) return;
                new Thread(() => Poll(last)) { IsBackground = true, Name = "DebugHub oslog" }.Start();
            }
            catch (Exception e)
            {
                LogRecorder.ReceiveNative(DateTime.Now, LogType.Warning, "DebugHub", "Không đọc được log iOS: " + e.Message, null);
            }
        }

        private static void Poll(double after)
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(POLL_MS);
                    var batch = DebugHub_ReadLog(after);
                    after = Read(batch, true, LogRecorder.ReceiveNative, after);
                    if (Failed(batch)) return;
                }
            }
            catch (Exception e)
            {
                LogRecorder.ReceiveNative(DateTime.Now, LogType.Warning, "DebugHub", "Log iOS đã dừng: " + e.Message, null);
            }
        }
#endif
    }
}
