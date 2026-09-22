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
    }
}
