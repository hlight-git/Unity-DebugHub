using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Kênh báo lỗi của project (spec ④). Class con đặt trên **chính object DebugHub** (object đó DontDestroyOnLoad)
    /// rồi kéo vào ô Reporter. Abstract MonoBehaviour thay vì interface: như DebuggerAuthenticationTrigger, Inspector
    /// mới vẽ được ô kéo-thả.
    ///
    /// Hub lo form và log. Payload, endpoint, token, đăng nhập, thông tin game/máy là việc của class con.
    ///
    /// Thành viên thêm sau này phải là `virtual` có mặc định: thêm `abstract` là vỡ mọi class con của project.
    public abstract class BugReporter : MonoBehaviour
    {
        /// Mọi ô của form theo thứ tự hiện (title, priority, assignee…). Class con giữ giá trị và tự quyết giữ hay xoá
        /// sau khi gửi. Gọi lại mỗi lần trang dựng lại (kể cả lùi về từ trang chọn): rẻ, không side effect.
        public abstract IEnumerable<DebugNode> Fields();

        /// Đọc field của mình trước `await` đầu tiên: QA mở lại được form trong lúc đang gửi. Trả chữ hiện ở dòng kết
        /// quả (mã issue…), null = "Đã gửi báo lỗi.". Ném = lỗi. Phải tự có timeout: hub không cắt.
        public abstract Task<string> Send(BugReport report);
    }

    /// Thứ chỉ hub có cho một lần gửi. sealed: hub không điền được field nó không biết — payload là class của
    /// project. Thêm field sau này = thêm overload constructor, class con cũ không vỡ.
    public sealed class BugReport
    {
        public BugReport(string logs)
        {
            Logs = logs;
        }

        /// Mọi log đang giữ sau mốc Xoá, bỏ qua lọc / tìm / Gộp, chụp lúc bấm Gửi. Định dạng như Copy tất cả.
        public string Logs { get; }
    }
}
