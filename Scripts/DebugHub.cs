using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
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
            if (entry.Activating || panel.IsOpen) return;
            if (CurrentAuthenticationState == AuthenticationState.Processing) return;
            if (CurrentAuthenticationState == AuthenticationState.Success)
            {
                entry.Activating = true;
                return;
            }
            if (DebuggerAuthenticationTrigger == null || !DebuggerAuthenticationTrigger.IsPerformedTriggerAction()) return;

            CurrentAuthenticationState = AuthenticationState.Processing;
            authenticationInputField.gameObject.SetActive(true);
            authenticationInputField.Select();
            authenticationInputField.ActivateInputField();
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
                page.AddButton("Commands", () => page.Push(CommandsPage.Root()));
                page.AddButton("Help", () => page.Push(HelpPage.Build()));
            });
        }
    }
}
