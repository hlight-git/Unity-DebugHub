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
    public static class CommandsPage
    {
        private const int DESCRIPTION_LIMIT = 90;

        public static DebugPage Root() => Folder(Array.Empty<string>(), "Commands");

        internal static DebugPage Folder(string[] prefix, string title)
        {
            return new DebugPage(title, panel => BuildFolder(panel, prefix));
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

            foreach (var folder in folders)
                panel.AddNavigation(folder.Key, Folder(Concat(prefix, folder.Key), folder.Key), null, folder.Value.ToString());

            foreach (var leaf in leaves)
            {
                var entry = leaf;
                NodeRenderer.Render(panel, entry.Node, (node, values) => Dispatch(panel, entry, node, values),
                    LabelOf(entry, leaves, false));
            }

            if (folders.Count == 0 && leaves.Count == 0) panel.AddText("No command registered.");
        }

        /// Tìm toàn registry — cái panel gọi khi page không tự khai Search. Quét path + description,
        /// **không** gọi Children của folder động: delegate đó là code của game.
        internal static void SearchAll(DebugHubPanel panel, string query)
        {
            var matches = new List<DebugRegistry.Entry>();
            foreach (var entry in DebugRegistry.All)
            {
                if (Matches(entry, query)) matches.Add(entry);
            }
            if (matches.Count == 0) { panel.AddText("Không có command nào khớp."); return; }

            foreach (var match in matches)
            {
                var entry = match;
                // Full path qua tham số label của Render, KHÔNG ghi vào entry.Node.Label: node đó
                // còn sống ở ParamsPage/confirm-page kế tiếp (title = node.Label) — ghi đè ở đây sẽ
                // rò rỉ full path vào tiêu đề trang sau, mất luôn tên lá.
                NodeRenderer.Render(panel, entry.Node, (node, values) => Dispatch(panel, entry, node, values),
                    entry.Path);
            }
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
            var ok = DebugRegistry.Run(node, values, out var message);
            if (!ok || (node.ShowsResult && !string.IsNullOrEmpty(message))) panel.ShowResult(message, !ok);
            else panel.HideResult();
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
            var ok = DebugRegistry.Run(entry.Node, values, out var message);

            // Lỗi thì luôn hiện. Còn lại chỉ hiện khi node là loại cần đọc kết quả và thật sự có in
            // ra gì: "> view.fps true" hay log của luồng load level nổi giữa màn hình chỉ là rác.
            if (!ok) panel.ShowResult($"{entry.Path}: {message}", true);
            else if (entry.Node.ShowsResult && !string.IsNullOrEmpty(message))
                panel.ShowResult($"> {entry.Path} {string.Join(" ", values)}\n{message}", false);
            else panel.HideResult();

            if (!ok) return;
            RecordLast(entry, values);

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

        /// Ghi lại dòng lệnh cho nút repeat, đối xứng với <see cref="DebugRegistry.Execute"/>: parse
        /// lại giá trị đã chạy thành công (không nối thô chuỗi nhập) rồi giao cho registry mã hoá.
        private static void RecordLast(DebugRegistry.Entry entry, string[] values)
        {
            var types = entry.Node is ActionNode action
                ? Array.ConvertAll(action.Parameters, p => p.Type)
                : new[] { ((ValueNode)entry.Node).Declared };

            var parsed = new object[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                if (!DebugValues.TryParse(values[i], types[i], out parsed[i], out _, allowVars: true)) return;
            }
            DebugRegistry.RecordLastCommand(entry.Path, entry.Node, parsed);
        }

        #region Label

        /// Tên hiện trên row: mặc định là segment cuối (hay full path ở trang Search, nơi tên lá mất
        /// ngữ cảnh). Overload trùng path trong cùng một thư mục thì thêm số tham số, không thì hai
        /// row giống nhau y hệt. Description do panel format.
        ///
        /// ponytail: bản cũ còn cắt bớt description dài (Shorten) trước khi đưa vào row — giữ lại hàm
        /// đó bên dưới cho test, nhưng không gọi ở đây nữa: NodeRenderer.Render không nhận description
        /// riêng cho row, mà node.Description còn bị ParamsPage/ActionsPage đọc lại nguyên văn ở trang
        /// sau — cắt ở đây là cắt luôn cả trang đó. Row dài thì tự wrap (GrowRowsToLabel lo phần cao).
        internal static string LabelOf(DebugRegistry.Entry entry, IReadOnlyList<DebugRegistry.Entry> siblings,
            bool fullPath)
        {
            var name = fullPath ? entry.Path : LastSegment(entry);
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

        /// Description dài thì cắt ở khoảng trắng gần nhất — toàn văn nằm ở page Help.
        internal static string Shorten(string description)
        {
            if (description.Length <= DESCRIPTION_LIMIT) return description;

            var cut = description.LastIndexOf(' ', DESCRIPTION_LIMIT);
            if (cut < DESCRIPTION_LIMIT / 2) cut = DESCRIPTION_LIMIT;
            return description.Substring(0, cut) + "…";
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
