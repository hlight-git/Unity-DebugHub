using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bộ ghi log (spec ① §4): chạy ngầm từ lúc khởi động, không UI. Ring nằm ở đây chứ không ở UI, nên
    /// trang log mở muộn vẫn đọc được cả phiên — đúng chỗ IngameDebugConsole hỏng.
    ///
    /// Máy không được ghi thì không đăng ký callback: Unity không marshal chuỗi log sang C#, chi phí 0.
    internal static class LogRecorder
    {
        /// ponytail: 4 MB chuỗi, chỉnh khi đo trên máy thật. Thiếu chỗ thì dùng chung instance cho stack giống hệt.
        internal const int DEFAULT_BUDGET = 4 * 1024 * 1024;

        private static readonly object Gate = new();
        private static LogEntry[] ring = new LogEntry[256];
        private static int head;
        private static int count;
        private static long bytes;
        private static long nextSeq = 1;
        private static long dropped;
        private static long errorCount;

        internal static int Budget { get; set; } = DEFAULT_BUDGET;
        internal static bool Recording { get; private set; }
        internal static DateTime StartedAt { get; private set; }

        internal static long Dropped { get { lock (Gate) return dropped; } }
        internal static long ErrorCount { get { lock (Gate) return errorCount; } }
        internal static long LastSeq { get { lock (Gate) return nextSeq - 1; } }

        /// Seq của entry cũ nhất còn giữ; ring rỗng thì là seq kế tiếp.
        internal static long OldestSeq { get { lock (Gate) return count > 0 ? ring[head].Seq : nextSeq; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Boot()
        {
            Reset();
            // ponytail: PlayerPrefs ở SubsystemRegistration chưa kiểm trên máy thật (spec §11.1);
            // hỏng thì lùi sang AfterAssembliesLoaded.
            if (HubAccess.MayRecordNow()) Start();
        }

        /// Gọi lại được: mở khoá giữa phiên (DebugHub.Remember) cũng đi qua đây.
        internal static void Start()
        {
            lock (Gate)
            {
                if (Recording) return;
                Recording = true;
                StartedAt = DateTime.Now;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            // Trước khi gắn callback: phần buffer logcat còn giữ (kể cả log Unity từ trước lúc bắt đầu ghi) vào ring
            // trước, thứ tự đúng mà không phải sắp lại.
            Logcat.Start();
#elif UNITY_IOS && !UNITY_EDITOR
            OsLog.Start();
#endif
            Application.logMessageReceivedThreaded += Receive;
        }

        /// Test và "Enter Play Mode without domain reload": static sống qua các lần Play.
        internal static void Reset()
        {
            Application.logMessageReceivedThreaded -= Receive;
            lock (Gate)
            {
                Recording = false;
                StartedAt = default;
                ring = new LogEntry[256];
                head = count = 0;
                bytes = 0;
                nextSeq = 1;
                dropped = errorCount = 0;
                Budget = DEFAULT_BUDGET;
            }
        }

        /// Callback của Unity, chạy trên thread đã log. Không gọi Unity API ở đây.
        internal static void Receive(string message, string stack, LogType type)
        {
            lock (Gate)
            {
                var entry = new LogEntry(nextSeq++, DateTime.Now, type, message, stack, LogKind.Log, LogSource.Unity);
                if (entry.IsError) errorCount++;
                Append(entry);
            }
        }

        /// Một log đọc từ logcat (spec ② §2), giữ giờ của logcat. tag null = log Unity lấy lại từ buffer trước khi
        /// callback gắn vào. Chấm đỏ chỉ đếm lỗi Unity: E của OS/SDK có ở mọi phiên, đếm vào thì chấm luôn sáng.
        internal static void ReceiveNative(DateTime time, LogType type, string tag, string message, string stack)
        {
            lock (Gate)
            {
                var source = tag == null ? LogSource.Unity : LogSource.Native;
                var entry = new LogEntry(nextSeq++, time, type, message, stack, LogKind.Log, source, tag);
                if (entry.IsError && source == LogSource.Unity) errorCount++;
                // "Ghi từ …" tính cả phần lấy lại từ buffer.
                if (Recording && time < StartedAt) StartedAt = time;
                Append(entry);
            }
        }

        /// Vạch command trong dòng thời gian. Không ghi thì không đánh dấu: không ai đọc.
        internal static long Mark(string line)
        {
            lock (Gate)
            {
                if (!Recording) return 0;
                var entry = new LogEntry(nextSeq++, DateTime.Now, LogType.Log, line, null, LogKind.Command, LogSource.Unity);
                Append(entry);
                return entry.Seq;
            }
        }

        /// Thêm vào into mọi entry có Seq > seq, theo thứ tự. Không có gì mới thì không cấp phát.
        internal static int CopySince(long seq, List<LogEntry> into)
        {
            lock (Gate)
            {
                if (count == 0 || seq >= nextSeq - 1) return 0;
                // Seq liên tục trong ring nên vị trí của seq + 1 tính thẳng, không quét.
                var skip = (int)Math.Max(0, seq + 1 - ring[head].Seq);
                for (var i = skip; i < count; i++) into.Add(ring[(head + i) % ring.Length]);
                return count - skip;
            }
        }

        private static void Append(LogEntry entry)
        {
            if (count == ring.Length) Grow();
            ring[(head + count) % ring.Length] = entry;
            count++;
            bytes += entry.Cost;
            // Luôn giữ entry mới nhất, kể cả khi một mình nó vượt ngân sách.
            while (bytes > Budget && count > 1)
            {
                bytes -= ring[head].Cost;
                ring[head] = default;
                head = (head + 1) % ring.Length;
                count--;
                dropped++;
            }
        }

        private static void Grow()
        {
            var bigger = new LogEntry[ring.Length * 2];
            for (var i = 0; i < count; i++) bigger[i] = ring[(head + i) % ring.Length];
            ring = bigger;
            head = 0;
        }
    }
}
