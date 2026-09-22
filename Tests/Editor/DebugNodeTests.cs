using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class DebugNodeTests
    {
        [Test]
        public void ActionNode_ClosesPanelByDefault_ValueNodeStays()
        {
            var action = Node.Action("run", () => { });
            var value = Node.Value("x", () => 1);

            Assert.AreEqual(DismissMode.ClosePanel, action.Dismiss);
            Assert.AreEqual(DismissMode.Stay, value.Dismiss);
        }

        [Test]
        public void ShowsResult_FollowsDismiss_UnlessOverridden()
        {
            Assert.IsFalse(Node.Action("run", () => { }).ShowsResult);
            Assert.IsTrue(Node.Action("run", () => { }).Stays().ShowsResult);
            Assert.IsTrue(Node.Action("run", () => { }).Reports().ShowsResult);
            Assert.IsFalse(Node.Action("run", () => { }).Stays().Silent().ShowsResult);
        }

        [Test]
        public void FluentHelpers_KeepConcreteType()
        {
            ValueNode value = Node.Value("x", () => 1).Confirms();
            Assert.IsTrue(value.Confirm);
        }

        [Test]
        public void Section_IsInlineFolder()
        {
            var section = Node.Section("Spec", new DebugNode[] { Node.Text("a") });

            Assert.IsTrue(section.Inline);
            Assert.AreEqual("Spec", section.Label);
            Assert.AreEqual(1, System.Linq.Enumerable.Count(section.Children()));
        }

        [Test]
        public void ValueNode_ReadsDeclaredTypeFromGetter_NotFromCurrentValue()
        {
            object boxed = 7;
            var node = Node.Value<object>("x", () => boxed);

            Assert.AreEqual(typeof(object), node.Declared);
            Assert.AreEqual(7, node.Get());
        }
    }
}
