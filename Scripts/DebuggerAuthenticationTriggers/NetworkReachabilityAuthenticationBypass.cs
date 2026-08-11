using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlight.Debug.Hub
{
    /// Coi máy như đã xác thực nếu reachable tới bất kỳ một địa chỉ nào trong checkUrls (nhiều
    /// target dùng cho các endpoint dự phòng của cùng một mạng). checkUrls để trống trong code,
    /// điền URL/IP thật qua Inspector tuỳ nhu cầu (mạng nội bộ công ty, VPN, hay bất kỳ mạng nào
    /// khác muốn coi là đã tin cậy).
    [Serializable]
    public class NetworkReachabilityAuthenticationBypass
    {
        [SerializeField] private string[] checkUrls;
        [SerializeField] private int timeoutSeconds = 3;

        public IEnumerator Check(Action<bool> onResult)
        {
            var urls = checkUrls?.Where(url => !string.IsNullOrWhiteSpace(url)).ToArray() ?? Array.Empty<string>();
            if (urls.Length == 0)
            {
                onResult(false);
                yield break;
            }

            var timeout = Mathf.Max(1, timeoutSeconds);
            var requests = new UnityWebRequest[urls.Length];
            var ops = new UnityWebRequestAsyncOperation[urls.Length];
            for (var i = 0; i < urls.Length; i++)
            {
                requests[i] = UnityWebRequest.Head(urls[i]);
                requests[i].timeout = timeout;
                ops[i] = requests[i].SendWebRequest();
            }

            // Poll thay vì yield từng request tuần tự: một target chậm/timeout không được chặn
            // các target khác, chỉ cần một cái xong sớm và thành công là dừng ngay.
            bool reachable;
            while (true)
            {
                reachable = false;
                var allDone = true;
                foreach (var op in ops)
                {
                    if (!op.isDone) { allDone = false; continue; }
                    if (op.webRequest.result == UnityWebRequest.Result.Success) reachable = true;
                }
                if (reachable || allDone) break;
                yield return null;
            }

            foreach (var request in requests) request.Dispose();
            onResult(reachable);
        }
    }
}
