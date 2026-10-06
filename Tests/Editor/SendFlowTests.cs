using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hlight.Debug.Hub.Tests
{
    /// Spec ④ §3: một kênh gửi. Bấm qua DebugRegistry.Run như row Gửi thật để thấy đúng dòng kết quả và
    /// row chạy được hay hỏng (hỏng = panel không đóng).
    public class SendFlowTests
    {
        private readonly List<(string text, bool error)> shown = new();
        private SendFlow flow;
        private int sends;

        [SetUp]
        public void SetUp()
        {
            shown.Clear();
            sends = 0;
            flow = new SendFlow("Đã gửi.", (text, error) => shown.Add((text, error)));
        }

        private bool Tap(Func<Task<string>> send, out string message)
        {
            var node = Node.Action("Gửi", () => flow.Start(() =>
            {
                sends++;
                return send();
            }));
            return DebugRegistry.Run(node, Array.Empty<string>(), out message);
        }

        [Test]
        public void AlreadyDone_ShowsTheResultAtOnce()
        {
            Assert.IsTrue(Tap(() => Task.FromResult("BUG-7"), out var message));
            Assert.AreEqual("BUG-7", message);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void EmptyResult_ShowsTheDefaultText()
        {
            Tap(() => Task.FromResult<string>(null), out var message);
            Assert.AreEqual("Đã gửi.", message);
        }

        /// Validate ném trước await đầu tiên ra Task lỗi sẵn: lỗi tại chỗ, form còn mở để sửa.
        [Test]
        public void AlreadyFaulted_FailsTheTap()
        {
            LogAssert.Expect(LogType.Exception, "Exception: chưa chọn assignee");
            Assert.IsFalse(Tap(() => Task.FromException<string>(new Exception("chưa chọn assignee")), out var message));
            Assert.AreEqual("chưa chọn assignee", message);
            Assert.IsFalse(flow.Busy);
        }

        /// Send không async mà ném thẳng: cùng đường với Task lỗi sẵn.
        [Test]
        public void ThrowingSend_FailsTheTap()
        {
            LogAssert.Expect(LogType.Exception, "Exception: không có mạng");
            Assert.IsFalse(Tap(() => throw new Exception("không có mạng"), out var message));
            Assert.AreEqual("không có mạng", message);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void NullTask_FailsTheTap()
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Send trả về null"));
            Assert.IsFalse(Tap(() => null, out _));
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void Pending_IsBusy_AndASecondTapDoesNotSendAgain()
        {
            var pending = new TaskCompletionSource<string>();
            Tap(() => pending.Task, out var first);
            Assert.AreEqual("Đang gửi…", first);
            Assert.IsTrue(flow.Busy);

            Tap(() => pending.Task, out var second);
            Assert.AreEqual(1, sends);
            Assert.AreEqual("Đang gửi lần trước…", second);
        }

        /// EditMode không có hub để chờ Task: gọi Finish thẳng như coroutine của DebugHub.Watch sẽ gọi.
        [Test]
        public void PendingThenDone_ShowsTheResult_AndFreesTheChannel()
        {
            var pending = new TaskCompletionSource<string>();
            Tap(() => pending.Task, out _);
            pending.SetResult("BUG-8");
            flow.Finish(pending.Task);

            Assert.AreEqual(("BUG-8", false), shown[0]);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void PendingThenFaulted_ShowsTheErrorInRed()
        {
            var pending = new TaskCompletionSource<string>();
            Tap(() => pending.Task, out _);
            LogAssert.Expect(LogType.Exception, "Exception: 500");
            pending.SetException(new Exception("500"));
            flow.Finish(pending.Task);

            Assert.AreEqual(("500", true), shown[0]);
            Assert.IsFalse(flow.Busy);
        }
    }
}
