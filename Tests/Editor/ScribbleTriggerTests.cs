using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class ScribbleTriggerTests
    {
        /// Cùng một nét (tính theo cạnh ngắn màn hình) cho cùng kết quả trên máy 720p và 1440p. Ngưỡng pixel cố định
        /// cũ bắt điểm thả trúng trong 10 px ≈ 0,6 mm trên máy 1080p: gần như không làm được.
        [Test]
        public void Completed_ScalesWithTheScreen()
        {
            foreach (var side in new[] { 720f, 1080f, 1440f })
            {
                Assert.IsTrue(ScribbleDebuggerAuthenticationTrigger.Completed(3.5f * side, 0.05f * side, side, 3f, 0.08f), $"{side}");
                Assert.IsFalse(ScribbleDebuggerAuthenticationTrigger.Completed(2f * side, 0.05f * side, side, 3f, 0.08f), "kéo chưa đủ");
                Assert.IsFalse(ScribbleDebuggerAuthenticationTrigger.Completed(3.5f * side, 0.2f * side, side, 3f, 0.08f), "thả xa điểm đầu");
            }
        }
    }
}
