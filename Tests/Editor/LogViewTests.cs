using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    public class LogViewTests
    {
        private DebugHubPanel panel;
        private LogView view;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            panel = TestPanel.Build();
            view = (LogView)TestPanel.Field(panel, "logView");
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
        }

        private void Open(long focusSeq = 0)
        {
            panel.Show(CommandsPage.Root());
            panel.Push(LogPage.Build(focusSeq));
            view.Tick();
        }

        private static void Logs(int n, LogType type = LogType.Log)
        {
            for (var i = 0; i < n; i++) LogRecorder.Receive($"log {i}", null, type);
        }

        private RectTransform Viewport => TestPanel.ScrollOf(panel).viewport;

        [Test]
        public void LogPage_SwapsTheScrollContent_AndBackRestoresIt()
        {
            Open();
            var scroll = TestPanel.ScrollOf(panel);
            Assert.IsTrue(view.IsOpen);
            Assert.AreSame(view.Content, scroll.content);
            Assert.IsFalse(panel.transform.Find("Window/Scroll View/Viewport/Content").gameObject.activeSelf);

            panel.Pop();
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual("Content", scroll.content.name);
            Assert.IsTrue(scroll.content.gameObject.activeSelf);
        }

        [Test]
        public void ThousandsOfLogs_OnlyBuildTheVisibleRows()
        {
            Logs(2000);
            Open();
            Assert.AreEqual(LogModel.Shared.RowCount * LogView.ROW_HEIGHT, view.Content.rect.height, 0.5f);
            var expected = Mathf.CeilToInt(Viewport.rect.height / LogView.ROW_HEIGHT) + 2;
            Assert.Greater(view.ActiveRowCount, 0);
            Assert.LessOrEqual(view.ActiveRowCount, expected);
        }

        [Test]
        public void LogPage_UsesTheFullPanelHeight_EvenWhenEmpty()
        {
            Open();
            var window = (RectTransform)TestPanel.Field(panel, "window");
            Assert.AreEqual(panel.MaxWindowHeight, window.rect.height, 1f);
        }

        [Test]
        public void Header_ShowsLogOnRoot_AndMoreOnTheLogPage()
        {
            var log = (Button)TestPanel.Field(panel, "logButton");
            var more = (Button)TestPanel.Field(panel, "moreButton");
            panel.Show(CommandsPage.Root());
            Assert.IsTrue(log.gameObject.activeSelf);
            Assert.IsFalse(more.gameObject.activeSelf);

            panel.Push(LogPage.Build());
            Assert.IsFalse(log.gameObject.activeSelf);
            Assert.IsTrue(more.gameObject.activeSelf);
        }

        /// Chip Unity: ẩn khi chưa có log native (Editor); đang bật thì luôn hiện — Xoá xong chưa có log native mới mà ẩn
        /// chip thì không tắt lọc được.
        [Test]
        public void UnityChip_ShowsWithNativeLogs_AndStaysWhileOn()
        {
            var chip = panel.transform.Find("Window/LogBar/ChipUnity").gameObject;
            Logs(1);
            Open();
            Assert.IsFalse(chip.activeSelf);

            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Log, "AppLovinSdk", "ready", null);
            view.Tick();
            Assert.IsTrue(chip.activeSelf);
            chip.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(LogModel.Shared.UnityOnly);

            LogModel.Shared.Clear();
            view.Tick();
            Assert.IsTrue(chip.activeSelf, "đang lọc thì chip phải còn để tắt");
        }

        [Test]
        public void ErrorChip_HidesErrors_ButKeepsTheCount()
        {
            Logs(3);
            Logs(2, LogType.Error);
            Open();
            panel.transform.Find("Window/LogBar/ChipError").GetComponent<Button>().onClick.Invoke();
            view.Tick();
            Assert.AreEqual(1 + 3, LogModel.Shared.RowCount);
            Assert.AreEqual(2, LogModel.Shared.Count(LogGroup.Error));
            Assert.AreEqual(LogModel.Shared.RowCount * LogView.ROW_HEIGHT, view.Content.rect.height, 0.5f);
        }

        [Test]
        public void HidingTheBulk_ShrinksTheList_AndKeepsRowsVisible()
        {
            Logs(2000);
            Logs(3, LogType.Warning);
            Open();
            panel.transform.Find("Window/LogBar/ChipLog").GetComponent<Button>().onClick.Invoke();
            view.Tick();
            Assert.AreEqual(1 + 3, LogModel.Shared.RowCount);
            Assert.AreEqual(LogModel.Shared.RowCount * LogView.ROW_HEIGHT, view.Content.rect.height, 0.5f);
            Assert.Greater(view.ActiveRowCount, 0);
        }

        private LogRowView RowShowing(int index)
        {
            foreach (var row in view.Content.GetComponentsInChildren<LogRowView>())
            {
                if (row.Index == index) return row;
            }
            Assert.Fail($"không có hàng nào đang hiện chỉ số {index}");
            return null;
        }

        private static string TextOf(LogRowView row, string path) => row.transform.Find(path).GetComponent<TMPro.TMP_Text>().text;

        [Test]
        public void Rows_ReuseTheCachedStrings_WhenNotSearching()
        {
            LogRecorder.Receive("bought <b>3</b>", "Harvest.Shop:Buy () (at Assets/Shop.cs:12)", LogType.Log);
            Open();
            var item = LogModel.Shared.RowAt(1);
            var row = RowShowing(1);
            Assert.AreSame(item.EscapedPreview, TextOf(row, "Entry/Message"));
            Assert.AreSame(item.Meta, TextOf(row, "Entry/Meta"));
        }

        [Test]
        public void Search_HighlightsACallerOnlyMatch()
        {
            LogRecorder.Receive("bought 3", "Harvest.Shop:Buy () (at Assets/Shop.cs:12)", LogType.Log);
            Open();
            panel.Query = "Shop";
            view.Tick();
            var item = LogModel.Shared.RowAt(1);
            var row = RowShowing(1);
            Assert.AreEqual($"{item.Time} – {LogText.Highlight(item.Caller, "Shop")}", TextOf(row, "Entry/Meta"));
            StringAssert.Contains("<mark=", TextOf(row, "Entry/Meta"));
            StringAssert.DoesNotContain("<mark=", TextOf(row, "Entry/Message"));
        }

        [Test]
        public void Scrollbar_FollowsTheLogList()
        {
            Logs(2000);
            Open();
            var bar = TestPanel.ScrollOf(panel).verticalScrollbar;
            Assert.IsTrue(bar.gameObject.activeSelf);
            // 2000 log: tỉ lệ thật gần như bằng 0, tay cầm vẫn giữ chiều dài tối thiểu của prefab để còn thấy
            // và kéo được — kể cả khi ScrollRect tự ghi size (giả lập bằng cách đặt size cực nhỏ).
            Assert.Less(Viewport.rect.height / view.Content.rect.height, 0.05f);
            bar.size = 0.0001f;
            Assert.GreaterOrEqual(bar.handleRect.rect.height, 95.5f);
        }

        [Test]
        public void ResetFilters_AlsoClosesTheSearch()
        {
            Logs(3);
            Open();
            panel.Query = "zzz";
            view.Tick();
            var reset = panel.transform.Find("Window/Scroll View/Viewport/LogEmpty/Reset");
            Assert.IsTrue(reset.gameObject.activeInHierarchy);

            reset.GetComponent<Button>().onClick.Invoke();
            view.Tick();
            Assert.AreEqual(string.Empty, panel.Query);
            Assert.IsFalse(((TMPro.TMP_InputField)TestPanel.Field(panel, "searchInput")).gameObject.activeSelf);
            Assert.AreEqual(1 + 3, LogModel.Shared.RowCount);
        }

        [Test]
        public void AfterClear_TheEmptyStateSaysSo_WithoutAResetButton()
        {
            Logs(3);
            Open();
            LogModel.Shared.Clear();
            view.Tick();
            var empty = panel.transform.Find("Window/Scroll View/Viewport/LogEmpty");
            Assert.IsTrue(empty.gameObject.activeSelf);
            Assert.AreEqual("Đã xoá – chưa có log mới", empty.Find("Label").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(empty.Find("Reset").gameObject.activeSelf);
        }

        [Test]
        public void MissingFocusMarker_FallsBackToFollowingTheBottom()
        {
            LogRecorder.Start();
            Logs(100);
            var seq = LogRecorder.Mark("level.goto 5");
            LogModel.Shared.Clear();
            Logs(100);
            Open(seq);
            Assert.AreEqual(view.Content.rect.height - Viewport.rect.height, view.Content.anchoredPosition.y, 1f);
            Assert.IsTrue(LogModel.Shared.Follow);
        }

        [Test]
        public void Following_StaysAtTheBottom_AsLogsArrive()
        {
            Logs(200);
            Open();
            Logs(50);
            view.Tick();
            var max = view.Content.rect.height - Viewport.rect.height;
            Assert.AreEqual(max, view.Content.anchoredPosition.y, 1f);
            Assert.IsFalse(view.PillVisible);
        }

        [Test]
        public void ScrolledUp_ShowsTheNewLogsPill_InsteadOfJumping()
        {
            Logs(200);
            Open();
            view.Content.anchoredPosition = Vector2.zero;
            TestPanel.ScrollOf(panel).onValueChanged.Invoke(Vector2.one);
            Assert.IsFalse(LogModel.Shared.Follow);

            Logs(5);
            view.Tick();
            Assert.AreEqual(0f, view.Content.anchoredPosition.y, 0.5f);
            Assert.IsTrue(view.PillVisible);
            Assert.AreEqual(5, view.NewRows);

            panel.transform.Find("Window/LogPill").GetComponent<Button>().onClick.Invoke();
            view.Tick();
            Assert.IsFalse(view.PillVisible);
            Assert.AreEqual(view.Content.rect.height - Viewport.rect.height, view.Content.anchoredPosition.y, 1f);
        }

        [Test]
        public void FocusSeq_ScrollsTheMarkerToTheTop_AndSelectsIt()
        {
            LogRecorder.Start();
            Logs(100);
            var seq = LogRecorder.Mark("level.goto 5");
            Logs(100);
            Open(seq);
            var index = LogModel.Shared.IndexOfSeq(seq);
            Assert.AreEqual(index * LogView.ROW_HEIGHT, view.Content.anchoredPosition.y, 1f);
            Assert.AreEqual(seq, LogModel.Shared.SelectedSeq);
            Assert.IsFalse(LogModel.Shared.Follow);
        }
    }
}
