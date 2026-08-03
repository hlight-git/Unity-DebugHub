using System.Collections.Generic;
using System.Linq;
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

        private static void TakeInt(int value) => lastInt = value;
        private static void NoArg() => noArgCalled = true;

        private GameObject instance;
        private DebugHubPanel panel;
        private Transform content;

        [SetUp]
        public void SetUp()
        {
            DebugLogConsole.AddCommand<int>("flowtest.takeint", "test", TakeInt);
            DebugLogConsole.AddCommand("flowtest.noarg", "test", NoArg);

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

        private void ClickBackground()
        {
            panel.transform.Find("BG").GetComponent<Button>().onClick.Invoke();
            // Awake không chạy ở EditMode nên listener của BG chưa được gắn -> gọi Pop trực tiếp.
            panel.Pop();
        }

        [Test]
        public void CommandsRoot_ListsCategories()
        {
            panel.Show(CommandsPage.Root());

            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("flowtest")),
                "category flowtest missing. Rows: " + string.Join(" | ", Rows().Select(LabelOf)));
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

        [Test]
        public void Background_GoesBackOneLevel()
        {
            panel.Show(CommandsPage.Root());
            Click("flowtest");
            Click("flowtest.takeint");
            Assert.AreEqual(1, content.GetComponentsInChildren<InputField>(false).Length, "should be on the params page");

            ClickBackground();
            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("flowtest.noarg")), "should be back on the category page");

            ClickBackground();
            Assert.IsTrue(Rows().Any(r => LabelOf(r).StartsWith("flowtest  (")), "should be back on the category list");

            ClickBackground();
            Assert.IsFalse(panel.IsOpen, "background on root page should close the panel");
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
            foreach (var row in rows)
            {
                var rect = (RectTransform)row.transform;
                Assert.AreEqual(ROW_HEIGHT, rect.rect.height, 0.5f, $"row '{LabelOf(row)}' has height {rect.rect.height}");
            }

            var contentHeight = ((RectTransform)content).rect.height;
            Assert.GreaterOrEqual(contentHeight, rows.Count * ROW_HEIGHT, "content must be tall enough to hold every row");
        }
    }
}
