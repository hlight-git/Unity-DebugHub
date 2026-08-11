using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlight.Debug.Hub
{
    /// Coi máy như đã xác thực nếu reachable tới bất kỳ một địa chỉ nội bộ nào trong checkUrls
    /// (nhiều target dùng cho các server dự phòng của cùng mạng công ty). checkUrls để trống trong
    /// code, điền URL/IP thật qua Inspector.
    [Serializable]
    public class CompanyNetworkAuthenticationBypass
    {
        [SerializeField] private string[] checkUrls;
        [SerializeField] private int timeoutSeconds = 3;

        public IEnumerator Check(Action<bool> onResult)
        {
            if (checkUrls == null || checkUrls.Length == 0)
            {
                onResult(false);
                yield break;
            }

            var requests = new UnityWebRequest[checkUrls.Length];
            var ops = new UnityWebRequestAsyncOperation[checkUrls.Length];
            for (var i = 0; i < checkUrls.Length; i++)
            {
                requests[i] = UnityWebRequest.Head(checkUrls[i]);
                requests[i].timeout = timeoutSeconds;
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
