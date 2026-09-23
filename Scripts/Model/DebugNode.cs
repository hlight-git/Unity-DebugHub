using System;
using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Làm gì với hub sau khi command chạy xong.
    public enum DismissMode
    {
        /// Giữ nguyên panel — cho row bật/tắt liên tiếp.
        Stay,

        /// Đóng panel, entry vẫn còn.
        ClosePanel,

        /// Ẩn cả entry để nhìn game không bị hub che. Gọi lại bằng trigger (lắc/gõ góc); panel mở lại
        /// đúng page đang xem vì DebugHubPanel.Close() giữ stack.
        HideHub,
    }

    /// Một tham số của command.
    public sealed class DebugParameter
    {
        public readonly string Name;
        public readonly Type Type;

        public DebugParameter(string name, Type type)
        {
            Name = name;
            Type = type;
        }
    }

    /// Kiểu chữ của TextNode. Table = monospace + tắt wrap, để cột không lệch.
    public enum TextStyle { Normal, Note, Good, Warn, Bad, Table }

    /// Một dòng hiện được trên panel. Bốn loại con, và NodeRenderer là nơi duy nhất
    /// biết loại nào ra row nào.
    public abstract class DebugNode
    {
        public string Label;
        public string Description;

        /// Id ổn định để nhớ state (tham số đã nhập). Registry cấp cho node đăng ký,
        /// Reflect cấp cho node reflection. Khác Address (địa chỉ resolve lại được).
        public string Key { get; internal set; }

        /// Chỉ ValueNode/ActionNode dùng. Để ở base vì thêm một tầng abstract chỉ để chứa
        /// ba field thì đắt hơn ba field không dùng trên hai loại kia.
        public DismissMode Dismiss;
        public bool Confirm;
        internal bool? showsResult;

        /// Có hiện dòng kết quả sau khi chạy hay không. Mặc định suy từ Dismiss vì đó là cùng
        /// một câu chuyện: panel ở lại = đang đọc dữ liệu nên cần thấy output.
        public bool ShowsResult => showsResult ?? Dismiss == DismissMode.Stay;
    }

    /// Một giá trị đọc được; Set != null thì thay được **chính giá trị này**
    /// (không liên quan tới việc mở vào trong nó).
    public sealed class ValueNode : DebugNode
    {
        public Type Declared;
        public Func<object> Get;
        public Action<object> Set;

        /// Địa chỉ resolve lại được, cho Watch (Task 17). null = không watch được.
        public string Address;
    }

    /// Một việc chạy được.
    public sealed class ActionNode : DebugNode
    {
        public DebugParameter[] Parameters = Array.Empty<DebugParameter>();
        public Action<object[]> Invoke;

        /// Kiểu trả về await được — ParamsPage hiện switch `Chờ kết quả`.
        public bool Awaitable;
    }

    /// Có con. Con liệt kê lúc mở (hoặc lúc dựng inline), không giữ sẵn: object có thể chết,
    /// list có thể đổi.
    public sealed class FolderNode : DebugNode
    {
        public Func<IEnumerable<DebugNode>> Children;

        /// Dựng thẳng trong trang cha dưới một dòng tiêu đề, không phải row nav.
        public bool Inline;

        /// Trang tự dựng lại 4 lần/giây.
        public bool Live;
    }

    /// Chữ: mô tả, ghi chú, bảng đã format.
    public sealed class TextNode : DebugNode
    {
        public string Text;
        public TextStyle Style;
    }
}
