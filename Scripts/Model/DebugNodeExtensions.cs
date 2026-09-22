namespace Hlight.Debug.Hub
{
    /// Hàm nối giữ đúng tên cũ của package. Generic để `Node.Value(...).Confirms()` vẫn
    /// trả về ValueNode chứ không tụt về DebugNode.
    public static class DebugNodeExtensions
    {
        /// Node đọc/ghi dữ liệu: ở lại page để tra tiếp.
        public static T Stays<T>(this T node) where T : DebugNode
        {
            node.Dismiss = DismissMode.Stay;
            return node;
        }

        /// Muốn xem kết quả thì hub phải biến mất (ẩn UI game, mở debugger SDK, chụp ảnh).
        public static T HidesHub<T>(this T node) where T : DebugNode
        {
            node.Dismiss = DismissMode.HideHub;
            return node;
        }

        public static T Confirms<T>(this T node) where T : DebugNode
        {
            node.Confirm = true;
            return node;
        }

        /// Luôn hiện dòng kết quả, kể cả khi node đóng panel sau khi chạy.
        public static T Reports<T>(this T node) where T : DebugNode
        {
            node.showsResult = true;
            return node;
        }

        /// Không bao giờ hiện dòng kết quả, kể cả khi node in log.
        public static T Silent<T>(this T node) where T : DebugNode
        {
            node.showsResult = false;
            return node;
        }

        /// Giá trị mặc định của page nhập liệu. Chỉ seed khi chưa có gì — không ghi đè cái người
        /// dùng vừa gõ, kể cả khi node được đăng ký lại.
        public static T Defaults<T>(this T node, params string[] args) where T : DebugNode
        {
            if (!DebugRegistry.HasArgs(node)) DebugRegistry.StoreArgs(node, args);
            return node;
        }
    }
}
