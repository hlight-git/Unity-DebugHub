# Console nội bộ thay IngameDebugConsole — spec ①

Ngày: 2026-10-01 · Package: `com.hlight.debug-hub` 2.0.0 → **3.0.0** · Trạng thái: thiết kế đã duyệt, chưa làm.

## 1. Vì sao

IngameDebugConsole (IDC) hiện chỉ còn một giá trị cho hub: cửa sổ xem log. Cái giá:

- **Mất log trước lần mở đầu.** Prefab console chỉ `Instantiate` khi bật `console.show`; `DebugLogManager` chỉ nghe log từ lúc tồn tại. Bấm dòng kết quả cũng tạo console *sau* khi log đã in → dòng vừa thấy không có trong cửa sổ.
- **~66 ms quét mọi assembly lúc khởi động** (`[ConsoleMethod]`, `RuntimeInitializeOnLoadMethod`) trên máy mọi người chơi, không define nào tắt được. Hub thì đã thay registry của IDC, chỉ còn lệnh cầu `hub "..."`.
- **Parser bị chép tay:** `DebugValues.Parseable` là bản sao bảng private `parseFunctions` của IDC; `ParseArgument` trả false lẫn lộn "sai cú pháp" với "không hỗ trợ kiểu".
- Hai UI khác nhau (canvas, font, palette), submodule phải clone kèm và hay dirty `.meta`.

### Tiêu chí

1. Mở trang log muộn vẫn đủ log từ đầu phiên.
2. Máy chưa mở khoá: không tốn gì.
3. Có log native (spec ② / ③, không thuộc spec này).
4. Hết phụ thuộc IDC.
5. Không mất tính năng xem log tester đang dùng; trang log phải trực quan và tối ưu, không làm tạm.

## 2. Phạm vi

| | |
|---|---|
| **Spec này (①)** | bộ ghi, trang log, mở khoá mới, parser tự giữ, gỡ IDC, gỡ symbol + xử lý `PRODUCTION` |
| ② sau | log native Android: Java thread đọc `logcat` của chính app từ lúc khởi động (máy được ghi log), giữ ring phía Java, trang log kéo theo lô một lời gọi JNI |
| ③ sau | log native iOS: `OSLogStore(scope: .currentProcessIdentifier)` (iOS 15+), hỏi lúc mở trang log |
| **Bỏ hẳn** | bắt crash / lưu log phiên crash (Android xem logcat là đủ); ghi log ra file; deep link mở khoá |

Bộ ghi phải nhận được nguồn khác (②③) mà không đổi hình: entry có `Source` (Unity / Native), giờ là `DateTime` để ghép theo thời gian. Không dựng interface cho nguồn khi mới có một nguồn.

## 3. Mở khoá

### 3.1 Hai quyền tách bạch

| Quyền | Điều kiện | Vì sao tách |
|---|---|---|
| **Được ghi log** | có cờ **hoặc** bản nội bộ | vô hình, chỉ RAM — reviewer Apple có bị ghi cũng không lộ gì; QA cài mới / xoá dữ liệu vẫn có log từ đầu phiên |
| **Được mở hub** | **chỉ** khi có cờ | reviewer Apple cài sandbox = giống TestFlight; lắc là cử chỉ vô tình |

Một hàm duy nhất cho mỗi quyền, dùng chung bởi hub và bộ ghi (hiện `unlocked` nằm riêng trong `DebugHub.Awake`).

### 3.2 Cờ

PlayerPrefs `DebugHub.AuthenticationState == 2` (giữ key/giá trị cũ để máy đã mở khoá không phải mở lại). Ghi bởi:

- **Password** gõ tay — dự phòng, mọi bản.
- **Dấu hiệu mạng công ty** (mục 3.4).

Cập nhật qua TestFlight / App Tester giữ dữ liệu app → mỗi máy mở khoá một lần.

### 3.3 Bản nội bộ (chỉ cấp quyền ghi log)

Tính lại mỗi lần mở app, không ghi cờ.

