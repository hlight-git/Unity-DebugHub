using System;
using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Page Commands: cây page dựng từ path của command trong storage của hub. Mỗi dấu '.' là một
    /// tầng (prefs.set.int -> prefs › set › int) nên không còn khái niệm "category" đặc biệt, chỉ có
    /// thư mục và lá; command không có dấu '.' nằm ngay ở tầng đầu.
    ///
    /// Page dựng từ **prefix**, không giữ sẵn danh sách command: panel giữ stack khi đóng nên page
    /// sống rất lâu, giữ danh sách thì mở lại có thể dựng row cho command đã rụng (owner bị Destroy).
    public static class CommandsPage
    {
        private const int DESCRIPTION_LIMIT = 90;

        private static string query = string.Empty;

        private const string BUILT_IN = "Built-in";

        public static DebugPage Root() => Folder(Array.Empty<string>(), "Commands", false);

        /// <paramref name="builtIn"/> chọn nửa nào của cây: command của game, hay command có sẵn của
        /// package. Hai nửa không trộn vào nhau ở bất kỳ tầng nào.
        internal static DebugPage Folder(string[] prefix, string title, bool builtIn)
        {
            return new DebugPage(title, panel => BuildFolder(panel, prefix, builtIn));
        }

        private static void BuildFolder(DebugHubPanel panel, string[] prefix, bool builtIn)
        {
            var folders = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var leaves = new List<DebugCommand>();
            var builtInCount = 0;

            foreach (var command in DebugCommands.All)
            {
                if (command.IsBuiltIn != builtIn)
                {
                    if (command.IsBuiltIn) builtInCount++;
                    continue;
                }
                if (!Contains(command.Segments, prefix)) continue;

                if (command.Segments.Length == prefix.Length + 1)
                {
                    leaves.Add(command);
                    continue;
                }

                var folder = command.Segments[prefix.Length];
                folders.TryGetValue(folder, out var count);
                folders[folder] = count + 1;
            }

            // Recent/Search dò cả hai nửa nên chỉ cần ở gốc của cây game.
            if (prefix.Length == 0 && !builtIn)
            {
                var recent = DebugCommands.Recent;
                if (recent.Count > 0) panel.AddShortcut("Recent", RecentPage(), recent.Count.ToString());
                panel.AddShortcut("Search", SearchPage());
            }

            foreach (var folder in folders)
            {
                var name = folder.Key;
                panel.AddNavigation(name, Folder(Concat(prefix, name), name, builtIn), null, folder.Value.ToString());
            }

            foreach (var leaf in leaves) AddLeaf(panel, leaf, leaves, false);

            if (folders.Count == 0 && leaves.Count == 0) panel.AddText("No command registered.");

            // Cuối cùng, sau cả leaf: đồ có sẵn của package không được chen vào giữa cheat của game.
            if (prefix.Length == 0 && !builtIn && builtInCount > 0)
            {
                panel.AddShortcut(BUILT_IN, Folder(Array.Empty<string>(), BUILT_IN, true), builtInCount.ToString());
            }
        }

        /// Một row cho một command, hình thái theo <see cref="DebugCommand.IsInstant"/> /
        /// <see cref="DebugCommand.IsInline"/> / <see cref="DebugCommand.Page"/>.
        /// <paramref name="fullPath"/> = true ở page Recent/Search, nơi chỉ có tên lá thì mất ngữ cảnh.
        private static void AddLeaf(DebugHubPanel panel, DebugCommand command, IReadOnlyList<DebugCommand> siblings,
            bool fullPath)
        {
            var label = LabelOf(command, siblings, fullPath);
            var description = string.IsNullOrEmpty(command.Description) ? null : Shorten(command.Description);

            if (command.Page != null)
            {
                panel.AddNavigation(label, new DebugPage(command.Path, command.Page), description);
                return;
            }

            if (command.IsInstant)
            {
                panel.AddAction(label, () => ConfirmThenRun(panel, command, Array.Empty<string>()), description);
                return;
            }

            if (command.IsInline)
            {
                var parameter = command.Parameters[0];
                panel.AddField(label, parameter.Type, DebugCommands.ToText(parameter.Current()), null,
                    value => ConfirmThenRun(panel, command, new[] { value }), description);
                return;
            }

            panel.AddNavigation(label, ParamsPage(command), description);
        }

        /// Tên hiện trên row: mặc định là segment cuối. Overload trùng tên trong cùng một thư mục thì
        /// thêm số tham số, không thì hai row giống nhau y hệt. Description do panel format.
        internal static string LabelOf(DebugCommand command, IReadOnlyList<DebugCommand> siblings, bool fullPath)
        {
            var name = fullPath ? command.Path : command.Label;
            return Duplicated(command, siblings) ? $"{name}  ({command.Parameters.Length} args)" : name;
        }

        private static bool Duplicated(DebugCommand command, IReadOnlyList<DebugCommand> siblings)
        {
            if (siblings == null) return false;
            foreach (var sibling in siblings)
            {
                if (sibling != command && string.Equals(sibling.Path, command.Path, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// Description dài thì cắt ở khoảng trắng gần nhất — toàn văn nằm ở page Help.
        internal static string Shorten(string description)
        {
            if (description.Length <= DESCRIPTION_LIMIT) return description;

            var cut = description.LastIndexOf(' ', DESCRIPTION_LIMIT);
            if (cut < DESCRIPTION_LIMIT / 2) cut = DESCRIPTION_LIMIT;
            return description.Substring(0, cut) + "…";
        }

        /// Page nhập liệu cho command nhiều tham số. Giá trị nằm ở <see cref="DebugCommand.Args"/> chứ
        /// không phải trong closure: page được dựng lại mỗi lần điều hướng (kể cả khi back từ page chọn
        /// enum) nên để trong closure là mất cái vừa nhập, và giữ ở đây thì lần sau vào không phải gõ lại.
        private static DebugPage ParamsPage(DebugCommand command)
        {
            return new DebugPage(command.Path, panel =>
            {
                if (command.Args == null || command.Args.Length != command.Parameters.Length)
                    command.Args = DebugCommands.SeedArgs(command);
                var values = command.Args;

                if (!string.IsNullOrEmpty(command.Description)) panel.AddText(command.Description);

                for (var i = 0; i < command.Parameters.Length; i++)
                {
                    var index = i;
                    var parameter = command.Parameters[index];
                    panel.AddField(parameter.Name, parameter.Type, values[index], value => values[index] = value);
                }

                panel.AddPrimary("Run", () => ConfirmThenRun(panel, command, values));
            });
        }

        private static void ConfirmThenRun(DebugHubPanel panel, DebugCommand command, string[] values)
        {
            if (!command.Confirm)
            {
                Run(panel, command, values);
                return;
            }

            panel.Push(new DebugPage(command.Label, page =>
            {
                page.AddText($"Xác nhận: <b>{DebugCommands.Signature(command, values)}</b>");
                page.AddPrimary("Chạy", () =>
                {
                    page.Pop();
                    Run(page, command, values);
                });
                page.AddButton("Huỷ", page.Pop);
            }));
        }

        private static void Run(DebugHubPanel panel, DebugCommand command, string[] values)
        {
            var ok = DebugCommands.TryRun(command, values, out var message);

            // Lỗi thì luôn hiện. Còn lại chỉ hiện khi command là loại cần đọc kết quả và thật sự có
            // in ra gì: "> view.fps true" hay log của luồng load level nổi giữa màn hình chỉ là rác.
            if (!ok) panel.ShowResult($"{command.Path}: {message}", true);
            else if (command.ShowsResult && !string.IsNullOrEmpty(message))
                panel.ShowResult(Result(command, values, message), false);
            else panel.HideResult();

            if (!ok) return;

            switch (command.Dismiss)
            {
                case DismissMode.ClosePanel:
                    panel.Close();
                    break;

                case DismissMode.HideHub:
                    // Close() cả ở đây: không có DebugHub trong scene (panel dùng riêng, test) thì
                    // Visible không làm gì và panel sẽ nằm nguyên đó.
                    DebugHub.Visible = false;
                    panel.Close();
                    break;
            }
        }

        /// Dòng lệnh vừa chạy + log mà nó in ra. Echo lại dòng lệnh để biết log đó của command nào.
        private static string Result(DebugCommand command, string[] values, string message)
        {
            return $"{DebugCommands.Signature(command, values)}\n{message}";
        }

        #region Recent, Search

        private static DebugPage RecentPage()
        {
            return new DebugPage("Recent", panel =>
            {
                var recent = DebugCommands.Recent;
                if (recent.Count == 0)
                {
                    panel.AddText("Chưa chạy command nào.");
                    return;
                }
                foreach (var command in recent) AddLeaf(panel, command, recent, true);
            });
        }

        private static DebugPage SearchPage()
        {
            return new DebugPage("Search", panel =>
            {
                // Nút riêng chứ không tìm ngay lúc onEndEdit: chốt bằng field thì Push() sẽ destroy
                // đúng cái InputField đang bắn event.
                panel.AddField("Từ khoá", typeof(string), query, value => query = value);
                panel.AddPrimary("Tìm", () => panel.Push(ResultsPage()));
            });
        }

        private static DebugPage ResultsPage()
        {
            return new DebugPage($"\"{query}\"", panel =>
            {
                var matches = new List<DebugCommand>();
                foreach (var command in DebugCommands.All)
                {
                    if (Matches(command, query)) matches.Add(command);
                }

                if (matches.Count == 0)
                {
                    panel.AddText("Không có command nào khớp.");
                    return;
                }
                foreach (var command in matches) AddLeaf(panel, command, matches, true);
            });
        }

        internal static bool Matches(DebugCommand command, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return true;
            if (command.Path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(command.Description) &&
                   command.Description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Path

        /// Command nằm dưới prefix này (và còn ít nhất một segment nữa để hiện).
        internal static bool Contains(string[] segments, string[] prefix)
        {
            if (segments.Length <= prefix.Length) return false;
            for (var i = 0; i < prefix.Length; i++)
            {
                if (!string.Equals(segments[i], prefix[i], StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static string[] Concat(string[] prefix, string segment)
        {
            var next = new string[prefix.Length + 1];
            Array.Copy(prefix, next, prefix.Length);
            next[prefix.Length] = segment;
            return next;
        }

        #endregion
    }
}
