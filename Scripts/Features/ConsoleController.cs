using System;
using System.Collections;
using System.Reflection;
using IngameDebugConsole;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class ConsoleController : MonoBehaviour
    {
        private const string AUTO_ENABLE_KEY = "AutoEnableDebugConsole";
        [SerializeField] private string resourcePath;
        [SerializeField] private RepeatButton repeat;
        [SerializeField] private DebugHubEntry entry;

        private Canvas inGameDebugConsoleCanvas;

        /// Console đang hiện hay không. Lần bật đầu tiên mới instantiate prefab console.
        public bool Enabled
        {
            get => inGameDebugConsoleCanvas && inGameDebugConsoleCanvas.enabled;
            set
            {
                // Build tắt log của Unity (com.hlight.logging dưới PRODUCTION) thì cửa sổ log trống trơn.
                // ponytail: bật lại cho hết phiên, không tắt lại khi đóng — chỉ máy tester mới tới được đây.
                if (value) UnityEngine.Debug.unityLogger.logEnabled = true;
                if (inGameDebugConsoleCanvas)
                {
                    inGameDebugConsoleCanvas.enabled = value;
                    return;
                }
                if (!value) return;

                var prefab = Resources.Load<Canvas>(resourcePath);
                if (prefab == null)
                {
                    // Ví dụ khi build có DISABLE_DEBUG_HUB: RenameFolderOnBuild đã đổi tên folder Resources.
                    UnityEngine.Debug.LogError($"Console prefab not found in Resources at \"{resourcePath}\".");
                    return;
                }
                inGameDebugConsoleCanvas = Instantiate(prefab);
            }
        }

        /// Tự bật console ở lần chạy sau.
        public bool AutoEnable
        {
            get => PlayerPrefs.GetInt(AUTO_ENABLE_KEY) == 1;
            set => PlayerPrefs.SetInt(AUTO_ENABLE_KEY, value ? 1 : 0);
        }

        public void TryEnableConsole()
        {
            if (AutoEnable) Enabled = true;
        }

        /// DebugHub gọi sau khi chắc mình là bản duy nhất — xem DebugHub.Awake.
        internal void Initialize()
        {
            // Command duy nhất còn đăng ký vào IDC: cầu để ô nhập lệnh của console vẫn chạy được
            // command của hub sau khi hub thôi dùng registry của IDC. VD: hub "level.goto 5".
            // Overload không có `out` tự log lỗi — gõ sai trong console phải thấy vì sao.
            DebugLogConsole.AddCommand<string>("hub", "Chạy một command của Debug Hub, VD: hub \"level.goto 5\".",
                line => DebugHub.Execute(line));

            // Đọc/ghi dữ liệu thì Stays(): kết quả hiện ở dòng kết quả, người ta còn tra tiếp chứ
            // không phải chạy một lần rồi ra nhìn game.
            DebugHub.Add<string>(this, "prefs.get", "Log giá trị đang lưu trong PlayerPrefs.", GetPlayerPrefsValue)
                .Stays();
            DebugHub.Add<string, string>(this, "prefs.set.str", "Ghi PlayerPrefs dạng string.", SetPlayerPrefsValue)
                .Stays();
            DebugHub.Add<string, int>(this, "prefs.set.int", "Ghi PlayerPrefs dạng int.", SetPlayerPrefsValue)
                .Stays();
            DebugHub.Add<string, float>(this, "prefs.set.float", "Ghi PlayerPrefs dạng float.", SetPlayerPrefsValue)
                .Stays();

            DebugHub.AddValue(this, "time.scale", "Time scale hiện tại.", () => Time.timeScale, SetTimeScale);

            // Các overload cũ chỉ tồn tại để đặt sẵn một tham số (speed = 100, assembly =
            // Assembly-CSharp). Page nhập liệu prefill được nên gộp về một command, đỡ hai row trùng tên.
            DebugHub.Add<float, float>(this, "time.skip", "Tua nhanh sec giây với time scale speed.", FastForward)
                .Defaults("1", "100");

            DebugHub.Notes.Add("Nút công cụ ở Commands gốc: Objects xem mục đã ghim, Duyệt để tìm type/instance; " +
                               "nút … trên một dòng để ghim hoặc lưu vào $biến.");

            // Debugger của SDK là UI riêng: hub còn hiện thì che mất, phải bấm được vào nó. Tra bằng tên nên
            // package không tham chiếu SDK nào — project không cài SDK đó thì không có row. FlattenHierarchy vì
            // MaxSdk khai method static ở lớp cha theo platform (MaxSdkUnityEditor/Android/iOS).
            const BindingFlags STATIC = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var maxDebugger = Type.GetType("MaxSdk, MaxSdk.Scripts")
                ?.GetMethod("ShowMediationDebugger", STATIC, null, Type.EmptyTypes, null);
            if (maxDebugger != null)
                DebugHub.Add(this, "sdk.max", "Mở mediation debugger của MAX.", () => maxDebugger.Invoke(null, null))
                    .HidesHub();

            var adInspector = Array.Find(
                Type.GetType("GoogleMobileAds.Api.MobileAds, GoogleMobileAds")?.GetMethods(STATIC) ?? Array.Empty<MethodInfo>(),
                method => method.Name == "OpenAdInspector" && method.GetParameters().Length == 1);
            if (adInspector != null)
                DebugHub.Add(this, "sdk.admob", "Mở ad inspector của AdMob.",
                    () => adInspector.Invoke(null, new object[] { AdInspectorClosed })).HidesHub();

            DebugHub.AddValue(this, "console.show", "Hiện cửa sổ log.", () => Enabled, value => Enabled = value);
            DebugHub.AddValue(this, "console.auto", "Tự bật console ở lần chạy sau.", () => AutoEnable, value => AutoEnable = value);
            if (ProximaFeature.Supported)
            {
                var proxima = new ProximaFeature();
                DebugHub.AddValue(this, "console.proxima", $"Bật Proxima Inspector. PIN kết nối: {proxima.Pin}",
                    () => proxima.Enabled, v => proxima.Enabled = v);
            }

            DebugHub.AddValue(this, "hub.repeat", "Nút chạy lại lệnh cuối.", () => repeat.Enabled, v => repeat.Enabled = v);
            DebugHub.Add(this, "hub.hide", "Ẩn hub để nhìn game — gọi lại bằng lắc / gõ 4 góc.", () => { }).HidesHub();

            // Root page cũ có toggle "Show entry button" — không qua Visible vì đó có side effect
            // đóng panel, chỉ set thẳng entry rồi Refresh() để nút repeat theo kịp.
            DebugHub.AddValue(this, "hub.entry", "Hiện/ẩn nút bấm mở hub.", () => entry.Activating, value =>
            {
                entry.Activating = value;
                repeat.Refresh();
            });
        }

        #region Commands

        void GetPlayerPrefsValue(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                UnityEngine.Debug.Log($"- As int: {PlayerPrefs.GetInt(key)}\n" +
                            $"- As float: {PlayerPrefs.GetFloat(key)}\n" +
                            $"- As string: {PlayerPrefs.GetString(key)}");
            }
            else
            {
                UnityEngine.Debug.LogError($"Không có key \"{key}\".");
            }
        }

        void SetPlayerPrefsValue(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            UnityEngine.Debug.Log($"PlayerPrefs string \"{key}\": {value}");
        }

        void SetPlayerPrefsValue(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            UnityEngine.Debug.Log($"PlayerPrefs int \"{key}\": {value}");
        }

        void SetPlayerPrefsValue(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            UnityEngine.Debug.Log($"PlayerPrefs float \"{key}\": {value}");
        }

        void SetTimeScale(float speed)
        {
            Time.timeScale = speed;
            UnityEngine.Debug.Log("Time scale: " + Time.timeScale);
        }

        private Coroutine timeSkip;
        private float timeScaleBeforeSkip;

        /// Tua sec giây game ở time scale speed, xong trả về time scale lúc trước. Chờ bằng giờ thật: game
        /// pause giữa chừng thì giờ game đứng im và time scale kẹt ở speed mãi.
        void FastForward(float sec, float speed)
        {
            if (timeSkip != null) StopCoroutine(timeSkip);
            else timeScaleBeforeSkip = Time.timeScale;

            Time.timeScale = speed;
            timeSkip = StartCoroutine(RestoreTimeScale(sec / Mathf.Max(Time.timeScale, 0.01f), sec));
        }

        private IEnumerator RestoreTimeScale(float realSeconds, float skipped)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = timeScaleBeforeSkip;
            timeSkip = null;
            UnityEngine.Debug.Log($"Đã tua {skipped}s.");
        }

        /// SDK đòi Action&lt;AdInspectorError&gt;; Action&lt;object&gt; vào được nhờ Action contravariant, nên khỏi
        /// tham chiếu kiểu lỗi của SDK.
        private static readonly Action<object> AdInspectorClosed = error =>
        {
            if (error == null) return;
            var message = error.GetType().GetMethod("GetMessage", Type.EmptyTypes)?.Invoke(error, null) ?? error;
            UnityEngine.Debug.LogError($"Ad Inspector: {message}");
        };

        #endregion
    }
}