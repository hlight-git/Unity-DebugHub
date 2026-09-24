using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Proxima Inspector như một plugin bật/tắt. Tìm bằng tên nên package không tham chiếu Proxima:
    /// project không cài thì Supported = false và không có row.
    ///
    /// Tắt = Destroy cả container. Chỉ ẩn canvas thì server vẫn chạy, máy vẫn mở cho người cùng mạng.
    internal sealed class ProximaFeature
    {
        // ponytail: tên assembly-qualified, tra thẳng không quét domain — chạy lúc boot trên mọi máy.
        // Stripping Medium/High có thể strip type chỉ được gọi qua reflection; lúc đó thêm link.xml qua
        // IUnityLinkerProcessor.
        private static readonly Type Inspector = Type.GetType("Proxima.ProximaInspector, Proxima");

        private GameObject container;

        public static bool Supported => Inspector != null;

        /// Mật khẩu kết nối, mới mỗi phiên: điều khiển máy từ xa thì phải đang nhìn thấy máy.
        public string Pin { get; } = new System.Random().Next(100000, 1000000).ToString();

        public bool Enabled
        {
            get => container;
            set
            {
                if (value == Enabled) return;
                if (!value)
                {
                    Object.Destroy(container);
                    container = null;
                    return;
                }

                container = new GameObject("ProximaContainer");
                Object.DontDestroyOnLoad(container);
                try
                {
                    var inspector = container.AddComponent(Inspector);
                    Set(inspector, "DisplayName", Application.productName);
                    Set(inspector, "Password", Pin);
                    Set(inspector, "InstantiateConnectUI", true);
                    Inspector.GetMethod("Run", Type.EmptyTypes).Invoke(inspector, null);
                }
                catch
                {
                    // Member bị đổi tên/strip: container còn sống thì Enabled báo bật mà không có server nào.
                    Object.Destroy(container);
                    container = null;
                    throw;
                }
            }
        }

        private static void Set(object target, string name, object value)
        {
            var property = Inspector.GetProperty(name);
            if (property != null) { property.SetValue(target, value); return; }

            var field = Inspector.GetField(name) ?? throw new MissingMemberException(Inspector.FullName, name);
            field.SetValue(target, value);
        }
    }
}
