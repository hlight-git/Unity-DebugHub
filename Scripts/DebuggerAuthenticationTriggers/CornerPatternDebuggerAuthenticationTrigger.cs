using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    public class CornerPatternDebuggerAuthenticationTrigger : DebuggerAuthenticationTrigger
    {
        enum ScreenPosition
        {
            Unknown,
            TopLeft,
            TopRight,
            BotLeft,
            BotRight,
        }

        /// Chỉ ba trạng thái mà state machine dưới cần, để phần đọc input (legacy vs Input System)
        /// không lan vào logic.
        enum TouchState
        {
            None,
            Held,
            Ended,
        }

        [SerializeField] private ScreenPosition[] triggerSteps =
        {
            ScreenPosition.TopLeft,
            ScreenPosition.BotRight,
            ScreenPosition.BotLeft,
            ScreenPosition.TopRight,
            ScreenPosition.TopLeft,
        };

        int validatedStepCount;

        /// Góc = 20% mỗi chiều. Tính thẳng, không duyệt Enum.GetValues: hàm này chạy mỗi frame người chơi
        /// đang giữ tay (bản store, chưa mở khoá), GetValues là một mảng + boxing mỗi lần.
        static ScreenPosition GetScreenPosition(Vector2 position)
        {
            var x = position.x / Screen.width;
            var y = position.y / Screen.height;
            var left = x < .2f;
            var right = x > .8f;
            if (y > .8f) return left ? ScreenPosition.TopLeft : right ? ScreenPosition.TopRight : ScreenPosition.Unknown;
            if (y < .2f) return left ? ScreenPosition.BotLeft : right ? ScreenPosition.BotRight : ScreenPosition.Unknown;
            return ScreenPosition.Unknown;
        }

        public override bool IsPerformedTriggerAction()
        {
            if (!TryReadPrimaryTouch(out Vector2 position, out TouchState state))
            {
                validatedStepCount = 0;
                return false;
            }

            if (state == TouchState.Held)
            {
                ScreenPosition touchingScreenPosition = GetScreenPosition(position);

                if (touchingScreenPosition == ScreenPosition.Unknown)
                {
                    return false;
                }

                if (validatedStepCount > 0 && touchingScreenPosition == triggerSteps[validatedStepCount - 1])
                {
                    return false;
                }

                if (validatedStepCount < triggerSteps.Length && touchingScreenPosition == triggerSteps[validatedStepCount])
                {
                    validatedStepCount++;
                    return false;
                }

                validatedStepCount = 0;
            }

            if (state == TouchState.Ended && validatedStepCount == triggerSteps.Length)
            {
                validatedStepCount = 0;
                return true;
            }

            return false;
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool TryReadPrimaryTouch(out Vector2 position, out TouchState state)
        {
            position = default;
            state = TouchState.None;

            var touch = Touchscreen.current?.primaryTouch;
            if (touch == null || !touch.press.isPressed && touch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.Ended)
            {
                return false;
            }

            position = touch.position.ReadValue();
            state = touch.phase.ReadValue() switch
            {
                UnityEngine.InputSystem.TouchPhase.Stationary => TouchState.Held,
                UnityEngine.InputSystem.TouchPhase.Moved => TouchState.Held,
                UnityEngine.InputSystem.TouchPhase.Ended => TouchState.Ended,
                _ => TouchState.None,
            };
            return true;
        }
#else
        bool TryReadPrimaryTouch(out Vector2 position, out TouchState state)
        {
            position = default;
            state = TouchState.None;

            if (Input.touchCount == 0) return false;

            Touch touch = Input.GetTouch(0);
            position = touch.position;
            state = touch.phase switch
            {
                TouchPhase.Stationary => TouchState.Held,
                TouchPhase.Moved => TouchState.Held,
                TouchPhase.Ended => TouchState.Ended,
                _ => TouchState.None,
            };
            return true;
        }
#endif
    }
}
