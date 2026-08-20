using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IngameDebugConsole;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Storage command của hub. Không đăng ký vào IngameDebugConsole nữa: IDC ở lại làm log window và
    /// parser giá trị (ParseArgument / GetTypeReadableName / FetchArgumentsFromCommand), còn cái gì
    /// hiện lên panel thì hub tự giữ để có description, hình thái, state hiện tại và owner — những
    /// thứ ConsoleMethodInfo không có chỗ để chứa.
    ///
    /// Đăng ký bằng delegate nên sai tên method là lỗi compile, khác với AddCommandInstance(nameof(...)).
    public static class DebugCommands
    {
        private const string RECENT_KEY = "DebugHub.RecentCommands";
        private const int RECENT_LIMIT = 5;
        private const string ERROR_COLOR = "#FF6B6B";

        private static readonly List<DebugCommand> commands = new();
        private static readonly List<string> arguments = new();

        /// Static giữ nguyên giữa các lần Play khi bật "Enter Play Mode without domain reload": không
        /// clear thì command của lần chạy trước còn nguyên và trỏ vào object đã chết.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            commands.Clear();
        }

        /// Command còn sống. Page truy vấn lại mỗi lần dựng, và lần nào cũng dọn thằng có owner đã Destroy.
        public static IReadOnlyList<DebugCommand> All
        {
            get
            {
                commands.RemoveAll(command => !command.Alive);
                return commands;
            }
        }

        #region Đăng ký

        public static DebugCommand Add(Object owner, string path, string description, Action run)
        {
            return Register(new DebugCommand(owner, path, description, null, _ => run()));
        }

        public static DebugCommand Add<T1>(Object owner, string path, string description, Action<T1> run,
            string parameterName = null)
        {
            return Register(new DebugCommand(owner, path, description,
                new[] { Parameter(run, 0, typeof(T1), parameterName) },
                args => run((T1)args[0])));
        }

        public static DebugCommand Add<T1, T2>(Object owner, string path, string description, Action<T1, T2> run,
            string parameterName1 = null, string parameterName2 = null)
        {
            return Register(new DebugCommand(owner, path, description,
                new[]
                {
                    Parameter(run, 0, typeof(T1), parameterName1),
                    Parameter(run, 1, typeof(T2), parameterName2),
                },
                args => run((T1)args[0], (T2)args[1])));
        }

        public static DebugCommand Add<T1, T2, T3>(Object owner, string path, string description,
            Action<T1, T2, T3> run, string parameterName1 = null, string parameterName2 = null,
            string parameterName3 = null)
        {
            return Register(new DebugCommand(owner, path, description,
                new[]
                {
                    Parameter(run, 0, typeof(T1), parameterName1),
                    Parameter(run, 1, typeof(T2), parameterName2),
                    Parameter(run, 2, typeof(T3), parameterName3),
                },
                args => run((T1)args[0], (T2)args[1], (T3)args[2])));
        }

        public static DebugCommand Add<T1, T2, T3, T4>(Object owner, string path, string description,
            Action<T1, T2, T3, T4> run, string parameterName1 = null, string parameterName2 = null,
            string parameterName3 = null, string parameterName4 = null)
        {
            return Register(new DebugCommand(owner, path, description,
                new[]
                {
                    Parameter(run, 0, typeof(T1), parameterName1),
                    Parameter(run, 1, typeof(T2), parameterName2),
                    Parameter(run, 2, typeof(T3), parameterName3),
                    Parameter(run, 3, typeof(T4), parameterName4),
                },
                args => run((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3])));
        }

        /// Row bật/tắt hiện đúng trạng thái hiện tại — hình thái mà cheat kiểu "ẩn/hiện UI", "tắt
        /// tracking" cần: bản cũ chỉ có action flip nên nhìn row không biết đang bật hay tắt.
        public static DebugCommand AddToggle(Object owner, string path, string description, Func<bool> get,
            Action<bool> set)
        {
            return Register(new DebugCommand(owner, path, description,
                new[] { new DebugParameter("value", typeof(bool), () => get()) },
                args => set((bool)args[0])));
        }

        /// Row nhập một giá trị, prefill bằng giá trị đang chạy (time scale, tốc độ...).
        public static DebugCommand AddValue<T>(Object owner, string path, string description, Func<T> get,
            Action<T> set, string parameterName = null)
        {
            return Register(new DebugCommand(owner, path, description,
                new[] { new DebugParameter(parameterName ?? "value", typeof(T), () => get()) },
                args => set((T)args[0])));
        }

        /// Command dạng page: dùng khi tập phần tử chỉ biết được lúc mở (canvas đang có trong scene,
        /// entry trong save...). Tập cố định thì dùng Add/AddToggle; tập theo lifecycle của một object
        /// thì để chính object đó đăng ký với owner = nó.
        public static DebugCommand AddPage(Object owner, string path, string description, Action<DebugHubPanel> page)
        {
            return Register(new DebugCommand(owner, path, description, null, null, page));
        }

        public static void Remove(DebugCommand command)
        {
            commands.Remove(command);
        }

        private static DebugCommand Register(DebugCommand command)
        {
            commands.Add(command);
            return command;
        }

        /// Tên tham số lấy từ chính delegate (method group hay lambda đều được) nên chỗ đăng ký không
        /// phải gõ lại tên; truyền parameterName chỉ khi muốn tên khác tên tham số thật.
        private static DebugParameter Parameter(Delegate run, int index, Type type, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                var parameters = run.Method.GetParameters();
                name = index < parameters.Length ? parameters[index].Name : type.Name;
            }
            return new DebugParameter(name, type);
        }

        #endregion

        #region Chạy

        /// Chạy command với giá trị dạng string theo đúng thứ tự tham số. Thành công thì
        /// <paramref name="message"/> là log mà command in ra lúc chạy, thất bại thì là lý do.
        public static bool TryRun(DebugCommand command, string[] values, out string message)
        {
            if (command.Run == null)
            {
                message = $"{command.Path} là command dạng page, không chạy trực tiếp.";
                return false;
            }

            if (!command.Alive)
            {
                message = $"{command.Path}: owner đã bị destroy.";
                return false;
            }

            var args = new object[command.Parameters.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var parameter = command.Parameters[i];
                var value = values != null && i < values.Length ? values[i] : string.Empty;
                if (DebugLogConsole.ParseArgument(value, parameter.Type, out args[i])) continue;

                message = $"'{value}' không phải {DebugLogConsole.GetTypeReadableName(parameter.Type)} cho {parameter.Name}";
                return false;
            }

            UnityEngine.Debug.Log(Signature(command, values));
            MarkRecent(command);

            // Kết quả của command = log nó in ra trong lúc chạy. Invoke là đồng bộ nên log bắt được ở
            // đây chắc chắn là của nó, và mọi cheat đang Debug.Log sẵn là tự nhiên có kết quả để hiện —
            // khỏi thêm một rừng overload Func<..., TResult> chỉ để lấy giá trị trả về.
            var captured = new StringBuilder();
            Application.LogCallback capture = (condition, stackTrace, type) =>
            {
                if (captured.Length > 0) captured.Append('\n');

                // Command chạy xong nhưng tự log lỗi (prefs.get với key không có) vẫn là "thành công",
                // nên phải tô đỏ ngay trong nội dung — dòng kết quả không có cách nào khác để biết.
                if (type == LogType.Log || type == LogType.Warning) captured.Append(condition);
                else captured.Append("<color=").Append(ERROR_COLOR).Append('>').Append(condition).Append("</color>");
            };

            Exception failure = null;
            Application.logMessageReceived += capture;
            try
            {
                command.Run(args);
            }
            catch (Exception exception)
            {
                failure = exception.InnerException ?? exception;
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            if (failure != null)
            {
                UnityEngine.Debug.LogException(failure);
                message = failure.Message;
                return false;
            }

            message = captured.ToString();
            return true;
        }

        /// Chạy bằng một dòng lệnh: "level.goto 5". Đây là đường cho Proxima (exec từ xa) và cho ô nhập
        /// lệnh của console — bỏ đăng ký vào IDC thì hai chỗ đó mất đường nếu không có hàm này.
        /// Tách tham số bằng FetchArgumentsFromCommand của IDC nên quote/ngoặc xử lý y như console.
        public static bool Execute(string line, out string message)
        {
            arguments.Clear();
            if (!string.IsNullOrEmpty(line)) DebugLogConsole.FetchArgumentsFromCommand(line, arguments);
            if (arguments.Count == 0)
            {
                message = "Câu lệnh rỗng.";
                return false;
            }

            var path = arguments[0];
            var values = new string[arguments.Count - 1];
            for (var i = 0; i < values.Length; i++) values[i] = arguments[i + 1];

            foreach (var command in All)
            {
                if (command.Run == null || command.Parameters.Length != values.Length) continue;
                if (!string.Equals(command.Path, path, StringComparison.OrdinalIgnoreCase)) continue;
                return TryRun(command, values, out message);
            }

            message = $"Không có command '{path}' nhận {values.Length} tham số.";
            return false;
        }

        public static bool Execute(string line)
        {
            var ok = Execute(line, out var message);
            if (!ok) UnityEngine.Debug.LogWarning(message);
            return ok;
        }

        public static string Signature(DebugCommand command, string[] values)
        {
            var builder = new StringBuilder("> ").Append(command.Path);
            if (values != null)
            {
                foreach (var value in values) builder.Append(' ').Append(string.IsNullOrEmpty(value) ? "\"\"" : value);
            }
            return builder.ToString();
        }

        #endregion

        #region Recent

        /// Command chạy gần nhất, mới nhất trước. Path lưu ở PlayerPrefs nên sống qua lần chạy sau;
        /// command nào không còn đăng ký thì tự rơi khỏi danh sách.
        public static IReadOnlyList<DebugCommand> Recent
        {
            get
            {
                var recent = new List<DebugCommand>();
                foreach (var path in RecentPaths())
                {
                    foreach (var command in All)
                    {
                        if (!string.Equals(command.Path, path, StringComparison.OrdinalIgnoreCase)) continue;
                        recent.Add(command);
                        break;
                    }
                }
                return recent;
            }
        }

        internal static List<string> RecentPaths()
        {
            var paths = new List<string>();
            var stored = PlayerPrefs.GetString(RECENT_KEY, string.Empty);
            if (string.IsNullOrEmpty(stored)) return paths;

            foreach (var path in stored.Split('\n'))
            {
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            return paths;
        }

        internal static void MarkRecent(DebugCommand command)
        {
            var paths = RecentPaths();
            paths.RemoveAll(path => string.Equals(path, command.Path, StringComparison.OrdinalIgnoreCase));
            paths.Insert(0, command.Path);
            if (paths.Count > RECENT_LIMIT) paths.RemoveRange(RECENT_LIMIT, paths.Count - RECENT_LIMIT);
            PlayerPrefs.SetString(RECENT_KEY, string.Join("\n", paths));
        }

        #endregion

        #region Giá trị và string

        /// Giá trị khởi tạo cho page nhập liệu: đọc state hiện tại nếu có, không thì một giá trị parse
        /// được (mở page ra là bấm Run được luôn, không phải điền cho đủ).
        public static string[] SeedArgs(DebugCommand command)
        {
            var seed = new string[command.Parameters.Length];
            for (var i = 0; i < seed.Length; i++)
            {
                var parameter = command.Parameters[i];
                seed[i] = parameter.Current != null ? ToText(parameter.Current()) : DefaultValueFor(parameter.Type);
            }
            return seed;
        }

        /// InvariantCulture: máy đặt locale dùng dấu phẩy thập phân thì "0,5" không parse lại được.
        public static string ToText(object value)
        {
            if (value == null) return string.Empty;
            if (value is bool flag) return flag ? "true" : "false";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static string DefaultValueFor(Type type)
        {
            if (type == typeof(bool)) return "false";
            if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                return names.Length > 0 ? names[0] : "0";
            }
            if (type == typeof(string) || type == typeof(char)) return string.Empty;
            if (type.IsPrimitive || type == typeof(decimal)) return "0";
            return string.Empty;
        }

        #endregion
    }
}
