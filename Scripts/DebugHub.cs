using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Việc mà DebugHub cần làm trong một frame, sau khi hỏi trigger action.
    internal enum DebugHubAction
    {
        None,
        ShowEntry,
        AskPassword,
    }

    [DefaultExecutionOrder(-100)]
    public class DebugHub : MonoBehaviour
    {
        private const string AUTHENTICATION_KEY = "DebugHub.AuthenticationState";
        private static DebugHub instance;

        private enum AuthenticationState { None, Processing , Success }
        [SerializeField] private string password;
        [SerializeField] private DebugHubEntry entry;
        [SerializeField] private DebugHubPanel panel;
        [SerializeField] private ConsoleController console;
        [SerializeField] private InputField authenticationInputField;
        [SerializeField] private MobileDeviceDebuggerAuthenticationTrigger mobileDeviceDebuggerAuthenticationTrigger;
        [SerializeField] private StandaloneDebuggerAuthenticationTrigger standaloneDebuggerAuthenticationTrigger;

        private ProximaFeature proxima;

        /// Dev note hiện ở page Help.
        public static List<string> Notes { get; } = new();

        /// Static giữ nguyên giữa các lần Play khi bật "Enter Play Mode without domain reload",
        /// không xoá thì note bị nhân đôi mỗi lần chạy.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Notes.Clear();
            instance = null;
        }

        private AuthenticationState cachedCurrentAuthenticationState;
        private AuthenticationState CurrentAuthenticationState
        {
            get
            {
#if ALWAYS_ENABLE_INGAME_DEBUGGER
                return AuthenticationState.Success;
#else
                return cachedCurrentAuthenticationState;
#endif
            }
            set
            {
#if !ALWAYS_ENABLE_INGAME_DEBUGGER
                cachedCurrentAuthenticationState = value;
                if (value != AuthenticationState.Processing)
                {
                    PlayerPrefs.SetInt(AUTHENTICATION_KEY, (int)value);
                }
#endif
            }
        }

        private IDebuggerAuthenticationTrigger DebuggerAuthenticationTrigger
        {
            get
            {
#if UNITY_EDITOR
                var playingWindowTitle = UnityEditor.EditorWindow.focusedWindow?.titleContent.text;
                if (playingWindowTitle == "Game") return standaloneDebuggerAuthenticationTrigger;
                if (playingWindowTitle == "Simulator") return mobileDeviceDebuggerAuthenticationTrigger;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_IPHONE
                return mobileDeviceDebuggerAuthenticationTrigger;
#else
                return standaloneDebuggerAuthenticationTrigger;
#endif
                return null;
            }
        }

        private void Awake()
        {
#if !PRODUCTION
            if (instance)
#endif
            {
                Destroy(gameObject);
                return;
            }

            console.TryEnableConsole();
            DontDestroyOnLoad(this);
            instance = this;
            proxima = new ProximaFeature(password);
            entry.Clicked += OpenRootPage;
            authenticationInputField.onEndEdit.AddListener(OnAuthenticationInputFieldSubmitted);

            cachedCurrentAuthenticationState = (AuthenticationState)PlayerPrefs.GetInt(AUTHENTICATION_KEY);
        }

        private void Update()
        {
            var action = DecideAction(
                panel.IsOpen,
                entry.Activating,
                CurrentAuthenticationState == AuthenticationState.Processing,
                CurrentAuthenticationState == AuthenticationState.Success,
                () => DebuggerAuthenticationTrigger != null && DebuggerAuthenticationTrigger.IsPerformedTriggerAction());

            switch (action)
            {
                case DebugHubAction.ShowEntry:
                    entry.Activating = true;
                    break;

                case DebugHubAction.AskPassword:
                    CurrentAuthenticationState = AuthenticationState.Processing;
                    authenticationInputField.gameObject.SetActive(true);
                    StartCoroutine(FocusNextFrame(authenticationInputField));
                    break;
            }
        }

        /// Trigger action là cửa cho cả hai việc: đã xác thực rồi thì nó hiện lại entry, chưa thì nó
        /// mở ô nhập password. Nhờ vậy tắt "Show entry button" mới giữ được sau khi đóng panel —
        /// bật entry ngay khi state là Success thì mỗi frame nó tự hiện lại.
        ///
        /// triggerPerformed là delegate chứ không phải bool: trigger trên mobile có state machine
        /// theo từng touch, hỏi nó lúc không cần sẽ làm hỏng chuỗi bước người dùng đang gõ.
        internal static DebugHubAction DecideAction(bool panelOpen, bool entryVisible, bool processing,
            bool authenticated, Func<bool> triggerPerformed)
        {
            if (panelOpen || entryVisible || processing) return DebugHubAction.None;
            if (triggerPerformed == null || !triggerPerformed()) return DebugHubAction.None;

            return authenticated ? DebugHubAction.ShowEntry : DebugHubAction.AskPassword;
        }

        /// Focus ngay trong frame vừa SetActive thì bị InputField.OnEnable xoá -> đợi một frame.
        private static IEnumerator FocusNextFrame(InputField field)
        {
            yield return null;
            field.Select();
            field.ActivateInputField();
        }

        private void OnAuthenticationInputFieldSubmitted(string input)
        {
            if (input != password)
            {
                CurrentAuthenticationState = AuthenticationState.None;
                authenticationInputField.gameObject.SetActive(false);
                return;
            }
            entry.Activating = true;
            Destroy(authenticationInputField.gameObject);
            CurrentAuthenticationState = AuthenticationState.Success;
        }

        private void OpenRootPage()
        {
            panel.Show(RootPage());
        }

        private DebugPage RootPage()
        {
            return new DebugPage("Debug Hub", page =>
            {
                page.AddToggle("Console", console.Enabled, value => console.Enabled = value);
                page.AddToggle("Auto enable console", console.AutoEnable, value => console.AutoEnable = value);
                if (proxima.Supported) page.AddToggle("Proxima", proxima.Enabled, value => proxima.Enabled = value);
                page.AddToggle("Show entry button", entry.Activating, value => entry.Activating = value);
                page.AddNavigation("Commands", CommandsPage.Root());
                page.AddNavigation("Help", HelpPage.Build());
            });
        }
    }
}
