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
            var names = NamesOf(new Sample());

            Assert.IsTrue(names.Contains("Auto"));
            Assert.IsFalse(names.Any(n => n.Contains("k__BackingField")));
        }

        [Test]
        public void Members_ComposeTheirOwnAddress_AndWriteThrough()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);

            var node = (ValueNode)Reflect.Members(cursor, root)
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
            var nodes = Reflect.Members(cursor, root).Cast<ValueNode>().ToList();

            Assert.IsNull(nodes.First(n => n.Label == "Frozen").Set);
            Assert.IsNotNull(nodes.First(n => n.Label == "Number").Set);
        }

        [Test]
        public void Members_SurfaceReadAndWriteErrors_InsteadOfSwallowingThem()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);
            var nodes = Reflect.Members(cursor, root).Cast<ValueNode>().ToList();

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
            var first = (ActionNode)Reflect.Methods(cursor, root)
                .First(n => n.Label == "Doubled");

            DebugRegistry.StoreArgs(first, new[] { "21" });

            // "Rebuild": cursor mới, và một lần liệt kê Reflect.Members mới — node là một instance
            // C# khác hẳn `first`, đúng như ParamsPage bị dựng lại từ đầu mỗi lần điều hướng.
            Address.TryResolve(root, out var cursor2, out _);
            var second = (ActionNode)Reflect.Methods(cursor2, root)
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

        /// Hình dạng thật của Harvest.Data.ProfileEntry: lớp dẫn xuất rỗng, mọi member ở lớp cha.
        private class EntryBase
        {
            public int CurrentValue = 1;
            public string Status { get; set; } = "ok";
            public readonly int Frozen = 7;
        }
        private class EmptyEntry : EntryBase { }

        private class Shadowing : EntryBase
        {
            public new int CurrentValue = 99;
        }

        [Test]
        public void Members_ShowsInheritedMembers_EvenWhenTheDerivedTypeDeclaresNothing()
        {
            var names = NamesOf(new EmptyEntry());

            Assert.IsTrue(names.Contains("CurrentValue"), "lớp dẫn xuất rỗng mà trang vẫn phải có member của lớp cha");
            Assert.IsTrue(names.Contains("Status"));
            Assert.IsTrue(names.Contains("Frozen"));
        }

        [Test]
        public void Members_MarksWhatCannotBeWritten_InsteadOfHidingIt()
        {
            var nodes = NodesOf(new EmptyEntry());

            Assert.IsNull(nodes.First(n => n.Label == "Frozen").Set, "readonly thì read-only, không phải biến mất");
            Assert.IsNotNull(nodes.First(n => n.Label == "CurrentValue").Set);
        }

        [Test]
        public void Members_WalksAllTheWayUpThroughUnityBaseTypes()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                var names = NamesOf(go.GetComponent<BoxCollider>());

                Assert.IsTrue(names.Contains("size"), "member của chính BoxCollider");
                Assert.IsTrue(names.Contains("enabled"), "của Collider/Behaviour");
                Assert.IsTrue(names.Contains("gameObject"), "của Component");
                Assert.IsTrue(names.Contains("hideFlags"), "của Object");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Members_SkipObsoleteMembers()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                var names = NamesOf(go.GetComponent<BoxCollider>());

                Assert.IsFalse(names.Contains("rigidbody"), "[Obsolete] của Unity chỉ ra dòng lỗi deprecated");
                Assert.IsFalse(NamesOf(new Sample()).Contains("Rotten"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Members_DropsCompilerBackingFields_ButKeepsHandWrittenPrivateFields()
        {
            var names = NamesOf(AddressFixture.Instance);

            Assert.IsFalse(names.Any(n => n.Contains("k__BackingField")));
            Assert.IsTrue(names.Contains("Number"), "field viết tay vẫn phải còn");
        }

        [Test]
        public void Members_ShowsOneRowPerName_WhenADerivedTypeShadowsABaseMember()
        {
            var names = NamesOf(new Shadowing());

            Assert.AreEqual(1, names.Count(n => n == "CurrentValue"),
                "hai dòng cùng tên mà bấm vào đều ra cái ở lớp dẫn xuất thì dòng thứ hai là dòng nói dối");
        }

        [Test]
        public void Methods_AreNotInTheValueList_AndHaveTheirOwnEnumerator()
        {
            var cursor = new Cursor(typeof(EmptyEntry), new EmptyEntry(), null);

            Assert.IsFalse(NamesOf(new EmptyEntry()).Contains("ToString"));
            Assert.IsTrue(Reflect.Methods(cursor, null).Any(n => n.Label == "ToString"));
            Assert.AreEqual(Reflect.Methods(cursor, null).Count(), Reflect.MethodCount(cursor));
        }

        private static List<ValueNode> NodesOf(object value)
        {
            var cursor = new Cursor(value.GetType(), value, null);
            return Reflect.Members(cursor, null).Cast<ValueNode>().ToList();
        }

        private static List<string> NamesOf(object value)
        {
            return NodesOf(value).Select(n => n.Label).ToList();
        }
    }
}
