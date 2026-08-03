using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace Hlight.Debug.Hub
{
    public static class ReflectionExtensions
    {
        public static object GetValueOfFieldOrPropertyRecursive(this Type type, object source, string memberName, out Type memberInfoType, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            FieldInfo fieldInfo = type.GetFieldRecursive(memberName, bindingFlags);
            if (fieldInfo != null)
            {
                memberInfoType = fieldInfo.FieldType;
                return fieldInfo.GetValue(source);
            }
            PropertyInfo propertyInfo = type.GetProperty(memberName, bindingFlags);
            if (propertyInfo != null)
            {
                memberInfoType = propertyInfo.PropertyType;
                return propertyInfo.GetValue(source);
            }

            memberInfoType = null;
            return null;
        }
        public static Type TryGetTypeOfFieldOrPropertyRecursive(this Type type, object source, string memberName, out object value, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            FieldInfo fieldInfo = type.GetFieldRecursive(memberName, bindingFlags);
            if (fieldInfo != null)
            {
                value = fieldInfo.GetValue(source);
                return fieldInfo.FieldType;
            }
            PropertyInfo propertyInfo = type.GetProperty(memberName, bindingFlags);
            if (propertyInfo != null)
            {
                value = propertyInfo.GetValue(source);
                return propertyInfo.PropertyType;
            }

            value = null;
            return null;
        }

        public static PropertyInfo GetIndexerRecursive(this Type type, IEnumerable<Type> parameterTypes, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            var indexerInfo = type.GetProperties(bindingFlags | BindingFlags.DeclaredOnly).FirstOrDefault(p => p.GetIndexParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes));
            if (indexerInfo == null && type.BaseType != null)
                return type.BaseType.GetIndexerRecursive(parameterTypes, bindingFlags);
            return indexerInfo;
        }

        public static void AddIndexersRecursive(this Type type, List<PropertyInfo> indexers, Func<PropertyInfo, bool> predicate, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            indexers.AddRange(type.GetProperties(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate));
            if (type.BaseType != null)
                AddIndexersRecursive(type.BaseType, indexers, predicate, bindingFlags);
        }

        public static MethodInfo GetMethodRecursive(this Type type, string methodName, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            var methodInfo = type.GetMethod(methodName, bindingFlags | BindingFlags.DeclaredOnly);
            if (methodInfo == null && type.BaseType != null)
                return type.BaseType.GetMethodRecursive(methodName, bindingFlags);
            return methodInfo;
        }

        public static void AddMethodsRecursive(this Type type, List<MethodInfo> methodInfos, Func<MethodInfo, bool> predicate, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            methodInfos.AddRange(type.GetMethods(bindingFlags | BindingFlags.DeclaredOnly).Where(predicate));
            if (type.BaseType != null)
                AddMethodsRecursive(type.BaseType, methodInfos, predicate, bindingFlags);
        }

        public static MethodInfo GetMethodRecursive(this Type type, string methodName, IEnumerable<Type> parameterTypes, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            var methodInfo = type.GetMethods(bindingFlags | BindingFlags.DeclaredOnly).FirstOrDefault(m => m.Name == methodName && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes));
            if (methodInfo == null && type.BaseType != null)
                return type.BaseType.GetMethodRecursive(methodName, parameterTypes, bindingFlags);
            return methodInfo;
        }

        public static FieldInfo GetFieldRecursive(this Type type, string name, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            var fieldInfo = type.GetField(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (fieldInfo == null && type.BaseType != null)
                return type.BaseType.GetFieldRecursive(name, bindingFlags);
            return fieldInfo;
        }

        public static PropertyInfo GetPropertyRecursive(this Type type, string name, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            var propertyInfo = type.GetProperty(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (propertyInfo == null && type.BaseType != null)
                return type.BaseType.GetPropertyRecursive(name, bindingFlags);
            return propertyInfo;
        }

        public static void TrySetValue(this object source, string memberName, object value, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            Type type = source.GetType();
            FieldInfo fieldInfo = type.GetFieldRecursive(memberName, bindingFlags);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(source, value);
                return;
            }
            PropertyInfo propertyInfo = type.GetProperty(memberName, bindingFlags);
            propertyInfo?.SetValue(source, value);
        }
    }
}