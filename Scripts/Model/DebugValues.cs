using System;
using System.Collections.Generic;
using System.Globalization;
using IngameDebugConsole;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Cầu duy nhất sang parser của IngameDebugConsole, cộng hai câu hỏi mà IDC không trả lời được:
    /// "kiểu này có đường parse không" và "kiểu này có editor gõ tại chỗ không".
    ///
    /// Phải tự giữ bảng vì `DebugLogConsole.parseFunctions` là private, còn `ParseArgument` trả false
    /// lẫn lộn giữa "sai cú pháp" với "không hỗ trợ kiểu".
    public static class DebugValues
    {
        /// Đúng bảng parser của IDC (DebugLogConsole.parseFunctions) — sửa ở đây khi submodule đổi.
        private static readonly HashSet<Type> Parseable = new()
        {
            typeof(string), typeof(bool), typeof(char),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(short), typeof(ushort), typeof(byte), typeof(sbyte),
            typeof(float), typeof(double), typeof(decimal),
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion),
            typeof(Color), typeof(Color32), typeof(Rect), typeof(RectOffset),
            typeof(Bounds), typeof(Vector2Int), typeof(Vector3Int), typeof(RectInt), typeof(BoundsInt),
            typeof(GameObject),
        };

        /// Kiểu có nhiều thành phần: ToText ghép bằng dấu cách, ParseVector tách lại bằng space/phẩy.
        private static readonly Dictionary<Type, Func<object, float[]>> VectorParts = new()
        {
            { typeof(Vector2), v => { var x = (Vector2)v; return new[] { x.x, x.y }; } },
            { typeof(Vector3), v => { var x = (Vector3)v; return new[] { x.x, x.y, x.z }; } },
            { typeof(Vector4), v => { var x = (Vector4)v; return new[] { x.x, x.y, x.z, x.w }; } },
            { typeof(Quaternion), v => { var x = (Quaternion)v; return new[] { x.x, x.y, x.z, x.w }; } },
            { typeof(Color), v => { var x = (Color)v; return new[] { x.r, x.g, x.b, x.a }; } },
            { typeof(Color32), v => { var x = (Color32)v; return new float[] { x.r, x.g, x.b, x.a }; } },
            { typeof(Rect), v => { var x = (Rect)v; return new[] { x.x, x.y, x.width, x.height }; } },
            { typeof(RectInt), v => { var x = (RectInt)v; return new float[] { x.x, x.y, x.width, x.height }; } },
            { typeof(RectOffset), v => { var x = (RectOffset)v; return new float[] { x.left, x.right, x.top, x.bottom }; } },
            { typeof(Vector2Int), v => { var x = (Vector2Int)v; return new float[] { x.x, x.y }; } },
            { typeof(Vector3Int), v => { var x = (Vector3Int)v; return new float[] { x.x, x.y, x.z }; } },
            { typeof(Bounds), v => { var x = (Bounds)v; return new[] { x.center.x, x.center.y, x.center.z, x.size.x, x.size.y, x.size.z }; } },
            { typeof(BoundsInt), v => { var x = (BoundsInt)v; return new float[] { x.position.x, x.position.y, x.position.z, x.size.x, x.size.y, x.size.z }; } },
        };

        /// Kiểu có đường parse. **Không** nói giá trị nào cũng biểu diễn được (xem TryToArgument).
        public static bool CanParse(Type type)
        {
            if (type == null) return false;
            if (Parseable.Contains(type) || type.IsEnum) return true;
            if (typeof(Component).IsAssignableFrom(type)) return true;
            var element = ElementTypeOf(type);
            return element != null && (Parseable.Contains(element) || element.IsEnum ||
                                       typeof(Component).IsAssignableFrom(element));
        }

        /// Kiểu được gõ thẳng trên row. Cố ý **hẹp hơn** CanParse: GameObject/Component và
        /// array/List có parser nhưng phải mở ra xem được, không nhét cả object vào một ô text.
        public static bool IsInlineValue(Type type)
        {
            if (type == null) return false;
            if (type.IsEnum) return true;
            return Parseable.Contains(type) && type != typeof(GameObject);
        }

        /// Dạng chữ của một giá trị, đọc được và parse lại được. InvariantCulture vì máy đặt
        /// locale dấu phẩy thập phân thì "0,5" không parse lại được.
        public static string ToText(object value)
        {
            if (value == null) return string.Empty;
            if (value is bool flag) return flag ? "true" : "false";

            var parts = PartsOf(value);
            if (parts != null)
            {
                var texts = new string[parts.Length];
                for (var i = 0; i < parts.Length; i++) texts[i] = parts[i].ToString("R", CultureInfo.InvariantCulture);
                return string.Join(" ", texts);
            }

            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// Một đối số dùng được trong dòng lệnh (nút repeat, console). Khác ToText ở hai chỗ:
        /// từ chối thứ không có biểu diễn độc lập với session, và bọc quote khi có khoảng trắng —
        /// không bọc thì Vector3 "1 2 3" bị FetchArgumentsFromCommand tách thành ba đối số.
        public static bool TryToArgument(object value, Type declared, out string text)
        {
            text = null;
            if (value is Object) return false;                 // reference Unity: tên không phải định danh

            // null reference types round-trip as "null"
            var nullable = !declared.IsValueType;
            if (value == null && nullable)
            {
                text = "null";
                return true;
            }

            if (value != null && !CanParse(value.GetType()) && !CanParse(declared)) return false;
            if (ElementTypeOf(declared) != null && typeof(Object).IsAssignableFrom(ElementTypeOf(declared))) return false;

            var raw = ToText(value);

            // Bọc quote chỉ cứu được khoảng trắng. Chuỗi chứa chính dấu `"`, xuống dòng hay tab thì
            // FetchArgumentsFromCommand không tách lại đúng, mà nó không có escape — từ chối hẳn còn
            // hơn lưu một dòng lệnh chạy ra giá trị khác.
            foreach (var bad in new[] { '"', '\n', '\r', '\t' })
            {
                if (raw.IndexOf(bad) >= 0) return false;
            }

            text = raw.Length == 0 || raw.IndexOf(' ') >= 0 ? $"\"{raw}\"" : raw;
            return true;
        }

        /// Parse một ô nhập. Trả false kèm lý do đọc được — ô nhập tô đỏ bằng chính cái này.
        ///
        /// allowVars = true chỉ ở page nhập tham số, page Gán và Execute. **Không** bật cho ô nhập
        /// tại chỗ của kiểu số: bật là mất bàn phím số trên điện thoại, đổi lấy một khả năng vô
        /// nghĩa (biến chứa số quy về đúng con số đó). Task 16 nối phần resolve `$var` vào đây.
        public static bool TryParse(string text, Type declared, out object value, out string error,
            bool allowVars = false)
        {
            value = null;
            error = null;
            if (declared == null)
            {
                error = "Không biết kiểu của giá trị này.";
                return false;
            }

            if (allowVars && !string.IsNullOrEmpty(text) && text.StartsWith("$"))
            {
                var name = text.Substring(1);
                if (!Vars.TryGet(name, out var bound))
                {
                    error = $"Biến '{name}' chưa được gán.";
                    return false;
                }
                if (bound is Object unityObject && !unityObject)
                {
                    error = $"Biến '{name}' trỏ vào object đã destroy.";
                    return false;
                }
                if (bound != null && !declared.IsInstanceOfType(bound))
                {
                    error = $"Biến '{name}' là {bound.GetType().Name}, không gán được vào {declared.Name}.";
                    return false;
                }
                value = bound;
                return true;
            }

            var nullable = !declared.IsValueType;
            if (nullable && declared != typeof(string) && text == "null") return true;

            if (!DebugLogConsole.ParseArgument(text, declared, out value))
            {
                error = $"'{text}' không phải {DebugLogConsole.GetTypeReadableName(declared)}";
                return false;
            }

            // ParseGameObject/ParseComponent của IDC trả true kể cả khi Find không thấy gì.
            // Im lặng nhận null ở đây là ghi null vào field mà người dùng tưởng đã tìm thấy object.
            if (value == null && typeof(Object).IsAssignableFrom(declared))
            {
                error = $"Không tìm thấy object tên '{text}'. Gõ 'null' nếu thật sự muốn xoá tham chiếu.";
                return false;
            }
            return true;
        }

        /// Giá trị khởi tạo parse được, để mở page nhập liệu ra là bấm Run được luôn.
        /// Tên kiểu đọc được: `SceneScope&lt;HomeSceneRoot&gt;` chứ không phải `SceneScope`1` — dạng
        /// backtick-arity của CLR không nói cho ai biết cái gì.
        public static string TypeName(Type type)
        {
            if (type == null) return string.Empty;
            if (!type.IsGenericType) return type.Name;

            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return $"{name}<{string.Join(", ", Array.ConvertAll(type.GetGenericArguments(), TypeName))}>";
        }

        public static string DefaultValueFor(Type type)
        {
            if (type == null) return string.Empty;
            if (type == typeof(bool)) return "false";
            if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                return names.Length > 0 ? names[0] : "0";
            }
            if (type == typeof(string) || type == typeof(char)) return string.Empty;

            // VectorParts là bảng đủ để trả lời "có nhiều thành phần không" — tra bằng KEY, không
            // cần dựng instance thật để hỏi. Trước đây gọi Activator.CreateInstance(type) trước khi
            // biết type có an toàn để dựng không: với GameObject, cái này thật sự tạo và bỏ rơi một
            // GameObject rỗng vào scene đang chạy; với Transform/Component/interface/abstract type
            // thì ném exception ngay (không có constructor không tham số công khai).
            if (VectorParts.ContainsKey(type))
                return ToText(Activator.CreateInstance(type));

            if (type.IsPrimitive || type == typeof(decimal)) return "0";
            return string.Empty;
        }

        private static float[] PartsOf(object value)
        {
            return value != null && VectorParts.TryGetValue(value.GetType(), out var read) ? read(value) : null;
        }

        private static Type ElementTypeOf(Type type)
        {
            if (type == null) return null;
            if (type.IsArray) return type.GetElementType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                ? type.GetGenericArguments()[0]
                : null;
        }
    }
}
