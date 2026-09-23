using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Nguồn node thứ hai, cạnh DebugRegistry: member của một object thật, sinh lúc mở trang.
    public static class Reflect
    {
        public const int ELEMENT_LIMIT = 100;

        private const BindingFlags ALL = BindingFlags.Static | BindingFlags.Instance |
                                         BindingFlags.NonPublic | BindingFlags.Public |
                                         BindingFlags.DeclaredOnly;

        public static bool IsCollection(object value) => value is IEnumerable && value is not string;

        /// Hiện **hết** field và property, đi hết chuỗi kế thừa, public lẫn private, instance lẫn static.
        /// Ghi được thì node có Set, không ghi được thì Set = null và renderer ra dòng read-only —
        /// không có bộ lọc nào nữa.
        ///
        /// Lý do bỏ "chỉ member khai báo ở lớp cuối": `Harvest.Data.ProfileEntry` không khai một member
        /// nào (tất cả ở `DataEntry<T>`), nên luật cũ mở nó ra là một trang trống.
        ///
        /// Thứ tự: lớp dẫn xuất trước, lớp cha sau — member của chính object nằm trên đầu, đồ của Unity
        /// rơi xuống cuối, mà không phải giấu cái gì.
        public static IEnumerable<DebugNode> Members(Cursor cursor, string address)
        {
            var type = cursor.Value?.GetType() ?? cursor.Declared;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var level = type; level != null && level != typeof(object); level = level.BaseType)
            {
                foreach (var field in level.GetFields(ALL))
                {
                    if (Generated(field) || !seen.Add(field.Name)) continue;
                    yield return ValueFor(cursor, address, field.Name, field.FieldType);
                }
                foreach (var property in level.GetProperties(ALL))
                {
                    if (Generated(property) || property.GetIndexParameters().Length > 0) continue;
                    if (!seen.Add(property.Name)) continue;
                    yield return ValueFor(cursor, address, property.Name, property.PropertyType);
                }
            }
        }

        /// Method tách khỏi danh sách giá trị. Không phải để giấu: `RootScope` có 239 method và
        /// `Transform` có 319 — trộn chung thì 72 dòng giá trị chìm mất. Trang member có một row
        /// `Method (N) ›` mở thẳng sang danh sách đầy đủ này.
        public static IEnumerable<DebugNode> Methods(Cursor cursor, string address)
        {
            var type = cursor.Value?.GetType() ?? cursor.Declared;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Đi tới tận `object` (khác Members): ToString/GetType là method gọi được thật.
            for (var level = type; level != null; level = level.BaseType)
            {
                foreach (var method in level.GetMethods(ALL))
                {
                    if (method.IsSpecialName || Generated(method)) continue;
                    if (!seen.Add($"{method.Name}#{method.GetParameters().Length}")) continue;
                    yield return ActionFor(cursor, address, method);
                }
            }
        }

        public static int MethodCount(Cursor cursor)
        {
            var count = 0;
            foreach (var _ in Methods(cursor, null)) count++;
            return count;
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

        private static ActionNode ActionFor(Cursor parent, string address, MethodInfo method)
        {
            var parameters = method.GetParameters();
            var descriptors = new DebugParameter[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                descriptors[i] = new DebugParameter(parameters[i].Name, parameters[i].ParameterType);

            var source = parent.Value;
            // Key ổn định qua các lần rebuild: ParamsPage giữ giá trị đã nhập ở DebugRegistry.ArgsByKey
            // theo Key này (không phải trong closure), nên thiếu nó thì chọn xong một tham số, back
            // ra rồi vào lại là mất — xem doc comment của DebugNode.Key. Ưu tiên address (định danh
            // đúng instance/static root đang đứng); không có address (con của FolderNode do game
            // dựng) thì lùi về FullName của type, đủ ổn định cho case đó.
            var owner = address ?? (parent.Value?.GetType() ?? parent.Declared)?.FullName;
            var awaitable = Awaitables.IsAwaitable(method.ReturnType);
            // Dựng node **trước** rồi mới gán Invoke: cờ chờ tra theo node.Key, mà object initializer
            // không tự tham chiếu được chính cái nó đang khởi tạo.
            var node = new ActionNode
            {
                Label = method.Name,
                Description = DebugLogConsoleName(method.ReturnType),
                Parameters = descriptors,
                Key = $"reflect:{owner}.{method.Name}#{parameters.Length}",
                Dismiss = DismissMode.Stay,
                Awaitable = awaitable,
            };
            node.Invoke = args =>
            {
                var result = method.Invoke(method.IsStatic ? null : source, args);
                if (method.ReturnType == typeof(void)) return;

                // Gọi một method async rồi in "UniTask" ra màn hình là vô nghĩa — mặc định chờ.
                if (awaitable && DebugRegistry.AwaitEnabled(node))
                {
                    DebugHub.Await(result, method.Name);
                    return;
                }
                UnityEngine.Debug.Log(DebugValues.ToText(result));
            };
            return node;
        }

        private static ValueNode Element(Cursor parent, string address, string index, string label, object value)
        {
            var childAddress = address == null ? null : Address.Index(address, index);
            var snapshot = parent.Value;
            var node = new ValueNode
            {
                Label = label,
                Declared = value?.GetType() ?? typeof(object),
                Address = childAddress,
                Dismiss = DismissMode.Stay,
                Get = () => value,
            };
            // node.Address, không phải childAddress đã capture — cùng lý do với ValueFor: đổi
            // Address sau khi dựng node phải đổi luôn chỗ Set trỏ tới.
            //
            // Lỗi phải nổi lên (cùng luật với ValueFor ở trên): nuốt lỗi ở đây thì Set âm thầm
            // không ghi gì, người dùng tưởng đã ghi xong.
            node.Set = snapshot is IList && childAddress != null
                ? v =>
                {
                    if (!Address.TryWrite(node.Address, v, out var error)) throw new Exception(error);
                }
                : null;
            return node;
        }

        /// Ngoại lệ **duy nhất** của "hiện hết": backing field của auto-property. Nó là đúng cùng một ô
        /// nhớ với property ngay trên nó, chỉ khác cái tên không đọc được — giữ cả hai là mọi
        /// auto-property ra hai dòng. Field viết tay (`_playerSave`) thì **giữ**: không có gì bảo đảm
        /// nó bằng property `PlayerSave`.
        private static bool Generated(MemberInfo member)
        {
            return member.IsDefined(typeof(CompilerGeneratedAttribute), false) ||
                   member.Name.Contains("k__BackingField");
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
