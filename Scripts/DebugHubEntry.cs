using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class DebugHubEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private FloatingBubble floatingBubble;

        public event Action Clicked;

        public bool Activating
        {
            get => button.gameObject.activeInHierarchy;
            set
            {
                if (value == button.gameObject.activeInHierarchy) return;
                button.gameObject.SetActive(value);
            }
        }

        private void Awake()
        {
            button.onClick.AddListener(() =>
            {
                if (!floatingBubble.SuppressClick) Clicked?.Invoke();
            });
            floatingBubble.DragStateChanged += value => button.enabled = !value;
            floatingBubble.Dismissed += () => DebugHub.Visible = false;
        }
    }
}
