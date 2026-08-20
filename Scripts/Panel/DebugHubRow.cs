using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Refs của một row template. Phải nằm trên root của row: bản Instantiate ra sẽ trỏ vào
    /// con của chính nó, còn serialized ref của panel thì vẫn trỏ về template.
    public class DebugHubRow : MonoBehaviour
    {
        public Text label;
        public Button button;
        public Toggle toggle;
        public InputField input;

        /// Chữ phụ căn phải (số lượng command trong thư mục). Chỉ row nav có.
        public Text detail;

        /// Núm của switch: panel dịch sang phải khi bật. Không có núm thì trạng thái tắt chỉ là một
        /// thanh trống, nhìn không ra là switch.
        public RectTransform knob;
    }
}
