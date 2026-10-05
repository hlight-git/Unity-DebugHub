# Changelog

## 3.0.0

### Phá vỡ tương thích
- Bỏ IngameDebugConsole (submodule, prefab console, `[ConsoleMethod]`, lệnh cầu `hub "..."`, `console.show`, `console.auto`). Log xem ở trang **Log** của hub; chạy một dòng lệnh bằng code vẫn có `DebugHub.Execute(line)`.
- Bỏ symbol `DISABLE_DEBUG_HUB`, `ALWAYS_ENABLE_INGAME_DEBUGGER` và `RenameFolderOnBuild`.
- Hub không còn xử lý `PRODUCTION`: project tắt log Unity thì hub không có log để hiện (kể cả dòng kết quả của command).
- `networkReachabilityAuthenticationBypass` → `autoUnlock` (danh sách trang nội bộ + chuỗi bắt buộc, khớp một trang là đủ). Mọi build. Android, PC kiểm lúc mở / quay lại app (mở khoá ngầm, lắc là bong bóng hiện); iOS, macOS chỉ kiểm khi ô password mở.
- Trigger vẽ nguệch ngoạc: `minDragDistance`/`maxStartEndDistance` (pixel) → `minDragScreens`/`maxStartEndGap` (theo cạnh ngắn màn hình). Giá trị đã chỉnh trong scene không mang sang.
- `ConsoleController` đổi tên `BuiltinCommands` (cùng GUID); field `DebugHub.console` thành `commands` (`FormerlySerializedAs`, scene/prefab cũ không mất reference).
- Bỏ event public `DebugHubToast.Clicked`: bấm dòng kết quả đi qua `DebugHubPanel.ResultClicked`.

### Thêm
- Bộ ghi log chạy từ lúc khởi động trên máy được ghi (đã mở khoá hoặc bản nội bộ: TestFlight/ad-hoc/Xcode, Firebase App Tester, Editor): mở trang log muộn vẫn đủ log của phiên.
- Trang Log: danh sách ảo hoá, lọc Log/Cảnh báo/Lỗi kèm bộ đếm, tìm có tô sáng, gộp log trùng, vạch command trong dòng thời gian, bám đáy + "N log mới", trang chi tiết (stack từng frame với kiểu đối số rút gọn, thanh Trước/Copy/Sau ghim ở đáy window), Copy tất cả, Xoá có hiện lại.
- Logcat Android của chính tiến trình trong trang log (log Java/native của SDK), lấy lại cả phần buffer từ lúc tiến trình khởi động. Chip **Unity** lọc riêng log Unity.
- Log hệ thống iOS (OSLogStore, iOS 15+) của chính tiến trình trong trang log: NSLog / os_log của SDK.
- Auto Unlock kiểm ngầm lúc mở app và khi quay lại app (trừ iOS): vào Wi-Fi công ty là mở khoá ngầm như bản 2.x, lắc là bong bóng hiện — không cần password.
- Ký tự font không có trong log hiện thành `?`, hết vòng warning TMP tự sinh log.
- Rich text trong log hiện như Unity console: `<b>`, `<i>`, `<color>` vẽ ra (tên màu Unity đổi ra mã); `<size>`, `<material>`, `<quad>` bỏ vì hàng log cao cố định; tag khác là chữ. Tìm và Copy đi trên chữ nhìn thấy.
- Chấm đỏ số lỗi chưa xem trên bong bóng và nút Log. Bấm dòng kết quả mở trang log tại command vừa chạy.
- Bong bóng nhớ trạng thái hiện/ẩn qua phiên.

### Sửa
- Hết ~66 ms quét mọi assembly lúc khởi động (của IngameDebugConsole) trên máy mọi người chơi.
- Vẽ nguệch ngoạc làm được trên điện thoại: điểm thả cũ phải trúng trong 10 px (≈ 0,6 mm trên máy 1080p), giờ là 8% cạnh ngắn.
- Parser đối số thuộc hub (mang từ IngameDebugConsole, MIT): hết bảng chép tay.

## 2.0.0

