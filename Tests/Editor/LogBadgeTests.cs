using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogBadgeTests
    {
        private DebugHubPanel panel;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
        }

        [Test]
        public void EntryBadge_ShowsCount_CapsAt99_HidesAtZero()
        {
            var entry = panel.transform.root.GetComponentInChildren<DebugHubEntry>(true);
            var badge = entry.transform.Find("Badge").gameObject;
            var count = badge.transform.Find("Count").GetComponent<TMP_Text>();

            entry.Badge = 3;
            Assert.IsTrue(badge.activeSelf);
            Assert.AreEqual("3", count.text);
            entry.Badge = 120;
            Assert.AreEqual("99+", count.text);
            entry.Badge = 0;
            Assert.IsFalse(badge.activeSelf);
        }

        /// Máy không ghi log (người chơi): DebugHub.Update không đếm lỗi mỗi frame và chấm đỏ của bong bóng ẩn.
        [Test]
        public void HubUpdate_HidesTheEntryBadge_WhileNotRecording()
        {
            var root = panel.transform.root;
            var hub = root.GetComponentInChildren<DebugHub>(true);
            var badge = root.GetComponentInChildren<DebugHubEntry>(true).transform.Find("Badge").gameObject;
            var update = typeof(DebugHub).GetMethod("Update",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            LogRecorder.Receive("e", null, LogType.Error);

            update.Invoke(hub, null);
            Assert.IsFalse(badge.activeSelf);

            LogRecorder.Start();
            update.Invoke(hub, null);
            Assert.IsTrue(badge.activeSelf);
        }

        /// Mở trang log là "đã xem": quay về gốc thì chấm đỏ trên nút Log tắt.
        [Test]
        public void LogButtonBadge_ClearsOnceTheLogPageWasSeen()
        {
            LogRecorder.Receive("e1", null, LogType.Error);
            LogRecorder.Receive("e2", null, LogType.Error);
            var badge = ((TMP_Text)TestPanel.Field(panel, "logBadge")).transform.parent.gameObject;

            panel.Show(CommandsPage.Root());
            Assert.IsTrue(badge.activeSelf);
            Assert.AreEqual("2", ((TMP_Text)TestPanel.Field(panel, "logBadge")).text);

            panel.Push(LogPage.Build());
            ((LogView)TestPanel.Field(panel, "logView")).Tick();
            panel.Pop();
            Assert.IsFalse(badge.activeSelf);
        }
    }
}
