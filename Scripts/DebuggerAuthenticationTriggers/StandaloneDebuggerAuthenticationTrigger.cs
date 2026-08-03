using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    [Serializable]
    public class StandaloneDebuggerAuthenticationTrigger : IDebuggerAuthenticationTrigger
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // Project chạy Input System thuần thì UnityEngine.Input ném exception, phải đọc qua Keyboard.
        [SerializeField] private Key activeKey = Key.Backquote;

        public bool IsPerformedTriggerAction()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[activeKey].wasReleasedThisFrame;
        }
#else
        [SerializeField] private KeyCode activeKeyCode = KeyCode.BackQuote;

        public bool IsPerformedTriggerAction()
        {
            return Input.GetKeyUp(activeKeyCode);
        }
#endif
    }
}
