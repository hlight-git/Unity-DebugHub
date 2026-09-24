using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Một danh sách duy nhất cho câu hỏi "bắt đầu soi từ object nào".
    ///
    /// Trước đây là ba trang — Watch (address đã lưu), Instances (tìm theo type), Vars (gán tay giữ
    /// reference) — nhưng `$tên` cũng là một address root, nên cả ba quy về *một địa chỉ + một nhãn*.
    /// Tách ba bắt người dùng chọn cửa trước khi biết cửa nào đi được. Instances không mất đi: nó
    /// thành bộ chọn của `Duyệt` (BrowsePage), và "Ghim" từ đó đẩy address vào đây.
    ///
    /// Khác biệt thật duy nhất giữa hai nguồn: biến `$` chết khi domain reload, nên nó được ghi rõ.
    internal static class ObjectsPage
    {
        public static DebugPage Root()
        {
            return new DebugPage("Objects", panel => Build(panel, string.Empty),
                search: Build);
        }

        private static void Build(DebugHubPanel panel, string query)
        {
            var scalars = new List<(string Address, ValueNode Node)>();
            var objects = new List<(string Address, ValueNode Node)>();
            var broken = new List<(string Address, string Error)>();

            foreach (var address in Watches.All)
                if (Matches(address, query)) Sort(address, false, scalars, objects, broken);
            foreach (var pair in Vars.All)
                if (Matches($"${pair.Key}", query)) Sort($"${pair.Key}", true, scalars, objects, broken);

            if (string.IsNullOrEmpty(query))
            {
                panel.AddNavigation("Tìm object", BrowsePage.Assemblies(),
                    "Chọn type hoặc instance, rồi bấm Ghim để đưa vào đây.");
                panel.AddNavigation("Nhập address…", ManualPage(),
                    "Dành cho address bạn đã biết; ví dụ #Namespace.Type[0].member");
            }

            if (scalars.Count == 0 && objects.Count == 0 && broken.Count == 0)
                panel.AddText(string.IsNullOrEmpty(query)
                    ? "Chưa có mục đã ghim."
                    : "Không có object hoặc giá trị nào khớp.");

            Section(panel, "Giá trị", scalars);
            Section(panel, "Object", objects);

            foreach (var item in broken)
            {
                panel.AddError(item.Address, item.Error);
                var address = item.Address;
                panel.AddButton("Gỡ", () => { Drop(address); panel.Refresh(); });
            }

            if (scalars.Count + objects.Count + broken.Count > 0 && string.IsNullOrEmpty(query))
                panel.AddButton("Làm mới giá trị", panel.Refresh);
        }

        private static bool Matches(string address, string query) =>
            address.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// Đường thoát cho thứ bộ chọn không tới được: method generic `Ten<$T>(x)`, hay biểu thức cần
        /// ngoặc lồng (chia bước qua $var). Chỉ **mở** — chính hai loại đó không ghim được; address ghim
        /// được thì trang mở ra có nút ghim ở cuối.
        private static DebugPage ManualPage()
        {
            var typed = string.Empty;
            return new DebugPage("Address", panel =>
            {
                panel.AddText("Nhập đường dẫn đầy đủ. Ví dụ:\n" +
                    $"<color={Palette.DIM}>#Game.Player[0].inventory</color>\n" +
                    $"<color={Palette.DIM}>Namespace.StaticType.Member</color>");
                // onSubmit trùng onChanged: AddField chỉ nối onEndEdit khi có onSubmit — thiếu nó thì
                // dán xong bấm Mở ngay không qua onValueChanged vẫn đọc `typed` rỗng.
                panel.AddField("Address", typeof(string), typed, v => typed = v, v => typed = v);
                panel.AddPrimary("Mở", () =>
                {
                    if (!Address.TryResolve(typed, out _, out var error)) { panel.ShowResult(error, true); return; }
                    panel.Push(BrowsePage.At(typed, Leaf(typed)));
                });
            }, searchable: false);
        }

        private static void Sort(string address, bool sessionOnly, List<(string, ValueNode)> scalars,
            List<(string, ValueNode)> objects, List<(string, string)> broken)
        {
            if (!Address.TryResolve(address, out var cursor, out var error))
            {
                broken.Add((address, error));
                return;
            }

            // Lần đọc **đầu tiên** (renderer dựng row ngay sau đây) dùng lại giá trị vừa resolve, nên
            // mỗi lần dựng danh sách chỉ resolve một lần. Lần đọc sau (trang member mở từ row này)
            // resolve lại, không bám bản cũ.
            //
            // Lỗi phải nổi lên (cùng luật với Reflect.ValueFor): Get nuốt lỗi thì một resolve thất bại
            // hiện thành "null" — không phân biệt được với giá trị thật sự null; Set nuốt lỗi thì người
            // dùng tưởng đã ghi xong.
            var snapshot = cursor.Value;
            var fresh = true;
            var node = new ValueNode
            {
                Label = Leaf(address),
                Description = sessionOnly ? $"{address} — chỉ trong phiên này" : address,
                Declared = cursor.Declared,
                Address = address,
                Get = () =>
                {
                    if (fresh) { fresh = false; return snapshot; }
                    if (!Address.TryResolve(address, out var again, out var message)) throw new System.Exception(message);
                    return again.Value;
                },
                Set = cursor.CanWrite
                    ? value =>
                    {
                        if (!Address.TryWrite(address, value, out var message)) throw new System.Exception(message);
                    }
                    : null,
                Dismiss = DismissMode.Stay,
            };

            (DebugValues.IsInlineValue(cursor.Declared) ? scalars : objects).Add((address, node));
        }

        private static void Section(DebugHubPanel panel, string title, List<(string Address, ValueNode Node)> items)
        {
            if (items.Count == 0) return;
            panel.AddText($"<b>{title}</b>");
            // Một ghim một row: `Gỡ` nằm trong nút `…` của chính dòng đó (Bỏ ghim / Bỏ biến).
            foreach (var item in items)
                NodeRenderer.Render(panel, item.Node, (node, values) => NodeRenderer.RunInspect(panel, node, values));
        }

        /// Một nút Gỡ cho hai kho: `$tên` về Vars, còn lại về Watches.
        private static void Drop(string address)
        {
            if (address.StartsWith("$")) Vars.Remove(address.Substring(1));
            else Watches.Remove(address);
        }

        private static string Leaf(string address)
        {
            var cut = address.LastIndexOfAny(new[] { '.', '[' });
            return cut < 0 ? address : address.Substring(cut + 1).TrimEnd(']');
        }
    }
}
