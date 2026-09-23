using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    public class DebugHubPanelTests
    {
        private DebugHubPanel panel;
        private Transform content;

        [SetUp]
        public void SetUp()
        {
            panel = TestPanel.Build();
            content = panel.transform.Find("Window/Scroll View/Viewport/Content");
            Assert.IsNotNull(content, "Content transform not found at expected path");
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
        }

        /// Row đầu tiên đang bật — template nằm trong Content nhưng luôn inactive.
        private RectTransform FirstRow()
        {
            foreach (Transform child in content)
            {
                if (child.gameObject.activeSelf) return (RectTransform)child;
            }
            Assert.Fail("no active row");
            return null;
        }

        private int RowCount()
        {
            var count = 0;
            foreach (Transform child in content)
            {
                if (child.gameObject.activeSelf) count++;
            }
            return count;
        }

        [Test]
        public void Show_BuildsRowsAndOpensPanel()
        {
            panel.Show(new DebugPage("Root", p =>
            {
                p.AddButton("btn", () => { });
                p.AddToggle("tgl", true, _ => { });
                p.AddText("hello");
            }));

            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual(3, RowCount());
        }

        [Test]
        public void Pop_FromRoot_ClosesPanelAndClearsRows()
        {
            panel.Show(new DebugPage("Root", p => p.AddButton("btn", () => { })));
            panel.Pop();

            Assert.IsFalse(panel.IsOpen);
            Assert.AreEqual(0, RowCount());
        }

        [Test]
        public void Push_ReplacesRowsWithChildPage_PopRestoresParent()
        {
            panel.Show(new DebugPage("Root", p => p.AddButton("only-root", () => { })));
            panel.Push(new DebugPage("Child", p =>
            {
                p.AddButton("a", () => { });
                p.AddButton("b", () => { });
            }));
            Assert.AreEqual(2, RowCount());

            panel.Pop();
            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual(1, RowCount());
        }

        /// Panel phải cao theo nội dung, không phải luôn cao gần full màn hình.
        [Test]
        public void Window_GrowsWithContent_AndClampsAtMax()
        {
            var window = (RectTransform)panel.transform.Find("Window");

            panel.Show(new DebugPage("Short", p => p.AddButton("a", () => { })));
            var shortHeight = window.rect.height;

            panel.ShowFromRoot(new DebugPage("Long", p =>
            {
                for (var i = 0; i < 30; i++) p.AddButton("row " + i, () => { });
            }));
            var longHeight = window.rect.height;

            Assert.Less(shortHeight, 500f, $"one row should not give a {shortHeight} tall window");
            Assert.Less(shortHeight, longHeight, "window must grow with content");
            Assert.LessOrEqual(longHeight, panel.MaxWindowHeight + 0.5f, $"window must stay within MaxWindowHeight, got {longHeight}");
        }

        /// Panel phải co lại, không chỉ nở ra: row của page cũ không được tính vào chiều cao
        /// (trong Play Mode Destroy() bị hoãn tới cuối frame nên chúng phải bị tắt trước khi đo).
        [Test]
        public void Window_ShrinksWhenNextPageHasFewerRows()
        {
            var window = (RectTransform)panel.transform.Find("Window");

            panel.Show(new DebugPage("Long", p =>
            {
                for (var i = 0; i < 20; i++) p.AddButton("row " + i, () => { });
            }));
            var tall = window.rect.height;

            panel.Push(new DebugPage("Short", p => p.AddButton("only one", () => { })));
            var shrunk = window.rect.height;

            Assert.Less(shrunk, tall, $"window must shrink back: {tall} -> {shrunk}");
            Assert.Less(shrunk, 500f, $"one row page should be short, got {shrunk}");
            Assert.AreEqual(1, RowCount(), "stale rows from the previous page must be gone");
        }

        /// Row có chiều cao phụ thuộc chiều rộng (text wrap) chỉ đo đúng sau khi width đã được áp,
        /// nếu không thì page Help co lại còn vài dòng.
        [Test]
        public void Window_FitsLongWrappedText()
        {
            var window = (RectTransform)panel.transform.Find("Window");
            var help = new System.Text.StringBuilder("<b>Available commands:</b>");
            for (var i = 0; i < 25; i++)
            {
                help.Append("\n  - command.name").Append(i).Append(" [String query]: description of what this command does");
            }

            panel.Show(new DebugPage("Help", p => p.AddText(help.ToString())));

            Assert.Greater(window.rect.height, 1000f,
                $"a 25-line help text must not collapse the window to {window.rect.height}");
        }

        /// Mọi template phải chừa lề như nhau: text block và field nằm trong layout group nên trước đó
        /// chỉ có 4px, dán sát mép trong khi row thường có 28px.
        [Test]
        public void Rows_KeepContentInsetFromTheRowEdge()
        {
            panel.Show(new DebugPage("Padding", p =>
            {
                p.AddNavigation("nav", new DebugPage("x", _ => { }));
                p.AddAction("action", () => { });
                p.AddToggle("toggle", false, _ => { });
                p.AddField("field", typeof(int), "0", _ => { });
                p.AddText("một đoạn text dài để nó phải wrap trong page Help");
            }));

            var corners = new Vector3[4];
            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf) continue;

                var row = (RectTransform)child;
                var label = child.GetComponentInChildren<TMP_Text>(true).rectTransform;
                label.GetWorldCorners(corners);
                var inset = row.InverseTransformPoint(corners[0]).x - row.rect.xMin;

                Assert.GreaterOrEqual(inset, 24f, $"row '{child.name}' chỉ chừa {inset} so với mép");
            }
        }

        [Test]
        public void AddField_PicksWidgetByType()
        {
            panel.Show(new DebugPage("Fields", p =>
            {
                p.AddField("enum", typeof(LogType), nameof(LogType.Warning), _ => { });
                p.AddField("bool", typeof(bool), "true", _ => { });
                p.AddField("int", typeof(int), "0", _ => { });
                p.AddField("vector", typeof(Vector3), string.Empty, _ => { });
            }));

            Assert.AreEqual(1, content.GetComponentsInChildren<Toggle>(false).Length, "bool should be a toggle");
            Assert.AreEqual(1, content.GetComponentsInChildren<Button>(false).Length, "enum should be a choice row (button)");

            var inputs = content.GetComponentsInChildren<TMP_InputField>(false);
            Assert.AreEqual(2, inputs.Length, "int and Vector3 should both be input fields");
            Assert.AreEqual(TMP_InputField.ContentType.IntegerNumber, inputs[0].contentType);
            Assert.AreEqual(TMP_InputField.ContentType.Standard, inputs[1].contentType);
        }

        /// Scene không có EventSystem thì không gõ được password. Prefab phải tự mang một cái,
        /// inactive, để EventSystemHandler chỉ bật khi scene chưa có.
        [Test]
        public void Prefab_ShipsWithInactiveEventSystem_WiredToHandler()
        {
            var instance = panel.transform.root.gameObject;
            var eventSystem = instance.transform.Find("EventSystem");
            Assert.IsNotNull(eventSystem, "prefab must carry its own EventSystem");
            Assert.IsFalse(eventSystem.gameObject.activeSelf, "embedded EventSystem must start inactive");
            Assert.IsNotNull(eventSystem.GetComponent<UnityEngine.EventSystems.EventSystem>());
            Assert.IsNotNull(eventSystem.GetComponent<UnityEngine.EventSystems.BaseInputModule>(), "EventSystem needs an input module");

            var handler = instance.GetComponent<IngameDebugConsole.EventSystemHandler>();
            Assert.IsNotNull(handler, "EventSystemHandler missing on prefab root");
            var wired = new SerializedObject(handler).FindProperty("embeddedEventSystem");
            Assert.AreEqual(eventSystem.gameObject, wired.objectReferenceValue);
        }

        /// Enum dùng page chọn giá trị thay cho UI.Dropdown: bấm row -> liệt kê -> chọn -> tự back.
        [Test]
        public void AddField_ForEnum_OpensChoicePage_AndReportsPickedValue()
        {
            var captured = string.Empty;
            panel.Show(new DebugPage("Enum", p =>
                p.AddField("type", typeof(LogType), nameof(LogType.Log), v => captured = v)));

            var row = content.GetComponentsInChildren<Button>(false)[0];
            StringAssert.Contains(nameof(LogType.Log), row.GetComponentInChildren<TMP_Text>(true).text);

            row.onClick.Invoke();
            var options = content.GetComponentsInChildren<Button>(false);
            Assert.AreEqual(System.Enum.GetNames(typeof(LogType)).Length, options.Length, "choice page must list every enum value");

            var wanted = System.Array.Find(options, o => o.GetComponentInChildren<TMP_Text>(true).text.StartsWith(nameof(LogType.Exception)));
            Assert.IsNotNull(wanted, "Exception option missing");
            wanted.onClick.Invoke();

            Assert.AreEqual(nameof(LogType.Exception), captured);
            Assert.AreEqual("Enum", panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>().text,
                "picking a value must return to the page that owns the field");
        }

        [Test]
        public void Close_FromChildPage_ClosesPanelAndClearsRows()
        {
            panel.Show(new DebugPage("Root", p => p.AddButton("only-root", () => { })));
            panel.Push(new DebugPage("Child", p => p.AddButton("a", () => { })));

            panel.Close();

            Assert.IsFalse(panel.IsOpen);
            Assert.AreEqual(0, RowCount());
        }

        /// Đóng rồi mở lại là về đúng page đang xem, không phải bò lại từ root; Reset mới về root.
        [Test]
        public void Show_AfterClose_RestoresTheClosedPage()
        {
            var title = panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>();
            var root = new DebugPage("Root", p => p.AddButton("only-root", () => { }));

            panel.Show(root);
            panel.Push(new DebugPage("Child", p => p.AddButton("a", () => { })));
            panel.Close();

            panel.Show(root);
            Assert.AreEqual("Child", title.text);

            panel.ShowFromRoot(root);
            Assert.AreEqual("Root", title.text);
        }

        /// Row có description hai dòng phải cao thêm, không cắt chữ.
        [Test]
        public void Rows_GrowWhenLabelWraps()
        {
            panel.Show(new DebugPage("Rows", p => p.AddNavigation("goto", new DebugPage("x", _ => { }))));
            var single = FirstRow().rect.height;

            panel.ShowFromRoot(new DebugPage("Rows", p => p.AddNavigation(
                "goto\n<size=38>Nhảy tới level bất kỳ, kể cả level chưa mở, và tải lại màn đang chơi ngay lập tức</size>",
                new DebugPage("x", _ => { }))));
            var wrapped = FirstRow().rect.height;

            Assert.Greater(wrapped, single + 1f, $"row có description phải cao hơn: {single} -> {wrapped}");
        }

        [Test]
        public void PooledRow_ReturnsToTemplateHeight_AfterATallPage()
        {
            var tall = new DebugPage("tall", page => page.AddButton(new string('x', 400), () => { }));
            var shortPage = new DebugPage("short", page => page.AddButton("x", () => { }));

            panel.ShowFromRoot(tall);
            var grown = TestPanel.Rows(panel)[0].GetComponent<RectTransform>().sizeDelta.y;

            panel.ShowFromRoot(shortPage);
            var reused = TestPanel.Rows(panel)[0].GetComponent<RectTransform>().sizeDelta.y;

            Assert.Less(reused, grown, "row tái dùng vẫn giữ chiều cao đã nở của page trước");
        }

        [Test]
        public void PooledRow_DropsListenersOfThePreviousPage()
        {
            var clicks = 0;
            panel.ShowFromRoot(new DebugPage("a", page => page.AddButton("a", () => clicks++)));
            panel.ShowFromRoot(new DebugPage("b", page => page.AddButton("b", () => { })));

            TestPanel.Rows(panel)[0].button.onClick.Invoke();

            Assert.AreEqual(0, clicks, "row tái dùng vẫn gọi listener của page cũ");
        }

        [Test]
        public void Refresh_KeepsScrollPosition_WhileShowResetsIt()
        {
            panel.ShowFromRoot(new DebugPage("long", page =>
            {
                for (var i = 0; i < 40; i++) page.AddButton($"row {i}", () => { });
            }));
            TestPanel.ScrollOf(panel).verticalNormalizedPosition = 0.25f;

            panel.Refresh();
            Assert.AreEqual(0.25f, TestPanel.ScrollOf(panel).verticalNormalizedPosition, 0.001f);

            // Page "again" cũng phải đủ dài để tràn scroll — page ngắn vừa khít viewport thì
            // ScrollRect không còn chỗ để phân biệt "đỉnh" khỏi trạng thái nghỉ, verticalNormalizedPosition
            // đọc ra 0 dù panel đã set 1 (giới hạn của chính ScrollRect, không phải của Refresh/Show).
            panel.ShowFromRoot(new DebugPage("again", page =>
            {
                for (var i = 0; i < 40; i++) page.AddButton($"again {i}", () => { });
            }));
            Assert.AreEqual(1f, TestPanel.ScrollOf(panel).verticalNormalizedPosition, 0.001f);
        }

        /// Trần 1500 cố định là chỗ làm một màn hình chỉ chứa được ~11 dòng. Trần giờ là tỉ lệ màn hình
        /// (đổi theo máy) — kiểm công thức, không kiểm một con số pixel phụ thuộc cửa sổ Editor.
        [Test]
        public void WindowHeight_IsCappedByAShareOfTheScreen_NotAFixedNumber()
        {
            panel.ShowFromRoot(new DebugPage("long", page =>
            {
                for (var i = 0; i < 200; i++) page.AddButton($"row {i}", () => { });
            }));

            var window = (RectTransform)TestPanel.Field(panel, "window");
            Assert.AreEqual(panel.MaxWindowHeight, window.sizeDelta.y, 0.5f);
            Assert.IsNull(typeof(DebugHubPanel).GetField("maxWindowHeight",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
                "hai trần cùng tồn tại thì không ai biết cái nào đang thắng");
        }

        [Test]
        public void NavRow_HasAMoreButton_SoObjectsCanBePinnedFromTheList()
        {
            panel.ShowFromRoot(new DebugPage("t", page => page.AddNavigation("obj", new DebugPage("x", _ => { }))));

            Assert.IsNotNull(TestPanel.Rows(panel)[0].more);
        }
    }
}
