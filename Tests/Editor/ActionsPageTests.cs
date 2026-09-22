using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class ActionsPageTests
    {
        private DebugHubPanel panel;

        [SetUp] public void SetUp() => panel = TestPanel.Build();
        [TearDown] public void TearDown() => TestPanel.Destroy(panel);

        [Test]
        public void InlineValue_HasCopy_ButNoAssignPage()
        {
            var node = Node.Value("n", () => 1, v => { });
            panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Exists(l => l.Contains("Copy")));
            Assert.IsFalse(labels.Exists(l => l.Contains("Gán")),
                "số đã có ô nhập ngay trên row rồi — thêm page Gán là hai đường sửa cho một thứ");
        }

        [Test]
        public void ReferenceValue_HasAssignPage()
        {
            var go = new GameObject("probe");
            try
            {
                var node = Node.Value<GameObject>("go", () => go, v => { });
                panel.ShowFromRoot(ActionsPage.For(node, go, (n, v) => { }));

                Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Gán")));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadOnlyValue_HasNoAssignPage()
        {
            var go = new GameObject("probe");
            try
            {
                var node = Node.Value<GameObject>("go", () => go, null);
                panel.ShowFromRoot(ActionsPage.For(node, go, (n, v) => { }));

                Assert.IsFalse(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Gán")));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NodeWithAddress_OffersWatch_AndSaysWhenItIsAlreadyWatched()
        {
            var node = Node.Value("n", () => 1, v => { });
            node.Address = "Hlight.Debug.Hub.Tests.AddressFixture.Instance.Number";
            try
            {
                panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));
                Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Watch")));

                TestPanel.ClickRowContaining(panel, "Watch");
                panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));
                Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Đã watch")));
            }
            finally { Watches.Remove(node.Address); }
        }

        [Test]
        public void NodeWithoutAddress_DoesNotOfferWatch()
        {
            var node = Node.Value("n", () => 1, v => { });

            panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));

            Assert.IsFalse(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Watch")));
        }

        [Test]
        public void SaveVar_BindsTheLiveObject_NotItsName()
        {
            var go = new GameObject("probe");
            try
            {
                var node = Node.Value<GameObject>("go", () => go, null);
                panel.ShowFromRoot(ActionsPage.For(node, go, (n, v) => { }));
                TestPanel.ClickRowContaining(panel, "Lưu vào");
                // page nhập tên biến
                TestPanel.Rows(panel)[0].input.text = "g";
                TestPanel.ClickRowContaining(panel, "Lưu");

                Assert.IsTrue(Vars.TryGet("g", out var bound));
                Assert.AreSame(go, bound);
            }
            finally { Vars.Remove("g"); Object.DestroyImmediate(go); }
        }
    }
}
