using System;
using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Factory ngắn cho chỗ dựng nội dung. Chỗ gọi viết `Node.Value(...)`, không phải
    /// `new ValueNode { ... }` với sáu dòng khởi tạo.
    public static class Node
    {
        public static ValueNode Value<T>(string label, Func<T> get, Action<T> set = null, string description = null)
        {
            return new ValueNode
            {
                Label = label,
                Description = description,
                Declared = typeof(T),
                Get = () => get(),
                Set = set == null ? null : v => set((T)v),
                Dismiss = DismissMode.Stay,
            };
        }

        public static ActionNode Action(string label, System.Action run, string description = null)
        {
            return new ActionNode
            {
                Label = label,
                Description = description,
                Invoke = _ => run(),
                Dismiss = DismissMode.ClosePanel,
            };
        }

        public static FolderNode Folder(string label, Func<IEnumerable<DebugNode>> children,
            string description = null, bool live = false)
        {
            return new FolderNode { Label = label, Description = description, Children = children, Live = live };
        }

        /// Nhóm có tiêu đề, dựng ngay trong trang cha. Con cố định nên nhận thẳng mảng.
        public static FolderNode Section(string title, IEnumerable<DebugNode> children)
        {
            return new FolderNode { Label = title, Children = () => children, Inline = true };
        }

        public static TextNode Text(string text, TextStyle style = TextStyle.Normal)
        {
            return new TextNode { Label = text, Text = text, Style = style };
        }

        /// Bảng monospace. Xem DebugTable — format là hàm thuần, node chỉ mang kết quả.
        public static TextNode Table(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            return new TextNode { Text = DebugTable.Format(headers, rows), Style = TextStyle.Table };
        }
    }
}
