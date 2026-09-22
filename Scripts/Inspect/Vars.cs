using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Biến tạm gán bằng `$tên`, dùng làm root hoặc tham số trong address.
    ///
    /// Stub tối thiểu cho Task 15 — chỉ đủ để Address.cs biên dịch và test qua. Task 16 xây UI +
    /// phần còn lại (liệt kê, xoá theo scope, …) trên chính API Bind/TryGet/Remove này.
    internal static class Vars
    {
        private static readonly Dictionary<string, object> values = new();

        public static void Bind(string name, object value) => values[name] = value;
        public static bool TryGet(string name, out object value) => values.TryGetValue(name, out value);
        public static void Remove(string name) => values.Remove(name);
    }
}
