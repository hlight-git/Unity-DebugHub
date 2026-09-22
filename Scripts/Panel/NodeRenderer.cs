using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Nơi **duy nhất** biết node nào ra row nào. Trang command, trang folder do game đăng ký,
    /// trang member của inspect và trang Watch đều đi qua đây — nếu không, cây quyết định
    /// khó nhất của package sẽ tồn tại hai bản.
    ///
    /// Xét từ trên xuống theo đúng thứ tự bảng §3 của spec.
    public static class NodeRenderer
    {
        /// <paramref name="label"/> ghi đè chữ hiện trên row (mặc định <see cref="DebugNode.Label"/> —
        /// chỉ segment cuối). CommandsPage.SearchAll dùng để hiện full path thay vì tên lá, không thì
        /// kết quả search mất hết ngữ cảnh (nhiều node trùng tên lá ở path khác nhau).
        public static void Render(DebugHubPanel panel, DebugNode node, Action<DebugNode, string[]> run,
            string label = null)
        {
            label ??= node.Label;
            switch (node)
            {
                case TextNode text: RenderText(panel, text); return;
                case FolderNode folder: RenderFolder(panel, folder, run, label); return;
                case ActionNode action: RenderAction(panel, action, run, label); return;
                case ValueNode value: RenderValue(panel, value, run, label); return;
            }
        }

        private static void RenderValue(DebugHubPanel panel, ValueNode node, Action<DebugNode, string[]> run,
            string label)
        {
            object current;
            try
            {
                current = node.Get();
            }
            catch (Exception exception)
            {
                // Getter của Unity ném khá thường (component đã chết, property obsolete).
                // Một row lỗi tốt hơn là cả trang không dựng được.
                panel.AddText($"{label}: <color={Palette.BAD}>{exception.Message}</color>");
                return;
            }

            if (IsNull(current))
            {
                // Vẫn dựng row có nút … : một field object đang null chính là chỗ hay cần `Gán`
                // nhất, và địa chỉ của nó watch được như thường.
                panel.AddCopyRow(label, "null", node.Description);
                panel.AttachActions(node, null, run);
                return;
            }

            if (DebugValues.IsInlineValue(node.Declared))
            {
                if (node.Set == null)
                {
                    var text = DebugValues.ToText(current);
                    panel.AddCopyRow(label, text, node.Description);
                }
                else
                {
                    panel.AddField(label, node.Declared, DebugValues.ToText(current), null,
                        value => run(node, new[] { value }), node.Description);
                }
                panel.AttachActions(node, current, run);
                return;
            }

            // Có parser (GameObject/Component/List) nhưng vẫn phải mở ra xem được.
            panel.AddNavigation(label, MembersPage(node), node.Description, Summary(current));
            panel.AttachActions(node, current, run);
        }

        private static void RenderAction(DebugHubPanel panel, ActionNode node, Action<DebugNode, string[]> run,
            string label)
        {
            if (node.Parameters.Length == 0)
            {
                panel.AddAction(label, () => run(node, Array.Empty<string>()), node.Description);
                return;
            }
            panel.AddNavigation(label, ParamsPage.For(node, run), node.Description);
        }

        private static void RenderFolder(DebugHubPanel panel, FolderNode node, Action<DebugNode, string[]> run,
            string label)
        {
            if (!node.Inline)
            {
                // Không gọi Children ở đây: đếm con của một folder động = gọi side effect của game
                // ở thời điểm không ai yêu cầu.
                panel.AddNavigation(label, FolderPage(node, run), node.Description);
                return;
            }

            panel.AddText($"<b>{label}</b>");
            foreach (var child in node.Children()) Render(panel, child, run);
        }

        private static void RenderText(DebugHubPanel panel, TextNode node)
        {
            var label = panel.AddText(Palette.Wrap(node.Text, node.Style));
            if (node.Style == TextStyle.Table) label.enableWordWrapping = false;
        }

        internal static DebugPage FolderPage(FolderNode node, Action<DebugNode, string[]> run)
        {
            return new DebugPage(node.Label, panel =>
            {
                foreach (var child in node.Children()) Render(panel, child, run);
            }, live: node.Live);
        }

        /// Tạm: Task 18 thay bằng Reflect.Members trên chính node.Address.
        private static DebugPage MembersPage(ValueNode node)
        {
            return new DebugPage(node.Label, panel => panel.AddText("Mở object cần Advanced — Task 18."));
        }

        /// Chữ phụ căn phải: đủ để biết bên trong có gì mà không phải mở ra.
        private static string Summary(object value)
        {
            if (value is ICollection collection) return $"{value.GetType().Name} ({collection.Count})";
            if (value is Object unityObject) return unityObject.name;
            return value.GetType().Name;
        }

        /// Object đã Destroy không `== null` theo nghĩa C#, mà mở vào nó thì mọi getter ném.
        internal static bool IsNull(object value) => value is Object unityObject ? !unityObject : value == null;
    }
}
