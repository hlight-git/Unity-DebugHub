# Spec ② — Logcat Android trong trang log, tự mở khoá tức thì

Ngày: 2026-10-02. Nối tiếp spec ① (`2026-10-01-in-house-console-design.md`). User đã duyệt trong chat.

## 1. Vấn đề (đo trên Redmi K70 Pro, Android 16, bản 1.7.1 cài APK)

- Trang log có đủ 29/29 log C# của phiên, nhưng thiếu toàn bộ log Java/native mà logcat có (AppLovin, Firebase, UnityAds, lỗi Java…). User so với logcat, không so với Unity console.
- Phiên đầu sau khi cài (APK, chưa mở khoá) chỉ ghi từ lúc mở khoá.
- Ở mạng công ty vẫn phải làm cử chỉ để ô password hiện thì Auto Unlock mới chạy.
- Log game có `→`, emoji: font game không có, TMP bắn warning mỗi lần hub vẽ, warning lại thành log mới.

## 2. Logcat của chính tiến trình

- Từ Android 4.1 app chỉ đọc được log của chính nó: `logcat -v threadtime --pid=<pid>`, không cần quyền. Chỉ chạy khi `LogRecorder` đang ghi (đã mở khoá hoặc bản nội bộ) — máy người chơi không chạy.
- **Lúc bắt đầu ghi** (khởi động hoặc mở khoá giữa phiên): đọc đồng bộ `logcat -d` (buffer còn giữ từ lúc tiến trình khởi động) và đưa vào ring **trước khi** gắn callback Unity → thứ tự đúng, phiên đầu không hụt đầu. Sau đó stream `logcat -T <giờ dòng cuối>` trên thread nền.
  - ponytail: dump đồng bộ ~50–150 ms, chỉ trên máy được ghi; chuyển sang async + giữ callback chờ khi hitch đáng kể.
- Dòng tag `Unity` trong logcat: lấy khi **trước** lúc gắn callback, bỏ khi **sau** (callback có message + stack chuẩn hơn).
- Dòng liên tiếp cùng header (giờ, pid, tid, mức, tag) gộp một entry: dòng đầu là message, các dòng sau là stack (stack Java của exception).
- Mức: V/D/I → Log, W → Warning, E/F/A → Error. Entry có `Source = Native`, `Tag`. Dòng phụ của hàng log hiện tag thay cho nơi gọi; tìm khớp cả tag.
- Chấm đỏ chỉ đếm lỗi của Unity: E của OS/SDK (MIUI, Zygote…) có ở mọi phiên, đếm vào thì chấm luôn sáng.
- Chung ring 4 MB với log Unity.
- Chip **Unity** trên thanh lọc, cạnh Gộp: bật = chỉ còn log Unity (kể cả log Android tới sau). Hiện khi đã có log native (Editor không có gì để lọc) hoặc khi đang bật. Bỏ lọc tắt nó. User 2026-10-02: chỉ cần nút này, không cần lọc theo từng tag.

## 3. Tự mở khoá tức thì (Android, PC)

- Chưa mở khoá và có Pages: kiểm ngầm lúc mở app và mỗi lần app quay lại foreground, tới khi mở khoá. Khớp = mở khoá ngầm + bắt đầu ghi, KHÔNG bật bong bóng (user 2026-10-02: bong bóng tự hiện kém tiện hơn bản 2.x) — lắc / vẽ 4 góc mới hiện. Ô password đang mở lúc khớp thì đóng ô và hiện bong bóng.
- iOS, tvOS, visionOS, app macOS 15+ giữ như spec ①: chỉ kiểm khi ô password mở. Các nền tảng này hiện hộp xin quyền "mạng cục bộ" lần đầu chạm LAN: kiểm lúc mở app là mọi người chơi và reviewer thấy. Danh sách cho phép ở `AutoUnlock.ChecksAtLaunch` (Android, Windows, Linux, Editor macOS).

## 4. Ký tự font không có

- `LogText.Escape` (cửa duy nhất đưa dữ liệu game vào TMP) đổi code point font không có thành `?`, tra theo font của hàng log (kể cả fallback). Copy và tìm vẫn trên chuỗi gốc. ASCII, ký tự điều khiển, ZWSP không tra.

## 4b. iOS (phần ③)

- `OSLogStore` phạm vi tiến trình hiện tại (iOS 15+, project target 15.0): NSLog / os_log của SDK. Plugin `DebugHubOsLog.mm` trả một lượt `giây ␟ mức ␟ nguồn ␟ sender ␟ nội dung ␞`, `!lý do` khi hỏng (báo một lần rồi dừng).
- Lượt đầu đồng bộ trước khi gắn callback (lấy từ lúc tiến trình khởi động), sau đó thread nền đọc mỗi 2 s từ giờ của entry cuối. Sender là tag. Log Unity và NSLog của SDK link tĩnh đều ra từ `UnityFramework`, subsystem rỗng: không tách được, nên từ SubsystemRegistration hub thay handler os_log của Trampoline (`UnitySetLogEntryHandler`) bằng bản y hệt ghi vào subsystem `com.hlight.debughub.unity` — nguồn `U` = log Unity, chỉ lấy trước khi callback gắn; còn lại là SDK, giữ.
- Mức: debug/info/notice → Log, error/fault → Error. Chung chip Unity, chung luật chấm đỏ.

## 5. Kiểm

- EditMode: parse dòng threadtime, gộp dòng, luật bỏ tag Unity, map mức, entry native vào ring, chấm đỏ không đếm lỗi native, quyết định tự kiểm theo nền tảng, thay ký tự thiếu.
- Máy thật: so trang log với `adb logcat --pid`, mở app ở Wi-Fi công ty rồi lắc là bong bóng hiện, không hỏi password.
