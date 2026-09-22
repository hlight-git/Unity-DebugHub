using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class DebugTableTests
    {
        [Test]
        public void Format_PadsEachColumnToItsWidest()
        {
            var text = DebugTable.Format(new[] { "color", "sets" },
                new[] { new[] { "A", "3" }, new[] { "Purple", "12" } });

            var lines = text.Split('\n');
            StringAssert.StartsWith("<mspace=", lines[0]);
            StringAssert.Contains("color   sets", lines[0]);
            StringAssert.Contains("A       3", lines[1]);
            StringAssert.Contains("Purple  12", lines[2]);
        }

        [Test]
        public void Format_TruncatesWidestColumn_WhenOverBudget()
        {
            var text = DebugTable.Format(new[] { "name", "n" },
                new[] { new[] { new string('x', 40), "1" } }, budget: 20);

            foreach (var line in text.Replace("<mspace=0.55em>", string.Empty).Split('\n'))
                Assert.LessOrEqual(line.Length, 20, $"dòng dài quá ngân sách: \"{line}\"");
            StringAssert.Contains("…", text);
        }

        [Test]
        public void Format_HandlesRaggedRows_WithoutThrowing()
        {
            var text = DebugTable.Format(new[] { "a", "b", "c" }, new[] { new[] { "1" } });

            Assert.IsNotEmpty(text);
        }
    }
}
