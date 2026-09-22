using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class TypeFinderTests
    {
        [Test]
        public void Find_AcceptsFullNameAndUniqueShortName()
        {
            Assert.AreEqual(typeof(DebugRegistry), TypeFinder.Find("Hlight.Debug.Hub.DebugRegistry"));
            Assert.AreEqual(typeof(DebugRegistry), TypeFinder.Find("DebugRegistry"));
        }

        [Test]
        public void Find_ReturnsNull_ForAmbiguousShortName_ButFullNameStillWorks()
        {
            // "Object" tồn tại ở cả UnityEngine lẫn System — short name không đủ để chọn.
            Assert.IsNull(TypeFinder.Find("Object"));
            Assert.AreEqual(typeof(UnityEngine.Object), TypeFinder.Find("UnityEngine.Object"));
        }

        [Test]
        public void Search_MatchesFragment_CaseInsensitively()
        {
            var found = TypeFinder.Search("debugregi");

            Assert.Contains(typeof(DebugRegistry), (System.Collections.ICollection)found);
        }

        [Test]
        public void Search_RespectsItsLimit()
        {
            Assert.LessOrEqual(TypeFinder.Search("e", limit: 5).Count, 5);
        }

        [Test]
        public void LongestPrefix_SplitsTypeFromMemberPath()
        {
            var type = TypeFinder.LongestPrefix("Hlight.Debug.Hub.DebugRegistry.LastCommand", out var rest);

            Assert.AreEqual(typeof(DebugRegistry), type);
            Assert.AreEqual("LastCommand", rest);
        }

        [Test]
        public void LongestPrefix_TakesTheLongestMatch_NotTheFirstOne()
        {
            // "Hlight" và "Hlight.Debug" không phải type; phải đi tới tận DebugRegistry.
            var type = TypeFinder.LongestPrefix("Hlight.Debug.Hub.DebugRegistry", out var rest);

            Assert.AreEqual(typeof(DebugRegistry), type);
            Assert.IsEmpty(rest);
        }

        [Test]
        public void LongestPrefix_ReturnsNull_WhenNothingResolves()
        {
            Assert.IsNull(TypeFinder.LongestPrefix("Khong.Co.Type.Nao", out _));
        }
    }
}
