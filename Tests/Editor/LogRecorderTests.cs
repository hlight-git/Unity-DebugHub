using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogRecorderTests
    {
        private readonly List<LogEntry> got = new();

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            got.Clear();
        }

        [TearDown]
        public void TearDown() => LogRecorder.Reset();

        [Test]
        public void Receive_NumbersEntriesInOrder()
        {
            LogRecorder.Receive("a", "", LogType.Log);
            LogRecorder.Receive("b", "", LogType.Warning);
            Assert.AreEqual(2, LogRecorder.CopySince(0, got));
            Assert.AreEqual(1, got[0].Seq);
            Assert.AreEqual(2, got[1].Seq);
            Assert.AreEqual("b", got[1].Message);
            Assert.AreEqual(LogType.Warning, got[1].Type);
        }

        /// E của OS/SDK có ở mọi phiên: đếm vào thì chấm đỏ luôn sáng. Log Unity lấy lại từ logcat thì vẫn đếm.
        [Test]
        public void Logcat_NativeErrorsDoNotLightTheBadge_UnityOnesDo()
        {
            var time = new System.DateTime(2026, 10, 2, 14, 26, 18);
            LogRecorder.ReceiveNative(time, LogType.Error, "Zygote", "noise", null);
            Assert.AreEqual(0, LogRecorder.ErrorCount);
            LogRecorder.ReceiveNative(time, LogType.Error, null, "NullReferenceException", "Harvest.X:Y()");
            Assert.AreEqual(1, LogRecorder.ErrorCount);

            LogRecorder.CopySince(0, got);
            Assert.AreEqual(LogSource.Native, got[0].Source);
            Assert.AreEqual("Zygote", got[0].Tag);
            Assert.AreEqual(time, got[0].Time, "giữ giờ của logcat, không phải giờ nhận");
            Assert.AreEqual(LogSource.Unity, got[1].Source);
        }

        /// Dòng ghi chú "Ghi từ …" phải tính cả phần lấy lại từ buffer logcat, cũ hơn lúc bắt đầu ghi.
        [Test]
        public void Logcat_OlderThanTheStart_MovesStartedAtBack()
        {
            LogRecorder.Start();
            var older = LogRecorder.StartedAt.AddMinutes(-2);
            LogRecorder.ReceiveNative(older, LogType.Log, "ActivityThread", "bind", null);
            Assert.AreEqual(older, LogRecorder.StartedAt);
        }

        [Test]
        public void CopySince_ReturnsOnlyNewer_AndNothingWhenUpToDate()
        {
            for (var i = 0; i < 5; i++) LogRecorder.Receive("x" + i, null, LogType.Log);
            Assert.AreEqual(2, LogRecorder.CopySince(3, got));
            Assert.AreEqual(4, got[0].Seq);
            got.Clear();
            Assert.AreEqual(0, LogRecorder.CopySince(LogRecorder.LastSeq, got));
            Assert.AreEqual(0, got.Count);
        }

        [Test]
        public void OverBudget_DropsOldestFirst()
        {
            var one = new LogEntry(0, default, LogType.Log, new string('x', 10), null, LogKind.Log, LogSource.Unity).Cost;
            LogRecorder.Budget = one * 3;
            for (var i = 0; i < 5; i++) LogRecorder.Receive(new string('x', 10), null, LogType.Log);
            Assert.AreEqual(2, LogRecorder.Dropped);
            Assert.AreEqual(3, LogRecorder.OldestSeq);
            Assert.AreEqual(3, LogRecorder.CopySince(0, got));
            Assert.AreEqual(3, got[0].Seq);
        }

        [Test]
        public void KeepsTheNewest_EvenIfItAloneIsOverBudget()
        {
            LogRecorder.Budget = 1;
            LogRecorder.Receive(new string('x', 1000), null, LogType.Log);
            Assert.AreEqual(0, LogRecorder.Dropped);
            LogRecorder.Receive("y", null, LogType.Log);
            Assert.AreEqual(1, LogRecorder.Dropped);
            Assert.AreEqual(1, LogRecorder.CopySince(0, got));
            Assert.AreEqual("y", got[0].Message);
        }

        [Test]
        public void ErrorCount_CountsErrorExceptionAssert()
        {
            LogRecorder.Receive("e", null, LogType.Error);
            LogRecorder.Receive("x", null, LogType.Exception);
            LogRecorder.Receive("a", null, LogType.Assert);
            LogRecorder.Receive("w", null, LogType.Warning);
            Assert.AreEqual(3, LogRecorder.ErrorCount);
        }

        [Test]
        public void Mark_OnlyWhileRecording()
        {
            Assert.AreEqual(0, LogRecorder.Mark("x"));
            LogRecorder.Start();
            var seq = LogRecorder.Mark("level.goto 5");
            Assert.Greater(seq, 0);
            LogRecorder.CopySince(0, got);
            Assert.IsTrue(got.Exists(e => e.Seq == seq && e.Kind == LogKind.Command && e.Message == "level.goto 5"));
        }

        [Test]
        public void Receive_FromManyThreads_KeepsEveryEntry()
        {
            var threads = new Thread[4];
            for (var t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    for (var i = 0; i < 500; i++) LogRecorder.Receive("t", null, LogType.Log);
                });
                threads[t].Start();
            }
            foreach (var thread in threads) thread.Join();

            LogRecorder.CopySince(0, got);
            Assert.AreEqual(2000, got.Count + LogRecorder.Dropped);
            for (var i = 1; i < got.Count; i++) Assert.AreEqual(got[i - 1].Seq + 1, got[i].Seq);
        }

        [Test]
        public void RunEntry_MarksTheCommandLine()
        {
            LogRecorder.Start();
            var node = DebugHub.Add(null, "recordertest.ping", "d", () => { });
            try
            {
                Assert.IsTrue(DebugHub.Execute("recordertest.ping", out _));
                LogRecorder.CopySince(0, got);
                Assert.IsTrue(got.Exists(e => e.Kind == LogKind.Command && e.Message == "recordertest.ping"));
            }
            finally { DebugHub.Remove(node); }
        }
    }
}
