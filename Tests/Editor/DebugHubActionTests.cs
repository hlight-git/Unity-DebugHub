using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    /// Trigger action (gõ 4 góc / lắc / phím) là cửa duy nhất để entry xuất hiện lại: đã xác thực thì
    /// hiện entry, chưa thì mở ô password. Nếu bỏ cửa này thì toggle "Show entry button" không giữ
    /// được sau khi đóng panel.
    public class DebugHubActionTests
    {
        private static DebugHubAction Decide(bool panelOpen, bool entryVisible, bool processing, bool authenticated, bool triggered)
        {
            return DebugHub.DecideAction(panelOpen, entryVisible, processing, authenticated, () => triggered);
        }

        [Test]
        public void Authenticated_ShowsEntry_OnlyWhenTriggerPerformed()
        {
            Assert.AreEqual(DebugHubAction.None,
                Decide(panelOpen: false, entryVisible: false, processing: false, authenticated: true, triggered: false),
                "entry must stay hidden until the trigger action is performed again");

            Assert.AreEqual(DebugHubAction.ShowEntry,
                Decide(panelOpen: false, entryVisible: false, processing: false, authenticated: true, triggered: true));
        }

        [Test]
        public void NotAuthenticated_AsksPassword_OnTrigger()
        {
            Assert.AreEqual(DebugHubAction.AskPassword,
                Decide(panelOpen: false, entryVisible: false, processing: false, authenticated: false, triggered: true));
        }

        [Test]
        public void DoesNothing_WhileEntryVisible_PanelOpen_OrWaitingForPassword()
        {
            Assert.AreEqual(DebugHubAction.None,
                Decide(panelOpen: false, entryVisible: true, processing: false, authenticated: true, triggered: true),
                "entry already visible");

            Assert.AreEqual(DebugHubAction.None,
                Decide(panelOpen: true, entryVisible: false, processing: false, authenticated: true, triggered: true),
                "panel is open");

            Assert.AreEqual(DebugHubAction.None,
                Decide(panelOpen: false, entryVisible: false, processing: true, authenticated: false, triggered: true),
                "password field is already up");
        }

        /// Trigger trên mobile có state machine theo từng touch, hỏi nó khi đang không cần sẽ ăn mất
        /// bước người dùng đang gõ.
        [Test]
        public void DoesNotPollTrigger_WhenThereIsNothingToDo()
        {
            var polled = 0;

            DebugHub.DecideAction(panelOpen: true, entryVisible: false, processing: false, authenticated: true,
                triggerPerformed: () => { polled++; return true; });
            DebugHub.DecideAction(panelOpen: false, entryVisible: true, processing: false, authenticated: true,
                triggerPerformed: () => { polled++; return true; });
            DebugHub.DecideAction(panelOpen: false, entryVisible: false, processing: true, authenticated: true,
                triggerPerformed: () => { polled++; return true; });

            Assert.AreEqual(0, polled, "the trigger must not be polled when the result cannot be used");
        }

        [Test]
        public void NullTrigger_IsHandled()
        {
            Assert.AreEqual(DebugHubAction.None,
                DebugHub.DecideAction(false, false, false, true, null));
        }
    }
}
