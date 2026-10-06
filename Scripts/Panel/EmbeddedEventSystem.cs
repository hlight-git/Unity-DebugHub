using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

namespace Hlight.Debug.Hub
{
    /// Prefab mang sẵn một EventSystem (tắt) để scene không có EventSystem vẫn gõ được password; chỉ bật
    /// nó khi scene chưa có cái nào. Thay EventSystemHandler của IngameDebugConsole.
    [DefaultExecutionOrder(1000)]
    public class EmbeddedEventSystem : MonoBehaviour
    {
        [SerializeField] private GameObject embeddedEventSystem;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private void Awake()
        {
            // Project chỉ bật Input System thì module cũ ném mỗi frame.
            if (embeddedEventSystem.TryGetComponent<StandaloneInputModule>(out var legacy))
            {
                DestroyImmediate(legacy);
                embeddedEventSystem.AddComponent<InputSystemUIInputModule>();
            }
        }
#endif

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            ActivateIfNeeded();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            embeddedEventSystem.SetActive(false);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Recheck();

        /// Gỡ scene cũng phải hỏi lại: scene vừa gỡ có thể mang theo EventSystem của game, hoặc cái nhúng đang là cái
        /// duy nhất — chỉ tắt là không còn EventSystem nào, không bấm được gì tới lần load scene sau.
        private void OnSceneUnloaded(Scene scene) => Recheck();

        /// Tắt trước rồi mới hỏi: scene mới mang EventSystem riêng thì EventSystem.current là cái của scene.
        private void Recheck()
        {
            embeddedEventSystem.SetActive(false);
            ActivateIfNeeded();
        }

        private void ActivateIfNeeded()
        {
            if (!EventSystem.current) embeddedEventSystem.SetActive(true);
        }
    }
}
