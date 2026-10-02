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