| Nền tảng | Là bản nội bộ khi | Kiểm |
|---|---|---|
| iOS | không cài từ App Store (TestFlight, ad-hoc qua App Distribution, Xcode) | tên file biên lai `sandboxReceipt` ≠ `receipt` |
| Android | cài qua app **App Tester** của Firebase App Distribution | `InstallSourceInfo` (API 30+); máy < Android 11 = không phải nội bộ |
| Editor | luôn | |
| PC exe, APK cài tay | **không** — file lộ ra ngoài là ai cũng cài được | |

Kiểm thẳng bằng native, không qua `Application.installerName` / `installMode`: `installerName` chỉ là app *thực hiện* cài (App Tester có thể nhờ trình cài hệ thống, khi đó tên ở trường *khởi tạo*), còn `installMode` dựa trên provisioning nên TestFlight bị báo là Store. Android: JNI `getInstallSourceInfo` (cả `getInitiatingPackageName` lẫn `getInstallingPackageName`). iOS: một hàm `.mm` đọc `appStoreReceiptURL`. Giả mạo được bằng `adb install -i` — làm được vậy là dev.

### 3.4 Dấu hiệu mạng công ty

Danh sách ở Inspector của `DebugHub` (thay field `networkReachabilityAuthenticationBypass`). Mỗi dòng một dấu hiệu, hai kiểu:

| Kiểu | Khớp khi | Giá trị của project này |
|---|---|---|
| Trang nội bộ | GET `url`, **không theo redirect**, status 200, body chứa `mustContain` | `http://10.10.0.204/` chứa `<title>Zego Dashboard</title>` |
| IP công khai | IP trả về bởi `https://www.cloudflare.com/cdn-cgi/trace` (dòng `ip=`) nằm trong danh sách | `123.24.205.180` |

- **Chỉ chạy khi ô password hiện ra** (sau cử chỉ bí mật), song song mọi dấu hiệu, khớp một cái là gọi đúng đường `AcceptAuthentication()` của password. UI không bao giờ chờ: ô password hiện ngay, khớp thì tự đóng.
- Timeout ~2 s. Lỗi / sai / hết giờ: im lặng, ô password vẫn đó.
- Bỏ điều kiện `Debug.isDebugBuild` và kiểm lúc `Awake` hiện tại: người chơi không bao giờ gửi request, iOS không hỏi quyền mạng cục bộ người chơi.
- Không dùng `ETag` / hash body cho kiểu trang: đổi mỗi lần đội dashboard deploy.
- Package editor script thêm `NSLocalNetworkUsageDescription` vào Info.plist nếu chưa có (tester iOS được hỏi một lần khi kiểu trang trỏ IP LAN).
- `http://` cần "Allow downloads over HTTP" = Always (project hiện là `insecureHttpOption: 2`). Ghi vào README.
- Giá trị trên là cấu hình của project, điền trên instance `DebugHub` trong `Root.unity` — package prefab để trống. Override cũ `networkReachabilityAuthenticationBypass.checkUrls` trong scene thành rác, Unity tự bỏ khi lưu lại.

### 3.5 Cử chỉ và bong bóng

- Máy **chưa có cờ**: cử chỉ bí mật (gõ 4 góc, vẽ, phím) → ô password (+ kiểm mạng). Lắc không có tác dụng.
- Máy **có cờ**: bong bóng **nhớ trạng thái hiện/ẩn qua phiên** (PlayerPrefs, mặc định ẩn). Mở khoá xong → hiện. Kéo vào X → ẩn và nhớ. Lắc / cử chỉ → hiện lại.
- Reviewer: không cờ → không bao giờ thấy gì.

### 3.6 Gỡ

`ALWAYS_ENABLE_INGAME_DEBUGGER`, `DISABLE_DEBUG_HUB` (giữ nhánh huỷ bản trùng trong `Awake`), `RenameFolderOnBuild.cs` (chỉ chạy dưới `DISABLE_DEBUG_HUB`; build hiện tại không đổi), `NetworkReachabilityAuthenticationBypass` + test, mọi xử lý `PRODUCTION`: bật tạm logger trong `DebugRegistry.Capture` + test của nó, bật logger trong `console.show`. Project tắt log Unity thì hub không có log để hiện — đúng như vậy.

## 4. Bộ ghi — `LogRecorder`

Static, không MonoBehaviour, không UI.

