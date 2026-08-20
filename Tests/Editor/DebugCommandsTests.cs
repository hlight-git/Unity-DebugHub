using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Storage command của hub: đăng ký, hình thái, owner, chạy bằng dòng lệnh.
    public class DebugCommandsTests
    {
        private const string RECENT_KEY = "DebugHub.RecentCommands";

        private readonly List<DebugCommand> registered = new();
        private string recentBackup;

        private static int lastInt;
        private static bool flag;
        private static string lastPair;

        private static void TakeLevel(int level) => lastInt = level;
        private static void TakePair(string key, int amount) => lastPair = key + amount;

        [SetUp]
        public void SetUp()
        {
            // Recent nằm ở PlayerPrefs thật của Editor: giữ lại rồi trả về, không xoá recent của người dùng.
            recentBackup = PlayerPrefs.GetString(RECENT_KEY, string.Empty);
            PlayerPrefs.DeleteKey(RECENT_KEY);
            registered.Clear();
            lastInt = 0;
            flag = false;
            lastPair = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var command in registered) DebugCommands.Remove(command);
            registered.Clear();
            PlayerPrefs.SetString(RECENT_KEY, recentBackup);
        }

        private DebugCommand Track(DebugCommand command)
        {
            registered.Add(command);
            return command;
        }

        [Test]
        public void Add_SplitsPathIntoSegments_LabelIsLastSegment()
        {
            var command = Track(DebugCommands.Add(null, "a.b.c", "desc", () => { }));

            Assert.AreEqual(new[] { "a", "b", "c" }, command.Segments);
            Assert.AreEqual("c", command.Label);
        }

        /// Tên tham số lấy từ chính delegate nên chỗ đăng ký không phải gõ lại.
        [Test]
        public void Add_TakesParameterNameFromDelegate()
        {
            var command = Track(DebugCommands.Add<int>(null, "level.goto", "desc", TakeLevel));

            Assert.AreEqual("level", command.Parameters[0].Name);
            Assert.AreEqual(typeof(int), command.Parameters[0].Type);
        }

        [Test]
        public void TryRun_ParsesArgumentsAndInvokes()
        {
            var command = Track(DebugCommands.Add<int>(null, "level.goto", "desc", TakeLevel));

            Assert.IsTrue(DebugCommands.TryRun(command, new[] { "42" }, out var message), message);
            Assert.AreEqual(42, lastInt);
        }

        [Test]
        public void TryRun_RejectsInvalidArgumentWithoutInvoking()
        {
            var command = Track(DebugCommands.Add<int>(null, "level.goto", "desc", TakeLevel));
            lastInt = -1;

            Assert.IsFalse(DebugCommands.TryRun(command, new[] { "nope" }, out var message));
            Assert.AreEqual(-1, lastInt);
            StringAssert.Contains("nope", message);
        }

        /// Kết quả trả về là log mà command in ra lúc chạy — đó là cái hiện lên dòng kết quả.
        [Test]
        public void TryRun_ReturnsWhatTheCommandLogged()
        {
            var command = Track(DebugCommands.Add(null, "misc.log", "desc", () => UnityEngine.Debug.Log("coins: 120")));

            Assert.IsTrue(DebugCommands.TryRun(command, null, out var message), message);
            StringAssert.Contains("coins: 120", message);
        }

        [Test]
        public void TryRun_ReportsExceptionAsFailure()
        {
            var command = Track(DebugCommands.Add(null, "misc.throw", "desc",
                () => throw new System.InvalidOperationException("bùm")));

            Assert.IsFalse(DebugCommands.TryRun(command, null, out var message));
            StringAssert.Contains("bùm", message);
        }

        [Test]
        public void AddToggle_IsInline_AndReadsCurrentState()
        {
            flag = true;
            var command = Track(DebugCommands.AddToggle(null, "view.ui", "desc", () => flag, value => flag = value));

            Assert.IsTrue(command.IsInline, "toggle phải là row tại chỗ, không mở page nhập liệu");
            Assert.AreEqual("true", DebugCommands.ToText(command.Parameters[0].Current()));

            Assert.IsTrue(DebugCommands.TryRun(command, new[] { "false" }, out var message), message);
            Assert.IsFalse(flag);
        }

        /// Toggle/value ở lại để bật tắt liên tiếp; chạy cheat thì đóng panel để nhìn game.
        [Test]
        public void Dismiss_DefaultsByShape()
        {
            var toggle = Track(DebugCommands.AddToggle(null, "view.ui", "desc", () => flag, value => flag = value));
            var action = Track(DebugCommands.Add(null, "level.next", "desc", () => { }));
            var withParams = Track(DebugCommands.Add<int>(null, "level.goto", "desc", TakeLevel));

            Assert.AreEqual(DismissMode.Stay, toggle.Dismiss);
            Assert.AreEqual(DismissMode.ClosePanel, action.Dismiss);
            Assert.AreEqual(DismissMode.ClosePanel, withParams.Dismiss);
        }

        [Test]
        public void FluentSetters_OverrideTheDefaults()
        {
            var command = Track(DebugCommands.Add<int>(null, "level.goto", "desc", TakeLevel))
                .Defaults("7").Confirms().HidesHub();

            Assert.AreEqual(new[] { "7" }, command.Args);
            Assert.IsTrue(command.Confirm);
            Assert.AreEqual(DismissMode.HideHub, command.Dismiss);
            Assert.AreEqual(DismissMode.Stay, command.Stays().Dismiss);
        }

        [Test]
        public void AddPage_HasNoRunAndIsNotInline()
        {
            var command = Track(DebugCommands.AddPage(null, "ui.canvases", "desc", _ => { }));

            Assert.IsNull(command.Run);
            Assert.IsFalse(command.IsInline);
            Assert.IsFalse(command.IsInstant);
            Assert.IsFalse(DebugCommands.TryRun(command, null, out var message));
            StringAssert.Contains("page", message);
        }

        /// Owner bị Destroy thì command tự rụng — chỗ đăng ký không phải viết OnDestroy gỡ tay.
        [Test]
        public void All_DropsCommandsWhoseOwnerWasDestroyed()
        {
            var owner = new GameObject("owner");
            var command = DebugCommands.Add(owner, "owned.command", "desc", () => { });
            Assert.Contains(command, (System.Collections.ICollection)DebugCommands.All);

            Object.DestroyImmediate(owner);

            Assert.IsFalse(command.Alive);
            CollectionAssert.DoesNotContain(DebugCommands.All, command);
        }

        [Test]
        public void SeedArgs_PrefillsFromCurrentStateOrParsableDefault()
        {
            var value = Track(DebugCommands.AddValue(null, "time.scale", "desc", () => 2.5f, _ => { }));
            var pair = Track(DebugCommands.Add<string, int>(null, "prefs.set.int", "desc", TakePair));

            Assert.AreEqual(new[] { "2.5" }, DebugCommands.SeedArgs(value));
            Assert.AreEqual(new[] { string.Empty, "0" }, DebugCommands.SeedArgs(pair));
        }

        /// Path không phải key: hai command trùng path là hợp lệ, phân biệt bằng số tham số.
        [Test]
        public void Execute_PicksOverloadByArgumentCount()
        {
            Track(DebugCommands.Add<int>(null, "dup.cmd", "một", TakeLevel));
            Track(DebugCommands.Add<string, int>(null, "dup.cmd", "hai", TakePair));

            Assert.IsTrue(DebugCommands.Execute("dup.cmd 7", out var message), message);
            Assert.AreEqual(7, lastInt);
            Assert.IsNull(lastPair);

            Assert.IsTrue(DebugCommands.Execute("dup.cmd coin 3", out message), message);
            Assert.AreEqual("coin3", lastPair);
        }

        [Test]
        public void Execute_QuotedArgumentStaysOneValue()
        {
            Track(DebugCommands.Add<string, int>(null, "prefs.set.int", "desc", TakePair));

            Assert.IsTrue(DebugCommands.Execute("prefs.set.int \"has space\" 5", out var message), message);
            Assert.AreEqual("has space5", lastPair);
        }

        [Test]
        public void Execute_UnknownCommandFails()
        {
            Assert.IsFalse(DebugCommands.Execute("khong.co 1", out var message));
            StringAssert.Contains("khong.co", message);
        }

        [Test]
        public void Recent_KeepsLastRunFirst()
        {
            var first = Track(DebugCommands.Add(null, "recent.first", "desc", () => { }));
            var second = Track(DebugCommands.Add(null, "recent.second", "desc", () => { }));

            DebugCommands.TryRun(first, null, out _);
            DebugCommands.TryRun(second, null, out _);

            var recent = DebugCommands.Recent;
            Assert.AreEqual(second, recent[0]);
            Assert.AreEqual(first, recent[1]);
        }

        /// Recent lưu path nên command đã gỡ đăng ký thì tự rơi khỏi danh sách, không thành row chết.
        [Test]
        public void Recent_SkipsCommandsNoLongerRegistered()
        {
            var command = DebugCommands.Add(null, "recent.gone", "desc", () => { });
            DebugCommands.TryRun(command, null, out _);
            DebugCommands.Remove(command);

            Assert.AreEqual(0, DebugCommands.Recent.Count);
        }
    }
}
