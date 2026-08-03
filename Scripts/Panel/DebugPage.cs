using System;

namespace Hlight.Debug.Hub
{
    /// Một page không phải object: chỉ là tiêu đề + cách dựng nội dung vào panel.
    public readonly struct DebugPage
    {
        public readonly string Title;
        public readonly Action<DebugHubPanel> Build;

        public DebugPage(string title, Action<DebugHubPanel> build)
        {
            Title = title;
            Build = build;
        }
    }
}
