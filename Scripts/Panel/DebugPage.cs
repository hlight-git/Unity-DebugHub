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

        public DebugPage(string title, Action<DebugHubPanel> build,
            Action<DebugHubPanel, string> search = null, bool searchable = true, bool live = false)
        {
            Title = title;
            Build = build;
            Search = search;
            Searchable = searchable;
            Live = live;
        }
    }
}
