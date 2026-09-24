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
            var nodes = Reflect.Members(cursor, root).OfType<ValueNode>().ToList();

            Assert.IsNull(nodes.First(n => n.Label == "Frozen").Set);
            Assert.IsNotNull(nodes.First(n => n.Label == "Number").Set);
        }

        [Test]
        public void Members_SurfaceReadAndWriteErrors_InsteadOfSwallowingThem()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
            Address.TryResolve(root, out var cursor, out _);
            var nodes = Reflect.Members(cursor, root).OfType<ValueNode>().ToList();

            // Lần đọc đầu lấy từ parent đã resolve cho cả trang; lần sau mới resolve lại theo address.
            var gone = nodes.First(n => n.Label == "Number");
            gone.Get();
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
                .First(n => n.Label.StartsWith("Doubled"));

            DebugRegistry.StoreArgs(first, new[] { "21" });

            // "Rebuild": cursor mới, và một lần liệt kê Reflect.Members mới — node là một instance
            // C# khác hẳn `first`, đúng như ParamsPage bị dựng lại từ đầu mỗi lần điều hướng.
            Address.TryResolve(root, out var cursor2, out _);
            var second = (ActionNode)Reflect.Methods(cursor2, root)
                .First(n => n.Label.StartsWith("Doubled"));

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.Key, second.Key);
            Assert.AreEqual(new[] { "21" }, DebugRegistry.ArgsFor(second));
        }

        [Test]
        public void Elements_ListsAListWithIndexAddresses()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance.Items";
            Address.TryResolve(root, out var cursor, out _);

            var nodes = Reflect.Elements(cursor, root).OfType<ValueNode>().ToList();

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

        private class Counting
        {
            public int Reads;
            public int Probe => ++Reads;
        }

        /// Dựng danh sách + renderer đọc một lần = đúng một lần gọi getter. Bản cũ đọc thêm một lần lúc
        /// dựng chỉ để hỏi "ghi được không" — property có side effect chạy gấp đôi, trang Live thì 8 lần/giây.
        [Test]
        public void Members_ReadEachGetterOnce_BuildPlusRender()
        {
            var probe = new Counting();
            var cursor = new Cursor(typeof(Counting), probe, null);

            var node = Reflect.Members(cursor, null).OfType<ValueNode>().First(n => n.Label == "Probe");
            node.Get();

            Assert.AreEqual(1, probe.Reads, $"getter chạy {probe.Reads} lần cho một lần dựng + một lần hiện");
        }

        [Test]
        public void Members_InstanceMemberOnAStaticRoot_IsNotWritable()
        {
            var cursor = new Cursor(typeof(AddressFixture), null, null);
            var number = Reflect.Members(cursor, "Hlight.Debug.Hub.Tests.AddressFixture")
                .OfType<ValueNode>().First(n => n.Label == "Number");

            Assert.IsNull(number.Set, "không có object nào để ghi vào");
        }

        [Test]
        public void Methods_KeepsEveryOverload_EvenWhenTheyShareArity()
        {
            var cursor = new Cursor(typeof(Overloaded), new Overloaded(), null);
            var labels = Reflect.Methods(cursor, null).Select(n => n.Label).ToList();

            Assert.AreEqual(2, labels.Count(l => l.StartsWith("Pick")),
                "hai overload cùng số tham số khác kiểu — bỏ một cái là nó không có đường nào gọi tới");
            Assert.IsTrue(labels.Any(l => l.Contains("Int32")), "nhãn phải kèm kiểu tham số để phân biệt");
        }

        private class Overloaded
        {
            public string Pick(int x) => "int";
            public string Pick(string x) => "string";
        }

        [Test]
        public void MethodCount_DoesNotBuildNodes()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                var cursor = new Cursor(typeof(BoxCollider), go.GetComponent<BoxCollider>(), null);
                // Một lượt khởi động: lần đầu GetMethods/GetParameters của Unity tự cache, đo nó là đo
                // reflection chứ không đo việc có dựng node hay không. So tương đối, không so số ms —
                // máy CI chậm thì cả hai cùng chậm.
                Reflect.MethodCount(cursor);
                Reflect.Methods(cursor, null).Count();

                // Xoá cache trước khi đo: đo một lần tra dictionary thì test này đúng cả khi
                // MethodCount quay lại dựng node.
                Reflect.ClearCaches();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var count = Reflect.MethodCount(cursor);
                var counting = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                Reflect.Methods(cursor, null).Count();
                var building = watch.Elapsed.TotalMilliseconds;

                watch.Restart();
                Reflect.MethodCount(cursor);
                var cached = watch.Elapsed.TotalMilliseconds;

                Assert.Greater(count, 0);

                // `counting * 2 < building` từng dùng ở đây là đo sát ngưỡng: tỉ lệ thật ~2,2 lần nên
                // nhiễu timing lật nó, test đỏ 2/3 lần chạy. So thẳng là đủ và không lật.
                Assert.Less(counting, building,
                    $"đếm {counting:0.00}ms vs dựng node {building:0.00}ms — đếm mà dựng cả trăm ActionNode thì mỗi lần mở trang member tốn cho một con số");
                Assert.Less(cached, counting,
                    $"lần đếm thứ hai {cached:0.00}ms — metadata method không đổi lúc chạy nên nó phải lấy từ cache");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Elements_KeepTheRuntimeTypeOfEachItem_EvenWithoutAnIndexer()
        {
            var set = new HashSet<int> { 1, 2, 3 };
            var nodes = Reflect.Elements(new Cursor(set.GetType(), set, null), null).OfType<ValueNode>().ToList();

            Assert.AreEqual(typeof(int), nodes[0].Declared,
                "Declared = object thì renderer cho nó là row nav phải bấm vào mới thấy số");
        }

        [Test]
        public void Members_DropsEngineNativeFields()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                var names = NamesOf(go.GetComponent<BoxCollider>());

                Assert.IsFalse(names.Any(n => n.StartsWith("m_")),
                    "m_CachedPtr là con trỏ phía C++: đọc ra số vô nghĩa, ghi vào là hỏng object");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Methods_DropsTheTwoDangerousObjectMethods_ButKeepsTheUsefulOnes()
        {
            var cursor = new Cursor(typeof(Overloaded), new Overloaded(), null);
            var labels = Reflect.Methods(cursor, null).Select(n => n.Label).ToList();

            Assert.IsFalse(labels.Any(l => l.StartsWith("Finalize")));
            Assert.IsFalse(labels.Any(l => l.StartsWith("MemberwiseClone")));
            Assert.IsTrue(labels.Any(l => l.StartsWith("ToString")));
            Assert.IsTrue(labels.Any(l => l.StartsWith("GetType")));
        }

        [Test]
        public void Members_AreGroupedByDeclaringType()
        {
            var go = new GameObject("probe", typeof(BoxCollider));
            try
            {
                var cursor = new Cursor(typeof(BoxCollider), go.GetComponent<BoxCollider>(), null);
                var nodes = Reflect.Members(cursor, null).ToList();

                var headers = nodes.OfType<TextNode>().Select(t => t.Text).ToList();
                Assert.IsTrue(headers.Any(h => h.Contains("BoxCollider")));
                Assert.IsTrue(headers.Any(h => h.Contains("Component")),
                    "member của Unity vẫn hiện, chỉ nằm dưới tiêu đề của lớp khai báo nó");

                // Tiêu đề đầu tiên phải là của chính type, không phải của lớp cha.
                Assert.IsTrue(((TextNode)nodes[0]).Text.Contains("BoxCollider"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Members_SkipAGroupHeaderWhenThatLevelHasNothingToShow()
        {
            var nodes = Reflect.Members(new Cursor(typeof(EmptyEntry), new EmptyEntry(), null), null).ToList();

            Assert.IsFalse(nodes.OfType<TextNode>().Any(t => t.Text.Contains("EmptyEntry")),
                "lớp không khai member nào thì không được có tiêu đề rỗng");
        }

        private static List<ValueNode> NodesOf(object value)
        {
            var cursor = new Cursor(value.GetType(), value, null);
            return Reflect.Members(cursor, null).OfType<ValueNode>().ToList();
        }

        private class GenericOverloads
        {
            public void Pick(List<int> values) { }
            public void Pick(List<string> values) { }
        }

        [Test]
        public void Methods_PreservesOverloadsWithDifferentGenericArguments()
        {
            var nodes = Reflect.Methods(new Cursor(typeof(GenericOverloads), new GenericOverloads(), null), null)
                .Where(node => node.Label.StartsWith("Pick")).ToArray();
            Assert.AreEqual(2, nodes.Length);
            Assert.AreNotEqual(nodes[0].Key, nodes[1].Key);
        }

        private static List<string> NamesOf(object value)
        {
            return NodesOf(value).Select(n => n.Label).ToList();
        }

        private class OwnPrefix { public int m_Score = 3; }

        [Test]
        public void Members_KeepGameFieldsThatHappenToStartWithM()
        {
            CollectionAssert.Contains(NamesOf(new OwnPrefix()), "m_Score",
                "chỉ field m_* của engine là con trỏ C++; field của game theo quy ước m_ vẫn phải hiện");
        }

        [Test]
        public void Elements_OfAnArray_CanBeWritten()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Instance.Numbers";
            Address.TryResolve(root, out var cursor, out _);

            Reflect.Elements(cursor, root).OfType<ValueNode>().ToList()[1].Set(99);

            Assert.AreEqual(99, AddressFixture.Instance.Numbers[1]);
        }

        [Test]
        public void Members_DoNotCloneTheRenderersMaterial()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var renderer = go.GetComponent<MeshRenderer>();
                var shared = renderer.sharedMaterial;
                var nodes = NodesOf(renderer);

                foreach (var node in nodes)
                {
                    try { node.Get(); }
                    catch (System.Exception) { }
                }

                Assert.AreSame(shared, renderer.sharedMaterial, "đọc `material` là tạo bản sao gắn vĩnh viễn vào renderer");
                Assert.Throws<System.Exception>(() => nodes.First(n => n.Label == "material").Get());
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// Dựng trang member rồi hiện nó = một lần resolve address, không phải một lần cho mỗi row.
        [Test]
        public void Members_WithAnAddress_ReadFromTheParentResolvedForThePage()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressFixture.Tracked";
            AddressFixture.TrackedReads = 0;
            Address.TryResolve(root, out var cursor, out _);

            var nodes = Reflect.Members(cursor, root).OfType<ValueNode>().ToList();
            foreach (var node in nodes.Where(n => n.Label == "Number" || n.Label == "Name")) node.Get();

            Assert.AreEqual(1, AddressFixture.TrackedReads,
                "mỗi row resolve lại từ gốc = gốc `#Type[i]` quét scene một lần cho mỗi row");
        }
    }
}
