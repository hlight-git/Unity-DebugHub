using System;
using System.Collections;
using IngameDebugConsole;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class ConsoleController : ADebugOperation
    {
        public const string DEFAULT_ASSEMBLY = "Assembly-CSharp";
        private const string AUTO_ENABLE_KEY = "AutoEnableDebugConsole";
        [SerializeField] private string resourcePath;
        [SerializeField] private Toggle autoEnableToggle;

        private Canvas inGameDebugConsoleCanvas;
        object ans;

        protected override void Awake()
        {
            base.Awake();
            autoEnableToggle.onValueChanged.AddListener(OnAutoEnableToggleValueChanged);
            
            DebugLogConsole.AddCommand<string>("prefs.get", "Log giá trị được lưu trong PlayerPrefs.", GetPlayerPrefsValue);
            DebugLogConsole.AddCommand<string, string>("prefs.set.str", "Dùng tương tự PlayerPrefs.SetString().", SetPlayerPrefsValue);
            DebugLogConsole.AddCommand<string, int>("prefs.set.int", "Dùng tương tự PlayerPrefs.SetInt().", SetPlayerPrefsValue);
            DebugLogConsole.AddCommand<string, float>("prefs.set.float", "Dùng tương tự PlayerPrefs.SetFloat().", SetPlayerPrefsValue);
            DebugLogConsole.AddCommand<float>("time.scale", "Set giá trị time scale.", SetTimeScale);
            DebugLogConsole.AddCommand<float>("time.skip", "Tua nhanh với time scale là 100.", FastForward);
            DebugLogConsole.AddCommand<float, float>("time.skip", "Tua nhanh với time scale là tự thiết lập.", FastForward);
            DebugLogConsole.AddCommand<string, string>("get", "Log ra giá trị của một object hoặc một lời gọi phương thức tùy thuộc query. VD: get Class Instance.indexer[0].Method<$r1>(string, $r2)[0.1].", Get);
            DebugLogConsole.AddCommand<string, string, string>("get", "Tương tự get<string, string> nhưng tìm type bằng tên Assembly.", Get);
            DebugLogConsole.AddCommand<string, string, string>("set", "Set giá trị của một object, query giống get.", Set);
            DebugLogConsole.AddCommand<string, string, string, string>("set", "Set giá trị của một object, query giống get, thêm param assemblyName.", Set);
            DebugLogConsole.AddCommand("ans", "Log ra giá trị đã get trước đó.", GetAns);
            DebugLogConsole.AddCommand<string>("reg.get", "Log ra giá trị đã lưu trong registry của debugger.", RegistryGet);
            DebugLogConsole.AddCommand<string>("reg.set.ans", "Lưu giá trị trong ans vào trong registry của debugger.", RegistrySetAns);
            DebugLogConsole.AddCommand<string, string, string>("reg.set", "Parse stringValue về type mong muốn và lưu vào trong registry của debugger.", RegistrySet);
            DebugLogConsole.AddCommand<string, string, string, string>("reg.set", "Parse stringValue về type mong muốn và lưu vào trong registry của debugger.", RegistrySet);
            DebugLogConsole.AddCommand<string, string>("reg.type", "Lưu type vào trong registry của debugger.", RegistrySetType);
            DebugLogConsole.AddCommand<string, string, string>("reg.type", "Lưu type vào trong registry của debugger.", RegistrySetType);
            #if MAX_SDK
            DebugLogConsole.AddCommand("ad.max", "Show max debugger", ShowMaxDebugger);
            #endif
            #if USE_ADMOB
            DebugLogConsole.AddCommand("ad.admob", "Show admob inspector", ShowAdMobDebugger);
            #endif
        }

        private void OnEnable()
        {
            var consoleActivated = inGameDebugConsoleCanvas && inGameDebugConsoleCanvas.enabled;
            toggle.SetIsOnWithoutNotify(consoleActivated);
            autoEnableToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(AUTO_ENABLE_KEY) == 1);
        }

        protected override void OnToggleValueChanged(bool value)
        {
            if (inGameDebugConsoleCanvas)
            {
                inGameDebugConsoleCanvas.enabled = value;
                return;
            }
            if (value)
            {
                inGameDebugConsoleCanvas = Instantiate(Resources.Load<Canvas>(resourcePath));
            }
        }

        public void TryEnableConsole()
        {
            if (PlayerPrefs.GetInt(AUTO_ENABLE_KEY) == 1)
            {
                OnToggleValueChanged(true);
            }
        }

        private void OnAutoEnableToggleValueChanged(bool value)
        {
            PlayerPrefs.SetInt(AUTO_ENABLE_KEY, value ? 1 : 0);
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

        void FastForward(float sec)
        {
            FastForward(sec, 100);
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

        public void Get(string typeName, string query)
        {
            Get(DEFAULT_ASSEMBLY, typeName, query);
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
        
        public void Set(string typeName, string query, string value)
        {
            Set(DEFAULT_ASSEMBLY, typeName, query, value);
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
            UnityEngine.Debug.Log(Executor.GetRegistedObject(key));
        }

        void RegistrySetAns(string key)
        {
            Executor.Bind(key, ans);
        }

        void RegistrySet(string key, string typeName, string stringValue)
        {
            RegistrySet(key, DEFAULT_ASSEMBLY, typeName, stringValue);
        }

        void RegistrySet(string key, string assemblyName, string typeName, string stringValue)
        {
            object parsedObj = Executor.ParseObject(stringValue, AppDomain.CurrentDomain.Load(assemblyName).GetType(typeName));
            Executor.Bind(key, parsedObj);
            UnityEngine.Debug.Log($"Registered: \"{key}\" - \"{parsedObj}\" (type: {parsedObj?.GetType()})");
        }

        void RegistrySetType(string key, string typeName)
        {
            RegistrySetType(key, DEFAULT_ASSEMBLY, typeName);
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