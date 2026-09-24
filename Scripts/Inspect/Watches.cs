using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Danh sách address đã ghim, lưu PlayerPrefs nên sống qua lần chạy sau.
    ///
    /// **Từ chối address có bước gọi method.** Trang Objects đọc lại mọi mục đã ghim mỗi lần mở và
    /// mỗi lần làm mới, nên một ghim vào `Factory.Spawn()` sẽ gọi nó mỗi lần. Luật áp cả lúc thêm lẫn
    /// lúc đọc lại từ PlayerPrefs — bản lưu cũ bị bỏ qua, không sống lại thành side effect.
    internal static class Watches
    {
        private const string KEY = "DebugHub.Watches";

        public static IReadOnlyList<string> All
        {
            get
            {
                var list = new List<string>();
                foreach (var line in PlayerPrefs.GetString(KEY, string.Empty).Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("$") || Address.HasMethodStep(line)) continue;
                    if (!list.Contains(line)) list.Add(line);
                }
                return list;
            }
        }

        public static bool Contains(string address) => All.Contains(address?.Trim());

        public static bool CanPin(string address) => Rejection(address?.Trim()) == null;

        public static bool TryAdd(string address, out string error)
        {
            address = address?.Trim();
            error = Rejection(address);
            if (error != null) return false;
            if (Contains(address)) return true;

            Save(new List<string>(All) { address });
            return true;
        }

        /// null = ghim được.
        private static string Rejection(string address)
        {
            if (string.IsNullOrEmpty(address)) return "Address rỗng.";
            // Biến `$` sống trong RAM và chết theo domain reload, còn danh sách này lưu PlayerPrefs — ghim
            // một address gốc `$` là bảo đảm một dòng đỏ ở lần chạy sau. Nó cũng đã nằm sẵn trong trang
            // Objects qua Vars.
            if (address.StartsWith("$"))
                return "Không ghim được address bắt đầu bằng biến $ — biến chỉ sống trong phiên này. " +
                       "Dùng một address cố định (ví dụ `@info.save`) nếu muốn giữ lại.";
            if (Address.HasMethodStep(address))
                return "Không ghim được address có gọi method — Objects đọc lại mọi mục đã ghim mỗi lần làm mới.";
            return null;
        }

        public static void Remove(string address)
        {
            var list = new List<string>(All);
            list.Remove(address?.Trim());
            Save(list);
        }

        private static void Save(List<string> list) => PlayerPrefs.SetString(KEY, string.Join("\n", list));
    }
}
