using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    public class LogDetailPageTests
    {
        private const string STACK =
            "UnityEngine.Debug:LogError (object)\n" +
            "Harvest.ItemPiece:OnPointerUp (UnityEngine.EventSystems.PointerEventData) (at Assets/ItemPiece.cs:212)\n";

        private DebugHubPanel panel;
        private LogView view;
        private string clipboard;

        [SetUp]
        public void SetUp()
        {
            clipboard = GUIUtility.systemCopyBuffer;
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
            GUIUtility.systemCopyBuffer = clipboard;
        }

        private void Open()
        {
            panel.Show(CommandsPage.Root());
            panel.Push(LogPage.Build());
            view.Tick();
        }

        /// Footer ghim đáy window, không nằm trong spawnedRows. Chưa bật thì fail ngay thay vì trả về một bar ẩn.
        private DebugHubBar Bar()
        {
            var footer = panel.transform.Find("Window/Footer");
            Assert.IsNotNull(footer, "prefab không có Window/Footer");
            Assert.IsTrue(footer.gameObject.activeSelf, "footer đang tắt");
            return footer.GetComponent<DebugHubBar>();
        }

        private LogItem Item(string message)
        {
            var model = LogModel.Shared;
            for (var i = 1; i < model.RowCount; i++)
            {
                if (model.RowAt(i).Entry.Message == message) return model.RowAt(i);
            }
            Assert.Fail($"không có log '{message}'");
            return null;
        }

        [Test]
        public void RowClick_OpensTheDetail_WithMessageFramesAndBar()
        {
            LogRecorder.Receive("boom", STACK, LogType.Error);
            Open();
            view.GetComponentsInChildren<LogRowView>(false).First(r => r.Index == 1).button.onClick.Invoke();

            Assert.AreEqual(3, panel.StackDepth);
            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Any(l => l.Contains("boom")));
            Assert.IsTrue(labels.Any(l => l.Contains("ItemPiece.OnPointerUp")));
            Assert.IsNotNull(Bar());
        }

        [Test]
        public void Next_ReplacesThePage_AndSkipsMarkers()
        {
            LogRecorder.Start();
            LogRecorder.Receive("first", null, LogType.Log);
            LogRecorder.Mark("cmd");
            LogRecorder.Receive("second", null, LogType.Log);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("first")));
            var depth = panel.StackDepth;

            Bar().transform.Find("Next").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(depth, panel.StackDepth);
            Assert.IsTrue(TestPanel.LabelsOf(panel).Any(l => l.Contains("second")));
        }

        [Test]
        public void FirstLog_HasNoPrevious()
        {
            LogRecorder.Receive("only", null, LogType.Log);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("only")));
            Assert.IsFalse(Bar().transform.Find("Previous").GetComponent<Button>().interactable);
        }

        [Test]
        public void Copy_PutsMessageAndStackOnTheClipboard()
        {
            LogRecorder.Receive("boom", STACK, LogType.Error);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("boom")));
            Bar().transform.Find("Primary").GetComponent<Button>().onClick.Invoke();
            StringAssert.Contains("boom", GUIUtility.systemCopyBuffer);
            StringAssert.Contains("ItemPiece.cs:212", GUIUtility.systemCopyBuffer);
        }

        /// Hàng lặp ≥ 100 lần: TailOf(40) cắt từ đầu nên dòng phụ phải đủ ngắn để không mất giờ đầu.
        [Test]
        public void CollapsedRepeat_KeepsTheFullTimeAndTheCount_InTheSubtitle()
        {
            for (var i = 0; i < 1234; i++) LogRecorder.Receive("spam", null, LogType.Log);
            Open();
            LogModel.Shared.Collapse = true;
            view.Tick();
            var item = Item("spam");
            Assert.AreEqual(1234, item.Repeat);

            panel.Push(LogDetailPage.For(LogModel.Shared, item));
            var subtitle = TestPanel.SubtitleOf(panel);
            StringAssert.StartsWith(item.Time, subtitle);
            StringAssert.Contains("×1.234", subtitle);
        }

        /// Footer ghim đáy, ngoài vùng cuộn; Trước/Sau không làm nó nhảy chỗ dù hai trang có độ dài stack khác nhau.
        [Test]
        public void Footer_StaysPinned_AndTheWindowKeepsItsMaxHeight_WhenNavigating()
        {
            var frames = new System.Text.StringBuilder();
            for (var i = 0; i < 40; i++)
                frames.Append($"Harvest.Gameplay.Piece{i}:Step{i} (UnityEngine.EventSystems.PointerEventData) (at Assets/Piece{i}.cs:{i + 1})\n");
            LogRecorder.Receive("long", frames.ToString(), LogType.Error);
            LogRecorder.Receive("short", null, LogType.Log);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("long")));

            var footer = (RectTransform)Bar().transform;
            var window = (RectTransform)TestPanel.Field(panel, "window");
            Assert.IsFalse(footer.IsChildOf(TestPanel.ScrollOf(panel).content), "footer nằm trong vùng cuộn");
            var anchored = footer.anchoredPosition;
            Assert.AreEqual(panel.MaxWindowHeight, window.sizeDelta.y, 0.01f);

            Bar().transform.Find("Next").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(TestPanel.LabelsOf(panel).Any(l => l.Contains("short")));
            Assert.AreEqual(anchored, ((RectTransform)Bar().transform).anchoredPosition);
            Assert.AreEqual(panel.MaxWindowHeight, window.sizeDelta.y, 0.01f);
        }

        /// Log native: dòng tiếp theo là chữ thường, hiện nguyên văn — qua bộ đọc frame C# thì `v12.3.0` thành `3.0`.
        [Test]
        public void NativeLog_ShowsItsFollowingLinesVerbatim()
        {
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Error, "AndroidRuntime", "FATAL EXCEPTION: main",
                "Initializing SDK v12.3.0\n\tat com.x.Y.z(Y.java:12)");
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("FATAL EXCEPTION: main")));

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Any(l => l.Contains("Initializing SDK v12.3.0")), string.Join(" | ", labels));
            Assert.IsTrue(labels.Any(l => l.Contains("com.x.Y.z(Y.java:12)")), string.Join(" | ", labels));
            Assert.IsFalse(labels.Any(l => l.Contains("Stack trace")));
        }

        /// Mỗi frame là một row dựng lại ở mỗi Trước/Sau: chỉ hiện MAX_FRAMES frame đầu + một dòng "… còn N".
        [Test]
        public void LongStack_ShowsTheFirstFramesAndARemainderRow_CopyKeepsEveryFrame()
        {
            var frames = new System.Text.StringBuilder();
            for (var i = 0; i < 100; i++)
                frames.Append($"Harvest.Gameplay.Piece{i}:Step{i} (UnityEngine.EventSystems.PointerEventData) (at Assets/Piece{i}.cs:{i + 1})\n");
            LogRecorder.Receive("deep", frames.ToString(), LogType.Error);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("deep")));

            var labels = TestPanel.LabelsOf(panel);
            Assert.AreEqual(LogDetailPage.MAX_FRAMES, labels.Count(l => l.Contains(".cs:")));
            Assert.IsTrue(labels.Any(l => l.Contains("… còn 40 frame")), string.Join(" | ", labels.Skip(60)));
            // Nội dung + "Stack trace" + 60 frame + dòng "…".
            Assert.LessOrEqual(labels.Count, LogDetailPage.MAX_FRAMES + 3);

            Bar().transform.Find("Primary").GetComponent<Button>().onClick.Invoke();
            StringAssert.Contains("Assets/Piece99.cs:100", GUIUtility.systemCopyBuffer);
        }

        /// Đỏ / vàng cho lỗi / cảnh báo; Log thường để màu tiêu đề mặc định, không tô mờ như trang bị tắt.
        [Test]
        public void Title_ColorsOnlyWarningsAndErrors()
        {
            LogRecorder.Receive("plain", null, LogType.Log);
            LogRecorder.Receive("careful", null, LogType.Warning);
            Open();
            Assert.AreEqual("Log", LogDetailPage.For(LogModel.Shared, Item("plain")).Title);
            StringAssert.Contains($"<color={Palette.WARN}>", LogDetailPage.For(LogModel.Shared, Item("careful")).Title);
        }

        [Test]
        public void LeavingTheDetail_HidesTheFooter_AndGivesTheScrollAreaItsHeightBack()
        {
            LogRecorder.Receive("boom", STACK, LogType.Error);
            Open();
            var scroll = (RectTransform)TestPanel.ScrollOf(panel).transform;
            var bottom = scroll.offsetMin.y;

            panel.Push(LogDetailPage.For(LogModel.Shared, Item("boom")));
            var footer = Bar().gameObject;
            Assert.Greater(scroll.offsetMin.y, bottom);

            panel.Pop();
            Assert.IsFalse(footer.activeSelf);
            Assert.AreEqual(bottom, scroll.offsetMin.y, 0.01f);
        }
    }
}
