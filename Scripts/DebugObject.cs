using UnityEngine;

namespace Hlight.Debug.Hub
{
    public class DebugObject : MonoBehaviour
    {
        private void Awake()
        {
            if (DebugHub.RegisterDebugObject(this, out var active))
            {
                gameObject.SetActive(active);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            DebugHub.UnregisterDebugObject(this);
        }
    }
}