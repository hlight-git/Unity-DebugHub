using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    /// Spec ④ §2–3: hai kênh gửi của project, form và log hub đưa.
    public class SendingTests
    {
        private class FakeReporter : BugReporter
        {
            public string Title = "";
            public BugReport Last;

            public override IEnumerable<DebugNode> Fields()
            {
                yield return Node.Value("title", () => Title, v => Title = v);
            }

            public override Task<string> Send(BugReport report)
            {
                Last = report;
                return Task.FromResult("BUG-1");
            }
        }

        private class FakeMessenger : MessageSender
        {
            public DebugMessage Last;

            public override IEnumerable<DebugNode> Fields()
            {
                yield return Node.Value<string>("to", () => "qa", null);
            }

            public override Task<string> Send(DebugMessage message)
            {
                Last = message;
                return Task.FromResult<string>(null);
            }
        }

        private GameObject owner;
        private FakeReporter reporter;
        private FakeMessenger messenger;
        private DebugHubPanel panel;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            owner = new GameObject("sending test");
            reporter = owner.AddComponent<FakeReporter>();
            messenger = owner.AddComponent<FakeMessenger>();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Sending.Reset();
            // Node đăng ký với owner này: owner chết là node rụng khỏi registry.
            Object.DestroyImmediate(owner);
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
        }

        private static void Show(string text, bool error) { }

        /// All đã lọc entry có owner chết: còn trong đây là row còn hiện.
        private static bool Registered(string path) => DebugRegistry.All.Any(entry => entry.Path == path);

        private static bool Tap(DebugNode send, out string message) =>
            DebugRegistry.Run(send, Array.Empty<string>(), out message);

        [Test]
        public void OnlyAssignedChannels_GetARow()
        {
            Sending.Initialize(reporter, null, Show);
            Assert.IsTrue(Registered("hub.report"));
            Assert.IsFalse(Registered("hub.message"));
            Assert.IsFalse(Sending.CanMessage);
        }

        [Test]
        public void MessengerAlone_GetsItsRow()
        {
            Sending.Initialize(null, messenger, Show);
            Assert.IsFalse(Registered("hub.report"));
            Assert.IsTrue(Registered("hub.message"));
            Assert.IsTrue(Sending.CanMessage);
        }

        /// Component đặt nhầm lên object bị unload: row rụng theo, không còn row chạy trên component đã chết.
        [Test]
        public void Rows_GoAway_WithTheirComponent()
        {
            Sending.Initialize(reporter, messenger, Show);

            Object.DestroyImmediate(reporter);
            Object.DestroyImmediate(messenger);

            Assert.IsFalse(Registered("hub.report"));
            Assert.IsFalse(Registered("hub.message"));
            Assert.IsFalse(Sending.CanMessage);
        }

        /// Form = Fields() của class con rồi tới Gửi; log chụp lúc bấm Gửi, không phải lúc mở form.
        [Test]
        public void ReportForm_IsFieldsThenSend_AndCarriesLogsAtTap()
        {
            Sending.Initialize(reporter, null, Show);
            LogRecorder.Receive("trước", null, LogType.Log);
            var nodes = Sending.ReportNodes().ToList();
            LogRecorder.Receive("sau", null, LogType.Log);

            Assert.AreEqual("title", nodes[0].Label);
            Assert.AreEqual("Gửi", nodes[nodes.Count - 1].Label);
            Assert.IsTrue(Tap(nodes[nodes.Count - 1], out var message));
            Assert.AreEqual("BUG-1", message);
            StringAssert.Contains("sau", reporter.Last.Logs);
        }

        [Test]
        public void MessageFromCommands_HasNoLogs()
        {
            Sending.Initialize(null, messenger, Show);
            var nodes = Sending.MessageNodes(null, 0).ToList();

            Assert.AreEqual("to", nodes[0].Label);
            Assert.IsTrue(Tap(nodes[nodes.Count - 1], out var message));
            Assert.AreEqual("Đã gửi.", message);
            Assert.IsNull(messenger.Last.Logs);
        }

        [Test]
        public void MessageWithLogs_SaysHowMany()
        {
            Sending.Initialize(null, messenger, Show);
            var first = Sending.MessageNodes("x", 1).First();

            Assert.IsInstanceOf<TextNode>(first);
            StringAssert.Contains("Kèm 1 log (theo bộ lọc).", ((TextNode)first).Text);
        }

        /// Đúng thứ QA đang thấy lúc mở form: theo bộ lọc, log tới sau không kèm.
        [Test]
        public void MessageFromLogPage_CarriesFilteredLogs_CapturedAtOpen()
        {
            Sending.Initialize(null, messenger, Show);
            LogRecorder.Receive("giữ", null, LogType.Log);
            LogRecorder.Receive("cảnh-báo", null, LogType.Warning);
            LogModel.Shared.Pull();
            LogModel.Shared.Toggle(LogGroup.Warning);

            var page = Sending.ForLogs(LogModel.Shared);
            LogRecorder.Receive("tới-sau", null, LogType.Log);
            LogModel.Shared.Pull();
            panel.ShowFromRoot(page);
            TestPanel.ClickRowContaining(panel, "Gửi");

            StringAssert.Contains("giữ", messenger.Last.Logs);
            StringAssert.DoesNotContain("cảnh-báo", messenger.Last.Logs);
            StringAssert.DoesNotContain("tới-sau", messenger.Last.Logs);
        }

        /// Panel đóng vẫn giữ stack: form còn nằm đó thì mở hub lần sau là về form cũ, Gửi là gửi lại bộ log cũ.
        [Test]
        public void MessageFromLogPage_LeavesTheStack_OnceSent()
        {
            Sending.Initialize(null, messenger, Show);
            LogRecorder.Receive("x", null, LogType.Log);
            LogModel.Shared.Pull();
            panel.ShowFromRoot(new DebugPage("root", p => p.AddText("root")));
            panel.Push(Sending.ForLogs(LogModel.Shared));

            TestPanel.ClickRowContaining(panel, "Gửi");

            Assert.AreEqual(1, panel.StackDepth);
        }

        private List<string> LogActions()
        {
            panel.ShowFromRoot(new DebugPage("root", p => p.AddText("root")));
            LogPage.Build().More(panel);
            return TestPanel.LabelsOf(panel);
        }

        [Test]
        public void LogPageAction_NeedsAMessenger()
        {
            Sending.Initialize(reporter, null, Show);
            LogRecorder.Receive("x", null, LogType.Log);
            LogModel.Shared.Pull();
            Assert.IsFalse(LogActions().Any(label => label.StartsWith("Gửi qua message")));
        }

        [Test]
        public void LogPageAction_NeedsAtLeastOneLog()
        {
            Sending.Initialize(null, messenger, Show);
            Assert.IsFalse(LogActions().Any(label => label.StartsWith("Gửi qua message")));

            LogRecorder.Receive("x", null, LogType.Log);
            LogModel.Shared.Pull();
            Assert.IsTrue(LogActions().Any(label => label.StartsWith("Gửi qua message")));
        }

        /// Row Gửi chạy qua RunInspect (node không đăng ký): không thành lệnh của nút repeat.
        [Test]
        public void Sending_IsNotRecordedForRepeat()
        {
            var backup = DebugRegistry.LastCommand;
            DebugRegistry.ClearLastCommand();
            try
            {
                Sending.Initialize(reporter, null, Show);
                var nodes = Sending.ReportNodes().ToList();
                panel.ShowFromRoot(new DebugPage("form", p =>
                {
                    foreach (var node in nodes) NodeRenderer.Render(p, node, (n, values) => NodeRenderer.RunInspect(p, n, values));
                }));
                TestPanel.ClickRowContaining(panel, "Gửi");

                Assert.AreEqual(string.Empty, DebugRegistry.LastCommand);
            }
            finally
            {
                if (backup.Length > 0) PlayerPrefs.SetString("DebugHub.LastCommand", backup);
            }
        }
    }
}
