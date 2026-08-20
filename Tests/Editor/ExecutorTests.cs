using System;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Base có method virtual để test override không bị đếm thành 2 overload giả (base + derived
    /// đều "declare" một MethodInfo riêng cho cùng một slot).
    public class ExecutorTargetBase
    {
        public virtual int Multiply(int x) => x;
    }

    /// Target cho Executor: nằm ở top level để `get`/`set` gọi được bằng tên đầy đủ.
    public class ExecutorTarget : ExecutorTargetBase
    {
        public static int StaticNumber;
        public static string StaticText;
        public static ExecutorTarget Instance;
        public static Vector3 StaticVector;

        public int Number;
        public Nested Child = new Nested();
        public Vector3 Vector;
        private int hidden;

        public int ReadOnlyNumber => Number;
        public int this[int index] => index * 10;

        public int Twice(int value) => value * 2;
        public int Hidden => hidden;
        public override int Multiply(int x) => x * 3;

        public class Nested
        {
            public int Value;
        }
    }

    /// Đúng hình dạng của Zego.IGlobalService&lt;T&gt;: static member khai ở interface cha, interface con
    /// chỉ kế thừa. Interface không có BaseType nên nếu chỉ đi bằng BaseType thì query
    /// "IExecutorService" + "Global.X" sẽ báo không tìm thấy member.
    public interface IExecutorGlobal<out T> where T : class
    {
        public static T Global { get; set; }
        public static bool Exist => Global != null;
    }

    public interface IExecutorService : IExecutorGlobal<IExecutorService>
    {
        int Number { get; }
    }

    public class ExecutorService : IExecutorService
    {
        public int Number => 42;
    }

    public class ExecutorTests
    {
        private const string TARGET = "Hlight.Debug.Hub.Tests.ExecutorTarget";
        private const string SERVICE = "Hlight.Debug.Hub.Tests.IExecutorService";

        private static System.Reflection.Assembly Assembly => typeof(ExecutorTests).Assembly;

        [SetUp]
        public void SetUp()
        {
            ExecutorTarget.StaticNumber = 1;
            ExecutorTarget.StaticText = "before";
            ExecutorTarget.StaticVector = Vector3.zero;
            ExecutorTarget.Instance = new ExecutorTarget { Number = 5 };
        }

        [Test]
        public void Get_ReadsStaticField()
        {
            Assert.AreEqual(1, Executor.Get(Assembly, TARGET, "StaticNumber"));
        }

        /// Query kiểu "Zego.IAdService" + "Global.IsRewardedVideoReady": Global là static property của
        /// interface **cha**, và interface không có BaseType để đi lên.
        [Test]
        public void Get_ReadsStaticMemberInheritedFromBaseInterface()
        {
            IExecutorGlobal<IExecutorService>.Global = new ExecutorService();

            Assert.AreEqual(42, Executor.Get(Assembly, SERVICE, "Global.Number"));
            Assert.AreEqual(true, Executor.Get(Assembly, SERVICE, "Exist"));
        }

        [Test]
        public void Get_WalksInstanceChain()
        {
            ExecutorTarget.Instance.Child.Value = 42;

            Assert.AreEqual(42, Executor.Get(Assembly, TARGET, "Instance.Child.Value"));
        }

        [Test]
        public void Get_CallsMethodAndIndexer()
        {
            Assert.AreEqual(14, Executor.Get(Assembly, TARGET, "Instance.Twice(7)"));
            Assert.AreEqual(30, Executor.Get(Assembly, TARGET, "Instance[3]"));
        }

        [Test]
        public void Get_ReadsPrivateFieldThroughProperty()
        {
            Assert.AreEqual(0, Executor.Get(Assembly, TARGET, "Instance.hidden"));
        }

        /// Multiply là virtual trên ExecutorTargetBase, override trên ExecutorTarget: base và derived
        /// đều "declare" một MethodInfo riêng cho cùng slot, nên nếu không lọc override ra thì
        /// AddMethodsRecursive thấy "2 overload" giả và bắt buộc phải disambiguate bằng {index} dù
        /// chỉ có một method thật để gọi.
        [Test]
        public void Get_CallsOverriddenMethod_WithoutAmbiguousOverloadError()
        {
            Assert.AreEqual(21, Executor.Get(Assembly, TARGET, "Instance.Multiply(7)"));
        }

        [Test]
        public void Set_StaticField_UpdatesValue()
        {
            Executor.Set(Assembly, TARGET, "StaticNumber", "50");

            Assert.AreEqual(50, ExecutorTarget.StaticNumber);
        }

        [Test]
        public void Set_StaticString_UpdatesValue()
        {
            Executor.Set(Assembly, TARGET, "StaticText", "after");

            Assert.AreEqual("after", ExecutorTarget.StaticText);
        }

        [Test]
        public void Set_NestedField_UpdatesValue()
        {
            Executor.Set(Assembly, TARGET, "Instance.Child.Value", "7");

            Assert.AreEqual(7, ExecutorTarget.Instance.Child.Value);
        }

        [Test]
        public void Set_UnknownMember_Throws()
        {
            Assert.Throws<Exception>(() => Executor.Set(Assembly, TARGET, "Instance.NoSuchMember", "1"),
                "setting a member that does not exist must fail loudly, not silently do nothing");
        }

        [Test]
        public void Set_MemberOfStruct_Throws()
        {
            Assert.Throws<Exception>(() => Executor.Set(Assembly, TARGET, "Instance.Vector.x", "3"),
                "writing through a boxed struct copy is lost, so it must fail loudly");
        }

        [Test]
        public void Set_ReadOnlyProperty_Throws()
        {
            Assert.Throws<Exception>(() => Executor.Set(Assembly, TARGET, "Instance.ReadOnlyNumber", "3"));
        }

        [Test]
        public void Registry_BindAndRead()
        {
            Executor.Bind("answer", 99);

            Assert.AreEqual(99, Executor.GetRegisteredObject("answer"));
            Assert.Throws<Exception>(() => Executor.GetRegisteredObject("missing"));
        }

        [Test]
        public void ParseObject_NullType_ThrowsReadableError()
        {
            var exception = Assert.Throws<Exception>(() => Executor.ParseObject("5", null));

            StringAssert.Contains("5", exception.Message);
        }
    }
}
