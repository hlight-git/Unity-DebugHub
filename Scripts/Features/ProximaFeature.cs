using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bọc Proxima Inspector. Không cần MonoBehaviour: chỉ tạo một GameObject rồi bật/tắt canvas của nó.
    public class ProximaFeature
    {
        private readonly string password;
        private Canvas canvas;

        public ProximaFeature(string password)
        {
            this.password = password;
        }

#if PROXIMA
        public bool Supported => true;
#else
        public bool Supported => false;
#endif

        public bool Enabled
        {
            get => canvas && canvas.enabled;
            set
            {
#if PROXIMA
                if (canvas)
                {
                    canvas.enabled = value;
                    return;
                }
                if (!value) return;

                var container = new GameObject("ProximaContainer");
                var inspector = container.AddComponent<Proxima.ProximaInspector>();
                inspector.DisplayName = Application.productName;
                inspector.Password = password;
                inspector.InstantiateConnectUI = true;
                inspector.Run();
                canvas = container.GetComponentInChildren<Canvas>();
                Proxima.ProximaInspector.RegisterCommands<ProximaFeature>();
#endif
            }
        }

#if PROXIMA
        [Proxima.ProximaCommand("Custom", "exec")]
        public static void ExecuteCommandInDebugConsole(string command)
        {
            IngameDebugConsole.DebugLogConsole.ExecuteCommand(command);
        }
#endif
    }
}
