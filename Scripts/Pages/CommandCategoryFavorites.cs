using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Root command categories in the order the user starred them.
    internal static class CommandCategoryFavorites
    {
        private const string Key = "DebugHub.CommandCategoryFavorites";

        public static IReadOnlyList<string> All
        {
            get
            {
                var result = new List<string>();
                foreach (var category in PlayerPrefs.GetString(Key, string.Empty).Split('\n'))
                    if (!string.IsNullOrWhiteSpace(category) && !result.Contains(category)) result.Add(category);
                return result;
            }
        }

        /// Không phân biệt hoa thường, như cây Commands gộp thư mục: tên thư mục lấy theo node đăng ký trước, có thể khác
        /// chữ hoa với bản đã lưu — so phân biệt là bỏ sao không được.
        public static bool Contains(string category) =>
            All.Any(saved => string.Equals(saved, category, System.StringComparison.OrdinalIgnoreCase));

        public static void Toggle(string category)
        {
            var items = new List<string>(All);
            if (items.RemoveAll(saved => string.Equals(saved, category, System.StringComparison.OrdinalIgnoreCase)) == 0)
                items.Add(category);
            PlayerPrefs.SetString(Key, string.Join("\n", items));
            PlayerPrefs.Save();
        }
    }
}
