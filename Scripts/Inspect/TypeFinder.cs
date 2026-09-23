using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Tên → Type. **Không có index toàn cục**: dựng nó trên project này mất 4142 ms và 135.358
    /// key, mà bộ chọn ở BrowsePage đã thu hẹp phạm vi theo từng bước nên không ai cần tra cả
    /// domain nữa.
    ///
    /// `Find` với full name là một tra cứu hash bên trong từng assembly (`Assembly.GetType`), không
    /// phải quét — 496 lần tra cứu vẫn là micro-giây. Chỉ tên ngắn (không có dấu '.') mới phải quét,
    /// và kết quả được cache theo tên.
    public static class TypeFinder
    {
        private static readonly Dictionary<string, Type> resolved = new(StringComparer.Ordinal);

        /// Khoá bằng lock: Suggester gọi Search (→ TypesOf) từ thread nền trong lúc main thread gọi Find.
        private static readonly Dictionary<Assembly, Type[]> typesOf = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            resolved.Clear();
            lock (typesOf) typesOf.Clear();
        }

        public static Type Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (resolved.TryGetValue(name, out var cached)) return cached;

            var found = Resolve(name);
            resolved[name] = found;
            return found;
        }

        private static Type Resolve(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var direct = assembly.GetType(name, false);
                if (direct != null) return direct;
            }

            // Tên ngắn: không tra cứu được, phải quét. Chỉ xảy ra với address gõ tay — address do
            // BrowsePage sinh ra luôn mang full name.
            if (name.IndexOf('.') >= 0) return null;

            Type match = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in TypesOf(assembly))
                {
                    if (type.Name != name) continue;
                    if (match != null && match != type) return null;   // trùng tên ngắn ở hai namespace
                    match = type;
                }
            }
            return match;
        }

        /// Assembly khớp chuỗi con. 496 cái, lọc là micro-giây — đây là bước đầu của bộ chọn.
        public static IReadOnlyList<Assembly> Assemblies(string fragment)
        {
            var found = new List<Assembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!string.IsNullOrEmpty(fragment) &&
                    name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                found.Add(assembly);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.GetName().Name, b.GetName().Name));
            return found;
        }

        /// Type trong **một** assembly. Một assembly của game có vài trăm–vài nghìn type, lọc xong
        /// dưới một mili-giây — khác hẳn 32 ms của bản quét 135k key.
        public static IReadOnlyList<Type> Search(Assembly assembly, string fragment, int limit = 200)
        {
            var found = new List<Type>();
            foreach (var type in TypesOf(assembly))
            {
                if (found.Count >= limit) break;
                if (!string.IsNullOrEmpty(fragment) &&
                    (type.FullName ?? type.Name).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                found.Add(type);
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

        /// Cache theo assembly: một assembly hỏng không được làm chết cả bộ chọn.
        internal static Type[] TypesOf(Assembly assembly)
        {
            lock (typesOf)
            {
                if (typesOf.TryGetValue(assembly, out var cached)) return cached;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception) { types = Array.FindAll(exception.Types, t => t != null); }
                catch (Exception) { types = Array.Empty<Type>(); }

                typesOf[assembly] = types;
                return types;
            }
        }
    }
}
