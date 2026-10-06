using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    public class HelpPageTests
    {
        private DebugHubPanel panel;

        [SetUp] public void SetUp() => panel = TestPanel.Build();
        [TearDown] public void TearDown() => TestPanel.Destroy(panel);

        private string Text() => string.Join("\n", TestPanel.LabelsOf(panel));

        /// Mở lại hub bằng gì là đọc từ chính các trigger đang gắn, không phải chữ viết sẵn.
        [Test]
        public void Help_ShowsHowToOpen_FromTheTriggerHints()
        {
            panel.ShowFromRoot(HelpPage.Build(new[] { "Phím F12." }));

            StringAssert.Contains("Phím F12.", Text());
        }

        [Test]
        public void Help_ShowsGameNotes_OnlyWhenThereAreSome()
        {
            var saved = DebugHub.Notes.ToList();
            try
            {
                DebugHub.Notes.Clear();
                panel.ShowFromRoot(HelpPage.Build(Array.Empty<string>()));
                StringAssert.DoesNotContain("Ghi chú của game", Text());

                DebugHub.Notes.Add("Level 1–3 không có quảng cáo.");
                panel.ShowFromRoot(HelpPage.Build(Array.Empty<string>()));
                StringAssert.Contains("Ghi chú của game", Text());
                StringAssert.Contains("Level 1–3 không có quảng cáo.", Text());
            }
            finally
            {
                DebugHub.Notes.Clear();
                DebugHub.Notes.AddRange(saved);
            }
        }

        /// Cùng thứ tự với cây Commands (có priority), kèm kiểu tham số và mô tả đầy đủ.
        [Test]
        public void Reference_ListsEveryCommand_InTreeOrder()
        {
            var b = DebugHub.Add<int>(null, "help.b", "Mô tả rất dài để không bị cắt ở trang Help.", _ => { });
            var a = DebugHub.Add(null, "help.a", "Chạy a.", () => { }).Priority(-1);
            var folder = DebugHub.AddFolder(null, "help.folder", string.Empty, () => Array.Empty<DebugNode>());
            try
            {
                panel.ShowFromRoot(HelpPage.Commands());
                var text = Text();

                Assert.Less(text.IndexOf("help.a", StringComparison.Ordinal), text.IndexOf("help.b", StringComparison.Ordinal));
                StringAssert.Contains("[Integer", text);
                StringAssert.Contains("Mô tả rất dài để không bị cắt ở trang Help.", text);
                StringAssert.Contains("help.folder</b> ›", text);
            }
            finally
            {
                DebugHub.Remove(a);
                DebugHub.Remove(b);
                DebugHub.Remove(folder);
            }
        }

        /// Help là chỗ tra, không phải chỗ đọc giá trị: mở nó không được gọi getter của game.
        [Test]
        public void Reference_DoesNotCallGetters()
        {
            var calls = 0;
            var node = DebugHub.AddValue<int>(null, "help.value", string.Empty, () => ++calls, v => { });
            try
            {
                panel.ShowFromRoot(HelpPage.Commands());

                StringAssert.Contains("help.value", Text());
                Assert.AreEqual(0, calls);
            }
            finally { DebugHub.Remove(node); }
        }

        [Test]
        public void AddressSyntax_ListsCopyableExamples()
        {
            panel.ShowFromRoot(HelpPage.AddressSyntax());

            var details = TestPanel.Rows(panel).Where(row => row.detail).Select(row => row.detail.text).ToList();
            Assert.IsTrue(details.Any(text => text.Contains("@economy.coin")));
            Assert.IsTrue(details.Any(text => text.Contains("#UnityEngine.Camera[0].fieldOfView")));
        }

        [Test]
        public void Triggers_DescribeThemselves()
        {
            var go = new GameObject("help triggers");
            try
            {
                StringAssert.Contains("trái trên › phải dưới › trái dưới › phải trên › trái trên",
                    go.AddComponent<CornerPatternDebuggerAuthenticationTrigger>().Hint);
                Assert.That(go.AddComponent<KeyPressDebuggerAuthenticationTrigger>().Hint, Does.Contain("backquote").IgnoreCase);
                Assert.IsNotNull(go.AddComponent<ShakeDebuggerAuthenticationTrigger>().Hint);
                Assert.IsNotNull(go.AddComponent<ScribbleDebuggerAuthenticationTrigger>().Hint);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
