using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hlight.Debug.Hub
{
    internal static class ReflectionExtensions
    {
        private const BindingFlags ALL = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static object GetValueOfFieldOrPropertyRecursive(this Type type, object source, string memberName, out Type memberInfoType, BindingFlags bindingFlags = ALL)
        {
            FieldInfo fieldInfo = type.GetFieldRecursive(memberName, bindingFlags);
            if (fieldInfo != null)
            {
                memberInfoType = fieldInfo.FieldType;
                return fieldInfo.GetValue(source);
            }

            PropertyInfo propertyInfo = type.GetPropertyRecursive(memberName, bindingFlags);
            if (propertyInfo != null)
            {
                memberInfoType = propertyInfo.PropertyType;
                return propertyInfo.GetValue(source);
            }

            memberInfoType = null;
            return null;
        }

        /// Kiểu của field/property, null nếu không có member nào tên đó.
        public static Type GetMemberType(this Type type, string memberName, BindingFlags bindingFlags = ALL)
        {
            FieldInfo fieldInfo = type.GetFieldRecursive(memberName, bindingFlags);
            if (fieldInfo != null) return fieldInfo.FieldType;
            return type.GetPropertyRecursive(memberName, bindingFlags)?.PropertyType;
        }

        /// Ghi giá trị vào field/property. Truyền source null cho member static.
        /// Ném lỗi khi không tìm thấy member hoặc property không có setter — im lặng bỏ qua thì
        /// người dùng tưởng lệnh đã chạy.
        public static void SetMemberValue(this Type type, object source, string memberName, object value, BindingFlags bindingFlags = ALL)
        {
            FieldInfo fieldInfo = type.GetFieldRecursive(memberName, bindingFlags);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(source, value);
                return;
            }

            PropertyInfo propertyInfo = type.GetPropertyRecursive(memberName, bindingFlags);
            if (propertyInfo == null)
                throw new Exception($"Not found field (or property) \"{memberName}\" in `{type.Name}`.");
            if (!propertyInfo.CanWrite)
                throw new Exception($"Property \"{memberName}\" in `{type.Name}` is read-only.");

            propertyInfo.SetValue(source, value);
        }

        public static void AddIndexersRecursive(this Type type, List<PropertyInfo> indexers, Func<PropertyInfo, bool> predicate, BindingFlags bindingFlags = ALL)
        {
            indexers.AddRange(type.GetProperties(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate));
            if (type.BaseType != null)
                AddIndexersRecursive(type.BaseType, indexers, predicate, bindingFlags);
        }

        public static void AddMethodsRecursive(this Type type, List<MethodInfo> methodInfos, Func<MethodInfo, bool> predicate, BindingFlags bindingFlags = ALL)
        {
            methodInfos.AddRange(type.GetMethods(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate));
            if (type.BaseType != null)
                AddMethodsRecursive(type.BaseType, methodInfos, predicate, bindingFlags);
        }

        public static FieldInfo GetFieldRecursive(this Type type, string name, BindingFlags bindingFlags = ALL)
        {
            var fieldInfo = type.GetField(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (fieldInfo == null && type.BaseType != null)
                return type.BaseType.GetFieldRecursive(name, bindingFlags);
            return fieldInfo;
        }

        /// GetProperty() không trả về property non-public của class cha, nên phải tự đi lên.
        public static PropertyInfo GetPropertyRecursive(this Type type, string name, BindingFlags bindingFlags = ALL)
        {
            var propertyInfo = type.GetProperty(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (propertyInfo == null && type.BaseType != null)
                return type.BaseType.GetPropertyRecursive(name, bindingFlags);
            return propertyInfo;
        }
    }
}
