using System;
using System.Linq;
using IngameDebugConsole;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class CommandsPageTests
    {
        private const string DESCRIPTION = "marker-desc";

        private static int lastInt;
        private static LogType lastEnum;

        private static void TakeInt(int value) => lastInt = value;
        private static void TakeEnum(LogType value) => lastEnum = value;
        private static void TakeTwo(string first, int second) { }

        [SetUp]
        public void SetUp()
        {
            DebugLogConsole.AddCommand<int>("hubtest.takeint", DESCRIPTION, TakeInt);
            DebugLogConsole.AddCommand<LogType>("hubtest.takeenum", DESCRIPTION, TakeEnum);
            DebugLogConsole.AddCommand<string, int>("hubtest.taketwo", DESCRIPTION, TakeTwo);
            DebugLogConsole.AddCommand("hubtestnodot", DESCRIPTION, () => { });
        }

        [TearDown]
        public void TearDown()
        {
            DebugLogConsole.RemoveCommand("hubtest.takeint");
            DebugLogConsole.RemoveCommand("hubtest.takeenum");
            DebugLogConsole.RemoveCommand("hubtest.taketwo");
            DebugLogConsole.RemoveCommand("hubtestnodot");
        }

        private static ConsoleMethodInfo Find(string command) =>
            DebugLogConsole.GetAllCommands().First(c => c.command == command);

        [Test]
        public void CategoryOf_UsesPrefixBeforeFirstDot()
        {
            Assert.AreEqual("hubtest", CommandsPage.CategoryOf(Find("hubtest.takeint")));
        }

        [Test]
        public void CategoryOf_FallsBackToGeneralWhenNoDot()
        {
            Assert.AreEqual("General", CommandsPage.CategoryOf(Find("hubtestnodot")));
        }

        [Test]
        public void Group_PutsBothCommandsOfSamePrefixTogether_KeysSorted()
        {
            var groups = CommandsPage.Group(DebugLogConsole.GetAllCommands());

            Assert.IsTrue(groups.ContainsKey("hubtest"));
            Assert.AreEqual(3, groups["hubtest"].Count, "takeint + takeenum + taketwo");
            CollectionAssert.IsOrdered(groups.Keys, StringComparer.OrdinalIgnoreCase);
        }

        [Test]
        public void HeaderOf_IsCommandNameOnly_NoSignatureNoDescription()
        {
            var header = CommandsPage.HeaderOf(Find("hubtest.takeint"));

            Assert.AreEqual("hubtest.takeint", header);
            Assert.IsFalse(header.Contains("<b>"), header);
            Assert.IsFalse(header.Contains(DESCRIPTION), header);
            Assert.IsFalse(header.Contains("["), header);
        }

        /// IDC để lại dấu cách ở cuối `parameters[i]` với tham số không phải cuối cùng, nên trim
        /// ngoặc trước khi trim whitespace sẽ sót dấu ']'.
        [Test]
        public void ParameterLabelOf_IsBareName_IncludingNonLastParameter()
        {
            var command = Find("hubtest.taketwo");

            Assert.AreEqual("first", CommandsPage.ParameterLabelOf(command, 0));
            Assert.AreEqual("second", CommandsPage.ParameterLabelOf(command, 1));
        }

        [Test]
        public void TryExecute_InvokesMethodWithParsedArgument()
        {
            lastInt = 0;

            var ok = CommandsPage.TryExecute(Find("hubtest.takeint"), new[] { "42" }, out var message);

            Assert.IsTrue(ok, message);
            Assert.AreEqual(42, lastInt);
        }

        [Test]
        public void TryExecute_ParsesEnumByName()
        {
            lastEnum = LogType.Log;

            var ok = CommandsPage.TryExecute(Find("hubtest.takeenum"), new[] { nameof(LogType.Assert) }, out var message);

            Assert.IsTrue(ok, message);
            Assert.AreEqual(LogType.Assert, lastEnum);
        }

        [Test]
        public void TryExecute_RejectsInvalidArgumentWithoutInvoking()
        {
            lastInt = -1;

            var ok = CommandsPage.TryExecute(Find("hubtest.takeint"), new[] { "not-a-number" }, out var message);

            Assert.IsFalse(ok);
            Assert.AreEqual(-1, lastInt);
            StringAssert.Contains("not-a-number", message);
        }

        [Test]
        public void DefaultValueFor_GivesParsableSeed()
        {
            Assert.IsTrue(DebugLogConsole.ParseArgument(CommandsPage.DefaultValueFor(typeof(int)), typeof(int), out _));
            Assert.IsTrue(DebugLogConsole.ParseArgument(CommandsPage.DefaultValueFor(typeof(float)), typeof(float), out _));
            Assert.IsTrue(DebugLogConsole.ParseArgument(CommandsPage.DefaultValueFor(typeof(bool)), typeof(bool), out _));
            Assert.IsTrue(DebugLogConsole.ParseArgument(CommandsPage.DefaultValueFor(typeof(LogType)), typeof(LogType), out _));
        }
    }
}
