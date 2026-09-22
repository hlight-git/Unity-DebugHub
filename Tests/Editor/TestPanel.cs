using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    /// Dựng panel từ prefab thật cho test. Pool trả row về Content ở trạng thái inactive, nên
    /// "row đang hiện" vẫn là "child đang active" — template cũng inactive và không bao giờ
    /// nằm trong spawnedRows.
    internal static class TestPanel
    {
        private const string PREFAB_PATH = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";

        private static readonly Dictionary<DebugHubPanel, GameObject> roots = new();

        public static DebugHubPanel Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, "DebugHub prefab not found");
            var instance = Object.Instantiate(prefab);
            var panel = instance.GetComponentInChildren<DebugHubPanel>(true);
            Assert.IsNotNull(panel, "DebugHubPanel component missing on prefab");
            roots[panel] = instance;
            return panel;
        }

        public static void Destroy(DebugHubPanel panel)
        {
            if (panel && roots.TryGetValue(panel, out var root)) Object.DestroyImmediate(root);
            roots.Remove(panel);
        }

        public static List<DebugHubRow> Rows(DebugHubPanel panel)
        {
            return (List<DebugHubRow>)Field("spawnedRows").GetValue(panel);
        }

        public static List<string> LabelsOf(DebugHubPanel panel)
        {
            var labels = new List<string>();
            foreach (var row in Rows(panel))
            {
                if (row && row.label) labels.Add(row.label.text);
            }
            return labels;
        }

        public static void ClickRowContaining(DebugHubPanel panel, string fragment)
        {
            foreach (var row in Rows(panel))
            {
                if (!row || !row.label || !row.label.text.Contains(fragment) || !row.button) continue;
                row.button.onClick.Invoke();
                return;
            }
            Assert.Fail($"không thấy row nào chứa \"{fragment}\"");
        }

        public static ScrollRect ScrollOf(DebugHubPanel panel) => (ScrollRect)Field("scrollRect").GetValue(panel);

        public static Button SearchButtonOf(DebugHubPanel panel) => (Button)Field("searchButton").GetValue(panel);

        public static GameObject ToastOf(DebugHubPanel panel) => ((DebugHubToast)Field("toast").GetValue(panel)).gameObject;

        public static RepeatButton BuildRepeatButton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            var instance = Object.Instantiate(prefab);
            return instance.GetComponentInChildren<RepeatButton>(true);
        }

        private static FieldInfo Field(string name)
        {
            return typeof(DebugHubPanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
