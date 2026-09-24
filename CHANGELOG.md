# Changelog

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
