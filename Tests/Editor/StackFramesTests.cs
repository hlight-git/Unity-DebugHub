using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class StackFramesTests
    {
        private const string LOG_STACK =
            "UnityEngine.Debug:Log (object)\n" +
            "Log:Info (object[]) (at ./Packages/com.hlight.logging/Runtime/Log.cs:34)\n" +
            "Harvest.Gameplay.ItemPiece:OnPointerUp (UnityEngine.EventSystems.PointerEventData) (at Assets/0_DevRoot/Scripts/ItemPiece.cs:212)\n" +
            "UnityEngine.EventSystems.ExecuteEvents:Execute (UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.BaseEventData) (at ./Library/PackageCache/com.unity.ugui/Runtime/EventSystem/ExecuteEvents.cs:58)\n";

        [Test]
        public void Caller_SkipsDebugAndTheLoggingWrapper()
        {
            Assert.AreEqual("ItemPiece.OnPointerUp:212", StackFrames.Caller(LOG_STACK));
        }

        [Test]
        public void Caller_ReadsExceptionFormat()
        {
            Assert.AreEqual("TrayController.TryPush:61",
                StackFrames.Caller("Harvest.Gameplay.TrayController.TryPush (Harvest.ItemPiece p) (at Assets/TrayController.cs:61)"));
        }

        /// IL2CPP release mặc định chỉ có tên method, không có `(at …)`.
        [Test]
        public void Caller_Il2CppWithoutLine()
        {
            Assert.AreEqual("ItemPiece.OnPointerUp", StackFrames.Caller("Harvest.ItemPiece:OnPointerUp(PointerEventData)"));
        }

        /// Frame engine đứng trước frame game (Instantiate gọi vào code game): "nơi gọi" là frame game.
        [Test]
        public void Caller_IsTheFirstGameFrame_NotTheFirstNonLoggingFrame()
        {
            Assert.AreEqual("Spawner.Spawn:9", StackFrames.Caller(
                "UnityEngine.Object:Instantiate (UnityEngine.Object)\n" +
                "Harvest.Spawner:Spawn () (at Assets/Spawner.cs:9)"));
        }

        [Test]
        public void Caller_WithOnlyEngineFrames_FallsBackToTheFirstOne()
        {
            Assert.AreEqual("Object.Instantiate", StackFrames.Caller("UnityEngine.Object:Instantiate (UnityEngine.Object)"));
        }

        [Test]
        public void Caller_NoStack_IsNull()
        {
            Assert.IsNull(StackFrames.Caller(""));
            Assert.IsNull(StackFrames.Caller(null));
        }

        [Test]
        public void Split_MarksEngineAndLoggingFramesAsNotGame()
        {
            var frames = StackFrames.Split(LOG_STACK);
            Assert.AreEqual(4, frames.Count);
            Assert.IsFalse(frames[0].Game);
            Assert.IsFalse(frames[1].Game);
            Assert.IsTrue(frames[2].Game);
            Assert.AreEqual("ItemPiece.cs:212", frames[2].Location);
            StringAssert.StartsWith("ItemPiece.OnPointerUp", frames[2].Method);
            Assert.IsFalse(frames[3].Game);
        }

        [Test]
        public void Method_ShortensArgumentTypes_InLogFormat()
        {
            var frame = StackFrames.Split(LOG_STACK)[2];
            Assert.AreEqual("ItemPiece.OnPointerUp (PointerEventData)", frame.Method);
            // Chỉ phần hiển thị rút gọn: tên đầy đủ và nơi gọi giữ nguyên.
            Assert.AreEqual("Harvest.Gameplay.ItemPiece:OnPointerUp", frame.Qualified);
            Assert.AreEqual("ItemPiece.OnPointerUp:212", frame.Caller);
            Assert.AreEqual("ItemPiece.cs:212", frame.Location);
        }

        [Test]
        public void Method_ShortensArgumentTypes_InExceptionFormat()
        {
            var frame = StackFrames.Split(
                "Harvest.Gameplay.TrayController.TryPush (Harvest.Gameplay.ItemPiece p) (at Assets/TrayController.cs:61)")[0];
            Assert.AreEqual("TrayController.TryPush (ItemPiece p)", frame.Method);
            Assert.AreEqual("TrayController.TryPush:61", frame.Caller);
        }

        /// Hai đối số: kiểu rút gọn và có chỗ ngắt dòng (U+200B) ngay sau dấu phẩy.
        [Test]
        public void Method_WithTwoArguments_CanWrapAfterTheComma()
        {
            var frame = StackFrames.Split(LOG_STACK)[3];
            Assert.AreEqual("ExecuteEvents.Execute (IPointerUpHandler,\u200BBaseEventData)", frame.Method);
            StringAssert.Contains(",\u200B", frame.Method);
        }
    }
}
