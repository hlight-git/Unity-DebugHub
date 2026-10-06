using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Kênh message của project (spec ④): chữ, đẩy log, không file. Gắn như BugReporter, vào ô Messenger.
    ///
    /// Thành viên thêm sau này phải là `virtual` có mặc định: thêm `abstract` là vỡ mọi class con của project.
    public abstract class MessageSender : MonoBehaviour
    {
        /// Mọi ô của form (text, người nhận…). Luật như BugReporter.Fields.
        public abstract IEnumerable<DebugNode> Fields();

        /// Luật như BugReporter.Send; null = "Đã gửi.".
        public abstract Task<string> Send(DebugMessage message);
    }

    /// Thứ chỉ hub có cho một lần gửi message. Luật như BugReport.
    public sealed class DebugMessage
    {
        public DebugMessage(string logs)
        {
            Logs = logs;
        }

        /// Log theo bộ lọc khi đẩy từ trang Log (như Copy tất cả, chụp lúc mở form); null khi gõ tay từ hub.message.
        public string Logs { get; }
    }
}
