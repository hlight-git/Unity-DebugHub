using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Vào trang hợp lệ như bản 2.x: mở khoá ngầm, bong bóng chờ cử chỉ (lắc / 4 góc) mới hiện.
    public class NetworkUnlockTests
    {
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;
        private DebugHubPanel panel;
        private DebugHub hub;
        private bool hadUnlock, hadEntry;
        private int savedUnlock, savedEntry;

        [SetUp]
        public void SetUp()
        {
            hadUnlock = PlayerPrefs.HasKey(HubAccess.UNLOCK_KEY);
            savedUnlock = PlayerPrefs.GetInt(HubAccess.UNLOCK_KEY);
            hadEntry = PlayerPrefs.HasKey(DebugHub.ENTRY_VISIBLE_KEY);
            savedEntry = PlayerPrefs.GetInt(DebugHub.ENTRY_VISIBLE_KEY);
            PlayerPrefs.DeleteKey(HubAccess.UNLOCK_KEY);
            PlayerPrefs.DeleteKey(DebugHub.ENTRY_VISIBLE_KEY);
            LogRecorder.Reset();
            panel = TestPanel.Build();
            hub = panel.transform.root.GetComponentInChildren<DebugHub>(true);
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            Restore(HubAccess.UNLOCK_KEY, hadUnlock, savedUnlock);
            Restore(DebugHub.ENTRY_VISIBLE_KEY, hadEntry, savedEntry);
        }

        private static void Restore(string key, bool had, int value)
        {
            if (had) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        private void Set(string field, object value) => typeof(DebugHub).GetField(field, PRIVATE).SetValue(hub, value);
        private object Get(string field) => typeof(DebugHub).GetField(field, PRIVATE).GetValue(hub);
        private void Matched() => typeof(DebugHub).GetMethod("OnNetworkMatched", PRIVATE).Invoke(hub, null);

        [Test]
        public void Match_UnlocksSilently_WithoutPoppingTheBubble()
        {
            Matched();
            Assert.IsTrue(HubAccess.ReadUnlocked());
            Assert.IsTrue(LogRecorder.Recording, "mở khoá là bắt đầu ghi log");
            // Không kiểm entry.Activating: Awake không chắc chạy trong EditMode nên Visible không làm gì, assert đó không
            // bao giờ đỏ. Bong bóng tự hiện thì phải đi qua RememberEntry(true) — PlayerPrefs bắt được.
            Assert.AreNotEqual(1, PlayerPrefs.GetInt(DebugHub.ENTRY_VISIBLE_KEY), "bong bóng chờ cử chỉ, phiên sau cũng không tự hiện");
        }

        /// Ô password đang mở = người dùng vừa làm cử chỉ: khớp thì đóng ô, như gõ đúng password.
        [Test]
        public void Match_WhileThePasswordBoxIsOpen_ClosesIt()
        {
            var input = (TMP_InputField)Get("authenticationInputField");
            input.gameObject.SetActive(true);
            Set("askingPassword", true);

            Matched();
            Assert.IsTrue(HubAccess.ReadUnlocked());
            Assert.IsFalse(input.gameObject.activeSelf);
            Assert.IsFalse((bool)Get("askingPassword"));
        }
    }
}
