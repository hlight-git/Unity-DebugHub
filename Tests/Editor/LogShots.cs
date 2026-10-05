using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    /// Chụp panel ra PNG để so với mockup đã duyệt — không phải test. Gọi qua `unity cmd eval_file`:
    /// System.Type.GetType("Hlight.Debug.Hub.Tests.LogShots, Hlight.Debug.Hub.Tests").GetMethod("CaptureAll").Invoke(null, null)
    public static class LogShots
    {
        private const string STACK =
            "UnityEngine.Debug:LogError (object)\n" +
            "Harvest.Gameplay.ItemPiece:OnPointerUp (UnityEngine.EventSystems.PointerEventData) (at Assets/0_DevRoot/Scripts/ItemPiece.cs:212)\n" +
            "Harvest.Gameplay.TrayController:TryPush (Harvest.Gameplay.ItemPiece) (at Assets/0_DevRoot/Scripts/TrayController.cs:61)\n" +
            "UnityEngine.EventSystems.ExecuteEvents:Execute (UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.BaseEventData)\n";

        public static string CaptureAll()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            LogRecorder.Start();
            Seed();
            try
            {
                // Trước khi mở trang log: header gốc có nút Log kèm chấm đỏ số lỗi chưa xem.
                var root = Shoot("Temp/debughub-root.png", _ => { });
                var bottom = Shoot("Temp/debughub-log.png", panel => panel.Push(LogPage.Build()));
                // Lỗi nằm giữa danh sách: hai nút Trước/Sau đều sáng. Log cuối danh sách: nút Sau mờ.
                var detail = Shoot("Temp/debughub-log-detail.png", panel => OpenDetail(panel, firstError: true));
                var last = Shoot("Temp/debughub-log-detail-last.png", panel => OpenDetail(panel, firstError: false));
                // Đầu danh sách, Gộp bật, đang cuộn lên khi có log mới: thấy dòng ghi chú, badge ×N và viên "N log mới".
                var top = Shoot("Temp/debughub-log-top.png", panel =>
                {
                    LogModel.Shared.Follow = false;
                    LogModel.Shared.Collapse = true;
                    panel.Push(LogPage.Build());
                    ((LogView)TestPanel.Field(panel, "logView")).Tick();
                    for (var i = 0; i < 3; i++) LogRecorder.Receive($"Combo {i + 3} x Corn", null, LogType.Log);
                });
                // Stack dài + log lặp 123 lần (Gộp đang bật từ shot trên): window cao tối đa, footer vẫn ghim đáy,
                // dòng phụ ghi "×123". Đặt sau shot `top` để các entry thêm vào không đổi những shot đã duyệt.
                var tall = Shoot("Temp/debughub-log-detail-long.png", panel =>
                {
                    for (var i = 0; i < 123; i++) LogRecorder.Receive("Spawn failed: pool exhausted", LongStack(), LogType.Error);
                    OpenDetail(panel, firstError: false);
                });
                // Bong bóng một mình (panel đóng) với chấm đỏ: số nhỏ, rồi số tràn "99+" — xem chữ có nằm gọn trong vòng tròn.
                var bubble = Shoot("Temp/debughub-entry-badge.png", panel => ShowBubble(panel, 12, 0, 3));
                var bubble99 = Shoot("Temp/debughub-entry-badge-99.png", panel => ShowBubble(panel, 12, 3, 120));
                return root + ", " + bottom + ", " + detail + ", " + last + ", " + top + ", " + tall + ", " + bubble + ", " + bubble99;
            }
            finally
            {
                LogRecorder.Reset();
                LogModel.ResetShared();
            }
        }

        private static string LongStack()
        {
            var stack = new System.Text.StringBuilder("UnityEngine.Debug:LogError (object)\n");
            for (var i = 0; i < 14; i++)
            {
                stack.Append($"Harvest.Gameplay.Spawner{i}:Step (Harvest.Gameplay.ItemPiece,System.Int32) (at Assets/Spawner{i}.cs:{20 + i})\n");
                stack.Append("UnityEngine.EventSystems.ExecuteEvents:Execute (UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.BaseEventData)\n");
            }
            return stack.ToString();
        }

        private static void ShowBubble(DebugHubPanel panel, long logs, long warnings, long errors)
        {
            panel.Close();
            var entry = panel.transform.root.GetComponentInChildren<DebugHubEntry>(true);
            entry.Activating = true;
            // Như trên máy: bong bóng dính mép phải, nút repeat nằm dưới entry.
            var bubble = entry.GetComponent<FloatingBubble>();
            typeof(FloatingBubble).GetField("dockedEdge", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(bubble, FloatingBubble.Edge.Right);
            var rect = (RectTransform)entry.transform;
            var canvas = (RectTransform)rect.parent;
            rect.anchoredPosition = new Vector2(canvas.rect.width * 0.5f - rect.rect.width * 0.5f - 24f, rect.anchoredPosition.y);
            entry.SetCounts(logs, warnings, errors);
            // Nút repeat cạnh entry, như sau khi đã chạy một lệnh.
            var repeat = panel.transform.root.GetComponentInChildren<RepeatButton>(true);
            repeat.gameObject.SetActive(true);
            repeat.Present("network.ping");
        }

        private static void OpenDetail(DebugHubPanel panel, bool firstError)
        {
            panel.Push(LogPage.Build());
            var model = LogModel.Shared;
            model.Pull();
            LogItem pick = null;
            for (var i = 1; i < model.RowCount; i++)
            {
                var item = model.RowAt(i);
                if (item.IsMarker || (firstError && item.Entry.Type != LogType.Error)) continue;
                pick = item;
                if (firstError) break;
            }
            panel.Push(LogDetailPage.For(model, pick));
        }

        private static void Seed()
        {
            LogRecorder.Receive("[Boot] RootScope ready in 412 ms", "Harvest.RootScope:Awake () (at Assets/RootScope.cs:38)", LogType.Log);
            LogRecorder.Receive("[Firebase] Remote config fetched, 12 keys", "Harvest.RemoteConfigService:OnFetch () (at Assets/RemoteConfigService.cs:91)", LogType.Log);
            for (var i = 0; i < 4; i++)
                LogRecorder.Receive("[MAX] Interstitial not ready, skipped", "Harvest.AdsManager:ShowInterstitial () (at Assets/AdsManager.cs:144)", LogType.Warning);
            LogRecorder.Mark("level.goto 5");
            LogRecorder.Receive("Load level 5 (preset Level_005)", "Harvest.LevelLoader:Load (int) (at Assets/LevelLoader.cs:57)", LogType.Log);
            LogRecorder.Receive("NullReferenceException: Object reference not set to an instance of an object", STACK, LogType.Error);
            LogRecorder.Receive("Match 3 x Tomato, combo 2", "Harvest.MatchBuffer:Resolve () (at Assets/MatchBuffer.cs:88)", LogType.Log);
            LogRecorder.Receive("Slot full, waiting for match slide", "Harvest.TrayController:Push () (at Assets/TrayController.cs:61)", LogType.Warning);
            LogRecorder.Receive("Failed to load addressable 'Level_006': InvalidKeyException", "Harvest.LevelPreset:LoadAsync (string) (at Assets/LevelPreset.cs:40)", LogType.Error);
            LogRecorder.Receive("[Ads] Banner loaded 320x50", "Harvest.BannerView:OnLoaded () (at Assets/BannerView.cs:23)", LogType.Log);
            LogRecorder.Receive("Set user_properties : <color=green><b>max_level</b></color> - 5 <size=60>List<int></size>", "Harvest.TrackerManager:SetUserProperty (string,object) (at Assets/TrackerManager.cs:55)", LogType.Log);
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Log, "AppLovinSdk", "Rewarded ad loaded in 412ms", null);
            LogRecorder.ReceiveNative(System.DateTime.Now, LogType.Warning, "MIUIInput", "[MotionEvent] ViewRootImpl windowName 'UnityPlayerActivity'", null);
        }

        internal static string Shoot(string path, Action<DebugHubPanel> arrange)
        {
            var panel = TestPanel.Build();
            var canvas = panel.GetComponentInParent<Canvas>();
            var camera = new GameObject("shot camera").AddComponent<Camera>();
            var texture = new RenderTexture(1080, 2160, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.227f, 0.2f);
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();

                panel.Show(CommandsPage.Root());
                arrange(panel);
                var view = (LogView)TestPanel.Field(panel, "logView");
                view.Tick();
                Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>()) text.ForceMeshUpdate();

                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(1080, 2160, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1080, 2160), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                TestPanel.Destroy(panel);
            }
        }
    }
}
