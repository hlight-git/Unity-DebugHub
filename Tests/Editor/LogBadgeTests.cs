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
        public void Entry_ShowsAllThreeCounts_DimsZeros_CapsAt99()
        {
            var entry = panel.transform.root.GetComponentInChildren<DebugHubEntry>(true);
            TMP_Text Label(string group) => entry.transform.Find(group + "/Label").GetComponent<TMP_Text>();
            float Alpha(string group) => entry.transform.Find(group).GetComponent<CanvasGroup>().alpha;

            entry.SetCounts(3, 0, 120);
            Assert.AreEqual("3", Label("Log").text);
            Assert.AreEqual(1f, Alpha("Log"));
            Assert.AreEqual("0", Label("Warning").text, "số 0 vẫn hiện, ô không đổi hình");
            Assert.Less(Alpha("Warning"), 1f, "số 0 mờ đi");
            Assert.AreEqual("99+", Label("Error").text);
        }

        /// DebugHub.Update: đếm log Unity mới theo loại, chỉ khi máy đang ghi. Log native (MIUI, SDK…) không đếm.
        [Test]
        public void HubUpdate_CountsUnseenUnityLogsByType_OnlyWhileRecording()
        {
            var root = panel.transform.root;
            var hub = root.GetComponentInChildren<DebugHub>(true);
            var entry = root.GetComponentInChildren<DebugHubEntry>(true).transform;
            TMP_Text Label(string group) => entry.Find(group + "/Label").GetComponent<TMP_Text>();
            var update = typeof(DebugHub).GetMethod("Update",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            LogRecorder.Receive("e", null, LogType.Error);

            update.Invoke(hub, null);
            Assert.AreEqual("0", Label("Error").text, "máy không ghi thì không đếm");

            LogRecorder.Start();
            LogRecorder.Receive("w", null, LogType.Warning);
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Error, "MIUIInput", "noise", null);
            update.Invoke(hub, null);
            Assert.AreEqual("0", Label("Log").text);
            Assert.AreEqual("1", Label("Warning").text);
            Assert.AreEqual("1", Label("Error").text, "lỗi native không đếm");
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
