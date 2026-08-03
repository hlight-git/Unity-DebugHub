using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    public class DebugHubPanelTests
    {
        private const string PREFAB_PATH = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";

        private GameObject instance;
        private DebugHubPanel panel;
        private Transform content;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, "DebugHub prefab not found");
            instance = Object.Instantiate(prefab);
            panel = instance.GetComponentInChildren<DebugHubPanel>(true);
            Assert.IsNotNull(panel, "DebugHubPanel component missing on prefab");
            content = panel.transform.Find("Window/Scroll View/Viewport/Content");
            Assert.IsNotNull(content, "Content transform not found at expected path");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(instance);
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

            panel.Show(new DebugPage("Long", p =>
            {
                for (var i = 0; i < 30; i++) p.AddButton("row " + i, () => { });
            }));
            var longHeight = window.rect.height;

            Assert.Less(shortHeight, 500f, $"one row should not give a {shortHeight} tall window");
            Assert.Less(shortHeight, longHeight, "window must grow with content");
            Assert.LessOrEqual(longHeight, 1500.5f, $"window must stay within maxWindowHeight, got {longHeight}");
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

            var inputs = content.GetComponentsInChildren<InputField>(false);
            Assert.AreEqual(2, inputs.Length, "int and Vector3 should both be input fields");
            Assert.AreEqual(InputField.ContentType.IntegerNumber, inputs[0].contentType);
            Assert.AreEqual(InputField.ContentType.Standard, inputs[1].contentType);
        }

        /// Scene không có EventSystem thì không gõ được password. Prefab phải tự mang một cái,
        /// inactive, để EventSystemHandler chỉ bật khi scene chưa có.
        [Test]
        public void Prefab_ShipsWithInactiveEventSystem_WiredToHandler()
        {
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
            StringAssert.Contains(nameof(LogType.Log), row.GetComponentInChildren<Text>(true).text);

            row.onClick.Invoke();
            var options = content.GetComponentsInChildren<Button>(false);
            Assert.AreEqual(System.Enum.GetNames(typeof(LogType)).Length, options.Length, "choice page must list every enum value");

            var wanted = System.Array.Find(options, o => o.GetComponentInChildren<Text>(true).text.StartsWith(nameof(LogType.Exception)));
            Assert.IsNotNull(wanted, "Exception option missing");
            wanted.onClick.Invoke();

            Assert.AreEqual(nameof(LogType.Exception), captured);
            Assert.AreEqual("Enum", panel.transform.Find("Window/Header/Title").GetComponent<Text>().text,
                "picking a value must return to the page that owns the field");
        }
    }
}
