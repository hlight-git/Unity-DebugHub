using System;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Page mở từ nút `…` của một row giá trị. Một nút một page thay vì rải hai–ba nút nhỏ lên
    /// mỗi row: panel bề ngang điện thoại đã có nhãn + giá trị + switch/field, thêm vùng bấm nữa
    /// là bấm nhầm. Task 8 thêm `Gán`, Task 19 thêm `Watch` và `Lưu vào $…`.
    public static class ActionsPage
    {
        public static DebugPage For(ValueNode node, object current, Action<DebugNode, string[]> run)
        {
            return new DebugPage(node.Label, panel =>
            {
                var text = DebugValues.ToText(current);
                if (string.IsNullOrEmpty(text)) return;

                panel.AddButton("Copy giá trị", () =>
                {
                    GUIUtility.systemCopyBuffer = text;
                    panel.ShowResult($"đã copy {node.Label}", false);
                    panel.Pop();
                });
            }, searchable: false);
        }
    }
}
