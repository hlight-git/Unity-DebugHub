using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Nút repeat: chạy lại DebugRegistry.LastCommand, tự ẩn khi chưa có lệnh nào.
    public class RepeatTests
    {
        private const string LAST_KEY = "DebugHub.LastCommand";

        private readonly List<DebugNode> registered = new();
        private string lastBackup;

        [SetUp]
        public void SetUp()
        {
            lastBackup = PlayerPrefs.GetString(LAST_KEY, string.Empty);
            PlayerPrefs.DeleteKey(LAST_KEY);
            registered.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var node in registered) DebugHub.Remove(node);
            registered.Clear();
            PlayerPrefs.SetString(LAST_KEY, lastBackup);
        }

        private T Track<T>(T node) where T : DebugNode
        {
            registered.Add(node);
            return node;
        }

        [Test]
        public void Repeat_RunsTheLastCommandAgain_WithTheSameArguments()
        {
            var got = 0;
            Track(DebugHub.Add<int>(null, "level.goto3", "d", v => got = v));
            DebugHub.Execute("level.goto3 5", out _);
            got = 0;

            Assert.IsTrue(DebugHub.Execute(DebugRegistry.LastCommand, out _));
            Assert.AreEqual(5, got);
        }

        [Test]
        public void Repeat_IsClearedWhenTheArgumentsCannotBeWrittenDown()
        {
            var go = new GameObject("probe");
            try
            {
                Track(DebugHub.Add<int>(null, "level.ok", "d", _ => { }));
                Track(DebugHub.Add<GameObject>(null, "scene.pick", "d", _ => { }));
                DebugHub.Execute("level.ok 1", out _);           // có một lệnh lưu được trước đã
                Assert.AreEqual("level.ok 1", DebugRegistry.LastCommand);

                DebugHub.Execute("scene.pick probe", out _);

                Assert.IsEmpty(DebugRegistry.LastCommand,
                    "reference Unity không có biểu diễn độc lập với session — không được giữ lệnh cũ");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RepeatButton_HiddenWhenThereIsNoLastCommand()
        {
            PlayerPrefs.DeleteKey("DebugHub.LastCommand");
            var button = TestPanel.BuildRepeatButton();
            try
            {
                button.Enabled = true;
                Assert.IsFalse(button.gameObject.activeSelf);
            }
            // Root chứ không phải riêng button: BuildRepeatButton() dựng cả prefab DebugHub, chỉ xoá
            // button thì phần còn lại (DebugHub, Entry, Panel, Console...) rò rỉ vào scene test dùng chung.
            finally { Object.DestroyImmediate(button.transform.root.gameObject); }
        }

        /// Nút một chạm chạy lại lệnh cuối phải cho thấy đó là lệnh gì: icon + tên lá, và nút rộng ra
        /// vừa đủ tên.
        [Test]
        public void RepeatButton_ShowsTheLeafNameNextToItsIcon()
        {
            var repeat = TestPanel.BuildRepeatButton();
            try
            {
                repeat.gameObject.SetActive(true);
                repeat.Present("level.goto 5");

                var label = repeat.GetComponentInChildren<TMPro.TMP_Text>(true);
                Assert.AreEqual("goto", label.text);
                Assert.AreEqual(DebugHubIcon.Symbol.Replay, repeat.GetComponentInChildren<DebugHubIcon>(true).symbol);
                Assert.Greater(((RectTransform)repeat.transform).sizeDelta.x, 92f + label.preferredWidth - 1f);
            }
            finally { Object.DestroyImmediate(repeat.transform.root.gameObject); }
        }
    }
}
