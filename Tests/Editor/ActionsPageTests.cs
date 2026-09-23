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
        public void NodeWithAddress_OffersPin_ThenUnpin()
        {
            var node = Node.Value("n", () => 1, v => { });
            node.Address = "Hlight.Debug.Hub.Tests.AddressFixture.Instance.Number";
            try
            {
                panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));
                TestPanel.ClickRowContaining(panel, "Ghim");
                Assert.IsTrue(Watches.Contains(node.Address));

                panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));
                TestPanel.ClickRowContaining(panel, "Bỏ ghim");
                Assert.IsFalse(Watches.Contains(node.Address));
            }
            finally { Watches.Remove(node.Address); }
        }

        [Test]
        public void NodeWithoutAddress_DoesNotOfferPin()
        {
            var node = Node.Value("n", () => 1, v => { });

            panel.ShowFromRoot(ActionsPage.For(node, 1, (n, v) => { }));

            Assert.IsFalse(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Ghim")));
        }

        [Test]
        public void Variable_OffersDrop_NotPin()
        {
            Vars.Bind("v", 7);
            try
            {
                var node = Node.Value("v", () => 7, null);
                node.Address = "$v";

                panel.ShowFromRoot(ActionsPage.For(node, 7, (n, v) => { }));
                Assert.IsFalse(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Ghim")));
                TestPanel.ClickRowContaining(panel, "Bỏ biến");

                Assert.IsFalse(Vars.TryGet("v", out _));
            }
            finally { Vars.Remove("v"); }
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
