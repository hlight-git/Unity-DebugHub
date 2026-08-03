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

            Assert.AreEqual(1, content.GetComponentsInChildren<Dropdown>(false).Length, "enum should be a dropdown");
            Assert.AreEqual(1, content.GetComponentsInChildren<Toggle>(false).Length, "bool should be a toggle");

            var inputs = content.GetComponentsInChildren<InputField>(false);
            Assert.AreEqual(2, inputs.Length, "int and Vector3 should both be input fields");
            Assert.AreEqual(InputField.ContentType.IntegerNumber, inputs[0].contentType);
            Assert.AreEqual(InputField.ContentType.Standard, inputs[1].contentType);
        }

        [Test]
        public void AddField_ReportsSelectedEnumValueImmediately()
        {
            var captured = string.Empty;
            panel.Show(new DebugPage("Enum", p => p.AddField("enum", typeof(LogType), nameof(LogType.Assert), v => captured = v)));

            Assert.AreEqual(nameof(LogType.Assert), captured);
        }
    }
}
