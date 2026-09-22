using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class VarsTests
    {
        [TearDown] public void TearDown() { foreach (var pair in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, object>>(Vars.All)) Vars.Remove(pair.Key); }

        [Test]
        public void Bind_KeepsIdentity_ForAnInactiveObject()
        {
            var go = new GameObject("hidden");
            go.SetActive(false);
            try
            {
                Vars.Bind("x", go);

                Assert.IsTrue(Vars.TryGet("x", out var value));
                Assert.AreSame(go, value, "GameObject.Find không thấy object inactive — biến phải giữ đúng reference");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Bind_KeepsIdentity_ForTheSecondComponentOfTheSameType()
        {
            var go = new GameObject("two", typeof(BoxCollider), typeof(BoxCollider));
            try
            {
                var second = go.GetComponents<BoxCollider>()[1];
                Vars.Bind("c", second);

                Assert.IsTrue(Vars.TryGet("c", out var value));
                Assert.AreSame(second, value, "GetComponent chỉ trả cái đầu tiên");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TryParse_ResolvesAVariable_WhenVarsAreAllowed()
        {
            var go = new GameObject("probe");
            try
            {
                Vars.Bind("g", go);

                Assert.IsTrue(DebugValues.TryParse("$g", typeof(GameObject), out var value, out var error,
                    allowVars: true), error);
                Assert.AreSame(go, value);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TryParse_IgnoresVariables_WhenTheFieldIsANumberField()
        {
            Vars.Bind("n", 5);

            Assert.IsFalse(DebugValues.TryParse("$n", typeof(int), out _, out _));
        }

        [Test]
        public void TryParse_RefusesAVariableOfTheWrongType()
        {
            Vars.Bind("s", "chữ");

            Assert.IsFalse(DebugValues.TryParse("$s", typeof(int), out _, out var error, allowVars: true));
            StringAssert.Contains("int", error.ToLowerInvariant());
        }

        [Test]
        public void TryParse_RefusesAVariableHoldingADestroyedObject()
        {
            var go = new GameObject("dead");
            Vars.Bind("d", go);
            Object.DestroyImmediate(go);

            Assert.IsFalse(DebugValues.TryParse("$d", typeof(GameObject), out _, out var error, allowVars: true));
            StringAssert.Contains("destroy", error.ToLowerInvariant());
        }

        [Test]
        public void TryParse_RefusesAnUnknownVariable()
        {
            Assert.IsFalse(DebugValues.TryParse("$khongco", typeof(int), out _, out var error, allowVars: true));
            StringAssert.Contains("khongco", error);
        }
    }
}
