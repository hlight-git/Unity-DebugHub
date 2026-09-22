using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    /// Đi đúng luồng người dùng: mở Commands -> vào thư mục -> chọn node -> nhập param -> Run,
    /// bật/tắt row tại chỗ, xác nhận, và mở lại panel đúng page cũ. Search theo từ khoá nằm ở
    /// SearchTests (header search, không còn row "Search" riêng ở gốc như bản cũ).
    /// Chạy được ở EditMode nên không cần click tay trong Play Mode.
    public class CommandsFlowTests
    {
        private const string PREFAB_PATH = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
        private const string LAST_KEY = "DebugHub.LastCommand";
        private const float ROW_HEIGHT = 120f;

        private static int lastInt;
        private static bool noArgCalled;
        private static bool toggled;
        private static int lastMixedAmount;
        private static LogType lastMixedType;

        private static void TakeInt(int value) => lastInt = value;
        private static void NoArg() => noArgCalled = true;

        private static void TakeMixed(int amount, LogType type)
        {
            lastMixedAmount = amount;
            lastMixedType = type;
        }

        private readonly List<DebugNode> registered = new();
        private GameObject instance;
        private DebugHubPanel panel;
        private Transform content;
        private string lastCommandBackup;

        private T Track<T>(T node) where T : DebugNode
        {
            registered.Add(node);
            return node;
        }

        [SetUp]
        public void SetUp()
        {
            lastCommandBackup = PlayerPrefs.GetString(LAST_KEY, string.Empty);
            PlayerPrefs.DeleteKey(LAST_KEY);

            lastInt = 0;
            noArgCalled = false;
            toggled = false;

            Track(DebugHub.Add<int>(null, "flowtest.takeint", "Nhận một số", TakeInt));
            Track(DebugHub.Add(null, "flowtest.noarg", "Chạy ngay", NoArg));
            Track(DebugHub.Add<int, LogType>(null, "flowtest.mixed", "Số và enum", TakeMixed));
            Track(DebugHub.AddValue(null, "flowtest.flag", "Bật/tắt cờ", () => toggled, value => toggled = value));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            instance = Object.Instantiate(prefab);
            panel = instance.GetComponentInChildren<DebugHubPanel>(true);
            content = panel.transform.Find("Window/Scroll View/Viewport/Content");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var node in registered) DebugHub.Remove(node);
            registered.Clear();
            PlayerPrefs.SetString(LAST_KEY, lastCommandBackup);
            Object.DestroyImmediate(instance);
        }

        /// Row hiện tên lá + description, không hiện cả đường dẫn và không hiện chữ ký tham số.
        [Test]
        public void CommandRows_ShowLeafNameAndDescription()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");

            var labels = TestPanel.LabelsOf(panel);
            foreach (var label in labels)
            {
                Assert.IsFalse(label.Contains("["), "row không được mang chữ ký tham số: " + label);
                Assert.IsFalse(label.StartsWith("flowtest."), "row chỉ hiện tên lá: " + label);
            }
            Assert.IsTrue(labels.Exists(label => label.StartsWith("takeint\n") && label.Contains("Nhận một số")),
                "row phải có description ở dòng thứ hai. Rows: " + string.Join(" | ", labels));
        }

        /// Số lượng command nằm ở cột phụ căn phải, không ghép vào tên (tên dài ngắn khác nhau thì
        /// số trôi theo, dò bằng mắt rất mệt).
        [Test]
        public void FolderRow_ShowsCountInRightAlignedColumn()
        {
            panel.Show(CommandsPage.Root());

            var row = TestPanel.Rows(panel).First(r => r.label.text.StartsWith("flowtest"));

            Assert.AreEqual("flowtest", row.label.text, "tên thư mục không được kèm số");
            Assert.IsNotNull(row.detail, "row nav phải có cột chữ phụ");
            Assert.AreEqual("4", row.detail.text);
            Assert.AreEqual(TextAlignmentOptions.Right, row.detail.alignment);
        }

        /// Switch tắt mà không có núm thì chỉ là một thanh trống, nhìn không ra là switch.
        [Test]
        public void ToggleRow_KnobMovesWithState()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");

            var row = TestPanel.Rows(panel).First(r => r.label.text.StartsWith("flag"));
            Assert.IsNotNull(row.knob, "switch phải có núm");
            var off = row.knob.anchoredPosition.x;

            row.toggle.isOn = true;

            Assert.Greater(row.knob.anchoredPosition.x, off + 1f, "núm phải chạy sang phải khi bật");
        }

        /// Chạy xong thì đóng panel để nhìn game (DismissMode mặc định của node dạng action).
        [Test]
        public void CommandWithoutParams_RunsOnClick_AndClosesPanel()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "noarg");

            Assert.IsTrue(noArgCalled);
            Assert.IsFalse(panel.IsOpen, "action mặc định đóng panel sau khi chạy");
        }

        /// HideHub phải đóng panel cả khi trong scene không có DebugHub (panel dùng riêng, test):
        /// DebugHub.Visible không có instance để tác động thì panel sẽ nằm nguyên đó.
        [Test]
        public void HideHubCommand_ClosesPanel_WithoutHubInstance()
        {
            Track(DebugHub.Add(null, "flowtest.hide", "Ẩn hub", NoArg).HidesHub());

            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "hide");

            Assert.IsTrue(noArgCalled);
            Assert.IsFalse(panel.IsOpen);
        }

        /// Dòng kết quả chỉ dành cho node đọc dữ liệu (Dismiss = Stay) và có in ra gì. Node đổi
        /// state của game thì im, kể cả khi luồng bên trong nó có log — đó là ca của level.*.
        [Test]
        public void Result_ShowsOnlyForReadingCommandsThatLogged()
        {
            var toast = instance.transform.Find("Toast").gameObject;
            Track(DebugHub.Add(null, "flowtest.quiet", "Không in gì", NoArg).Stays());
            Track(DebugHub.Add(null, "flowtest.reads", "Đọc dữ liệu",
                () => UnityEngine.Debug.Log("coins: 120")).Stays());
            Track(DebugHub.Add(null, "flowtest.changes", "Đổi state, luồng bên trong có log",
                () => UnityEngine.Debug.Log("[Flow] loading level 5")));

            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "quiet");
            Assert.IsFalse(toast.activeSelf, "không in gì thì không hiện dòng kết quả");

            TestPanel.ClickRowContaining(panel, "reads");
            Assert.IsTrue(toast.activeSelf);
            StringAssert.Contains("coins: 120", toast.GetComponentInChildren<TMP_Text>(true).text);

            TestPanel.ClickRowContaining(panel, "changes");
            Assert.IsFalse(toast.activeSelf, "command đổi state thì im, dù trong lúc chạy có log");
        }

        [Test]
        public void Result_ShowsErrorEvenWhenNothingWasLogged()
        {
            var toast = instance.transform.Find("Toast").gameObject;
            Track(DebugHub.Add(null, "flowtest.boom", "Nổ",
                () => throw new System.InvalidOperationException("bùm")));

            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: bùm");
            TestPanel.ClickRowContaining(panel, "boom");

            Assert.IsTrue(toast.activeSelf, "lỗi thì luôn phải hiện");
            var text = toast.GetComponentInChildren<TMP_Text>(true).text;
            StringAssert.Contains("flowtest.boom", text);
            StringAssert.Contains("bùm", text);
        }

        [Test]
        public void CommandWithParams_OpensFieldPage_AndRunsTypedValue()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "takeint");

            var input = content.GetComponentsInChildren<TMP_InputField>(false).Single();
            Assert.AreEqual(TMP_InputField.ContentType.IntegerNumber, input.contentType);

            input.text = "7";
            TestPanel.ClickRowContaining(panel, "Run");

            Assert.AreEqual(7, lastInt);
        }

        /// Row bật/tắt: đọc đúng state hiện tại, bấm là áp ngay, và panel ở lại để bật tắt tiếp.
        [Test]
        public void ToggleCommand_AppliesImmediately_AndKeepsPanelOpen()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");

            var toggle = TestPanel.Rows(panel).First(r => r.label.text.StartsWith("flag")).toggle;
            Assert.IsFalse(toggle.isOn, "switch phải hiện state hiện tại (đang tắt)");

            toggle.isOn = true;

            Assert.IsTrue(toggled, "bật switch là áp ngay, không cần nút Run");
            Assert.IsTrue(panel.IsOpen, "row bật/tắt phải ở lại page");
        }

        /// Page được build lại mỗi lần Push/Pop, nên đi chọn enum rồi quay lại không được mất
        /// giá trị đã gõ ở các field khác.
        [Test]
        public void PickingEnum_KeepsValuesTypedInOtherFields()
        {
            lastMixedAmount = 0;
            lastMixedType = LogType.Log;

            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "mixed");

            content.GetComponentsInChildren<TMP_InputField>(false).Single().text = "7";

            TestPanel.ClickRowContaining(panel, "type:");
            TestPanel.ClickRowContaining(panel, nameof(LogType.Exception));

            Assert.AreEqual("7", content.GetComponentsInChildren<TMP_InputField>(false).Single().text,
                "typed value must survive the trip to the choice page");

            TestPanel.ClickRowContaining(panel, "Run");
            Assert.AreEqual(7, lastMixedAmount);
            Assert.AreEqual(LogType.Exception, lastMixedType);
        }

        /// Giá trị nhập lần trước còn đó, khỏi gõ lại mỗi lần vào.
        [Test]
        public void ParamsPage_RemembersLastTypedValue()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "takeint");
            content.GetComponentsInChildren<TMP_InputField>(false).Single().text = "13";
            panel.Pop();

            TestPanel.ClickRowContaining(panel, "takeint");

            Assert.AreEqual("13", content.GetComponentsInChildren<TMP_InputField>(false).Single().text);
        }

        [Test]
        public void ConfirmCommand_AsksBeforeRunning()
        {
            Track(DebugHub.Add(null, "flowtest.danger", "Nguy hiểm", NoArg).Confirms());

            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "danger");

            Assert.IsFalse(noArgCalled, "phải hỏi lại trước khi chạy");
            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.StartsWith("Huỷ")), "page xác nhận phải có nút Huỷ");

            TestPanel.ClickRowContaining(panel, "Chạy");
            Assert.IsTrue(noArgCalled);
        }

        /// Đóng panel rồi mở lại phải về đúng page đang xem, không bò lại từ root.
        [Test]
        public void ReopeningPanel_ReturnsToThePageItWasClosedOn()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "takeint");
            var title = panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>();
            Assert.AreEqual("takeint", title.text);

            panel.Close();
            panel.Show(CommandsPage.Root());

            Assert.AreEqual("takeint", title.text, "mở lại phải ở đúng page cũ");
            Assert.AreEqual(1, content.GetComponentsInChildren<TMP_InputField>(false).Length);
        }

        [Test]
        public void Reset_GoesBackToRootPage()
        {
            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            panel.Close();

            panel.ShowFromRoot(CommandsPage.Root());

            Assert.AreEqual("Commands", panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>().text);
        }

        [Test]
        public void Background_ClosesPanel_RegardlessOfStackDepth()
        {
            // EditMode không gọi Awake khi Instantiate prefab (có lần gọi, có lần không, tuỳ domain
            // reload) nên gọi tay để listener của background chắc chắn được gắn. Gọi hai lần cũng
            // không sao vì Close() không phải thao tác cộng dồn — khác Pop(), nên test nút back
            // không dùng cách này.
            typeof(DebugHubPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(panel, null);

            panel.Show(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "flowtest");
            TestPanel.ClickRowContaining(panel, "takeint");
            Assert.AreEqual(1, content.GetComponentsInChildren<TMP_InputField>(false).Length, "should be on the params page");

            panel.transform.Find("BG").GetComponent<Button>().onClick.Invoke();

            Assert.IsFalse(panel.IsOpen, "background must close the panel even from a deep page");
        }

        [Test]
        public void SpawnedRows_KeepRowHeight_AndStackWithoutOverlap()
        {
            panel.Show(new DebugPage("Layout", page =>
            {
                page.AddButton("a", () => { });
                page.AddToggle("b", false, _ => { });
                page.AddField("c", typeof(int), "0", _ => { });
            }));

            LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);

            var rows = TestPanel.Rows(panel);
            Assert.AreEqual(3, rows.Count);
            var total = 0f;
            foreach (var row in rows)
            {
                var rect = (RectTransform)row.transform;
                Assert.GreaterOrEqual(rect.rect.height, ROW_HEIGHT - 0.5f,
                    $"row '{row.label.text}' is {rect.rect.height} tall, below the {ROW_HEIGHT} touch target");
                total += rect.rect.height;
            }

            var contentHeight = ((RectTransform)content).rect.height;
            Assert.GreaterOrEqual(contentHeight, total, "content must be tall enough to hold every row");
        }

        /// Back phải nhìn thấy được, không chỉ dựa vào việc bấm ra ngoài panel.
        [Test]
        public void BackButton_HiddenOnRootPage_VisibleDeeper_AndPops()
        {
            var back = panel.transform.Find("Window/Header/Back").GetComponent<Button>();

            panel.Show(CommandsPage.Root());
            Assert.IsFalse(back.gameObject.activeSelf, "root page has nowhere to go back to");

            TestPanel.ClickRowContaining(panel, "flowtest");
            Assert.IsTrue(back.gameObject.activeSelf, "sub page must show the back button");

            // Không bấm back.onClick: EditMode instantiate prefab đã chạy Awake nên gọi Awake tay lần
            // nữa sẽ gắn listener Pop hai lần và pop hai tầng.
            panel.Pop();
            Assert.IsTrue(TestPanel.LabelsOf(panel).Contains("flowtest"), "back must return to the folder list");
            Assert.IsFalse(back.gameObject.activeSelf);
        }

        [Test]
        public void Root_MixesPackageFoldersWithGameFolders_Alphabetically()
        {
            Track(DebugHub.Add(null, "aaa.one", "d", () => { }));
            Track(DebugHub.Add(null, "zzz.two", "d", () => { }));
            panel.ShowFromRoot(CommandsPage.Root());

            var labels = TestPanel.LabelsOf(panel);
            Assert.Less(labels.FindIndex(l => l.Contains("aaa")), labels.FindIndex(l => l.Contains("zzz")));
            Assert.IsFalse(labels.Exists(l => l.Contains("Built-in")));
            Assert.IsFalse(labels.Exists(l => l.Contains("Recent")));
        }

        [Test]
        public void ValueCommand_ShowsCurrentState_AndAppliesOnSubmit()
        {
            var on = false;
            Track(DebugHub.AddValue(null, "view.ui2", "d", () => on, v => on = v));
            panel.ShowFromRoot(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "view");

            TestPanel.Rows(panel)[0].toggle.onValueChanged.Invoke(true);

            Assert.IsTrue(on);
        }

        [Test]
        public void FolderCommand_BuildsItsNodesWhenOpened()
        {
            Track(DebugHub.AddFolder(null, "info.app2", "d",
                () => new DebugNode[] { Node.Value("bundle", () => "com.x") }));
            panel.ShowFromRoot(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "info");
            TestPanel.ClickRowContaining(panel, "app2");

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("bundle")));
        }

        [Test]
        public void DuplicatePathsInSameFolder_AreToldApartByArgCount()
        {
            Track(DebugHub.Add(null, "dup.x", "d", () => { }));
            Track(DebugHub.Add<int>(null, "dup.x", "d", _ => { }));
            panel.ShowFromRoot(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "dup");

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("1 args")));
        }

        /// Regression: BuildFolder/SearchAll từng ghi thẳng vào entry.Node.Label để hiện full path /
        /// nhãn kèm (N args) trên row — nhưng entry.Node là object sống, ParamsPage.For và trang xác
        /// nhận của Run đều lấy title từ chính node.Label đó. Ghi đè ở đây thì mở tiếp vào trang sau
        /// sẽ thấy full path (hoặc "(N args)") rò vào tiêu đề, dù trang đó chỉ cần tên lá.
        [Test]
        public void SearchResult_OpensParamsPage_WithLeafTitle_NotFullPath()
        {
            Track(DebugHub.Add<int>(null, "search.deep.thing", "d", _ => { }));
            panel.ShowFromRoot(CommandsPage.Root());
            panel.Query = "deep";

            TestPanel.ClickRowContaining(panel, "search.deep.thing");

            var title = panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>();
            Assert.AreEqual("thing", title.text,
                "mở từ kết quả search không được để full path rò vào tiêu đề trang sau");
        }

        [Test]
        public void DuplicateArityRow_OpensParamsPage_WithoutRowLabelLeakingIntoTitle()
        {
            Track(DebugHub.Add(null, "dup.y", "d", () => { }));
            Track(DebugHub.Add<int>(null, "dup.y", "d", _ => { }));
            panel.ShowFromRoot(CommandsPage.Root());
            TestPanel.ClickRowContaining(panel, "dup");

            TestPanel.ClickRowContaining(panel, "1 args");

            var title = panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>();
            Assert.AreEqual("y", title.text,
                "nhãn '(1 args)' chỉ ở trên row, không được rò vào tiêu đề trang params");
        }
    }
}
