using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bản phát qua kênh nội bộ (spec ① §3.3): chỉ cấp quyền ghi log, không cấp quyền mở hub.
    ///
    /// Kiểm thẳng bằng native: `Application.installerName` chỉ là app *thực hiện* cài (App Tester có thể
    /// nhờ trình cài hệ thống), `installMode` báo TestFlight là Store. APK cài tay không tính — file lộ
    /// ra ngoài là ai cũng cài được. Giả được bằng `adb install -i`: làm được vậy là dev.
    internal static class InstallSource
    {
        internal const string APP_TESTER = "dev.firebase.appdistribution";

        private static bool? isInternal;

        internal static bool IsInternal => isInternal ??= Detect();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => isInternal = null;

        internal static bool IsAppTester(string initiating, string installing) =>
            initiating == APP_TESTER || installing == APP_TESTER;

        private static bool Detect()
        {
#if UNITY_EDITOR
            return true;
#elif UNITY_IOS
            // TestFlight, ad-hoc, Xcode: biên lai sandboxReceipt. App Store: receipt.
            return DebugHub_IsSandboxReceipt() != 0;
#elif UNITY_ANDROID
            return DetectAndroid();
#else
            return false;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        // int chứ không bool: bool của C++ 1 byte, marshal mặc định của C# là BOOL 4 byte.
        [DllImport("__Internal")]
        private static extern int DebugHub_IsSandboxReceipt();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// getInstallSourceInfo có từ API 30; máy cũ hơn ném NoSuchMethodError → không phải nội bộ.
        private static bool DetectAndroid()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var packages = activity.Call<AndroidJavaObject>("getPackageManager");
                using var info = packages.Call<AndroidJavaObject>("getInstallSourceInfo", Application.identifier);
                return IsAppTester(info.Call<string>("getInitiatingPackageName"), info.Call<string>("getInstallingPackageName"));
            }
            catch (Exception)
            {
                return false;
            }
        }
#endif
    }
}
