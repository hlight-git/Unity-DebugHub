using System;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Target cho Executor: nằm ở top level để `get`/`set` gọi được bằng tên đầy đủ.
    public class ExecutorTarget
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

        public class Nested
        {
            public int Value;
        }
    }

    public class ExecutorTests
    {
        private const string TARGET = "Hlight.Debug.Hub.Tests.ExecutorTarget";

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
