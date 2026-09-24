using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlight.Debug.Hub
{
    /// Chat head kiểu Messenger — mọi hành vi quy về một mô hình:
    /// - Kéo: bubble dính ngón tay. Lại gần nút X thì bị hút hẳn vào tâm X; ra khỏi vùng (rộng hơn vùng hút,
    ///   để khỏi giật ra-vào ở rìa) thì lò xo đưa về ngón tay rồi dính tay tiếp. Thả lúc đang bị hút = ẩn.
    /// - Thả: chiếu đà — điểm dừng = chỗ nhấc tay + vận tốc × momentum. Nửa màn hình chứa điểm đó quyết định
    ///   mép trái/phải, y dừng đúng ở điểm đó. Không có ngưỡng "ném" hay "đặt chỗ": thả nhẹ thì đà nhỏ, tự về
    ///   mép gần; hất mạnh thì đà lớn, tự sang mép bên kia.
    /// - Sau khi thả: lò xo nhận nguyên vận tốc tay, nên chuyển động nối liền cú thả.
    public class FloatingBubble : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum Edge { Left, Right }

        private const string EdgeKey = "DebugHub.BubbleEdge";
        private const string AlongKey = "DebugHub.BubbleAlong";
        private const float MaxReleaseSpeed = 4800f;
        private const float FlightOverflow = 34f;
        private const float CaptureRadius = 150f;
        private const float ReleaseRadius = 230f;
        private const float CatchUpDistance = 8f;

        /// Vận tốc thả đo trên chừng này giây cuối tính tới lúc nhấc tay, như VelocityTracker của Android.
        /// Ngón tay đứng yên trước khi nhấc thì quãng đứng yên nằm trong phép đo — vận tốc tự về 0.
        private const float VelocityWindow = 0.1f;
        private const int SampleCount = 16;

        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Canvas canvas;
        [SerializeField] private float snapSpeed = 14f;
        [Tooltip("Giây đà khi thả: điểm dừng = chỗ nhấc tay + vận tốc × momentum. Lớn = ném đi xa hơn.")]
        [SerializeField, Min(0.05f)] private float momentum = 0.18f;
        [Tooltip("Vùng thả để ẩn bubble, con trực tiếp của canvas.")]
        [SerializeField] private RectTransform dismissTarget;
        [SerializeField] private CanvasGroup dismissGroup;
        [SerializeField] private CanvasGroup bubbleGroup;

        private readonly Vector2[] samplePositions = new Vector2[SampleCount];
        private readonly float[] sampleTimes = new float[SampleCount];
        private int sampleHead;
        private int sampleFill;

        private bool initialized;
        private bool isDragging;
        private bool captured;
        private bool following;
        private Vector2 pointerOffset;
        private Vector2 finger;
        private Vector2 target;
        private Vector2 springVelocity;
        private Vector2 canvasSize;
        private Edge dockedEdge;
        private float suppressClickUntil;
        private float visualScale = 1f;
        private float scaleVelocity;
        private float desiredScale = 1f;
        private Rect lastSafeArea;

        public event Action<bool> DragStateChanged;
        public event Action Dismissed;
        public bool SuppressClick => Time.unscaledTime < suppressClickUntil;
        public bool IsDragging => isDragging;
        public Edge DockedEdge => dockedEdge;

        /// Đã nằm yên ở mép: không kéo, không trượt. Nút repeat hỏi cái này để khỏi dời theo mỗi frame.
        public bool Settled { get; private set; }
        private RectTransform CanvasRect => (RectTransform)canvas.transform;

        private void OnEnable()
        {
            bubbleGroup.alpha = 0f;
            bubbleGroup.blocksRaycasts = false;
            initialized = false;
            TryInitialize();
        }

        private void OnDisable()
        {
            isDragging = false;
            captured = false;
            springVelocity = Vector2.zero;
            desiredScale = visualScale = 1f;
            rectTransform.localScale = Vector3.one;
            if (dismissTarget) dismissTarget.gameObject.SetActive(false);
        }

        private bool TryInitialize()
        {
            Canvas.ForceUpdateCanvases();
            if (CanvasRect.rect.width < 2f || CanvasRect.rect.height < 2f ||
                rectTransform.rect.width < 2f || rectTransform.rect.height < 2f) return false;

            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            canvasSize = CanvasRect.rect.size;
            lastSafeArea = Screen.safeArea;
            PositionDismissTarget();

            // Bản cũ còn lưu Top/Bottom (2/3): quy về phải.
            dockedEdge = PlayerPrefs.GetInt(EdgeKey, (int)Edge.Right) == (int)Edge.Left ? Edge.Left : Edge.Right;
            target = Dock(dockedEdge, PlayerPrefs.GetFloat(AlongKey, 0.65f));
            rectTransform.anchoredPosition = target;
            rectTransform.localScale = Vector3.one;
            springVelocity = Vector2.zero;
            initialized = true;
            bubbleGroup.alpha = 1f;
            bubbleGroup.blocksRaycasts = true;
            return true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!initialized && !TryInitialize()) return;
            if (!Local(eventData, out var pointer)) return;
            isDragging = true;
            captured = false;
            following = true;
            springVelocity = Vector2.zero;
            pointerOffset = rectTransform.anchoredPosition - pointer;
            finger = rectTransform.anchoredPosition;
            sampleFill = 0;
            Sample(finger, Time.realtimeSinceStartup);
            desiredScale = 1.08f;
            dismissTarget.gameObject.SetActive(true);
            dismissGroup.alpha = 0.72f;
            dismissTarget.localScale = Vector3.one;
            DragStateChanged?.Invoke(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || !Local(eventData, out var pointer)) return;
            var now = Time.realtimeSinceStartup;
            var fingerDelta = pointer + pointerOffset - finger;
            finger += fingerDelta;
            Sample(finger, now);

            var wasCaptured = captured;
            captured = Vector2.Distance(finger, dismissTarget.anchoredPosition) < (captured ? ReleaseRadius : CaptureRadius);
            if (captured && !wasCaptured)
            {
                // Bị hút đi với chính đà của ngón tay, không khựng lại rồi mới bay.
                following = false;
                springVelocity = VelocityAt(finger, now);
            }
            target = captured ? dismissTarget.anchoredPosition : finger;
            // Vừa thoát khỏi X: bubble đi theo tay ngay, lò xo chỉ co khoảng hở — đuổi theo đích đang chạy thì
            // tay càng nhanh càng trễ, đọc như dây thun.
            if (following) rectTransform.anchoredPosition = finger;
            else if (!captured) rectTransform.anchoredPosition += fingerDelta;

            dismissGroup.alpha = captured ? 1f : 0.72f;
            dismissTarget.localScale = Vector3.one * (captured ? 1.12f : 1f);
            desiredScale = captured ? 0.76f : 1.08f;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;
            suppressClickUntil = Time.unscaledTime + 0.2f;
            desiredScale = 1f;
            dismissTarget.gameObject.SetActive(false);
            DragStateChanged?.Invoke(false);

            if (captured)
            {
                captured = false;
                Dismissed?.Invoke();
                return;
            }
            Release(VelocityAt(finger, Time.realtimeSinceStartup));
        }

        /// Chiếu đà ra điểm dừng, chọn mép theo nửa màn hình chứa điểm đó.
        internal void Release(Vector2 velocity)
        {
            var bounds = Bounds();
            var rest = rectTransform.anchoredPosition + velocity * momentum;
            dockedEdge = rest.x < bounds.center.x ? Edge.Left : Edge.Right;
            target = Dock(dockedEdge, Mathf.InverseLerp(bounds.yMin, bounds.yMax, rest.y));
            springVelocity = velocity;
            Save();
        }

        internal void Sample(Vector2 position, float time)
        {
            sampleHead = (sampleHead + 1) % SampleCount;
            samplePositions[sampleHead] = position;
            sampleTimes[sampleHead] = time;
            sampleFill = Mathf.Min(sampleFill + 1, SampleCount);
        }

        /// Quãng đường ngón tay đi trong VelocityWindow cuối (tính tới `now`) chia cho thời gian.
        internal Vector2 VelocityAt(Vector2 position, float now)
        {
            var fromPosition = position;
            var fromTime = now;
            for (var i = 0; i < sampleFill; i++)
            {
                var index = (sampleHead - i + SampleCount) % SampleCount;
                fromPosition = samplePositions[index];
                fromTime = sampleTimes[index];
                if (now - fromTime >= VelocityWindow) break;
            }
            var elapsed = now - fromTime;
            return elapsed > 1e-3f
                ? Vector2.ClampMagnitude((position - fromPosition) / elapsed, MaxReleaseSpeed)
                : Vector2.zero;
        }

        private void Update()
        {
            if (!initialized)
            {
                TryInitialize();
                return;
            }
            // Nằm yên thì không gán gì: gán RectTransform mỗi frame là canvas dựng lại batch mỗi frame.
            if (!Mathf.Approximately(visualScale, desiredScale))
            {
                visualScale = Mathf.SmoothDamp(visualScale, desiredScale, ref scaleVelocity,
                    0.075f, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(visualScale - desiredScale) < 0.001f) visualScale = desiredScale;
                rectTransform.localScale = Vector3.one * visualScale;
            }
            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (isDragging)
            {
                Settled = false;
                if (following) return;
                // Bị hút vào X, hoặc vừa thoát ra và đang đuổi theo ngón tay.
                StepSpring(deltaTime);
                if (!captured && (finger - rectTransform.anchoredPosition).sqrMagnitude < CatchUpDistance * CatchUpDistance)
                {
                    following = true;
                    rectTransform.anchoredPosition = finger;
                }
                return;
            }
            if (CanvasRect.rect.size != canvasSize || Screen.safeArea != lastSafeArea)
            {
                var along = Along(target);
                canvasSize = CanvasRect.rect.size;
                lastSafeArea = Screen.safeArea;
                PositionDismissTarget();
                target = Dock(dockedEdge, along);
            }
            Settled = StepSpring(deltaTime);
        }

        /// true = đã nằm yên ở target.
        private bool StepSpring(float deltaTime)
        {
            var position = rectTransform.anchoredPosition;
            if (position == target && springVelocity == Vector2.zero) return true;
            if (deltaTime <= 0f) return false;
            var distance = target - position;
            if (distance.sqrMagnitude < 0.08f && springVelocity.sqrMagnitude < 9f)
            {
                rectTransform.anchoredPosition = target;
                springVelocity = Vector2.zero;
                return true;
            }

            // x: hơi thiếu cản để nảy nhẹ vào mép. y sau khi thả: cản tới hạn với ω = 1/momentum — lò xo đó nhận
            // vận tốc v thì trôi đúng v × momentum rồi dừng, tức đúng điểm Release đã chiếu, không vượt.
            var frequency = Mathf.Max(6f, snapSpeed);
            var frequencyY = isDragging ? frequency : 1f / momentum;
            springVelocity += new Vector2(
                distance.x * frequency * frequency - springVelocity.x * frequency * 1.35f,
                distance.y * frequencyY * frequencyY - springVelocity.y * frequencyY * 2f) * deltaTime;
            var next = position + springVelocity * deltaTime;
            var flightBounds = Bounds();
            var min = flightBounds.min - Vector2.one * FlightOverflow;
            var max = flightBounds.max + Vector2.one * FlightOverflow;
            if (next.x < min.x || next.x > max.x)
            {
                next.x = Mathf.Clamp(next.x, min.x, max.x);
                springVelocity.x *= -0.18f;
            }
            if (next.y < min.y || next.y > max.y)
            {
                next.y = Mathf.Clamp(next.y, min.y, max.y);
                springVelocity.y *= -0.18f;
            }
            rectTransform.anchoredPosition = next;
            return false;
        }

        private Vector2 Dock(Edge edge, float along)
        {
            var bounds = Bounds();
            return new Vector2(edge == Edge.Left ? bounds.xMin : bounds.xMax,
                Mathf.Lerp(bounds.yMin, bounds.yMax, Mathf.Clamp01(along)));
        }

        private float Along(Vector2 point)
        {
            var bounds = Bounds();
            return Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y);
        }

        private void Save()
        {
            PlayerPrefs.SetInt(EdgeKey, (int)dockedEdge);
            PlayerPrefs.SetFloat(AlongKey, Along(target));
            PlayerPrefs.Save();
        }

        private Rect Bounds()
        {
            var canvasRect = CanvasRect.rect;
            var half = rectTransform.rect.size * 0.5f;
            var safe = Screen.safeArea;
            var sx = canvasRect.width / Mathf.Max(1f, Screen.width);
            var sy = canvasRect.height / Mathf.Max(1f, Screen.height);
            var min = new Vector2(canvasRect.xMin + safe.xMin * sx, canvasRect.yMin + safe.yMin * sy);
            var max = new Vector2(canvasRect.xMin + safe.xMax * sx, canvasRect.yMin + safe.yMax * sy);
            return Rect.MinMaxRect(min.x + half.x - 18f, min.y + half.y - 18f,
                max.x - half.x + 18f, max.y - half.y + 18f);
        }

        private bool Local(PointerEventData eventData, out Vector2 point) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, eventData.position,
                eventData.pressEventCamera, out point);

        private void PositionDismissTarget()
        {
            var canvasRect = CanvasRect.rect;
            var safe = Screen.safeArea;
            var safeBottom = canvasRect.yMin + safe.yMin / Mathf.Max(1f, Screen.height) * canvasRect.height;
            dismissTarget.anchoredPosition = new Vector2(0f, safeBottom + 105f);
        }
    }
}
