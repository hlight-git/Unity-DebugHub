using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bọc Proxima Inspector. Không cần MonoBehaviour: chỉ tạo một GameObject rồi bật/tắt canvas của nó.
    /// Toàn bộ phần phụ thuộc Proxima nằm sau define PROXIMA, project không có SDK thì Supported = false
    /// và root page không hiện toggle.
    public class ProximaFeature
    {
        private readonly string password;

        public ProximaFeature(string password)
        {
            this.password = password;
        }

#if PROXIMA
        private Canvas canvas;

        public bool Supported => true;

        public bool Enabled
        {
            get => canvas && canvas.enabled;
            set
            {
                if (canvas)
                {
                    canvas.enabled = value;
                    return;
                }
                if (!value) return;

                // Đăng ký command trước khi Run(): Proxima publish danh sách command lúc chạy,
                // đăng ký sau thì lệnh exec không xuất hiện ở phía remote.
                Proxima.ProximaInspector.RegisterCommands<ProximaFeature>();

                var container = new GameObject("ProximaContainer");
                var inspector = container.AddComponent<Proxima.ProximaInspector>();
                inspector.DisplayName = Application.productName;
                inspector.Password = password;
                inspector.InstantiateConnectUI = true;
                inspector.Run();
                canvas = container.GetComponentInChildren<Canvas>();
            }
        }

        /// Chạy qua registry của hub, không phải của IDC: cheat không còn đăng ký vào IDC nên
        /// DebugLogConsole.ExecuteCommand ở đây sẽ không thấy command nào của game.
        [Proxima.ProximaCommand("Custom", "exec")]
        public static void ExecuteCommandInDebugConsole(string command)
        {
            DebugCommands.Execute(command);
        }
#else
        public bool Supported => false;

        public bool Enabled
        {
            get => false;
            set { }
        }
#endif
    }
}
