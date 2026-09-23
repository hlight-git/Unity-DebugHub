using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Tính gợi ý ở thread nền để gõ không chẹn UI.
    ///
    /// Dùng được vì việc của nó là **thuần .NET**: lọc tên assembly, tên type, tên member — không
    /// chạm một API Unity nào. Thứ chạm Unity (đọc giá trị, dựng row) vẫn ở main thread.
    ///
    /// Trang gọi `Request(query)` mỗi lần dựng (trang bộ chọn là Live nên 4 lần/giây); lặp lại cùng
    /// một query thì không chạy lại. Query mới thay chỗ query cũ — kết quả về muộn của cái cũ bị bỏ.
    internal sealed class Suggester<T>
    {
        private static readonly IReadOnlyList<T> Empty = Array.Empty<T>();

        private readonly Func<string, IReadOnlyList<T>> work;
        private readonly float debounce;

        private string requested;           // query mới nhất người dùng gõ
        private volatile string running;    // query đang chạy — worker đọc để biết mình còn được nhận không
        private float dueAt;
        private volatile bool working;

        private IReadOnlyList<T> results = Empty;
        private string resultsFor;

        public Suggester(Func<string, IReadOnlyList<T>> work, float debounceSeconds = 0.15f)
        {
            this.work = work;
            debounce = debounceSeconds;
        }

        public bool Working => working;
        public IReadOnlyList<T> Results => results;
        public string ResultsFor => resultsFor;

        public void Request(string query)
        {
            query ??= string.Empty;
            if (query == resultsFor && !working) return;     // đã có kết quả cho đúng query này
            if (query == running && working) return;         // đang chạy đúng query này

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
            working = true;
            Task.Run(() =>
            {
                IReadOnlyList<T> computed;
                try { computed = work(query) ?? Empty; }
                catch (Exception) { computed = Empty; }      // worker ném thì trang vẫn dựng được

                // Chỉ nhận nếu vẫn là query đang chạy: bản chậm về sau không được ghi đè bản mới.
                //
                // Thứ tự ba dòng này là hợp đồng với main thread: `working` là volatile và được ghi
                // **cuối cùng**, nên lúc main thread thấy working == false thì results/resultsFor đã
                // nằm đó. Đảo thứ tự là main thread đọc được cặp nửa cũ nửa mới.
                if (running == query)
                {
                    results = computed;
                    resultsFor = query;
                    working = false;
                }
            });
        }

        /// Time.realtimeSinceStartup gọi được cả trong EditMode test; Time.unscaledTime thì không
        /// nhích khi không có player loop.
        private static float Now => Time.realtimeSinceStartup;
    }
}
