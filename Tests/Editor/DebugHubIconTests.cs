using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    public class DebugHubIconTests
    {
        /// Thiếu nhánh vẽ thì symbol rơi vào hình mặc định (đĩa tròn) hoặc trống — icon đọc sai nghĩa.
        /// Mỗi symbol phải vẽ một hình riêng, khác với mặc định Disc (khác số đỉnh hoặc khác tọa độ).
        [Test]
        public void EverySymbol_DrawsSomething()
        {
            var go = new GameObject("icon", typeof(RectTransform));
            try
            {
                ((RectTransform)go.transform).sizeDelta = new Vector2(100f, 100f);
                var icon = go.AddComponent<DebugHubIcon>();
                var populate = typeof(DebugHubIcon).GetMethod("OnPopulateMesh",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);

                // Lấy tọa độ mặc định (Disc) để so sánh.
                icon.symbol = DebugHubIcon.Symbol.Disc;
                using var discMesh = new VertexHelper();
                populate.Invoke(icon, new object[] { discMesh });
                var discPositions = new HashSet<Vector3>();
                var vertex = default(UIVertex);
                for (var i = 0; i < discMesh.currentVertCount; i++)
                {
                    discMesh.PopulateUIVertex(ref vertex, i);
                    discPositions.Add(vertex.position);
                }

                // Kiểm mỗi symbol: vẽ được gì, và khác Disc.
                foreach (DebugHubIcon.Symbol symbol in Enum.GetValues(typeof(DebugHubIcon.Symbol)))
                {
                    icon.symbol = symbol;
                    using var mesh = new VertexHelper();
                    populate.Invoke(icon, new object[] { mesh });
                    Assert.Greater(mesh.currentVertCount, 0, symbol.ToString());

                    // Nếu symbol là Disc, bỏ qua so sánh.
                    if (symbol == DebugHubIcon.Symbol.Disc) continue;

                    // Kiểm symbol khác Disc: số đỉnh hoặc tọa độ khác.
                    var symbolPositions = new HashSet<Vector3>();
                    for (var i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        symbolPositions.Add(vertex.position);
                    }

                    var isDifferent = mesh.currentVertCount != discMesh.currentVertCount;
                    if (!isDifferent)
                    {
                        foreach (var pos in symbolPositions)
                        {
                            if (!discPositions.Contains(pos))
                            {
                                isDifferent = true;
                                break;
                            }
                        }
                    }
                    Assert.IsTrue(isDifferent, $"{symbol} có cùng hình với Disc");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
