using System.Collections.Generic;
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class SearchTests
    {
        private DebugHubPanel panel;
        private readonly List<DebugNode> registered = new();

        [SetUp] public void SetUp() { panel = TestPanel.Build(); registered.Clear(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var node in registered) DebugHub.Remove(node);
            TestPanel.Destroy(panel);
        }

        [Test]
        public void EmptyQuery_BuildsThePageNormally()
        {
            registered.Add(DebugHub.Add(null, "level.goto2", "Nhảy tới level.", () => { }));
            panel.ShowFromRoot(CommandsPage.Root());

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("level")));
        }

        [Test]
        public void Query_OnACommandPage_SearchesTheWholeRegistry_ShowingFullPaths()
        {
            registered.Add(DebugHub.Add(null, "economy.coin2", "Cộng xu.", () => { }));
            panel.ShowFromRoot(CommandsPage.Root());

            panel.Query = "coin2";

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("economy.coin2")));
        }

        [Test]
        public void Query_MatchesDescription_NotOnlyPath()
        {
            registered.Add(DebugHub.Add(null, "zz.hidden", "Cộng xu cho tester.", () => { }));
            panel.ShowFromRoot(CommandsPage.Root());

            panel.Query = "tester";

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("zz.hidden")));
        }

        [Test]
        public void Query_OnAPageWithItsOwnSearch_FiltersThatPage()
        {
            var page = new DebugPage("list",
                p => { p.AddButton("alpha", () => { }); p.AddButton("beta", () => { }); },
                (p, query) => { if ("beta".Contains(query)) p.AddButton("beta", () => { }); });
            panel.ShowFromRoot(page);

            panel.Query = "bet";

            Assert.AreEqual(new List<string> { "beta" }, TestPanel.LabelsOf(panel));
        }

        [Test]
        public void SearchDoesNotInvokeChildrenOfDynamicFolders()
        {
            var calls = 0;
            registered.Add(DebugHub.AddFolder(null, "heavy.folder", "d", () => { calls++; return new DebugNode[0]; }));
            panel.ShowFromRoot(CommandsPage.Root());

            panel.Query = "heavy";

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void SearchButton_HiddenOnPagesThatDeclareNothingToSearch()
        {
            panel.ShowFromRoot(new DebugPage("confirm", p => p.AddButton("ok", () => { }), searchable: false));

            Assert.IsFalse(TestPanel.SearchButtonOf(panel).gameObject.activeSelf);
        }
    }
}
