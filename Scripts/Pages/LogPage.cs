using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Trang log (spec ① §5). Model sống suốt phiên (LogModel.Shared) nên lọc / gộp / xoá giữ nguyên qua
    /// các lần mở. Tìm dùng ô tìm của header như mọi trang khác.
    internal static class LogPage
    {
        internal static DebugPage Build(long focusSeq = 0)
        {
            var model = LogModel.Shared;
            return new DebugPage("Log",
                panel =>
                {
                    model.Query = string.Empty;
                    panel.ShowLog(model, focusSeq);
                    // Chỉ nhảy lần đầu; lùi từ trang chi tiết về thì giữ chỗ đang đọc.
                    focusSeq = 0;
                },
                (panel, query) =>
                {
                    model.Query = query;
                    panel.ShowLog(model, 0);
                },
                more: panel => panel.Push(Actions(model)),
                isLog: true);
        }

        private static DebugPage Actions(LogModel model)
        {
            return new DebugPage("Thao tác", panel =>
            {
                panel.AddAction("Copy tất cả", () =>
                {
                    GUIUtility.systemCopyBuffer = model.CopyAll();
                    panel.ShowResult($"đã copy {LogText.Count(model.EntryRowCount)} log", false);
                    panel.Pop();
                }, "Theo bộ lọc đang bật. Quá dài thì giữ phần mới nhất.");
                panel.AddAction("Xoá", () =>
                {
                    model.Clear();
                    panel.Pop();
                }, "Ẩn mọi log tới lúc này. Bấm dòng đầu danh sách để hiện lại.");
            }, searchable: false);
        }
    }
}
