using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Bảng "Row suy từ node" trong README. Kiểm bằng loại row panel dựng ra, không kiểm pixel.
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
        public void WritableNullString_HasAnEditor()
        {
            Render(Node.Value<string>("text", () => null, _ => { }));
            Assert.IsNotNull(TestPanel.Rows(panel)[0].input);
        }

        [Test]
        public void WritableNullable_HasAnEditor_AndAcceptsANumber()
        {
            int? stored = null;
            Render(Node.Value<int?>("maybe", () => stored, v => stored = v));

            var input = TestPanel.Rows(panel)[0].input;
            Assert.IsNotNull(input);
            Assert.AreEqual(TMPro.TMP_InputField.ContentType.IntegerNumber, input.contentType);
        }

        [Test]
        public void NestedCommand_HonorsConfirmationAndDismiss()
        {
            var calls = 0;
            var node = new ActionNode { Label = "nested", Confirm = true, Dismiss = DismissMode.ClosePanel,
                Invoke = _ => calls++ };
            panel.ShowFromRoot(new DebugPage("root", p => p.AddText("root")));
            NodeRenderer.RunInspect(panel, node, System.Array.Empty<string>());
            Assert.AreEqual(0, calls);
            Assert.AreEqual(2, panel.StackDepth);
            TestPanel.ClickRowContaining(panel, "Huỷ");
            Assert.AreEqual(0, calls);
            NodeRenderer.RunInspect(panel, node, System.Array.Empty<string>());
            TestPanel.ClickRowContaining(panel, "Chạy");
            Assert.AreEqual(1, calls);
            Assert.IsFalse(panel.IsOpen);
        }

        [Test]
        public void ParamsPage_RejectsInvalidArgumentsBeforeConfirmation()
        {
            var calls = 0;
            var node = new ActionNode { Label = "action", Parameters = new[] { new DebugParameter("amount", typeof(int)) } };
            panel.ShowFromRoot(ParamsPage.For(node, (_, _) => calls++));
            TestPanel.Rows(panel)[0].input.text = "invalid";
            TestPanel.ClickRowContaining(panel, "Chạy");
            Assert.AreEqual(0, calls);
            StringAssert.Contains("amount", panel.LastResult);
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
            // ToggleRow từng thiếu nút `…` — Gán/Watch/Lưu vào $var không mở được cho bool.
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

        /// Sửa một field trên Members page thành công mà không log gì thì phải
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

        [Test]
        public void ThrowingFolder_ShowsAnErrorRow_InsteadOfBreakingThePanel()
        {
            Render(Node.Folder("info", () => throw new System.InvalidOperationException("chưa có level")));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("chưa có level"));
            Assert.DoesNotThrow(() => TestPanel.ClickRowContaining(panel, "info"));

            Assert.AreEqual(2, panel.StackDepth, "trang folder vẫn mở");
            StringAssert.Contains("chưa có level", string.Join("|", TestPanel.LabelsOf(panel)));
        }

        /// Transform implement IEnumerable (duyệt con) nhưng là object có member thật: mở ra phải thấy
        /// position/rotation, danh sách con nằm ở một row riêng.
        [Test]
        public void Transform_OpensAsAnObject_WithItsChildrenOnASeparateRow()
        {
            var go = new GameObject("parent");
            new GameObject("child").transform.SetParent(go.transform);
            try
            {
                var cursor = new Cursor(typeof(Transform), go.transform, null);
                panel.ShowFromRoot(new DebugPage("t", p => NodeRenderer.RenderMembers(p, cursor, null)));

                var labels = TestPanel.LabelsOf(panel);
                Assert.IsTrue(labels.Exists(l => l.StartsWith("localPosition")), string.Join(" | ", labels));
                Assert.IsTrue(labels.Exists(l => l.StartsWith("Phần tử")));
            }
            finally { Object.DestroyImmediate(go); }
        }

        private void Render(DebugNode node)
        {
            panel.ShowFromRoot(new DebugPage("t", p => NodeRenderer.Render(p, node, (_, _) => { })));
        }
    }
}
