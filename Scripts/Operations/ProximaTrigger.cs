using UnityEngine;

namespace Hlight.Debug.Hub
{
    public class ProximaTrigger : ADebugOperation
    {
        private Canvas proximaCanvas;

        protected override void Awake()
        {
#if PROXIMA
            base.Awake();
            Proxima.ProximaInspector.RegisterCommands<ProximaTrigger>();
            return;
#endif
            gameObject.SetActive(false);
        }
        
#if PROXIMA
        [Proxima.ProximaCommand("Custom", "exec")]
        public static void ExecuteCommandInDebugConsole(string command)
        {
            IngameDebugConsole.DebugLogConsole.ExecuteCommand(command);
        }
#endif
        
        protected override void OnToggleValueChanged(bool value)
        {
#if PROXIMA
            if (proximaCanvas)
            {
                proximaCanvas.enabled = value;
                return;
            }

            if (value)
            {
                var proximaGameObject = new GameObject("ProximaContainer");
                var inspector = proximaGameObject.AddComponent<Proxima.ProximaInspector>();
                inspector.DisplayName = Application.productName;
                inspector.Password = DebugHub.InternalInstance.Password;
                inspector.InstantiateConnectUI = true;
                inspector.Run();
                proximaCanvas = proximaGameObject.GetComponentInChildren<Canvas>();
            }
#endif
        }
    }
}