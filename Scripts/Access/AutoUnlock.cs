using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlight.Debug.Hub
{
    /// Tự mở khoá ở công ty (spec ① §3.4): ô password đang mở mà tới được trang nội bộ, và trang trả đúng chuỗi
    /// biết trước = coi như đã gõ đúng password.
    ///
    /// IP nội bộ không phải danh tính (mạng nào cũng tự đặt được 10.x), nên trang phải trả đúng một chuỗi. Chỉ
    /// kiểm khi ô password đang mở: người chơi không làm cử chỉ bí mật nên không bao giờ gửi request, và iOS
    /// không hỏi quyền mạng cục bộ họ.
    ///
    /// Trang chứ không IP công khai: server là của công ty, còn IP nhà mạng có thể đổi giữa hai lần phát hành.
    /// Giả được trang thì cũng đọc được password nằm cạnh nó trong cùng component, nên IP không an toàn hơn.
    [Serializable]
    public class AutoUnlock
    {
        /// Server trong LAN trả lời trong vài chục ms; quá chừng này coi như không ở công ty.
        private const int TIMEOUT_SECONDS = 2;

        [Serializable]
        public struct Page
        {
            [Tooltip("Trang chỉ mở được trong mạng công ty, ví dụ http://10.10.0.204/. http:// cần Player Settings → Allow downloads over HTTP = Always.")]
            public string url;

            [Tooltip("Chuỗi bắt buộc có trong trang, ví dụ <title>Zego Dashboard</title>. Dùng tiêu đề, đừng dùng ETag/hash: hai thứ đó đổi mỗi lần deploy.")]
            public string mustContain;
        }

        /// Nhiều trang để dự phòng: một server đổi IP hay sập giữa hai lần phát hành thì trang khác vẫn mở khoá.
        [Tooltip("Khớp một trang là đủ. Thêm trang của server khác để dự phòng.")]
        [SerializeField] private Page[] pages = Array.Empty<Page>();

        internal bool Configured
        {
            get
            {
                foreach (var page in pages)
                {
                    if (Usable(page)) return true;
                }
                return false;
            }
        }

        private static bool Usable(Page page) => !string.IsNullOrWhiteSpace(page.url) && !string.IsNullOrEmpty(page.mustContain);

        /// Gửi mọi trang song song; trang đầu tiên khớp thì gọi onMatch đúng một lần rồi dừng.
        public IEnumerator Check(Action onMatch)
        {
            var pending = new List<(UnityWebRequest Request, string MustContain)>();
            foreach (var page in pages)
            {
                if (Usable(page)) pending.Add((Send(page.url), page.mustContain));
            }

            try
            {
                while (pending.Count > 0)
                {
                    for (var i = pending.Count - 1; i >= 0; i--)
                    {
                        var (request, mustContain) = pending[i];
                        if (!request.isDone) continue;
                        pending.RemoveAt(i);

                        var body = request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null;
                        var matched = PageMatches(request.result, request.responseCode, body, mustContain);
                        request.Dispose();
                        if (!matched) continue;
                        onMatch();
                        yield break;
                    }
                    yield return null;
                }
            }
            finally
            {
                foreach (var (request, _) in pending) request.Dispose();
            }
        }

        private static UnityWebRequest Send(string url)
        {
            var request = UnityWebRequest.Get(url);
            // Redirect sang trang đăng nhập Wi-Fi không được tính là "tới được".
            request.redirectLimit = 0;
            request.timeout = TIMEOUT_SECONDS;
            request.SendWebRequest();
            return request;
        }

        internal static bool PageMatches(UnityWebRequest.Result result, long code, string body, string mustContain) =>
            result == UnityWebRequest.Result.Success && code == 200 && !string.IsNullOrEmpty(mustContain) &&
            body != null && body.Contains(mustContain);
    }
}
