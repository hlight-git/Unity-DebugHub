using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Parser mang từ IDC phải giữ đúng hành vi cũ: dòng lệnh đã lưu (nút repeat, Watches) parse lại y như trước.
    public class ArgumentParserTests
    {
        [Flags]
        public enum TestFlags { A = 1, B = 2 }

        private static List<string> Split(string line)
        {
            var into = new List<string>();
            DebugValues.SplitArguments(line, into);
            return into;
        }

        private static object Parse(string text, Type type)
        {
            Assert.IsTrue(DebugValues.ParseArgument(text, type, out var value), $"'{text}' → {type.Name}");
            return value;
        }

        [Test]
        public void Split_OnSpaces()
        {
            CollectionAssert.AreEqual(new[] { "level.goto", "5" }, Split("level.goto 5"));
        }

        [Test]
        public void Split_KeepsQuotedSpaces()
        {
            CollectionAssert.AreEqual(new[] { "a", "b c", "d" }, Split("a \"b c\" d"));
        }

        [Test]
        public void Split_Brackets_KeepNesting()
        {
            CollectionAssert.AreEqual(new[] { "1 2", "3 4" }, Split("[1 2] [3 4]"));
            CollectionAssert.AreEqual(new[] { "[1 2] [3 4]" }, Split("[[1 2] [3 4]]"));
        }

        [Test]
        public void Split_DropsTrailingComma()
        {
            CollectionAssert.AreEqual(new[] { "1", "2" }, Split("1, 2"));
        }

        [Test]
        public void Parse_Numbers_InvariantCulture_AndSuffixes()
        {
            Assert.AreEqual(0.5f, Parse("0.5f", typeof(float)));
            Assert.AreEqual(12L, Parse("12L", typeof(long)));
            Assert.AreEqual(true, Parse("1", typeof(bool)));
            Assert.AreEqual(false, Parse("FALSE", typeof(bool)));
        }

        [Test]
        public void Parse_Vector3_FillsMissingWithZero()
        {
            Assert.AreEqual(new Vector3(1f, 2f, 0f), Parse("1, 2", typeof(Vector3)));
        }

        [Test]
        public void Parse_FlagsEnum_CaseInsensitive()
        {
            Assert.AreEqual(TestFlags.A | TestFlags.B, Parse("a|B", typeof(TestFlags)));
        }

        [Test]
        public void Parse_ArrayAndList()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, (int[])Parse("1 2 3", typeof(int[])));
            CollectionAssert.AreEqual(new List<int> { 4, 5 }, (List<int>)Parse("4 5", typeof(List<int>)));
        }

        [Test]
        public void Parse_UnsupportedType_ReturnsFalse()
        {
            Assert.IsFalse(DebugValues.ParseArgument("x", typeof(Uri), out _));
        }

        [Test]
        public void ReadableName_UsesFriendlyNames()
        {
            Assert.AreEqual("Integer", DebugValues.ReadableName(typeof(int)));
            Assert.AreEqual("Integer[]", DebugValues.ReadableName(typeof(int[])));
            Assert.AreEqual("Vector3", DebugValues.ReadableName(typeof(Vector3)));
        }
    }
}
