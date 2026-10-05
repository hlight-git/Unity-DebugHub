using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Threading;
#endif

namespace Hlight.Debug.Hub
{
    /// Một log native đã đọc xong (logcat, OSLog) — production đưa vào LogRecorder.ReceiveNative.
    internal delegate void NativeLogEmit(DateTime time, LogType type, string tag, string message, string stack);

    /// Logcat của chính tiến trình (spec ②): từ Android 4.1 app chỉ đọc được log của nó, không cần quyền — đủ log
    /// Java/native của SDK trong game (AppLovin, Firebase…), không có log của tiến trình khác. Chỉ chạy khi
    /// LogRecorder đang ghi: máy người chơi không chạy.
    internal static class Logcat
    {
        internal const string UNITY_TAG = "Unity";

        internal readonly struct Line
        {
            public readonly DateTime Time;
            public readonly char Level;
            public readonly string Tag;
            public readonly string Text;

            /// Giờ, pid, tid, mức, tag nguyên văn: mọi dòng của một lần ghi có cùng header.
            public readonly string Header;

            public Line(DateTime time, char level, string tag, string text, string header)
            {
                Time = time;
                Level = level;
                Tag = tag;
                Text = text;
                Header = header;
            }
        }

        /// `10-02 14:26:21.750 12915 12930 D UnityAds: Wrote file` — tag được đệm khoảng trắng tới `: `.
        private static readonly Regex Threadtime = new(
            @"^(\d\d)-(\d\d) (\d\d):(\d\d):(\d\d)\.(\d{3}) +\d+ +\d+ ([VDIWEFA]) (.*?) *: ?(.*)$",
            RegexOptions.CultureInvariant);

