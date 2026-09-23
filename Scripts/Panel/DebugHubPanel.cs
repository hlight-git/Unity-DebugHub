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
        private static readonly Color AccentColor = new Color(0.47f, 0.67f, 1f);

        private const string DescriptionColor = "#8A929C";
        /// TMP nhận size theo phần trăm, không như legacy Text — nên description tự co theo cỡ chữ
        /// của từng template thay vì cứng 38.
        private const string DescriptionSize = "80%";

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
        [Tooltip("Dòng kết quả nổi ở đáy màn hình. Nằm ngoài panel nên vẫn thấy sau khi panel đóng.")]
        [SerializeField] private DebugHubToast toast;

        [Header("Header")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button advancedButton;
        [SerializeField] private Button helpButton;

        private string query = string.Empty;

        [Header("Row templates (inactive children of content)")]
        [SerializeField] private DebugHubRow buttonTemplate;
        [SerializeField] private DebugHubRow navTemplate;
        [SerializeField] private DebugHubRow actionTemplate;
        [SerializeField] private DebugHubRow toggleTemplate;
        [SerializeField] private DebugHubRow inputTemplate;
        [SerializeField] private DebugHubRow textTemplate;

        private readonly Stack<DebugPage> stack = new();
        private readonly List<DebugHubRow> spawnedRows = new();
        private readonly Dictionary<DebugHubRow, Stack<DebugHubRow>> pool = new();
        private float nextRefresh;
        private bool refreshLater;

        public bool IsOpen => gameObject.activeSelf;

        /// Bấm vào dòng kết quả. DebugHub nối vào đây để mở console log window (toàn văn ở đó).
        public event Action ResultClicked;

        /// Nội dung dòng kết quả lần cuối. Test đọc cái này thay vì bới vào toast.
        public string LastResult { get; private set; }

        /// Số tầng đang mở. Test dùng để biết một cú bấm đã pop hay chưa thay vì giả định
        /// Awake có chạy hay không.
        internal int StackDepth => stack.Count;

        /// Từ khoá đang lọc. Ô nhập nằm trong Header, **ngoài** content — đó là lý do kỹ thuật khiến
        /// search phải ở tiêu đề: Rebuild() destroy sạch content, nên ô nhập đặt trong list sẽ tự giết
        /// chính nó ngay khi gõ chữ đầu tiên.
        public string Query
        {
            get => query;
            set
            {
                if (query == value) return;
                query = value ?? string.Empty;
                Rebuild();
            }
        }

        public void OpenSearch()
        {
            searchInput.gameObject.SetActive(true);
            title.gameObject.SetActive(false);
            searchInput.SetTextWithoutNotify(query);
            searchInput.Select();
            searchInput.ActivateInputField();
        }

        public void CloseSearch()
        {
            searchInput.gameObject.SetActive(false);
            title.gameObject.SetActive(true);
            Query = string.Empty;
        }

        private void Awake()
        {
            backgroundButton.onClick.AddListener(Close);
            if (backButton) backButton.onClick.AddListener(Pop);
            if (toast) toast.Clicked += () => ResultClicked?.Invoke();

            searchButton.onClick.AddListener(() =>
            {
                if (searchInput.gameObject.activeSelf) CloseSearch();
                else OpenSearch();
            });
            searchInput.onValueChanged.AddListener(value => Query = value);
            helpButton.onClick.AddListener(() => Push(HelpPage.Build()));
            if (subtitle && subtitle.TryGetComponent<Button>(out var copy))
            {
                copy.onClick.AddListener(() =>
                {
                    if (stack.Count == 0 || string.IsNullOrEmpty(stack.Peek().Subtitle)) return;
                    GUIUtility.systemCopyBuffer = stack.Peek().Subtitle;
                    ShowResult("đã copy address", false);
                });
            }
            advancedButton.onClick.AddListener(() => Push(AdvancedPage.Root()));
        }

        private void Update()
        {
            if (!IsOpen || stack.Count == 0 || (!stack.Peek().Live && !refreshLater)) return;
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
        public void Show(DebugPage root)
        {
            if (stack.Count == 0) stack.Push(root);
            gameObject.SetActive(true);
            Rebuild();
            if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
        }

        /// Về page gốc, bỏ đường đã đi. Không đặt tên Reset: MonoBehaviour.Reset là magic method của
        /// Unity (menu Reset trong inspector), để trùng tên chỉ gây nhầm.
        public void ShowFromRoot(DebugPage root)
        {
            stack.Clear();
            Show(root);
        }

        /// Hiện kết quả (hoặc lỗi) của command vừa chạy.
        public void ShowResult(string text, bool error)
        {
            LastResult = text;
            if (toast) toast.Show(text, error);
        }

        public void HideResult()
        {
            LastResult = string.Empty;
            if (toast) toast.Hide();
        }

        public void Push(DebugPage page)
        {
            stack.Push(page);
            Rebuild();
            if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
        }

        public void Pop()
        {
            if (stack.Count > 0) stack.Pop();
            if (stack.Count == 0)
            {
                Clear();
                gameObject.SetActive(false);
                return;
            }
            Rebuild();
            if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
        }

        /// Bấm ra ngoài panel: đóng hẳn bất kể đang ở page nào, khác với Pop() (lùi từng bước qua nút back).
        ///
        /// Giữ nguyên stack: mở lại là về đúng page đang xem, khỏi bò lại từ root mỗi lần — nhất là với
        /// DismissMode.HideHub (ẩn hub để xem game rồi gọi lại tiếp tục làm việc đang làm).
        public void Close()
        {
            Clear();
            gameObject.SetActive(false);
        }

        /// Dựng lại trang hiện tại mà **giữ** vị trí scroll. Trang Live gọi cái này 4 lần/giây;
        /// Show/Push/Pop mới là điều hướng và mới được kéo scroll về đầu.
        public void Refresh()
        {
            if (stack.Count == 0) return;
            var scroll = scrollRect ? scrollRect.verticalNormalizedPosition : 1f;
            Rebuild();
            if (scrollRect) scrollRect.verticalNormalizedPosition = scroll;
        }

        private void Rebuild()
        {
            Clear();
            var page = stack.Peek();
            title.text = page.Title;
            if (backButton) backButton.gameObject.SetActive(stack.Count > 1);
            if (searchButton) searchButton.gameObject.SetActive(page.Searchable);
            if (advancedButton) advancedButton.gameObject.SetActive(!InAdvanced());
            ShowSubtitle(page.Subtitle);

            // page.Searchable phải được xét ở đây, không chỉ để ẩn cái nút: query sống qua các lần
            // Rebuild, nên một trang nhập tham số mở ra trong lúc còn query sẽ bị thay bằng kết quả
            // search và không bao giờ dựng được.
            if (string.IsNullOrEmpty(query) || !page.Searchable) page.Build?.Invoke(this);
            else if (page.Search != null) page.Search(this, query);
            else CommandsPage.SearchAll(this, query);   // mặc định: tìm toàn registry

            FitWindowToContent();
            // scroll về đầu chuyển sang Show/Push/Pop — Refresh() tự khôi phục vị trí cũ.
        }

        /// Trần theo **tỉ lệ màn hình**, không phải một con số cố định: 1500 px cứng làm panel chỉ dùng
        /// 62% chiều cao trên máy 1080×2400 và chừa 900 px đen, trong khi danh sách member phải cuộn 5 lần.
        internal float MaxWindowHeight
        {
            get
            {
                var canvas = window.GetComponentInParent<Canvas>();
                var scale = canvas ? canvas.scaleFactor : 1f;
                return Screen.height / Mathf.Max(scale, 0.0001f) * maxScreenHeight;
            }
        }

        private bool InAdvanced()
        {
            foreach (var entry in stack)
            {
                if (entry.AdvancedRoot) return true;
            }
            return false;
        }

        private const float SUBTITLE_HEIGHT = 40f;

        private void ShowSubtitle(string address)
        {
            if (!subtitle) return;
            var has = !string.IsNullOrEmpty(address);
            subtitle.gameObject.SetActive(has);
            subtitle.text = has ? TailOf(address, 40) : string.Empty;

            // Có dòng phụ thì tiêu đề nhích lên chừa chỗ; không có thì về giữa header như cũ.
            var rect = title.rectTransform;
            rect.offsetMin = new Vector2(rect.offsetMin.x, has ? SUBTITLE_HEIGHT : 0f);
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
            if (!window || !scrollRect) return;

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
                row.gameObject.SetActive(false);
                row.transform.SetParent(content, false);
                if (!pool.TryGetValue(row.Template, out var stack)) pool[row.Template] = stack = new Stack<DebugHubRow>();
                stack.Push(row);
            }
            spawnedRows.Clear();
        }

        #region Elements

        /// Row trung tính, không ngụ ý sẽ đi đâu hay chạy gì.
        public void AddButton(string label, Action onClick, string description = null)
        {
            Spawn(buttonTemplate, label, description).button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// Row có chevron: bấm là mở page khác. <paramref name="detail"/> là chữ phụ căn phải (số
        /// lượng command trong thư mục) — căn phải để mắt dò theo một cột thẳng thay vì trôi theo tên.
        public void AddNavigation(string label, DebugPage page, string description = null, string detail = null)
        {
            var row = Spawn(navTemplate, label, description);
            if (row.detail) row.detail.text = detail;
            row.button.onClick.AddListener(() => Push(page));
        }

        /// Row tối, tên màu accent: bấm là chạy ngay. Không tô nền accent — một page toàn row nền
        /// accent thì thành tường màu, và description trên nền đó đọc không nổi.
        public void AddAction(string label, Action onClick, string description = null)
        {
            var row = Spawn(buttonTemplate, label, description);
            row.label.color = AccentColor;
            row.button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// Row nền accent: hành động chính của page (nút Run). Mỗi page chỉ nên có một cái.
        public void AddPrimary(string label, Action onClick)
        {
            Spawn(actionTemplate, label).button.onClick.AddListener(() => onClick?.Invoke());
        }

        public void AddToggle(string label, bool value, Action<bool> onChanged, string description = null)
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
        public void AddError(string label, string message)
        {
            Spawn(buttonTemplate, $"<color={Palette.BAD}>{label}</color>", message);
        }

        public TMP_Text AddText(string content)
        {
            var row = Spawn(textTemplate, content);
            row.gameObject.SetActive(!string.IsNullOrEmpty(content));
            return row.label;
        }

        /// Row chỉ đọc: nhãn trái, giá trị căn phải, bấm là copy. Copy thành hành vi mặc định của
        /// mọi dòng giá trị chứ không phải thứ mỗi trang info tự viết lại.
        public void AddCopyRow(string label, string value, string description = null)
        {
            // buttonTemplate chứ không phải navTemplate: row này **không** đi đâu cả, mà navTemplate có
            // chevron nên nhìn như mở được. Nó cũng là template mang nút … (§3).
            var row = Spawn(buttonTemplate, label, description);
            if (row.detail) row.detail.text = value;
            row.button.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = value;
                ShowResult($"đã copy {label}", false);
            });
        }

        /// Gắn nút `…` (page Thao tác) cho row vừa dựng. `run` đi kèm vì page Thao tác có thao tác
        /// `Gán` — nó phải đi qua đúng luồng chạy của node (Confirm, Dismiss, dòng kết quả), không
        /// được gọi thẳng node.Set.
        public void AttachActions(ValueNode node, object current, Action<DebugNode, string[]> run)
        {
            var row = spawnedRows[spawnedRows.Count - 1];
            if (!row.more) return;
            row.more.gameObject.SetActive(true);

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
        public void AddChoice(string label, IReadOnlyList<string> options, string current, Action<string> onChanged,
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
                        onChanged?.Invoke(picked);
                        page.Pop();
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
        public void AddField(string label, Type type, string current, Action<string> onChanged,
            Action<string> onSubmit = null, string description = null)
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
            input.contentType = ContentTypeFor(type);
            input.characterLimit = type == typeof(char) ? 1 : 0;
            if (input.placeholder is TMP_Text placeholder) placeholder.text = DebugLogConsole.GetTypeReadableName(type);
            input.SetTextWithoutNotify(current);

            var validColor = input.textComponent.color;
            input.onValueChanged.AddListener(value =>
            {
                input.textComponent.color = DebugLogConsole.ParseArgument(value, type, out _) ? validColor : InvalidColor;
                onChanged?.Invoke(value);
            });
            if (onSubmit != null)
            {
                input.onEndEdit.AddListener(value =>
                {
                    if (DebugLogConsole.ParseArgument(value, type, out _)) onSubmit(value);
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
            return row;
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
                row.label.enableWordWrapping = template.label.enableWordWrapping;
                if (row.label.TryGetComponent<LayoutElement>(out var element) &&
                    template.label.TryGetComponent<LayoutElement>(out var sourceElement))
                    element.preferredHeight = sourceElement.preferredHeight;
            }
            if (row.detail) row.detail.text = string.Empty;
            if (row.button) row.button.onClick.RemoveAllListeners();
            if (row.toggle) row.toggle.onValueChanged.RemoveAllListeners();
            if (row.input)
            {
                row.input.onValueChanged.RemoveAllListeners();
                row.input.onEndEdit.RemoveAllListeners();
                row.input.contentType = template.input.contentType;
                row.input.characterLimit = template.input.characterLimit;
                row.input.textComponent.color = template.input.textComponent.color;
            }
            if (row.more) row.more.gameObject.SetActive(false);
        }

        #endregion
    }
}
