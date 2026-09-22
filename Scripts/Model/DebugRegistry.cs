using System;
using System.Collections.Generic;
using System.Text;
using IngameDebugConsole;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Storage node của hub. IDC ở lại làm log window và parser giá trị; cái gì hiện lên panel
    /// thì hub tự giữ để có description, hình thái, owner — thứ ConsoleMethodInfo không chứa được.
    public static class DebugRegistry
    {
        private const string LAST_KEY = "DebugHub.LastCommand";
        private const string ERROR_COLOR = "#E5484D";

        public sealed class Entry
        {
            public string Path;          // nguyên văn lúc đăng ký, để hiển thị
            public string Lookup;        // ToLowerInvariant, để tra cứu
            public DebugNode Node;
            public Object Owner;
            public bool Owned;
            public bool Alive => !Owned || Owner;
        }

        private static readonly List<Entry> entries = new();
        private static readonly Dictionary<string, string[]> argsByKey = new();
        private static readonly List<string> arguments = new();

        /// Static giữ nguyên giữa các lần Play khi bật "Enter Play Mode without domain reload":
        /// không clear thì node của lần chạy trước còn nguyên và trỏ vào object đã chết.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
            argsByKey.Clear();
        }

        public static IReadOnlyList<Entry> All
        {
            get
            {
                entries.RemoveAll(entry => !entry.Alive);
                return entries;
            }
        }

        public static string LastCommand => PlayerPrefs.GetString(LAST_KEY, string.Empty);

        public static void ClearLastCommand()
        {
            PlayerPrefs.DeleteKey(LAST_KEY);
            LastCommandChanged?.Invoke();
        }

        internal static T Register<T>(Object owner, string path, T node) where T : DebugNode
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path rỗng.", nameof(path));

            var segments = path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            node.Label ??= segments.Length > 0 ? segments[segments.Length - 1] : path;

            var lookup = path.ToLowerInvariant();
            node.Key = KeyFor(lookup, node);

            // Dọn entry chết cùng path TRƯỚC khi xét trùng: entry chỉ rụng lúc All được đọc, mà
            // scene load lại thì Register() chạy trước khi ai đọc All — không dọn thì object mới
            // bị chính bản cũ của nó chặn.
            entries.RemoveAll(entry => !entry.Alive && entry.Lookup == lookup);

            foreach (var entry in entries)
            {
                if (entry.Lookup != lookup) continue;

                // Chỉ ActionNode được trùng path khi khác arity. ValueNode/FolderNode chiếm riêng
                // exact path của mình: `Execute("view.fps true")` không được phép lưỡng lự giữa
                // một ValueNode và một ActionNode một tham số ở cùng path.
                var sameShape = entry.Node.Key == node.Key;
                var exclusive = entry.Node is ValueNode || entry.Node is FolderNode ||
                                node is ValueNode || node is FolderNode;
                if (!sameShape && !exclusive) continue;

                // LogError chứ không throw: một cheat không có quyền làm sập game. Vẫn trả node
                // hợp lệ (chưa đăng ký) để .Stays()/.Defaults() nối tiếp không NRE.
                UnityEngine.Debug.LogError($"[DebugHub] '{path}' đụng một node đã đăng ký " +
                                           $"({entry.Node.Key} vs {node.Key}). Bỏ qua lần đăng ký này.");
                return node;
            }

            // Địa chỉ `@path` của ValueNode đăng ký: nhờ nó mà nút … trên một row cheat watch được
            // (§9.4). Không gán ở đây thì `@economy.coin` chỉ resolve được khi gõ tay ở Execute.
            if (node is ValueNode registered) registered.Address = "@" + lookup;

            entries.Add(new Entry { Path = path, Lookup = lookup, Node = node, Owner = owner, Owned = owner });
            return node;
        }

        /// Key cố định ngay khi đăng ký, kể cả khi chỉ có một node ở path đó: thêm overload sau
        /// không được làm dời tham số người dùng đã nhập.
        private static string KeyFor(string lookup, DebugNode node)
        {
            return node switch
            {
                ActionNode action => $"cmd:{lookup}#{action.Parameters.Length}",
                ValueNode => $"cmd:{lookup}#value",
                FolderNode => $"cmd:{lookup}#folder",
                _ => $"cmd:{lookup}#text",
            };
        }

        internal static void Remove(DebugNode node)
        {
            entries.RemoveAll(entry => entry.Node == node);
            // Bỏ luôn args đã lưu: node bị gỡ tay (DebugHub.Remove, hoặc TearDown của test) coi như
            // xong hẳn — không để lại default rác cho một node KHÁC lỡ đăng ký cùng path sau này.
            // Khác với path "owner chết", path đó không qua đây (chỉ lọc entry chết trong Register/All)
            // nên chuyện "giữ giá trị đã gõ qua reload scene" ở Defaults() không bị ảnh hưởng.
            if (node.Key != null) argsByKey.Remove(node.Key);
        }

        internal static string[] ArgsFor(DebugNode node)
        {
            if (node is not ActionNode action) return Array.Empty<string>();
            if (node.Key != null && argsByKey.TryGetValue(node.Key, out var stored) &&
                stored.Length == action.Parameters.Length) return stored;

            var seed = new string[action.Parameters.Length];
            for (var i = 0; i < seed.Length; i++) seed[i] = DebugValues.DefaultValueFor(action.Parameters[i].Type);
            return seed;
        }

        internal static void StoreArgs(DebugNode node, string[] args)
        {
            if (node.Key != null) argsByKey[node.Key] = args;
        }

        internal static bool HasArgs(DebugNode node) => node.Key != null && argsByKey.ContainsKey(node.Key);

        #region Chạy

        /// Chạy một node với giá trị dạng string theo đúng thứ tự tham số. Thành công thì
        /// message là log node in ra lúc chạy, thất bại thì là lý do.
        internal static bool Run(DebugNode node, string[] values, out string message)
        {
            switch (node)
            {
                case ActionNode action: return RunAction(action, values, out message);
                case ValueNode value: return RunValue(value, values, out message);
                default:
                    message = $"{node.Label} là thư mục, không chạy trực tiếp được.";
                    return false;
            }
        }

        private static bool RunAction(ActionNode action, string[] values, out string message)
        {
            var args = new object[action.Parameters.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var parameter = action.Parameters[i];
                var text = values != null && i < values.Length ? values[i] : string.Empty;
                // allowVars: page nhập tham số nhận `$var`, nên lúc chạy cũng phải nhận — không thì
                // ô nhập báo hợp lệ rồi bấm Run lại báo sai.
                if (DebugValues.TryParse(text, parameter.Type, out args[i], out var error, allowVars: true)) continue;
                message = $"{parameter.Name}: {error}";
                return false;
            }

            return Capture(() => action.Invoke(args), out message);
        }

        private static bool RunValue(ValueNode value, string[] values, out string message)
        {
            if (values == null || values.Length == 0)
            {
                message = DebugValues.ToText(value.Get());
                return true;
            }
            if (value.Set == null)
            {
                message = $"{value.Label} là read-only.";
                return false;
            }
            if (!DebugValues.TryParse(values[0], value.Declared, out var parsed, out var error, allowVars: true))
            {
                message = error;
                return false;
            }
            return Capture(() => value.Set(parsed), out message);
        }

        /// Kết quả của node = log nó in ra trong lúc chạy. Invoke là đồng bộ nên log bắt được ở
        /// đây chắc chắn là của nó, và mọi cheat đang Debug.Log sẵn là tự nhiên có kết quả để hiện.
        private static bool Capture(Action run, out string message)
        {
            var captured = new StringBuilder();
            Application.LogCallback capture = (condition, _, type) =>
            {
                if (captured.Length > 0) captured.Append('\n');
                if (type == LogType.Log || type == LogType.Warning) captured.Append(condition);
                else captured.Append("<color=").Append(ERROR_COLOR).Append('>').Append(condition).Append("</color>");
            };

            Exception failure = null;
            Application.logMessageReceived += capture;
            try { run(); }
            catch (Exception exception) { failure = exception.InnerException ?? exception; }
            finally { Application.logMessageReceived -= capture; }

            if (failure != null)
            {
                UnityEngine.Debug.LogException(failure);
                message = failure.Message;
                return false;
            }
            message = captured.ToString();
            return true;
        }

        /// Ghi lại dòng lệnh cho nút repeat. Encode từ **giá trị đã parse sau khi chạy thành công**,
        /// không nối thô chuỗi nhập. Giá trị nào không có biểu diễn độc lập với session (reference
        /// Unity) thì xoá hẳn bản lưu, không để nút trỏ vào lệnh cũ.
        internal static void RecordLastCommand(string path, DebugNode node, object[] parsed)
        {
            var line = new StringBuilder(path);
            var types = node is ActionNode action
                ? Array.ConvertAll(action.Parameters, p => p.Type)
                : new[] { ((ValueNode)node).Declared };

            for (var i = 0; i < parsed.Length; i++)
            {
                if (!DebugValues.TryToArgument(parsed[i], types[i], out var text))
                {
                    ClearLastCommand();
                    return;
                }
                line.Append(' ').Append(text);
            }
            PlayerPrefs.SetString(LAST_KEY, line.ToString());
            LastCommandChanged?.Invoke();
        }

        /// Nút repeat sống ngoài panel nên không thấy được lúc nào có lệnh mới; không có event này
        /// thì nhãn của nó đứng im tới lần bật/tắt sau.
        internal static event Action LastCommandChanged;

        /// Chạy bằng một dòng lệnh: "level.goto 5". Đường cho Proxima (exec từ xa) và cho ô nhập
        /// lệnh của console. Tách tham số bằng FetchArgumentsFromCommand nên quote xử lý y như console.
        internal static bool Execute(string line, out string message)
        {
            var values = ArgumentsOf(line);
            if (values == null)
            {
                message = "Câu lệnh rỗng.";
                return false;
            }
            if (!Find(line, out var entry))
            {
                message = $"Không có command '{PathOf(line)}' nhận {values.Length} tham số.";
                return false;
            }

            var ok = Run(entry.Node, values, out message);
            if (ok && values.Length > 0) RecordParsed(entry, values);
            return ok;
        }

        /// Tách khỏi Execute vì nút repeat phải hỏi "lệnh này là node nào" (để biết nó có
        /// Confirms() không) mà **không** được chạy nó.
        internal static bool Find(string line, out Entry found)
        {
            found = null;
            var values = ArgumentsOf(line);
            if (values == null) return false;

            var path = PathOf(line).ToLowerInvariant();
            foreach (var entry in All)
            {
                if (entry.Lookup != path) continue;
                if (entry.Node is ActionNode action && action.Parameters.Length != values.Length) continue;
                if (entry.Node is ValueNode && values.Length > 1) continue;
                found = entry;
                return true;
            }
            return false;
        }

        /// Đối số sau path. null = dòng lệnh rỗng. Tách bằng parser của IDC nên quote xử lý y như console.
        internal static string[] ArgumentsOf(string line)
        {
            arguments.Clear();
            if (!string.IsNullOrEmpty(line)) DebugLogConsole.FetchArgumentsFromCommand(line, arguments);
            if (arguments.Count == 0) return null;

            var values = new string[arguments.Count - 1];
            for (var i = 0; i < values.Length; i++) values[i] = arguments[i + 1];
            return values;
        }

        private static string PathOf(string line)
        {
            arguments.Clear();
            if (!string.IsNullOrEmpty(line)) DebugLogConsole.FetchArgumentsFromCommand(line, arguments);
            return arguments.Count > 0 ? arguments[0] : string.Empty;
        }

        private static void RecordParsed(Entry entry, string[] values)
        {
            var types = entry.Node is ActionNode action
                ? Array.ConvertAll(action.Parameters, p => p.Type)
                : new[] { ((ValueNode)entry.Node).Declared };
            var parsed = new object[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                if (!DebugValues.TryParse(values[i], types[i], out parsed[i], out _)) return;
            }
            RecordLastCommand(entry.Path, entry.Node, parsed);
        }

        #endregion
    }
}
