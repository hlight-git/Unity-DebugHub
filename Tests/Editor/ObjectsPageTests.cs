using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hlight.Debug.Hub.Tests
{
    public class ObjectsPageTests
    {
        private const string ROOT = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
        private DebugHubPanel panel;
        private string backup;

        [SetUp]
        public void SetUp()
        {
            backup = PlayerPrefs.GetString("DebugHub.Watches", string.Empty);
            PlayerPrefs.DeleteKey("DebugHub.Watches");
            ClearVars();   // test file khác để sót biến $ thì nút "Gỡ" đầu tiên là của nó
            AddressFixture.Reset();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetString("DebugHub.Watches", backup);
            ClearVars();
            TestPanel.Destroy(panel);
        }

        private static void ClearVars()
        {
            foreach (var pair in new List<KeyValuePair<string, object>>(Vars.All)) Vars.Remove(pair.Key);
        }

        [Test]
        public void Root_ShowsPinnedAddressesAndVariablesInOneList()
        {
            Watches.TryAdd($"{ROOT}.Number", out _);
            Vars.Bind("v", 7);

            panel.ShowFromRoot(ObjectsPage.Root());

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Exists(l => l.Contains("Number")));
            Assert.IsTrue(labels.Exists(l => l.Contains("$v")));
        }

        [Test]
        public void Variables_AreMarkedAsSessionOnly()
        {
            Vars.Bind("v", 7);

            panel.ShowFromRoot(ObjectsPage.Root());

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("phiên này")),
                "biến $ chết khi domain reload — phải nói ra, khác address đã lưu");
        }

        [Test]
        public void ScalarsAndObjects_AreSplitIntoTwoSections()
        {
            Watches.TryAdd($"{ROOT}.Number", out _);
            Watches.TryAdd($"{ROOT}.Box", out _);

            panel.ShowFromRoot(ObjectsPage.Root());

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Exists(l => l.Contains("Giá trị")));
            Assert.IsTrue(labels.Exists(l => l.Contains("Object")));
        }

        [Test]
        public void BrokenAddress_ShowsTheReason_AndCanBeDropped()
        {
            Watches.TryAdd("Khong.Co.Gi.O.Day", out _);

            panel.ShowFromRoot(ObjectsPage.Root());
            TestPanel.ClickRowContaining(panel, "Gỡ");

            Assert.AreEqual(0, Watches.All.Count);
        }

        [Test]
        public void DroppingAVariable_RemovesItFromVarsNotFromWatches()
        {
            Vars.Bind("v", 7);

            panel.ShowFromRoot(ObjectsPage.Root());
            TestPanel.ClickRowContaining(panel, "Gỡ");

            Assert.AreEqual(0, Vars.All.Count);
        }

        [Test]
        public void Page_IsLive()
        {
            Assert.IsTrue(ObjectsPage.Root().Live);
        }

        /// Chuyển từ WatchPage cũ: ghi hỏng phải hiện lỗi ở dòng kết quả, không im như đã ghi xong.
        [Test]
        public void FailingWrite_ShowsVisibleError_InsteadOfSilentSuccess()
        {
            Assert.IsTrue(Watches.TryAdd($"{ROOT}.Explosive", out var addError), addError);

            panel.ShowFromRoot(ObjectsPage.Root());
            var row = TestPanel.Rows(panel).First(r => r.label.text.StartsWith("Explosive"));
            Assert.IsNotNull(row.input, "Explosive là int — phải là ô nhập tại chỗ");

            LogAssert.Expect(LogType.Exception, "Exception: bùm");
            row.input.onEndEdit.Invoke("9");

            StringAssert.Contains("bùm", panel.LastResult);
        }
    }
}
