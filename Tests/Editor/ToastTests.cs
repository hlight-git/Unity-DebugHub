using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class ToastTests
    {
        [Test]
        public void Truncate_NeverCutsInsideARichTextTag()
        {
            var text = "ok <color=#E5484D>lỗi dài</color>";

            // Giới hạn rơi vào giữa "<color=#E5…" — nửa tag hiện ra thành chữ rác.
            var cut = DebugHubToast.Truncate(text, 10);

            Assert.AreEqual("ok …", cut);
        }

        [Test]
        public void Truncate_LeavesShortTextAlone()
        {
            Assert.AreEqual("ngắn", DebugHubToast.Truncate("ngắn", 240));
        }

        [Test]
        public void SafeBottomInset_ConvertsDevicePixelsToCanvasUnits()
        {
            Assert.AreEqual(80f, DebugHubToast.SafeBottomInset(1920f, 840f, 35f), 0.001f);
        }
    }
}
