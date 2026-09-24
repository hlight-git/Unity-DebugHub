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
        public void Search_FiltersObjects_InsteadOfSwitchingToCommands()
        {
            Vars.Bind("needle", 7);
            Vars.Bind("unrelated", 9);
            panel.ShowFromRoot(ObjectsPage.Root());
            panel.Query = "needle";
            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Exists(label => label.Contains("$needle")));
            Assert.IsFalse(labels.Exists(label => label.Contains("$unrelated")));
        }

        [Test]
        public void ManualAddress_PreservesDraftWhenRebuilt()
        {
            panel.ShowFromRoot(ObjectsPage.Root());
            TestPanel.ClickRowContaining(panel, "Nhập address");
            TestPanel.Rows(panel).First(row => row.input).input.text = ROOT;
            panel.Refresh();
            Assert.AreEqual(ROOT, TestPanel.Rows(panel).First(row => row.input).input.text);
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
        public void DroppingAVariable_GoesThroughItsMoreButton()
        {
            Vars.Bind("v", 7);

            panel.ShowFromRoot(ObjectsPage.Root());
            TestPanel.Rows(panel).First(r => r.label.text.StartsWith("$v")).more.onClick.Invoke();
            TestPanel.ClickRowContaining(panel, "Bỏ biến");

            Assert.AreEqual(0, Vars.All.Count);
        }

        [Test]
        public void EachPin_TakesOneRow_NotTwo()
        {
            Watches.TryAdd($"{ROOT}.Number", out _);
            Watches.TryAdd($"{ROOT}.Frozen", out _);

            panel.ShowFromRoot(ObjectsPage.Root());

            Assert.AreEqual(0, TestPanel.LabelsOf(panel).Count(l => l.Trim() == "Gỡ"),
                "Gỡ nằm trong nút … của chính dòng đó, không phải một row riêng");
        }

        [Test]
        public void ManualAddress_OpensTheMemberListAtThatAddress()
        {
            panel.ShowFromRoot(ObjectsPage.Root());
            TestPanel.ClickRowContaining(panel, "Nhập address");
            TestPanel.Rows(panel).First(row => row.input).input.onEndEdit.Invoke(ROOT);
            TestPanel.ClickRowContaining(panel, "Mở");

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Number")));
            Assert.AreEqual(0, Watches.All.Count, "chỉ mở — ghim bằng nút ở cuối trang");
            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Ghim vào Objects")));
        }

        /// Hai loại address mà trang này tồn tại để mở — gốc `$` và có gọi method — đều không ghim được.
        /// Bản cũ bắt ghim trước khi mở nên cả hai bị từ chối.
        [Test]
        public void ManualAddress_OpensAVariableAddress_ThatCannotBePinned()
        {
            Vars.Bind("fx", AddressFixture.Instance);
            try
            {
                panel.ShowFromRoot(ObjectsPage.Root());
                TestPanel.ClickRowContaining(panel, "Nhập address");
                TestPanel.Rows(panel).First(row => row.input).input.onEndEdit.Invoke("$fx.Box");
                TestPanel.ClickRowContaining(panel, "Mở");

                var labels = TestPanel.LabelsOf(panel);
                Assert.IsTrue(labels.Exists(l => l.StartsWith("Value")), string.Join(" | ", labels));
                Assert.IsFalse(labels.Exists(l => l.Contains("Ghim")), "address gốc $ không có nút ghim");
            }
            finally { Vars.Remove("fx"); }
        }

        [Test]
        public void Root_ResolvesEachPinnedAddressOncePerBuild()
        {
            Watches.TryAdd($"{ROOT}.Counted", out _);
            AddressFixture.CountedReads = 0;

            panel.ShowFromRoot(ObjectsPage.Root());

            Assert.AreEqual(1, AddressFixture.CountedReads,
                "mỗi lần dựng chỉ được resolve một lần");
        }

        [Test]
        public void Page_DoesNotRebuildContinuously()
        {
            Assert.IsFalse(ObjectsPage.Root().Live,
                "rebuilding pooled input rows four times per second causes visible flicker");
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
