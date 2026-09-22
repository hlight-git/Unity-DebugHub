using System;
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class HelpPageTests
    {
        private DebugHubPanel panel;

        [SetUp] public void SetUp() => panel = TestPanel.Build();
        [TearDown] public void TearDown() => TestPanel.Destroy(panel);

        [Test]
        public void Help_ListsEveryRegisteredPath_WithFullDescription()
        {
            var node = DebugHub.Add<int>(null, "help.sample", "Mô tả rất dài để không bị cắt ở trang Help.", _ => { });
            try
            {
                panel.ShowFromRoot(HelpPage.Build());
                var text = string.Join("\n", TestPanel.LabelsOf(panel));

                StringAssert.Contains("help.sample", text);
                StringAssert.Contains("Mô tả rất dài để không bị cắt ở trang Help.", text);
                StringAssert.Contains("[Integer", text);
            }
            finally { DebugHub.Remove(node); }
        }

        [Test]
        public void Help_ValueNode_ShowsCurrentValue()
        {
            var node = DebugHub.AddValue<int>(null, "help.value", string.Empty, () => 42, v => { });
            try
            {
                panel.ShowFromRoot(HelpPage.Build());
                var text = string.Join("\n", TestPanel.LabelsOf(panel));

                StringAssert.Contains("help.value = 42", text);
            }
            finally { DebugHub.Remove(node); }
        }

        [Test]
        public void Help_FolderNode_ShowsChevron()
        {
            var node = DebugHub.AddFolder(null, "help.folder", string.Empty, () => Array.Empty<DebugNode>());
            try
            {
                panel.ShowFromRoot(HelpPage.Build());
                var text = string.Join("\n", TestPanel.LabelsOf(panel));

                StringAssert.Contains("help.folder ›", text);
            }
            finally { DebugHub.Remove(node); }
        }
    }
}
