using UnityEngine;

namespace Hlight.Debug.Hub
{
    public class ShowDebugHubEntry : ADebugOperation
    {
        [SerializeField] private DebugHubEntry inGameDebuggerEntry;

        private void OnEnable()
        {
            toggle.isOn = inGameDebuggerEntry.Activating;
        }

        protected override void OnToggleValueChanged(bool value)
        {
            inGameDebuggerEntry.Activating = value;
        }
    }
}