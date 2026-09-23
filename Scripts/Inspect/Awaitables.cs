using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hlight.Debug.Hub
{
    /// Nhận biết và chờ một giá trị awaitable **theo mẫu của ngôn ngữ**, không theo kiểu cụ thể:
    /// có `GetAwaiter()` trả về thứ vừa `INotifyCompletion` vừa có `IsCompleted` và `GetResult()`.
    ///
    /// Nhờ vậy `Task`, `ValueTask`, `UniTask` và awaitable tự viết đều vào, mà package không phải
    /// tham chiếu UniTask hay bất cứ gì.
    ///
    /// Ngoài phạm vi: `IEnumerator` (coroutine Unity) không theo mẫu này.
    public static class Awaitables
    {
        private const BindingFlags PUBLIC = BindingFlags.Public | BindingFlags.Instance;

        public static bool IsAwaitable(Type type)
        {
            if (type == null || type == typeof(void)) return false;

            var getAwaiter = type.GetMethod("GetAwaiter", PUBLIC, null, Type.EmptyTypes, null);
            if (getAwaiter == null) return false;

            var awaiter = getAwaiter.ReturnType;
            return typeof(INotifyCompletion).IsAssignableFrom(awaiter) &&
                   awaiter.GetProperty("IsCompleted", PUBLIC) != null &&
                   awaiter.GetMethod("GetResult", PUBLIC, null, Type.EmptyTypes, null) != null;
        }

        /// Poll `IsCompleted` mỗi frame rồi gọi `GetResult()`. Poll chứ không `OnCompleted`: chạy
        /// tiếp ở main thread là điều kiện để in log và đụng UI, mà `OnCompleted` của một awaiter
        /// bất kỳ không hứa điều đó.
        ///
        /// Trần mặc định 60 giây: awaitable không bao giờ xong (UniTask chờ một event không tới) thì
        /// coroutine này poll reflection mỗi frame, mãi mãi, im lặng.
        public static IEnumerator Wait(object awaitable, Action<object, Exception> done, float timeoutSeconds = 60f)
        {
            object awaiter;
            PropertyInfo completed;
            MethodInfo result;
            try
            {
                awaiter = awaitable.GetType().GetMethod("GetAwaiter", PUBLIC, null, Type.EmptyTypes, null)
                    .Invoke(awaitable, null);
                completed = awaiter.GetType().GetProperty("IsCompleted", PUBLIC);
                result = awaiter.GetType().GetMethod("GetResult", PUBLIC, null, Type.EmptyTypes, null);
            }
            catch (Exception exception)
            {
                done(null, exception.InnerException ?? exception);
                yield break;
            }

            var deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;
            while (!(bool)completed.GetValue(awaiter))
            {
                if (UnityEngine.Time.realtimeSinceStartup >= deadline)
                {
                    done(null, new Exception($"quá hạn {timeoutSeconds}s"));
                    yield break;
                }
                yield return null;
            }

            object value = null;
            Exception failure = null;
            try { value = result.Invoke(awaiter, null); }
            catch (Exception exception) { failure = exception.InnerException ?? exception; }

            done(value, failure);
        }
    }
}
