using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Biến của debugger: đặt tên cho một **ảnh chụp** giá trị hoặc type rồi dùng lại bằng `$tên`.
    ///
    /// Khác hẳn Watch (địa chỉ sống, đọc lại mỗi lần) — lằn ranh này cố ý giữ. Vars tồn tại vì hai
    /// lý do text không làm được: truyền đúng reference (tên Unity object không phải định danh —
    /// `GameObject.Find` không thấy object inactive, `GetComponent` chỉ trả cái đầu tiên), và
    /// truyền tham số generic.
    ///
    /// Chỉ nằm trong RAM vì nó giữ object sống.
    internal static class Vars
    {
        private static readonly Dictionary<string, object> bound = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => bound.Clear();

        public static bool TryGet(string name, out object value) => bound.TryGetValue(name, out value);

        public static void Bind(string name, object value) => bound[name] = value;

        public static void Remove(string name) => bound.Remove(name);

        public static IReadOnlyList<KeyValuePair<string, object>> All
        {
            get
            {
                var list = new List<KeyValuePair<string, object>>(bound);
                list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                return list;
            }
        }
    }
}
