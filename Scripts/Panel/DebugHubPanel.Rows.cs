using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Row của panel: các hàm Add* mà page gọi, pool row, căn cột trong row.
    public partial class DebugHubPanel
    {
        private static readonly Color InvalidColor = new Color(1f, 0.42f, 0.42f);

        private const string DescriptionColor = Palette.DIM;

        /// Chiều cao row một dòng / row có giá trị bên dưới, trên canvas tham chiếu 1080 — khớp prefab.
        private const float ROW_HEIGHT = 132f;
        private const float VALUE_HEIGHT = 172f;
        /// Footer cao 132 + 12 đệm: vùng cuộn co đúng chừng này khi footer hiện.
        private const float FOOTER_INSET = 144f;
        /// TMP nhận size theo phần trăm, không như legacy Text — nên description tự co theo cỡ chữ
        /// của từng template thay vì cứng 38.
        private const string DescriptionSize = "90%";

        /// Cột nút `…` bên phải row: ô nhập chừa đúng chừng này, và các cột khác lùi vào đúng chừng này.
        private const float MORE_COLUMN = 164f;
        /// Chữ phụ (giá trị) nằm dưới nhãn: cao chừng này, nhãn bắt đầu ngay trên nó.
        private const float DETAIL_HEIGHT = 82f;

        private readonly List<DebugHubRow> spawnedRows = new();
        private readonly Dictionary<DebugHubRow, Stack<DebugHubRow>> pool = new();

        /// Trả row về pool thay vì Destroy. Bắt buộc vì trang Live dựng lại 4 lần/giây (~80 row/giây),
        /// và nó xoá luôn cái vá Destroy-deferred-vs-DestroyImmediate.
        private void Clear()
        {
            FooterShown = false;
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

        /// Footer ghim đáy window (ngoài vùng cuộn, không phải một row): nút chính ở giữa, hai nút phụ hai bên
        /// (action null = mờ). Rebuild tắt nó trước khi dựng trang kế, nên trang nào cần thì tự gọi lại.
        internal void ShowBar(string previous, Action onPrevious, string primary, Action onPrimary, string next, Action onNext)
        {
            FooterShown = true;
            footer.Set(previous, onPrevious, primary, onPrimary, next, onNext);
        }

        /// Bật/tắt footer đi kèm đổi lề đáy của vùng cuộn — hai việc luôn đi cùng nhau nên nằm trong setter.
        private bool FooterShown
        {
            get => footer.gameObject.activeSelf;
            set
            {
                if (FooterShown == value) return;
                footer.gameObject.SetActive(value);
                // Tắt thì gỡ listener luôn: nút giữ closure của trang (model, item) nếu để nguyên.
                if (!value) footer.Release();
                var scroll = (RectTransform)scrollRect.transform;
                scroll.offsetMin += new Vector2(0f, value ? FOOTER_INSET : -FOOTER_INSET);
            }
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
        /// chữ. AddText không có nền nên lỗi trôi sát mép, nhìn như lỗi của cả panel. Nhãn (address, tên mục) và lời
        /// lỗi (exception của game) là dữ liệu: qua Escape.
        internal void AddError(string label, string message)
        {
            Spawn(buttonTemplate, $"<color={Palette.BAD}>{LogText.Escape(label)}</color>", LogText.Escape(message));
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
            // Giá trị là dữ liệu của game (chuỗi, tên object): qua LogText.Escape như mọi chỗ khác — `<noparse>` tự
            // bọc thì chuỗi chứa chính `</noparse>` vẫn thoát ra thành định dạng.
            if (row.detail) row.detail.text = LogText.Escape(value);
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
                field.offsetMax = new Vector2(-MORE_COLUMN, field.offsetMax.y);
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
        ///
        /// Lựa chọn là dữ liệu (Node.Choice lấy từ API) nên hiện qua Escape; so và trả về vẫn là chuỗi gốc.
        internal void AddChoice(string label, IReadOnlyList<string> options, string current, Action<string> onChanged,
            string description = null)
        {
            AddNavigation($"{label}:  <b>{LogText.Escape(current)}</b>", new DebugPage(label, page =>
            {
                var any = false;
                foreach (var option in options)
                {
                    // Phần tử null trong danh sách lấy từ API: không có gì để hiện, cũng không có gì để chọn.
                    if (option == null) continue;
                    any = true;
                    var picked = option;
                    if (picked == current)
                    {
                        page.AddAction(LogText.Escape(picked), page.Pop);
                        continue;
                    }
                    page.AddButton(LogText.Escape(picked), () =>
                    {
                        // Pop sau onChanged (ParamsPage phải StoreArgs trước khi dựng lại), nhưng chỉ khi
                        // onChanged không mở trang mới: node có Confirms() push trang xác nhận, Pop mù là
                        // gỡ luôn trang đó.
                        var depth = stack.Count;
                        onChanged?.Invoke(picked);
                        if (stack.Count == depth) page.Pop();
                    });
                }
                if (!any) page.AddText("Chưa có lựa chọn nào.");
            }), description);
        }

        /// Field cho một giá trị kiểu <paramref name="type"/>: enum ra page chọn, bool ra switch,
        /// số ra input chỉ nhận số, còn lại là input text. Mọi giá trị được validate bằng
        /// DebugValues.ParseArgument nên không có kiểu nào lọt qua mà không kiểm.
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
            if (input.placeholder is TMP_Text placeholder) placeholder.text = DebugValues.ReadableName(shape);
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
            var right = hasMore ? MORE_COLUMN : 36f;
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
                compactLabel.offsetMin = new Vector2(INSET, 8f);
                compactLabel.offsetMax = new Vector2(-right - 116f, -8f);
                ((RectTransform)row.transform).sizeDelta = new Vector2(
                    ((RectTransform)row.transform).sizeDelta.x, ROW_HEIGHT);
                return;
            }
            detail.anchorMin = Vector2.zero;
            detail.anchorMax = new Vector2(1f, 0f);
            detail.offsetMin = new Vector2(INSET, 8f);
            detail.offsetMax = new Vector2(-right, DETAIL_HEIGHT);
            row.detail.alignment = TextAlignmentOptions.MidlineLeft;
            var label = row.label.rectTransform;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(INSET, hasDetail ? DETAIL_HEIGHT : 8f);
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
