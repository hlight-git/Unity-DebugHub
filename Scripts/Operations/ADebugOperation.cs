using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public abstract class ADebugOperation : MonoBehaviour
    {
        [SerializeField] protected Toggle toggle;

        protected virtual void Awake()
        {
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
        
        protected abstract void OnToggleValueChanged(bool value);
    }
}