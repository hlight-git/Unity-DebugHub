using System;
using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Page Commands: cây page dựng từ path của node trong <see cref="DebugRegistry"/>. Mỗi dấu '.'
    /// là một tầng (prefs.set.int -> prefs › set › int) nên không còn khái niệm "category" đặc biệt,
    /// chỉ có thư mục và lá; node không có dấu '.' nằm ngay ở tầng đầu.
    ///
    /// Page dựng từ **prefix**, không giữ sẵn danh sách: panel giữ stack khi đóng nên page sống rất
    /// lâu, giữ danh sách thì mở lại có thể dựng row cho node đã rụng (owner bị Destroy).
    internal static class CommandsPage
    {
        public static DebugPage Root() => Folder(Array.Empty<string>(), "Commands");

        internal static DebugPage Folder(string[] prefix, string title)
        {
            return new DebugPage(title, panel => BuildFolder(panel, prefix),
                (panel, query) => SearchFolder(panel, query, prefix),
                subtitle: prefix.Length == 0 ? null : "Commands › " + string.Join(" › ", prefix),
                showTools: prefix.Length == 0);
        }

        private static void BuildFolder(DebugHubPanel panel, string[] prefix)
        {
            var folders = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var leaves = new List<DebugRegistry.Entry>();

            foreach (var entry in DebugRegistry.All)
            {
                var segments = Segments(entry);
                if (!Contains(segments, prefix)) continue;
                if (segments.Length == prefix.Length + 1) { leaves.Add(entry); continue; }

                var folder = segments[prefix.Length];
                folders.TryGetValue(folder, out var count);
                folders[folder] = count + 1;
            }

            if (prefix.Length == 0)
            {
                var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var favoriteCategories = CommandCategoryFavorites.All;
                var hasFavorite = false;
                foreach (var category in favoriteCategories)
                {
                    if (!folders.TryGetValue(category, out var count)) continue;
                    if (!hasFavorite) panel.AddText($"<color={Palette.FAVORITE}><b>Yêu thích</b></color>");
                    hasFavorite = true;
                    AddRootCategory(panel, category, count, true);
                    rendered.Add(category);
                }
                if (hasFavorite && rendered.Count < folders.Count)
                    panel.AddText($"<color={Palette.MUTED}><b>Tất cả</b></color>");
                foreach (var folder in folders)
                {
                    if (rendered.Contains(folder.Key)) continue;
                    AddRootCategory(panel, folder.Key, folder.Value, false);
                }
            }
            else
            {
                foreach (var folder in folders)
                    panel.AddNavigation(folder.Key, Folder(Concat(prefix, folder.Key), folder.Key), null,
                        folder.Value.ToString());
            }

            foreach (var leaf in leaves)
            {
                var entry = leaf;
                NodeRenderer.Render(panel, entry.Node, (node, values) => Dispatch(panel, entry, node, values),
                    LabelOf(entry, leaves));
            }

            if (folders.Count == 0 && leaves.Count == 0) panel.AddText("Chưa có command nào.");
        }

        /// Chỉ tìm trong nhánh đang mở. Không gọi Children của folder động khi tìm registry.
        private static void SearchFolder(DebugHubPanel panel, string query, string[] prefix)
        {
            var matches = new List<DebugRegistry.Entry>();
            foreach (var entry in DebugRegistry.All)
            {
                if (Contains(Segments(entry), prefix) && Matches(entry, query)) matches.Add(entry);
            }
            if (matches.Count == 0) { panel.AddText("Không có command nào khớp."); return; }

            panel.AddPaged(matches, match =>
            {
                var entry = match;
                // Full path qua tham số label của Render, KHÔNG ghi vào entry.Node.Label: node đó
                // còn sống ở ParamsPage/confirm-page kế tiếp (title = node.Label) — ghi đè ở đây sẽ
                // rò rỉ full path vào tiêu đề trang sau, mất luôn tên lá.
                NodeRenderer.Render(panel, entry.Node, (node, values) => Dispatch(panel, entry, node, values),
                    entry.Path);
            });
        }

        private static void AddRootCategory(DebugHubPanel panel, string category, int count, bool favorite)
        {
            panel.AddNavigation(category, Folder(new[] { category }, category), null, count.ToString());
            // Đặc/rỗng chứ không chỉ khác màu: phân biệt được cả khi không nhìn ra màu.
            panel.AttachTrailingAction(favorite ? DebugHubIcon.Symbol.StarFilled : DebugHubIcon.Symbol.Star,
                Palette.ToColor(favorite ? Palette.FAVORITE : Palette.MUTED), () =>
                {
                    CommandCategoryFavorites.Toggle(category);
                    panel.Refresh();
                });
        }

        /// Callback của một leaf đi xuống **cả cây con** của nó: mở một FolderNode do game đăng ký rồi
        /// bấm một row bên trong sẽ bắn về đúng callback này, với node là **con**, không phải leaf.
        /// Chạy `entry` một cách vô điều kiện = bấm gì trong `info.app` cũng chạy `info.app`.
        private static void Dispatch(DebugHubPanel panel, DebugRegistry.Entry entry, DebugNode node, string[] values)
        {
            if (ReferenceEquals(node, entry.Node))
            {
                Run(panel, entry, values);
                return;
            }

            // Node con do game dựng trong AddFolder: không có path nên không vào LastCommand và không
            // có gì để echo ngoài log của chính nó.
            NodeRenderer.RunInspect(panel, node, values);
        }

        /// Cửa vào: node có Confirm thì hỏi trước.
        internal static void Run(DebugHubPanel panel, DebugRegistry.Entry entry, string[] values)
        {
            if (!entry.Node.Confirm)
            {
                RunNow(panel, entry, values);
                return;
            }

            panel.Push(new DebugPage(entry.Node.Label, page =>
            {
                page.AddText($"Xác nhận: <b>{entry.Path} {string.Join(" ", values)}</b>");
                page.AddPrimary("Chạy", () =>
                {
                    page.Pop();
                    RunNow(page, entry, values);
                });
                page.AddButton("Huỷ", page.Pop);
            }, searchable: false));
        }

        internal static void RunNow(DebugHubPanel panel, DebugRegistry.Entry entry, string[] values)
        {
            var ok = DebugRegistry.RunEntry(entry, values, out var message);

            // Lỗi thì luôn hiện. Còn lại chỉ hiện khi node là loại cần đọc kết quả và thật sự có in
            // ra gì: "> view.fps true" hay log của luồng load level nổi giữa màn hình chỉ là rác.
            if (!ok) panel.ShowResult($"{entry.Path}: {message}", true);
            else if (entry.Node.ShowsResult && !string.IsNullOrEmpty(message))
                panel.ShowResult($"> {entry.Path} {string.Join(" ", values)}\n{message}", false);
            else panel.HideResult();

            if (!ok) return;

            switch (entry.Node.Dismiss)
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

        #region Label

        /// Tên hiện trên row: segment cuối (full path ở trang Search đi qua tham số label của
        /// NodeRenderer.Render trực tiếp bằng entry.Path — xem SearchAll, không qua đây). Overload
        /// trùng path trong cùng một thư mục thì thêm số tham số, không thì hai row giống nhau y hệt.
        /// Description do panel format, không cắt: ParamsPage/ActionsPage đọc lại nguyên văn node.Description,
        /// row dài thì tự wrap (GrowRowsToLabel lo phần cao).
        internal static string LabelOf(DebugRegistry.Entry entry, IReadOnlyList<DebugRegistry.Entry> siblings)
        {
            var name = LastSegment(entry);
            return Duplicated(entry, siblings) ? $"{name}  ({ArgCount(entry)} args)" : name;
        }

        private static int ArgCount(DebugRegistry.Entry entry) =>
            entry.Node is ActionNode action ? action.Parameters.Length : 0;

        private static bool Duplicated(DebugRegistry.Entry entry, IReadOnlyList<DebugRegistry.Entry> siblings)
        {
            if (siblings == null) return false;
            foreach (var sibling in siblings)
            {
                if (sibling != entry && string.Equals(sibling.Path, entry.Path, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string LastSegment(DebugRegistry.Entry entry)
        {
            var segments = Segments(entry);
            return segments.Length > 0 ? segments[segments.Length - 1] : entry.Path;
        }

        #endregion

        #region Search

        internal static bool Matches(DebugRegistry.Entry entry, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (entry.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var description = entry.Node.Description;
            return !string.IsNullOrEmpty(description) &&
                   description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Path

        /// Node nằm dưới prefix này (và còn ít nhất một segment nữa để hiện).
        internal static bool Contains(string[] segments, string[] prefix)
        {
            if (segments.Length <= prefix.Length) return false;
            for (var i = 0; i < prefix.Length; i++)
            {
                if (!string.Equals(segments[i], prefix[i], StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static string[] Segments(DebugRegistry.Entry entry) =>
            entry.Path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

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
