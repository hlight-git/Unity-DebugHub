using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
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

        /// Hỏi hệ thống ngay trên thread đang gọi (JNI + binder tới PackageManager trên Android). Lúc khởi động dùng
        /// IsInternalAsync: pha khởi động mà chặn thì game không vẽ được khung nào và Android báo ANR.
        internal static bool IsInternal => isInternal ??= Detect();

        /// Như IsInternal nhưng không bao giờ chặn main thread lúc khởi động: đợi khung đầu vẽ xong, hỏi hệ thống trên thread
        /// riêng, kết quả về lại main thread. Editor và nền tảng không phải hỏi hệ thống thì trả ngay.
#pragma warning disable CS1998 // nhánh editor / nền tảng khác không có await: trả ngay là đúng ý
        internal static async Awaitable<bool> IsInternalAsync()
        {
            if (isInternal.HasValue) return isInternal.Value;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Lần đầu NextFrameAsync có thể chạy ngay trong khung đầu; lần thứ hai chắc chắn khung đầu đã vẽ xong.
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var detected = new TaskCompletionSource<bool>();
            new Thread(() =>
            {
#if UNITY_ANDROID
                AndroidJNI.AttachCurrentThread();
#endif
                try { detected.SetResult(Detect()); }
                catch (Exception) { detected.SetResult(false); }
#if UNITY_ANDROID
                finally { AndroidJNI.DetachCurrentThread(); }
#endif
            }) { IsBackground = true, Name = "DebugHub install source" }.Start();
            // await giữ SynchronizationContext của Unity: phần sau chạy lại trên main thread.
            var result = await detected.Task;
            return isInternal ??= result;
#else
            return IsInternal;
#endif
        }
#pragma warning restore CS1998

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
