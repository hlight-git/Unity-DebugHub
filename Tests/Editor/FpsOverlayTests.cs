using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    /// Ô FPS (`view.fps`): đếm theo cửa sổ, FPS nhắm tới để tô màu.
    public class FpsOverlayTests
    {
        [Test]
        public void Meter_ReportsOnlyWhenAWindowEnds()
        {
            var meter = new FpsMeter();
            // Bước 0,03 s để tổng không rơi đúng vào 0,5 s (sai số float quyết định cửa sổ đóng ở khung nào).
            for (var i = 0; i < 16; i++) Assert.IsFalse(meter.Add(0.03f));
            Assert.IsTrue(meter.Add(0.03f));
            Assert.AreEqual(33, meter.Fps); // 17 khung trong 0,51 s
        }

        [Test]
        public void Meter_WorstIsTheSlowestFrameOfTheWindow_ThenResets()
        {
            var meter = new FpsMeter();
            meter.Add(0.1f);
            meter.Add(0.25f);
            Assert.IsTrue(meter.Add(0.2f));
            Assert.AreEqual(250f, meter.WorstMs, 0.01f);
            Assert.AreEqual(5, meter.Fps); // 3 khung trong 0,55 s

            for (var i = 0; i < 8; i++) Assert.IsFalse(meter.Add(0.06f));
            Assert.IsTrue(meter.Add(0.06f));
            Assert.AreEqual(60f, meter.WorstMs, 0.01f);
        }

        [TestCase(-1, 0, true, 120.0, ExpectedResult = 30)]   // mobile mặc định 30
        [TestCase(60, 0, true, 120.0, ExpectedResult = 60)]
        [TestCase(120, 0, true, 60.0, ExpectedResult = 60)]   // mobile không vượt tần số màn hình
        [TestCase(60, 1, true, 120.0, ExpectedResult = 60)]   // mobile bỏ qua vSyncCount
        [TestCase(-1, 0, false, 144.0, ExpectedResult = 144)]
        [TestCase(30, 0, false, 144.0, ExpectedResult = 30)]
        [TestCase(30, 2, false, 144.0, ExpectedResult = 72)]  // desktop: vSync thắng targetFrameRate
        [TestCase(-1, 0, false, 0.0, ExpectedResult = 60)]    // không đọc được tần số
        public int Target_FollowsUnityFrameRateRules(int targetFrameRate, int vSync, bool mobile, double refresh) =>
            FpsOverlay.TargetFps(targetFrameRate, vSync, mobile, refresh);

        [TestCase(60, 60, ExpectedResult = TextStyle.Good)]
        [TestCase(54, 60, ExpectedResult = TextStyle.Good)]
        [TestCase(53, 60, ExpectedResult = TextStyle.Warn)]
        [TestCase(36, 60, ExpectedResult = TextStyle.Warn)]
        [TestCase(35, 60, ExpectedResult = TextStyle.Bad)]
        public TextStyle Grade_ByShareOfTarget(int fps, int target) => FpsOverlay.Grade(fps, target);
    }
}
