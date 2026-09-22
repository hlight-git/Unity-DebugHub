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
