using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    public class DebugHubEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject mainMenuGo;
        [SerializeField] private Button closeMainMenuButton;
        [SerializeField] private FloatingBubble floatingBubble;

        public bool Activating
        {
            get => button.gameObject.activeInHierarchy;
            set
            {
                if (value == button.gameObject.activeInHierarchy) return;
                button.gameObject.SetActive(value);
            }
        }
        public bool IsMainMenuActivating => mainMenuGo.activeInHierarchy;

        private void Awake()
        {
            button.onClick.AddListener(OpenMainMenu);
            closeMainMenuButton.onClick.AddListener(CloseMainMenu);
            floatingBubble.DragStateChanged += value => button.enabled = !value;
        }

        private void OpenMainMenu()
        {
            mainMenuGo.SetActive(true);
        }

        private void CloseMainMenu()
        {
            mainMenuGo.SetActive(false);
        }
    }
}