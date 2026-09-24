using NUnit.Framework;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlight.Debug.Hub.Tests
{
    public class FloatingBubbleTests
    {
        private GameObject instance;
        private GameObject events;
        private int edgeBackup;
        private float alongBackup;

        [SetUp]
        public void SetUp()
        {
            edgeBackup = PlayerPrefs.GetInt("DebugHub.BubbleEdge", -1);
            alongBackup = PlayerPrefs.GetFloat("DebugHub.BubbleAlong", -1f);
            events = new GameObject("events", typeof(EventSystem));
        }

        [TearDown]
        public void TearDown()
        {
            if (instance) Object.DestroyImmediate(instance);
            Object.DestroyImmediate(events);
            if (edgeBackup < 0) PlayerPrefs.DeleteKey("DebugHub.BubbleEdge");
            else PlayerPrefs.SetInt("DebugHub.BubbleEdge", edgeBackup);
            if (alongBackup < 0f) PlayerPrefs.DeleteKey("DebugHub.BubbleAlong");
            else PlayerPrefs.SetFloat("DebugHub.BubbleAlong", alongBackup);
        }

        [Test]
        public void DraggingOntoTheTarget_CapturesTheBubble_AndReleasingDismisses()
        {
            var bubble = BuildBubble(FloatingBubble.Edge.Left);
            var target = instance.GetComponentsInChildren<RectTransform>(true)
                .First(item => item.name == "Bubble Dismiss");
            Assert.IsFalse(target.gameObject.activeSelf);

            var dismissed = false;
            bubble.Dismissed += () => dismissed = true;
            var data = PointerAt((RectTransform)bubble.transform);
            bubble.OnBeginDrag(data);
            Assert.IsTrue(target.gameObject.activeSelf);
            data.position = RectTransformUtility.WorldToScreenPoint(null, target.position);
            bubble.OnDrag(data);
            bubble.OnEndDrag(data);

            Assert.IsTrue(dismissed);
            Assert.IsTrue(bubble.SuppressClick);
            Assert.IsFalse(target.gameObject.activeSelf);
        }

        /// Kéo nhanh tới chỗ, dừng lại rồi mới nhấc tay: quãng đứng yên nằm trong cửa sổ đo nên vận tốc là 0.
        [Test]
        public void FingerRestingBeforeLift_HasNoVelocity()
        {
            var bubble = BuildBubble(FloatingBubble.Edge.Left);
            bubble.Sample(Vector2.zero, 10f);
            bubble.Sample(new Vector2(200f, 0f), 10.05f);
            Assert.AreEqual(Vector2.zero, bubble.VelocityAt(new Vector2(200f, 0f), 10.6f));
            Assert.AreEqual(4000f, bubble.VelocityAt(new Vector2(200f, 0f), 10.05f).x, 1f);
        }

        /// Một luật chiếu đà thay cho mọi ngưỡng: đà nhỏ ở lại mép gần, đà lớn sang mép kia, y dừng ở điểm chiếu.
        [Test]
        public void Release_DocksToTheSideOfTheProjectedRestPoint()
        {
            var bubble = BuildBubble(FloatingBubble.Edge.Left);
            var rect = (RectTransform)bubble.transform;
            var start = rect.anchoredPosition;

            bubble.Release(new Vector2(800f, 0f));
            Assert.AreEqual(FloatingBubble.Edge.Left, bubble.DockedEdge, "đà nhẹ không qua nổi nửa màn hình");

            bubble.Release(new Vector2(4800f, 300f));
            Assert.AreEqual(FloatingBubble.Edge.Right, bubble.DockedEdge, "hất mạnh sang phải");
            var target = GetPrivate<Vector2>(bubble, "target");
            Assert.AreEqual(start.y + 300f * GetPrivate<float>(bubble, "momentum"), target.y, 1f);

            var before = rect.anchoredPosition;
            typeof(FloatingBubble).GetMethod("StepSpring", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bubble, new object[] { 1f / 60f });
            Assert.Greater(rect.anchoredPosition.x, before.x, "lò xo nhận nguyên đà tay, chuyển động nối liền cú thả");
        }

        private FloatingBubble BuildBubble(FloatingBubble.Edge edge)
        {
            PlayerPrefs.SetInt("DebugHub.BubbleEdge", (int)edge);
            PlayerPrefs.SetFloat("DebugHub.BubbleAlong", 0.5f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab");
            instance = Object.Instantiate(prefab);
            var bubble = instance.GetComponentInChildren<FloatingBubble>(true);
            bubble.gameObject.SetActive(true);
            typeof(FloatingBubble).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bubble, null);
            return bubble;
        }

        private PointerEventData PointerAt(RectTransform rect) =>
            new PointerEventData(events.GetComponent<EventSystem>())
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.position)
            };

        private static T GetPrivate<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
