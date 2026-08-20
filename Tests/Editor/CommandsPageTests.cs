using System.Collections.Generic;
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    /// Phần dựng nhãn/cây của page Commands. Luồng bấm thật nằm ở CommandsFlowTests.
    public class CommandsPageTests
    {
        private const string DESCRIPTION = "Nhảy tới level";

        private readonly List<DebugCommand> registered = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var command in registered) DebugCommands.Remove(command);
            registered.Clear();
        }

        private DebugCommand Add(string path, string description = DESCRIPTION)
        {
            var command = DebugCommands.Add<int>(null, path, description, level => { }, "level");
            registered.Add(command);
            return command;
        }

        /// Row chỉ hiện tên lá; đường dẫn nằm ở header, description do panel format thành dòng thứ hai.
        [Test]
        public void LabelOf_IsLastSegmentOnly()
        {
            var command = Add("level.goto");

            var label = CommandsPage.LabelOf(command, null, false);

            Assert.AreEqual("goto", label);
            Assert.IsFalse(label.Contains(DESCRIPTION), "description không nằm trong label: " + label);
        }

        [Test]
        public void LabelOf_FullPath_ForRecentAndSearchRows()
        {
            var command = Add("level.goto", string.Empty);

            Assert.AreEqual("level.goto", CommandsPage.LabelOf(command, null, true));
        }

        /// Trùng path trong cùng một chỗ thì hai row giống nhau y hệt, phải kèm số tham số.
        [Test]
        public void LabelOf_DisambiguatesSamePathByParameterCount()
        {
            var one = Add("time.skip", string.Empty);
            var two = DebugCommands.Add<float, float>(null, "time.skip", string.Empty, (sec, speed) => { });
            registered.Add(two);
            var siblings = new List<DebugCommand> { one, two };

            StringAssert.Contains("(1 args)", CommandsPage.LabelOf(one, siblings, false));
            StringAssert.Contains("(2 args)", CommandsPage.LabelOf(two, siblings, false));
        }

        [Test]
        public void Shorten_CutsLongDescriptionAtWordBoundary()
        {
            var description = new string('a', 40) + " " + new string('b', 80);

            var shortened = CommandsPage.Shorten(description);

            Assert.Less(shortened.Length, description.Length);
            StringAssert.EndsWith("…", shortened);
            Assert.IsFalse(shortened.Contains(new string('b', 80)), "phần dư phải bị cắt");
        }

        [Test]
        public void Shorten_LeavesShortDescriptionAlone()
        {
            Assert.AreEqual(DESCRIPTION, CommandsPage.Shorten(DESCRIPTION));
        }

        [Test]
        public void Contains_MatchesOnlyCommandsUnderPrefixWithSegmentLeft()
        {
            var deep = Add("prefs.set.int", string.Empty);
            var shallow = Add("prefs", string.Empty);

            Assert.IsTrue(CommandsPage.Contains(deep.Segments, new[] { "prefs" }));
            Assert.IsTrue(CommandsPage.Contains(deep.Segments, new[] { "prefs", "set" }));
            Assert.IsFalse(CommandsPage.Contains(deep.Segments, new[] { "time" }));
            Assert.IsFalse(CommandsPage.Contains(shallow.Segments, new[] { "prefs" }),
                "chính nó không nằm dưới nó");
        }

        [Test]
        public void Matches_LooksAtPathAndDescription()
        {
            var command = Add("level.goto");

            Assert.IsTrue(CommandsPage.Matches(command, "goto"));
            Assert.IsTrue(CommandsPage.Matches(command, "LEVEL"));
            Assert.IsTrue(CommandsPage.Matches(command, "Nhảy"));
            Assert.IsFalse(CommandsPage.Matches(command, "booster"));
        }
    }
}