- Đăng ký `Application.logMessageReceivedThreaded` ở `RuntimeInitializeLoadType.SubsystemRegistration` nếu "được ghi log" (3.1). Chưa đủ quyền lúc khởi động mà mở khoá giữa phiên → bắt đầu ghi từ lúc đó.
- **Ring trong RAM, giới hạn theo dung lượng chuỗi**: 4 MB (`message.Length + stack.Length` × 2 byte). Đầy thì bỏ cũ nhất, đếm `Dropped`. Hằng số có ghi chú `ponytail:` — chỉnh khi đo thật; nâng cấp nếu thiếu: dùng chung instance cho stack giống hệt.
- Entry: `Seq` (long tăng dần), `Time` (`DateTime.Now` — an toàn mọi thread), `Type` (`LogType`), `Message`, `Stack`, `Kind` (Log / Command), `Source` (Unity; Native cho ②③).
- `lock` một object; callback không gọi Unity API.
- **Vạch command**: `DebugRegistry` ghi một entry `Kind = Command` (dòng lệnh) trước khi chạy → trang log vẽ vạch `▶ level.goto 5`.
- API đọc: `CopySince(long seq, List<LogEntry> into)` trả các entry có `Seq > seq`; `OldestSeq`, `StartedAt`, `Dropped`. Không cấp phát khi không có gì mới.
- Không ghi file. App tắt là mất.

