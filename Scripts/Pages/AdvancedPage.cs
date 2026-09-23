using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Mặt thứ hai của hub: **đồ nghề của chính hub**, không phải một category command.
    /// Ranh giới: cái gì đăng ký được thành node thì ở Commands; cái gì để soi/chỉnh bản thân
    /// runtime thì ở đây.
    public static class AdvancedPage
    {
        private static string typed = string.Empty;

        /// Static giữ nguyên giữa các lần Play khi bật "Enter Play Mode without domain reload":
        /// không reset thì trang Execute mở phiên chạy sau vẫn còn address đã gõ ở phiên trước.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => typed = string.Empty;

        public static DebugPage Root()
        {
            return new DebugPage("Advanced", panel =>
            {
                panel.AddNavigation("Objects", ObjectsPage.Root(), "Object và giá trị đã ghim.",
                    (Watches.All.Count + Vars.All.Count).ToString());
                panel.AddNavigation("Duyệt", InstancesPage(), "Tìm type/instance rồi mở ra.");
            });
        }

        private static DebugPage StaticPage(System.Type type)
        {
            return new DebugPage(type.Name, panel =>
            {
                NodeRenderer.RenderMembers(panel, new Cursor(type, null, null), type.FullName);
            });
        }

        public static DebugPage InstancesPage()
        {
            return new DebugPage("Instances",
                panel => panel.AddText("Bấm Tìm rồi gõ tên type (Component hoặc ScriptableObject)."),
                search: (panel, query) =>
                {
                    var type = TypeFinder.Find(query);
                    if (type == null || !typeof(Object).IsAssignableFrom(type))
                    {
                        panel.AddText("Chưa khớp type UnityEngine.Object nào.");
                        return;
                    }

                    var found = Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (found.Length == 0) { panel.AddText("Không có instance nào đang sống."); return; }

                    for (var i = 0; i < found.Length; i++)
                    {
                        var address = $"#{type.FullName}[{i}]";
                        var instance = found[i];
                        panel.AddNavigation(instance.name, ObjectPage(address, instance.name), address);
                    }
                });
        }

        private static DebugPage ObjectPage(string address, string title)
        {
            return new DebugPage(title, panel =>
            {
                if (!Address.TryResolve(address, out var cursor, out var error))
                {
                    panel.AddText($"<color={Palette.BAD}>{error}</color>");
                    return;
                }
                NodeRenderer.RenderMembers(panel, cursor, address);
                panel.AddButton(Watches.Contains(address) ? "Đã watch" : "+ Watch trang này", () =>
                {
                    if (!Watches.TryAdd(address, out var message)) panel.ShowResult(message, true);
                    panel.Refresh();
                });
            });
        }

        /// Đường thoát cho thứ duyệt bằng tay không tới được: method generic, và biểu thức cần
        /// ngoặc lồng (chia bước qua $var).
        public static DebugPage ExecutePage()
        {
            return new DebugPage("Execute", panel =>
            {
                // onSubmit trùng onChanged: AddField chỉ nối listener onEndEdit khi có onSubmit —
                // thiếu nó thì dán/gõ xong rồi bấm Get ngay không qua onValueChanged vẫn đọc `typed` rỗng.
                panel.AddField("Address", typeof(string), typed, value => typed = value, value => typed = value);
                var pending = string.Empty;
                // onSubmit trùng onChanged, cùng lý do với Address ở trên: thiếu nó thì dán/gõ xong
                // bấm Set ngay không qua onEndEdit vẫn đọc `pending` rỗng.
                panel.AddField("Giá trị (cho Set)", typeof(string), string.Empty, value => pending = value,
                    value => pending = value);

                panel.AddPrimary("Get", () =>
                {
                    if (!Address.TryResolve(typed, out var cursor, out var error)) panel.ShowResult(error, true);
                    else panel.ShowResult($"{typed} = {DebugValues.ToText(cursor.Value)}", false);
                });
                panel.AddButton("Set", () =>
                {
                    if (!Address.TrySet(typed, pending, out var error)) panel.ShowResult(error, true);
                    else panel.ShowResult($"{typed} = {pending}", false);
                });
                panel.AddButton("Watch address này", () =>
                {
                    // TryAdd **trước** TryResolve: resolve một address có `Factory.Spawn()` là gọi
                    // nó ngay tại đây, rồi mới báo "không watch được" — đúng cái side effect mà
                    // luật từ chối method sinh ra để tránh.
                    if (!Watches.TryAdd(typed, out var error)) { panel.ShowResult(error, true); return; }
                    if (!Address.TryResolve(typed, out _, out error))
                    {
                        Watches.Remove(typed);
                        panel.ShowResult(error, true);
                        return;
                    }
                    panel.ShowResult($"watch {typed}", false);
                });
                panel.AddButton("Lưu vào $…", () =>
                {
                    if (!Address.TryResolve(typed, out var cursor, out var error)) { panel.ShowResult(error, true); return; }
                    Vars.Bind("ans", cursor.Value);
                    panel.ShowResult($"$ans = {DebugValues.ToText(cursor.Value)}", false);
                });
            }, searchable: false);
        }

        private static string Leaf(string address)
        {
            var cut = address.LastIndexOfAny(new[] { '.', '[' });
            return cut < 0 ? address : address.Substring(cut + 1).TrimEnd(']');
        }
    }
}
