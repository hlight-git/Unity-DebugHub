using System;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Hợp đồng §4 của spec: kiểu nào parse được, kiểu nào có editor tại chỗ, và
    /// ToText phải quay lại đúng giá trị cũ qua parser của IDC.
    public class DebugValuesTests
    {
        private enum Flavor { Sweet, Salty }

        [Test]
        public void CanParse_CoversIdcTable_ButNotArbitraryTypes()
        {
            Assert.IsTrue(DebugValues.CanParse(typeof(int)));
            Assert.IsTrue(DebugValues.CanParse(typeof(Vector3)));
            Assert.IsTrue(DebugValues.CanParse(typeof(Flavor)));
            Assert.IsTrue(DebugValues.CanParse(typeof(GameObject)));
            Assert.IsTrue(DebugValues.CanParse(typeof(int[])));
            Assert.IsFalse(DebugValues.CanParse(typeof(DebugValuesTests)));
        }

        [Test]
        public void IsInlineValue_ExcludesUnityReferencesAndCollections()
        {
            Assert.IsTrue(DebugValues.IsInlineValue(typeof(float)));
            Assert.IsTrue(DebugValues.IsInlineValue(typeof(Color)));
            Assert.IsTrue(DebugValues.IsInlineValue(typeof(Flavor)));

            // Có parser nhưng phải mở ra xem được, không nhét vào một ô text.
            Assert.IsFalse(DebugValues.IsInlineValue(typeof(GameObject)));
            Assert.IsFalse(DebugValues.IsInlineValue(typeof(Transform)));
            Assert.IsFalse(DebugValues.IsInlineValue(typeof(int[])));
            Assert.IsFalse(DebugValues.IsInlineValue(typeof(System.Collections.Generic.List<int>)));
        }

        [Test]
        public void ToText_RoundTripsThroughIdcParser()
        {
            AssertRoundTrip(42, typeof(int));
            AssertRoundTrip(0.5f, typeof(float));
            AssertRoundTrip(true, typeof(bool));
            AssertRoundTrip(Flavor.Salty, typeof(Flavor));
            AssertRoundTrip(new Vector3(1.5f, -2f, 3.25f), typeof(Vector3));
            AssertRoundTrip(new Vector2(1f, 2f), typeof(Vector2));
            AssertRoundTrip(new Color(0.1f, 0.2f, 0.3f, 0.4f), typeof(Color));
            AssertRoundTrip(new Rect(1f, 2f, 3f, 4f), typeof(Rect));
            AssertRoundTrip(new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f)), typeof(Bounds));
            AssertRoundTrip(new Vector3Int(1, 2, 3), typeof(Vector3Int));
        }

        [Test]
        public void ToText_UsesInvariantCulture_NotMachineDecimalSeparator()
        {
            Assert.AreEqual("0.5", DebugValues.ToText(0.5f));
        }

        [Test]
        public void TryToArgument_QuotesValuesThatContainSpaces()
        {
            Assert.IsTrue(DebugValues.TryToArgument(new Vector3(1f, 2f, 3f), typeof(Vector3), out var vector));
            Assert.AreEqual("\"1 2 3\"", vector);

            Assert.IsTrue(DebugValues.TryToArgument(string.Empty, typeof(string), out var empty));
            Assert.AreEqual("\"\"", empty);

            Assert.IsTrue(DebugValues.TryToArgument(7, typeof(int), out var number));
            Assert.AreEqual("7", number);
        }

        [Test]
        public void TryToArgument_RefusesUnityReferences()
        {
            var go = new GameObject("probe");
            try
            {
                Assert.IsFalse(DebugValues.TryToArgument(go, typeof(GameObject), out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryParse_TreatsFailedUnityLookupAsError_NotAsNull()
        {
            // ParseGameObject của IDC luôn trả true, kể cả khi Find không thấy gì.
            Assert.IsFalse(DebugValues.TryParse("khong-co-object-ten-nay", typeof(GameObject), out _, out var error));
            Assert.IsNotEmpty(error);

            Assert.IsTrue(DebugValues.TryParse("null", typeof(GameObject), out var explicitNull, out _));
            Assert.IsNull(explicitNull);
        }

        [Test]
        public void TryParse_RejectsGarbage_WithReadableError()
        {
            Assert.IsFalse(DebugValues.TryParse("bảy", typeof(int), out _, out var error));
            StringAssert.Contains("Integer", error);
        }

        private static void AssertRoundTrip(object value, Type type)
        {
            var text = DebugValues.ToText(value);
            Assert.IsTrue(DebugValues.TryParse(text, type, out var parsed, out var error),
                $"{type.Name}: ToText ra \"{text}\" mà parse lại không được ({error})");
            Assert.AreEqual(value, parsed, $"{type.Name}: đi qua \"{text}\" rồi về không bằng giá trị cũ");
        }
    }
}
