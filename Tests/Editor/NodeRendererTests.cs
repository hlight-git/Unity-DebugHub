using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Bảng §3 của spec. Kiểm bằng loại row panel dựng ra, không kiểm pixel.
    public class NodeRendererTests
    {
        private DebugHubPanel panel;

        [SetUp] public void SetUp() => panel = TestPanel.Build();
        [TearDown] public void TearDown() => TestPanel.Destroy(panel);

        [Test]
        public void ReadOnlyBool_IsTextRow_NotASwitch()
        {
            Render(Node.Value<bool>("flag", () => true, null));

            Assert.IsNull(TestPanel.Rows(panel)[0].toggle);
        }

        [Test]
        public void WritableBool_IsASwitch()
        {
            Render(Node.Value("flag", () => true, v => { }));

            Assert.IsNotNull(TestPanel.Rows(panel)[0].toggle);
        }

        [Test]
        public void ComponentField_OpensMembers_EvenThoughIdcCanParseIt()
        {
            var go = new GameObject("probe");
            try
            {
                Render(Node.Value<Transform>("tf", () => go.transform, null));
                Assert.IsNull(TestPanel.Rows(panel)[0].input, "Component không được thành ô nhập text");
                Assert.IsNotNull(TestPanel.Rows(panel)[0].detail, "phải là row nav");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NullValue_IsTextRow_AndCannotBeOpened()
        {
            var clicked = false;
            Render(Node.Value<object>("thing", () => null, v => clicked = true));

            Assert.AreEqual(1, TestPanel.Rows(panel).Count);
            StringAssert.Contains("null", TestPanel.Rows(panel)[0].detail.text);
            Assert.IsFalse(clicked);
        }

        [Test]
        public void DestroyedUnityObject_CountsAsNull()
        {
            var go = new GameObject("dead");
            Object.DestroyImmediate(go);

            Render(Node.Value<GameObject>("go", () => go, null));

            StringAssert.Contains("null", TestPanel.Rows(panel)[0].detail.text);
        }

        [Test]
        public void ThrowingGetter_ShowsErrorRow_AndDoesNotEscape()
        {
            Assert.DoesNotThrow(() => Render(Node.Value<int>("boom",
                () => throw new System.InvalidOperationException("bùm"), null)));

            StringAssert.Contains("bùm", TestPanel.Rows(panel)[0].label.text);
        }

        [Test]
        public void InlineFolder_BuildsChildrenInPlace_WithATitleRow()
        {
            Render(Node.Section("Spec", new DebugNode[] { Node.Text("a"), Node.Text("b") }));

            Assert.AreEqual(3, TestPanel.Rows(panel).Count);
        }

        [Test]
        public void DynamicFolder_DoesNotCallChildrenWhileRenderingTheParentRow()
        {
            var calls = 0;
            Render(Node.Folder("heavy", () => { calls++; return new DebugNode[0]; }));

            Assert.AreEqual(0, calls, "dựng row cha mà đã gọi delegate của game");
        }

        private void Render(DebugNode node)
        {
            panel.ShowFromRoot(new DebugPage("t", p => NodeRenderer.Render(p, node, (_, _) => { })));
        }
    }
}
