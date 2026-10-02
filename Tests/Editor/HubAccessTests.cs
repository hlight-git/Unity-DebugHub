using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class HubAccessTests
    {
        private bool hadKey;
        private int savedValue;

        [SetUp]
        public void SetUp()
        {
            hadKey = PlayerPrefs.HasKey(HubAccess.UNLOCK_KEY);
            savedValue = PlayerPrefs.GetInt(HubAccess.UNLOCK_KEY);
            PlayerPrefs.DeleteKey(HubAccess.UNLOCK_KEY);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadKey) PlayerPrefs.SetInt(HubAccess.UNLOCK_KEY, savedValue);
            else PlayerPrefs.DeleteKey(HubAccess.UNLOCK_KEY);
        }

        /// Máy đã mở khoá ở bản cũ phải vẫn mở: cùng key, cùng giá trị 2.
        [Test]
        public void Unlock_IsRememberedUnderTheOldKey()
        {
            Assert.IsFalse(HubAccess.ReadUnlocked());
            HubAccess.SaveUnlocked();
            Assert.IsTrue(HubAccess.ReadUnlocked());
            Assert.AreEqual(2, PlayerPrefs.GetInt("DebugHub.AuthenticationState"));
        }

        [Test]
        public void Record_WhenUnlockedOrInternal_Only()
        {
            Assert.IsTrue(HubAccess.MayRecord(true, false));
            Assert.IsTrue(HubAccess.MayRecord(false, true));
            Assert.IsFalse(HubAccess.MayRecord(false, false));
        }

        /// App Tester có thể tự cài (installing) hoặc nhờ trình cài hệ thống (initiating).
        [Test]
        public void AppTester_MatchesEitherField()
        {
            Assert.IsTrue(InstallSource.IsAppTester(InstallSource.APP_TESTER, "com.google.android.packageinstaller"));
            Assert.IsTrue(InstallSource.IsAppTester(null, InstallSource.APP_TESTER));
            Assert.IsFalse(InstallSource.IsAppTester("com.android.vending", "com.android.vending"));
            Assert.IsFalse(InstallSource.IsAppTester(null, null));
        }

        [Test]
        public void Editor_IsInternal()
        {
            Assert.IsTrue(InstallSource.IsInternal);
        }
    }
}
