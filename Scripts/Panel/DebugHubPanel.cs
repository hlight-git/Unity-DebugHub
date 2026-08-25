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

        /// Row điều hướng của chính hub (Recent, Search, Built-in) — không phải thư mục command của
        /// game, nên không dùng chung màu với chúng.
        private static readonly Color ShortcutColor = new Color(0.85f, 0.70f, 0.42f);
        private const string DescriptionColor = "#8A929C";
        /// TMP nhận size theo phần trăm, không như legacy Text — nên description tự co theo cỡ chữ
        /// của từng template thay vì cứng 38.
        private const string DescriptionSize = "80%";

        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform window;
        [Tooltip("Chiều cao tối đa của window; quá thì scroll.")]
        [SerializeField] private float maxWindowHeight = 1500f;
        [Tooltip("Dòng kết quả nổi ở đáy màn hình. Nằm ngoài panel nên vẫn thấy sau khi panel đóng.")]
        [SerializeField] private DebugHubToast toast;

        [Header("Row templates (inactive children of content)")]
        [SerializeField] private DebugHubRow buttonTemplate;
        [SerializeField] private DebugHubRow navTemplate;
        [SerializeField] private DebugHubRow actionTemplate;
        [SerializeField] private DebugHubRow toggleTemplate;
        [SerializeField] private DebugHubRow inputTemplate;
        [SerializeField] private DebugHubRow textTemplate;

        private readonly Stack<DebugPage> stack = new();
        private readonly List<DebugHubRow> spawnedRows = new();

        public bool IsOpen => gameObject.activeSelf;

        /// Bấm vào dòng kết quả. DebugHub nối vào đây để mở console log window (toàn văn ở đó).
        public event Action ResultClicked;

        /// Số tầng đang mở. Test dùng để biết một cú bấm đã pop hay chưa thay vì giả định
        /// Awake có chạy hay không.
        internal int StackDepth => stack.Count;

        private void Awake()
        {
            backgroundButton.onClick.AddListener(Close);
            if (backButton) backButton.onClick.AddListener(Pop);
            if (toast) toast.Clicked += () => ResultClicked?.Invoke();
        }

        /// Mở panel. Đóng panel không xoá stack nên lần mở sau về đúng page đang xem lúc đóng;
        /// <paramref name="root"/> chỉ dùng khi chưa có gì trong stack.
        public void Show(DebugPage root)
        {
            if (stack.Count == 0) stack.Push(root);
            gameObject.SetActive(true);
            Rebuild();
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
            if (toast) toast.Show(text, error);
        }

        public void HideResult()
        {
            if (toast) toast.Hide();
        }

        public void Push(DebugPage page)
        {
            stack.Push(page);
            Rebuild();
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

        private void Rebuild()
        {
            Clear();
            var page = stack.Peek();
            title.text = page.Title;
            if (backButton) backButton.gameObject.SetActive(stack.Count > 1);
            page.Build?.Invoke(this);
            FitWindowToContent();
            if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
        }

        /// Window cao đúng bằng nội dung, chặn trên bởi maxWindowHeight (quá thì scroll).
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
            window.sizeDelta = new Vector2(window.sizeDelta.x, Mathf.Min(desired, maxWindowHeight));
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

        private void Clear()
        {
            foreach (var row in spawnedRows)
            {
                if (!row) continue;

                // Tắt trước khi destroy: trong Play Mode, Destroy() bị hoãn tới cuối frame nên row
                // của page cũ vẫn được layout group tính vào chiều cao -> panel chỉ nở, không co lại.
                row.gameObject.SetActive(false);
                DestroyRow(row.gameObject);
            }
            spawnedRows.Clear();
        }

        private static void DestroyRow(GameObject row)
        {
            // Ở EditMode (test) thì Destroy() bị hoãn tới cuối frame và không bao giờ tới.
            if (Application.isPlaying) Destroy(row);
            else DestroyImmediate(row);
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

        /// Row điều hướng của chính hub, không phải nội dung do game đăng ký: Recent, Search, Built-in.
        public void AddShortcut(string label, DebugPage page, string detail = null)
        {
            var row = Spawn(navTemplate, label);
            row.label.color = ShortcutColor;
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

        public TMP_Text AddText(string content)
        {
            var row = Spawn(textTemplate, content);
            row.gameObject.SetActive(!string.IsNullOrEmpty(content));
            return row.label;
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
            var row = Instantiate(template, content);
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

        #endregion
    }
}
