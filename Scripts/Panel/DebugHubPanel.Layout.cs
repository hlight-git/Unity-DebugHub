using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Layout của panel: header, khung window, scrollbar. Kích thước theo canvas tham chiếu 1080 — khớp prefab.
    public partial class DebugHubPanel
    {
        private const float MIN_TOUCH_PIXELS = 44f;

        /// Header: hàng tiêu đề, cộng dòng address khi có, cộng ô tìm khi đang tìm.
        private const float HEADER_HEIGHT = 148f;
        private const float SUBTITLE_HEIGHT = 72f;
        private const float SEARCH_HEIGHT = 144f;

        /// Lề ngang chung của chữ trong header và trong row.
        private const float INSET = 28f;

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
            var height = HEADER_HEIGHT + (hasSubtitle ? SUBTITLE_HEIGHT : 0f) + (searching ? SEARCH_HEIGHT : 0f);
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
            sub.offsetMin = new Vector2(INSET, -216f);
            sub.offsetMax = new Vector2(-INSET, -144f);

            var field = (RectTransform)searchInput.transform;
            field.anchorMin = Vector2.zero;
            field.anchorMax = new Vector2(1f, 0f);
            field.offsetMin = new Vector2(INSET, 8f);
            field.offsetMax = new Vector2(-INSET, 140f);
            if (searchInput.placeholder is TMP_Text hint)
                hint.text = page.ShowTools ? "Tìm tất cả lệnh…" : $"Tìm trong {page.Title}…";
            searchButton.GetComponentInChildren<DebugHubIcon>(true).color =
                searching ? AccentColor : Palette.ToColor("#D9E4F1");
            var viewport = (RectTransform)scrollRect.transform;
            var logBar = page.IsLog ? LogView.BAR_HEIGHT : 0f;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -height - 12f - logBar);
            logView.PlaceBar(height);
        }

        private void ShowSubtitle(string address)
        {
            var has = !string.IsNullOrEmpty(address);
            subtitle.gameObject.SetActive(has);
            subtitle.text = has ? LogText.Escape(TailOf(address, 40)) : string.Empty;

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
            // Trang log cao tối đa cố định: co theo số log thì panel nhảy mỗi lần có log mới. Trang có footer
            // ghim đáy cũng vậy: footer mà co theo nội dung thì nhảy chỗ mỗi lần Trước/Sau.
            var desired = TopIsFixedHeight ? MaxWindowHeight : LayoutUtility.GetPreferredHeight(content) + chrome;

            // Ép anchor dọc về giữa: nếu window còn neo stretch (prefab/scene cũ) thì sizeDelta.y
            // chỉ là phần cộng thêm vào khoảng anchor, panel sẽ cao hơn max mà không hiểu vì sao.
            window.anchorMin = new Vector2(window.anchorMin.x, 0.5f);
            window.anchorMax = new Vector2(window.anchorMax.x, 0.5f);
            window.pivot = new Vector2(window.pivot.x, 0.5f);
            window.sizeDelta = new Vector2(window.sizeDelta.x, Mathf.Min(desired, MaxWindowHeight));
            window.anchoredPosition = new Vector2(window.anchoredPosition.x, 0f);
            SyncScrollbar();
        }

        /// Đo theo content đang gắn vào ScrollRect, không theo field `content`: trang log thay content bằng
        /// danh sách của nó và gọi lại đây mỗi lần danh sách đổi chiều cao.
        internal void SyncScrollbar()
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
            var shown = scrollRect.content;
            var visible = shown.rect.height > viewport.rect.height + 1f;
            bar.gameObject.SetActive(visible);
            // Chiều dài tối thiểu của tay cầm nằm ở prefab (handle +96, Sliding Area −96): ScrollRect tự ghi
            // size mỗi lần cuộn/dựng layout nên một sàn đặt ở đây sẽ bị đè ngay.
            bar.size = Mathf.Clamp01(viewport.rect.height / Mathf.Max(1f, shown.rect.height));
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
    }
}
