using NUnit.Framework;
using UnityEngine.Networking;

namespace Hlight.Debug.Hub.Tests
{
    public class AutoUnlockTests
    {
        private const string TITLE = "<title>Zego Dashboard</title>";

        [Test]
        public void Page_MatchesOnlyOk200_WithTheMarker()
        {
            Assert.IsTrue(AutoUnlock.PageMatches(UnityWebRequest.Result.Success, 200, "<html>" + TITLE + "</html>", TITLE));
            Assert.IsFalse(AutoUnlock.PageMatches(UnityWebRequest.Result.Success, 200, "<title>Router</title>", TITLE),
                "máy khác trùng IP nội bộ không được tính");
            Assert.IsFalse(AutoUnlock.PageMatches(UnityWebRequest.Result.Success, 204, TITLE, TITLE));
            Assert.IsFalse(AutoUnlock.PageMatches(UnityWebRequest.Result.ProtocolError, 302, TITLE, TITLE),
                "redirect sang trang đăng nhập không được tính");
            Assert.IsFalse(AutoUnlock.PageMatches(UnityWebRequest.Result.ConnectionError, 0, null, TITLE));
            Assert.IsFalse(AutoUnlock.PageMatches(UnityWebRequest.Result.Success, 200, TITLE, ""),
                "chưa cấu hình chuỗi thì không bao giờ khớp");
        }

        /// Spec ② §3: vào mạng hợp lệ là mở khoá ngay, trừ iOS — chạm mạng cục bộ lúc mở app làm mọi người chơi
        /// thấy hộp xin quyền.
        [Test]
        public void ChecksAtLaunch_EverywhereButIos()
        {
            foreach (var platform in new[] { UnityEngine.RuntimePlatform.Android, UnityEngine.RuntimePlatform.WindowsPlayer,
                         UnityEngine.RuntimePlatform.WindowsEditor, UnityEngine.RuntimePlatform.OSXEditor })
                Assert.IsTrue(AutoUnlock.ChecksAtLaunch(platform), platform.ToString());
            // Các nền tảng này hỏi quyền mạng cục bộ lần đầu chạm LAN.
            foreach (var platform in new[] { UnityEngine.RuntimePlatform.IPhonePlayer, UnityEngine.RuntimePlatform.OSXPlayer,
                         UnityEngine.RuntimePlatform.tvOS, UnityEngine.RuntimePlatform.VisionOS })
                Assert.IsFalse(AutoUnlock.ChecksAtLaunch(platform), platform.ToString());
        }

        /// Không có trang nào (mảng rỗng, như component mới thêm) thì không gửi gì.
        [Test]
        public void Unconfigured_SendsNothing_AndNeverMatches()
        {
            var unlock = new AutoUnlock();
            var matched = false;
            Assert.IsFalse(unlock.Configured);
            Assert.IsFalse(unlock.Check(() => matched = true).MoveNext());
            Assert.IsFalse(matched);
        }
    }
}
