using System.Collections.Generic;
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    /// Phần dựng nhãn/cây của page Commands. Luồng bấm thật nằm ở CommandsFlowTests.
    public class CommandsPageTests
    {
        private const string DESCRIPTION = "Nhảy tới level";

        private readonly List<DebugNode> registered = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var node in registered) DebugHub.Remove(node);
            registered.Clear();
        }

        private DebugRegistry.Entry Add(string path, string description = DESCRIPTION)
        {
            var node = DebugHub.Add<int>(null, path, description, level => { }, "level");
            registered.Add(node);
            return EntryFor(node);
        }

        private static DebugRegistry.Entry EntryFor(DebugNode node)
        {
            foreach (var entry in DebugRegistry.All)
            {
                if (entry.Node == node) return entry;
            }
            Assert.Fail("node vừa đăng ký không thấy trong DebugRegistry.All");
            return null;
        }

        /// Row chỉ hiện tên lá; đường dẫn nằm ở header, description do panel format thành dòng thứ hai.
        [Test]
        public void LabelOf_IsLastSegmentOnly()
        {
            var entry = Add("level.goto");

            var label = CommandsPage.LabelOf(entry, null);

            Assert.AreEqual("goto", label);
            Assert.IsFalse(label.Contains(DESCRIPTION), "description không nằm trong label: " + label);
        }

        /// Trùng path trong cùng một thư mục thì hai row giống nhau y hệt, phải kèm số tham số.
        [Test]
        public void LabelOf_DisambiguatesSamePathByParameterCount()
        {
            var one = Add("time.skip", string.Empty);
            var twoNode = DebugHub.Add<float, float>(null, "time.skip", string.Empty, (sec, speed) => { });
            registered.Add(twoNode);
            var two = EntryFor(twoNode);
            var siblings = new List<DebugRegistry.Entry> { one, two };

            StringAssert.Contains("(1 args)", CommandsPage.LabelOf(one, siblings));
            StringAssert.Contains("(2 args)", CommandsPage.LabelOf(two, siblings));
        }



        [Test]
        public void Contains_MatchesOnlyNodesUnderPrefixWithSegmentLeft()
        {
            var deep = Add("prefs.set.int", string.Empty);
            var shallow = Add("prefs", string.Empty);

            var deepSegments = deep.Path.Split('.');
            Assert.IsTrue(CommandsPage.Contains(deepSegments, new[] { "prefs" }));
            Assert.IsTrue(CommandsPage.Contains(deepSegments, new[] { "prefs", "set" }));
            Assert.IsFalse(CommandsPage.Contains(deepSegments, new[] { "time" }));
            Assert.IsFalse(CommandsPage.Contains(shallow.Path.Split('.'), new[] { "prefs" }),
                "chính nó không nằm dưới nó");
        }

        [Test]
        public void Matches_LooksAtPathAndDescription()
        {
            var entry = Add("level.goto");

            Assert.IsTrue(CommandsPage.Matches(entry, "goto"));
            Assert.IsTrue(CommandsPage.Matches(entry, "LEVEL"));
            Assert.IsTrue(CommandsPage.Matches(entry, "Nhảy"));
            Assert.IsFalse(CommandsPage.Matches(entry, "booster"));
        }
    }
}
