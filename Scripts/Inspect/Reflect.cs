using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
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

        /// Hiện **hết** field và property dùng được, đi hết chuỗi kế thừa, public lẫn private, instance
        /// lẫn static. Ghi được thì node có Set, không thì Set = null và renderer ra dòng read-only.
        ///
        /// Mỗi lớp khai báo một dòng tiêu đề. Đây là cách giải phần ồn còn lại **mà không giấu gì**:
        /// `RootScope` có ~20 member đến từ MonoBehaviour/Component/Object, chúng dùng được thật
        /// (`enabled`, `name`, `tag`) nên không có cớ để lọc — nhưng gom vào một khối có tên thì mắt bỏ
        /// qua cả khối trong một nhịp.
        ///
        /// Lý do đi hết chuỗi kế thừa: `Harvest.Data.ProfileEntry` không khai một member nào (tất cả ở
        /// `DataEntry<T>`), luật cũ "chỉ lớp cuối" mở nó ra là một trang trống.
        public static IEnumerable<DebugNode> Members(Cursor cursor, string address)
        {
            var type = cursor.Value?.GetType() ?? cursor.Declared;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var level = type; level != null && level != typeof(object); level = level.BaseType)
            {
                var rows = new List<DebugNode>();

                foreach (var field in level.GetFields(ALL))
                {
                    if (Skip(field) || !seen.Add(field.Name)) continue;
                    rows.Add(ValueFor(cursor, address, field.Name, field.FieldType));
                }
                foreach (var property in level.GetProperties(ALL))
                {
                    if (Skip(property) || property.GetIndexParameters().Length > 0) continue;
                    if (!seen.Add(property.Name)) continue;
                    rows.Add(ValueFor(cursor, address, property.Name, property.PropertyType));
                }

                // Lớp không có gì để hiện thì không có tiêu đề rỗng.
                if (rows.Count == 0) continue;

                yield return Node.Text(level == type ? level.Name : $"↑ {level.Name}", TextStyle.Note);
                foreach (var row in rows) yield return row;
            }
        }

        /// Method tách khỏi danh sách giá trị: `RootScope` có ~200, `Transform` ~300 — trộn chung thì
        /// mấy chục dòng giá trị chìm mất. Trang member có một row `Method  N ›` mở sang đây.
        public static IEnumerable<DebugNode> Methods(Cursor cursor, string address)
        {
            var type = cursor.Value?.GetType() ?? cursor.Declared;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Đi tới tận `object` (khác Members): ToString/GetType là method gọi được thật.
            for (var level = type; level != null; level = level.BaseType)
            {
                foreach (var method in level.GetMethods(ALL))
                {
                    if (!Keep(method) || !seen.Add(Signature(method))) continue;
                    yield return ActionFor(cursor, address, method);
                }
            }
        }

        /// Đếm mà **không dựng node**: bản cũ gọi Methods() rồi bỏ đi, tức dựng vài trăm ActionNode
        /// kèm DebugParameter[], GetTypeReadableName và một Awaitables.IsAwaitable mỗi cái — 7,5 ms
        /// cho mỗi lần mở một trang member, chỉ để in một con số.
        public static int MethodCount(Cursor cursor)
        {
            var type = cursor.Value?.GetType() ?? cursor.Declared;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;

            for (var level = type; level != null; level = level.BaseType)
            {
                foreach (var method in level.GetMethods(ALL))
                {
                    if (Keep(method) && seen.Add(Signature(method))) count++;
                }
            }
            return count;
        }

        /// Chữ ký đầy đủ, không phải `Tên#số-tham-số`: `GetComponent(String)` và `GetComponent(Type)`
        /// cùng arity, dedupe theo arity là **nuốt một cái** — đo trên Transform: 327 chữ ký thật chỉ
        /// ra 268 dòng. Cái bị nuốt không có đường nào gọi tới từ UI.
        private static string Signature(MethodInfo method)
        {
            var builder = new StringBuilder(method.Name).Append('(');
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(parameters[i].ParameterType.Name);
            }
            return builder.Append(')').ToString();
        }

        private static bool Keep(MethodInfo method)
        {
            if (method.IsSpecialName || Skip(method)) return false;

            // Hai cái này thừa hưởng từ object, protected, và gọi thật thì hại: Finalize chạy destructor
            // sớm, MemberwiseClone đẻ ra một bản sao nông không ai quản.
            return method.Name != "Finalize" && method.Name != "MemberwiseClone";
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
                // Element chứ không phải Node.Value<object>: Declared = object thì renderer cho mỗi số
                // trong HashSet<int> là một row nav phải bấm vào mới thấy. Address null — không index
                // được nên không ghim được.
                yield return Element(cursor, null, null, $"[{index++}]", captured);
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
                // Nhãn kèm kiểu tham số: hai overload cùng tên phải phân biệt được bằng mắt.
                Label = parameters.Length == 0
                    ? method.Name
                    : $"{method.Name}({string.Join(", ", Array.ConvertAll(parameters, p => p.ParameterType.Name))})",
                Description = DebugLogConsoleName(method.ReturnType),
                Parameters = descriptors,
                // Theo chữ ký: hai overload cùng arity phải có hai ô nhớ tham số riêng.
                Key = $"reflect:{owner}.{Signature(method)}",
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

        /// Những thứ **không dùng được**, khác với "ít dùng":
        /// - `[Obsolete]`: 13 property của mọi Component (`rigidbody`, `camera`…) đọc là ném "deprecated";
        /// - backing field của auto-property: đúng cùng một ô nhớ với property, chỉ khác cái tên;
        /// - `m_*` của engine (`m_CachedPtr`, `m_InstanceID`): con trỏ/handle phía C++, đọc ra số vô
        ///   nghĩa và ghi vào là hỏng object. 3–4 dòng mỗi object.
        ///
        /// Field viết tay (`_playerSave`) thì **giữ**, kể cả khi có property cùng tên: không có gì bảo
        /// đảm property trả về đúng field đó.
        private static bool Skip(MemberInfo member)
        {
            if (member.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
            if (member.IsDefined(typeof(ObsoleteAttribute), true)) return true;
            if (member.Name.Contains("k__BackingField")) return true;
            return member is FieldInfo && member.Name.StartsWith("m_", StringComparison.Ordinal);
        }

        private static bool Writable(Cursor parent, string name) => Address.CanWrite(parent, name);

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

        private static string DebugLogConsoleName(Type type)
        {
            return type == typeof(void) ? null : IngameDebugConsole.DebugLogConsole.GetTypeReadableName(type);
        }
    }
}
