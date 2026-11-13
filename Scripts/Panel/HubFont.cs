using System;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Chữ của hub vẽ bằng font hệ thống của máy, không bằng font mặc định của TMP Settings (là font game: thường
    /// chỉ có đủ chữ game cần, thiếu dấu tiếng Việt như ơ ư ự). Font game vẫn là đường lui khi máy không có font
    /// nào trong danh sách.
    ///
    /// Tạo lúc hub hiện lần đầu chứ không lúc mở app: máy người chơi không bao giờ mở hub thì không nạp file font,
    /// không cấp atlas.
    internal static class HubFont
    {
        /// Font hệ thống phổ biến có đủ tiếng Việt, theo thứ tự thử: Android, Windows, Apple, rồi Arial.
        internal static readonly string[] Families = { "Roboto", "Noto Sans", "Segoe UI", "Helvetica Neue", "Arial" };

        private const string Style = "Regular";

        /// Font phải vẽ được mấy chữ này mới được nhận (font hệ thống cùng tên có thể là bản cắt bớt).
        internal const string Probe = "ơưựẫđ";

        private static bool tried;
        private static TMP_FontAsset font;

        /// Font đã tạo, null khi chưa tạo hoặc máy không có font nào hợp.
        internal static TMP_FontAsset Current => font;

        /// Gán font hệ thống cho mọi chữ dưới root, kể cả template đang tắt: row nhân bản từ template sau này mang
        /// sẵn font. Không tìm được font hợp thì để nguyên (TMP dùng font mặc định).
        internal static void Apply(GameObject root)
        {
            var hubFont = Get();
            if (!hubFont) return;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != hubFont) text.font = hubFont;
            }
        }

        private static TMP_FontAsset Get()
        {
            if (tried) return font;
            tried = true;
            var installed = Font.GetOSInstalledFontNames() ?? Array.Empty<string>();
            foreach (var family in Families)
            {
                // Tra trước: CreateFontAsset(family, style) tự log một dòng mỗi tên không có trên máy.
                if (Array.IndexOf(installed, family) < 0) continue;

                var candidate = TMP_FontAsset.CreateFontAsset(family, Style);
                if (!candidate) continue;
                if (candidate.HasCharacters(Probe, out uint[] _, false, true))
                {
                    candidate.name = "DebugHub " + family;
                    font = candidate;
                    break;
                }
                foreach (var atlas in candidate.atlasTextures) Object.Destroy(atlas);
                Object.Destroy(candidate.material);
                Object.Destroy(candidate);
            }

            if (!font) UnityEngine.Debug.LogWarning("[DebugHub] Không tìm được font hệ thống có tiếng Việt, dùng font mặc định của TMP.");
            return font;
        }

        /// Thoát Play không reload domain: asset tạo lúc chạy đã bị huỷ, phải tạo lại ở lần Play sau.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            tried = false;
            font = null;
        }
    }
}
