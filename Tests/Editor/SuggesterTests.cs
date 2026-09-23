using System.Threading;
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class SuggesterTests
    {
        [Test]
        public void Request_RunsTheWorkOffTheCallingThread()
        {
            var callingThread = Thread.CurrentThread.ManagedThreadId;
            var workerThread = 0;
            var suggester = new Suggester<string>(q => { workerThread = Thread.CurrentThread.ManagedThreadId; return new[] { q }; }, 0f);

            suggester.Request("a");
            WaitFor(() => !suggester.Working);

            Assert.AreNotEqual(callingThread, workerThread, "gợi ý phải chạy thread khác, không chẹn UI");
        }

        [Test]
        public void Results_MatchTheLastQueryThatFinished()
        {
            var suggester = new Suggester<string>(q => new[] { q.ToUpperInvariant() }, 0f);

            suggester.Request("abc");
            WaitFor(() => !suggester.Working);

            Assert.AreEqual("abc", suggester.ResultsFor);
            Assert.AreEqual("ABC", suggester.Results[0]);
        }

        [Test]
        public void ANewRequest_SupersedesTheOneStillRunning()
        {
            var gate = new ManualResetEventSlim(false);
            var suggester = new Suggester<string>(q =>
            {
                if (q == "slow") gate.Wait(2000);
                return new[] { q };
            }, 0f);

            suggester.Request("slow");
            suggester.Request("fast");
            WaitFor(() => suggester.ResultsFor == "fast");
            gate.Set();
            Thread.Sleep(50);

            Assert.AreEqual("fast", suggester.ResultsFor);
        }

        [Test]
        public void RepeatingTheSameQuery_DoesNotRunTheWorkAgain()
        {
            var runs = 0;
            var suggester = new Suggester<string>(q => { Interlocked.Increment(ref runs); return new[] { q }; }, 0f);

            suggester.Request("a");
            WaitFor(() => !suggester.Working);
            suggester.Request("a");
            suggester.Request("a");
            WaitFor(() => !suggester.Working);

            Assert.AreEqual(1, runs, "trang Live gọi Request 4 lần/giây — lặp lại cùng query không được chạy lại");
        }

        [Test]
        public void AThrowingWorker_DoesNotEscape_AndLeavesAnEmptyResult()
        {
            var suggester = new Suggester<string>(q => throw new System.Exception("bùm"), 0f);

            Assert.DoesNotThrow(() => suggester.Request("a"));
            WaitFor(() => !suggester.Working);

            Assert.AreEqual(0, suggester.Results.Count);
        }

        private static void WaitFor(System.Func<bool> done)
        {
            var deadline = System.DateTime.UtcNow.AddSeconds(3);
            while (!done() && System.DateTime.UtcNow < deadline) Thread.Sleep(5);
            Assert.IsTrue(done(), "quá hạn chờ");
        }
    }
}
