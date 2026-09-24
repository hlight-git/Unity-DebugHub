using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        /// Cùng key và giá trị với bản trước: máy đã mở khoá thì vẫn mở.
        private const string AUTHENTICATION_KEY = "DebugHub.AuthenticationState";
        private const int AUTHENTICATED = 2;

        private static DebugHub instance;

        [Tooltip("Password để mở hub. Để trống thì không mở được.")]
        [SerializeField] private string password;
        [SerializeField] private DebugHubEntry entry;
        [SerializeField] private DebugHubPanel panel;
        [SerializeField] private ConsoleController console;
        [SerializeField] private RepeatButton repeat;
        [SerializeField] private TMP_InputField authenticationInputField;
        [SerializeField] private NetworkReachabilityAuthenticationBypass networkReachabilityAuthenticationBypass;

        /// Mọi trigger trong danh sách đều được hỏi mỗi frame, bất kể platform/editor window — mỗi
        /// trigger tự biết đọc input của nó có sẵn hay không (không có touchscreen/keyboard thì tự
        /// trả false), nên không cần chọn trước một cách theo nền tảng. Thêm cách trigger mới chỉ
        /// cần viết class con của DebuggerAuthenticationTrigger rồi kéo component vào đây, không
        /// cần sửa file này.
        [SerializeField] private DebuggerAuthenticationTrigger[] triggers;

        private bool unlocked;
        private bool askingPassword;
        private Func<bool> triggerPerformed;

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
                    if (!instance.Unlocked) return;
                    instance.entry.Activating = true;
                    instance.repeat.Refresh();
                    return;
                }

                // Ẩn cả dòng kết quả: "ẩn hub" là để màn hình sạch (chụp ảnh level, xem UI game),
                // chừa lại một dòng chữ nổi ở đáy thì vẫn dính vào ảnh.
                instance.entry.Activating = false;
                instance.repeat.gameObject.SetActive(false);
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

        private bool Unlocked
        {
            get
            {
#if ALWAYS_ENABLE_INGAME_DEBUGGER
                return true;
#else
                return unlocked;
#endif
            }
        }

        /// Trigger có RequiresAlreadyAuthenticated (ví dụ lắc) chỉ được hỏi khi đã xác thực rồi —
        /// không phải một cách để mở khoá lần đầu, chỉ để gọi lại entry đã ẩn cho tiện.
        internal static bool AnyTriggerPerformed(IReadOnlyList<DebuggerAuthenticationTrigger> triggers, bool authenticated)
        {
            for (var i = 0; i < triggers.Count; i++)
            {
                if (triggers[i].RequiresAlreadyAuthenticated && !authenticated) continue;
                if (triggers[i].IsPerformedTriggerAction()) return true;
            }
            return false;
        }

        private void Awake()
        {
#if !DISABLE_DEBUG_HUB
            if (instance)
#endif
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(this);
            instance = this;
            // Đăng ký command từ đây chứ không từ Awake của console: bản bị huỷ ngay trên kia (trùng hoặc
            // DISABLE_DEBUG_HUB) thì không đăng ký gì.
            console.Initialize();
            console.TryEnableConsole();
            repeat.Initialize();
            entry.Clicked += OpenCommandTree;
            // Kết quả dài bị cắt ở dòng nổi; toàn văn kèm stack trace nằm ở log window.
            panel.ResultClicked += () => console.Enabled = true;
            authenticationInputField.onEndEdit.AddListener(OnAuthenticationInputFieldSubmitted);
            // Tạo một lần: lambda tạo trong Update là một lần cấp phát mỗi frame trên máy mọi người chơi.
            triggerPerformed = () => AnyTriggerPerformed(triggers, Unlocked);
            unlocked = PlayerPrefs.GetInt(AUTHENTICATION_KEY) == AUTHENTICATED;

#if !ALWAYS_ENABLE_INGAME_DEBUGGER
            if (string.IsNullOrEmpty(password))
                UnityEngine.Debug.LogError("[DebugHub] Chưa điền password (Inspector của component DebugHub) — hub không mở được.");
#endif

            // EditMode test không tick player loop nên coroutine không bao giờ resume sau yield, và test
            // không được bắn network request thật — chỉ start khi Play thật.
            //
            // Chỉ build development: hub nằm cả trong bản store, mà ở đó mọi máy người chơi chưa mở khoá sẽ
            // gửi request tới checkUrls (thường là IP nội bộ) mỗi lần mở app — iOS hỏi quyền mạng cục bộ, và
            // mạng nào tình cờ có máy ở IP đó là mở khoá luôn.
            if (Application.isPlaying && UnityEngine.Debug.isDebugBuild && !Unlocked)
            {
                StartCoroutine(networkReachabilityAuthenticationBypass.Check(reachable =>
                {
                    if (!reachable) return;
                    if (askingPassword) AcceptAuthentication();
                    else Remember();
                }));
            }
        }

        // Bản trùng bị Destroy trong Awake chưa Initialize: `-=` handler chưa đăng ký là no-op.
        private void OnDestroy() => repeat.Release();

        private void Update()
        {
            switch (DecideAction(panel.IsOpen, entry.Activating, askingPassword, Unlocked, triggerPerformed))
            {
                case DebugHubAction.ShowEntry:
                    // Qua Visible chứ không set entry.Activating trực tiếp: nút repeat sống ngoài
                    // entry, phải được refresh cùng lúc entry hiện lại.
                    Visible = true;
                    break;

                case DebugHubAction.AskPassword:
                    askingPassword = true;
                    authenticationInputField.text = string.Empty;
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
            // Tắt ô nhập cũng bắn onEndEdit — cờ này chặn lần bắn thứ hai.
            if (!askingPassword) return;
            if (!string.IsNullOrEmpty(password) && input == password)
            {
                AcceptAuthentication();
                return;
            }
            askingPassword = false;
            authenticationInputField.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(input)) panel.ShowResult("Sai mật khẩu.", true);
        }

        private void AcceptAuthentication()
        {
            askingPassword = false;
            authenticationInputField.text = string.Empty;
            authenticationInputField.gameObject.SetActive(false);
            Remember();
            Visible = true;
        }

        private void Remember()
        {
            unlocked = true;
            PlayerPrefs.SetInt(AUTHENTICATION_KEY, AUTHENTICATED);
        }

        /// Gốc panel là cây command thẳng — không còn trang menu trung gian "Debug Hub". Các row cũ
        /// của trang đó đều đã có chỗ riêng: Help ở nút "?" header, Console/Auto/Proxima là command
        /// console.*, "Show entry button" là command hub.entry (ConsoleController.Initialize()).
        private void OpenCommandTree()
        {
            panel.Show(CommandsPage.Root());
        }

        #region API

        public static ActionNode Add(Object owner, string path, string description, Action run)
        {
            return DebugRegistry.Register(owner, path,
                new ActionNode { Description = description, Invoke = _ => run(), Dismiss = DismissMode.ClosePanel });
        }

        public static ActionNode Add<T1>(Object owner, string path, string description, Action<T1> run, string name = null)
        {
            return DebugRegistry.Register(owner, path, new ActionNode
            {
                Description = description,
                Parameters = new[] { Parameter(run, 0, typeof(T1), name) },
                Invoke = args => run((T1)args[0]),
                Dismiss = DismissMode.ClosePanel,
            });
        }

        public static ActionNode Add<T1, T2>(Object owner, string path, string description, Action<T1, T2> run,
            string name1 = null, string name2 = null)
        {
            return DebugRegistry.Register(owner, path, new ActionNode
            {
                Description = description,
                Parameters = new[]
                {
                    Parameter(run, 0, typeof(T1), name1),
                    Parameter(run, 1, typeof(T2), name2),
                },
                Invoke = args => run((T1)args[0], (T2)args[1]),
                Dismiss = DismissMode.ClosePanel,
            });
        }

        public static ActionNode Add<T1, T2, T3>(Object owner, string path, string description, Action<T1, T2, T3> run,
            string name1 = null, string name2 = null, string name3 = null)
        {
            return DebugRegistry.Register(owner, path, new ActionNode
            {
                Description = description,
                Parameters = new[]
                {
                    Parameter(run, 0, typeof(T1), name1),
                    Parameter(run, 1, typeof(T2), name2),
                    Parameter(run, 2, typeof(T3), name3),
                },
                Invoke = args => run((T1)args[0], (T2)args[1], (T3)args[2]),
                Dismiss = DismissMode.ClosePanel,
            });
        }

        public static ActionNode Add<T1, T2, T3, T4>(Object owner, string path, string description,
            Action<T1, T2, T3, T4> run, string name1 = null, string name2 = null, string name3 = null, string name4 = null)
        {
            return DebugRegistry.Register(owner, path, new ActionNode
            {
                Description = description,
                Parameters = new[]
                {
                    Parameter(run, 0, typeof(T1), name1),
                    Parameter(run, 1, typeof(T2), name2),
                    Parameter(run, 2, typeof(T3), name3),
                    Parameter(run, 3, typeof(T4), name4),
                },
                Invoke = args => run((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]),
                Dismiss = DismissMode.ClosePanel,
            });
        }

        public static ValueNode AddValue<T>(Object owner, string path, string description,
            Func<T> get, Action<T> set, string name = null)
        {
            return DebugRegistry.Register(owner, path, new ValueNode
            {
                Description = description,
                Declared = typeof(T),
                Get = () => get(),
                Set = set == null ? null : v => set((T)v),
                Dismiss = DismissMode.Stay,
            });
        }

        public static FolderNode AddFolder(Object owner, string path, string description,
            Func<IEnumerable<DebugNode>> children, bool live = false)
        {
            return DebugRegistry.Register(owner, path,
                new FolderNode { Description = description, Children = children, Live = live });
        }

        public static void Remove(DebugNode node) => DebugRegistry.Remove(node);

        /// Nút repeat: chạy lại dòng lệnh qua đúng luồng của panel — xác nhận, dòng kết quả, Dismiss, ghi
        /// lệnh cuối. Command đã mất đăng ký thì xoá bản lưu, nút tự ẩn.
        internal static void Repeat(string line)
        {
            if (!instance) return;
            if (!DebugRegistry.Find(line, out var entry))
            {
                instance.panel.ShowResult($"Không còn command '{line}'.", true);
                DebugRegistry.ClearLastCommand();
                return;
            }

            var values = DebugRegistry.ArgumentsOf(line);
            if (!entry.Node.Confirm)
            {
                CommandsPage.RunNow(instance.panel, entry, values);
                return;
            }
            // Một chạm chạy thẳng `save.wipe` là tai nạn chờ sẵn: mở panel tới trang xác nhận.
            instance.panel.Show(CommandsPage.Root());
            CommandsPage.Run(instance.panel, entry, values);
        }

        /// Chạy coroutine chờ một awaitable. Ở trên DebugHub vì nó là MonoBehaviour sống suốt phiên —
        /// node do reflection sinh thì không có chỗ nào để chạy coroutine.
        ///
        /// Kết quả về **sau** khi DebugRegistry.Run đã trả nên nó vào console, không vào dòng kết quả
        /// của lần chạy đó — dòng kết quả bấm được để mở console.
        internal static void Await(object awaitable, string label)
        {
            if (awaitable == null) return;
            if (!instance)
            {
                // Không có hub (EditMode) thì không có chỗ chạy coroutine: in chính object, như lúc tắt chờ.
                UnityEngine.Debug.Log(DebugValues.ToText(awaitable));
                return;
            }

            instance.StartCoroutine(Awaitables.Wait(awaitable, (result, error) =>
            {
                if (error != null) UnityEngine.Debug.LogError($"{label}: {error.Message}");
                else UnityEngine.Debug.Log($"{label} xong: {DebugValues.ToText(result)}");
            }));
        }

        public static bool Execute(string line, out string message) => DebugRegistry.Execute(line, out message);

        public static bool Execute(string line)
        {
            var ok = Execute(line, out var message);
            if (!ok) UnityEngine.Debug.LogWarning(message);
            return ok;
        }

        /// Tên tham số lấy từ chính delegate nên chỗ đăng ký không phải gõ lại tên.
        private static DebugParameter Parameter(Delegate run, int index, Type type, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                var parameters = run.Method.GetParameters();
                name = index < parameters.Length ? parameters[index].Name : type.Name;
            }
            return new DebugParameter(name, type);
        }

        #endregion
    }
}
