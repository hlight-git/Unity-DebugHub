using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Hlight.Debug.Hub.Tests.Instances;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests.Instances
{
    /// Fixture cho root `#Type[i]`: phải là UnityEngine.Object thật (TryInstance đòi
    /// `typeof(Object).IsAssignableFrom`), và sống trong sub-namespace riêng để FullName có dấu
    /// '.' — đúng hình dạng gây bug (UnityEngine.Camera, Harvest.UI.MouseCursor, ...) và tên ngắn
    /// "AddressInstanceFixture" không đụng type nào khác trong project (không mơ hồ).
    public class AddressInstanceFixture : MonoBehaviour
    {
        public int Value = 7;
    }
}

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

        /// Đếm số lần đọc — test "resolve một lần mỗi lần dựng". Riêng khỏi Number: nhiều test ghi
        /// thẳng vào Number, đổi nó thành property là đổi luôn thứ Members báo cáo.
        public static int CountedReads;
        public int Counted => ++CountedReads;

        /// Root có đếm: mỗi lần một address bắt đầu từ đây được resolve là một lần đọc.
        public static int TrackedReads;
        public static AddressFixture Tracked { get { TrackedReads++; return Instance; } }

        /// Setter luôn ném — fixture cho "Run/Set thất bại phải hiện lỗi rõ ràng, không được nuốt"
        /// (xem AdvancedPageTests.WatchPage_FailingWrite_...).
        public int Explosive { get => 0; set => throw new System.Exception("bùm"); }
        public Inner Box = new Inner();
        public Leg TheLeg;
        public Pair ThePair;
        public List<int> Items = new List<int> { 1, 2, 3 };
        public Dictionary<string, int> Map = new Dictionary<string, int> { { "a", 1 } };
        public int[] Numbers = { 10, 20, 30 };
        public Inner[] Boxes = { new Inner() };
        public TwoIndexers Indexed = new TwoIndexers();

        /// Hai indexer: GetProperty("Item") trên type này ném AmbiguousMatchException.
        public class TwoIndexers
        {
            public int this[int i] => i;
            public int this[string s] => s.Length;
        }

        /// Getter-only trả về **reference** — sửa member bên trong vẫn phải được.
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

    /// 3 fixture dưới phủ những trường hợp ReflectionExtensions (GetFieldRecursive/GetPropertyRecursive/
    /// AddMethodsRecursive) phải xử lý mà AddressFixture ở trên không chạm tới: field private, method
    /// bị override, và static member kế thừa từ interface cha.
    public class AddressOverrideBase
    {
        public virtual int Multiply(int x) => x;
    }

    public class AddressOverrideTarget : AddressOverrideBase
    {
        public static AddressOverrideTarget Instance = new AddressOverrideTarget();
        private int hidden = 5;
        public override int Multiply(int x) => x * 3;
    }

    /// Đúng hình dạng của Zego.IGlobalService&lt;T&gt;: static member khai ở interface cha, interface con
    /// chỉ kế thừa. Interface không có BaseType nên nếu chỉ đi bằng BaseType thì address
    /// "IAddressService" + "Global.X" sẽ báo không tìm thấy member.
    public interface IAddressGlobal<out T> where T : class
    {
        public static T Global { get; set; }
    }

    public interface IAddressService : IAddressGlobal<IAddressService>
    {
        int Number { get; }
    }

    public class AddressServiceImpl : IAddressService
    {
        public int Number => 42;
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
            // Chỉ value type mới cần write-back. Property chỉ có getter mà trả về
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
            // Luật write-back áp dụng y hệt cho bước indexer: ThePair là struct (như Vector2/Vector3/Color
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

        /// `<`, `>` và `{n}` trong **tham số** không phải cú pháp generic / chọn overload.
        [Test]
        public void Resolve_BracketsInsideStringArguments_AreJustText()
        {
            // Thứ tự overload do reflection quyết: thử cả hai, đúng một cái là bản nhận string.
            foreach (var argument in new[] { "a<b", "<b>" })
            {
                var picked = 0;
                for (var order = 0; order < 2; order++)
                {
                    if (Address.TryResolve($"{ROOT}.Pick{{{order}}}(\"{argument}\")", out var cursor, out _) &&
                        (string)cursor.Value == "string") picked++;
                }
                Assert.AreEqual(1, picked, argument);
            }

            string error;

            Assert.IsFalse(Address.TryResolve($"{ROOT}.Pick(\"{{1}}\")", out _, out error),
                "{1} trong tham số không được âm thầm chọn overload");
            StringAssert.Contains("{0}", error);

            Assert.IsFalse(Address.TryResolve($"{ROOT}.Doubled<System.Int32>(1)", out _, out error));
            StringAssert.Contains("generic", error);
        }

        [Test]
        public void Elements_OfAMultiDimensionalArray_AreListed_NotAnError()
        {
            var grid = new Cursor(typeof(int[,]), new int[2, 3], null);
            var count = 0;
            foreach (var _ in Reflect.Elements(grid, null)) count++;
            Assert.AreEqual(6, count);
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

        [Test]
        public void Resolve_ReadsAPrivateFieldByName()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressOverrideTarget.Instance";

            Assert.IsTrue(Address.TryResolve($"{root}.hidden", out var cursor, out var error), error);
            Assert.AreEqual(5, cursor.Value);
        }

        /// Multiply là virtual trên AddressOverrideBase, override trên AddressOverrideTarget: base và
        /// derived đều "declare" một MethodInfo riêng cho cùng slot — nếu AddMethodsRecursive không lọc
        /// override ra thì đây thành "2 overload" giả, bắt buộc phải disambiguate bằng {index} dù chỉ
        /// có một method thật để gọi.
        [Test]
        public void Resolve_CallsAnOverriddenMethod_WithoutAmbiguousOverloadError()
        {
            const string root = "Hlight.Debug.Hub.Tests.AddressOverrideTarget.Instance";

            Assert.IsTrue(Address.TryResolve($"{root}.Multiply(7)", out var cursor, out var error), error);
            Assert.AreEqual(21, cursor.Value);
        }

        /// Query kiểu "Zego.IAdService" + "Global.IsRewardedVideoReady": Global là static property của
        /// interface **cha**, và interface không có BaseType để đi lên.
        [Test]
        public void Resolve_ReadsAStaticMemberInheritedFromABaseInterface()
        {
            IAddressGlobal<IAddressService>.Global = new AddressServiceImpl();
            try
            {
                Assert.IsTrue(Address.TryResolve("Hlight.Debug.Hub.Tests.IAddressService.Global.Number",
                    out var cursor, out var error), error);
                Assert.AreEqual(42, cursor.Value);
            }
            finally { IAddressGlobal<IAddressService>.Global = null; }
        }

        [Test]
        public void Resolve_InstanceRoot_UnqualifiedShortName_WithIndex()
        {
            var go = new GameObject("probe", typeof(AddressInstanceFixture));
            try
            {
                Assert.IsTrue(Address.TryResolve("#AddressInstanceFixture[0]", out var cursor, out var error), error);
                Assert.AreEqual(go.GetComponent<AddressInstanceFixture>(), cursor.Value);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Resolve_InstanceRoot_UnqualifiedShortName_NoIndexDefaultsToFirst()
        {
            var go = new GameObject("probe", typeof(AddressInstanceFixture));
            try
            {
                Assert.IsTrue(Address.TryResolve("#AddressInstanceFixture", out var cursor, out var error), error);
                Assert.AreEqual(go.GetComponent<AddressInstanceFixture>(), cursor.Value);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// Bug đã sửa: `#Namespace.Type[i]` từng bị cắt root ở dấu '.' đầu tiên của namespace
        /// (`CutAfterDollarToken`/`TrySplitInstanceRoot` sửa lại việc này), nên
        /// `#UnityEngine.Camera[0]` từng báo lỗi ngay ở "UnityEngine".
        [Test]
        public void Resolve_InstanceRoot_NamespacedType_WithIndex()
        {
            var go = new GameObject("probe", typeof(AddressInstanceFixture));
            try
            {
                Assert.IsTrue(
                    Address.TryResolve("#Hlight.Debug.Hub.Tests.Instances.AddressInstanceFixture[0]",
                        out var cursor, out var error), error);
                Assert.AreEqual(go.GetComponent<AddressInstanceFixture>(), cursor.Value);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Resolve_InstanceRoot_NamespacedType_NoIndex()
        {
            var go = new GameObject("probe", typeof(AddressInstanceFixture));
            try
            {
                Assert.IsTrue(
                    Address.TryResolve("#Hlight.Debug.Hub.Tests.Instances.AddressInstanceFixture",
                        out var cursor, out var error), error);
                Assert.AreEqual(go.GetComponent<AddressInstanceFixture>(), cursor.Value);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// `#Namespace.Type[i]` phải dừng root đúng ở `]` rồi mới cho bước member tiếp theo đi —
        /// khác trần trước đây coi hết phần sau dấu '.' đầu tiên của namespace là step.
        [Test]
        public void Resolve_InstanceRoot_NamespacedTypeWithIndex_ThenMemberStep()
        {
            var go = new GameObject("probe", typeof(AddressInstanceFixture));
            try
            {
                Assert.IsTrue(
                    Address.TryResolve("#Hlight.Debug.Hub.Tests.Instances.AddressInstanceFixture[0].Value",
                        out var cursor, out var error), error);
                Assert.AreEqual(7, cursor.Value);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Indexer_ReadsAndWritesArrayElements()
        {
            Assert.IsTrue(Address.TryResolve($"{ROOT}.Numbers[1]", out var cursor, out var error), error);
            Assert.AreEqual(20, cursor.Value);

            Assert.IsTrue(Address.TrySet($"{ROOT}.Numbers[1]", "25", out error), error);
            Assert.AreEqual(25, AddressFixture.Instance.Numbers[1]);
        }

        [Test]
        public void Indexer_DrillsIntoAnArrayElement()
        {
            Assert.IsTrue(Address.TrySet($"{ROOT}.Boxes[0].Value", "8", out var error), error);
            Assert.AreEqual(8, AddressFixture.Instance.Boxes[0].Value);
        }

        [Test]
        public void Indexer_OutsideTheArray_IsAnError()
        {
            Assert.IsFalse(Address.TryResolve($"{ROOT}.Numbers[9]", out _, out var error));
            StringAssert.Contains("9", error);
        }

        [Test]
        public void GenericMethod_WithTheWrongTypeArguments_IsAnError_NotAnException()
        {
            string error = null;
            Assert.DoesNotThrow(() => Address.TryResolve($"{ROOT}.Echo<Int32 Int32>(5)", out _, out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void GenericMethod_AcceptsANamespacedTypeArgument()
        {
            Assert.IsTrue(Address.TryResolve($"{ROOT}.Echo<System.Int32>(5)", out var cursor, out var error), error);
            Assert.AreEqual(5, cursor.Value);
        }

        [Test]
        public void MemberNamedItem_OnATypeWithSeveralIndexers_IsAnError_NotAnException()
        {
            string error = null;
            Assert.DoesNotThrow(() => Address.TryResolve($"{ROOT}.Indexed.Item", out _, out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void StepSplit_IgnoresDotsInsideQuotesAndBrackets()
        {
            AddressFixture.Instance.Map["a.b"] = 9;

            Assert.IsTrue(Address.TryResolve($"{ROOT}.Map[\"a.b\"]", out var cursor, out var error), error);
            Assert.AreEqual(9, cursor.Value);
        }
    }
}
