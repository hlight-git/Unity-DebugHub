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
        private const string DEFAULT_CATEGORY = "\uFFFFHub's built-in";

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
                panel.AddNavigation($"{category}  <color=#7A828C>{commands.Count}</color>", CategoryPage(category, commands));
            }
        }

        private static DebugPage CategoryPage(string category, List<ConsoleMethodInfo> commands)
        {
            return new DebugPage(category, panel =>
            {
                foreach (var info in commands)
                {
                    var command = info;
                    var label = LabelOf(command, commands);
                    if (command.parameterTypes.Length == 0)
                    {
                        panel.AddAction(label, () => Run(command, Array.Empty<string>(), null));
                        continue;
                    }
                    panel.AddNavigation(label, ParamsPage(command));
                }
            });
        }

        /// Row chỉ hiện tên command. Overload cùng tên trong một category thì thêm số lượng tham số,
        /// không thì hai row giống nhau y hệt và không biết bấm cái nào.
        private static string LabelOf(ConsoleMethodInfo command, List<ConsoleMethodInfo> siblings)
        {
            var duplicated = false;
            foreach (var sibling in siblings)
            {
                if (sibling == command || sibling.command != command.command) continue;
                duplicated = true;
                break;
            }
            return duplicated ? $"{command.command}  ({command.parameterTypes.Length} args)" : command.command;
        }

        private static DebugPage ParamsPage(ConsoleMethodInfo command)
        {
            // values nằm ngoài builder: page được build lại mỗi lần Push/Pop, để trong closure
            // thì giá trị đã nhập bị reset khi quay lại từ page chọn giá trị.
            var values = new string[command.parameterTypes.Length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = DefaultValueFor(command.parameterTypes[i]);
            }

            return new DebugPage(HeaderOf(command), panel =>
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var index = i;
                    panel.AddField(ParameterLabelOf(command, index), command.parameterTypes[index], values[index],
                        value => values[index] = value);
                }

                var status = panel.AddText(string.Empty);
                panel.AddAction("Run", () => Run(command, values, status));
            });
        }

        private static void Run(ConsoleMethodInfo command, string[] values, Text status)
        {
            if (TryExecute(command, values, out var message))
            {
                if (status) status.gameObject.SetActive(false);
                return;
            }

            if (status)
            {
                status.text = message;
                status.gameObject.SetActive(true);
            }
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

                message = $"'{value}' is not a valid {DebugLogConsole.GetTypeReadableName(command.parameterTypes[i])} for {ParameterLabelOf(command, i)}";
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
            if (command.method != null && command.method.DeclaringType != null && 
                (command.method.DeclaringType.Assembly == typeof(CommandsPage).Assembly || command.method.DeclaringType.Assembly == typeof(DebugLogConsole).Assembly))
            {
                return DEFAULT_CATEGORY;
            }
            var dot = command.command.IndexOf('.');
            return dot > 0 ? command.command.Substring(0, dot) : string.Empty;
        }

        /// Tên command, không kèm chữ ký tham số.
        public static string HeaderOf(ConsoleMethodInfo command)
        {
            return command.command;
        }

        /// `parameters[i]` có dạng "[Int amount]" — và với tham số không phải cuối cùng thì IDC
        /// còn để lại dấu cách ở cuối, nên phải trim whitespace TRƯỚC khi cắt ngoặc.
        /// Chỉ lấy tên tham số; kiểu đã hiện ở placeholder của field.
        public static string ParameterLabelOf(ConsoleMethodInfo command, int index)
        {
            if (command.parameters != null && index < command.parameters.Length)
            {
                var chunk = command.parameters[index].Trim().Trim('[', ']').Trim();
                var space = chunk.LastIndexOf(' ');
                var name = space >= 0 ? chunk.Substring(space + 1) : chunk;
                if (name.Length > 0) return name;
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
