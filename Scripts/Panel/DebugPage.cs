using System;

namespace Hlight.Debug.Hub
{
    /// Một page không phải object: chỉ là tiêu đề + cách dựng nội dung vào panel.
    internal readonly struct DebugPage
    {
        public readonly string Title;
        public readonly Action<DebugHubPanel> Build;

        /// Lọc nội dung của chính trang này. null = không có tìm kiếm.
        public readonly Action<DebugHubPanel, string> Search;

        /// false thì header ẩn nút Tìm — page nhập tham số và page xác nhận không có gì để tìm.
        public readonly bool Searchable;

        /// Tự dựng lại 4 lần/giây (bỏ nhịp khi đang gõ).
        public readonly bool Live;

        /// Dòng nhỏ dưới tiêu đề — address của trang inspect. Drill 4 tầng thì tiêu đề chỉ còn tên lá,
        /// dòng này nói mình đang ở đâu; bấm vào là copy.
        public readonly string Subtitle;

        /// Hiện nút công cụ (Advanced), `?` và Log ở header — chỉ Commands gốc.
        public readonly bool ShowTools;

        /// Nút `…` ở header mở thao tác của cả trang. null = ẩn.
        public readonly Action<DebugHubPanel> More;

        /// Trang log: panel cao tối đa cố định (không co theo nội dung), chừa chỗ thanh lọc dưới header.
        public readonly bool IsLog;

        /// Window luôn cao tối đa thay vì co theo nội dung: trang có footer ghim đáy (chi tiết log) thì footer
        /// mới đứng yên khi chuyển trang. Trang log tự tính là cố định.
        public readonly bool FixedHeight;

        public DebugPage(string title, Action<DebugHubPanel> build,
            Action<DebugHubPanel, string> search = null, bool searchable = true, bool live = false,
            string subtitle = null, bool showTools = false, Action<DebugHubPanel> more = null, bool isLog = false,
            bool fixedHeight = false)
        {
            Title = title;
            Build = build;
            Search = search;
            Searchable = searchable && search != null;
            Live = live;
            Subtitle = subtitle;
            ShowTools = showTools;
            More = more;
            IsLog = isLog;
            FixedHeight = fixedHeight || isLog;
        }
    }
}
