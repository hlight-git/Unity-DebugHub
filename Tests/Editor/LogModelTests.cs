using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogModelTests
    {
        private LogModel model;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            model = new LogModel();
        }

        [TearDown]
        public void TearDown() => LogRecorder.Reset();

        private static void Log(string message, LogType type = LogType.Log, string stack = null) =>
            LogRecorder.Receive(message, stack, type);

        [Test]
        public void Pull_TurnsEntriesIntoRows_AfterTheNote()
        {
            Log("a"); Log("b"); Log("c");
            Assert.AreEqual(3, model.Pull());
            Assert.AreEqual(4, model.RowCount);
            Assert.IsNull(model.RowAt(0));
            Assert.AreEqual("a", model.RowAt(1).Entry.Message);
            Assert.AreEqual(0, model.Pull(), "kéo lần hai không có gì mới");
        }

        [Test]
        public void Counts_StayTotal_WhenFilteringAndSearching()
        {
            Log("l1"); Log("l2"); Log("w", LogType.Warning); Log("e", LogType.Error); Log("x", LogType.Exception);
            model.Pull();
            Assert.AreEqual(2, model.Count(LogGroup.Log));
            Assert.AreEqual(1, model.Count(LogGroup.Warning));
            Assert.AreEqual(2, model.Count(LogGroup.Error));

            model.Toggle(LogGroup.Error);
            Assert.IsFalse(model.IsShown(LogGroup.Error));
            Assert.AreEqual(1 + 3, model.RowCount);
            Assert.AreEqual(2, model.Count(LogGroup.Error));

            model.Query = "w";
            Assert.AreEqual(1 + 1, model.RowCount);
            Assert.AreEqual(5, model.Total);
        }

        [Test]
        public void Search_MatchesMessageOrCaller_IgnoringCase()
        {
            Log("tap", LogType.Log, "Harvest.ItemPiece:OnPointerUp () (at Assets/ItemPiece.cs:212)");
            Log("other");
            model.Pull();
            model.Query = "itempiece";
            Assert.AreEqual(2, model.RowCount);
            Assert.AreEqual("tap", model.RowAt(1).Entry.Message);
            model.Query = "zzz";
            Assert.AreEqual(1, model.RowCount);
        }

        /// Log logcat không có stack C#: dòng phụ, tìm và Copy dùng tag của nó.
        [Test]
        public void NativeEntries_UseTheirTag_AsCaller_InSearchAndCopy()
        {
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Warning, "AppLovinSdk", "slow", null);
            model.Pull();
            Assert.AreEqual("AppLovinSdk", model.RowAt(1).Caller);
            StringAssert.Contains("AppLovinSdk: slow", LogModel.Describe(model.RowAt(1)));
            model.Query = "applovin";
            Assert.AreEqual(2, model.RowCount);
        }

        private static void Native(string tag, string message = "x") =>
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Log, tag, message, null);

        /// Logcat gộp các lần ghi cùng mili giây thành một log: các dòng sau vẫn phải tìm được.
        [Test]
        public void Search_FindsTheFollowingLinesOfANativeLog()
        {
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Log, "AppLovinSdk", "config:", "adapter: Mintegral 16.8");
            model.Pull();
            model.Query = "mintegral";
            Assert.AreEqual(2, model.RowCount);
        }

        /// Gộp theo cả tag: cùng nội dung mà khác tag là hai nguồn khác nhau.
        [Test]
        public void Collapse_KeepsTheSameTextFromDifferentTagsApart()
        {
            Native("AppLovinSdk", "ready"); Native("UnityAds", "ready"); Log("ready");
            model.Pull();
            model.Collapse = true;
            Assert.AreEqual(1 + 3, model.RowCount);
        }

        /// Chip Unity: chỉ còn log Unity, kể cả với log Android tới sau khi bật. Số trên chip loại vẫn là tổng.
        [Test]
        public void UnityOnly_HidesAndroidLogs_IncludingLaterOnes()
        {
            Log("game");
            model.Pull();
            Assert.IsFalse(model.HasNative, "chưa có log Android thì chip không có gì để lọc");

            Native("AppLovinSdk");
            model.Pull();
            Assert.IsTrue(model.HasNative);
            model.UnityOnly = true;
            Native("MIUIInput");
            model.Pull();
            Assert.AreEqual(1 + 1, model.RowCount);
            Assert.AreEqual("game", model.RowAt(1).Entry.Message);
            Assert.AreEqual(3, model.Total);
        }

        [Test]
        public void ResetFilters_TurnsUnityOnlyOff()
        {
            Log("game"); Native("AppLovinSdk");
            model.Pull();
            model.UnityOnly = true;
            model.ResetFilters();
            Assert.IsFalse(model.UnityOnly);
            Assert.AreEqual(1 + 2, model.RowCount);
        }

        /// Tìm và Copy đi trên chữ nhìn thấy: tag rich text không khớp, không lẫn vào chỗ dán.
        [Test]
        public void SearchAndCopy_UseTheVisibleText_NotTheRichTextTags()
        {
            Log("<color=#FF0000><b>boom</b></color>");
            model.Pull();
            model.Query = "color";
            Assert.AreEqual(1, model.RowCount);
            model.Query = "boom";
            Assert.AreEqual(2, model.RowCount);
            StringAssert.EndsWith("] boom", model.CopyAll(out _));
        }

        [Test]
        public void Collapse_KeepsTheFirstPosition_AndCountsRepeats()
        {
            Log("a"); Log("b"); Log("a"); Log("a");
            model.Pull();
            model.Collapse = true;
            Assert.AreEqual(3, model.RowCount);
            Assert.AreEqual("a", model.RowAt(1).Entry.Message);
            Assert.AreEqual(3, model.RowAt(1).Repeat);

            var version = model.Version;
            Log("a");
            Assert.AreEqual(0, model.Pull(), "log trùng không thêm hàng");
            Assert.AreEqual(4, model.RowAt(1).Repeat);
            Assert.AreNotEqual(version, model.Version, "hàng đã có đổi nội dung → view phải dựng lại");
        }

        [Test]
        public void Eviction_DropsRowsTheRecorderNoLongerHas()
        {
            LogRecorder.Budget = new LogEntry(0, default, LogType.Log, "x0", null, LogKind.Log, LogSource.Unity).Cost * 3;
            for (var i = 0; i < 3; i++) Log("x" + i);
            model.Pull();
            var version = model.Version;

            for (var i = 3; i < 6; i++) Log("x" + i);
            var appended = model.Pull();
            Assert.AreEqual(1 + 3, model.RowCount);
            Assert.AreEqual(LogRecorder.OldestSeq, model.RowAt(1).Entry.Seq);
            Assert.AreNotEqual(version, model.Version, "bỏ log cũ → view phải dựng lại");
            Assert.Greater(appended, 0);
        }

        [Test]
        public void CopyAll_KeepsTheTail_WhenTheNewestBlockAloneIsOverTheLimit()
        {
            Log(new string('a', LogModel.COPY_LIMIT) + "0123456789");
            model.Pull();
            var text = model.CopyAll(out var kept);
            Assert.IsNotEmpty(text);
            Assert.LessOrEqual(text.Length, LogModel.COPY_LIMIT);
            StringAssert.EndsWith("0123456789", text);
            Assert.AreEqual(1, kept);
        }

        /// "đã copy N log" / "Kèm N log" phải đếm log thật sự nằm trong chuỗi, không phải số hàng đang hiện.
        [Test]
        public void CopyAll_CountsOnlyTheLogsItKept()
        {
            var half = new string('a', LogModel.COPY_LIMIT / 2);
            Log(half); Log(half); Log(half);
            model.Pull();

            model.CopyAll(out var kept);

            Assert.AreEqual(1, kept);
        }

        [Test]
        public void Clear_HidesEverythingSoFar_UnclearBringsItBack()
        {
            Log("a"); Log("b"); Log("c");
            model.Pull();
            model.Clear();
            Assert.AreEqual(1, model.RowCount);
            Assert.IsTrue(model.Cleared);
            StringAssert.Contains("đã ẩn 3", model.NoteText());
            Assert.AreEqual(0, model.Total);

            Log("d");
            model.Pull();
            Assert.AreEqual(2, model.RowCount);

            model.Unclear();
            Assert.AreEqual(5, model.RowCount);
        }

        [Test]
        public void Markers_IgnoreTypeFilters_AndAreFoundBySeq()
        {
            LogRecorder.Start();
            Log("before");
            var seq = LogRecorder.Mark("level.goto 5");
            Log("after");
            model.Pull();
            model.Toggle(LogGroup.Log);
            var index = model.IndexOfSeq(seq);
            Assert.Greater(index, 0);
            Assert.IsTrue(model.RowAt(index).IsMarker);
            Assert.AreEqual("level.goto 5", model.RowAt(index).Entry.Message);
        }

        [Test]
        public void Neighbour_SkipsMarkers()
        {
            LogRecorder.Start();
            Log("first");
            LogRecorder.Mark("cmd");
            Log("second");
            model.Pull();
            LogItem first = null;
            for (var i = 1; i < model.RowCount; i++)
            {
                if (model.RowAt(i).Entry.Message == "first") first = model.RowAt(i);
            }
            Assert.IsNotNull(first);
            Assert.AreEqual("second", model.Neighbour(first, 1).Entry.Message);
            Assert.IsNull(model.Neighbour(first, -1));
        }

        [Test]
        public void Describe_HasTimeTypeMessageAndStack()
        {
            Log("boom", LogType.Error, "A:B () (at A.cs:1)\n");
            model.Pull();
            var text = LogModel.Describe(model.RowAt(1));
            StringAssert.Contains("[Lỗi]", text);
            StringAssert.Contains("boom", text);
            StringAssert.Contains("A.cs:1", text);
        }

        [Test]
        public void FollowsByDefault()
        {
            Assert.IsTrue(model.Follow);
        }

        /// Spec ④ §2.1: log của báo lỗi không phụ thuộc chip QA quên tắt, nhưng tôn trọng Xoá ("tính từ đây").
        [Test]
        public void AllText_IgnoresFiltersSearchAndCollapse_ButStartsAfterClear()
        {
            Log("cũ");
            model.Pull();
            model.Clear();
            Log("một"); Log("cảnh-báo", LogType.Warning); Log("một");
            model.Pull();
            model.Toggle(LogGroup.Warning);
            model.Query = "zzz";
            model.Collapse = true;

            var text = model.AllText();

            StringAssert.DoesNotContain("cũ", text);
            StringAssert.Contains("cảnh-báo", text);
            Assert.AreEqual(3, text.Split('\n').Length, "không gộp hai dòng `một`");
        }

        /// Đọc thẳng bộ ghi: không được ăn mất "N log mới" của trang log.
        [Test]
        public void AllText_DoesNotPullIntoTheModel()
        {
            Log("x");
            model.AllText();
            Assert.AreEqual(1, model.Pull());
        }
    }
}
