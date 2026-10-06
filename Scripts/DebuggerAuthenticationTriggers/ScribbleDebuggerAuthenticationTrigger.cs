using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    /// Nhấn trong một vùng màn hình cố định (triggerArea, toạ độ viewport chuẩn hoá 0-1 — giống việc
    /// bản gốc gán trên một Image ở vị trí nhất định, chỉ khác là so toạ độ trực tiếp thay vì raycast
    /// qua Graphic nên không cần thêm UI element vào scene), rồi kéo lung tung, rồi thả gần đúng chỗ
    /// bắt đầu: tính là trigger nếu tổng quãng kéo trong lúc giữ vượt minDragScreens VÀ điểm thả cách
    /// điểm bắt đầu không quá maxStartEndGap.
    ///
    /// Hai ngưỡng tính theo cạnh ngắn màn hình chứ không theo pixel: 10 px là 0,6 mm trên máy 1080p
    /// nhưng gấp đôi trên máy 720p, cùng một nét vẽ lúc được lúc không.
    public class ScribbleDebuggerAuthenticationTrigger : DebuggerAuthenticationTrigger
    {
        [SerializeField] private Rect triggerArea = new(0f, 0f, 1f, 1f);

        [Tooltip("Tổng quãng kéo tối thiểu, tính bằng số lần cạnh ngắn màn hình.")]
        [SerializeField, Min(0f)] private float minDragScreens = 3f;

        [Tooltip("Điểm thả cách điểm bắt đầu tối đa, tính bằng tỉ lệ cạnh ngắn màn hình (0,08 ≈ 5 mm trên điện thoại).")]
        [SerializeField, Range(0f, 0.5f)] private float maxStartEndGap = 0.08f;

        public override string Hint =>
            $"Vẽ nguệch ngoạc dài cỡ {minDragScreens.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} lần " +
            "cạnh ngắn màn hình, rồi thả tay gần chỗ bắt đầu.";

        private bool dragging;
        private float accumulatedDistance;
        private Vector2 startPosition;
        private Vector2 lastPosition;

        public override bool IsPerformedTriggerAction()
        {
            if (!TryReadPointer(out var position, out var pressed))
            {
                dragging = false;
                return false;
            }

            if (pressed && !dragging)
            {
                if (!IsInsideTriggerArea(position)) return false;
                dragging = true;
                accumulatedDistance = 0f;
                startPosition = position;
                lastPosition = position;
                return false;
            }

            if (pressed && dragging)
            {
                accumulatedDistance += Vector2.Distance(position, lastPosition);
                lastPosition = position;
                return false;
            }

            if (!pressed && dragging)
            {
                dragging = false;
                return Completed(accumulatedDistance, Vector2.Distance(startPosition, lastPosition),
                    Mathf.Min(Screen.width, Screen.height), minDragScreens, maxStartEndGap);
            }

            return false;
        }

        internal static bool Completed(float path, float gap, float shortSide, float minScreens, float maxGap) =>
            path > minScreens * shortSide && gap <= maxGap * shortSide;

        private bool IsInsideTriggerArea(Vector2 screenPosition)
        {
            var normalized = new Vector2(screenPosition.x / Screen.width, screenPosition.y / Screen.height);
            return triggerArea.Contains(normalized);
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static bool TryReadPointer(out Vector2 position, out bool pressed)
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                position = default;
                pressed = false;
                return false;
            }

            position = pointer.position.ReadValue();
            pressed = pointer.press.isPressed;
            return true;
        }
#else
        private static bool TryReadPointer(out Vector2 position, out bool pressed)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                position = touch.position;
                pressed = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return true;
            }

            if (Input.mousePresent)
            {
                position = Input.mousePosition;
                pressed = Input.GetMouseButton(0);
                return true;
            }

            position = default;
            pressed = false;
            return false;
        }
#endif
    }
}
