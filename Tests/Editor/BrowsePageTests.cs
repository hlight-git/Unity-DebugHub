using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class BrowsePageTests
    {
        private DebugHubPanel panel;

        [SetUp] public void SetUp() { AddressFixture.Reset(); panel = TestPanel.Build(); }
        [TearDown] public void TearDown() => TestPanel.Destroy(panel);

        [Test]
        public void Assemblies_AreEmptyUntilTyped_ThenFilter()
        {
            panel.ShowFromRoot(BrowsePage.Assemblies());
            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Tìm")));

            panel.Query = "Hlight.Debug";
            TestPanel.PumpUntil(panel, () => TestPanel.LabelsOf(panel).Exists(l => l.Contains("Hlight.Debug.Hub")));
        }

        [Test]
        public void Types_AreScopedToTheChosenAssembly()
        {
            panel.ShowFromRoot(BrowsePage.Types(typeof(DebugRegistry).Assembly));

            panel.Query = "DebugRegistry";
            TestPanel.PumpUntil(panel, () => TestPanel.LabelsOf(panel).Exists(l => l.Contains("DebugRegistry")));
        }

        /// Hai trang Types của hai assembly khác nhau, cùng một query: kết quả không được lọt sang nhau.
        [Test]
        public void Types_OfTwoAssemblies_DoNotShareResults()
        {
            panel.ShowFromRoot(BrowsePage.Types(typeof(DebugRegistry).Assembly));
            panel.Query = "DebugRegistry";
            TestPanel.PumpUntil(panel, () => TestPanel.LabelsOf(panel).Exists(l => l.Contains("DebugRegistry")));

            panel.ShowFromRoot(BrowsePage.Types(typeof(Rigidbody).Assembly));
            panel.Query = "DebugRegistry";
            TestPanel.PumpUntil(panel, () => TestPanel.LabelsOf(panel).Exists(l => l.Contains("Không có type nào khớp")));
        }

        [Test]
        public void Types_OfferStaticMembers_EvenForTypesWithNoInstance()
        {
            panel.ShowFromRoot(BrowsePage.Instances(typeof(DebugRegistry)));

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("static")),
                "type không phải UnityEngine.Object vẫn phải mở được member static");
        }

        [Test]
        public void Instances_ListLiveObjects_WithAnAddressEach()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                panel.ShowFromRoot(BrowsePage.Instances(typeof(BoxCollider)));

                var labels = TestPanel.LabelsOf(panel);
                Assert.IsTrue(labels.Exists(l => l.Contains("probe")));
                Assert.IsTrue(labels.Exists(l => l.Contains("#UnityEngine.BoxCollider[")));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void At_OpensTheMemberListAndCanPinTheAddress()
        {
            var backup = PlayerPrefs.GetString("DebugHub.Watches", string.Empty);
            PlayerPrefs.DeleteKey("DebugHub.Watches");
            try
            {
                panel.ShowFromRoot(BrowsePage.At("Hlight.Debug.Hub.Tests.AddressFixture.Instance", "fixture"));

                Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Number")));
                TestPanel.ClickRowContaining(panel, "Ghim");
                Assert.AreEqual(1, Watches.All.Count);
            }
            finally { PlayerPrefs.SetString("DebugHub.Watches", backup); }
        }

        [Test]
        public void At_ShowsTheAddressUnderTheTitle_NotAsTheFirstRow()
        {
            panel.ShowFromRoot(BrowsePage.At("Hlight.Debug.Hub.Tests.AddressFixture.Instance", "fixture"));

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsFalse(labels[0].Contains("Copy address"), "dòng đầu là chỗ đắt nhất trang");
            StringAssert.Contains("AddressFixture.Instance", TestPanel.SubtitleOf(panel));
        }

        [Test]
        public void DrillingIntoAMember_KeepsTheAddressVisible()
        {
            panel.ShowFromRoot(BrowsePage.At("Hlight.Debug.Hub.Tests.AddressFixture.Instance", "fixture"));
            TestPanel.ClickRowContaining(panel, "Box");

            StringAssert.Contains(".Box", TestPanel.SubtitleOf(panel));
        }

        /// Bug đã sửa: nút Adv chỉ ẩn ở đúng trang gốc Advanced — ở Objects/Duyệt/trang member bấm vào
        /// là chồng thêm một Advanced nữa lên stack.
        [Test]
        public void AdvButton_StaysHidden_AnywhereUnderTheAdvancedRoot()
        {
            panel.ShowFromRoot(AdvancedPage.Root());
            panel.Push(BrowsePage.At("Hlight.Debug.Hub.Tests.AddressFixture.Instance", "fixture"));
            TestPanel.ClickRowContaining(panel, "Box");

            Assert.IsFalse(((UnityEngine.UI.Button)TestPanel.Field(panel, "advancedButton")).gameObject.activeSelf);
        }

        [Test]
        public void AdvButton_ShowsOnCommandPages()
        {
            panel.ShowFromRoot(BrowsePage.At("Hlight.Debug.Hub.Tests.AddressFixture.Instance", "fixture"));

            Assert.IsTrue(((UnityEngine.UI.Button)TestPanel.Field(panel, "advancedButton")).gameObject.activeSelf);
        }

        /// Root static có Value null — trang Method vẫn phải liệt kê được, không ra chữ "null".
        [Test]
        public void At_StaticRoot_OpensItsMethodPage()
        {
            panel.ShowFromRoot(BrowsePage.At("Hlight.Debug.Hub.DebugRegistry", "DebugRegistry"));
            TestPanel.ClickRowContaining(panel, "Method");

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Run")));
        }
    }
}
