using System.Collections.Generic;
using System.Linq;
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

        /// Bug đã sửa: nút `…` của row input chiếm một hàng riêng mà row không cao thêm — layout group
        /// ép label thấp hơn một dòng và TMP (Truncate) giấu mất tên field.
        [Test]
        public void WritableNumber_KeepsItsLabelVisible_WithTheMoreButton()
        {
            Render(Node.Value("Number", () => 1, v => { }));

            var label = TestPanel.Rows(panel)[0].label;
            Assert.GreaterOrEqual(label.rectTransform.rect.height + 1f, label.preferredHeight,
                "label bị ép thấp hơn một dòng chữ thì TMP không vẽ gì");
        }

        [Test]
        public void WritableBool_HasMoreButton()
        {
            // Task 8: ToggleRow thiếu nút `…` — Gán/Watch/Lưu vào $var không mở được cho bool.
            Render(Node.Value("flag", () => true, v => { }));

            Assert.IsNotNull(TestPanel.Rows(panel)[0].more);
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

        /// Task 18 follow-up: sửa một field trên Members page thành công mà không log gì thì phải
        /// im re, không bật một toast rỗng (RunInspect phải theo đúng luật của CommandsPage.Dispatch).
        [Test]
        public void SilentSuccessfulEdit_OnAMembersPage_DoesNotPopAToast()
        {
            var holder = new Holder();
            Render(Node.Value<Holder>("h", () => holder, null));
            TestPanel.ClickRowContaining(panel, "h");

            var row = TestPanel.Rows(panel).First(r => r.label.text.StartsWith("Number"));
            Assert.IsNotNull(row.input, "Number phải là ô nhập tại chỗ");
            row.input.onEndEdit.Invoke("9");

            Assert.AreEqual(9, holder.Number, "giá trị vẫn phải ghi được");
            Assert.IsFalse(TestPanel.ToastOf(panel).activeSelf,
                "sửa field thành công không log gì thì không được bật toast rỗng");
        }

        private class Holder { public int Number = 1; }

        private void Render(DebugNode node)
        {
            panel.ShowFromRoot(new DebugPage("t", p => NodeRenderer.Render(p, node, (_, _) => { })));
        }
    }
}
