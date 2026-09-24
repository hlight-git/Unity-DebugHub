using System;
using System.Collections.Generic;
using IngameDebugConsole;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Panel duy nhất của debug hub. Nội dung do page dựng ra qua các hàm Add*; điều hướng bằng stack.
    /// Back (nút ‹ ở header) lùi từng bước; bấm ra vùng tối bên ngoài đóng panel (stack giữ nguyên).
    ///
    /// Ngôn ngữ hình ảnh: row tối + chevron = đi sang page khác, row tối + tên accent = chạy ngay,
    /// row nền accent = hành động chính của page (một cái mỗi page), row tối + switch = bật/tắt.
    public class DebugHubPanel : MonoBehaviour
    {
        private static readonly Color InvalidColor = new Color(1f, 0.42f, 0.42f);

        /// Tên của command chạy ngay. Đủ để đọc ra "bấm là chạy" mà không phải tô nền cả row.
        private static readonly Color AccentColor = new Color(0.60f, 0.87f, 0.83f);

        private const string DescriptionColor = Palette.DIM;

        /// Chiều cao row một dòng / row có giá trị bên dưới, trên canvas tham chiếu 1080 — khớp prefab.
        private const float ROW_HEIGHT = 132f;
        private const float VALUE_HEIGHT = 172f;
        private const float MIN_TOUCH_PIXELS = 44f;
        /// TMP nhận size theo phần trăm, không như legacy Text — nên description tự co theo cỡ chữ
        /// của từng template thay vì cứng 38.
        private const string DescriptionSize = "90%";

        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [Tooltip("Dòng address dưới tiêu đề; bấm là copy.")]
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform window;
        [Tooltip("Phần chiều cao màn hình tối đa mà panel được chiếm; quá thì scroll.")]
        [SerializeField, Range(0.5f, 1f)] private float maxScreenHeight = 0.85f;
        [SerializeField, Min(320f)] private float maxWindowWidth = 1080f;
        [Tooltip("Dòng kết quả nổi ở đáy màn hình. Nằm ngoài panel nên vẫn thấy sau khi panel đóng.")]
        [SerializeField] private DebugHubToast toast;

        [Header("Header")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button advancedButton;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button closeButton;

        private string query = string.Empty;

        [Header("Row templates (inactive children of content)")]
        [SerializeField] private DebugHubRow buttonTemplate;
        [SerializeField] private DebugHubRow navTemplate;
        [SerializeField] private DebugHubRow actionTemplate;
        [SerializeField] private DebugHubRow toggleTemplate;
        [SerializeField] private DebugHubRow inputTemplate;
        [SerializeField] private DebugHubRow textTemplate;

        private sealed class PageState
        {
            public DebugPage Page;
            public string Query = string.Empty;
            public bool SearchOpen;
            public float Scroll = 1f;
            public int Offset;
        }

        private readonly Stack<PageState> stack = new();
        private readonly List<DebugHubRow> spawnedRows = new();
        private readonly Dictionary<DebugHubRow, Stack<DebugHubRow>> pool = new();
        private float nextRefresh;
        private bool refreshLater;
        private string pendingQuery;
        private float searchDue;
        private Vector2 fittedCanvasSize;
        private string lastBuildError;

        public bool IsOpen => gameObject.activeSelf;

        /// Bấm vào dòng kết quả. DebugHub nối vào đây để mở console log window (toàn văn ở đó).
        public event Action ResultClicked;

        /// Nội dung dòng kết quả lần cuối. Test đọc cái này thay vì bới vào toast.
        public string LastResult { get; private set; }

        /// Số tầng đang mở. Test dùng để biết một cú bấm đã pop hay chưa thay vì giả định
        /// Awake có chạy hay không.
        internal int StackDepth => stack.Count;

        /// Từ khoá của trang hiện tại. Ô nhập ở Header để giữ focus khi content được dựng lại.
        /// Gán trực tiếp áp ngay; thao tác gõ qua UI được gom trong 120 ms để tránh dựng lại mỗi ký tự.
        internal string Query
        {
            get => query;
            set
            {
                value ??= string.Empty;
                pendingQuery = null;
                if (query == value) return;
                query = value;
                searchInput.SetTextWithoutNotify(query);
                if (stack.Count == 0) return;
                stack.Peek().Offset = 0;
                if (query.Length > 0) stack.Peek().SearchOpen = true;
                Rebuild();
                RestoreScroll(1f);
            }
        }

        internal void OpenSearch()
        {
            if (stack.Count == 0 || !stack.Peek().Page.Searchable) return;
            stack.Peek().SearchOpen = true;
            searchInput.gameObject.SetActive(true);
            LayoutHeader(stack.Peek().Page);
            FitWindowToContent();
            searchInput.SetTextWithoutNotify(query);
            searchInput.Select();
            searchInput.ActivateInputField();
        }

        internal void CloseSearch()
        {
            if (stack.Count > 0) stack.Peek().SearchOpen = false;
            searchInput.gameObject.SetActive(false);
            title.gameObject.SetActive(true);
            Query = string.Empty;
            if (stack.Count > 0)
            {
                LayoutHeader(stack.Peek().Page);
                FitWindowToContent();
            }
        }

        private void Awake()
        {
            backgroundButton.onClick.AddListener(Close);
            backButton.onClick.AddListener(Pop);
            closeButton.onClick.AddListener(Close);
            toast.Clicked += () => ResultClicked?.Invoke();

            searchButton.onClick.AddListener(() =>
            {
                if (searchInput.gameObject.activeSelf) CloseSearch();
                else OpenSearch();
            });
            searchInput.onValueChanged.AddListener(value =>
            {
                pendingQuery = value;
                searchDue = Time.unscaledTime + 0.12f;
            });
            helpButton.onClick.AddListener(() => Push(HelpPage.Build()));
            if (subtitle.TryGetComponent<Button>(out var copy))
            {
                copy.onClick.AddListener(() =>
                {
                    if (stack.Count == 0 || string.IsNullOrEmpty(stack.Peek().Page.Subtitle)) return;
                    GUIUtility.systemCopyBuffer = stack.Peek().Page.Subtitle;
                    ShowResult("đã copy address", false);
                });
            }
            advancedButton.onClick.AddListener(() => Push(AdvancedPage.Root()));
        }

        private void Update()
        {
            if (!IsOpen || stack.Count == 0) return;
            if (((RectTransform)transform).rect.size != fittedCanvasSize)
            {
                var position = scrollRect.verticalNormalizedPosition;
                FitWindowToContent();
                RestoreScroll(position);
            }
            if (pendingQuery != null)
            {
                if (Time.unscaledTime >= searchDue) Query = pendingQuery;
                return;
            }
            if (!stack.Peek().Page.Live && !refreshLater) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            // Chỉ bỏ nhịp khi người dùng đang **gõ**. currentSelectedGameObject != null là sai:
            // Button/Toggle cũng giữ selection sau khi bấm nên panel sẽ đứng hình vĩnh viễn.
            foreach (var row in spawnedRows)
            {
                if (row && row.input && row.input.isFocused) return;
            }
            // KHÔNG chặn theo searchInput: nó nằm trong Header, **ngoài** `content`, nên Rebuild() không
            // đụng tới nó và focus không mất. Chặn ở đây là trang bộ chọn không bao giờ hiện được kết quả
            // gợi ý (kết quả về từ thread nền đúng lúc người dùng đang gõ).

            refreshLater = false;
            Refresh();
        }

        /// Xin **một** nhịp dựng lại ở lượt refresh kế tiếp, cho trang không Live. Trang bộ chọn gọi cái
        /// này khi còn chờ gợi ý từ thread nền, và thôi gọi khi kết quả đã về — Live 4 lần/giây trên một
        /// danh sách 170 assembly là ~0,35 s mỗi nhịp, trong khi chỉ lúc chờ mới cần dựng lại.
        internal void RefreshLater() => refreshLater = true;

        /// Mở panel. Đóng panel không xoá stack nên lần mở sau về đúng page đang xem lúc đóng;
        /// <paramref name="root"/> chỉ dùng khi chưa có gì trong stack.
        internal void Show(DebugPage root)
        {
            if (stack.Count > 0 && IsOpen) SavePageState();
            if (stack.Count == 0) stack.Push(new PageState { Page = root });
            gameObject.SetActive(true);
            query = stack.Peek().Query;
            Rebuild();
            RestoreScroll(stack.Peek().Scroll);
        }

        /// Về page gốc, bỏ đường đã đi. Không đặt tên Reset: MonoBehaviour.Reset là magic method của
        /// Unity (menu Reset trong inspector), để trùng tên chỉ gây nhầm.
        internal void ShowFromRoot(DebugPage root)
        {
            stack.Clear();
            query = string.Empty;
            pendingQuery = null;
            Show(root);
        }

        /// Hiện kết quả (hoặc lỗi) của command vừa chạy.
        public void ShowResult(string text, bool error)
        {
            LastResult = text;
            toast.Show(text, error);
        }

        public void HideResult()
        {
            LastResult = string.Empty;
            toast.Hide();
        }

        internal void Push(DebugPage page)
        {
            SavePageState();
            stack.Push(new PageState { Page = page });
            query = string.Empty;
            Rebuild();
            RestoreScroll(1f);
        }

        internal void Pop()
        {
            pendingQuery = null;
            if (stack.Count > 0) stack.Pop();
            if (stack.Count == 0)
            {
                Clear();
                gameObject.SetActive(false);
                return;
            }
            query = stack.Peek().Query;
            Rebuild();
            RestoreScroll(stack.Peek().Scroll);
        }

        /// Bấm ra ngoài panel: đóng hẳn bất kể đang ở page nào, khác với Pop() (lùi từng bước qua nút back).
        ///
        /// Giữ nguyên stack: mở lại là về đúng page đang xem, khỏi bò lại từ root mỗi lần — nhất là với
        /// DismissMode.HideHub (ẩn hub để xem game rồi gọi lại tiếp tục làm việc đang làm).
        public void Close()
        {
            if (IsOpen) SavePageState();
            Clear();
            gameObject.SetActive(false);
        }

        /// Dựng lại trang hiện tại mà giữ vị trí scroll. Trang Live gọi cái này 4 lần/giây.
        internal void Refresh()
        {
            if (stack.Count == 0 || !IsOpen) return;
            var scroll = scrollRect.verticalNormalizedPosition;
            Rebuild();
            scrollRect.verticalNormalizedPosition = scroll;
            SyncScrollbar();
        }

        private void SavePageState()
        {
            if (stack.Count == 0) return;
            var state = stack.Peek();
            state.Query = pendingQuery ?? query;
            state.Scroll = scrollRect.verticalNormalizedPosition;
            pendingQuery = null;
        }

        private void RestoreScroll(float position)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = position;
            SyncScrollbar();
        }

        private void Rebuild()
        {
            if (stack.Count == 0 || !IsOpen) return;
            refreshLater = false;
            Clear();
            var page = stack.Peek().Page;
            var searching = page.Searchable && stack.Peek().SearchOpen;
            searchInput.gameObject.SetActive(searching);
            searchInput.SetTextWithoutNotify(query);
            title.gameObject.SetActive(true);
            title.text = page.Title;
            backButton.gameObject.SetActive(stack.Count > 1);
            searchButton.gameObject.SetActive(page.Searchable);
            advancedButton.gameObject.SetActive(page.ShowTools);
            helpButton.gameObject.SetActive(page.ShowTools);
            ShowSubtitle(page.Subtitle);
            LayoutHeader(page);

            // Chỗ duy nhất mọi trang đi qua, nên cũng là chỗ duy nhất bắt lỗi: folder của game ném (chưa load
            // level, service chưa có) thì trang hiện một dòng lỗi, không dừng giữa chừng với window sai cỡ.
            try
            {
                // Trang nhập liệu/xác nhận không bị thay bằng kết quả command search.
                if (string.IsNullOrEmpty(query) || !page.Searchable) page.Build?.Invoke(this);
                else if (page.Search != null) page.Search(this, query);
                lastBuildError = null;
            }
            catch (Exception exception)
            {
                var message = exception.Unwrap().Message;
                // Trang Live dựng lại 4 lần/giây: cùng một lỗi chỉ log một lần.
                if (message != lastBuildError) UnityEngine.Debug.LogException(exception);
                lastBuildError = message;
                AddError("Trang này bị lỗi", message);
            }

            FitWindowToContent();
        }

        /// Trần theo **tỉ lệ màn hình**, không phải một con số cố định: 1500 px cứng làm panel chỉ dùng
        /// 62% chiều cao trên máy 1080×2400 và chừa 900 px đen, trong khi danh sách member phải cuộn 5 lần.
        internal float MaxWindowHeight
        {
            get
            {
                var available = ((RectTransform)transform).rect.height;
                if (available > 0f) return available * maxScreenHeight;
                var canvas = window.GetComponentInParent<Canvas>();
                var scale = canvas ? canvas.scaleFactor : 1f;
                return Screen.height / Mathf.Max(scale, 0.0001f) * maxScreenHeight;
            }
        }

        private void LayoutHeader(DebugPage page)
        {
            var header = (RectTransform)title.transform.parent;
            var hasSubtitle = !string.IsNullOrEmpty(page.Subtitle);
            var searching = page.Searchable && stack.Peek().SearchOpen;
            var height = 148f + (hasSubtitle ? 72f : 0f) + (searching ? 144f : 0f);
            header.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            header.anchoredPosition = new Vector2(0f, -height * 0.5f);

            var toolsWidth = 0f;
            var buttons = (RectTransform)searchButton.transform.parent;
            foreach (Transform child in buttons)
                if (child.gameObject.activeSelf) toolsWidth += LayoutUtility.GetPreferredWidth((RectTransform)child);
            buttons.anchorMin = buttons.anchorMax = Vector2.one;
            buttons.pivot = Vector2.one;
            buttons.anchoredPosition = new Vector2(-160f, -8f);
            buttons.sizeDelta = new Vector2(toolsWidth, 132f);

            var left = stack.Count > 1 ? 180f : 36f;
            var rect = title.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, -140f);
            rect.offsetMax = new Vector2(-172f - toolsWidth, -8f);

            var sub = subtitle.rectTransform;
            sub.anchorMin = new Vector2(0f, 1f);
            sub.anchorMax = Vector2.one;
            sub.offsetMin = new Vector2(28f, -216f);
            sub.offsetMax = new Vector2(-28f, -144f);

            var field = (RectTransform)searchInput.transform;
            field.anchorMin = Vector2.zero;
            field.anchorMax = new Vector2(1f, 0f);
            field.offsetMin = new Vector2(28f, 8f);
            field.offsetMax = new Vector2(-28f, 140f);
            if (searchInput.placeholder is TMP_Text hint)
                hint.text = page.ShowTools ? "Tìm tất cả lệnh…" : $"Tìm trong {page.Title}…";
            searchButton.GetComponentInChildren<DebugHubIcon>(true).color =
                searching ? AccentColor : Palette.ToColor("#D9E4F1");
            var viewport = (RectTransform)scrollRect.transform;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -height - 12f);
        }

        private void ShowSubtitle(string address)
        {
            var has = !string.IsNullOrEmpty(address);
            subtitle.gameObject.SetActive(has);
            subtitle.text = has ? TailOf(address, 40) : string.Empty;

        }

        /// Cắt từ đầu: `…RootScope[0].PlayerSave.Profile` nói được mình đang ở đâu, còn
        /// `#Harvest.RootScope[0].Player…` thì không.
        internal static string TailOf(string address, int max)
        {
            return address.Length <= max ? address : "…" + address.Substring(address.Length - max + 1);
        }

        /// Window cao đúng bằng nội dung, chặn trên bởi MaxWindowHeight (quá thì scroll).
        private void FitWindowToContent()
        {
            fittedCanvasSize = ((RectTransform)transform).rect.size;
            window.anchorMin = new Vector2(0.5f, window.anchorMin.y);
            window.anchorMax = new Vector2(0.5f, window.anchorMax.y);
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(1f, Mathf.Min(maxWindowWidth, fittedCanvasSize.x - 64f)));
            window.anchoredPosition = new Vector2(0f, window.anchoredPosition.y);

            // Hai lần: pass đầu áp chiều rộng cho row, pass sau mới đo được chiều cao của row mà
            // chiều cao phụ thuộc chiều rộng (text wrap). Đo một lần thì page Help co lại còn vài dòng
            // vì text được đo ở width của template. Row cao theo label cũng phải nằm giữa hai pass đó.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            GrowRowsToLabel();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            var viewport = (RectTransform)scrollRect.transform;
            // Scroll view neo stretch trong window nên khoảng chừa cho header + padding = offset trên/dưới.
            var chrome = viewport.offsetMin.y - viewport.offsetMax.y;
            var desired = LayoutUtility.GetPreferredHeight(content) + chrome;

            // Ép anchor dọc về giữa: nếu window còn neo stretch (prefab/scene cũ) thì sizeDelta.y
            // chỉ là phần cộng thêm vào khoảng anchor, panel sẽ cao hơn max mà không hiểu vì sao.
            window.anchorMin = new Vector2(window.anchorMin.x, 0.5f);
            window.anchorMax = new Vector2(window.anchorMax.x, 0.5f);
            window.pivot = new Vector2(window.pivot.x, 0.5f);
            window.sizeDelta = new Vector2(window.sizeDelta.x, Mathf.Min(desired, MaxWindowHeight));
            window.anchoredPosition = new Vector2(window.anchoredPosition.x, 0f);
            SyncScrollbar();
        }

        private void SyncScrollbar()
        {
            if (!scrollRect.verticalScrollbar) return;
            var viewport = scrollRect.viewport ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            var bar = scrollRect.verticalScrollbar;
            // Giữ track nhìn mảnh nhưng vùng bắt ngón tay đạt tối thiểu 44 px thật. Chỉ tăng RectTransform
            // sẽ biến scrollbar thành một cột màu dày; raycastPadding cho cùng khả năng kéo mà không đổi hình.
            if (bar.TryGetComponent<Graphic>(out var hitGraphic))
            {
                var canvas = bar.GetComponentInParent<Canvas>();
                var scale = canvas ? canvas.scaleFactor : 1f;
                var visualWidth = ((RectTransform)bar.transform).rect.width;
                var requiredWidth = MIN_TOUCH_PIXELS / Mathf.Max(0.01f, scale);
                var padding = Mathf.Max(0f, (requiredWidth - visualWidth) * 0.5f);
                hitGraphic.raycastPadding = new Vector4(padding, 0f, padding, 0f);
            }
            var visible = content.rect.height > viewport.rect.height + 1f;
            bar.gameObject.SetActive(visible);
            bar.size = Mathf.Clamp01(viewport.rect.height / Mathf.Max(1f, content.rect.height));
            bar.SetValueWithoutNotify(scrollRect.verticalNormalizedPosition);
        }

        /// Row nào có label wrap quá chỗ dành cho nó (tên + description hai dòng) thì cao thêm đúng
        /// phần thiếu. Template giữ nguyên chiều cao một dòng nên row không có description không đổi gì.
        private void GrowRowsToLabel()
        {
            foreach (var row in spawnedRows)
            {
                if (!row || !row.label) continue;

                var label = row.label;
                var extra = label.preferredHeight - label.rectTransform.rect.height;
                if (extra <= 1f) continue;

                // Label bị một layout group trong row điều khiển (row input) thì phải nới cả nó,
                // không thì row cao ra nhưng chữ vẫn bị cắt.
                if (label.TryGetComponent<LayoutElement>(out var labelElement))
                    labelElement.preferredHeight = label.preferredHeight;

                // Sửa thẳng sizeDelta chứ không phải LayoutElement.preferredHeight: layout group của
                // content để ChildControlHeight = 0 nên nó lấy sizeDelta của row, không nhìn LayoutElement.
                var rect = (RectTransform)row.transform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + extra);
            }
        }

        /// Trả row về pool thay vì Destroy. Bắt buộc vì trang Live dựng lại 4 lần/giây (~80 row/giây),
        /// và nó xoá luôn cái vá Destroy-deferred-vs-DestroyImmediate.
        private void Clear()
        {
            foreach (var row in spawnedRows)
            {
                if (!row) continue;
                // Release captured objects immediately, even if this template is never reused.
                // Disabling a focused TMP field fires EndEdit; navigation must not write a value.
                ReleaseListeners(row);
                row.gameObject.SetActive(false);
                if (!pool.TryGetValue(row.Template, out var stack)) pool[row.Template] = stack = new Stack<DebugHubRow>();
                stack.Push(row);
            }
            spawnedRows.Clear();
        }

        #region Elements

        /// Bound the number of expensive UI rows while keeping every result reachable.
        internal void AddPaged<T>(IReadOnlyList<T> items, Action<T> render, int pageSize = 40)
        {
            var state = stack.Peek();
            state.Offset = items.Count == 0 ? 0 : Mathf.Min(state.Offset, (items.Count - 1) / pageSize * pageSize);
            var end = Mathf.Min(state.Offset + pageSize, items.Count);
            if (items.Count > pageSize) AddText($"{state.Offset + 1}–{end} / {items.Count}");
            for (var i = state.Offset; i < end; i++) render(items[i]);
            if (state.Offset > 0) AddButton("‹ Trang trước", () => ChangePage(-pageSize));
            if (end < items.Count) AddButton("Trang tiếp ›", () => ChangePage(pageSize));
        }

        private void ChangePage(int delta)
        {
            stack.Peek().Offset = Mathf.Max(0, stack.Peek().Offset + delta);
            Rebuild();
            RestoreScroll(1f);
        }

        /// Row trung tính, không ngụ ý sẽ đi đâu hay chạy gì.
        internal void AddButton(string label, Action onClick, string description = null)
        {
            Spawn(buttonTemplate, label, description).button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// Row có chevron: bấm là mở page khác. <paramref name="detail"/> nằm dưới nhãn
        /// để tên và giá trị không tranh chiều rộng trên màn hình hẹp.
        internal void AddNavigation(string label, DebugPage page, string description = null, string detail = null)
        {
            var row = Spawn(navTemplate, label, description);

            // Không có mô tả thì nhãn vốn là một dòng: để nó wrap là tên dài vỡ giữa từ
            // (`AllIn1SpriteShaderAss` / `embly`) và row cao gấp ba. Cắt bằng `…` đọc được hơn hẳn.
            // Row có mô tả vẫn wrap như cũ, và Reset() trả cờ này về template khi tái dùng row.
            if (string.IsNullOrEmpty(description) && row.label)
            {
                row.label.textWrappingMode = TextWrappingModes.NoWrap;
                row.label.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (row.detail) row.detail.text = detail;
            // Empty detail/more columns used to truncate assembly names halfway across the row.
            FitRowColumns(row, !string.IsNullOrEmpty(detail), !string.IsNullOrEmpty(detail) && detail.Length <= 4);
            row.button.onClick.AddListener(() => Push(page));
        }

        /// Nút icon ở mép phải của row vừa dựng (sao yêu thích). Là một vùng bấm riêng nên không vô tình
        /// mở/chạy row bên dưới.
        internal void AttachTrailingAction(DebugHubIcon.Symbol symbol, Color color, Action action)
        {
            if (spawnedRows.Count == 0) return;
            var row = spawnedRows[spawnedRows.Count - 1];
            if (!row.more) return;
            row.more.gameObject.SetActive(true);
            var text = row.more.GetComponent<TMP_Text>();
            if (text)
            {
                // Button.targetGraphic points at this TMP component. Disabling it made the star
                // visible through the vector child but removed the raycast target, so taps did nothing.
                text.text = string.Empty;
                text.color = Color.clear;
                text.enabled = true;
            }
            var icon = row.more.GetComponentInChildren<DebugHubIcon>(true);
            icon.gameObject.SetActive(true);
            icon.symbol = symbol;
            icon.color = color;
            var hasDetail = row.detail && !string.IsNullOrEmpty(row.detail.text);
            FitRowColumns(row, hasDetail, hasDetail && row.detail.text.Length <= 4);
            row.more.onClick.RemoveAllListeners();
            row.more.onClick.AddListener(() => action?.Invoke());
        }

        /// Row tối, tên màu accent: bấm là chạy ngay. Không tô nền accent — một page toàn row nền
        /// accent thì thành tường màu, và description trên nền đó đọc không nổi.
        internal void AddAction(string label, Action onClick, string description = null)
        {
            var row = Spawn(buttonTemplate, label, description);
            row.label.color = AccentColor;
            row.button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// Row nền accent: hành động chính của page (nút Run). Mỗi page chỉ nên có một cái.
        internal void AddPrimary(string label, Action onClick)
        {
            Spawn(actionTemplate, label).button.onClick.AddListener(() => onClick?.Invoke());
        }

        internal void AddToggle(string label, bool value, Action<bool> onChanged, string description = null)
        {
            var row = Spawn(toggleTemplate, label, description);
            row.toggle.SetIsOnWithoutNotify(value);
            MoveKnob(row, value);
            row.toggle.onValueChanged.AddListener(v =>
            {
                MoveKnob(row, v);
                onChanged?.Invoke(v);
            });
        }

        /// Núm switch chỉ có hai vị trí, không animate: page dựng lại liên tục nên tween chỉ tốn
        /// coroutine mà không ai kịp thấy.
        private static void MoveKnob(DebugHubRow row, bool on)
        {
            if (!row.knob) return;

            var track = (RectTransform)row.knob.parent;
            var inset = (track.rect.height - row.knob.rect.height) * 0.5f;
            var offset = inset + row.knob.rect.width * 0.5f;
            row.knob.anchoredPosition = new Vector2(on ? track.rect.width - offset : offset, 0f);
        }

        /// Dòng lỗi của **một mục**, không phải của cả trang: có nền row như mọi dòng khác, chỉ khác màu
        /// chữ. AddText không có nền nên lỗi trôi sát mép, nhìn như lỗi của cả panel.
        internal void AddError(string label, string message)
        {
            Spawn(buttonTemplate, $"<color={Palette.BAD}>{label}</color>", message);
        }

        internal TMP_Text AddText(string content)
        {
            var row = Spawn(textTemplate, content);
            row.gameObject.SetActive(!string.IsNullOrEmpty(content));
            return row.label;
        }

        /// Row chỉ đọc: nhãn trên, giá trị dưới, bấm là copy. Copy thành hành vi mặc định của
        /// mọi dòng giá trị chứ không phải thứ mỗi trang info tự viết lại.
        internal void AddCopyRow(string label, string value, string description = null)
        {
            // buttonTemplate chứ không phải navTemplate: row này **không** đi đâu cả, mà navTemplate có
            // chevron nên nhìn như mở được. Nó cũng là template mang nút ….
            var row = Spawn(buttonTemplate, label, description);
            // Giá trị là dữ liệu của game (chuỗi, tên object): `<noparse>` để TMP không đọc `<b>`, `<s>`… trong đó
            // thành định dạng.
            if (row.detail) row.detail.text = $"<noparse>{value}</noparse>";
            FitRowColumns(row, true);
            row.button.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = value;
                ShowResult($"đã copy {label}", false);
            });
        }

        /// Gắn nút `…` (page Thao tác) cho row vừa dựng. `run` đi kèm vì page Thao tác có thao tác
        /// `Gán` — nó phải đi qua đúng luồng chạy của node (Confirm, Dismiss, dòng kết quả), không
        /// được gọi thẳng node.Set.
        internal void AttachActions(ValueNode node, object current, Action<DebugNode, string[]> run)
        {
            var row = spawnedRows[spawnedRows.Count - 1];
            if (!row.more) return;
            row.more.gameObject.SetActive(true);
            if (row.input)
            {
                var field = (RectTransform)row.input.transform;
                field.offsetMax = new Vector2(-164f, field.offsetMax.y);
            }
            if (!row.input && !row.toggle) FitRowColumns(row, row.detail && !string.IsNullOrEmpty(row.detail.text));

            // Row input xếp dọc (tên → ô nhập → …): `…` chiếm một hàng riêng mà row vẫn cao bằng
            // template, nên layout group ép label xuống dưới một dòng và TMP (Truncate) giấu mất tên
            // field. Nới row đúng bằng phần `…` thêm vào; Reset() trả sizeDelta về template khi tái dùng.
            if (row.TryGetComponent<VerticalLayoutGroup>(out var column))
            {
                var rect = (RectTransform)row.transform;
                var extra = LayoutUtility.GetPreferredHeight((RectTransform)row.more.transform) + column.spacing;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y + extra);
            }

            row.more.onClick.RemoveAllListeners();
            row.more.onClick.AddListener(() => Push(ActionsPage.For(node, current, run)));
        }

        /// Chọn một giá trị trong danh sách: row hiện giá trị hiện tại, bấm vào thì mở page liệt kê
        /// lựa chọn, chọn xong tự back. Dùng page thay cho UI.Dropdown vì dropdown sinh canvas lồng
        /// bên trong Mask của scroll view nên list bị mờ và không bấm được.
        internal void AddChoice(string label, IReadOnlyList<string> options, string current, Action<string> onChanged,
            string description = null)
        {
            AddNavigation($"{label}:  <b>{current}</b>", new DebugPage(label, page =>
            {
                foreach (var option in options)
                {
                    var picked = option;
                    if (picked == current)
                    {
                        page.AddAction(picked, page.Pop);
                        continue;
                    }
                    page.AddButton(picked, () =>
                    {
                        // Pop sau onChanged (ParamsPage phải StoreArgs trước khi dựng lại), nhưng chỉ khi
                        // onChanged không mở trang mới: node có Confirms() push trang xác nhận, Pop mù là
                        // gỡ luôn trang đó.
                        var depth = stack.Count;
                        onChanged?.Invoke(picked);
                        if (stack.Count == depth) page.Pop();
                    });
                }
            }), description);
        }

        /// Field cho một giá trị kiểu <paramref name="type"/>: enum ra page chọn, bool ra switch,
        /// số ra input chỉ nhận số, còn lại là input text. Mọi giá trị được validate bằng
        /// DebugLogConsole.ParseArgument nên không có kiểu nào lọt qua mà không kiểm.
        ///
        /// <paramref name="onChanged"/> bắn theo từng lần sửa (page nhập liệu dùng để giữ giá trị),
        /// <paramref name="onSubmit"/> chỉ bắn khi người dùng chốt giá trị — row chạy ngay tại chỗ
        /// dùng cái này, không thì gõ "0.5" sẽ áp lần lượt "0" rồi "0.5". Switch và page chọn thì
        /// chốt luôn lúc đổi nên bắn cả hai.
        internal void AddField(string label, Type type, string current, Action<string> onChanged,
            Action<string> onSubmit = null, string description = null, bool allowVars = false)
        {
            if (type == typeof(bool))
            {
                bool.TryParse(current, out var on);
                AddToggle(label, on, value =>
                {
                    var text = value ? "true" : "false";
                    onChanged?.Invoke(text);
                    onSubmit?.Invoke(text);
                }, description);
                return;
            }

            if (type.IsEnum && !Attribute.IsDefined(type, typeof(FlagsAttribute)))
            {
                AddChoice(label, Enum.GetNames(type), current, value =>
                {
                    onChanged?.Invoke(value);
                    onSubmit?.Invoke(value);
                }, description);
                return;
            }

            var row = Spawn(inputTemplate, label, description);
            var input = row.input;
            var shape = Nullable.GetUnderlyingType(type) ?? type;
            input.contentType = ContentTypeFor(shape);
            input.characterLimit = shape == typeof(char) ? 1 : 0;
            if (input.placeholder is TMP_Text placeholder) placeholder.text = DebugLogConsole.GetTypeReadableName(shape);
            input.SetTextWithoutNotify(current);

            // GameObject/Component parse bằng GameObject.Find: tô màu theo từng phím là quét scene theo
            // từng phím. Kiểu đó chỉ kiểm lúc chốt (onEndEdit) hoặc lúc bấm Run.
            var liveCheck = DebugValues.IsInlineValue(type);
            var validColor = input.textComponent.color;
            input.textComponent.color = !liveCheck || DebugValues.TryParse(current, type, out _, out _, allowVars)
                ? validColor : InvalidColor;
            input.onValueChanged.AddListener(value =>
            {
                if (liveCheck)
                    input.textComponent.color = DebugValues.TryParse(value, type, out _, out _, allowVars) ? validColor : InvalidColor;
                onChanged?.Invoke(value);
            });
            if (onSubmit != null)
            {
                input.onEndEdit.AddListener(value =>
                {
                    if (input.wasCanceled || value == current) return;
                    if (!DebugValues.TryParse(value, type, out _, out var error, allowVars))
                    {
                        ShowResult($"{label}: {error}", true);
                        return;
                    }
                    current = value;
                    onSubmit(value);
                });
            }
        }

        private static TMP_InputField.ContentType ContentTypeFor(Type type)
        {
            if (type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte))
                return TMP_InputField.ContentType.IntegerNumber;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return TMP_InputField.ContentType.DecimalNumber;
            return TMP_InputField.ContentType.Standard;
        }

        /// Description là dòng thứ hai, nhỏ và xám, nằm trong cùng Text với tên. Panel format thay vì
        /// để chỗ gọi tự ghép markup: màu và size phụ thuộc template, chỗ gọi không cần biết.
        private DebugHubRow Spawn(DebugHubRow template, string label, string description = null)
        {
            DebugHubRow row;
            if (pool.TryGetValue(template, out var stack) && stack.Count > 0)
            {
                row = stack.Pop();
                Reset(row, template);
            }
            else
            {
                row = Instantiate(template, content);
                row.Template = template;
            }

            row.transform.SetAsLastSibling();
            row.gameObject.SetActive(true);
            if (row.label)
            {
                row.label.text = string.IsNullOrEmpty(description)
                    ? label
                    : $"{label}\n<size={DescriptionSize}><color={DescriptionColor}>{description}</color></size>";
            }
            spawnedRows.Add(row);
            FitRowColumns(row, false);
            return row;
        }

        private static void FitRowColumns(DebugHubRow row, bool hasDetail, bool compactDetail = false)
        {
            if (!row.label || !row.detail) return;
            var hasMore = row.more && row.more.gameObject.activeSelf;
            var chevron = row.transform.Find("Chevron") as RectTransform;
            var right = hasMore ? 164f : 36f;
            if (chevron)
            {
                chevron.anchoredPosition = new Vector2(-right - 18f, 0f);
                right += 48f;
            }
            var detail = row.detail.rectTransform;
            if (compactDetail)
            {
                detail.anchorMin = new Vector2(1f, 0f);
                detail.anchorMax = Vector2.one;
                detail.offsetMin = new Vector2(-right - 104f, 8f);
                detail.offsetMax = new Vector2(-right, -8f);
                row.detail.alignment = TextAlignmentOptions.MidlineRight;
                var compactLabel = row.label.rectTransform;
                compactLabel.anchorMin = Vector2.zero;
                compactLabel.anchorMax = Vector2.one;
                compactLabel.offsetMin = new Vector2(28f, 8f);
                compactLabel.offsetMax = new Vector2(-right - 116f, -8f);
                ((RectTransform)row.transform).sizeDelta = new Vector2(
                    ((RectTransform)row.transform).sizeDelta.x, ROW_HEIGHT);
                return;
            }
            detail.anchorMin = Vector2.zero;
            detail.anchorMax = new Vector2(1f, 0f);
            detail.offsetMin = new Vector2(28f, 8f);
            detail.offsetMax = new Vector2(-right, 82f);
            row.detail.alignment = TextAlignmentOptions.MidlineLeft;
            var label = row.label.rectTransform;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(28f, hasDetail ? 82f : 8f);
            label.offsetMax = new Vector2(-right, -8f);
            var rect = (RectTransform)row.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x,
                hasDetail ? VALUE_HEIGHT : ROW_HEIGHT);
        }

        /// Row cũ mang theo mọi thứ lần dựng trước đã sửa. Thiếu một dòng ở đây là một bug
        /// "thỉnh thoảng row cao bất thường / bấm ra hành động của page khác".
        private static void Reset(DebugHubRow row, DebugHubRow template)
        {
            var rect = (RectTransform)row.transform;
            var source = (RectTransform)template.transform;
            rect.sizeDelta = source.sizeDelta;

            if (row.label)
            {
                row.label.color = template.label.color;
                row.label.textWrappingMode = template.label.textWrappingMode;
                row.label.overflowMode = template.label.overflowMode;
                row.label.rectTransform.offsetMax = template.label.rectTransform.offsetMax;
                if (row.label.TryGetComponent<LayoutElement>(out var element) &&
                    template.label.TryGetComponent<LayoutElement>(out var sourceElement))
                    element.preferredHeight = sourceElement.preferredHeight;
            }
            if (row.detail) row.detail.text = string.Empty;
            if (row.input)
            {
                row.input.contentType = template.input.contentType;
                row.input.characterLimit = template.input.characterLimit;
                row.input.textComponent.color = template.input.textComponent.color;
                ((RectTransform)row.input.transform).offsetMax = ((RectTransform)template.input.transform).offsetMax;
            }
            if (row.more)
            {
                row.more.gameObject.SetActive(false);
                var label = row.more.GetComponent<TMP_Text>();
                var sourceLabel = template.more ? template.more.GetComponent<TMP_Text>() : null;
                if (label && sourceLabel)
                {
                    label.enabled = true;
                    label.text = sourceLabel.text;
                    label.color = sourceLabel.color;
                }
                var icon = row.more.GetComponentInChildren<DebugHubIcon>(true);
                if (icon) icon.gameObject.SetActive(false);
            }
        }

        private static void ReleaseListeners(DebugHubRow row)
        {
            if (row.button) row.button.onClick.RemoveAllListeners();
            if (row.toggle) row.toggle.onValueChanged.RemoveAllListeners();
            if (row.more) row.more.onClick.RemoveAllListeners();
            if (!row.input) return;
            row.input.onValueChanged.RemoveAllListeners();
            row.input.onEndEdit.RemoveAllListeners();
        }

        #endregion
    }
}
