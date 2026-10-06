using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlight.Debug.Hub
{
    /// Hai kênh gửi của project (spec ④): `hub.report`, `hub.message`, và Log › … › Gửi qua message. DebugHub gắn
    /// lúc Awake; test gắn thẳng. Form = Fields() của class con + row Gửi, dựng bằng NodeRenderer như mọi folder.
    internal static class Sending
    {
        private static BugReporter reporter;
        private static MessageSender messenger;
        private static SendFlow reportFlow;
        private static SendFlow messageFlow;

        internal static bool CanMessage => messenger;

        /// Static giữ nguyên giữa các lần Play khi tắt domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            reporter = null;
            messenger = null;
            reportFlow = null;
            messageFlow = null;
        }

        /// Ô trống = kênh đó không có row (spec ④ §2). Owner của row là chính component: đặt nhầm lên object bị unload thì
        /// row rụng theo, không còn row bấm vào là chạy trên component đã chết.
        internal static void Initialize(BugReporter reporter, MessageSender messenger, Action<string, bool> show)
        {
            Sending.reporter = reporter;
            Sending.messenger = messenger;
            reportFlow = new SendFlow("Đã gửi báo lỗi.", show);
            messageFlow = new SendFlow("Đã gửi.", show);

            if (reporter) DebugHub.AddFolder(reporter, "hub.report", "Báo lỗi lên nền tảng issue của project.", ReportNodes);
            if (messenger) DebugHub.AddFolder(messenger, "hub.message", "Gửi message qua API của project.", () => MessageNodes(null, 0));
        }

        /// Log chụp trong lambda của row Gửi, tức lúc bấm, không phải lúc mở form.
        internal static IEnumerable<DebugNode> ReportNodes()
        {
            foreach (var node in reporter.Fields()) yield return node;
            yield return SendNode(() => reportFlow.Start(() => reporter.Send(new BugReport(LogModel.Shared.AllText()))));
        }

        /// <paramref name="sent"/>: chạy khi lần gửi đã đi (không phải khi kênh đang bận, cũng không khi Send hỏng ngay).
        internal static IEnumerable<DebugNode> MessageNodes(string logs, int count, Action sent = null)
        {
            if (logs != null) yield return Node.Text($"Kèm {LogText.Count(count)} log (theo bộ lọc).", TextStyle.Note);
            foreach (var node in messenger.Fields()) yield return node;
            yield return SendNode(() => messageFlow.Start(() => messenger.Send(new DebugMessage(logs))), sent);
        }

        /// Log theo bộ lọc chụp lúc mở form: đúng thứ QA đang thấy, log tới sau không kèm. Gửi đi rồi thì bỏ form khỏi
        /// stack: panel đóng vẫn giữ stack, để lại thì mở hub lần sau là về form cũ và Gửi là gửi lại bộ log cũ.
        internal static DebugPage ForLogs(LogModel model)
        {
            var logs = model.CopyAll(out var count);
            return new DebugPage("message", panel =>
            {
                foreach (var node in MessageNodes(logs, count, panel.Pop))
                    NodeRenderer.Render(panel, node, (n, values) => NodeRenderer.RunInspect(panel, n, values));
            }, searchable: false);
        }

        /// Reports(): Node.Action mặc định không hiện dòng kết quả, mà "Đang gửi…" phải hiện sau khi panel đóng.
        private static ActionNode SendNode(Func<bool> send, Action sent = null)
        {
            return Node.Action("Gửi", () =>
            {
                // AddField chỉ ghi giá trị lúc onEndEdit: gõ xong bấm Gửi ngay thì bỏ chọn ô trước để giá trị cuối vào
                // class con.
                var events = EventSystem.current;
                if (events) events.SetSelectedGameObject(null);
                if (send()) sent?.Invoke();
            }).Reports();
        }
    }
}
