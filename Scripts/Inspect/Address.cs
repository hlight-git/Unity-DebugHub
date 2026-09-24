using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IngameDebugConsole;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Một chỗ đứng trong cây dữ liệu: kiểu khai báo, giá trị hiện tại, và cách ghi đè giá trị đó.
    internal readonly struct Cursor
    {
        public readonly Type Declared;
        public readonly object Value;
        public readonly Action<object> Write;

        public Cursor(Type declared, object value, Action<object> write)
        {
            Declared = declared;
            Value = value;
            Write = write;
        }

        public bool CanWrite => Write != null;
    }

    /// Địa chỉ là **trạng thái điều hướng duy nhất** của inspect: trang chỉ giữ một chuỗi và resolve
    /// lại mỗi lần dựng, nên không có tham chiếu object cũ nào nằm chờ chết ở đâu cả.
    ///
    ///     address = root ( "." member | "[" args "]" | "." Method(args) )*
    ///     root    = TypeName | $var | #TypeName[i] | @command.path
    ///
    /// Hai trần cố ý: không parse ngoặc cùng loại lồng nhau (bắc cầu qua $var), và `#Type[i]` đánh số
    /// theo InstanceID — ổn định trong phiên cho tới khi có object cùng type sinh ra/bị huỷ.
    internal static class Address
    {
        private const BindingFlags ALL = BindingFlags.Static | BindingFlags.Instance |
                                         BindingFlags.NonPublic | BindingFlags.Public;

        public static string Member(string address, string name) => $"{address}.{name}";
        public static string Index(string address, string index) => $"{address}[{index}]";

        /// Ghim từ chối address có bước gọi method — nếu không, `Factory.Spawn()` bị gọi mỗi lần trang
        /// Objects làm mới. Xét **step đã tách**, không tìm dấu ngoặc trong chuỗi: literal của indexer
        /// chứa được ngoặc.
        public static bool HasMethodStep(string address)
        {
            if (!TrySplit(address, out _, out var steps, out _)) return false;
            foreach (var step in steps)
            {
                // Indexer trước: literal bên trong "[...]" chứa được dấu '(' (vd khoá dictionary
                // "a(b)"), nên phải loại indexer ra trước khi tìm '(' — không thì literal đó bị
                // tưởng nhầm là một lời gọi method.
                if (!IsIndexer(step) && IsMethod(step)) return true;
            }
            return false;
        }

        public static bool TryResolve(string address, out Cursor cursor, out string error)
        {
            cursor = default;
            if (!TrySplit(address, out var root, out var steps, out error)) return false;
            if (!TryRoot(root, out cursor, out var staticRoot, out error)) return false;

            var walked = root;
            var first = true;
            foreach (var step in steps)
            {
                // Root là **type** thì Value = null có chủ đích: bước đầu tiên đọc member static
                // của nó. Chặn null vô điều kiện ở đây là `Type.StaticField` không bao giờ resolve.
                if (cursor.Value == null && !(first && staticRoot))
                {
                    error = $"'{walked}' là null nên không đi tiếp tới '{step}' được.";
                    return false;
                }
                first = false;
                if (!TryStep(cursor, step, out cursor, out error))
                {
                    error = $"{walked}: {error}";
                    return false;
                }
                walked = IsIndexer(step) ? walked + step : $"{walked}.{step}";
            }
            return true;
        }

        public static bool TryWrite(string address, object value, out string error)
        {
            if (!TryResolve(address, out var cursor, out error)) return false;
            return TryWrite(address, cursor, value, out error);
        }

        /// TrySet parse theo `cursor.Declared` (đòi resolve trước để biết kiểu) rồi mới ghi — dùng
        /// lại đúng cursor đó cho việc ghi thay vì để TryWrite(string, …) resolve lại lần hai: getter
        /// nằm trên đường đi (property, indexer, method step) có side effect thì double-resolve là
        /// chạy nó hai lần.
        public static bool TrySet(string address, string text, out string error)
        {
            if (!TryResolve(address, out var cursor, out error)) return false;
            return DebugValues.TryParse(text, cursor.Declared, out var value, out error, allowVars: true) &&
                   TryWrite(address, cursor, value, out error);
        }

        private static bool TryWrite(string address, Cursor cursor, object value, out string error)
        {
            error = null;
            if (!cursor.CanWrite)
            {
                error = $"'{address}' không ghi được (readonly, chỉ có getter, hoặc struct cha không ghi lại được).";
                return false;
            }

            try
            {
                cursor.Write(value);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Unwrap().Message;
                return false;
            }
        }

        #region Tách chuỗi

        /// Peel root trước rồi mới tách step: root có thể chứa dấu '.' (namespace, path command),
        /// nên tách trước là hỏng.
        private static bool TrySplit(string address, out string root, out string[] steps, out string error)
        {
            root = null;
            steps = Array.Empty<string>();
            error = null;

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Address rỗng.";
                return false;
            }

            address = address.Trim();
            string rest;

            if (address[0] == '$')
            {
                var cut = CutAfterDollarToken(address);
                root = address.Substring(0, cut);
                rest = cut < address.Length ? address.Substring(cut).TrimStart('.') : string.Empty;
            }
            else if (address[0] == '#')
            {
                if (!TrySplitInstanceRoot(address, out root, out rest, out error)) return false;
            }
            else if (address[0] == '@')
            {
                // Prefix dài nhất khớp path của một ValueNode đã đăng ký.
                if (!DebugRegistry.LongestValuePath(address.Substring(1), out var path, out rest))
                {
                    error = $"Không có command giá trị nào khớp '{address}'.";
                    return false;
                }
                root = "@" + path;
            }
            else
            {
                // Chỉ cho TypeFinder dò phần trước dấu '[' hoặc '(' đầu tiên, rồi cắt lại trên
                // **chuỗi gốc** — cắt theo độ dài của `rest` là sai ngay khi address có indexer.
                var head = HeadOf(address);
                if (TypeFinder.LongestPrefix(head, out var headRest) == null)
                {
                    error = $"Không tìm thấy type nào ở đầu '{address}'.";
                    return false;
                }
                var rootLength = head.Length - (headRest.Length == 0 ? 0 : headRest.Length + 1);
                root = address.Substring(0, rootLength);
                rest = address.Substring(rootLength).TrimStart('.');
            }

            steps = SplitSteps(rest);
            return true;
        }

        /// Cắt ở '.' và trước '[' — chỉ khi đang ở ngoài mọi cặp ngoặc và ngoài chuỗi trong nháy kép:
        /// `Echo<System.Int32>(5)`, `Map["a.b"]`, `Pick{1}(2.5)` đều là một bước.
        internal static string[] SplitSteps(string rest)
        {
            if (string.IsNullOrEmpty(rest)) return Array.Empty<string>();

            var steps = new List<string>();
            var closers = new Stack<char>();
            var quoted = false;
            var start = 0;
            for (var i = 0; i < rest.Length; i++)
            {
                var c = rest[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (quoted) continue;

                if (closers.Count == 0 && (c == '.' || c == '['))
                {
                    if (i > start) steps.Add(rest.Substring(start, i - start));
                    start = c == '.' ? i + 1 : i;
                }

                switch (c)
                {
                    case '[': closers.Push(']'); break;
                    case '(': closers.Push(')'); break;
                    case '{': closers.Push('}'); break;
                    case '<': closers.Push('>'); break;
                    default:
                        if (closers.Count > 0 && c == closers.Peek()) closers.Pop();
                        break;
                }
            }
            if (start < rest.Length) steps.Add(rest.Substring(start));
            return steps.ToArray();
        }

        /// Phần đầu tới trước dấu ngoặc đầu tiên — chỗ TypeFinder được phép dò.
        private static string HeadOf(string address)
        {
            var cut = address.IndexOfAny(new[] { '[', '(', '<', '{' });
            return cut < 0 ? address : address.Substring(0, cut);
        }

        /// $var không bao giờ có dấu '.' trong tên — dừng ở '.' (member step) hoặc '[' (indexer
        /// step) đầu tiên là đủ, khác hẳn '#' bên dưới vì tên type có thể chứa '.' (namespace).
        private static int CutAfterDollarToken(string address)
        {
            for (var i = 1; i < address.Length; i++)
            {
                if (address[i] == '.' || address[i] == '[') return i;
            }
            return address.Length;
        }

        /// Root `#TypeName[i]`: TypeName có thể có dấu '.' (namespace) nên không cắt ở '.' đầu tiên
        /// được — dùng lại đúng pattern `HeadOf` + `TypeFinder.LongestPrefix` mà nhánh type thường
        /// (else ở dưới) đã dùng để tìm prefix dài nhất khớp một type thật. Sau đó nếu `[i]` nằm
        /// ngay sau tên type thì nuốt luôn vào root — đây là index chọn instance, khác bước
        /// indexer thường ở `TryIndexer` phía sau.
        private static bool TrySplitInstanceRoot(string address, out string root, out string rest, out string error)
        {
            root = null;
            rest = string.Empty;
            error = null;

            var body = address.Substring(1);
            var head = HeadOf(body);
            if (TypeFinder.LongestPrefix(head, out var headRest) == null)
            {
                error = $"Không tìm thấy type nào ở đầu '{address}'.";
                return false;
            }

            var typeNameLength = head.Length - (headRest.Length == 0 ? 0 : headRest.Length + 1);
            var cut = 1 + typeNameLength; // +1 cho ký tự '#'
            if (cut < address.Length && address[cut] == '[')
            {
                var close = address.IndexOf(']', cut);
                cut = close >= 0 ? close + 1 : address.Length;
            }

            root = address.Substring(0, cut);
            rest = cut < address.Length ? address.Substring(cut).TrimStart('.') : string.Empty;
            return true;
        }

        private static bool IsMethod(string step) => step.Contains('(');
        private static bool IsIndexer(string step) => step.StartsWith("[");

        #endregion

        #region Root

        /// <paramref name="staticRoot"/> = root là một type, nên Value = null là đúng và bước
        /// đầu tiên phải đọc member static.
        private static bool TryRoot(string root, out Cursor cursor, out bool staticRoot, out string error)
        {
            cursor = default;
            staticRoot = false;
            error = null;

            if (root[0] == '$')
            {
                if (!Vars.TryGet(root.Substring(1), out var value))
                {
                    error = $"Biến '{root}' chưa được gán.";
                    return false;
                }
                cursor = new Cursor(value?.GetType() ?? typeof(object), value, null);
                return true;
            }

            if (root[0] == '@')
            {
                if (!DebugRegistry.TryFindValue(root.Substring(1), out var node))
                {
                    error = $"Không có command giá trị '{root}'.";
                    return false;
                }
                try
                {
                    cursor = new Cursor(node.Declared, node.Get(), node.Set);
                }
                catch (Exception exception)
                {
                    error = $"{root}: {exception.Unwrap().Message}";
                    return false;
                }
                return true;
            }

            if (root[0] == '#') return TryInstance(root, out cursor, out error);

            var type = TypeFinder.Find(root);
            if (type == null)
            {
                error = $"Không tìm thấy type '{root}'.";
                return false;
            }
            // Root là type: Value = null, nên step đầu tiên đọc member **static** của nó.
            cursor = new Cursor(type, null, null);
            staticRoot = true;
            return true;
        }

        /// Instance đang sống (kể cả inactive) theo đúng thứ tự mà `#Type[i]` đánh số. Danh sách của
        /// Duyệt cũng lấy từ đây: SortMode.None không hứa thứ tự giữa hai lần gọi, nên danh sách và lần
        /// resolve sau đó có thể trỏ hai object khác nhau.
        internal static Object[] LiveInstances(Type type) =>
            Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        /// `#Type` = instance đầu tiên đang sống, `#Type[i]` = cái thứ i.
        private static bool TryInstance(string root, out Cursor cursor, out string error)
        {
            cursor = default;
            error = null;
            var body = root.Substring(1);
            var order = 0;
            var bracket = body.IndexOf('[');
            if (bracket >= 0)
            {
                var index = body.Substring(bracket + 1).TrimEnd(']');
                if (!int.TryParse(index, out order))
                {
                    error = $"'{index}' không phải một số nguyên.";
                    return false;
                }
                body = body.Substring(0, bracket);
            }

            var type = TypeFinder.Find(body);
            if (type == null || !typeof(Object).IsAssignableFrom(type))
            {
                error = $"'{body}' không phải một UnityEngine.Object type.";
                return false;
            }
            // FindObjectsByType ném với type generic mở (`Singleton`1`) — cùng luật BrowsePage.
            if (type.ContainsGenericParameters)
            {
                error = $"'{body}' là generic mở — không có instance nào mang đúng type này.";
                return false;
            }

            var found = LiveInstances(type);
            if (order < 0 || order >= found.Length)
            {
                error = $"Chỉ có {found.Length} instance của {body} đang sống.";
                return false;
            }
            cursor = new Cursor(type, found[order], null);
            return true;
        }

        #endregion

        #region Step

        private static bool TryStep(Cursor parent, string step, out Cursor cursor, out string error)
        {
            if (IsIndexer(step)) return TryIndexer(parent, step, out cursor, out error);
            if (IsMethod(step)) return TryMethod(parent, step, out cursor, out error);
            return TryMember(parent, step, out cursor, out error);
        }

        /// Getter của Unity/TMP mà chỉ cần **đọc** là tạo bản sao asset gắn vĩnh viễn vào object (mất
        /// batching, rò bộ nhớ). Trang member đọc mọi property nên phải chặn ở đây.
        private static readonly HashSet<string> CloningGetters = new(StringComparer.Ordinal)
        {
            "UnityEngine.Renderer.material", "UnityEngine.Renderer.materials",
            "UnityEngine.MeshFilter.mesh", "UnityEngine.Collider.material",
            "TMPro.TMP_Text.fontMaterial", "TMPro.TMP_Text.fontMaterials",
            // Sub-mesh của fallback font / sprite: getter gọi CreateMaterialInstance.
            "TMPro.TMP_SubMesh.material", "TMPro.TMP_SubMeshUI.material",
        };

        internal static bool IsCloningGetter(PropertyInfo property) =>
            CloningGetters.Contains($"{property.DeclaringType?.FullName}.{property.Name}");

        /// Quyền ghi xét **từng bước**:
        /// - parent là reference → ghi thẳng lên object, không cần parent.Write;
        /// - parent là value type đã boxing → phải có parent.Write để ghi cả bản copy về chỗ cũ;
        /// - readonly/const/không có setter → bước này không ghi được, nhưng vẫn mở vào trong được.
        internal static bool TryMember(Cursor parent, string name, out Cursor cursor, out string error)
        {
            cursor = default;
            error = null;
            var type = parent.Value?.GetType() ?? parent.Declared;
            var source = parent.Value;
            var needsWriteBack = source != null && source.GetType().IsValueType;

            var field = type.GetFieldRecursive(name, ALL);
            if (field != null)
            {
                object value;
                try { value = field.GetValue(field.IsStatic ? null : source); }
                catch (Exception exception) { error = exception.Unwrap().Message; return false; }

                Action<object> write = null;
                if (Writable(field, parent))
                {
                    write = v =>
                    {
                        field.SetValue(field.IsStatic ? null : source, v);
                        if (!field.IsStatic && needsWriteBack) parent.Write(source);
                    };
                }
                cursor = new Cursor(field.FieldType, value, write);
                return true;
            }

            var property = type.GetPropertyRecursive(name, ALL);
            if (property == null)
            {
                error = $"không có field hoặc property '{name}' trong `{type.Name}`";
                return false;
            }

            if (IsCloningGetter(property))
            {
                error = $"không đọc '{name}': đọc là tạo bản sao asset gắn vào object — dùng bản shared.";
                return false;
            }

            var getter = property.GetMethod;
            object read;
            try { read = property.GetValue(getter != null && getter.IsStatic ? null : source); }
            catch (Exception exception) { error = exception.Unwrap().Message; return false; }

            Action<object> setter = null;
            var isStatic = getter != null && getter.IsStatic;
            if (Writable(property, parent))
            {
                setter = v =>
                {
                    property.SetValue(isStatic ? null : source, v);
                    if (!isStatic && needsWriteBack) parent.Write(source);
                };
            }
            cursor = new Cursor(property.PropertyType, read, setter);
            return true;
        }

        /// Ghi được member `name` của parent không — xét **chỉ bằng metadata**, không đọc giá trị.
        /// Reflect hỏi câu này cho mọi dòng lúc dựng trang; đi qua TryMember là đọc mỗi getter một lần
        /// chỉ để vứt giá trị đi, rồi renderer đọc lại lần nữa.
        internal static bool CanWrite(Cursor parent, string name)
        {
            var type = parent.Value?.GetType() ?? parent.Declared;
            var field = type.GetFieldRecursive(name, ALL);
            if (field != null) return Writable(field, parent);
            var property = type.GetPropertyRecursive(name, ALL);
            return property != null && Writable(property, parent);
        }

        /// Một bản duy nhất của luật ghi, dùng chung cho TryMember và CanWrite.
        /// Member instance trên root static (Value null) không ghi được: không có object để ghi vào.
        private static bool Writable(FieldInfo field, Cursor parent)
        {
            if (field.IsInitOnly || field.IsLiteral) return false;
            return field.IsStatic || (parent.Value != null && (!parent.Value.GetType().IsValueType || parent.Write != null));
        }

        private static bool Writable(PropertyInfo property, Cursor parent)
        {
            if (!property.CanWrite) return false;
            var accessor = property.GetMethod ?? property.SetMethod;
            if (accessor.IsStatic) return true;
            return parent.Value != null && (!parent.Value.GetType().IsValueType || parent.Write != null);
        }

        private static bool TryIndexer(Cursor parent, string step, out Cursor cursor, out string error)
        {
            cursor = default;
            error = null;
            var source = parent.Value;
            var arguments = new List<string>();
            DebugLogConsole.FetchArgumentsFromCommand(Between(step, '[', ']'), arguments);
            if (arguments.Count == 0)
            {
                error = "indexer không có tham số nào";
                return false;
            }

            // Indexer luôn là member instance (C# không có static indexer) — root static (`Type[0]`
            // ngay bước đầu, chưa qua member nào ra một instance) thì source null, và source.GetType()
            // dưới đây ném NRE thay vì lỗi đọc được. cùng luật với needsWriteBack vài dòng dưới.
            if (source == null)
            {
                error = "không có instance để đọc indexer (đang đứng ở một type, không phải object).";
                return false;
            }

            // Mảng không có indexer công khai nào (chỉ có IList.Item khai tường minh, bị lọc ở dưới).
            if (source is Array array) return TryArrayElement(array, arguments, out cursor, out error);

            var indexers = new List<PropertyInfo>();
            // Loại explicit interface implementation (`System.Collections.IList.Item`,
            // `IDictionary.Item`, …): List<T>/Dictionary<K,V> khai cả bản public THẬT lẫn bản này
            // trên chính class, nên không lọc thì Items[1] luôn báo "2 indexer cùng khớp" dù
            // người dùng chỉ thấy đúng một indexer khi gõ code — property.Name chứa '.' là dấu
            // hiệu duy nhất phân biệt được hai loại này (indexer khai bình thường không có '.').
            source.GetType().AddIndexersRecursive(indexers,
                property => property.GetIndexParameters().Length == arguments.Count &&
                            !property.Name.Contains('.'), ALL);
            if (indexers.Count == 0)
            {
                error = $"không có indexer nào nhận {arguments.Count} tham số";
                return false;
            }

            // `{n}` nằm sau `]`: key `["{1}"]` không được hiểu thành "chọn indexer số 1".
            if (!TryPick(indexers, step.Substring(step.LastIndexOf(']') + 1), "indexer", out var indexer, out error)) return false;
            var parameters = indexer.GetIndexParameters();
            var keys = new object[arguments.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                if (!DebugValues.TryParse(arguments[i], parameters[i].ParameterType, out keys[i], out error,
                        allowVars: true)) return false;
            }

            object value;
            try { value = indexer.GetValue(source, keys); }
            catch (Exception exception) { error = exception.Unwrap().Message; return false; }

            // Cùng luật write-back với TryMember: parent là struct đã boxing thì phải có parent.Write
            // để ghi cả bản copy về chỗ cũ — không thì `SomeVectorField[0] = 5` chạy không lỗi nhưng
            // chỉ sửa một bản copy vứt đi.
            var needsWriteBack = source != null && source.GetType().IsValueType;
            Action<object> write = null;
            if (indexer.CanWrite && (!needsWriteBack || parent.Write != null))
            {
                write = v =>
                {
                    indexer.SetValue(source, v, keys);
                    if (needsWriteBack) parent.Write(source);
                };
            }
            cursor = new Cursor(indexer.PropertyType, value, write);
            return true;
        }

        /// Mảng là reference nên ghi thẳng vào phần tử, không cần write-back qua parent.
        private static bool TryArrayElement(Array array, List<string> arguments, out Cursor cursor, out string error)
        {
            cursor = default;
            error = null;
            if (arguments.Count != array.Rank)
            {
                error = $"mảng {array.Rank} chiều cần {array.Rank} chỉ số";
                return false;
            }

            var indices = new int[arguments.Count];
            for (var i = 0; i < indices.Length; i++)
            {
                if (int.TryParse(arguments[i], out indices[i]) && indices[i] >= 0 && indices[i] < array.GetLength(i))
                    continue;
                error = $"chỉ số '{arguments[i]}' nằm ngoài mảng (dài {array.GetLength(i)})";
                return false;
            }

            cursor = new Cursor(array.GetType().GetElementType(), array.GetValue(indices),
                value => array.SetValue(value, indices));
            return true;
        }

        /// Gọi method: đọc được nhưng **không** ghi được, và Ghim từ chối address chứa bước này.
        private static bool TryMethod(Cursor parent, string step, out Cursor cursor, out string error)
        {
            cursor = default;
            error = null;
            var source = parent.Value;
            // `<…>` và `{n}` chỉ đọc ở phần trước `(`: `Find("a<b")` có `<` nằm trong tham số, không phải
            // type argument. Tên dừng ở ký tự đặc biệt đầu tiên: `Find<$T>{1}(x)` có tên là `Find`.
            var head = step.Substring(0, step.IndexOf('('));
            var special = head.IndexOfAny(new[] { '<', '{' });
            var name = special < 0 ? head : head.Substring(0, special);
            var arguments = new List<string>();
            DebugLogConsole.FetchArgumentsFromCommand(step.Substring(step.IndexOf('(') + 1).TrimEnd(')'), arguments);

            var overloads = new List<MethodInfo>();
            (source?.GetType() ?? parent.Declared).AddMethodsRecursive(overloads,
                method => method.Name == name && method.GetParameters().Length == arguments.Count, ALL);
            if (overloads.Count == 0)
            {
                error = $"không có method '{name}' nhận {arguments.Count} tham số";
                return false;
            }

            if (!TryPick(overloads, head, "method", out var target, out error)) return false;

            if (head.Contains('<'))
            {
                if (!target.IsGenericMethodDefinition)
                {
                    error = $"'{name}' không phải method generic";
                    return false;
                }
                var typeArguments = new List<string>();
                DebugLogConsole.FetchArgumentsFromCommand(Between(head, '<', '>'), typeArguments);
                var resolved = new Type[typeArguments.Count];
                for (var i = 0; i < resolved.Length; i++)
                {
                    var token = typeArguments[i];
                    resolved[i] = token.StartsWith("$") && Vars.TryGet(token.Substring(1), out var bound)
                        ? bound as Type
                        : TypeFinder.Find(token);
                    if (resolved[i] == null)
                    {
                        error = $"'{token}' không phải một type (gán type vào biến rồi dùng $tên)";
                        return false;
                    }
                }
                // Sai số type argument hoặc vi phạm ràng buộc `where` → ArgumentException; hàm Try* không
                // được ném ra ngoài.
                try { target = target.MakeGenericMethod(resolved); }
                catch (ArgumentException exception)
                {
                    error = exception.Message;
                    return false;
                }
            }
            else if (target.IsGenericMethodDefinition)
            {
                error = $"'{name}' là generic — truyền type theo cú pháp {name}<$T>(...)";
                return false;
            }
            var parameters = target.GetParameters();
            var args = new object[parameters.Length];
            for (var i = 0; i < args.Length; i++)
            {
                if (!DebugValues.TryParse(arguments[i], parameters[i].ParameterType, out args[i], out error,
                        allowVars: true)) return false;
            }

            object value;
            try { value = target.Invoke(target.IsStatic ? null : source, args); }
            catch (Exception exception) { error = exception.Unwrap().Message; return false; }

            cursor = new Cursor(target.ReturnType, value, null);
            return true;
        }

        /// Nhiều ứng viên cùng khớp thì phải để người dùng chọn bằng `{n}`, **không** lấy bừa
        /// cái đầu: hai overload khác kiểu tham số nhưng cùng số lượng là chuyện thường, và chạy
        /// nhầm cái kia thì im lặng ra kết quả sai.
        /// <paramref name="selector"/> là phần của bước được phép chứa `{n}` — không gồm tham số.
        private static bool TryPick<T>(List<T> candidates, string selector, string kind, out T picked, out string error)
        {
            error = null;
            if (candidates.Count == 1)
            {
                picked = candidates[0];
                return true;
            }

            if (selector.Contains('{') && selector.Contains('}') &&
                int.TryParse(Between(selector, '{', '}'), out var order) && order >= 0 && order < candidates.Count)
            {
                picked = candidates[order];
                return true;
            }

            picked = default;
            error = $"có {candidates.Count} {kind} cùng khớp — chọn bằng ngoặc nhọn, ví dụ Ten{{0}}(...):\n- " +
                    string.Join("\n- ", candidates);
            return false;
        }

        /// Đoạn giữa cặp ký tự đầu tiên. Không xử lý lồng nhau — trần đã ghi ở đầu class.
        private static string Between(string text, char head, char tail)
        {
            var start = text.IndexOf(head) + 1;
            var end = text.IndexOf(tail, start);
            return start > 0 && end >= 0 ? text.Substring(start, end - start) : string.Empty;
        }

        #endregion
    }
}
