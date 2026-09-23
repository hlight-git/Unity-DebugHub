using System;
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
        public static DebugPage Assemblies()
        {
            // Mỗi trang một Suggester: dùng chung thì kết quả của trang trước lọt sang trang sau.
            var suggester = new Suggester<Assembly>(TypeFinder.Assemblies);
            return new DebugPage("Assembly",
                panel => panel.AddText("Bấm Tìm rồi gõ tên assembly (ví dụ: Assembly-CSharp, Harvest, Hlight)."),
                search: (panel, query) => Suggest(panel, suggester, query, "Không có assembly nào khớp.",
                    assembly => panel.AddNavigation(assembly.GetName().Name, Types(assembly))),
                live: true);
        }

        public static DebugPage Types(Assembly assembly)
        {
            var suggester = new Suggester<Type>(fragment => TypeFinder.Search(assembly, fragment));
            return new DebugPage(assembly.GetName().Name,
                panel => panel.AddText($"{TypeFinder.TypesOf(assembly).Length} type. Bấm Tìm rồi gõ tên."),
                search: (panel, query) => Suggest(panel, suggester, query, "Không có type nào khớp.",
                    type => panel.AddNavigation(type.FullName, Instances(type))),
                live: true);
        }

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
        public static DebugPage At(string address, string title)
        {
            return new DebugPage(title, panel =>
            {
                if (!Address.TryResolve(address, out var cursor, out var error))
                {
                    panel.AddText($"<color={Palette.BAD}>{error}</color>");
                    return;
                }

                // Address ở description chứ không ở cột phải như AddCopyRow: nó dài, cột phải cắt mất.
                panel.AddButton("Copy address", () =>
                {
                    GUIUtility.systemCopyBuffer = address;
                    panel.ShowResult("đã copy address", false);
                }, address);
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
            });
        }

        /// Kết quả cũ vẫn hiện trong lúc query mới đang chờ — gõ thêm một chữ không làm list chớp trắng.
        private static void Suggest<T>(DebugHubPanel panel, Suggester<T> suggester, string query, string none,
            Action<T> row)
        {
            suggester.Request(query);
            if (suggester.Results.Count == 0)
            {
                panel.AddText(suggester.ResultsFor != query ? "Đang tìm…" : none);
                return;
            }
            foreach (var item in suggester.Results) row(item);
        }
    }
}
