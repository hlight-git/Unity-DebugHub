using System;
using System.Collections.Generic;
using System.Text;
using IngameDebugConsole;

namespace Hlight.Debug.Hub
{
    internal static class HelpPage
    {
        public static DebugPage Build() => new DebugPage("Trợ giúp", panel => panel.AddText(BuildText()));

        private static string BuildText()
        {
            var builder = new StringBuilder();
            builder.Append("<b>Ghi chú:</b>");
            foreach (var note in DebugHub.Notes)
            {
                builder.Append("\n  + ").Append(note);
            }

            // Trang này là chỗ đọc hết mọi command trong một khối text, không phải dò từng row.
            builder.Append("\n\n<b>Command:</b>");
            var entries = new List<DebugRegistry.Entry>(DebugRegistry.All);
            entries.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));

            foreach (var entry in entries)
            {
                builder.Append("\n  - ").Append(entry.Path);
                AppendShape(builder, entry.Node);
                if (!string.IsNullOrEmpty(entry.Node.Description))
                    builder.Append("  <color=").Append(Palette.DIM).Append('>').Append(entry.Node.Description).Append("</color>");
            }
            return builder.ToString();
        }

        /// Loại node quyết định gì hiện sau path: ActionNode liệt kê kiểu tham số như trước,
        /// ValueNode in giá trị hiện tại, FolderNode chỉ báo có thể mở tiếp.
        private static void AppendShape(StringBuilder builder, DebugNode node)
        {
            switch (node)
            {
                case ActionNode action:
                    foreach (var parameter in action.Parameters)
                    {
                        builder.Append(" [").Append(DebugLogConsole.GetTypeReadableName(parameter.Type)).Append(' ')
                            .Append(parameter.Name).Append(']');
                    }
                    return;

                case ValueNode value:
                    // Getter của Unity ném khá thường (component đã chết) — một dòng lỗi tốt hơn
                    // cả trang Help vỡ ngang chừng, xem NodeRenderer.RenderValue.
                    try
                    {
                        var current = value.Get();
                        builder.Append(" = ").Append(current is null ? "null" : DebugValues.ToText(current));
                    }
                    catch (Exception exception) { builder.Append(" = <color=").Append(Palette.BAD).Append('>').Append(exception.Message).Append("</color>"); }
                    return;

                case FolderNode:
                    builder.Append(" ›");
                    return;
            }
        }
    }
}
