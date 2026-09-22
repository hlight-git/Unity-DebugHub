using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Mẫu vật cho Address/Reflect. Public static Instance để address bắt đầu bằng type là đủ.
    public class AddressFixture
    {
        public static AddressFixture Instance = new AddressFixture();

        public int Number = 1;
        public readonly int Frozen = 3;
        public string Name { get; set; } = "a";
        public int Computed => Number * 2;
        public Inner Box = new Inner();
        public Leg TheLeg;
        public Pair ThePair;
        public List<int> Items = new List<int> { 1, 2, 3 };
        public Dictionary<string, int> Map = new Dictionary<string, int> { { "a", 1 } };

        /// Getter-only trả về **reference** — sửa member bên trong vẫn phải được (§9.2).
        public Inner Child { get; } = new Inner();

        public int Doubled(int x) => x * 2;

        /// Hai overload cùng số tham số — để test luật chọn bằng `{n}`.
        public string Pick(int x) => "int";
        public string Pick(string x) => "string";

        public T Echo<T>(T value) => value;

        public class Inner { public int Value = 5; }
        public struct Leg { public int Length; public Foot Foot; }
        public struct Foot { public int Size; }

        /// Struct có indexer public — kiểu Vector2/Vector3/Color của Unity. Test write-back qua
        /// bước indexer trên struct lồng nhau, riêng khỏi Leg/Foot vốn chỉ đi qua member (dấu chấm).
        public struct Pair
        {
            public int A;
            public int B;
            public int this[int i]
            {
                get => i == 0 ? A : B;
                set { if (i == 0) A = value; else B = value; }
            }
        }

        public static void Reset()
        {
            Instance = new AddressFixture();
        }
    }

    public class AddressTests
    {
        private const string ROOT = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";

        [SetUp] public void SetUp() => AddressFixture.Reset();

        [Test]
        public void Resolve_WalksStaticRootThenMembers()
        {
            Assert.IsTrue(Address.TryResolve($"{ROOT}.Number", out var cursor, out var error), error);
            Assert.AreEqual(1, cursor.Value);
            Assert.AreEqual(typeof(int), cursor.Declared);
        }

        [Test]
        public void Write_SetsAField()
        {
            Assert.IsTrue(Address.TryWrite($"{ROOT}.Number", 9, out var error), error);
            Assert.AreEqual(9, AddressFixture.Instance.Number);
        }

        [Test]
        public void Write_GoesThroughAGetterOnlyPropertyThatReturnsAReference()
        {
            // Luật §9.2: chỉ value type mới cần write-back. Property chỉ có getter mà trả về
            // reference thì member bên trong vẫn ghi được — nếu không, gần như cả game read-only.
            Assert.IsTrue(Address.TryWrite($"{ROOT}.Child.Value", 9, out var error), error);
            Assert.AreEqual(9, AddressFixture.Instance.Child.Value);
        }

        [Test]
        public void Write_ThroughNestedStructs_LandsBackOnTheOriginal()
        {
            Assert.IsTrue(Address.TryWrite($"{ROOT}.TheLeg.Foot.Size", 4, out var error), error);
            Assert.AreEqual(4, AddressFixture.Instance.TheLeg.Foot.Size);
        }

        [Test]
        public void Write_ThroughAnIndexerOnAStructField_LandsBackOnTheOriginal()
        {
            // §9.2 áp dụng y hệt cho bước indexer: ThePair là struct (như Vector2/Vector3/Color
            // của Unity, đều có this[int]) — ghi qua ngoặc vuông phải write-back giống ghi qua dấu
            // chấm, không thì chỉ sửa được một bản copy vứt đi.
            Assert.IsTrue(Address.TryWrite($"{ROOT}.ThePair[1]", 9, out var error), error);
            Assert.AreEqual(9, AddressFixture.Instance.ThePair.B);
        }

        [Test]
        public void Write_RefusesReadonlyField()
        {
            Assert.IsFalse(Address.TryWrite($"{ROOT}.Frozen", 9, out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(3, AddressFixture.Instance.Frozen);
        }

        [Test]
        public void Write_RefusesGetterOnlyValue()
        {
            Assert.IsFalse(Address.TryWrite($"{ROOT}.Computed", 9, out _));
        }

        [Test]
        public void Resolve_ReadsListElement_AndWritesItBack()
        {
            Assert.IsTrue(Address.TryResolve($"{ROOT}.Items[1]", out var cursor, out var error), error);
            Assert.AreEqual(2, cursor.Value);

            Assert.IsTrue(Address.TryWrite($"{ROOT}.Items[1]", 20, out error), error);
            Assert.AreEqual(20, AddressFixture.Instance.Items[1]);
        }

        [Test]
        public void Resolve_ReadsDictionaryByKey()
        {
            Assert.IsTrue(Address.TryResolve($"{ROOT}.Map[\"a\"]", out var cursor, out var error), error);
            Assert.AreEqual(1, cursor.Value);
        }

        [Test]
        public void Resolve_CallsAMethodStep()
        {
            Assert.IsTrue(Address.TryResolve($"{ROOT}.Doubled(21)", out var cursor, out var error), error);
            Assert.AreEqual(42, cursor.Value);
        }

        [Test]
        public void Resolve_RefusesAnAmbiguousOverload_AndTakesTheBraceHint()
        {
            // AddressFixture.Pick có hai overload cùng 1 tham số.
            Assert.IsFalse(Address.TryResolve($"{ROOT}.Pick(1)", out _, out var error));
            StringAssert.Contains("{0}", error);

            Assert.IsTrue(Address.TryResolve($"{ROOT}.Pick{{0}}(1)", out var cursor, out error), error);
            Assert.IsNotNull(cursor.Value);
        }

        [Test]
        public void Resolve_CallsAGenericMethodThroughATypeVariable()
        {
            Vars.Bind("T", typeof(int));
            try
            {
                Assert.IsTrue(Address.TryResolve($"{ROOT}.Echo<$T>(5)", out var cursor, out var error), error);
                Assert.AreEqual(5, cursor.Value);
            }
            finally { Vars.Remove("T"); }
        }

        [Test]
        public void HasMethodStep_SeesCallsButNotBracketLiterals()
        {
            Assert.IsTrue(Address.HasMethodStep($"{ROOT}.Doubled(21)"));
            Assert.IsFalse(Address.HasMethodStep($"{ROOT}.Map[\"a(b)\"]"),
                "dấu ngoặc nằm trong literal của indexer không phải lời gọi method");
        }

        [Test]
        public void Resolve_FromAVarRoot()
        {
            Vars.Bind("fix", AddressFixture.Instance);

            Assert.IsTrue(Address.TryResolve("$fix.Number", out var cursor, out var error), error);
            Assert.AreEqual(1, cursor.Value);
        }

        [Test]
        public void Resolve_FromARegisteredValueNode()
        {
            var node = DebugHub.AddValue(null, "addr.coin", "d", () => 7, _ => { });
            try
            {
                Assert.IsTrue(Address.TryResolve("@addr.coin", out var cursor, out var error), error);
                Assert.AreEqual(7, cursor.Value);
            }
            finally { DebugHub.Remove(node); }
        }

        [Test]
        public void Resolve_SaysHowFarItGot_WhenSomethingIsMissing()
        {
            Assert.IsFalse(Address.TryResolve($"{ROOT}.KhongCoMember", out _, out var error));
            StringAssert.Contains("KhongCoMember", error);
        }

        [Test]
        public void Resolve_RefusesToWalkThroughNull()
        {
            AddressFixture.Instance.Box = null;

            Assert.IsFalse(Address.TryResolve($"{ROOT}.Box.Value", out _, out var error));
            StringAssert.Contains("null", error);
        }
    }
}
