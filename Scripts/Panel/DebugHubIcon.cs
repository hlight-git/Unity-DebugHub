using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    // Small vector icons stay sharp at any canvas scale and need no font glyphs or textures.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DebugHubIcon : MaskableGraphic
    {
        public enum Symbol { Search, Tools, Close, Star, Disc, Replay, StarFilled }

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("symbol")] private Symbol symbolShape;

        /// Property chứ không field trần: đổi hình phải dựng lại mesh, không thì icon giữ hình cũ tới khi
        /// có gì khác làm nó dirty.
        public Symbol symbol
        {
            get => symbolShape;
            set
            {
                if (symbolShape == value) return;
                symbolShape = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (symbol == Symbol.Search)
            {
                Circle(mesh, new Vector2(-0.10f, 0.10f), 0.28f);
                Line(mesh, new Vector2(0.10f, -0.10f), new Vector2(0.38f, -0.38f));
            }
            else if (symbol == Symbol.Tools)
            {
                for (var i = 0; i < 3; i++)
                {
                    var y = 0.28f - i * 0.28f;
                    var x = i == 1 ? 0.16f : -0.14f;
                    Line(mesh, new Vector2(-0.4f, y), new Vector2(x - 0.09f, y));
                    Line(mesh, new Vector2(x + 0.09f, y), new Vector2(0.4f, y));
                    Circle(mesh, new Vector2(x, y), 0.09f);
                }
            }
            else if (symbol == Symbol.Close)
            {
                Line(mesh, new Vector2(-0.28f, -0.28f), new Vector2(0.28f, 0.28f));
                Line(mesh, new Vector2(-0.28f, 0.28f), new Vector2(0.28f, -0.28f));
            }
            else if (symbol == Symbol.Star || symbol == Symbol.StarFilled)
            {
                var points = new Vector2[10];
                for (var i = 0; i < points.Length; i++)
                {
                    var angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    var radius = i % 2 == 0 ? 0.38f : 0.17f;
                    points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                if (symbol == Symbol.StarFilled) FilledFan(mesh, points);
                else for (var i = 0; i < points.Length; i++) Line(mesh, points[i], points[(i + 1) % points.Length]);
            }
            else if (symbol == Symbol.Replay)
            {
                // Open circular arrow. Keeping it as geometry avoids missing-glyph squares on
                // stripped mobile font assets.
                const int segments = 20;
                const float start = 0.35f;
                const float sweep = 4.75f;
                var previous = new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * 0.31f;
                for (var i = 1; i <= segments; i++)
                {
                    var angle = start + sweep * i / segments;
                    var next = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.31f;
                    Line(mesh, previous, next);
                    previous = next;
                }
                Line(mesh, previous, previous + new Vector2(-0.19f, 0.02f));
                Line(mesh, previous, previous + new Vector2(-0.04f, 0.19f));
            }
            else FilledCircle(mesh, Vector2.zero, 0.48f);
        }

        private void Circle(VertexHelper mesh, Vector2 center, float radius)
        {
            for (var i = 0; i < 24; i++)
            {
                var a = i * Mathf.PI / 12f;
                var b = (i + 1) * Mathf.PI / 12f;
                Line(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius);
            }
        }

        /// Quạt tam giác từ tâm: đủ cho hình sao vì mọi đỉnh đều nhìn thấy tâm.
        private void FilledFan(VertexHelper mesh, Vector2[] points)
        {
            var rect = rectTransform.rect;
            var scale = Mathf.Min(rect.width, rect.height);
            var center = mesh.currentVertCount;
            mesh.AddVert(rect.center, color, Vector2.zero);
            foreach (var point in points) mesh.AddVert(rect.center + point * scale, color, Vector2.zero);
            for (var i = 0; i < points.Length; i++)
                mesh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % points.Length);
        }

        private void FilledCircle(VertexHelper mesh, Vector2 center, float radius)
        {
            var rect = rectTransform.rect;
            var scale = Mathf.Min(rect.width, rect.height);
            var first = mesh.currentVertCount;
            mesh.AddVert(rect.center + center * scale, color, Vector2.zero);
            for (var i = 0; i <= 32; i++)
            {
                var angle = i * Mathf.PI * 2f / 32f;
                mesh.AddVert(rect.center + (center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius) * scale,
                    color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Line(VertexHelper mesh, Vector2 a, Vector2 b)
        {
            var rect = rectTransform.rect;
            var scale = Mathf.Min(rect.width, rect.height);
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * 0.032f;
            var start = mesh.currentVertCount;
            mesh.AddVert(rect.center + (a - normal) * scale, color, Vector2.zero);
            mesh.AddVert(rect.center + (a + normal) * scale, color, Vector2.zero);
            mesh.AddVert(rect.center + (b + normal) * scale, color, Vector2.zero);
            mesh.AddVert(rect.center + (b - normal) * scale, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
