namespace Hlight.Debug.Hub
{
    public class ShowDebugObjects : ADebugOperation
    {
        public bool IsOn => toggle.isOn;
        protected override void Awake()
        {
            base.Awake();
            toggle.isOn = true;
        }

        protected override void OnToggleValueChanged(bool value)
        {
            foreach (var debugObject in DebugHub.InternalInstance.DebugObjects)
            {
                debugObject.gameObject.SetActive(value);
            }
        }
    }
}