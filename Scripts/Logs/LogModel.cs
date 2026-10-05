using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    internal enum LogGroup { Log, Warning, Error }

    /// Một entry đã kéo về trang log, kèm những gì tính một lần rồi giữ: cuộn qua lại không cấp phát lại.
    internal sealed class LogItem
    {
        public readonly LogEntry Entry;
        public int Repeat = 1;
        public DateTime Last;

        private string time;
        private string caller;
        private LogText.Rich rich;
        private string escapedPreview;
        private string meta;
        private string markerLine;

        public LogItem(LogEntry entry)
        {
            Entry = entry;
            Last = entry.Time;
        }

        public bool IsMarker => Entry.Kind == LogKind.Command;

        public LogGroup Group => LogModel.GroupOf(Entry.Type);

        public string Time => time ??= LogText.Time(Entry.Time);
        /// Log logcat không có stack C#: tag của nó là "nơi gọi" (dòng phụ, tìm, Copy).
        public string Caller => caller ??= Entry.Source == LogSource.Native ? Entry.Tag
            : StackFrames.Caller(Entry.Stack) ?? string.Empty;
        public LogText.Rich Rich => rich ??= LogText.Parse(Entry.Message);

        /// Chữ nhìn thấy, không tag rich text: tìm và Copy đi trên chuỗi này.
        public string Plain => Rich.Plain;

        /// Chuỗi TMP cho hàng log khi không tìm (spec §5.5: cuộn chỉ gán chuỗi đã cache). Đang tìm thì hàng tự
        /// Render kèm từ khoá — chuỗi đó đổi theo từ khoá nên không cache.
        public string EscapedPreview => escapedPreview ??= LogText.Render(Rich, null, LogText.ROW_CHARS);
        public string Meta => meta ??= Caller.Length == 0 ? Time : $"{Time} – {LogText.Escape(Caller)}";
        public string MarkerLine => markerLine ??= "› " + LogText.Escape(Entry.Message);
    }

    /// Trạng thái trang log, tách khỏi UI để test được (spec ① §5.2–5.3). Hàng 0 luôn là dòng ghi chú.
    internal sealed class LogModel
    {
        internal const int COPY_LIMIT = 500_000;

        /// Sống suốt phiên: lọc / gộp / xoá giữ nguyên qua các lần mở trang.
        internal static LogModel Shared { get; private set; } = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetShared() => Shared = new LogModel();

        private readonly List<LogItem> items = new();
        private readonly List<LogItem> rows = new();
        private readonly Dictionary<(LogType, string, string, string), LogItem> firsts = new();
        private readonly List<LogEntry> incoming = new();
        private readonly bool[] shown = { true, true, true };
        private readonly int[] counts = new int[3];
        private bool unityOnly;
        private bool collapse;
        private string query = string.Empty;
        private long lastSeq;
        private long clearedAt;

        /// Đang bám đáy. View đặt khi người dùng cuộn; giữ ở model để lùi từ trang chi tiết về vẫn đúng.
        public bool Follow = true;

        /// Hàng có viền mint: log vừa xem chi tiết, hoặc vạch command vừa nhảy tới.
        public long SelectedSeq;

        /// Số log Unity đã thấy theo loại (chỉ số LogGroup). Log mới chưa xem = LogRecorder.CountOf - Seen: ô đếm trên
        /// entry và chấm đỏ nút Log. View đặt mỗi frame khi trang log đang mở.
        public readonly long[] Seen = new long[3];

        /// Error, Exception, Assert cùng là Lỗi.
        internal static LogGroup GroupOf(LogType type) =>
            type == LogType.Log ? LogGroup.Log : type == LogType.Warning ? LogGroup.Warning : LogGroup.Error;

        /// Tăng khi nội dung một hàng đã có đổi (lọc lại, gộp thêm, bỏ log cũ): view gán lại hàng đang hiện.
        public int Version { get; private set; }

        public int RowCount => rows.Count + 1;
        public LogItem RowAt(int index) => index <= 0 ? null : rows[index - 1];
        public int Count(LogGroup group) => counts[(int)group];
        public int Total => counts[0] + counts[1] + counts[2];
        public bool IsShown(LogGroup group) => shown[(int)group];
        public bool Cleared => clearedAt > 0;

        public int EntryRowCount
        {
            get
            {
                var n = 0;
                foreach (var row in rows)
                {
                    if (!row.IsMarker) n++;
                }
                return n;
            }
        }

        public bool Collapse
        {
            get => collapse;
            set
            {
                if (collapse == value) return;
                collapse = value;
                Rebuild();
            }
        }

        public string Query
        {
            get => query;
            set
            {
                value ??= string.Empty;
                if (query == value) return;
                query = value;
                Rebuild();
            }
        }

        public void Toggle(LogGroup group)
        {
            shown[(int)group] = !shown[(int)group];
            Rebuild();
        }

        public void ResetFilters()
        {
            shown[0] = shown[1] = shown[2] = true;
            query = string.Empty;
            unityOnly = false;
            Rebuild();
        }

        /// Chip Unity: chỉ còn log Unity, ẩn log logcat của SDK/OS — kể cả log Android tới sau khi bật.
        public bool UnityOnly
        {
            get => unityOnly;
            set
            {
                if (unityOnly == value) return;
                unityOnly = value;
                Rebuild();
            }
        }

        /// Đã có log native (logcat, OSLog) chưa. Chưa (Editor) thì chip Unity không có gì để lọc, view ẩn nó đi.
        public bool HasNative { get; private set; }

        /// Ẩn mọi entry có Seq ≤ hiện tại của bộ ghi, kể cả entry chưa kéo về (trang log đang bị trang khác che).
        public void Clear()
        {
            clearedAt = LogRecorder.LastSeq;
            Rebuild();
        }

        public void Unclear()
        {
            clearedAt = 0;
            Rebuild();
        }

        /// Kéo entry mới từ bộ ghi. Trả về số hàng mới nối vào cuối — view dùng để bám đáy / đếm "N log mới".
        public int Pull()
        {
            incoming.Clear();
            LogRecorder.CopySince(lastSeq, incoming);
            var evicted = DropEvicted();
            if (incoming.Count == 0 && !evicted) return 0;

            var before = rows.Count;
            foreach (var entry in incoming)
            {
                lastSeq = entry.Seq;
                var item = new LogItem(entry);
                items.Add(item);
                if (!evicted) Admit(item);
            }
            if (!evicted) return rows.Count - before;

            Rebuild();
            // ponytail: sau khi bỏ log cũ thì đếm xấp xỉ bằng số entry vừa tới — con số chỉ để hiện "N log mới".
            return incoming.Count;
        }

        public string NoteText()
        {
            var text = LogRecorder.Recording
                ? $"Ghi từ {LogRecorder.StartedAt.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}"
                : "Máy này chưa ghi log";
            var dropped = LogRecorder.Dropped;
            if (dropped > 0) text += $" – đã bỏ {LogText.Count(dropped)} log cũ nhất";
            if (Cleared) text += $" – đã ẩn {LogText.Count(HiddenByClear())} log, bấm để hiện lại";
            return text;
        }

        /// Chỉ số hàng của một Seq (rows tăng dần theo Seq nên tìm nhị phân). -1 = không hiện.
        public int IndexOfSeq(long seq)
        {
            int low = 0, high = rows.Count - 1;
            while (low <= high)
            {
                var mid = (low + high) / 2;
                var at = rows[mid].Entry.Seq;
                if (at == seq) return mid + 1;
                if (at < seq) low = mid + 1;
                else high = mid - 1;
            }
            return -1;
        }

        /// Hàng log kế trước/sau theo bộ lọc hiện tại, bỏ qua vạch command — nút Trước/Sau ở trang chi tiết.
        public LogItem Neighbour(LogItem item, int direction)
        {
            var index = IndexOfSeq(item.Entry.Seq);
            if (index < 0) return null;
            for (var i = index + direction; i >= 1 && i < RowCount; i += direction)
            {
                if (!RowAt(i).IsMarker) return RowAt(i);
            }
            return null;
        }

        /// Text của mọi hàng đang hiện. Quá COPY_LIMIT thì giữ phần mới nhất.
        public string CopyAll()
        {
            var blocks = new List<string>();
            var length = 0;
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var block = Describe(rows[i]);
                if (length + block.Length > COPY_LIMIT)
                {
                    // Block mới nhất một mình đã quá giới hạn: giữ đuôi của nó thay vì trả chuỗi rỗng.
                    if (blocks.Count == 0) blocks.Add(block.Substring(block.Length - COPY_LIMIT));
                    break;
                }
                blocks.Add(block);
                length += block.Length + 1;
            }
            blocks.Reverse();
            return string.Join("\n", blocks);
        }

        internal static string Describe(LogItem item)
        {
            if (item.IsMarker) return $"[{item.Time}] › {item.Entry.Message}";
            var repeat = item.Repeat > 1 ? $" ×{item.Repeat}" : string.Empty;
            var stack = string.IsNullOrEmpty(item.Entry.Stack) ? string.Empty : "\n" + item.Entry.Stack.TrimEnd();
            var tag = item.Entry.Source == LogSource.Native ? item.Entry.Tag + ": " : string.Empty;
            return $"[{item.Time}] [{LogText.TypeLabel(item.Entry.Type)}]{repeat} {tag}{item.Plain}{stack}";
        }

        private bool DropEvicted()
        {
            var oldest = LogRecorder.OldestSeq;
            var drop = 0;
            while (drop < items.Count && items[drop].Entry.Seq < oldest) drop++;
            if (drop == 0) return false;
            items.RemoveRange(0, drop);
            return true;
        }

        private void Rebuild()
        {
            rows.Clear();
            firsts.Clear();
            Array.Clear(counts, 0, counts.Length);
            HasNative = false;
            foreach (var item in items)
            {
                item.Repeat = 1;
                item.Last = item.Entry.Time;
            }
            foreach (var item in items) Admit(item);
            Version++;
        }

        private void Admit(LogItem item)
        {
            if (item.Entry.Seq <= clearedAt) return;
            if (item.IsMarker)
            {
                rows.Add(item);
                return;
            }

            counts[(int)item.Group]++;
            var native = item.Entry.Source == LogSource.Native;
            if (native) HasNative = true;
            if (!IsShown(item.Group) || native && unityOnly || !Matches(item)) return;

            if (collapse)
            {
                // Có tag: cùng nội dung mà khác nguồn (AppLovinSdk, UnityAds, Unity) không phải log lặp.
                var key = (item.Entry.Type, item.Entry.Message, item.Entry.Stack, item.Entry.Tag);
                if (firsts.TryGetValue(key, out var first))
                {
                    first.Repeat++;
                    first.Last = item.Entry.Time;
                    Version++;
                    return;
                }
                firsts[key] = item;
            }
            rows.Add(item);
        }

        private bool Matches(LogItem item)
        {
            if (query.Length == 0) return true;
            // Log native: các dòng sau (logcat gộp các lần ghi cùng mili giây) là nội dung, không phải stack — phải tìm được.
            return item.Plain.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.Caller.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.Entry.Source == LogSource.Native &&
                   (item.Entry.Stack?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        private int HiddenByClear()
        {
            var n = 0;
            foreach (var item in items)
            {
                if (item.Entry.Seq <= clearedAt && !item.IsMarker) n++;
            }
            return n;
        }
    }
}
