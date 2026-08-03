using System;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    [Serializable]
    public class MobileDeviceDebuggerAuthenticationTrigger : IDebuggerAuthenticationTrigger
    {
        enum ScreenPosition
        {
            Unknown,
            TopLeft,
            TopRight,
            BotLeft,
            BotRight,
        }
        
        [SerializeField] private float shakeThreshold = 50f;
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

        public bool IsPerformedTriggerAction()
        {
            if (Input.acceleration.sqrMagnitude >= shakeThreshold)
            {
                return true;
            }

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
                {
                    ScreenPosition touchingScreenPosition = GetScreenPosition(touch.position);

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

                if (touch.phase == TouchPhase.Ended && validatedStepCount == triggerSteps.Length)
                {
                    validatedStepCount = 0;
                    return true;
                }
            }
            else
            {
                validatedStepCount = 0;
            }
            return false;
        }
    }
}