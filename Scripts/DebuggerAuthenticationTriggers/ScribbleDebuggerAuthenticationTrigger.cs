using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    /// Nhấn trong một vùng màn hình cố định (triggerArea, toạ độ viewport chuẩn hoá 0-1 — giống việc
    /// bản gốc gán trên một Image ở vị trí nhất định, chỉ khác là so toạ độ trực tiếp thay vì raycast
    /// qua Graphic nên không cần thêm UI element vào scene), rồi kéo lung tung, rồi thả gần đúng chỗ
    /// bắt đầu: tính là trigger nếu tổng khoảng cách di chuyển trong lúc giữ vượt minDragDistance VÀ
    /// điểm thả cách điểm bắt đầu không quá maxStartEndDistance.
    public class ScribbleDebuggerAuthenticationTrigger : DebuggerAuthenticationTrigger
    {
        [SerializeField] private Rect triggerArea = new(0f, 0f, 1f, 1f);
        [SerializeField] private float minDragDistance = 3500f;
        [SerializeField] private float maxStartEndDistance = 10f;

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
                return accumulatedDistance > minDragDistance && Vector2.Distance(startPosition, lastPosition) <= maxStartEndDistance;
            }

            return false;
        }

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
