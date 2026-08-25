using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        [SerializeField] private TMP_InputField authenticationInputField;
        [SerializeField] private NetworkReachabilityAuthenticationBypass networkReachabilityAuthenticationBypass;

        private ProximaFeature proxima;

        /// Mọi trigger trong danh sách đều được hỏi mỗi frame, bất kể platform/editor window — mỗi
        /// trigger tự biết đọc input của nó có sẵn hay không (không có touchscreen/keyboard thì tự
        /// trả false), nên không cần chọn trước một cách theo nền tảng. Thêm cách trigger mới chỉ
        /// cần viết class con của DebuggerAuthenticationTrigger rồi kéo component vào đây, không
        /// cần sửa file này.
        [SerializeField] private DebuggerAuthenticationTrigger[] triggers;

        /// Dev note hiện ở page Help.
        public static List<string> Notes { get; } = new();

        /// Ẩn/hiện nhanh cả hub từ bất kỳ đâu — cho code game gọi, và cho
        /// <see cref="DismissMode.HideHub"/> dùng sau khi chạy command.
        ///
        /// Ẩn = đóng panel + ẩn entry; panel giữ stack nên gọi lại là về đúng page đang xem. Đường gọi
        /// lại vẫn là trigger (lắc / gõ 4 góc) như khi tắt "Show entry button".
        ///
        /// Bật lại chỉ ăn khi đã xác thực: nếu không, gọi Visible = true từ code game là một đường vòng
        /// qua password.
        public static bool Visible
        {
            get => instance && instance.entry.Activating;
            set
            {
                if (!instance) return;

                if (value)
                {
                    if (instance.CurrentAuthenticationState != AuthenticationState.Success) return;
                    instance.entry.Activating = true;
                    return;
                }

                // Ẩn cả dòng kết quả: "ẩn hub" là để màn hình sạch (chụp ảnh level, xem UI game),
                // chừa lại một dòng chữ nổi ở đáy thì vẫn dính vào ảnh.
                instance.entry.Activating = false;
                instance.panel.HideResult();
                instance.panel.Close();
            }
        }

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

        /// Trigger có RequiresAlreadyAuthenticated (ví dụ lắc) chỉ được hỏi khi đã xác thực rồi —
        /// không phải một cách để mở khoá lần đầu, chỉ để gọi lại entry đã ẩn cho tiện.
        internal static bool AnyTriggerPerformed(IEnumerable<DebuggerAuthenticationTrigger> triggers, bool authenticated)
        {
            foreach (var trigger in triggers)
            {
                if (trigger.RequiresAlreadyAuthenticated && !authenticated) continue;
                if (trigger.IsPerformedTriggerAction()) return true;
            }
            return false;
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
            // Kết quả dài bị cắt ở dòng nổi; toàn văn kèm stack trace nằm ở log window.
            panel.ResultClicked += () => console.Enabled = true;
            authenticationInputField.onEndEdit.AddListener(OnAuthenticationInputFieldSubmitted);

            cachedCurrentAuthenticationState = (AuthenticationState)PlayerPrefs.GetInt(AUTHENTICATION_KEY);

            // EditMode test không tick player loop nên coroutine không bao giờ resume sau yield, và test
            // không được bắn network request thật — chỉ start khi Play thật (giống DestroyRow bên DebugHubPanel).
            if (Application.isPlaying && CurrentAuthenticationState != AuthenticationState.Success)
            {
                StartCoroutine(networkReachabilityAuthenticationBypass.Check(reachable =>
                {
                    if (!reachable) return;
                    if (CurrentAuthenticationState == AuthenticationState.Processing) AcceptAuthentication();
                    else CurrentAuthenticationState = AuthenticationState.Success;
                }));
            }
        }

        private void Update()
        {
            var authenticated = CurrentAuthenticationState == AuthenticationState.Success;
            var action = DecideAction(
                panel.IsOpen,
                entry.Activating,
                CurrentAuthenticationState == AuthenticationState.Processing,
                authenticated,
                () => AnyTriggerPerformed(triggers, authenticated));

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
        private static IEnumerator FocusNextFrame(TMP_InputField field)
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
            AcceptAuthentication();
        }

        private void AcceptAuthentication()
        {
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
                page.AddNavigation("Commands", CommandsPage.Root());
                page.AddNavigation("Help", HelpPage.Build());
                page.AddToggle("Console", console.Enabled, value => console.Enabled = value);
                page.AddToggle("Auto enable console", console.AutoEnable, value => console.AutoEnable = value);
                if (proxima.Supported) page.AddToggle("Proxima", proxima.Enabled, value => proxima.Enabled = value);
                page.AddToggle("Show entry button", entry.Activating, value => entry.Activating = value);
            });
        }
    }
}
