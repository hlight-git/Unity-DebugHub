using System;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    [Serializable]
    public class StandaloneDebuggerAuthenticationTrigger : IDebuggerAuthenticationTrigger
    {
        [SerializeField] private KeyCode activeKeyCode = KeyCode.BackQuote;
        
        public bool IsPerformedTriggerAction()
        {
            return Input.GetKeyUp(activeKeyCode);
        }
    }
}