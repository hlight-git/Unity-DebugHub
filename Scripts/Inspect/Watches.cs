using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Danh sách address đang theo dõi, lưu PlayerPrefs nên sống qua lần chạy sau.
    ///
    /// **Từ chối address có bước gọi method.** Trang Watch resolve lại 4 lần/giây, nên một watch
    /// vào `Factory.Spawn()` sẽ gọi nó 4 lần/giây. Luật này áp cả lúc thêm lẫn lúc đọc lại từ
    /// PlayerPrefs — bản lưu cũ không được sống lại thành một vòng lặp side effect.
    public static class Watches
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

        public static bool TryAdd(string address, out string error)
        {
            error = null;
            address = address?.Trim();
            if (string.IsNullOrEmpty(address))
            {
                error = "Address rỗng.";
                return false;
            }
            // Biến `$` sống trong RAM và chết theo domain reload, còn danh sách này lưu PlayerPrefs — ghim
            // một address gốc `$` là bảo đảm một dòng đỏ ở lần chạy sau. Nó cũng đã nằm sẵn trong trang
            // Objects qua Vars, nên ghim thêm chỉ tạo dòng trùng mà `Gỡ` không xoá hết.
            if (address.StartsWith("$"))
            {
                error = "Không ghim được address bắt đầu bằng biến $ — biến chỉ sống trong phiên này. " +
                        "Dùng một address cố định (ví dụ `@info.save`) nếu muốn giữ lại.";
                return false;
            }
            if (Address.HasMethodStep(address))
            {
                error = "Không watch được address có gọi method — trang Watch đọc lại 4 lần/giây.";
                return false;
            }
            if (Contains(address)) return true;

            var list = new List<string>(All) { address };
            Save(list);
            return true;
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
