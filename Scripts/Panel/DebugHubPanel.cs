using System;
using System.Collections.Generic;
using IngameDebugConsole;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Panel duy nhất của debug hub. Nội dung do page dựng ra qua các hàm Add*;
    /// điều hướng bằng stack. Back (nút ‹ ở header) lùi từng bước; bấm ra vùng tối bên ngoài đóng hẳn.
    ///
    /// Ngôn ngữ hình ảnh: row tối + chevron = đi sang page khác, row xanh = chạy ngay,
    /// row tối + switch = bật/tắt trạng thái.
    public class DebugHubPanel : MonoBehaviour
    {
        private static readonly Color InvalidColor = new Color(1f, 0.42f, 0.42f);

        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Text title;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform window;
        [Tooltip("Chiều cao tối đa của window; quá thì scroll.")]
        [SerializeField] private float maxWindowHeight = 1500f;

        [Header("Row templates (inactive children of content)")]
        [SerializeField] private DebugHubRow buttonTemplate;
        [SerializeField] private DebugHubRow navTemplate;
        [SerializeField] private DebugHubRow actionTemplate;
        [SerializeField] private DebugHubRow toggleTemplate;
        [SerializeField] private DebugHubRow inputTemplate;
        [SerializeField] private DebugHubRow textTemplate;

        private readonly Stack<DebugPage> stack = new();
        private readonly List<GameObject> spawnedRows = new();

        public bool IsOpen => gameObject.activeSelf;

        /// Số tầng đang mở. Test dùng để biết một cú bấm đã pop hay chưa thay vì giả định
        /// Awake có chạy hay không.
        internal int StackDepth => stack.Count;

        private void Awake()
        {
            backgroundButton.onClick.AddListener(Close);
            if (backButton) backButton.onClick.AddListener(Pop);
        }

        public void Show(DebugPage root)
        {
            stack.Clear();
            stack.Push(root);
            gameObject.SetActive(true);
            Rebuild();
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
        public void Close()
        {
            stack.Clear();
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
            // vì text được đo ở width của template.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
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

        private void Clear()
        {
            foreach (var row in spawnedRows)
            {
                if (!row) continue;

                // Tắt trước khi destroy: trong Play Mode, Destroy() bị hoãn tới cuối frame nên row
                // của page cũ vẫn được layout group tính vào chiều cao -> panel chỉ nở, không co lại.
                row.SetActive(false);
                DestroyRow(row);
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
        public void AddButton(string label, Action onClick)
        {
            Spawn(buttonTemplate, label).button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// Row có chevron: bấm là mở page khác.
        public void AddNavigation(string label, DebugPage page)
        {
            Spawn(navTemplate, label).button.onClick.AddListener(() => Push(page));
        }

        /// Row accent: bấm là chạy ngay.
        public void AddAction(string label, Action onClick)
        {
            Spawn(actionTemplate, label).button.onClick.AddListener(() => onClick?.Invoke());
        }

        public void AddToggle(string label, bool value, Action<bool> onChanged)
        {
            var row = Spawn(toggleTemplate, label);
            row.toggle.SetIsOnWithoutNotify(value);
            row.toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
        }

        public Text AddText(string content)
        {
            var row = Spawn(textTemplate, content);
            row.gameObject.SetActive(!string.IsNullOrEmpty(content));
            return row.label;
        }

        /// Chọn một giá trị trong danh sách: row hiện giá trị hiện tại, bấm vào thì mở page liệt kê
        /// lựa chọn, chọn xong tự back. Dùng page thay cho UI.Dropdown vì dropdown sinh canvas lồng
        /// bên trong Mask của scroll view nên list bị mờ và không bấm được.
        public void AddChoice(string label, IReadOnlyList<string> options, string current, Action<string> onChanged)
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
            }));
        }

        /// Field cho một giá trị kiểu <paramref name="type"/>: enum ra page chọn, bool ra switch,
        /// số ra input chỉ nhận số, còn lại là input text. Mọi giá trị được validate bằng
        /// DebugLogConsole.ParseArgument nên không có kiểu nào lọt qua mà không kiểm.
        public void AddField(string label, Type type, string current, Action<string> onChanged)
        {
            if (type == typeof(bool))
            {
                AddToggle(label, current == "true", value => onChanged?.Invoke(value ? "true" : "false"));
                return;
            }

            if (type.IsEnum && !Attribute.IsDefined(type, typeof(FlagsAttribute)))
            {
                AddChoice(label, Enum.GetNames(type), current, onChanged);
                return;
            }

            var row = Spawn(inputTemplate, label);
            var input = row.input;
            input.contentType = ContentTypeFor(type);
            input.characterLimit = type == typeof(char) ? 1 : 0;
            if (input.placeholder is Text placeholder) placeholder.text = DebugLogConsole.GetTypeReadableName(type);
            input.SetTextWithoutNotify(current);

            var validColor = input.textComponent.color;
            input.onValueChanged.AddListener(value =>
            {
                input.textComponent.color = DebugLogConsole.ParseArgument(value, type, out _) ? validColor : InvalidColor;
                onChanged?.Invoke(value);
            });
        }

        private static InputField.ContentType ContentTypeFor(Type type)
        {
            if (type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte))
                return InputField.ContentType.IntegerNumber;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return InputField.ContentType.DecimalNumber;
            return InputField.ContentType.Standard;
        }

        private DebugHubRow Spawn(DebugHubRow template, string label)
        {
            var row = Instantiate(template, content);
            row.gameObject.SetActive(true);
            if (row.label) row.label.text = label;
            spawnedRows.Add(row.gameObject);
            return row;
        }

        #endregion
    }
}
