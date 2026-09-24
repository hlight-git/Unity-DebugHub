using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hlight.Debug.Hub.Tests
{
    /// Storage của hub: đăng ký, key, owner, trùng path, và chạy bằng dòng lệnh.
    public class DebugRegistryTests
    {
        private const string LAST_KEY = "DebugHub.LastCommand";

        private readonly List<DebugNode> registered = new();
        private string lastBackup;
        private static int takenInt;
        private static bool flag;

        private static void TakeLevel(int level) => takenInt = level;

        [SetUp]
        public void SetUp()
        {
            lastBackup = PlayerPrefs.GetString(LAST_KEY, string.Empty);
            PlayerPrefs.DeleteKey(LAST_KEY);
            registered.Clear();
            takenInt = 0;
            flag = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var node in registered) DebugHub.Remove(node);
            registered.Clear();
            PlayerPrefs.SetString(LAST_KEY, lastBackup);
        }

        /// Trang Params/Xác nhận giữ entry qua lần đóng panel: owner chết trong lúc đó thì bấm Chạy không
        /// được gọi delegate của object đã bị Destroy.
        [Test]
        public void RunEntry_RefusesAnEntryWhoseOwnerWasDestroyed()
        {
            var owner = new GameObject("owner");
            var ran = false;
            Track(DebugHub.Add(owner, "deadtest.run", "d", () => ran = true));
            Assert.IsTrue(DebugRegistry.Find("deadtest.run", out var entry));
            Object.DestroyImmediate(owner);

            Assert.IsFalse(DebugRegistry.RunEntry(entry, System.Array.Empty<string>(), out var message));
            Assert.IsFalse(ran);
            StringAssert.Contains("gỡ", message);
        }

        private T Track<T>(T node) where T : DebugNode
        {
            registered.Add(node);
            return node;
        }

        [Test]
        public void Add_UsesLastSegmentAsLabel()
        {
            var node = Track(DebugHub.Add(null, "a.b.c", "desc", () => { }));

            Assert.AreEqual("c", node.Label);
        }

        [Test]
        public void Label_KeepsOriginalCasing_EvenThoughLookupIsLowercase()
        {
            var node = Track(DebugHub.Add(null, "AdMob.Reset", "desc", () => { }));

            Assert.AreEqual("Reset", node.Label);
            Assert.IsTrue(DebugHub.Execute("admob.reset", out _));
        }

        [Test]
        public void Key_IsStable_WhenAnOverloadIsAddedLater()
        {
            var one = Track(DebugHub.Add<int>(null, "dup.cmd", "d", TakeLevel));
            var keyBefore = one.Key;

            Track(DebugHub.Add(null, "dup.cmd", "d", () => { }));

            Assert.AreEqual(keyBefore, one.Key);
            Assert.AreNotEqual(one.Key, registered[1].Key);
        }

        [Test]
        public void DuplicatePathAndArity_LogsError_ButStillReturnsAChainableNode()
        {
            Track(DebugHub.Add(null, "same.path", "d", () => { }));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("same.path"));
            var second = DebugHub.Add(null, "same.path", "d", () => { }).Stays();

            Assert.IsNotNull(second);
            Assert.AreEqual(DismissMode.Stay, second.Dismiss);
            Assert.AreEqual(1, CountOf("same.path"));
        }

        [Test]
        public void ReRegisteringAfterOwnerDied_IsNotBlockedByTheDeadEntry()
        {
            var owner = new GameObject("owner");
            DebugHub.Add(owner, "scene.cmd", "d", () => { });
            Object.DestroyImmediate(owner);

            // Không đọc DebugRegistry.All ở giữa: đúng thứ tự mà scene load lại gây ra.
            Track(DebugHub.Add(null, "scene.cmd", "d", () => { }));

            Assert.AreEqual(1, CountOf("scene.cmd"));
        }

        [Test]
        public void Execute_SetsValueNodeWithOneArgument_AndLogsItWithNone()
        {
            Track(DebugHub.AddValue(null, "view.fps", "d", () => flag, v => flag = v));

            Assert.IsTrue(DebugHub.Execute("view.fps true", out _));
            Assert.IsTrue(flag);

            Assert.IsTrue(DebugHub.Execute("view.fps", out var message));
            StringAssert.Contains("true", message);
        }

        [Test]
        public void Execute_RefusesFolderNode()
        {
            Track(DebugHub.AddFolder(null, "info.app", "d", () => new DebugNode[0]));

            Assert.IsFalse(DebugHub.Execute("info.app", out var message));
            StringAssert.Contains("thư mục", message);
        }

        [Test]
        public void Execute_RefusesReadOnlyValueNode()
        {
            Track(DebugHub.AddValue<int>(null, "stat.level", "d", () => 3, null));

            Assert.IsFalse(DebugHub.Execute("stat.level 9", out var message));
            StringAssert.Contains("read-only", message);
        }

        [Test]
        public void LastCommand_RecordsWhatJustRan_QuotingArgumentsThatNeedIt()
        {
            Track(DebugHub.Add<int>(null, "level.goto", "d", TakeLevel));

            Assert.IsTrue(DebugHub.Execute("level.goto 5", out _));

            Assert.AreEqual(5, takenInt);
            Assert.AreEqual("level.goto 5", DebugRegistry.LastCommand);
        }

        [Test]
        public void Run_ReportsExceptionAsFailure()
        {
            var node = Track(DebugHub.Add(null, "boom", "d",
                () => throw new System.InvalidOperationException("bùm")));

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: bùm");
            Assert.IsFalse(DebugRegistry.Run(node, new string[0], out var message));
            StringAssert.Contains("bùm", message);
        }

        [Test]
        public void Defaults_SeedsArguments_ButDoesNotOverwriteWhatUserTyped()
        {
            var node = Track(DebugHub.Add<int>(null, "time.skip", "d", TakeLevel).Defaults("7"));
            Assert.AreEqual(new[] { "7" }, DebugRegistry.ArgsFor(node));

            DebugRegistry.StoreArgs(node, new[] { "9" });
            node.Defaults("7");

            Assert.AreEqual(new[] { "9" }, DebugRegistry.ArgsFor(node));
        }

        /// Bug đã sửa: Remove() từng chỉ gỡ khỏi `entries` mà quên dọn `argsByKey`, nên args của một
        /// node đã gỡ tay rò rỉ sang node KHÁC đăng ký lại đúng path sau đó — lộ ra dưới dạng chạy lại
        /// [SetUp]/[TearDown] hai lần trong cùng một domain (đúng thứ tự TearDown của chính test này).
        [Test]
        public void Remove_ClearsStoredArgs_SoANewRegistrationAtThatPathStartsFresh()
        {
            var first = DebugHub.Add<int>(null, "time.repeat", "d", TakeLevel);
            DebugRegistry.StoreArgs(first, new[] { "9" });

            DebugHub.Remove(first);

            var second = Track(DebugHub.Add<int>(null, "time.repeat", "d", TakeLevel).Defaults("7"));
            Assert.AreEqual(new[] { "7" }, DebugRegistry.ArgsFor(second));
        }

        [Test]
        public void Run_ReportsTheThrownException_NotItsCause()
        {
            var node = Track(DebugHub.Add(null, "wrap.boom", "d",
                () => throw new System.InvalidOperationException("ngoài", new System.ArgumentException("trong"))));

            // Unity log cả chuỗi inner exception thành nhiều dòng; thứ cần kiểm là message trả về.
            LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(DebugRegistry.Run(node, new string[0], out var message));
            StringAssert.Contains("ngoài", message);
        }

        [Test]
        public void Run_CapturesLogs_EvenWhenUnityLoggingIsSilenced()
        {
            var node = Track(DebugHub.Add(null, "quiet.log", "d", () => UnityEngine.Debug.Log("vẫn thấy")));
            var logger = UnityEngine.Debug.unityLogger;
            logger.logEnabled = false;
            try
            {
                Assert.IsTrue(DebugRegistry.Run(node, new string[0], out var message));
                StringAssert.Contains("vẫn thấy", message);
                Assert.IsFalse(logger.logEnabled, "chạy xong phải trả logger về như cũ");
            }
            finally { logger.logEnabled = true; }
        }

        [Test]
        public void OwnerDestroyedBeforeRegistering_DoesNotMakeTheNodeImmortal()
        {
            var owner = new GameObject("dead owner");
            Object.DestroyImmediate(owner);

            DebugHub.Add(owner, "ghost.cmd", "d", () => { });

            Assert.AreEqual(0, CountOf("ghost.cmd"));
        }

        [Test]
        public void Defaults_OnARejectedDuplicate_DoesNotTouchTheRegisteredNode()
        {
            var kept = Track(DebugHub.Add<int>(null, "dup.args", "d", TakeLevel));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("dup.args"));
            DebugHub.Add<int>(null, "dup.args", "d", TakeLevel).Defaults("99");

            CollectionAssert.AreNotEqual(new[] { "99" }, DebugRegistry.ArgsFor(kept));
        }

        [Test]
        public void Execute_RecordsZeroArgumentCommands_LikeThePanelDoes()
        {
            Track(DebugHub.Add(null, "level.next", "d", () => { }));

            Assert.IsTrue(DebugHub.Execute("level.next", out _));

            Assert.AreEqual("level.next", DebugRegistry.LastCommand);
        }

        [Test]
        public void Execute_ReadingAValue_IsNotRecorded()
        {
            Track(DebugHub.Add<int>(null, "level.goto", "d", TakeLevel));
            Track(DebugHub.AddValue(null, "view.fps", "d", () => flag, v => flag = v));
            DebugHub.Execute("level.goto 5", out _);

            DebugHub.Execute("view.fps", out _);

            Assert.AreEqual("level.goto 5", DebugRegistry.LastCommand);
        }

        [Test]
        public void Execute_WithAVariableArgument_ClearsTheStaleLastCommand()
        {
            Track(DebugHub.Add<int>(null, "level.goto", "d", TakeLevel));
            Track(DebugHub.Add<GameObject>(null, "pick.go", "d", _ => { }));
            DebugHub.Execute("level.goto 5", out _);
            var go = new GameObject("var target");
            Vars.Bind("g", go);
            try
            {
                Assert.IsTrue(DebugHub.Execute("pick.go $g", out _));
                Assert.IsEmpty(DebugRegistry.LastCommand, "reference không ghi lại được — xoá, không giữ lệnh cũ");
            }
            finally
            {
                Vars.Remove("g");
                Object.DestroyImmediate(go);
            }
        }

        private static int CountOf(string path)
        {
            var count = 0;
            foreach (var entry in DebugRegistry.All)
            {
                if (string.Equals(entry.Path, path, System.StringComparison.OrdinalIgnoreCase)) count++;
            }
            return count;
        }
    }
}
