using System.Linq;
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
        public void Find_DoesNotBuildAGlobalIndex()
        {
            var field = typeof(TypeFinder).GetField("index",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.IsNull(field, "index toàn cục 135k key đã bị xoá — dựng nó tốn 4.1 giây trên main thread");
        }

        [Test]
        public void Find_ResolvesAFullNameFast()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var type = TypeFinder.Find("Hlight.Debug.Hub.DebugRegistry");

            Assert.AreEqual(typeof(DebugRegistry), type);
            Assert.Less(watch.ElapsedMilliseconds, 200, "full name phải là tra cứu, không phải quét");
        }

        [Test]
        public void Assemblies_FiltersByFragment()
        {
            var found = TypeFinder.Assemblies("Hlight.Debug");

            Assert.IsTrue(found.Any(a => a.GetName().Name == "Hlight.Debug.Hub"));
        }

        [Test]
        public void Search_IsScopedToOneAssembly()
        {
            var hub = typeof(DebugRegistry).Assembly;

            var found = TypeFinder.Search(hub, "Debug");

            Assert.IsTrue(found.Contains(typeof(DebugRegistry)));
            Assert.IsTrue(found.All(t => t.Assembly == hub), "không được trả type của assembly khác");
        }

        [Test]
        public void Search_RespectsItsLimit()
        {
            Assert.LessOrEqual(TypeFinder.Search(typeof(DebugRegistry).Assembly, "e", limit: 3).Count, 3);
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
