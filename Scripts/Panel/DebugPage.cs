using System;

namespace Hlight.Debug.Hub
{
    /// Một page không phải object: chỉ là tiêu đề + cách dựng nội dung vào panel.
    public readonly struct DebugPage
    {
        public readonly string Title;
        public readonly Action<DebugHubPanel> Build;

        /// Lọc nội dung theo từ khoá. null = panel tìm toàn registry thay cho page này.
        public readonly Action<DebugHubPanel, string> Search;

        /// false thì header ẩn nút Tìm — page nhập tham số và page xác nhận không có gì để tìm.
        public readonly bool Searchable;

        /// Tự dựng lại 4 lần/giây (bỏ nhịp khi đang gõ).
        public readonly bool Live;

        /// Dòng nhỏ dưới tiêu đề — address của trang inspect. Drill 4 tầng thì tiêu đề chỉ còn tên lá,
        /// dòng này nói mình đang ở đâu; bấm vào là copy.
        public readonly string Subtitle;

        /// Trang gốc của nhánh Advanced. Panel ẩn nút `Adv` khi trang này nằm **bất kỳ đâu** trong
        /// stack — trang member/method/tham số mở từ trong Advanced dùng chung với phía Commands nên
        /// không gắn cờ lên chúng được.
        public readonly bool AdvancedRoot;

        public DebugPage(string title, Action<DebugHubPanel> build,
            Action<DebugHubPanel, string> search = null, bool searchable = true, bool live = false,
            string subtitle = null, bool advancedRoot = false)
        {
            Title = title;
            Build = build;
            Search = search;
            Searchable = searchable;
            Live = live;
            Subtitle = subtitle;
            AdvancedRoot = advancedRoot;
        }
    }
}
