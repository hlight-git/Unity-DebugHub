using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Abstract MonoBehaviour thay vì interface: field kiểu này trên DebugHub cho kéo-thả component
    /// thẳng trong Inspector, interface thì Unity không vẽ được ô kéo-thả.
    public abstract class DebuggerAuthenticationTrigger : MonoBehaviour
    {
        /// True nếu trigger này chỉ có tác dụng khi máy đã xác thực rồi — dùng để gọi lại entry đã
        /// ẩn cho tiện, không phải một cách để mở khoá lần đầu. Mặc định false: trigger nào cũng
        /// dùng được để mở khoá (nhập password) khi chưa xác thực.
        public virtual bool RequiresAlreadyAuthenticated => false;

        /// Một dòng ở trang Trợ giúp: làm cử chỉ này thế nào, đọc từ chính cấu hình đang gắn để không bao giờ ghi sai.
        /// null = không nhắc.
        public virtual string Hint => null;

        public abstract bool IsPerformedTriggerAction();
    }
}
