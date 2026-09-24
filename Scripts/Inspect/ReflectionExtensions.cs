using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hlight.Debug.Hub
{
    internal static class ReflectionExtensions
    {
        private const BindingFlags ALL = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// Chỉ bóc TargetInvocationException — cái vỏ mà Invoke bọc quanh lỗi thật. Bóc mọi
        /// InnerException là đánh mất chính lỗi được ném mỗi khi nó có cause.
        public static Exception Unwrap(this Exception exception) =>
            exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

        /// Method/indexer virtual bị override thì base type và derived type đều "declare" một
        /// MethodInfo riêng cho cùng một slot — GetBaseDefinition() trỏ về cùng khai báo gốc, dùng
        /// để lọc bản ở base type ra, tránh báo "nhiều overload" giả cho một method chỉ bị override.
        static bool IsOverriddenBy(MethodInfo candidate, IEnumerable<MethodInfo> alreadyCollected)
        {
            return candidate.IsVirtual && alreadyCollected.Any(m => m.IsVirtual && m.GetBaseDefinition().Equals(candidate.GetBaseDefinition()));
        }

        public static void AddIndexersRecursive(this Type type, List<PropertyInfo> indexers, Func<PropertyInfo, bool> predicate, BindingFlags bindingFlags = ALL)
        {
            var collectedAccessors = indexers.Select(i => i.GetMethod ?? i.SetMethod).Where(a => a != null).ToList();
            foreach (var indexer in type.GetProperties(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate))
            {
                var accessor = indexer.GetMethod ?? indexer.SetMethod;
                if (accessor != null && IsOverriddenBy(accessor, collectedAccessors)) continue;
                indexers.Add(indexer);
            }
            if (type.BaseType != null)
                AddIndexersRecursive(type.BaseType, indexers, predicate, bindingFlags);

            foreach (var parent in InheritedInterfaces(type))
                indexers.AddRange(parent.GetProperties(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate));
        }

        public static void AddMethodsRecursive(this Type type, List<MethodInfo> methodInfos, Func<MethodInfo, bool> predicate, BindingFlags bindingFlags = ALL)
        {
            methodInfos.AddRange(type.GetMethods(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate).Where(m => !IsOverriddenBy(m, methodInfos)));
            if (type.BaseType != null)
                AddMethodsRecursive(type.BaseType, methodInfos, predicate, bindingFlags);

            foreach (var parent in InheritedInterfaces(type))
                methodInfos.AddRange(parent.GetMethods(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate));
        }

        public static FieldInfo GetFieldRecursive(this Type type, string name, BindingFlags bindingFlags = ALL)
        {
            var fieldInfo = type.GetField(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (fieldInfo != null) return fieldInfo;
            if (type.BaseType != null) return type.BaseType.GetFieldRecursive(name, bindingFlags);

            foreach (var parent in InheritedInterfaces(type))
            {
                var inherited = parent.GetField(name, bindingFlags | BindingFlags.DeclaredOnly);
                if (inherited != null) return inherited;
            }
            return null;
        }

        /// GetProperty() không trả về property non-public của class cha, nên phải tự đi lên. Chỉ property
        /// không có tham số: indexer đi đường `[...]`, và GetProperty("Item") ném AmbiguousMatchException
        /// khi type có nhiều indexer.
        public static PropertyInfo GetPropertyRecursive(this Type type, string name, BindingFlags bindingFlags = ALL)
        {
            var propertyInfo = PlainProperty(type, name, bindingFlags);
            if (propertyInfo != null) return propertyInfo;
            if (type.BaseType != null) return type.BaseType.GetPropertyRecursive(name, bindingFlags);

            foreach (var parent in InheritedInterfaces(type))
            {
                var inherited = PlainProperty(parent, name, bindingFlags);
                if (inherited != null) return inherited;
            }
            return null;
        }

        private static PropertyInfo PlainProperty(Type type, string name, BindingFlags bindingFlags) =>
            Array.Find(type.GetProperties(bindingFlags | BindingFlags.DeclaredOnly),
                property => property.Name == name && property.GetIndexParameters().Length == 0);

        /// Interface không có BaseType, member kế thừa của nó nằm ở các interface cha — kể cả member
        /// **static** (kiểu `Zego.IGlobalService&lt;T&gt;.Global` mà `IAdService` kế thừa). Đi bằng
        /// BaseType không thì query `Zego.IAdService` + `Global.X` luôn báo không tìm thấy member.
        ///
        /// GetInterfaces() đã trả về cả interface cha gián tiếp nên không cần đệ quy, và cũng không
        /// bị trùng khi thừa kế hình thoi.
        private static Type[] InheritedInterfaces(Type type)
        {
            return type.IsInterface ? type.GetInterfaces() : Type.EmptyTypes;
        }
    }
}
