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
                if (!string.IsNullOrEmpty(text))
                {
                    panel.AddButton("Copy giá trị", () =>
                    {
                        GUIUtility.systemCopyBuffer = text;
                        panel.ShowResult($"đã copy {node.Label}", false);
                        panel.Pop();
                    });
                }

                // Gán chỉ cho giá trị **không** có editor tại chỗ: số/bool/enum/vector đã sửa được
                // ngay trên row, thêm một page nữa là hai đường sửa cho cùng một thứ. Chỗ thật sự
                // cần là gán một reference (hoặc null) cho field kiểu object.
                if (node.Set != null && !DebugValues.IsInlineValue(node.Declared))
                    panel.AddNavigation("Gán giá trị", AssignPage(node, run));

                if (node.Address != null)
                {
                    var watched = Watches.Contains(node.Address);
                    panel.AddButton(watched ? "Đã watch" : "Watch", () =>
                    {
                        if (watched) { panel.Pop(); return; }
                        if (!Watches.TryAdd(node.Address, out var error)) panel.ShowResult(error, true);
                        else panel.ShowResult($"watch {node.Address}", false);
                        panel.Pop();
                    });
                }

                if (!NodeRenderer.IsNull(current)) panel.AddNavigation("Lưu vào $…", SaveVarPage(node, current));
            }, searchable: false);
        }

        /// Giữ đúng reference, kể cả object inactive hay component thứ hai cùng kiểu — thứ mà
        /// GameObject.Find/GetComponent không lấy lại được (§4).
        private static DebugPage SaveVarPage(ValueNode node, object current)
        {
            return new DebugPage($"Lưu {node.Label}", panel =>
            {
                var name = string.Empty;
                panel.AddField("Tên biến", typeof(string), string.Empty, value => name = value);
                panel.AddPrimary("Lưu", () =>
                {
                    if (string.IsNullOrWhiteSpace(name)) { panel.ShowResult("Tên biến rỗng.", true); return; }
                    Vars.Bind(name.TrimStart('$'), current);
                    panel.ShowResult($"${name.TrimStart('$')} = {DebugValues.ToText(current)}", false);
                    panel.Pop();
                });
            }, searchable: false);
        }

        private static DebugPage AssignPage(ValueNode node, Action<DebugNode, string[]> run)
        {
            return new DebugPage($"Gán {node.Label}", panel =>
            {
                var typed = string.Empty;
                panel.AddText($"Kiểu: {node.Declared.Name}. Gõ 'null' để xoá tham chiếu, `$tên` để gán biến.");
                panel.AddField("Giá trị", typeof(string), string.Empty, value => typed = value);
                panel.AddPrimary("Gán", () =>
                {
                    // Kiểm trước để báo lỗi ngay tại chỗ nhập…
                    if (!DebugValues.TryParse(typed, node.Declared, out _, out var error, allowVars: true))
                    {
                        panel.ShowResult(error, true);
                        return;
                    }

                    // …còn việc ghi thì đi qua đúng luồng chạy của node: node.Set thẳng sẽ bỏ qua
                    // Confirm, bỏ qua DismissMode và không bắt được log/exception mà setter in ra.
                    // Truyền **text gốc** chứ không serialize lại: `$g` mà ToText lại là mất reference.
                    panel.Pop();
                    run(node, new[] { typed });
                });
            }, searchable: false);
        }
    }
}
