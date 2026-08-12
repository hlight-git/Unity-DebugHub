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

        (Vector2 min, Vector2 max)? GetScreenPositionAnchors(ScreenPosition screenPosition) => screenPosition switch
        {
            ScreenPosition.TopLeft => (new(0, .8f), new(.2f, 1)),
            ScreenPosition.TopRight => (new(.8f, .8f), new(1, 1)),
            ScreenPosition.BotLeft => (new(0, 0), new(.2f, .2f)),
            ScreenPosition.BotRight => (new(.8f, 0), new(1, .2f)),
            _ => null,
        };

        ScreenPosition GetScreenPosition(Vector2 position)
        {
            Vector2 screenSize = new(Screen.width, Screen.height);

            foreach (ScreenPosition screenPosition in System.Enum.GetValues(typeof(ScreenPosition)))
            {
                (Vector2 min, Vector2 max)? anchors = GetScreenPositionAnchors(screenPosition);

                if (!anchors.HasValue)
                {
                    continue;
                }

                if (position.x > screenSize.x * anchors.Value.min.x &&
                    position.x < screenSize.x * anchors.Value.max.x &&
                    position.y > screenSize.y * anchors.Value.min.y &&
                    position.y < screenSize.y * anchors.Value.max.y)
                {
                    return screenPosition;
                }
            }

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
