using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class OsLogTests
    {
        private readonly List<(LogType Type, string Tag, string Message, string Stack)> got = new();

        /// NUnit dùng một instance cho cả fixture: không xoá thì log của test trước dồn sang test sau.
        [SetUp]
        public void SetUp() => got.Clear();

        private void Collect(System.DateTime _, LogType type, string tag, string message, string stack) =>
            got.Add((type, tag, message, stack));

        private static string Record(double seconds, char level, char origin, string sender, string text) =>
            seconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\u001f" + level + "\u001f" + origin +
            "\u001f" + sender + "\u001f" + text + "\u001e";

        /// Một lượt của plugin: mỗi entry `giây ␟ mức ␟ nguồn ␟ sender ␟ nội dung ␞`. Sender (AppLovinSDK, CFNetwork…) là tag.
        [Test]
        public void Read_TurnsABatchIntoLogs_AndReturnsTheLastTime()
        {
            var batch = Record(100.5, 'I', 'A', "AppLovinSDK", "ad loaded") + Record(101.25, 'E', 'A', "CFNetwork", "Task failed\nline 2");
            var last = OsLog.Read(batch, skipUnity: true, Collect, after: 0);

            Assert.AreEqual(101.25, last);
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(("AppLovinSDK", "ad loaded"), (got[0].Tag, got[0].Message));
            Assert.AreEqual(LogType.Log, got[0].Type);
            Assert.AreEqual(LogType.Error, got[1].Type);
            Assert.AreEqual("Task failed\nline 2", got[1].Message, "log native giữ nguyên nhiều dòng");
        }

        /// Lượt sau đọc tiếp từ giờ cuối: entry cũ hơn hay bằng không vào lần nữa.
        [Test]
        public void Read_SkipsWhatTheLastBatchAlreadyHad()
        {
            OsLog.Read(Record(100, 'I', 'A', "X", "old") + Record(101, 'I', 'A', "X", "new"), true, Collect, after: 100);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("new", got[0].Message);
        }

        /// Log Unity (nguồn U, subsystem riêng của hub): trước khi callback gắn thì lấy, tách dòng đầu; sau đó bỏ.
        [Test]
        public void Read_UnityLogs_KeptBeforeTheCallback_SkippedAfter()
        {
            var batch = Record(100, 'N', 'U', "UnityFramework", "[Save] Loaded\nHarvest.LoadLocalData:Execute()\n");
            OsLog.Read(batch, skipUnity: false, Collect, after: 0);
            Assert.AreEqual(1, got.Count);
            Assert.IsNull(got[0].Tag);
            Assert.AreEqual("[Save] Loaded", got[0].Message);
            Assert.AreEqual("Harvest.LoadLocalData:Execute()", got[0].Stack);

            got.Clear();
            OsLog.Read(batch, skipUnity: true, Collect, after: 0);
            Assert.AreEqual(0, got.Count);
        }

        /// SDK link tĩnh ghi log từ chính UnityFramework: không phải log Unity, vẫn giữ sau khi callback gắn.
        [Test]
        public void Read_StaticSdkLogsFromUnityFramework_AreKept()
        {
            OsLog.Read(Record(100, 'N', 'A', "UnityFramework", "<AppLovinSdk> ready"), skipUnity: true, Collect, after: 0);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("UnityFramework", got[0].Tag);
            Assert.AreEqual("<AppLovinSdk> ready", got[0].Message);
        }

        /// Plugin không mở được store thì trả `!lý do`: báo lên trang log, không im lặng.
        [Test]
        public void Read_ReportsAPluginError()
        {
            var last = OsLog.Read("!Operation not permitted", true, Collect, after: 7);
            Assert.AreEqual(7, last);
            Assert.IsTrue(OsLog.Failed("!Operation not permitted"));
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(LogType.Warning, got[0].Type);
            Assert.AreEqual("DebugHub", got[0].Tag);
            StringAssert.Contains("Operation not permitted", got[0].Message);
        }
    }
}
