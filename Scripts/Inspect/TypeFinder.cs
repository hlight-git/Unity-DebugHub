using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Tên → Type, quét mọi assembly đã nạp một lần rồi cache.
    ///
    /// Tồn tại để bỏ hẳn tham số `assembly` của inspect cũ: ô đầu tiên của `inspect.get` gần như
    /// luôn là "Assembly-CSharp" — một ô bắt gõ mà không mang thông tin.
    public static class TypeFinder
    {
        private static Dictionary<string, List<Type>> index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => index = null;

        public static Type Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Build();
            if (!index.TryGetValue(name, out var matches)) return null;
            if (matches.Count == 1) return matches[0];

            // Short name trùng ở nhiều namespace: chỉ chọn được khi người dùng gõ full name.
            foreach (var type in matches)
            {
                if (type.FullName == name) return type;
            }
            return null;
        }

        public static IReadOnlyList<Type> Search(string fragment, int limit = 50)
        {
            var found = new List<Type>();
            if (string.IsNullOrEmpty(fragment)) return found;
            Build();

            foreach (var pair in index)
            {
                if (found.Count >= limit) break;
                if (pair.Key.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var type in pair.Value)
                {
                    if (!found.Contains(type)) found.Add(type);
                }
            }
            found.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return found;
        }

        /// Ranh giới root/step của address (§9.1): lấy **prefix dài nhất** phân giải được thành một
        /// type, phần còn lại là step. `Game.Board.Slots` với type `Game.Board` → root `Game.Board`,
        /// rest `Slots`. Tra cứu xác định nên không còn trường hợp mơ hồ.
        public static Type LongestPrefix(string path, out string rest)
        {
            rest = string.Empty;
            if (string.IsNullOrEmpty(path)) return null;

            var cut = path.Length;
            while (cut > 0)
            {
                var candidate = path.Substring(0, cut);
                var type = Find(candidate);
                if (type != null)
                {
                    rest = cut < path.Length ? path.Substring(cut + 1) : string.Empty;
                    return type;
                }
                cut = candidate.LastIndexOf('.');
            }
            return null;
        }

        private static void Build()
        {
            if (index != null) return;
            index = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    // Một assembly nạp hỏng không được làm chết cả bảng — lấy phần nạp được.
                    types = Array.FindAll(exception.Types, type => type != null);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    Add(type.Name, type);
                    if (type.FullName != null && type.FullName != type.Name) Add(type.FullName, type);
                }
            }
        }

        private static void Add(string key, Type type)
        {
            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<Type>();
            list.Add(type);
        }
    }
}
