using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class ReflectTests
    {
        private class Base { public int Inherited = 1; }
        private class Sample : Base
        {
            public int Field = 2;
            public string Auto { get; set; } = "x";
            [System.Obsolete("đừng đọc")] public int Rotten => throw new System.Exception("nổ");
            public int Method() => 1;
        }

        [SetUp] public void SetUp() => AddressFixture.Reset();

        [Test]
        public void Members_SkipsCompilerGeneratedBackingFields()
        {
            var names = NamesOf(new Sample(), MemberFilter.Default);

            Assert.IsTrue(names.Contains("Auto"));
            Assert.IsFalse(names.Any(n => n.Contains("k__BackingField")));
        }

        [Test]
        public void Members_SkipsObsoleteMembers_SoTheirGetterNeverRuns()
        {
            Assert.DoesNotThrow(() => NamesOf(new Sample(), MemberFilter.Default));
            Assert.IsFalse(NamesOf(new Sample(), MemberFilter.Default).Contains("Rotten"));
        }

        [Test]
        public void Members_HidesInheritedUntilAsked()
        {
            Assert.IsFalse(NamesOf(new Sample(), MemberFilter.Default).Contains("Inherited"));
            Assert.IsTrue(NamesOf(new Sample(), MemberFilter.Inherited).Contains("Inherited"));
        }

        [Test]
        public void Members_HidesMethodsUntilAsked()
        {
            Assert.IsFalse(NamesOf(new Sample(), MemberFilter.Default).Contains("Method"));
            Assert.IsTrue(NamesOf(new Sample(), MemberFilter.Methods).Contains("Method"));
        }

        [Test]
        public void Members_StopsAtMonoBehaviour()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                var names = NamesOf(go.GetComponent<BoxCollider>(), MemberFilter.Default);

                Assert.IsFalse(names.Contains("gameObject"), "đi lên tới Component là ra một rừng member Unity");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Members_ComposeTheirOwnAddress_AndWriteThrough()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);

            var node = (ValueNode)Reflect.Members(cursor, root, MemberFilter.Default)
                .First(n => n.Label == "Number");

            Assert.AreEqual($"{root}.Number", node.Address);
            node.Set(9);
            Assert.AreEqual(9, AddressFixture.Instance.Number);
        }

        [Test]
        public void Members_MarkReadOnlyWhatCannotBeWritten()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);
            var nodes = Reflect.Members(cursor, root, MemberFilter.Default).Cast<ValueNode>().ToList();

            Assert.IsNull(nodes.First(n => n.Label == "Frozen").Set);
            Assert.IsNotNull(nodes.First(n => n.Label == "Number").Set);
        }

        [Test]
        public void Members_SurfaceReadAndWriteErrors_InsteadOfSwallowingThem()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);
            var nodes = Reflect.Members(cursor, root, MemberFilter.Default).Cast<ValueNode>().ToList();

            var gone = nodes.First(n => n.Label == "Number");
            gone.Address = "Khong.Co.Gi";
            Assert.Throws<System.Exception>(() => gone.Get());
        }

        /// Bug đã sửa: ActionFor không gán Key nên DebugRegistry.ArgsFor luôn seed mới và StoreArgs
        /// là no-op cho method reflection — ParamsPage bị rebuild (đi vào page chọn giá trị của một
        /// tham số enum rồi back ra) là mất trắng cái vừa chọn. Key ổn định qua các lần rebuild là
        /// đủ: node được lấy lại từ MỘT lần gọi Reflect.Members MỚI (mô phỏng panel dựng lại trang)
        /// vẫn phải thấy đúng args đã lưu ở lần trước.
        [Test]
        public void ActionFor_KeyIsStable_SoStoredArgsSurviveARebuild()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);
            var first = (ActionNode)Reflect.Members(cursor, root, MemberFilter.Methods)
                .First(n => n.Label == "Doubled");

            DebugRegistry.StoreArgs(first, new[] { "21" });

            // "Rebuild": cursor mới, và một lần liệt kê Reflect.Members mới — node là một instance
            // C# khác hẳn `first`, đúng như ParamsPage bị dựng lại từ đầu mỗi lần điều hướng.
            Address.TryResolve(root, out var cursor2, out _);
            var second = (ActionNode)Reflect.Members(cursor2, root, MemberFilter.Methods)
                .First(n => n.Label == "Doubled");

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.Key, second.Key);
            Assert.AreEqual(new[] { "21" }, DebugRegistry.ArgsFor(second));
        }

        [Test]
        public void Elements_ListsAListWithIndexAddresses()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance.Items";
            Address.TryResolve(root, out var cursor, out _);

            var nodes = Reflect.Elements(cursor, root).Cast<ValueNode>().ToList();

            Assert.AreEqual(3, nodes.Count);
            Assert.AreEqual($"{root}[1]", nodes[1].Address);
            Assert.AreEqual(2, nodes[1].Get());
        }

        [Test]
        public void Elements_ListsADictionaryByKey()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance.Map";
            Address.TryResolve(root, out var cursor, out _);

            var nodes = Reflect.Elements(cursor, root).Cast<ValueNode>().ToList();

            Assert.AreEqual("a", nodes[0].Label);
            Assert.AreEqual(1, nodes[0].Get());
        }

        [Test]
        public void Elements_StopsAtTheLimit_AndSaysHowManyAreLeft()
        {
            var big = new List<int>();
            for (var i = 0; i < 250; i++) big.Add(i);
            var cursor = new Cursor(typeof(List<int>), big, null);

            var nodes = Reflect.Elements(cursor, null).ToList();

            Assert.AreEqual(Reflect.ELEMENT_LIMIT + 1, nodes.Count);
            StringAssert.Contains("150", ((TextNode)nodes[^1]).Text);
        }

        private static List<string> NamesOf(object value, MemberFilter filter)
        {
            var cursor = new Cursor(value.GetType(), value, null);
            return Reflect.Members(cursor, null, filter).Select(n => n.Label).ToList();
        }
    }
}
