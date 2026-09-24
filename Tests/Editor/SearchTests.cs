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

        [Test]
        public void CategorySearch_StaysInsideItsBranch_IncludingDescendants()
        {
            registered.Add(DebugHub.Add(null, "scope.coins.add", "needle", () => { }));
            registered.Add(DebugHub.Add(null, "scope.coins.reset", "needle", () => { }));
            registered.Add(DebugHub.Add(null, "scope.other.reset", "needle", () => { }));
            registered.Add(DebugHub.Add(null, "scope.coinsExtra.reset", "needle", () => { }));
            panel.ShowFromRoot(CommandsPage.Folder(new[] { "scope", "coins" }, "coins"));
            panel.Query = "needle";
            var labels = TestPanel.LabelsOf(panel);
            Assert.AreEqual(2, labels.Count);
            Assert.IsTrue(labels.TrueForAll(label => label.Contains("scope.coins.")));
            panel.Query = "other";
            Assert.IsFalse(TestPanel.LabelsOf(panel).Exists(label => label.Contains("scope.other")));
        }

        [Test]
        public void PagesWithoutSearch_DoNotOfferSearchOrFallBackToRegistry()
        {
            registered.Add(DebugHub.Add(null, "unexpected.command", "needle", () => { }));
            panel.ShowFromRoot(new DebugPage("plain", p => p.AddText("local content")));
            Assert.IsFalse(TestPanel.SearchButtonOf(panel).gameObject.activeSelf);
            panel.Query = "needle";
            CollectionAssert.AreEqual(new[] { "local content" }, TestPanel.LabelsOf(panel));
            panel.ShowFromRoot(AdvancedPage.Root());
            Assert.IsFalse(TestPanel.SearchButtonOf(panel).gameObject.activeSelf);
            panel.ShowFromRoot(HelpPage.Build());
            Assert.IsFalse(TestPanel.SearchButtonOf(panel).gameObject.activeSelf);
        }

        [Test]
        public void DynamicFolderSearch_FiltersItsChildren_AndKeepsItsRunCallback()
        {
            var ran = false;
            var child = new ActionNode { Label = "needle", Invoke = _ => { } };
            var folder = new FolderNode { Label = "folder", Children = () => new DebugNode[]
                { child, new ActionNode { Label = "unrelated" } } };
            panel.ShowFromRoot(NodeRenderer.FolderPage(folder, (node, args) => ran = node == child));
            panel.Query = "needle";
            Assert.AreEqual(1, TestPanel.Rows(panel).Count);
            TestPanel.Rows(panel)[0].button.onClick.Invoke();
            Assert.IsTrue(ran);
        }
    }
}
