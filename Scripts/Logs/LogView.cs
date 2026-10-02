using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Trang log trong panel (spec ① §5). Mượn ScrollRect của panel; danh sách ảo hoá: hàng cao cố định,
    /// chỉ ~15 hàng UI thật, đặt theo chỉ số. Panel thường dựng lại ~0,9 ms/hàng nên không chứa nổi vài
    /// nghìn log.
    public class LogView : MonoBehaviour
    {
        internal const float ROW_HEIGHT = 176f;
        internal const float BAR_HEIGHT = 112f;
        private const int EXTRA_ROWS = 2;

        [Serializable]
        internal struct Chip
        {
            public Button button;
            public Image background;
            public CanvasGroup group;
            public TMP_Text label;
        }

        private static readonly Color ChipOn = new(0.145f, 0.196f, 0.275f);
        private static readonly Color WarningOn = new(0.227f, 0.184f, 0.102f);
        private static readonly Color ErrorOn = new(0.227f, 0.110f, 0.133f);

        [SerializeField] private RectTransform bar;
        [SerializeField] private Chip chipLog;
        [SerializeField] private Chip chipWarning;
        [SerializeField] private Chip chipError;
        [SerializeField] private Chip chipCollapse;
        [SerializeField] private Button pill;
        [SerializeField] private TMP_Text pillLabel;
        [SerializeField] private GameObject empty;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button emptyReset;
        [SerializeField] private LogRowView rowTemplate;

        private readonly List<LogRowView> pool = new();
        private readonly int[] shownCounts = { -1, -1, -1 };
        private DebugHubPanel panel;
        private ScrollRect scrollRect;
        private RectTransform previousContent;
        private LogModel model;
        private bool wired;
        private bool pendingBottom;
        private long pendingFocus;
        private int newRows;
        /// Version lúc dựng chiều cao lần cuối. Không đọc Version đầu Tick: lọc/gộp/xoá chạy từ handler bấm,
        /// tức trước LateUpdate, nên so trong cùng một Tick là không bao giờ thấy đổi.
        private int seenVersion = -1;
        private int shownNewRows = -1;
        private int shownEmpty = -1;

        internal RectTransform Content => (RectTransform)transform;
        internal bool IsOpen => gameObject.activeSelf;
        internal bool PillVisible => pill.gameObject.activeSelf;
        internal int NewRows => newRows;

        internal int ActiveRowCount
        {
            get
            {
                var n = 0;
                foreach (var row in pool)
                {
                    if (row.gameObject.activeSelf) n++;
                }
                return n;
            }
        }

        private RectTransform Viewport => scrollRect.viewport ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        /// Panel gọi từ Build của trang log. Không cuộn ở đây: panel còn RestoreScroll sau Build, nên cuộn
        /// (bám đáy / nhảy tới vạch) để Tick ở LateUpdate làm.
        internal void Open(DebugHubPanel owner, ScrollRect scroll, LogModel logModel, long focusSeq)
        {
            panel = owner;
            scrollRect = scroll;
            model = logModel;
            Wire();
            if (!IsOpen)
            {
                previousContent = scrollRect.content;
                previousContent.gameObject.SetActive(false);
                gameObject.SetActive(true);
                bar.gameObject.SetActive(true);
                scrollRect.content = Content;
                foreach (var row in pool) row.Index = -1;
                seenVersion = shownNewRows = shownEmpty = -1;
                for (var i = 0; i < shownCounts.Length; i++) shownCounts[i] = -1;
            }
            if (focusSeq > 0)
            {
                pendingFocus = focusSeq;
                model.SelectedSeq = focusSeq;
                model.Follow = false;
            }
            else if (model.Follow) pendingBottom = true;
        }

        internal void Close()
        {
            if (!IsOpen) return;
            scrollRect.content = previousContent;
            previousContent.gameObject.SetActive(true);
            gameObject.SetActive(false);
            bar.gameObject.SetActive(false);
            pill.gameObject.SetActive(false);
            empty.SetActive(false);
        }

        /// Thanh lọc nằm ngay dưới header, ngoài vùng cuộn nên không trôi theo danh sách.
        internal void PlaceBar(float headerHeight) => bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, -headerHeight);

        private void LateUpdate() => Tick();

        internal void Tick()
        {
            if (!IsOpen || model == null) return;

            var appended = model.Pull();
            var changed = appended > 0 || model.Version != seenVersion;
            seenVersion = model.Version;
            // Cao đúng số hàng trước khi cuộn: ScrollTo kẹp theo chiều cao content.
            var resized = changed || pendingFocus > 0 || pendingBottom;
            if (resized)
            {
                Content.sizeDelta = new Vector2(Content.sizeDelta.x, model.RowCount * ROW_HEIGHT);
                // Lọc làm danh sách ngắn lại thì vị trí cũ có thể nằm quá đáy: kẹp lại, không thì trang trống.
                // Chỉ khi quá đáy — ScrollTo dừng quán tính, gọi mỗi log mới là cắt ngang cú vuốt.
                var max = Mathf.Max(0f, Content.rect.height - Viewport.rect.height);
                if (Content.anchoredPosition.y > max) ScrollTo(max);
            }

            if (pendingFocus > 0)
            {
                var index = model.IndexOfSeq(pendingFocus);
                // Vạch đã bị Xoá hoặc bị bỏ vì cũ: không nhảy được thì về đáy, đừng đứng ở đầu với Follow tắt.
                if (index > 0) ScrollTo(index * ROW_HEIGHT);
                else ScrollToBottom();
                pendingFocus = 0;
                pendingBottom = false;
                newRows = 0;
            }
            else if (pendingBottom || (changed && model.Follow))
            {
                ScrollToBottom();
                pendingBottom = false;
            }
            else if (appended > 0) newRows += appended;
            if (model.Follow) newRows = 0;
            // Sau khi cuộn xong để thanh cuộn nhận cả cỡ lẫn vị trí mới.
            if (resized) panel.SyncScrollbar();

            model.SeenErrors = LogRecorder.ErrorCount;
            BindRows();
            UpdateChrome();
        }

        /// Gắn ở đây chứ không ở Awake: EditMode test instantiate prefab không chắc gọi Awake.
        private void Wire()
        {
            if (wired) return;
            wired = true;
            chipLog.button.onClick.AddListener(() => model.Toggle(LogGroup.Log));
            chipWarning.button.onClick.AddListener(() => model.Toggle(LogGroup.Warning));
            chipError.button.onClick.AddListener(() => model.Toggle(LogGroup.Error));
            chipCollapse.button.onClick.AddListener(() => model.Collapse = !model.Collapse);
            pill.onClick.AddListener(() =>
            {
                model.Follow = true;
                pendingBottom = true;
            });
            emptyReset.onClick.AddListener(() =>
            {
                model.ResetFilters();
                // Từ khoá nằm ở ô tìm của header: không đóng nó thì ô vẫn ghi chữ cũ mà danh sách đã bỏ lọc.
                panel.CloseSearch();
            });
            scrollRect.onValueChanged.AddListener(_ => OnScrolled());
        }

        private void OnScrolled()
        {
            if (!IsOpen || model == null) return;
            var max = Mathf.Max(0f, Content.rect.height - Viewport.rect.height);
            model.Follow = Content.anchoredPosition.y >= max - ROW_HEIGHT * 0.5f;
        }

        private void ScrollTo(float y)
        {
            scrollRect.StopMovement();
            var max = Mathf.Max(0f, Content.rect.height - Viewport.rect.height);
            Content.anchoredPosition = new Vector2(Content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
        }

        private void ScrollToBottom()
        {
            ScrollTo(float.MaxValue);
            model.Follow = true;
        }

        private void BindRows()
        {
            var needed = Mathf.CeilToInt(Viewport.rect.height / ROW_HEIGHT) + EXTRA_ROWS;
            while (pool.Count < needed) pool.Add(CreateRow());

            var count = pool.Count;
            var first = Mathf.Max(0, Mathf.FloorToInt(Content.anchoredPosition.y / ROW_HEIGHT));
            for (var k = 0; k < count; k++)
            {
                // Hàng chỉ số i luôn ở ô i % count: cuộn một hàng chỉ phải gán lại một ô.
                var index = first + ((k - first) % count + count) % count;
                var row = pool[k];
                if (index >= model.RowCount)
                {
                    if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                    row.Index = -1;
                    continue;
                }
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);

                var item = model.RowAt(index);
                var isSelected = item != null && item.Entry.Seq == model.SelectedSeq;
                if (row.Index == index && row.Version == model.Version && row.Selected == isSelected) continue;
                row.Index = index;
                row.Version = model.Version;
                row.Selected = isSelected;
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0f, -index * ROW_HEIGHT);

                if (item == null) row.BindMarker(LogText.Escape(model.NoteText()), false, false);
                else if (item.IsMarker) row.BindMarker(item.MarkerLine, true, isSelected);
                else row.BindEntry(item, model.Query, isSelected, model.Collapse);
            }
        }

        private LogRowView CreateRow()
        {
            var row = Instantiate(rowTemplate, Content);
            row.gameObject.SetActive(false);
            row.button.onClick.AddListener(() => OnRowClicked(row));
            return row;
        }

        private void OnRowClicked(LogRowView row)
        {
            if (row.Index == 0)
            {
                if (model.Cleared) model.Unclear();
                return;
            }
            var item = model.RowAt(row.Index);
            if (item == null || item.IsMarker) return;
            panel.Push(LogDetailPage.For(model, item));
        }

        /// Chỉ gán chữ khi con số đổi: trang đang mở mà không có log mới thì không cấp phát gì mỗi frame.
        private void UpdateChrome()
        {
            SetChip(chipLog, 0, model.IsShown(LogGroup.Log), ChipOn, model.Count(LogGroup.Log));
            SetChip(chipWarning, 1, model.IsShown(LogGroup.Warning), WarningOn, model.Count(LogGroup.Warning));
            SetChip(chipError, 2, model.IsShown(LogGroup.Error), ErrorOn, model.Count(LogGroup.Error));
            SetLook(chipCollapse, model.Collapse, ChipOn);

            var showPill = newRows > 0 && !model.Follow;
            if (pill.gameObject.activeSelf != showPill) pill.gameObject.SetActive(showPill);
            if (showPill && shownNewRows != newRows)
            {
                shownNewRows = newRows;
                pillLabel.text = $"{LogText.Count(newRows)} log mới";
            }

            // 0 = có hàng, 1 = chưa có log nào, 2 = có log nhưng bộ lọc ẩn hết, 3 = đã Xoá, chưa có log mới
            // (Total không đếm log đã ẩn). Ca 3 không có nút Bỏ lọc: dòng ghi chú đầu danh sách đã có "hiện lại".
            var state = model.RowCount > 1 ? 0 : model.Total > 0 ? 2 : model.Cleared ? 3 : 1;
            if (state == shownEmpty) return;
            shownEmpty = state;
            empty.SetActive(state != 0);
            emptyLabel.text = state == 1 ? "Chưa có log" : state == 3 ? "Đã xoá – chưa có log mới" : "Không có log khớp bộ lọc";
            emptyReset.gameObject.SetActive(state == 2);
        }

        private void SetChip(Chip chip, int slot, bool on, Color onColor, int count)
        {
            SetLook(chip, on, onColor);
            if (shownCounts[slot] == count) return;
            shownCounts[slot] = count;
            chip.label.text = LogText.Count(count);
        }

        /// So trước khi gán: gán alpha của CanvasGroup mỗi frame có thể làm canvas dựng lại dù không đổi gì.
        private static void SetLook(Chip chip, bool on, Color onColor)
        {
            var color = on ? onColor : Color.clear;
            if (chip.background.color != color) chip.background.color = color;
            var alpha = on ? 1f : 0.45f;
            if (!Mathf.Approximately(chip.group.alpha, alpha)) chip.group.alpha = alpha;
        }
    }
}
