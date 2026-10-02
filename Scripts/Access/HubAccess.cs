using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Hai quyền tách bạch (spec ① §3.1). Ghi log vô hình, chỉ nằm trong RAM, nên cấp rộng hơn mở hub:
    /// reviewer Apple cài sandbox giống hệt TestFlight — bị ghi log cũng không thấy gì, nhưng mở được hub
    /// (bong bóng, lắc) thì lộ.
    internal static class HubAccess
    {
        /// Cùng key và giá trị với bản trước: máy đã mở khoá thì vẫn mở.
        internal const string UNLOCK_KEY = "DebugHub.AuthenticationState";
        private const int UNLOCKED = 2;

        /// Cờ mở hub: password hoặc dấu hiệu mạng công ty đã ghi.
        internal static bool ReadUnlocked() => PlayerPrefs.GetInt(UNLOCK_KEY) == UNLOCKED;

        /// Save ngay: mở khoá là việc một lần, app bị giết trước lần auto-save là mất.
        internal static void SaveUnlocked()
        {
            PlayerPrefs.SetInt(UNLOCK_KEY, UNLOCKED);
            PlayerPrefs.Save();
        }

        internal static bool MayRecord(bool unlocked, bool internalBuild) => unlocked || internalBuild;

        /// Hỏi nguồn cài trước: bản nội bộ thì khỏi đọc PlayerPrefs ở pha khởi động sớm nhất.
        internal static bool MayRecordNow() => InstallSource.IsInternal || ReadUnlocked();
    }
}
