using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    [Flags]
    public enum MemberFilter
    {
        Default = 0,
        Inherited = 1,
        Methods = 2,
        Obsolete = 4,
    }

    /// Nguồn node thứ hai, cạnh DebugRegistry: member của một object thật, sinh lúc mở trang.
    public static class Reflect
    {
        public const int ELEMENT_LIMIT = 100;

        private const BindingFlags ALL = BindingFlags.Static | BindingFlags.Instance |
                                         BindingFlags.NonPublic | BindingFlags.Public |
                                         BindingFlags.DeclaredOnly;

        /// Dừng trước mấy type này khi đi lên: đi tiếp là ra một rừng member của Unity mà không ai
        /// mở hub để xem.
        private static readonly HashSet<Type> Stop = new()
        {
            typeof(MonoBehaviour), typeof(Behaviour), typeof(Component), typeof(ScriptableObject), typeof(Object),
        };

        public static bool IsCollection(object value) => value is IEnumerable && value is not string;

        public static IEnumerable<DebugNode> Members(Cursor cursor, string address, MemberFilter filter)
        {
            var type = cursor.Value?.GetType() ?? cursor.Declared;

            for (var level = type; level != null && !Stop.Contains(level); level = level.BaseType)
            {
                foreach (var field in level.GetFields(ALL))
                {
                    if (Skip(field, filter)) continue;
                    yield return ValueFor(cursor, address, field.Name, field.FieldType);
                }
                foreach (var property in level.GetProperties(ALL))
                {
                    if (Skip(property, filter) || property.GetIndexParameters().Length > 0) continue;
                    yield return ValueFor(cursor, address, property.Name, property.PropertyType);
                }
                if ((filter & MemberFilter.Methods) != 0)
                {
                    foreach (var method in level.GetMethods(ALL))
                    {
                        if (Skip(method, filter) || method.IsSpecialName) continue;
                        yield return ActionFor(cursor, method);
                    }
                }
                if ((filter & MemberFilter.Inherited) == 0) yield break;
            }
        }

        public static IEnumerable<DebugNode> Elements(Cursor cursor, string address)
        {
            if (cursor.Value is IDictionary map)
            {
                var shown = 0;
                foreach (DictionaryEntry pair in map)
                {
                    if (shown++ >= ELEMENT_LIMIT) break;
                    var key = DebugValues.ToText(pair.Key);
                    yield return Element(cursor, address, $"\"{key}\"", key, pair.Value);
                }
                if (map.Count > ELEMENT_LIMIT) yield return Rest(map.Count - ELEMENT_LIMIT);
                yield break;
            }

            if (cursor.Value is IList list)
            {
                for (var i = 0; i < list.Count && i < ELEMENT_LIMIT; i++)
                    yield return Element(cursor, address, i.ToString(), $"[{i}]", list[i]);
                if (list.Count > ELEMENT_LIMIT) yield return Rest(list.Count - ELEMENT_LIMIT);
                yield break;
            }

            // IEnumerable thường: không index được nên phần tử không có address (không watch được),
            // và phải cắt — nguồn tự sinh vô hạn là chuyện có thật.
            var index = 0;
            var overflow = false;
            foreach (var item in (IEnumerable)cursor.Value)
            {
                if (index >= ELEMENT_LIMIT) { overflow = true; break; }
                var captured = item;
                yield return Node.Value<object>($"[{index++}]", () => captured);
            }
            if (overflow) yield return Node.Text("còn nữa — cắt ở 100 phần tử.", TextStyle.Note);
        }

        private static DebugNode Rest(int count) => Node.Text($"còn {count} phần tử nữa.", TextStyle.Note);

        /// Node đọc/ghi qua **address**, không qua một box bắt được lúc dựng row: callback resolve
        /// lại trước mỗi lần đọc/ghi nên không bao giờ ghi vào bản copy cũ.
        private static ValueNode ValueFor(Cursor parent, string address, string name, Type declared)
        {
            var childAddress = address == null ? null : Address.Member(address, name);
            var node = new ValueNode
            {
                Label = name,
                Declared = declared,
                Address = childAddress,
                Dismiss = DismissMode.Stay,
            };

            // Lỗi phải nổi lên, không được nuốt: `Get` nuốt lỗi thì row hiện `null` cho một
            // property đang ném, và `Set` nuốt lỗi thì người dùng tưởng đã ghi xong. Renderer đã
            // bắt exception của Get để ra dòng lỗi (§3), còn exception của Set thì DebugRegistry.Run
            // bắt và đẩy ra dòng kết quả.
            if (childAddress != null)
            {
                // Đọc node.Address (không phải childAddress đã capture): test đổi Address sau khi
                // dựng node phải đổi luôn chỗ Get/Set trỏ tới — đúng như doc comment ở trên hứa
                // "resolve lại trước mỗi lần đọc/ghi", không phải resolve lại **địa chỉ cũ**.
                node.Get = () =>
                {
                    if (!Address.TryResolve(node.Address, out var fresh, out var error)) throw new Exception(error);
                    return fresh.Value;
                };
                node.Set = Writable(parent, name)
                    ? value =>
                    {
                        if (!Address.TryWrite(node.Address, value, out var error)) throw new Exception(error);
                    }
                    : null;
                return node;
            }

            // Không có address (con của một FolderNode do game dựng): đọc/ghi thẳng trên cursor này.
            var snapshot = parent;
            node.Get = () => Read(snapshot, name);
            node.Set = Writable(parent, name) ? value => Write(snapshot, name, value) : null;
            return node;
        }

        private static ActionNode ActionFor(Cursor parent, MethodInfo method)
        {
            var parameters = method.GetParameters();
            var descriptors = new DebugParameter[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                descriptors[i] = new DebugParameter(parameters[i].Name, parameters[i].ParameterType);

            var source = parent.Value;
            return new ActionNode
            {
                Label = method.Name,
                Description = DebugLogConsoleName(method.ReturnType),
                Parameters = descriptors,
                Dismiss = DismissMode.Stay,
                Invoke = args =>
                {
                    var result = method.Invoke(method.IsStatic ? null : source, args);
                    if (method.ReturnType != typeof(void)) UnityEngine.Debug.Log(DebugValues.ToText(result));
                },
            };
        }

        private static ValueNode Element(Cursor parent, string address, string index, string label, object value)
        {
            var childAddress = address == null ? null : Address.Index(address, index);
            var snapshot = parent.Value;
            return new ValueNode
            {
                Label = label,
                Declared = value?.GetType() ?? typeof(object),
                Address = childAddress,
                Dismiss = DismissMode.Stay,
                Get = () => value,
                Set = snapshot is IList list && childAddress != null
                    ? v => Address.TryWrite(childAddress, v, out _)
                    : null,
            };
        }

        private static bool Skip(MemberInfo member, MemberFilter filter)
        {
            // [Obsolete] của Unity ném hoặc log lỗi ngay khi đọc — quên cái này là trang inspect vỡ
            // ngay lần đầu mở trên một Component.
            if ((filter & MemberFilter.Obsolete) == 0 && member.IsDefined(typeof(ObsoleteAttribute), true)) return true;

            // Backing field của auto-property: không lọc thì mọi property hiện hai lần.
            if (member.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
            return member.Name.Contains("k__BackingField");
        }

        private static bool Writable(Cursor parent, string name)
        {
            return Step(parent, name, out var child) && child.CanWrite;
        }

        private static object Read(Cursor parent, string name)
        {
            if (!Address.TryMember(parent, name, out var child, out var error)) throw new Exception(error);
            return child.Value;
        }

        private static void Write(Cursor parent, string name, object value)
        {
            if (!Address.TryMember(parent, name, out var child, out var error)) throw new Exception(error);
            if (!child.CanWrite) throw new Exception($"'{name}' không ghi được.");
            child.Write(value);
        }

        private static bool Step(Cursor parent, string name, out Cursor child)
        {
            return Address.TryMember(parent, name, out child, out _);
        }

        private static string DebugLogConsoleName(Type type)
        {
            return type == typeof(void) ? null : IngameDebugConsole.DebugLogConsole.GetTypeReadableName(type);
        }
    }
}
