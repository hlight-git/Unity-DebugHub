using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hlight.Debug.Hub.Tests
{
    public class AdvancedPageTests
    {
        private const string ROOT = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
        private DebugHubPanel panel;
        private string watchBackup;

        [SetUp]
        public void SetUp()
        {
            watchBackup = PlayerPrefs.GetString("DebugHub.Watches", string.Empty);
            PlayerPrefs.DeleteKey("DebugHub.Watches");
            AddressFixture.Reset();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetString("DebugHub.Watches", watchBackup);
            TestPanel.Destroy(panel);
        }

        [Test]
        public void Root_ListsTheFiveTools()
        {
            panel.ShowFromRoot(AdvancedPage.Root());

            var labels = TestPanel.LabelsOf(panel);
            foreach (var name in new[] { "Watch", "Types", "Instances", "Vars", "Execute" })
                Assert.IsTrue(labels.Exists(l => l.Contains(name)), $"thiếu {name}");
        }

        [Test]
        public void WatchPage_ShowsScalarsInPlace_AndObjectsAsNavRows()
        {
            Watches.TryAdd($"{ROOT}.Number", out _);
            Watches.TryAdd($"{ROOT}.Box", out _);

            panel.ShowFromRoot(AdvancedPage.WatchPage());

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Exists(l => l.Contains("Number")));
            Assert.IsTrue(labels.Exists(l => l.Contains("Box")));
        }

        [Test]
        public void WatchPage_IsLive()
        {
            Assert.IsTrue(AdvancedPage.WatchPage().Live);
        }

        [Test]
        public void WatchPage_ShowsABrokenAddressAsAnErrorWithARemoveButton()
        {
            Watches.TryAdd("Khong.Co.Gi.O.Day", out _);

            panel.ShowFromRoot(AdvancedPage.WatchPage());

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Gỡ")));
        }

        /// Bug đã sửa: Section (dùng cho row Watch) chạy node qua
        /// `(n, v) => DebugRegistry.Run(n, v, out _)`, nuốt luôn kết quả — Set ném lỗi thì panel
        /// vẫn im re như đã ghi thành công. Giờ đi qua NodeRenderer.RunInspect nên lỗi phải nổi lên
        /// panel.LastResult, đúng chính sách chính NodeRenderer.RenderValue/ValueFor đã ghi trong
        /// doc comment.
        [Test]
        public void WatchPage_FailingWrite_ShowsVisibleError_InsteadOfSilentSuccess()
        {
            Assert.IsTrue(Watches.TryAdd($"{ROOT}.Explosive", out var addError), addError);

            panel.ShowFromRoot(AdvancedPage.WatchPage());
            var row = TestPanel.Rows(panel).First(r => r.label.text.StartsWith("Explosive"));
            Assert.IsNotNull(row.input, "Explosive là int — phải là ô nhập tại chỗ");

            LogAssert.Expect(LogType.Exception, "Exception: bùm");
            row.input.onEndEdit.Invoke("9");

            StringAssert.Contains("bùm", panel.LastResult);
        }

        [Test]
        public void TypesPage_IsEmptyUntilTwoCharactersAreTyped()
        {
            panel.ShowFromRoot(AdvancedPage.TypesPage());
            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("2 ký tự")));

            panel.Query = "DebugRegistry";
            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("DebugRegistry")));
        }

        [Test]
        public void VarsPage_ListsBoundVariables_AndCanDropThem()
        {
            Vars.Bind("v", 7);
            try
            {
                panel.ShowFromRoot(AdvancedPage.VarsPage());
                Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("v")));
            }
            finally { Vars.Remove("v"); }
        }

        [Test]
        public void ExecutePage_ReadsAnAddress()
        {
            panel.ShowFromRoot(AdvancedPage.ExecutePage());
            TestPanel.Rows(panel)[0].input.onEndEdit.Invoke($"{ROOT}.Number");
            TestPanel.ClickRowContaining(panel, "Get");

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("1")) ||
                          panel.LastResult.Contains("1"));
        }
    }
}
