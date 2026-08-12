using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    /// Giữ rồi kéo lung tung trước khi thả: nếu tổng khoảng cách di chuyển trong lúc giữ vượt
    /// minDragDistance thì tính là trigger. Đọc con trỏ (chuột hoặc chạm) trực tiếp mỗi frame, không
    /// qua EventSystem/Graphic, nên không cần thêm UI element nào vào scene — chỉ cần add component.
    public class ScribbleDebuggerAuthenticationTrigger : DebuggerAuthenticationTrigger
    {
        [SerializeField] private float minDragDistance = 3500f;

        private bool dragging;
        private float accumulatedDistance;
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
                dragging = true;
                accumulatedDistance = 0f;
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
                return accumulatedDistance > minDragDistance;
            }

            return false;
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
