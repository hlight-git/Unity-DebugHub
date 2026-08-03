using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    [DefaultExecutionOrder(-100)]
    public class DebugHub : MonoBehaviour
    {
        private const string AUTHENTICATION_KEY = "DebugHub.AuthenticationState";
        internal static DebugHub InternalInstance { get; private set; }
        
        private enum AuthenticationState { None, Processing , Success }
        [SerializeField] private string password;
        [SerializeField] private DebugHubEntry entry;
        [SerializeField] private ConsoleController inGameDebugConsoleTrigger;
        [SerializeField] private ShowDebugObjects showDebugObjects;
        [SerializeField] private InputField authenticationInputField;
        [SerializeField] private MobileDeviceDebuggerAuthenticationTrigger mobileDeviceDebuggerAuthenticationTrigger;
        [SerializeField] private StandaloneDebuggerAuthenticationTrigger standaloneDebuggerAuthenticationTrigger;

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
        
        public ConsoleController InGameDebugConsoleAdapter => inGameDebugConsoleTrigger;
        
        internal string Password => password;
        internal List<DebugObject> DebugObjects { get; } = new();

        private void Awake()
        {
#if !PRODUCTION
            if (InternalInstance)
#endif
            {
                Destroy(gameObject);
                return;
            }
            
            inGameDebugConsoleTrigger.TryEnableConsole();
            DontDestroyOnLoad(this);
            InternalInstance = this;
            authenticationInputField.onEndEdit.AddListener(OnAuthenticationInputFieldSubmitted);
            
            cachedCurrentAuthenticationState = (AuthenticationState)PlayerPrefs.GetInt(AUTHENTICATION_KEY);
        }

        private void Update()
        {
            if (entry.Activating || entry.IsMainMenuActivating) return;
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

        #region API

        public static bool RegisterDebugObject(DebugObject debugObject, out bool active)
        {
#if PRODUCTION
            active = false;
            return false;
#endif
            InternalInstance.DebugObjects.Add(debugObject);
            active = InternalInstance.showDebugObjects.IsOn;
            return true;
        }

        public static void UnregisterDebugObject(DebugObject debugObject)
        {
#if !PRODUCTION
            InternalInstance.DebugObjects.Remove(debugObject);
#endif
        }

        #endregion
    }
}