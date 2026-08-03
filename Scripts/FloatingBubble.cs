using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlight.Debug.Hub
{
    public class FloatingBubble : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Canvas canvas;
        [SerializeField] private float snapSpeed = 12f;
        
        private bool isDragging;
        public event Action<bool> DragStateChanged;

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            DragStateChanged?.Invoke(isDragging);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            DragStateChanged?.Invoke(isDragging);
        }

        private void Update()
        {
            if (isDragging) return;
            var targetPos = GetNearestEdge(rectTransform.anchoredPosition);
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                targetPos,
                Time.deltaTime * snapSpeed
            );
        }

        private Vector2 GetNearestEdge(Vector2 currentPos)
        {
            var canvasRect = (RectTransform) canvas.transform;

            var halfCanvasWidth = canvasRect.rect.width / 2;
            var halfCanvasHeight = canvasRect.rect.height / 2;

            var halfWidth = rectTransform.rect.width / 2;
            var halfHeight = rectTransform.rect.height / 2;

            var left = -halfCanvasWidth + halfWidth;
            var right = halfCanvasWidth - halfWidth;
            var top = halfCanvasHeight - halfHeight;
            var bottom = -halfCanvasHeight + halfHeight;

            var distLeft = Mathf.Abs(currentPos.x - left);
            var distRight = Mathf.Abs(currentPos.x - right);
            var distTop = Mathf.Abs(currentPos.y - top);
            var distBottom = Mathf.Abs(currentPos.y - bottom);

            var minDist = Mathf.Min(distLeft, distRight, distTop, distBottom);

            if (Mathf.Approximately(minDist, distLeft)) return new Vector2(left, Mathf.Clamp(currentPos.y, bottom, top));
            if (Mathf.Approximately(minDist, distRight)) return new Vector2(right, Mathf.Clamp(currentPos.y, bottom, top));
            if (Mathf.Approximately(minDist, distTop)) return new Vector2(Mathf.Clamp(currentPos.x, left, right), top);
            return new Vector2(Mathf.Clamp(currentPos.x, left, right), bottom);
        }
    }
}