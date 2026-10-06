using System;
using System.Collections.Generic;
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
    public partial class DebugHubPanel : MonoBehaviour
    {
        /// Tên của command chạy ngay. Đủ để đọc ra "bấm là chạy" mà không phải tô nền cả row.
        private static readonly Color AccentColor = new Color(0.60f, 0.87f, 0.83f);

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

        [Header("Log")]
        [SerializeField] private LogView logView;
        [SerializeField] private Button logButton;
        [Tooltip("Số lỗi chưa xem trên nút Log. Ẩn/hiện qua object cha (Badge).")]
        [SerializeField] private TMP_Text logBadge;
        [SerializeField] private Button moreButton;
        [Tooltip("Hàng nút ghim đáy window, ngoài vùng cuộn (trang chi tiết log). Tắt mặc định; bật qua ShowBar.")]
        [SerializeField] private DebugHubBar footer;

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
        private float nextRefresh;
        private bool refreshLater;
        private string pendingQuery;
        private float searchDue;
        private Vector2 fittedCanvasSize;
        private string lastBuildError;

        public bool IsOpen => gameObject.activeSelf;

        internal bool TopIsFixedHeight => stack.Count > 0 && stack.Peek().Page.FixedHeight;

        /// Trang trên cùng là danh sách log (không tính trang chi tiết): bấm dòng kết quả thì thay nó, khỏi chồng thêm một tầng log.
        internal bool TopIsLog => stack.Count > 0 && stack.Peek().Page.IsLog;

        /// Bấm vào dòng kết quả. DebugHub nối vào đây để mở trang log tại <see cref="LastResultSeq"/>.
        public event Action ResultClicked;

        /// Nội dung dòng kết quả lần cuối. Test đọc cái này thay vì bới vào toast.
        public string LastResult { get; private set; }

        /// Vạch log của chính lần chạy đã tạo dòng kết quả đang hiện; 0 = không do command (copy, lỗi nhập,
        /// inspect) — bấm vào thì mở log ở cuối.
        internal long LastResultSeq { get; private set; }

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
            logButton.onClick.AddListener(() => Push(LogPage.Build()));
            moreButton.onClick.AddListener(() =>
            {
                if (stack.Count > 0) stack.Peek().Page.More?.Invoke(this);
            });
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

        /// Gọi tường minh từ DebugHub.Awake, không từ Awake của panel: Panel tắt sẵn trong prefab nên Awake chỉ
        /// chạy ở lần mở đầu — trước đó bấm dòng kết quả không làm gì. Gọi lại không gắn thêm.
        internal void WireToast() => toast.Wire(() => ResultClicked?.Invoke());

        /// Hiện kết quả (hoặc lỗi) của command vừa chạy. <paramref name="logSeq"/>: vạch log của chính lần chạy
        /// đó (chỉ CommandsPage.RunNow có), để bấm vào là tới đúng chỗ.
        public void ShowResult(string text, bool error, long logSeq = 0)
        {
            LastResult = text;
            LastResultSeq = logSeq;
            toast.Show(text, error);
        }

        public void HideResult()
        {
            LastResult = string.Empty;
            LastResultSeq = 0;
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

        /// Thay trang trên cùng (Trước/Sau ở trang chi tiết): đi qua mười log không chất mười tầng stack.
        internal void Replace(DebugPage page)
        {
            SavePageState();
            if (stack.Count > 0) stack.Pop();
            stack.Push(new PageState { Page = page });
            query = string.Empty;
            Rebuild();
            RestoreScroll(1f);
        }

        /// Trang log gọi từ Build của nó: LogView mượn ScrollRect, giấu Content của row.
        internal void ShowLog(LogModel model, long focusSeq) => logView.Open(this, scrollRect, model, focusSeq);

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
            // Mọi trang đều qua đây: rời trang log thì trả ScrollRect về Content của row.
            logView.Close();
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
            logButton.gameObject.SetActive(page.ShowTools);
            moreButton.gameObject.SetActive(page.More != null);
            var unseen = LogRecorder.ErrorCount - LogModel.Shared.Seen[(int)LogGroup.Error];
            logBadge.transform.parent.gameObject.SetActive(unseen > 0);
            if (unseen > 0) logBadge.text = LogText.Badge(unseen);
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

    }
}
