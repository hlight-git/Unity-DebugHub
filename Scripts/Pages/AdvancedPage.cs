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

        public static DebugPage Root()
        {
            return new DebugPage("Advanced", panel =>
            {
                panel.AddNavigation("Watch", WatchPage(), "Giá trị đang theo dõi.", Watches.All.Count.ToString());
                panel.AddNavigation("Types", TypesPage(), "Gõ tên type để mở member static.");
                panel.AddNavigation("Instances", InstancesPage(), "Object đang sống theo type.");
                panel.AddNavigation("Vars", VarsPage(), "Biến $ đã lưu.", Vars.All.Count.ToString());
                panel.AddNavigation("Execute", ExecutePage(), "Đọc/ghi theo address.");
            });
        }

        /// Live: đọc lại 4 lần/giây (panel tự bỏ nhịp khi đang gõ). Chia hai section theo **cùng
        /// phân loại của renderer**, không có bảng quyết định thứ hai.
        public static DebugPage WatchPage()
        {
            return new DebugPage("Watch", panel =>
            {
                var addresses = Watches.All;
                if (addresses.Count == 0) { panel.AddText("Chưa watch gì. Thêm từ nút … trên một dòng giá trị."); }

                var scalars = new List<(string Address, ValueNode Node)>();
                var objects = new List<(string Address, ValueNode Node)>();

                foreach (var address in addresses)
                {
                    if (!Address.TryResolve(address, out var cursor, out var error))
                    {
                        panel.AddText($"<color={Palette.BAD}>{address}: {error}</color>");
                        var broken = address;
                        panel.AddButton("Gỡ", () => { Watches.Remove(broken); panel.Refresh(); });
                        continue;
                    }

                    var node = new ValueNode
                    {
                        Label = Leaf(address),
                        Description = address,
                        Declared = cursor.Declared,
                        Address = address,
                        Get = () => Address.TryResolve(address, out var fresh, out _) ? fresh.Value : null,
                        Set = cursor.CanWrite ? value => Address.TryWrite(address, value, out _) : null,
                        Dismiss = DismissMode.Stay,
                    };
                    (DebugValues.IsInlineValue(cursor.Declared) ? scalars : objects).Add((address, node));
                }

                Section(panel, "Giá trị", scalars);
                Section(panel, "Object", objects);
            }, live: true);
        }

        private static void Section(DebugHubPanel panel, string title, List<(string Address, ValueNode Node)> items)
        {
            if (items.Count == 0) return;
            panel.AddText($"<b>{title}</b>");
            foreach (var item in items)
            {
                NodeRenderer.Render(panel, item.Node, (node, values) => DebugRegistry.Run(node, values, out _));
                var address = item.Address;
                panel.AddButton("Gỡ", () => { Watches.Remove(address); panel.Refresh(); });
            }
        }

        /// Trống cho tới khi gõ ≥ 2 ký tự: index type có hàng chục nghìn dòng, liệt kê hết là vô dụng.
        public static DebugPage TypesPage()
        {
            return new DebugPage("Types",
                panel => panel.AddText("Bấm Tìm rồi gõ ≥ 2 ký tự tên type."),
                search: (panel, query) =>
                {
                    if (query.Length < 2) { panel.AddText("Gõ ít nhất 2 ký tự."); return; }
                    foreach (var type in TypeFinder.Search(query))
                    {
                        var target = type;
                        panel.AddNavigation(target.FullName, StaticPage(target));
                    }
                });
        }

        private static DebugPage StaticPage(System.Type type)
        {
            return new DebugPage(type.Name, panel =>
            {
                var cursor = new Cursor(type, null, null);
                foreach (var node in Reflect.Members(cursor, type.FullName, MemberFilter.Default))
                    NodeRenderer.Render(panel, node, (n, values) => DebugRegistry.Run(n, values, out _));
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
                foreach (var node in Reflect.Members(cursor, address, MemberFilter.Default))
                    NodeRenderer.Render(panel, node, (n, values) => DebugRegistry.Run(n, values, out _));
                panel.AddButton(Watches.Contains(address) ? "Đã watch" : "+ Watch trang này", () =>
                {
                    if (!Watches.TryAdd(address, out var message)) panel.ShowResult(message, true);
                    panel.Refresh();
                });
            });
        }

        public static DebugPage VarsPage()
        {
            return new DebugPage("Vars", panel =>
            {
                var all = Vars.All;
                if (all.Count == 0) { panel.AddText("Chưa có biến nào. Lưu từ nút … trên một dòng giá trị."); return; }

                foreach (var pair in all)
                {
                    var name = pair.Key;
                    var value = pair.Value;
                    panel.AddCopyRow($"${name}", DebugValues.ToText(value), value?.GetType().Name);
                    panel.AddButton("Gỡ", () => { Vars.Remove(name); panel.Refresh(); });
                }
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
                panel.AddField("Giá trị (cho Set)", typeof(string), string.Empty, value => pending = value);

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
