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

        [Test]
        public void Navigation_RestoresSearchAndScroll_WithoutFilteringTheChild()
        {
            panel.ShowFromRoot(new DebugPage("parent", p => p.AddText("all"), (p, q) =>
            {
                for (var i = 0; i < 40; i++) p.AddButton(q + i, () => { });
            }));
            panel.Query = "needle";
            TestPanel.ScrollOf(panel).verticalNormalizedPosition = 0.3f;
            panel.Show(default);
            Assert.AreEqual("needle", panel.Query, "showing an already open panel preserves its current state");
            panel.Push(new DebugPage("child", p => p.AddText("child content"), searchable: false));
            Assert.AreEqual(string.Empty, panel.Query);
            Assert.IsFalse(((TMP_InputField)TestPanel.Field(panel, "searchInput")).gameObject.activeSelf);
            panel.Pop();
            Assert.AreEqual("needle", panel.Query);
            Assert.AreEqual(0.3f, TestPanel.ScrollOf(panel).verticalNormalizedPosition, 0.001f);
            Assert.IsTrue(((TMP_InputField)TestPanel.Field(panel, "searchInput")).gameObject.activeSelf);
            panel.Close();
            panel.Show(default);
            Assert.AreEqual("needle", panel.Query);
            Assert.AreEqual(0.3f, TestPanel.ScrollOf(panel).verticalNormalizedPosition, 0.001f);
        }

        [Test]
        public void Query_BeforeOpening_DoesNotThrow_AndNewRootClearsOldSearch()
        {
            Assert.DoesNotThrow(() => panel.Query = "old");
            panel.ShowFromRoot(new DebugPage("root", p => p.AddText("content")));
            Assert.AreEqual(string.Empty, panel.Query);
            CollectionAssert.AreEqual(new[] { "content" }, TestPanel.LabelsOf(panel));
        }

        [Test]
        public void Paging_KeepsEveryItemReachable_AndRestoresPageAfterBack()
        {
            var items = new string[93];
            for (var i = 0; i < items.Length; i++) items[i] = "item " + i;
            panel.ShowFromRoot(new DebugPage("paged", p => p.AddPaged(items, item => p.AddText(item)),
                (p, q) => p.AddPaged(new[] { "filtered" }, item => p.AddText(item))));
            Assert.AreEqual(42, TestPanel.Rows(panel).Count);
            TestPanel.ClickRowContaining(panel, "Trang tiếp");
            StringAssert.Contains("41–80 / 93", TestPanel.Rows(panel)[0].label.text);
            panel.Push(new DebugPage("child", p => p.AddText("child")));
            panel.Pop();
            StringAssert.Contains("41–80 / 93", TestPanel.Rows(panel)[0].label.text);
            TestPanel.ClickRowContaining(panel, "Trang tiếp");
            Assert.IsTrue(TestPanel.LabelsOf(panel).Contains("item 92"));
            Assert.IsFalse(TestPanel.LabelsOf(panel).Contains("Trang tiếp ›"));
            panel.Query = "filter";
            CollectionAssert.AreEqual(new[] { "filtered" }, TestPanel.LabelsOf(panel));
        }

        [Test]
        public void Field_SubmitsOnlyChanges_AndReportsInvalidValues()
        {
            var writes = 0;
            panel.ShowFromRoot(new DebugPage("field", p => p.AddField("number", typeof(int), "1", null, _ => writes++)));
            var input = TestPanel.Rows(panel)[0].input;
            input.onEndEdit.Invoke("1");
            Assert.AreEqual(0, writes);
            input.onEndEdit.Invoke("bad");
            Assert.AreEqual(0, writes);
            StringAssert.Contains("number", panel.LastResult);
            input.onEndEdit.Invoke("2");
            input.onEndEdit.Invoke("2");
            Assert.AreEqual(1, writes);
            panel.Close();
            input.onEndEdit.Invoke("3");
            Assert.AreEqual(1, writes, "pooled fields must release their submit callbacks on close");
        }

        [Test]
        public void ShortNavigationDetail_SharesOneLine_AndPoolRestoresHeight()
        {
            panel.ShowFromRoot(new DebugPage("wide", p => p.AddNavigation("long name", default)));
            var height = ((RectTransform)TestPanel.Rows(panel)[0].transform).rect.height;
            panel.ShowFromRoot(new DebugPage("detail", p => p.AddNavigation("name", default, detail: "42")));
            var row = TestPanel.Rows(panel)[0];
            Assert.AreEqual(height, ((RectTransform)row.transform).rect.height, 0.5f);
            var labelCorners = new Vector3[4];
            var valueCorners = new Vector3[4];
            row.label.rectTransform.GetWorldCorners(labelCorners);
            row.detail.rectTransform.GetWorldCorners(valueCorners);
            Assert.Greater(valueCorners[2].y, labelCorners[0].y);
            Assert.LessOrEqual(labelCorners[2].x, valueCorners[0].x);
            panel.ShowFromRoot(new DebugPage("plain", p => p.AddNavigation("short", default)));
            Assert.AreEqual(height, ((RectTransform)TestPanel.Rows(panel)[0].transform).rect.height, 0.5f);
        }

        [Test]
        public void MobileTypographyAndControls_DoNotShrinkToDesktopDensity()
        {
            panel.ShowFromRoot(new DebugPage("mobile", p =>
            {
                p.AddButton("Readable action", () => { });
                p.AddField("Amount", typeof(int), "123", _ => { });
            }));
            const float phoneScale = 360f / 1080f;
            var rows = TestPanel.Rows(panel);
            Assert.GreaterOrEqual(rows[0].label.fontSize * phoneScale, 18f);
            Assert.GreaterOrEqual(((RectTransform)rows[0].transform).rect.height * phoneScale, 44f);
            Assert.GreaterOrEqual(rows[1].input.textComponent.fontSize * phoneScale, 18f);
            Assert.LessOrEqual(((RectTransform)rows[1].transform).rect.height, 240f,
                "larger type must not bring back excessive field padding");
            Assert.GreaterOrEqual(((RectTransform)rows[1].input.transform).rect.height * phoneScale, 44f);
            var close = (Button)TestPanel.Field(panel, "closeButton");
            Assert.GreaterOrEqual(((RectTransform)close.transform).rect.width * phoneScale, 44f);
        }

        [Test]
        public void Toast_RemainsReadableOnAPhone_WithoutBecomingATallOverlay()
        {
            const float phoneScale = 360f / 1080f;
            var toast = (DebugHubToast)TestPanel.Field(panel, "toast");
            var label = toast.GetComponentInChildren<TMP_Text>(true);
            Assert.GreaterOrEqual(label.fontSize * phoneScale, 16f);
            Assert.LessOrEqual(((RectTransform)toast.transform).rect.height * phoneScale, 96f);
        }

        [Test]
        public void HeaderIcons_RenderAndShareTheTitleRow()
        {
            panel.ShowFromRoot(CommandsPage.Root());
            Canvas.ForceUpdateCanvases();
            var title = (TMP_Text)TestPanel.Field(panel, "title");
            var titleCorners = new Vector3[4];
            title.rectTransform.GetWorldCorners(titleCorners);
            foreach (var name in new[] { "searchButton", "advancedButton" })
            {
                var button = (Button)TestPanel.Field(panel, name);
                var icon = button.GetComponentInChildren<DebugHubIcon>();
                Assert.IsNotNull(icon);
                Assert.IsFalse(icon.raycastTarget);
                Assert.Greater(icon.canvasRenderer.GetMesh().vertexCount, 0, "icon must produce visible geometry");
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Assert.GreaterOrEqual(corners[0].x, titleCorners[2].x);
                Assert.AreEqual(titleCorners[0].y, corners[0].y, 1f);
            }
        }

        [Test]
        public void Scrollbar_AppearsForOverflow_DrivesScroll_AndHidesForShortPages()
        {
            panel.ShowFromRoot(new DebugPage("long", p =>
            {
                for (var i = 0; i < 60; i++) p.AddButton("row " + i, () => { });
            }));
            Canvas.ForceUpdateCanvases();
            var scroll = TestPanel.ScrollOf(panel);
            Assert.AreEqual(ScrollRect.MovementType.Elastic, scroll.movementType);
            var bar = scroll.verticalScrollbar;
            Assert.IsNotNull(bar);
            Assert.IsTrue(bar.gameObject.activeSelf);
            Assert.Less(bar.size, 1f);
            Assert.GreaterOrEqual(((RectTransform)bar.transform).rect.width, 40f);
            var hitGraphic = bar.GetComponent<Graphic>();
            var canvas = bar.GetComponentInParent<Canvas>();
            var effectiveHitWidth = (((RectTransform)bar.transform).rect.width +
                hitGraphic.raycastPadding.x + hitGraphic.raycastPadding.z) * canvas.scaleFactor;
            Assert.GreaterOrEqual(effectiveHitWidth, 44f,
                "scrollbar can look thin, but its real finger target must remain at least 44 px");
            bar.value = 0f;
            Assert.AreEqual(0f, scroll.verticalNormalizedPosition, 0.001f);
            bar.value = 1f;
            Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 0.001f);
            panel.ShowFromRoot(new DebugPage("short", p => p.AddText("short")));
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(bar.gameObject.activeSelf);
        }

        [Test]
        public void Search_LeavesTitleVisible_AndUsesItsOwnHeaderRow()
        {
            panel.ShowFromRoot(CommandsPage.Root());
            panel.OpenSearch();
            var heading = (TMP_Text)TestPanel.Field(panel, "title");
            var input = (TMP_InputField)TestPanel.Field(panel, "searchInput");
            Assert.IsTrue(heading.gameObject.activeSelf);
            Assert.IsTrue(input.gameObject.activeSelf);
            var titleCorners = new Vector3[4];
            var searchCorners = new Vector3[4];
            heading.rectTransform.GetWorldCorners(titleCorners);
            ((RectTransform)input.transform).GetWorldCorners(searchCorners);
            Assert.LessOrEqual(searchCorners[1].y, titleCorners[0].y, "search must not cover the page title");
            panel.CloseSearch();
            Assert.IsFalse(input.gameObject.activeSelf);
        }

        [Test]
        public void EditableValue_KeepsMoreBesideLabel_InOneOpaqueCard()
        {
            panel.ShowFromRoot(new DebugPage("fields", p => NodeRenderer.Render(p,
                Node.Value("tag", () => "Untagged", _ => { }), (_, _) => { })));
            var row = TestPanel.Rows(panel)[0];
            Assert.IsNull(row.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(1f, row.GetComponent<Image>().color.a);
            var label = new Vector3[4];
            var more = new Vector3[4];
            row.label.rectTransform.GetWorldCorners(label);
            ((RectTransform)row.more.transform).GetWorldCorners(more);
            Assert.LessOrEqual(label[2].x, more[0].x, "label must not overlap its more button");
            Assert.Greater(more[2].y, label[0].y, "more belongs beside the label, not below the input");
            var inputCorners = new Vector3[4];
            ((RectTransform)row.input.transform).GetWorldCorners(inputCorners);
            Assert.LessOrEqual(inputCorners[2].x, more[0].x, "the actions target must not intercept text editing");
        }
    }
}
