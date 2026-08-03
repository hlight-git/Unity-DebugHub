using System;
using System.Collections.Generic;
using IngameDebugConsole;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Page Commands: lấy thẳng command registry của IngameDebugConsole, chia theo category
    /// (phần trước dấu '.' đầu tiên) nên cheat chỉ cần đăng ký một lần là dùng được cả console lẫn panel.
    public static class CommandsPage
    {
        private const string DEFAULT_CATEGORY = "General";

        public static DebugPage Root() => new DebugPage("Commands", BuildRoot);

        private static void BuildRoot(DebugHubPanel panel)
        {
            var groups = Group(DebugLogConsole.GetAllCommands());
            if (groups.Count == 0)
            {
                panel.AddText("No command registered.");
                return;
            }

            foreach (var group in groups)
            {
                var category = group.Key;
                var commands = group.Value;
                panel.AddButton($"{category}  ({commands.Count})", () => panel.Push(CategoryPage(category, commands)));
            }
        }

        private static DebugPage CategoryPage(string category, List<ConsoleMethodInfo> commands)
        {
            return new DebugPage(category, panel =>
            {
                foreach (var info in commands)
                {
                    var command = info;
                    if (command.parameterTypes.Length == 0)
                    {
                        panel.AddButton(HeaderOf(command), () => Run(command, Array.Empty<string>(), null));
                        continue;
                    }
                    panel.AddButton(HeaderOf(command), () => panel.Push(ParamsPage(command)));
                }
            });
        }

        private static DebugPage ParamsPage(ConsoleMethodInfo command)
        {
            return new DebugPage(HeaderOf(command), panel =>
            {
                var values = new string[command.parameterTypes.Length];
                for (var i = 0; i < values.Length; i++)
                {
                    var index = i;
                    var type = command.parameterTypes[i];
                    values[i] = DefaultValueFor(type);
                    panel.AddField(LabelOf(command, i), type, values[i], value => values[index] = value);
                }

                var status = panel.AddText(string.Empty);
                panel.AddButton("Run", () => Run(command, values, status));
            });
        }

        private static void Run(ConsoleMethodInfo command, string[] values, Text status)
        {
            if (TryExecute(command, values, out var message))
            {
                if (status) status.text = string.Empty;
                return;
            }

            if (status) status.text = message;
            UnityEngine.Debug.LogWarning(message);
        }

        /// Gọi thẳng MethodInfo thay vì DebugLogConsole.ExecuteCommand(string): đi qua string thì
        /// overload cùng tên (time.skip, get, set) có thể bị chọn sai.
        public static bool TryExecute(ConsoleMethodInfo command, string[] values, out string message)
        {
            var args = new object[command.parameterTypes.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var value = i < values.Length ? values[i] : string.Empty;
                if (DebugLogConsole.ParseArgument(value, command.parameterTypes[i], out args[i])) continue;

                message = $"'{value}' is not a valid {DebugLogConsole.GetTypeReadableName(command.parameterTypes[i])} for {LabelOf(command, i)}";
                return false;
            }

            UnityEngine.Debug.Log($"> {command.command} {string.Join(" ", values)}".TrimEnd());
            try
            {
                var returned = command.method.Invoke(command.instance, args);
                if (command.method.ReturnType != typeof(void)) UnityEngine.Debug.Log(returned);
            }
            catch (Exception exception)
            {
                var actual = exception.InnerException ?? exception;
                UnityEngine.Debug.LogException(actual);
                message = actual.Message;
                return false;
            }

            message = null;
            return true;
        }

        public static SortedDictionary<string, List<ConsoleMethodInfo>> Group(IEnumerable<ConsoleMethodInfo> commands)
        {
            var groups = new SortedDictionary<string, List<ConsoleMethodInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in commands)
            {
                if (!command.IsValid()) continue;

                var category = CategoryOf(command);
                if (!groups.TryGetValue(category, out var list))
                {
                    list = new List<ConsoleMethodInfo>();
                    groups[category] = list;
                }
                list.Add(command);
            }
            return groups;
        }

        public static string CategoryOf(ConsoleMethodInfo command)
        {
            var dot = command.command.IndexOf('.');
            return dot > 0 ? command.command.Substring(0, dot) : DEFAULT_CATEGORY;
        }

        /// `signature` có dạng "&lt;b&gt;cmd [Int a]&lt;/b&gt;: description" — chỉ lấy phần trong thẻ b.
        public static string HeaderOf(ConsoleMethodInfo command)
        {
            var signature = command.signature;
            var end = signature.IndexOf("</b>", StringComparison.Ordinal);
            if (end >= 0) signature = signature.Substring(0, end);
            return signature.Replace("<b>", string.Empty).Trim();
        }

        /// `parameters[i]` có dạng "[Int amount]".
        private static string LabelOf(ConsoleMethodInfo command, int index)
        {
            if (command.parameters != null && index < command.parameters.Length)
            {
                var label = command.parameters[index].Trim('[', ']').Trim();
                if (label.Length > 0) return label;
            }
            return DebugLogConsole.GetTypeReadableName(command.parameterTypes[index]);
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
    }
}
