using System;
using System.Collections;
using IngameDebugConsole;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class ConsoleController : MonoBehaviour
    {
        public const string DEFAULT_ASSEMBLY = "Assembly-CSharp";
        private const string AUTO_ENABLE_KEY = "AutoEnableDebugConsole";
        [SerializeField] private string resourcePath;
        [SerializeField] private RepeatButton repeat;
        [SerializeField] private DebugHubEntry entry;

        private Canvas inGameDebugConsoleCanvas;
        object ans;

        /// Đẩy vào từ DebugHub.Awake() (chạy trước nhờ DefaultExecutionOrder(-100)): Proxima dùng
        /// chung password với xác thực hub nên DebugHub vẫn là nơi khởi tạo, console chỉ đăng ký command.
        internal ProximaFeature proxima;

        /// Console đang hiện hay không. Lần bật đầu tiên mới instantiate prefab console.
        public bool Enabled
        {
            get => inGameDebugConsoleCanvas && inGameDebugConsoleCanvas.enabled;
            set
            {
                if (inGameDebugConsoleCanvas)
                {
                    inGameDebugConsoleCanvas.enabled = value;
                    return;
                }
                if (!value) return;

                var prefab = Resources.Load<Canvas>(resourcePath);
                if (prefab == null)
                {
                    // Ví dụ khi build PRODUCTION: RenameFolderOnBuild đã đổi tên folder Resources.
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

        private void Awake()
        {
            // Command duy nhất còn đăng ký vào IDC: cầu để ô nhập lệnh của console vẫn chạy được
            // command của hub sau khi hub thôi dùng registry của IDC. VD: hub "level.goto 5".
            DebugLogConsole.AddCommand<string>("hub", "Chạy một command của Debug Hub, VD: hub \"level.goto 5\".",
                line => DebugHub.Execute(line, out _));

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

            // "inspect" thay cho get/set/reg trần ở gốc: cây page chỉ hiện tên lá nên một row tên
            // "get" không nói được nó đọc cái gì. Description phải một câu, ví dụ query để ở dev note.
            DebugHub.Add<string, string, string>(this, "inspect.get",
                    "Đọc giá trị của một object hoặc một lời gọi phương thức theo query.", Get)
                .Defaults(DEFAULT_ASSEMBLY, string.Empty, string.Empty).Stays();
            DebugHub.Add<string, string, string, string>(this, "inspect.set",
                    "Ghi giá trị vào một object, query giống inspect.get.", Set)
                .Defaults(DEFAULT_ASSEMBLY, string.Empty, string.Empty, string.Empty).Stays();
            DebugHub.Add(this, "inspect.last", "Log lại giá trị của lần đọc gần nhất.", GetAns)
                .Stays();

            DebugHub.Notes.Add("query của inspect: inspect.get Assembly-CSharp Class Instance.indexer[0].Method<$r1>(string, $r2)[0.1]");

            // Biến của debugger: đặt tên cho một giá trị/type rồi dùng lại trong query bằng $tên.
            DebugHub.Add<string>(this, "inspect.var.get", "Log giá trị đang gán cho một biến.", RegistryGet)
                .Stays();
            DebugHub.Add<string>(this, "inspect.var.save", "Gán giá trị của lần đọc gần nhất cho một biến.", RegistrySetAns)
                .Stays();
            DebugHub.Add<string, string, string, string>(this, "inspect.var.set",
                    "Parse stringValue theo type rồi gán cho một biến.", RegistrySet)
                .Defaults(string.Empty, DEFAULT_ASSEMBLY, string.Empty, string.Empty).Stays();
            DebugHub.Add<string, string, string>(this, "inspect.var.type", "Gán một type cho một biến.", RegistrySetType)
                .Defaults(string.Empty, DEFAULT_ASSEMBLY, string.Empty).Stays();

            // Debugger của SDK là UI riêng: hub còn hiện thì che mất, phải bấm được vào nó.
#if MAX_SDK
            DebugHub.Add(this, "sdk.max", "Mở mediation debugger của MAX.", ShowMaxDebugger).HidesHub();
#endif
#if USE_ADMOB
            DebugHub.Add(this, "sdk.admob", "Mở ad inspector của AdMob.", ShowAdMobDebugger).HidesHub();
#endif

            DebugHub.AddValue(this, "console.show", "Hiện cửa sổ log.", () => Enabled, value => Enabled = value);
            DebugHub.AddValue(this, "console.auto", "Tự bật console ở lần chạy sau.", () => AutoEnable, value => AutoEnable = value);
            if (proxima.Supported)
                DebugHub.AddValue(this, "console.proxima", "Bật Proxima Inspector.", () => proxima.Enabled, v => proxima.Enabled = v);

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
                UnityEngine.Debug.LogError("Key doesn't exist!");
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
            UnityEngine.Debug.Log($"PlayerPrefs int \"{key}\": - {value}");
        }

        void SetPlayerPrefsValue(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            UnityEngine.Debug.Log($"PlayerPrefs float \"{key}\": - {value}");
        }
        
        
        
        void SetTimeScale(float speed)
        {
            Time.timeScale = speed;
            UnityEngine.Debug.Log("Current time scale: " + Time.timeScale);
        }

        Coroutine timeSkip;
        void FastForward(float sec, float speed)
        {
            if (timeSkip != null)
            {
                StopCoroutine(timeSkip);
                timeSkip = null;
            }

            Time.timeScale = speed;

            timeSkip = StartCoroutine(Routine(sec, () =>
            {
                Time.timeScale = 1;
                timeSkip = null;
                UnityEngine.Debug.Log($"Skipped {sec}s!");
            }));
            return;
            
            IEnumerator Routine(float sec, System.Action callback)
            {
                yield return new WaitForSeconds(sec);
                callback?.Invoke();
            }
        }

        public void Get(string assemblyName, string typeName, string query)
        {
            ans = ExecuteAndGet(assemblyName, typeName, query);
            UnityEngine.Debug.Log("Result: " + ans);
        }

        public object ExecuteAndGet(string assemblyName, string typeName, string query)
        {
            return Executor.Get(AppDomain.CurrentDomain.Load(assemblyName), typeName, query);
        }
        
        public void Set(string assemblyName, string typeName, string query, string value)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);
            Executor.Set(assembly, typeName, query, value);
            UnityEngine.Debug.Log(string.Join('.', typeName, query) + ": " + Executor.Get(assembly, typeName, query));
        }

        void GetAns()
        {
            UnityEngine.Debug.Log("Ans: " + ans);
        }

        void RegistryGet(string key)
        {
            UnityEngine.Debug.Log(Executor.GetRegisteredObject(key));
        }

        void RegistrySetAns(string key)
        {
            Executor.Bind(key, ans);
        }

        void RegistrySet(string key, string assemblyName, string typeName, string stringValue)
        {
            object parsedObj = Executor.ParseObject(stringValue, AppDomain.CurrentDomain.Load(assemblyName).GetType(typeName));
            Executor.Bind(key, parsedObj);
            UnityEngine.Debug.Log($"Registered: \"{key}\" - \"{parsedObj}\" (type: {parsedObj?.GetType()})");
        }

        void RegistrySetType(string key, string assemblyName, string typeName)
        {
            Type type = Executor.GetType(AppDomain.CurrentDomain.Load(assemblyName), typeName);
            Executor.Bind(key, type);
            UnityEngine.Debug.Log($"Registered: \"{key}\" - \"{type}\" (type: {type?.GetType()})");
        }

        #endregion
        
        #if MAX_SDK
        public void ShowMaxDebugger()
        {
            MaxSdk.ShowMediationDebugger();
        }
        #endif
        
        #if USE_ADMOB
        public void ShowAdMobDebugger()
        {
            GoogleMobileAds.Api.MobileAds.OpenAdInspector(err =>
                {
                    if (err != null)
                    {
                        UnityEngine.Debug.LogError(
                            $"Ad Inspector failed: {err.GetMessage()} " +
                            $"Code: {err.GetCode()}"
                        );
                        return;
                    }

                    UnityEngine.Debug.Log("Ad Inspector closed");
                });
        }
        #endif
    }
}