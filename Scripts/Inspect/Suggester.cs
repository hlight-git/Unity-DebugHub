using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Tính gợi ý ở thread nền để gõ không chẹn UI.
    ///
    /// Dùng được vì việc của nó là **thuần .NET**: lọc tên type, tên member — không chạm một API
    /// Unity nào. Thứ chạm Unity (đọc giá trị, dựng row) vẫn ở main thread.
    ///
    /// Trang gọi `Request(query)` mỗi lần dựng; lặp lại cùng một query thì không chạy lại. Query mới
    /// thay chỗ query cũ — kết quả về muộn của cái cũ bị bỏ.
    internal sealed class Suggester<T>
    {
        private static readonly IReadOnlyList<T> Empty = Array.Empty<T>();

        /// Ba thứ main thread cần phải khớp nhau: kết quả, query của nó, và còn đang chạy hay không.
        /// Là **class** chứ không phải struct để `Volatile.Read/Write` gán được nguyên khối một tham
        /// chiếu — ba field rời thì main thread đọc được cặp "query mới, kết quả cũ".
        internal sealed class Snapshot
        {
            public readonly string Query;
            public readonly IReadOnlyList<T> Items;
            public readonly bool Working;

            public Snapshot(string query, IReadOnlyList<T> items, bool working)
            {
                Query = query;
                Items = items;
                Working = working;
            }
        }

        private readonly Func<string, IReadOnlyList<T>> work;
        private readonly float debounce;

        private string requested;           // query mới nhất người dùng gõ
        private volatile string running;    // query đang chạy — worker đọc để biết mình còn được nhận không
        private float dueAt;
        private Snapshot state = new(null, Empty, false);

        public Suggester(Func<string, IReadOnlyList<T>> work, float debounceSeconds = 0.15f)
        {
            this.work = work;
            debounce = debounceSeconds;
        }

        /// Một lần đọc ra cả ba — không còn cửa cho cặp "query mới, kết quả cũ".
        public Snapshot Current => Volatile.Read(ref state);

        public bool Working => Current.Working;
        public IReadOnlyList<T> Results => Current.Items;
        public string ResultsFor => Current.Query;

        public void Request(string query)
        {
            query ??= string.Empty;
            var now = Current;
            if (query == now.Query && !now.Working) return;     // đã có kết quả cho đúng query này
            if (query == running && now.Working) return;        // đang chạy đúng query này

            if (query != requested)
            {
                requested = query;
                dueAt = Now + debounce;
            }
            if (Now < dueAt) return;

            Start(query);
        }

        private void Start(string query)
        {
            running = query;
            // Giữ kết quả cũ hiện tiếp trong lúc chờ (cố ý — gõ thêm một chữ không làm list chớp trắng),
            // chỉ bật cờ Working.
            var now = Current;
            Volatile.Write(ref state, new Snapshot(now.Query, now.Items, true));

            Task.Run(() =>
            {
                IReadOnlyList<T> computed;
                try { computed = work(query) ?? Empty; }
                catch (Exception) { computed = Empty; }      // worker ném thì trang vẫn dựng được

                // Chỉ nhận nếu vẫn là query đang chạy: bản chậm về sau không được ghi đè bản mới.
                // Đúng **một** lệnh ghi — main thread thấy trọn bản mới hoặc trọn bản cũ.
                if (running == query) Volatile.Write(ref state, new Snapshot(query, computed, false));
            });
        }

        /// Time.realtimeSinceStartup gọi được cả trong EditMode test; Time.unscaledTime thì không
        /// nhích khi không có player loop.
        private static float Now => Time.realtimeSinceStartup;
    }
}