### Phá vỡ tương thích
- Prefab không còn mang password: điền ở Inspector của `DebugHub` trong scene.
- Symbol của hub là `DISABLE_DEBUG_HUB`, không còn dùng `PRODUCTION`.
- Chỉ còn public API đăng ký: `DebugHub`, các kiểu node, `Node`, `DebugNodeExtensions`, `TextStyle`, `DismissMode`, `Palette` và các MonoBehaviour. `Address`, `Reflect`, `DebugRegistry`, các page… là `internal`.
- Bỏ `DebugHub.NeedsConfirm`, `DebugHub.OpenConfirm`, `DebugHub.Report`.
- MAX/AdMob/Proxima tìm bằng reflection: bỏ symbol `MAX_SDK`/`USE_ADMOB`/`PROXIMA` và reference asmdef. Proxima bỏ lệnh `exec`.
- Package không mang font; TMP dùng font mặc định của game.

### Thêm
- Nút repeat hiện tên lệnh.
- Address: indexer mảng, type argument có namespace, `$$` cho chuỗi bắt đầu bằng `$`.
- `int?`/`bool?`… sửa được tại chỗ.

### Sửa
- Transform/`IEnumerable` mở ra trang member (trước đây chỉ thấy danh sách con).
- "Nhập address…" mở được address gốc `$` và có gọi method (trước đây bắt ghim nên bị từ chối).
- Trang nào ném exception cũng chỉ hiện dòng lỗi, không vỡ panel.
- `Address` không bao giờ ném (sai số type argument, `.Item` trên type nhiều indexer).
- Trang member không còn đọc getter clone asset (`Renderer.material`…) và không lọc field `m_*` của game.
- Lệnh cuối: một luật ghi cho panel, `Execute` và nút repeat; nút repeat hiện kết quả và áp `HidesHub`.
- Lỗi báo đúng exception được ném (chỉ bóc `TargetInvocationException`).
- Log của command vẫn bắt được khi `com.hlight.logging` tắt log Unity.
- Owner đã huỷ lúc đăng ký không làm node sống mãi; `.Defaults()` trên node bị từ chối không ghi đè node cũ.
- `time.skip` trả time scale về giá trị trước đó và không kẹt khi game pause.
- Bỏ qua password theo mạng chỉ chạy ở build development.
- Entry bubble theo mô hình chat head của Messenger: vận tốc thả đo trên 0.1s cuối (tay dừng trước khi nhấc thì vận tốc 0), điểm dừng = chỗ nhấc tay + vận tốc × `momentum` quyết mép trái/phải và độ cao. Chỉ dính mép trái/phải (bỏ trên/dưới). Nút X hút hẳn bubble vào; thoát ra thì bubble theo tay ngay. `flingSpeed` thay bằng `momentum`.
- Nút repeat bật lần đầu lúc chưa có lệnh nào thì không bao giờ hiện (subscribe trong Awake của object lưu inactive).
- Chạy lệnh từ trang Params/Xác nhận mở lại sau khi owner đã bị huỷ: bị từ chối thay vì gọi delegate của object chết.
- Giá trị enum có `Confirms()`: chọn xong không còn tự gỡ luôn trang xác nhận.
- Address: `<`, `>`, `{n}` trong tham số chuỗi không bị hiểu là type argument / chọn overload; `<…>` trên method không generic báo lỗi thay vì ném; `#Type` generic mở báo lỗi thay vì ném.
- Mảng nhiều chiều mở ra danh sách phần tử thay vì lỗi; member explicit interface (`System.IAsyncResult.…`) không còn hiện thành row không mở được; `TMP_SubMesh(UI).material` không bị đọc.
- Trang Thao tác chỉ hiện "Ghim" khi ghim được (cùng luật nút ghim trên row).
- Proxima: thiết lập qua reflection lỗi giữa chừng thì gỡ container, không để row báo "bật" mà không có server.

### Hiệu năng
- Không cấp phát mỗi frame ở `DebugHub.Update`, kể cả lúc người chơi đang giữ tay (trigger 4 góc bỏ `Enum.GetValues`); bubble/nút repeat không đụng RectTransform khi đứng yên.
- Gợi ý type ra ngay sau debounce của ô tìm, không chờ thêm một nhịp làm mới.
- Trang member resolve address một lần mỗi lần dựng, không phải một lần mỗi row.
- Danh sách instance chia trang; TypeFinder sắp trước khi cắt và bỏ type do compiler sinh.
- Giao diện bake vào prefab thay vì sửa style lúc chạy.
