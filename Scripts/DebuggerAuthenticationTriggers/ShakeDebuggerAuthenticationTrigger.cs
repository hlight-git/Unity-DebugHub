using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Hlight.Debug.Hub
{
    /// Lắc mạnh để gọi lại entry đã ẩn. RequiresAlreadyAuthenticated = true: chỉ có tác dụng khi máy
    /// đã xác thực rồi, không phải một cách để mở khoá lần đầu — lắc lúc chưa xác thực không làm gì.
    public class ShakeDebuggerAuthenticationTrigger : DebuggerAuthenticationTrigger
    {
        [SerializeField] private float shakeThreshold = 50f;

        public override bool RequiresAlreadyAuthenticated => true;

        public override bool IsPerformedTriggerAction()
        {
            return ShakeSqrMagnitude() >= shakeThreshold;
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        float ShakeSqrMagnitude()
        {
            var accelerometer = Accelerometer.current;
            if (accelerometer == null) return 0f;

            // Sensor của Input System mặc định bị disable, không bật thì đọc ra 0 mãi.
            if (!accelerometer.enabled) InputSystem.EnableDevice(accelerometer);

            return accelerometer.acceleration.ReadValue().sqrMagnitude;
        }
#else
        float ShakeSqrMagnitude()
        {
            return Input.acceleration.sqrMagnitude;
        }
#endif
    }
}