Chi phí: máy không được ghi → 0 (không đăng ký callback thì Unity không marshal chuỗi log sang C#, nếu không ai khác nghe). Máy được ghi → hai chuỗi marshal + một lock + một ô mảng mỗi log.

## 5. Trang log

Mockup đã duyệt (2026-10-01): danh sách + trang chi tiết, palette/font của hub hiện tại.

### 5.1 Vị trí và đường vào

- Một trang trong panel hub (`DebugPage` + view riêng), dùng chung `‹`, `×`, bấm nền để đóng, ô tìm ở header. **Panel giữ chiều cao tối đa cố định** khi đang ở trang log (các trang khác vẫn co theo nội dung).
- Nút **Log** ở header Commands gốc, cạnh công cụ và `?`, có số lỗi mới.
- Bấm dòng kết quả command → mở trang log, cuộn tới vạch của lần chạy đó.
- Chấm đỏ + số lỗi mới (từ lần xem trang log gần nhất) trên bong bóng.
- Gỡ `console.show`, `console.auto`.

### 5.2 Danh sách

- **Ảo hoá**: dòng cao cố định, pool ~ số dòng nhìn thấy + 2 (≈15), đặt vị trí theo chỉ số; content height = số dòng × chiều cao. Vạch command cùng chiều cao dòng log: vị trí mọi dòng = chỉ số × chiều cao, không cần bảng cộng dồn (đánh đổi: vạch thoáng hơn mockup).
- Danh sách lọc lưu **Seq**, không lưu vị trí ring; ring bỏ cũ thì cắt các Seq < `OldestSeq`.
- **Một dòng**: icon theo loại (chấm / tam giác / tròn ×) — khác hình, không chỉ khác màu; nội dung tối đa 2 dòng, cắt `…`; dòng phụ: `HH:mm:ss.fff – nơi gọi`; badge `×N` khi đang gộp. Chỉ dùng ký tự ngoài chữ cái mà README cho phép (`› ‹ … – — ×`): `·`, `▶`, `↓` không chắc có trong font game nên thay bằng `–`, `›` và icon vector. Dòng lỗi nền đỏ nhạt, chữ hồng nhạt. Dòng đang chọn có viền trái mint.
- **Nơi gọi** = frame đầu tiên thuộc code game: bỏ frame `UnityEngine.Debug*`, `UnityEngine.Logger*`, `Hlight.Logging.*`, `LogTag`, `LogAlways`. Không có stack → dòng phụ chỉ có giờ. Tính một lần khi entry lần đầu được vẽ, giữ lại.
- **Đầu danh sách**: "Ghi từ HH:mm:ss" (+ "· đã bỏ N log cũ nhất" khi `Dropped > 0`).
- **Bám đáy**: chỉ tự cuộn theo khi đang ở đáy. Đang cuộn lên → không đẩy danh sách, hiện viên "↓ N log mới", bấm là xuống đáy.
- Nội dung log là dữ liệu game: **bọc `<noparse>`** (chuỗi `</noparse>` trong log bị chèn ký tự zero-width để không thoát ra được) thay vì tắt rich text — tắt thì mất tô sáng chữ khớp khi tìm.
- Rỗng: "Chưa có log" / "Không có log khớp bộ lọc" + nút bỏ lọc.

### 5.3 Thanh lọc (dưới header)

- Ba chip Log / Cảnh báo / Lỗi, bấm bật/tắt; **số đếm là tổng**, không đổi theo ô tìm. `Error`, `Exception`, `Assert` tính là Lỗi.
- Chip **Gộp**, mặc định **tắt** (dòng thời gian đúng thứ tự có giá trị hơn). Bật: gộp theo (loại, nội dung, stack), giữ vị trí lần đầu (không nhảy), đếm số lần, nhớ giờ lần cuối.
- Ô tìm của header: khớp nội dung + nơi gọi, không phân biệt hoa thường, phần khớp được tô. Gom gõ 120 ms như các trang khác.
- Nút `…`: **Copy tất cả** (theo bộ lọc hiện tại, dạng text có giờ + loại + nội dung + stack; quá 500k ký tự thì giữ phần mới nhất), **Xoá** = ẩn mọi entry có Seq ≤ hiện tại. **Hoàn tác** nằm ở dòng đầu danh sách ("đã ẩn N log, bấm để hiện lại"), không ở dòng kết quả — bấm dòng kết quả đã có nghĩa là "mở log tới command vừa chạy".

### 5.4 Trang chi tiết

- Tiêu đề = loại, tô màu theo loại. Dòng phụ = giờ (+ "– lặp N lần, lần cuối …" khi gộp).
- Nội dung quá 4000 ký tự thì cắt khi hiện, Copy vẫn lấy đủ.
- Nội dung đầy đủ trong khối nền.
- **Stack trace**: mỗi frame một dòng; frame game chữ sáng + `file:dòng` dưới; frame engine/thư viện mờ.
- Một nút chính **Copy** (nội dung + stack). **‹ Trước / Sau ›** đi qua các entry của bộ lọc hiện tại.
- Font của game (không mang font riêng).

### 5.5 Tối ưu (ngân sách)

- Không có log mới và không cuộn: 0 cấp phát mỗi frame.
- Cuộn: chỉ gán chuỗi đã cache cho dòng pool.
- Lọc / tìm lại trên 5k entry: ≤ 5 ms (đo trong Editor, ghi vào test như mốc tham khảo, không assert thời gian).
- Gộp: bảng băm, O(1) mỗi entry mới.
- Trang log tự lấy entry mới mỗi frame qua `CopySince` (không qua cơ chế Live 4 Hz của panel — Live dựng lại toàn trang).

## 6. Parser tự giữ

- Mang từ `DebugLogConsole` của IDC (MIT, giữ header bản quyền tác giả): bộ tách đối số (`FetchArgumentsFromCommand` + nhóm nháy/ngoặc), bảng parse theo kiểu, `ParseArgument` (enum, mảng, `Component`, `GameObject`), tên kiểu dễ đọc. ~600 dòng, cắt phần hub không dùng.
- Gom vào `DebugValues` (có thể tách `partial` sang file thứ hai). Bảng parse là **nguồn duy nhất**: `Parseable` suy ra từ khoá của bảng, hết bản chép.
- Giữ nguyên hành vi: `DebugValuesTests`, `AddressTests`, `DebugRegistryTests` phải xanh không sửa kỳ vọng.
- Nơi đang gọi IDC: `Address.cs`, `DebugRegistry.cs`, `DebugValues.cs`, `HelpPage.cs`, `DebugHubPanel.cs`, `Reflect.cs`.

## 7. Dòng kết quả command

Giữ callback riêng sống trong lúc command chạy (`DebugRegistry.Capture`), chỉ bỏ phần bật tạm logger. Không đọc từ bộ ghi vì `DebugHub.Execute` là API public gọi được trên máy không ghi log, và bộ ghi nhận log mọi thread (log thread khác in đúng lúc sẽ bị tính nhầm). Bộ ghi chỉ thêm vạch command để nhảy tới.

## 8. Gỡ IDC

| Gỡ | Thay |
|---|---|
| submodule `ThirdParty/UnityIngameDebugConsole`, `.gitmodules`, reference `IngameDebugConsole.Runtime` trong `Hlight.Debug.Hub.asmdef` và `Tests/Editor/Hlight.Debug.Hub.Tests.asmdef` | — |
| `Resources/IDC Variant.prefab` + folder `Resources` | — |
| `IngameDebugConsole.EventSystemHandler` trên gốc `DebugHub.prefab` | component ~40 dòng của hub, cùng logic (bật EventSystem nhúng khi scene chưa có, đổi sang `InputSystemUIInputModule` khi chỉ có Input System). Sửa prefab qua Unity MCP hoặc `unity cmd` + `PrefabUtility.LoadPrefabContents` |
| lệnh cầu `hub "..."`, `console.show`, `console.auto`, `resourcePath` | nút Log |
| `ConsoleController` | đổi tên `BuiltinCommands`, **giữ GUID `.meta`** để prefab không mất reference; còn `prefs.*`, `time.*`, `sdk.*`, `hub.*`, `console.proxima` |

## 9. Tài liệu và phiên bản

- README: bỏ hướng dẫn submodule, bảng Symbol, đoạn `PRODUCTION`, trần "IDC quét mọi assembly"; viết lại phần mở khoá (3.1–3.5), thêm phần trang log; ghi chú bản quyền MIT của phần parser.
- CHANGELOG 3.0.0 — breaking: gỡ 2 symbol, gỡ lệnh `hub "..."`, `console.show`/`console.auto`, đổi field mạng trên `DebugHub`.

## 10. Test (EditMode, `Hlight.Debug.Hub.Tests`)

- `LogRecorder`: Seq tăng; vượt 4 MB bỏ cũ nhất + `Dropped`; `CopySince` đúng biên; ghi từ thread nền an toàn (một test nhiều thread ghi, đếm đủ).
- View model trang log (tách khỏi UI): lọc theo loại, đếm tổng không đổi theo tìm, tìm khớp nội dung + nơi gọi, gộp giữ vị trí lần đầu, cắt Seq < `OldestSeq`, Xoá/Hoàn tác, bám đáy vs viên "log mới".
- Trích nơi gọi: bỏ frame Debug / Logger / Hlight.Logging; stack IL2CPP không số dòng; stack rỗng.
- Mở khoá: hai quyền theo (cờ, nội bộ); so dấu hiệu — body chứa / không chứa, status khác 200, redirect, dòng `ip=` khớp / không khớp. Không gọi mạng thật.
- Parser: test sẵn có giữ nguyên.
- Prefab: `DebugHubPanelTests` chuyển từ `EventSystemHandler` của IDC sang component mới.

Lệnh: `unity cmd --project-path . run_tests --mode EditMode --filter "Hlight.Debug.Hub.Tests" --filter_type assembly`.

## 11. Phải kiểm trên máy thật

1. `PlayerPrefs` đọc được ở `SubsystemRegistration` (không thì lùi `AfterAssembliesLoaded`).
2. `Application.installerName` / `installMode`: TestFlight có bị báo là Store không; App Tester hiện ở trường nào của `InstallSourceInfo`.
3. iOS: hộp thoại quyền mạng cục bộ chỉ hiện với tester, một lần.
4. Định dạng stack IL2CPP release để trích nơi gọi.
5. Lilita One ở cỡ chữ của dòng log: đọc được, đủ dấu tiếng Việt.

## 12. Trần đã biết

- Phiên mở khoá lần đầu trên máy không phải bản nội bộ chỉ có log từ lúc mở khoá.
- IP công khai văn phòng chưa biết tĩnh hay động; dashboard có thể đổi tên/chuyển máy — giữ cả hai dấu hiệu để chỉ hỏng khi cả hai cùng hỏng; sửa = một ô Inspector, theo build kế tiếp.
- Dòng cao cố định: log một dòng thừa chỗ; đổi lại cuộn mượt.
- Bản lậu ký lại / adb giả installer: không quan tâm.
