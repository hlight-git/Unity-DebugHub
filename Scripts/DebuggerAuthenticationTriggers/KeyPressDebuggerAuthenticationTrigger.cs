using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    public class KeyPressDebuggerAuthenticationTrigger : DebuggerAuthenticationTrigger
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // Project chạy Input System thuần thì UnityEngine.Input ném exception, phải đọc qua Keyboard.
        [SerializeField] private Key activeKey = Key.Backquote;

        public override bool IsPerformedTriggerAction()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[activeKey].wasReleasedThisFrame;
        }
#else
        [SerializeField] private KeyCode activeKeyCode = KeyCode.BackQuote;

        public override bool IsPerformedTriggerAction()
        {
            return Input.GetKeyUp(activeKeyCode);
        }
#endif
    }
}
