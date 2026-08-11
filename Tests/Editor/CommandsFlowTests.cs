using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IngameDebugConsole;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    /// Đi đúng luồng người dùng: mở Commands -> chọn category -> chọn command -> nhập param -> Run,
    /// và back bằng cách bấm background. Chạy được ở EditMode nên không cần click tay trong Play Mode.
    public class CommandsFlowTests
    {
        private const string PREFAB_PATH = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
        private const float ROW_HEIGHT = 120f;

        private static int lastInt;
        private static bool noArgCalled;
        private static int lastMixedAmount;
        private static LogType lastMixedType;

        private static void TakeInt(int value) => lastInt = value;
        private static void NoArg() => noArgCalled = true;

        private static void TakeMixed(int amount, LogType type)
        {
            lastMixedAmount = amount;
            lastMixedType = type;
        }

        private GameObject instance;
        private DebugHubPanel panel;
        private Transform content;

        [SetUp]
        public void SetUp()
        {
            DebugLogConsole.AddCommand<int>("flowtest.takeint", "test", TakeInt);
            DebugLogConsole.AddCommand("flowtest.noarg", "test", NoArg);
            DebugLogConsole.AddCommand<int, LogType>("flowtest.mixed", "test", TakeMixed);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            instance = Object.Instantiate(prefab);
            panel = instance.GetComponentInChildren<DebugHubPanel>(true);
            content = panel.transform.Find("Window/Scroll View/Viewport/Content");
        }

        [TearDown]
        public void TearDown()
        {
            DebugLogConsole.RemoveCommand("flowtest.takeint");
            DebugLogConsole.RemoveCommand("flowtest.noarg");
            DebugLogConsole.RemoveCommand("flowtest.mixed");
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
            var text = row.GetComponent<Text>();
            if (text != null) return text.text;
            var label = row.transform.Find("Label");
            if (label != null) return label.GetComponent<Text>().text;
            return row.GetComponentInChildren<Text>(true).text;
        }

        private void Click(string labelStartsWith)
        {
            var row = Rows().FirstOrDefault(r => LabelOf(r).StartsWith(labelStartsWith));
            Assert.IsNotNull(row, $"no row starting with '{labelStartsWith}'. Rows: {string.Join(" | ", Rows().Select(LabelOf))}");
            var button = row.GetComponent<Button>();
            Assert.IsNotNull(button, $"row '{labelStartsWith}' has no button");
            button.onClick.Invoke();
        }

        [Test]
        public void CommandsRoot_ListsCategories()
        {
            panel.Show(CommandsPage.Root());

            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("flowtest")),
                "category flowtest missing. Rows: " + string.Join(" | ", Rows().Select(LabelOf)));
        }

        [Test]
        public void CommandRows_ShowNameOnly_WithoutParameterSignature()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");

            foreach (var row in Rows())
            {
                Assert.IsFalse(LabelOf(row).Contains("["), "row label must not carry the parameter signature: " + LabelOf(row));
            }
            Assert.IsTrue(Rows().Any(r => LabelOf(r) == "flowtest.takeint"), "expected a row labelled exactly 'flowtest.takeint'");
        }

        [Test]
        public void CommandWithoutParams_RunsOnClick()
        {
            noArgCalled = false;
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("flowtest.noarg");

            Assert.IsTrue(noArgCalled);
        }

        [Test]
        public void CommandWithParams_OpensFieldPage_AndRunsTypedValue()
        {
            lastInt = 0;
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("flowtest.takeint");

            var input = content.GetComponentsInChildren<InputField>(false).Single();
            Assert.AreEqual(InputField.ContentType.IntegerNumber, input.contentType);

            input.text = "7";
            Click("Run");

            Assert.AreEqual(7, lastInt);
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
            Click("flowtest.mixed");

            content.GetComponentsInChildren<InputField>(false).Single().text = "7";

            Click("type:");
            Click(nameof(LogType.Exception));

            Assert.AreEqual("7", content.GetComponentsInChildren<InputField>(false).Single().text,
                "typed value must survive the trip to the choice page");

            Click("Run");
            Assert.AreEqual(7, lastMixedAmount);
            Assert.AreEqual(LogType.Exception, lastMixedType);
        }

        [Test]
        public void Background_ClosesPanel_RegardlessOfStackDepth()
        {
            typeof(DebugHubPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(panel, null);

            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("flowtest.takeint");
            Assert.AreEqual(1, content.GetComponentsInChildren<InputField>(false).Length, "should be on the params page");

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

            panel.Pop();
            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("flowtest  ")), "back must return to the category list");
            Assert.IsFalse(back.gameObject.activeSelf);
        }
    }
}
