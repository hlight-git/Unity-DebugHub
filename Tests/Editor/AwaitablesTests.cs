using System.Threading.Tasks;
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class AwaitablesTests
    {
        private struct FakeAwaitable
        {
            public FakeAwaiter GetAwaiter() => new FakeAwaiter();
        }

        private struct FakeAwaiter : System.Runtime.CompilerServices.INotifyCompletion
        {
            public bool IsCompleted => true;
            public int GetResult() => 42;
            public void OnCompleted(System.Action continuation) => continuation();
        }

        [Test]
        public void IsAwaitable_RecognisesTheAwaiterPattern_NotJustTask()
        {
            Assert.IsTrue(Awaitables.IsAwaitable(typeof(Task)));
            Assert.IsTrue(Awaitables.IsAwaitable(typeof(Task<int>)));
            Assert.IsTrue(Awaitables.IsAwaitable(typeof(ValueTask)));
            Assert.IsTrue(Awaitables.IsAwaitable(typeof(FakeAwaitable)),
                "nhận theo mẫu GetAwaiter/IsCompleted/GetResult thì UniTask cũng vào, không cần tham chiếu package");
        }

        [Test]
        public void IsAwaitable_SaysNo_ForOrdinaryTypes()
        {
            Assert.IsFalse(Awaitables.IsAwaitable(typeof(int)));
            Assert.IsFalse(Awaitables.IsAwaitable(typeof(void)));
            Assert.IsFalse(Awaitables.IsAwaitable(typeof(System.Collections.IEnumerator)),
                "coroutine của Unity không theo mẫu awaiter — ngoài phạm vi");
        }

        [Test]
        public void Wait_ReportsTheResult_WhenAlreadyComplete()
        {
            object got = null;
            System.Exception failed = null;
            var routine = Awaitables.Wait(new FakeAwaitable(), (r, e) => { got = r; failed = e; });
            while (routine.MoveNext()) { }

            Assert.AreEqual(42, got);
            Assert.IsNull(failed);
        }

        [Test]
        public void Wait_ReportsTheException_InsteadOfThrowingIntoTheCaller()
        {
            object got = null;
            System.Exception failed = null;
            var task = Task.FromException(new System.InvalidOperationException("bùm"));
            var routine = Awaitables.Wait(task, (r, e) => { got = r; failed = e; });
            while (routine.MoveNext()) { }

            Assert.IsNull(got);
            Assert.IsNotNull(failed);
            StringAssert.Contains("bùm", failed.Message);
        }

        [Test]
        public void ReflectedMethod_IsMarkedAwaitable_WhenItReturnsATask()
        {
            var cursor = new Cursor(typeof(Sample), new Sample(), null);
            var nodes = new System.Collections.Generic.List<DebugNode>(Reflect.Methods(cursor, null));

            Assert.IsTrue(((ActionNode)nodes.Find(n => n.Label == "Slow")).Awaitable);
            Assert.IsFalse(((ActionNode)nodes.Find(n => n.Label == "Fast")).Awaitable);
        }

        /// Method awaitable 0 tham số phải mở trang tham số — nơi duy nhất có switch `Chờ kết quả`.
        [Test]
        public void AwaitableWithoutParameters_OpensTheParamsPage_WithTheAwaitSwitch()
        {
            var panel = TestPanel.Build();
            try
            {
                var cursor = new Cursor(typeof(Sample), new Sample(), null);
                var slow = new System.Collections.Generic.List<DebugNode>(Reflect.Methods(cursor, null))
                    .Find(n => n.Label == "Slow");

                panel.ShowFromRoot(new DebugPage("t", p => NodeRenderer.Render(p, slow, (n, v) => { })));
                TestPanel.ClickRowContaining(panel, "Slow");

                Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("Chờ kết quả")));
            }
            finally { TestPanel.Destroy(panel); }
        }

        private class Sample
        {
            public Task Slow() => Task.CompletedTask;
            public int Fast() => 1;
        }
    }
}