        /// Logcat không in năm. ponytail: lấy năm hiện tại, lệch một lần lúc giao thừa.
        internal static bool TryParse(string raw, int year, out Line line)
        {
            line = default;
            var match = Threadtime.Match(raw ?? string.Empty);
            if (!match.Success) return false;
            try
            {
                var time = new DateTime(year, Int(match, 1), Int(match, 2), Int(match, 3), Int(match, 4), Int(match, 5), Int(match, 6));
                var text = match.Groups[9];
                line = new Line(time, match.Groups[7].Value[0], match.Groups[8].Value, text.Value, raw.Substring(0, text.Index));
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static int Int(Match match, int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

        internal static LogType TypeOf(char level) => level switch
        {
            'W' => LogType.Warning,
            'E' or 'F' or 'A' => LogType.Error,
            _ => LogType.Log,
        };

        /// Stream nối tiếp dump bằng `-T <giờ dòng cuối của dump>`, mà -T in lại cả các dòng cùng mili giây đó: dòng mới là
        /// dòng sau giờ đó, hoặc cùng mili giây mà dump chưa có.
        internal static bool IsNew(in Line line, string raw, DateTime after, ICollection<string> seen) =>
            line.Time > after || line.Time == after && !seen.Contains(raw);

        /// Gom các dòng liên tiếp cùng header thành một log: dòng đầu là message, các dòng sau là stack (stack Java
        /// của exception). Log Unity kết thúc bằng một dòng rỗng: cắt ở đó, không thì hai log cùng mili giây dính
        /// vào nhau.
        internal sealed class Sink
        {
            private readonly bool skipUnity;
            private readonly NativeLogEmit emit;
            private readonly StringBuilder stack = new();
            private Line first;
            private bool pending;

            /// skipUnity: callback Unity đã gắn thì log Unity đi đường đó (message + stack chuẩn hơn), logcat bỏ.
            internal Sink(bool skipUnity, NativeLogEmit emit)
            {
                this.skipUnity = skipUnity;
                this.emit = emit;
            }

            internal void Add(in Line line)
            {
                var unity = line.Tag == UNITY_TAG;
                if (unity && skipUnity) return;
                var end = unity && line.Text.Length == 0;
                if (pending && line.Header == first.Header)
                {
                    if (end)
                    {
                        Flush();
                        return;
                    }
                    if (stack.Length > 0) stack.Append('\n');
                    stack.Append(line.Text);
                    return;
                }

                Flush();
                if (end) return;
                first = line;
                pending = true;
            }

            internal void Flush()
            {
                if (!pending) return;
                pending = false;
                var tag = first.Tag == UNITY_TAG ? null : first.Tag;
                emit(first.Time, TypeOf(first.Level), tag, first.Text, stack.Length > 0 ? stack.ToString() : null);
                stack.Clear();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// Gọi trên main thread, TRƯỚC khi gắn callback Unity (LogRecorder.Start): dump phần buffer còn giữ từ lúc
        /// tiến trình khởi động — kể cả log Unity từ trước lúc bắt đầu ghi — rồi stream phần mới trên thread nền.
        /// ponytail: dump đồng bộ ~50–150 ms, chỉ trên máy được ghi; chuyển async + giữ callback chờ khi hitch đáng kể.
        internal static void Start()
        {
            try
            {
                string pid;
                using (var process = new AndroidJavaClass("android.os.Process"))
                    pid = process.CallStatic<int>("myPid").ToString(CultureInfo.InvariantCulture);

                // Dump rỗng thì stream nối tiếp từ lúc bắt đầu dump: dòng ghi giữa dump và stream không mất.
                var last = DateTime.Now;
                var lastLines = new HashSet<string>();
                int lines = 0, parsed = 0;
                var dump = new Sink(skipUnity: false, LogRecorder.ReceiveNative);
                Read(new[] { "logcat", "-d", "-v", "threadtime", "--pid=" + pid }, raw =>
                {
                    if (raw.StartsWith("---------", StringComparison.Ordinal)) return;   // "--------- beginning of main"
                    lines++;
                    if (!TryParse(raw, DateTime.Now.Year, out var line)) return;
                    parsed++;
                    if (parsed == 1 || line.Time > last)
                    {
                        last = line.Time;
                        lastLines.Clear();
                    }
                    lastLines.Add(raw);
                    dump.Add(line);
                }, null);
                dump.Flush();

                // Có dòng mà không dòng nào khớp threadtime: ROM đổi định dạng. Báo, không thì trang log im lặng thiếu log native.
                if (lines > 0 && parsed == 0)
                {
                    LogRecorder.ReceiveNative(DateTime.Now, LogType.Warning, "DebugHub", "logcat có định dạng lạ, không đọc được log native", null);
                    return;
                }
                new Thread(() => Stream(pid, last, lastLines)) { IsBackground = true, Name = "DebugHub logcat" }.Start();
            }
            catch (Exception e)
            {
                LogRecorder.ReceiveNative(DateTime.Now, LogType.Warning, "DebugHub", "Không đọc được logcat: " + e.Message, null);
            }
        }

        private static void Stream(string pid, DateTime after, HashSet<string> seen)
        {
            AndroidJNI.AttachCurrentThread();
            try
            {
                var sink = new Sink(skipUnity: true, LogRecorder.ReceiveNative);
                var since = after.ToString("MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                Read(new[] { "logcat", "-v", "threadtime", "--pid=" + pid, "-T", since }, raw =>
                {
                    if (TryParse(raw, DateTime.Now.Year, out var line) && IsNew(line, raw, after, seen)) sink.Add(line);
                }, sink.Flush);
                sink.Flush();
                // Stream chỉ hết khi tiến trình logcat thoát (từ chối tham số, bị giết): báo, không thì trang log im
                // lặng thiếu log native.
                LogRecorder.ReceiveNative(DateTime.Now, LogType.Warning, "DebugHub", "logcat đã dừng, không còn log native mới", null);
            }
            catch (Exception e)
            {
                LogRecorder.ReceiveNative(DateTime.Now, LogType.Warning, "DebugHub", "logcat dừng: " + e.Message, null);
            }
            finally
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// Chạy lệnh, onLine cho từng dòng tới hết. onIdle khi không còn dòng nào chờ đọc: một lần ghi ra hết các
        /// dòng của nó cùng lúc, nên log đang gom lúc đó đã đủ dòng.
        private static void Read(string[] command, Action<string> onLine, Action onIdle)
        {
            using var runtimeClass = new AndroidJavaClass("java.lang.Runtime");
            using var runtime = runtimeClass.CallStatic<AndroidJavaObject>("getRuntime");
            using var process = runtime.Call<AndroidJavaObject>("exec", (object)command);
            using var input = process.Call<AndroidJavaObject>("getInputStream");
            using var streamReader = new AndroidJavaObject("java.io.InputStreamReader", input);
            using var reader = new AndroidJavaObject("java.io.BufferedReader", streamReader);
            string raw;
            while ((raw = reader.Call<string>("readLine")) != null)
            {
                onLine(raw);
                if (onIdle != null && !reader.Call<bool>("ready")) onIdle();
            }
        }
#endif
    }
}
