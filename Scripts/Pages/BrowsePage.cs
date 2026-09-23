using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Bộ chọn nhiều bước: Assembly → Type → Instance → member.
    ///
    /// Mỗi bước là một trang có `Search` riêng, lọc bằng chính ô Tìm ở header — không đẻ widget mới,
    /// và ô nhập nằm ngoài vùng rebuild nên gõ không tự giết chính nó.
    ///
    /// Thu hẹp theo từng bước là thứ làm nó nhanh: 496 assembly lọc trong micro-giây, một assembly
    /// có vài trăm–vài nghìn type. Bản cũ tìm type trên cả domain mất 32 ms mỗi ký tự vì phải quét
    /// index 135k key.
    ///
    /// Bước sau bước Instance **không** thuộc trang này: trang member hiện có đã đệ quy không giới
    /// hạn, và cả hai đều chỉ nối thêm bước vào cùng một chuỗi address.
    public static class BrowsePage
    {
        /// Chưa gõ gì cũng có đường mò: `Mọi assembly` + các assembly không thuộc Unity/.NET. Lọc tên
        /// assembly là micro-giây nên làm ngay ở main thread, không qua Suggester.
        public static DebugPage Assemblies()
        {
            return new DebugPage("Assembly", panel =>
            {
                panel.AddNavigation("Mọi assembly", AnyAssembly(), "Tìm type mà không cần biết assembly.");

                // Code game nằm ở `Assembly-CSharp`. Xếp thuần theo chữ cái thì nó rơi xuống hàng
                // thứ bảy, sau AdjustSdk/AllIn1SpriteShader/AndroidPlayerBuildProgram — thứ hay cần
                // nhất lại khó thấy nhất.
                var hidden = 0;
                var rest = new List<Assembly>();
                foreach (var assembly in TypeFinder.Assemblies(string.Empty))
                {
                    var name = assembly.GetName().Name;
                    if (IsPlatform(name)) { hidden++; continue; }
                    if (name.StartsWith("Assembly-CSharp", StringComparison.Ordinal))
                        panel.AddNavigation(name, Types(assembly));
                    else rest.Add(assembly);
                }
                foreach (var assembly in rest) panel.AddNavigation(assembly.GetName().Name, Types(assembly));
                panel.AddText(Palette.Wrap($"Ẩn {hidden} assembly của Unity/.NET — bấm Tìm rồi gõ để thấy.", TextStyle.Note));
            },
            search: (panel, query) =>
            {
                var found = TypeFinder.Assemblies(query);
                if (found.Count == 0) { panel.AddText("Không có assembly nào khớp."); return; }
                foreach (var assembly in found) panel.AddNavigation(assembly.GetName().Name, Types(assembly));
            });
        }

        /// Chậm hơn tìm trong một assembly, nhưng chạy trên worker nên không ai thấy — và nó cứu đúng
        /// trường hợp hay gặp: biết tên type, không biết assembly.
        private static DebugPage AnyAssembly()
        {
            var suggester = new Suggester<Type>(fragment => TypeFinder.SearchAll(fragment));
            return new DebugPage("Mọi assembly",
                panel => panel.AddText("Bấm Tìm rồi gõ tên type."),
                search: (panel, query) => Suggest(panel, suggester, query, "Không có type nào khớp.", type => TypeRow(panel, type)));
        }

        public static DebugPage Types(Assembly assembly)
        {
            var suggester = new Suggester<Type>(fragment => TypeFinder.Search(assembly, fragment));
            return new DebugPage(assembly.GetName().Name,
                panel => panel.AddText($"{TypeFinder.TypesOf(assembly).Length} type. Bấm Tìm rồi gõ tên."),
                search: (panel, query) => Suggest(panel, suggester, query, "Không có type nào khớp.", type => TypeRow(panel, type)));
        }

        /// Tên ngắn trên nhãn, full name xuống dòng mô tả: full name trên nhãn thì mọi dòng bắt đầu bằng
        /// cùng một namespace và bị cắt mất đúng phần khác nhau.
        private static void TypeRow(DebugHubPanel panel, Type type)
        {
            panel.AddNavigation(type.Name, Instances(type), type.FullName);
        }

        /// Assembly của engine/runtime — ẩn khỏi danh sách mặc định (vẫn tìm được bằng ô Tìm). Chỉ là
        /// cách sắp xếp chỗ nhìn, không lọc gì khỏi kết quả tìm.
        private static bool IsPlatform(string name)
        {
            foreach (var prefix in PlatformPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return name == "mscorlib" || name == "netstandard";
        }

        private static readonly string[] PlatformPrefixes =
        {
            "System", "Unity", "Mono.", "Microsoft.", "nunit", "Bee.", "JetBrains", "Newtonsoft",
        };

        /// Type nào cũng mở được member static; là UnityEngine.Object thì thêm danh sách instance
        /// đang sống. Không phải Object **không** có nghĩa là không soi được — service C# thuần đi
        /// qua static, hoặc qua một object đã ghim ở Objects.
        public static DebugPage Instances(Type type)
        {
            return new DebugPage(type.Name, panel =>
            {
                panel.AddNavigation("Member static", At(type.FullName, type.Name), type.FullName);

                // Generic chưa đóng (Singleton<T>) không có instance nào để tìm — FindObjectsByType ném.
                if (!typeof(Object).IsAssignableFrom(type) || type.ContainsGenericParameters) return;

                var found = Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found.Length == 0) { panel.AddText("Không có instance nào đang sống."); return; }

                for (var i = 0; i < found.Length; i++)
                {
                    var address = $"#{type.FullName}[{i}]";
                    panel.AddNavigation(found[i].name, At(address, found[i].name), address);
                }
            });
        }

        /// Đứng tại một address: danh sách member (đệ quy bằng cách bấm), dòng address để copy, và
        /// nút ghim vào Objects.
        ///
        /// **Cố ý không Live.** Dựng lại một trang member tốn ~1 ms mỗi row — `RootScope` 55 row là
        /// 58 ms, tức Live 4 lần/giây ngốn 1/4 thời gian chạy của game chỉ để vẽ lại một danh sách.
        /// Chỗ để ngồi nhìn giá trị đổi là trang `Objects`: nó chỉ có những dòng đã ghim nên nhỏ, và
        /// nó Live. Trang này là chỗ **duyệt**, mở lại là có số mới.
        public static DebugPage At(string address, string title)
        {
            return new DebugPage(title, panel =>
            {
                if (!Address.TryResolve(address, out var cursor, out var error))
                {
                    panel.AddText($"<color={Palette.BAD}>{error}</color>");
                    return;
                }

                NodeRenderer.RenderMembers(panel, cursor, address);

                panel.AddButton(Watches.Contains(address) ? "Đã ghim" : "+ Ghim vào Objects", () =>
                {
                    if (!Watches.TryAdd(address, out var message)) panel.ShowResult(message, true);
                    panel.Refresh();
                });
            },
            search: (panel, query) =>
            {
                if (!Address.TryResolve(address, out var cursor, out var error))
                    panel.AddText($"<color={Palette.BAD}>{error}</color>");
                else
                    NodeRenderer.FilterMembers(panel, cursor, address, query);
            },
            subtitle: address);
        }

        /// Kết quả cũ vẫn hiện trong lúc query mới đang chờ — gõ thêm một chữ không làm list chớp trắng —
        /// nhưng có một dòng "Đang tìm…" ở đầu để biết list đó chưa phải của chữ vừa gõ.
        private static void Suggest<T>(DebugHubPanel panel, Suggester<T> suggester, string query, string none,
            Action<T> row)
        {
            suggester.Request(query);
            var snapshot = suggester.Current;    // đọc một lần: query và kết quả phải cùng một bản
            var pending = snapshot.Query != query;

            // Còn chờ thì xin thêm một nhịp dựng lại — trang không Live, kết quả về sẽ không tự hiện.
            if (pending)
            {
                panel.RefreshLater();
                panel.AddText(Palette.Wrap("Đang tìm…", TextStyle.Note));
            }
            if (snapshot.Items.Count == 0)
            {
                if (!pending) panel.AddText(none);
                return;
            }
            foreach (var item in snapshot.Items) row(item);
        }
    }
}
