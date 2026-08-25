using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    /// Đi đúng luồng người dùng: mở Commands -> vào thư mục -> chọn command -> nhập param -> Run,
    /// bật/tắt row tại chỗ, xác nhận, tìm kiếm, và mở lại panel đúng page cũ.
    /// Chạy được ở EditMode nên không cần click tay trong Play Mode.
    public class CommandsFlowTests
    {
        private const string PREFAB_PATH = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
        private const string RECENT_KEY = "DebugHub.RecentCommands";
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

        private readonly List<DebugCommand> registered = new();
        private GameObject instance;
        private DebugHubPanel panel;
        private Transform content;
        private string recentBackup;

        [SetUp]
        public void SetUp()
        {
            recentBackup = PlayerPrefs.GetString(RECENT_KEY, string.Empty);
            PlayerPrefs.DeleteKey(RECENT_KEY);

            lastInt = 0;
            noArgCalled = false;
            toggled = false;

            registered.Add(DebugCommands.Add<int>(null, "flowtest.takeint", "Nhận một số", TakeInt));
            registered.Add(DebugCommands.Add(null, "flowtest.noarg", "Chạy ngay", NoArg));
            registered.Add(DebugCommands.Add<int, LogType>(null, "flowtest.mixed", "Số và enum", TakeMixed));
            registered.Add(DebugCommands.AddToggle(null, "flowtest.flag", "Bật/tắt cờ",
                () => toggled, value => toggled = value));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            instance = Object.Instantiate(prefab);
            panel = instance.GetComponentInChildren<DebugHubPanel>(true);
            content = panel.transform.Find("Window/Scroll View/Viewport/Content");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var command in registered) DebugCommands.Remove(command);
            registered.Clear();
            PlayerPrefs.SetString(RECENT_KEY, recentBackup);
            Object.DestroyImmediate(instance);
        }

        private List<GameObject> Rows()
        {
            var rows = new List<GameObject>();
            foreach (Transform child in content)
            {
                if (child.gameObject.activeSelf) rows.Add(child.gameObject);
            }
            return rows;
        }

        private static string LabelOf(GameObject row)
        {
            var text = row.GetComponent<TMP_Text>();
            if (text != null) return text.text;
            var label = row.transform.Find("Label");
            if (label != null) return label.GetComponent<TMP_Text>().text;
            return row.GetComponentInChildren<TMP_Text>(true).text;
        }

        private void Click(string labelStartsWith)
        {
            var row = Rows().FirstOrDefault(r => LabelOf(r).StartsWith(labelStartsWith));
            Assert.IsNotNull(row, $"no row starting with '{labelStartsWith}'. Rows: {string.Join(" | ", Rows().Select(LabelOf))}");
            var button = row.GetComponent<Button>();
            Assert.IsNotNull(button, $"row '{labelStartsWith}' has no button");
            button.onClick.Invoke();
        }

        private DebugHubRow Row(string labelStartsWith)
        {
            var row = Rows().FirstOrDefault(r => LabelOf(r).StartsWith(labelStartsWith));
            Assert.IsNotNull(row, $"no row starting with '{labelStartsWith}'. Rows: {string.Join(" | ", Rows().Select(LabelOf))}");
            return row.GetComponent<DebugHubRow>();
        }

        private Toggle ToggleRow(string labelStartsWith)
        {
            var row = Rows().FirstOrDefault(r => LabelOf(r).StartsWith(labelStartsWith));
            Assert.IsNotNull(row, $"no row starting with '{labelStartsWith}'. Rows: {string.Join(" | ", Rows().Select(LabelOf))}");
            var toggle = row.GetComponent<Toggle>();
            Assert.IsNotNull(toggle, $"row '{labelStartsWith}' is not a switch");
            return toggle;
        }

        [Test]
        public void CommandsRoot_ListsFoldersAndSearch()
        {
            panel.Show(CommandsPage.Root());

            var labels = Rows().Select(LabelOf).ToList();
            Assert.IsTrue(labels.Any(label => label.StartsWith("flowtest")),
                "thư mục flowtest thiếu. Rows: " + string.Join(" | ", labels));
            Assert.IsTrue(labels.Any(label => label.StartsWith("Search")), "root phải có row Search");
        }

        /// Recent / Search / Built-in là điều hướng của chính hub, không phải thư mục command của game
        /// — phải khác màu để không đọc lẫn vào danh sách.
        [Test]
        public void HubShortcutRows_UseTheirOwnColor()
        {
            registered.Add(DebugCommands.Add(panel, "builtintest.thing", "Đồ có sẵn", NoArg));
            DebugCommands.TryRun(registered[0], new[] { "1" }, out _);   // để Recent có gì đó

            panel.Show(CommandsPage.Root());

            var folder = Row("flowtest").label.color;
            foreach (var shortcut in new[] { "Recent", "Search", "Built-in" })
            {
                Assert.AreNotEqual(folder, Row(shortcut).label.color, $"row {shortcut} phải khác màu thư mục");
            }
            Assert.AreEqual(Row("Recent").label.color, Row("Built-in").label.color, "ba row này cùng một màu");
        }

        /// Command của package (owner nằm trong assembly của hub) bị dồn vào một menu riêng ở cuối,
        /// không chen vào giữa cheat của game.
        [Test]
        public void BuiltInCommands_LiveInTheirOwnMenuAtTheBottom()
        {
            // panel là component của package nên command lấy nó làm owner được coi là built-in.
            registered.Add(DebugCommands.Add(panel, "builtintest.thing", "Đồ có sẵn", NoArg));

            panel.Show(CommandsPage.Root());
            var labels = Rows().Select(LabelOf).ToList();

            Assert.AreEqual("Built-in", labels[labels.Count - 1], "menu built-in phải nằm cuối cùng");
            Assert.IsFalse(labels.Any(label => label.StartsWith("builtintest")),
                "cây của game không được chứa command built-in. Rows: " + string.Join(" | ", labels));

            Click("Built-in");
            Assert.IsTrue(Rows().Select(LabelOf).Any(label => label.StartsWith("builtintest")),
                "vào menu built-in thì phải thấy nó");
            Assert.IsFalse(Rows().Select(LabelOf).Any(label => label.StartsWith("flowtest")),
                "và không thấy cheat của game ở trong đó");
        }

        /// Row hiện tên lá + description, không hiện cả đường dẫn và không hiện chữ ký tham số.
        [Test]
        public void CommandRows_ShowLeafNameAndDescription()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");

            var labels = Rows().Select(LabelOf).ToList();
            foreach (var label in labels)
            {
                Assert.IsFalse(label.Contains("["), "row không được mang chữ ký tham số: " + label);
                Assert.IsFalse(label.StartsWith("flowtest."), "row chỉ hiện tên lá: " + label);
            }
            Assert.IsTrue(labels.Any(label => label.StartsWith("takeint\n") && label.Contains("Nhận một số")),
                "row phải có description ở dòng thứ hai. Rows: " + string.Join(" | ", labels));
        }

        /// Số lượng command nằm ở cột phụ căn phải, không ghép vào tên (tên dài ngắn khác nhau thì
        /// số trôi theo, dò bằng mắt rất mệt).
        [Test]
        public void FolderRow_ShowsCountInRightAlignedColumn()
        {
            panel.Show(CommandsPage.Root());

            var row = Rows().First(r => LabelOf(r).StartsWith("flowtest"))
                .GetComponent<DebugHubRow>();

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
            Click("flowtest");

            var row = Rows().First(r => LabelOf(r).StartsWith("flag")).GetComponent<DebugHubRow>();
            Assert.IsNotNull(row.knob, "switch phải có núm");
            var off = row.knob.anchoredPosition.x;

            row.toggle.isOn = true;

            Assert.Greater(row.knob.anchoredPosition.x, off + 1f, "núm phải chạy sang phải khi bật");
        }

        /// Chạy xong thì đóng panel để nhìn game (DismissMode mặc định của command dạng action).
        [Test]
        public void CommandWithoutParams_RunsOnClick_AndClosesPanel()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("noarg");

            Assert.IsTrue(noArgCalled);
            Assert.IsFalse(panel.IsOpen, "action mặc định đóng panel sau khi chạy");
        }

        /// HideHub phải đóng panel cả khi trong scene không có DebugHub (panel dùng riêng, test):
        /// DebugHub.Visible không có instance để tác động thì panel sẽ nằm nguyên đó.
        [Test]
        public void HideHubCommand_ClosesPanel_WithoutHubInstance()
        {
            registered.Add(DebugCommands.Add(null, "flowtest.hide", "Ẩn hub", NoArg).HidesHub());

            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("hide");

            Assert.IsTrue(noArgCalled);
            Assert.IsFalse(panel.IsOpen);
        }

        /// Dòng kết quả chỉ dành cho command đọc dữ liệu (Dismiss = Stay) và có in ra gì. Command đổi
        /// state của game thì im, kể cả khi luồng bên trong nó có log — đó là ca của level.*.
        [Test]
        public void Result_ShowsOnlyForReadingCommandsThatLogged()
        {
            var toast = instance.transform.Find("Toast").gameObject;
            registered.Add(DebugCommands.Add(null, "flowtest.quiet", "Không in gì", NoArg).Stays());
            registered.Add(DebugCommands.Add(null, "flowtest.reads", "Đọc dữ liệu",
                () => UnityEngine.Debug.Log("coins: 120")).Stays());
            registered.Add(DebugCommands.Add(null, "flowtest.changes", "Đổi state, luồng bên trong có log",
                () => UnityEngine.Debug.Log("[Flow] loading level 5")));

            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("quiet");
            Assert.IsFalse(toast.activeSelf, "không in gì thì không hiện dòng kết quả");

            Click("reads");
            Assert.IsTrue(toast.activeSelf);
            StringAssert.Contains("coins: 120", toast.GetComponentInChildren<TMP_Text>(true).text);

            Click("changes");
            Assert.IsFalse(toast.activeSelf, "command đổi state thì im, dù trong lúc chạy có log");
        }

        [Test]
        public void Result_ShowsErrorEvenWhenNothingWasLogged()
        {
            var toast = instance.transform.Find("Toast").gameObject;
            registered.Add(DebugCommands.Add(null, "flowtest.boom", "Nổ",
                () => throw new System.InvalidOperationException("bùm")));

            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("boom");

            Assert.IsTrue(toast.activeSelf, "lỗi thì luôn phải hiện");
            var text = toast.GetComponentInChildren<TMP_Text>(true).text;
            StringAssert.Contains("flowtest.boom", text);
            StringAssert.Contains("bùm", text);
        }

        [Test]
        public void CommandWithParams_OpensFieldPage_AndRunsTypedValue()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("takeint");

            var input = content.GetComponentsInChildren<TMP_InputField>(false).Single();
            Assert.AreEqual(TMP_InputField.ContentType.IntegerNumber, input.contentType);

            input.text = "7";
            Click("Run");

            Assert.AreEqual(7, lastInt);
        }

        /// Row bật/tắt: đọc đúng state hiện tại, bấm là áp ngay, và panel ở lại để bật tắt tiếp.
        [Test]
        public void ToggleCommand_AppliesImmediately_AndKeepsPanelOpen()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");

            var toggle = ToggleRow("flag");
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
            Click("flowtest");
            Click("mixed");

            content.GetComponentsInChildren<TMP_InputField>(false).Single().text = "7";

            Click("type:");
            Click(nameof(LogType.Exception));

            Assert.AreEqual("7", content.GetComponentsInChildren<TMP_InputField>(false).Single().text,
                "typed value must survive the trip to the choice page");

            Click("Run");
            Assert.AreEqual(7, lastMixedAmount);
            Assert.AreEqual(LogType.Exception, lastMixedType);
        }

        /// Giá trị nhập lần trước còn đó, khỏi gõ lại mỗi lần vào.
        [Test]
        public void ParamsPage_RemembersLastTypedValue()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("takeint");
            content.GetComponentsInChildren<TMP_InputField>(false).Single().text = "13";
            panel.Pop();

            Click("takeint");

            Assert.AreEqual("13", content.GetComponentsInChildren<TMP_InputField>(false).Single().text);
        }

        [Test]
        public void ConfirmCommand_AsksBeforeRunning()
        {
            var command = DebugCommands.Add(null, "flowtest.danger", "Nguy hiểm", NoArg);
            command.Confirm = true;
            registered.Add(command);

            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("danger");

            Assert.IsFalse(noArgCalled, "phải hỏi lại trước khi chạy");
            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("Huỷ")), "page xác nhận phải có nút Huỷ");

            Click("Chạy");
            Assert.IsTrue(noArgCalled);
        }

        /// Đóng panel rồi mở lại phải về đúng page đang xem, không bò lại từ root.
        [Test]
        public void ReopeningPanel_ReturnsToThePageItWasClosedOn()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("takeint");
            var title = panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>();
            Assert.AreEqual("flowtest.takeint", title.text);

            panel.Close();
            panel.Show(CommandsPage.Root());

            Assert.AreEqual("flowtest.takeint", title.text, "mở lại phải ở đúng page cũ");
            Assert.AreEqual(1, content.GetComponentsInChildren<TMP_InputField>(false).Length);
        }

        [Test]
        public void Reset_GoesBackToRootPage()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            panel.Close();

            panel.ShowFromRoot(CommandsPage.Root());

            Assert.AreEqual("Commands", panel.transform.Find("Window/Header/Title").GetComponent<TMP_Text>().text);
        }

        [Test]
        public void Search_ListsMatchesByFullPath()
        {
            panel.Show(CommandsPage.Root());
            Click("Search");

            content.GetComponentsInChildren<TMP_InputField>(false).Single().text = "takeint";
            Click("Tìm");

            var labels = Rows().Select(LabelOf).ToList();
            Assert.IsTrue(labels.Any(label => label.StartsWith("flowtest.takeint")),
                "kết quả tìm phải hiện đường dẫn đầy đủ. Rows: " + string.Join(" | ", labels));
        }

        [Test]
        public void Recent_ListsWhatWasJustRun()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("noarg");

            panel.ShowFromRoot(CommandsPage.Root());
            Click("Recent");

            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("flowtest.noarg")),
                "command vừa chạy phải nằm trong Recent");
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
            Click("flowtest");
            Click("takeint");
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

            var rows = Rows();
            Assert.AreEqual(3, rows.Count);
            var total = 0f;
            foreach (var row in rows)
            {
                var rect = (RectTransform)row.transform;
                Assert.GreaterOrEqual(rect.rect.height, ROW_HEIGHT - 0.5f,
                    $"row '{LabelOf(row)}' is {rect.rect.height} tall, below the {ROW_HEIGHT} touch target");
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

            Click("flowtest");
            Assert.IsTrue(back.gameObject.activeSelf, "sub page must show the back button");

            // Không bấm back.onClick: EditMode instantiate prefab đã chạy Awake nên gọi Awake tay lần
            // nữa sẽ gắn listener Pop hai lần và pop hai tầng.
            panel.Pop();
            Assert.IsTrue(Rows().Any(r => LabelOf(r) == "flowtest"), "back must return to the folder list");
            Assert.IsFalse(back.gameObject.activeSelf);
        }
    }
}
