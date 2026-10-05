using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogcatTests
    {
        private readonly List<(LogType Type, string Tag, string Message, string Stack)> got = new();

        /// NUnit dùng một instance cho cả fixture: không xoá thì log của test trước dồn sang test sau.
        [SetUp]
        public void SetUp() => got.Clear();

        private Logcat.Sink SinkOf(bool skipUnity) =>
            new(skipUnity, (_, type, tag, message, stack) => got.Add((type, tag, message, stack)));

        private static void Feed(Logcat.Sink sink, params string[] lines)
        {
            foreach (var raw in lines)
            {
                Assert.IsTrue(Logcat.TryParse(raw, 2026, out var line), raw);
                sink.Add(line);
            }
            sink.Flush();
        }

        [Test]
        public void Parse_ReadsAThreadtimeLine()
        {
            Assert.IsTrue(Logcat.TryParse("10-02 14:26:21.750 12915 12930 D UnityAds: Wrote file: /data/x", 2026, out var line));
            Assert.AreEqual(new DateTime(2026, 10, 2, 14, 26, 21, 750), line.Time);
            Assert.AreEqual('D', line.Level);
            Assert.AreEqual("UnityAds", line.Tag);
            Assert.AreEqual("Wrote file: /data/x", line.Text);
            Assert.IsFalse(Logcat.TryParse("--------- beginning of main", 2026, out _));
        }

        [Test]
        public void Parse_KeepsEmptyMessages_AndTrimsPaddedTags()
        {
            Assert.IsTrue(Logcat.TryParse("10-02 14:26:19.511 12915 12930 I Unity   : ", 2026, out var line));
            Assert.AreEqual("Unity", line.Tag);
            Assert.AreEqual(string.Empty, line.Text);
        }

        /// -T in lại các dòng cùng mili giây với dòng cuối của dump: dòng dump đã có thì bỏ, dòng mới cùng mili giây thì giữ.
        [Test]
        public void Stream_SkipsWhatTheDumpHad_KeepsNewLinesOfTheSameMillisecond()
        {
            const string had = "10-02 14:26:21.750 12915 12930 D A: had";
            const string fresh = "10-02 14:26:21.750 12915 12930 D A: fresh";
            Logcat.TryParse(had, 2026, out var last);
            var seen = new HashSet<string> { had };

            Logcat.TryParse("10-02 14:26:21.700 12915 12930 D A: old", 2026, out var old);
            Assert.IsFalse(Logcat.IsNew(old, "old", last.Time, seen));
            Assert.IsFalse(Logcat.IsNew(last, had, last.Time, seen));
            Logcat.TryParse(fresh, 2026, out var sameMs);
            Assert.IsTrue(Logcat.IsNew(sameMs, fresh, last.Time, seen));
            Logcat.TryParse("10-02 14:26:21.751 12915 12930 D A: later", 2026, out var later);
            Assert.IsTrue(Logcat.IsNew(later, "later", last.Time, seen));
        }

        [Test]
        public void Levels_MapToUnityLogTypes()
        {
            foreach (var level in "VDI") Assert.AreEqual(LogType.Log, Logcat.TypeOf(level), level.ToString());
            Assert.AreEqual(LogType.Warning, Logcat.TypeOf('W'));
            foreach (var level in "EFA") Assert.AreEqual(LogType.Error, Logcat.TypeOf(level), level.ToString());
        }

        /// Một lần ghi nhiều dòng (exception Java) ra nhiều dòng cùng header: gộp lại, dòng đầu là message.
        [Test]
        public void Sink_GroupsLinesOfOneWrite_IntoMessageAndStack()
        {
            Feed(SinkOf(skipUnity: true),
                "10-02 14:26:30.100 12915 13001 E AndroidRuntime: FATAL EXCEPTION: main",
                "10-02 14:26:30.100 12915 13001 E AndroidRuntime: java.lang.IllegalStateException: boom",
                "10-02 14:26:30.100 12915 13001 E AndroidRuntime: \tat com.x.Y.z(Y.java:12)",
                "10-02 14:26:30.200 12915 13001 I AppLovinSdk: ready");

            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(LogType.Error, got[0].Type);
            Assert.AreEqual("AndroidRuntime", got[0].Tag);
            Assert.AreEqual("FATAL EXCEPTION: main", got[0].Message);
            StringAssert.Contains("Y.java:12", got[0].Stack);
            Assert.AreEqual("AppLovinSdk", got[1].Tag);
            Assert.IsNull(got[1].Stack);
        }

        /// Log Unity lấy từ buffer khi callback chưa gắn (tag null = nguồn Unity); sau đó callback lo, logcat bỏ.
        /// Unity kết thúc mỗi log bằng một dòng rỗng: hai log trong cùng mili giây không bị gộp.
        [Test]
        public void Sink_UnityTag_IsKeptBeforeTheCallback_AndSkippedAfter()
        {
            string[] lines =
            {
                "10-02 14:26:20.666 12915 12930 I Unity   : Set user_properties : a - 1",
                "10-02 14:26:20.666 12915 12930 I Unity   : PerformanceTracker.TrackerManager:SetUserProperties(T)",
                "10-02 14:26:20.666 12915 12930 I Unity   : ",
                "10-02 14:26:20.666 12915 12930 I Unity   : Set user_properties : b - 2",
                "10-02 14:26:20.666 12915 12930 I Unity   : ",
                "10-02 14:26:20.700 12915 12950 W MiuiPerf: slow",
            };

            Feed(SinkOf(skipUnity: false), lines);
            Assert.AreEqual(3, got.Count);
            Assert.IsNull(got[0].Tag);
            Assert.AreEqual("Set user_properties : a - 1", got[0].Message);
            Assert.AreEqual("PerformanceTracker.TrackerManager:SetUserProperties(T)", got[0].Stack);
            Assert.AreEqual("Set user_properties : b - 2", got[1].Message);
            Assert.IsNull(got[1].Stack);

            got.Clear();
            Feed(SinkOf(skipUnity: true), lines);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("MiuiPerf", got[0].Tag);
        }
    }
}
