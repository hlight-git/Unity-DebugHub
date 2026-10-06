# Hlight Debug Hub

In-game debug hub: cheat command theo cây path, hệ inspect bằng reflection, trang log — tất cả sau một password. Hub nằm được cả trong bản store: chưa mở khoá thì gần như không tốn gì.

## Cài đặt

Thêm package vào `Packages/` (embedded) hoặc qua git URL. Không cần clone kèm gì khác.

## Thiết lập

1. Kéo `Prefabs/DebugHub.prefab` vào scene đầu tiên.
2. Chọn object `DebugHub` trong scene → component **DebugHub** → điền ô **Password** → lưu scene. Để trống thì hub không mở được (có log lỗi lúc chạy).

Người dùng mở hub bằng trigger (vẽ 4 góc, phím tắt…) → gõ password → máy nhớ trạng thái đã mở khoá (PlayerPrefs `DebugHub.AuthenticationState`).

### Mở khoá

| Quyền | Điều kiện |
|---|---|
| Được ghi log (vô hình, chỉ RAM) | đã mở khoá **hoặc** bản nội bộ: iOS không cài từ App Store (TestFlight, ad-hoc, Xcode), Android cài qua app **App Tester** của Firebase App Distribution, Editor. APK cài tay / exe **không** tính. |
| Được mở hub | chỉ khi đã mở khoá (PlayerPrefs `DebugHub.AuthenticationState`) |

Mở khoá bằng password, hoặc tự động khi máy tới được một **trang nội bộ** của công ty (`Auto Unlock › Pages` trên component DebugHub, kiểm song song, khớp một trang là đủ — UI không chờ):

- **Android, Windows, Linux, Editor**: kiểm ngầm lúc mở app và mỗi lần app quay lại màn hình, tới khi mở khoá. Khớp là mở khoá **ngầm** (bắt đầu ghi log, không hiện gì) như bản 2.x: lắc hoặc vẽ 4 góc là bong bóng hiện, dùng được ngay từ lần mở app đầu tiên. Ô password đang mở lúc khớp thì ô tự đóng và bong bóng hiện.
- **iOS, tvOS, visionOS, app macOS**: chỉ kiểm khi ô password mở (sau cử chỉ). Lần đầu chạm mạng cục bộ các nền tảng này hiện hộp xin quyền (macOS từ 15); kiểm lúc mở app thì mọi người chơi và reviewer Apple đều thấy hộp đó.

Mỗi trang:

- **Url**: trang chỉ mở được trong mạng công ty, ví dụ `http://10.10.0.204/`. Không theo redirect, chờ tối đa 2 s.
- **Must Contain**: chuỗi bắt buộc có trong trang, ví dụ `<title>Zego Dashboard</title>` (dùng tiêu đề, không dùng ETag/hash — đổi mỗi lần deploy).

Thêm trang của server khác để dự phòng: một server đổi IP hay sập thì bản đã phát hành vẫn tự mở khoá.

Ngoài công ty request hỏng trong ≤ 2 s, người chơi không thấy gì. `http://` cần "Allow downloads over HTTP" = Always. iOS: package tự thêm `NSLocalNetworkUsageDescription` khi build; lần đầu tester được hỏi quyền mạng cục bộ và lần kiểm đó hết 2 s trước khi kịp bấm — cho phép xong thì đóng rồi mở lại ô password. Bấm "Không cho phép" thì phải bật lại trong Cài đặt › Quyền riêng tư › Mạng cục bộ.

Bong bóng mặc định ẩn, nhớ trạng thái qua phiên (PlayerPrefs `DebugHub.EntryVisible`) — chỉ nhớ thao tác cố ý: gõ đúng password / cử chỉ gọi lại thì hiện (mở khoá ngầm qua mạng công ty thì không), kéo vào nút X thì ẩn, `hub.entry`. Ẩn tạm (`DebugHub.Visible = false`, `hub.hide`, `.HidesHub()`) không nhớ, phiên sau bong bóng vẫn hiện. Lắc chỉ có tác dụng khi đã mở khoá — reviewer Apple (cài sandbox như TestFlight) không thấy gì.

## Node

Mọi thứ hiện lên panel là một trong bốn loại `DebugNode`, đăng ký qua `DebugHub`:

| Loại | Là gì | Field chính |
|---|---|---|
| `ActionNode` | một việc chạy được | `Parameters[]`, `Invoke` |
| `ValueNode` | một giá trị đọc được, ghi được nếu `Set != null` | `Declared`, `Get`, `Set`, `Address`, `Options` |
| `FolderNode` | có con, liệt kê lúc mở (không giữ sẵn) | `Children`, `Inline`, `Live` |
| `TextNode` | chữ: mô tả, ghi chú, bảng đã format | `Text`, `Style` |

```csharp
DebugHub.Add(this, "level.next", "Sang level kế tiếp.", NextLevel);                 // ActionNode
DebugHub.Add<int>(this, "level.goto", "Nhảy tới level bất kỳ.", GoToLevel);          // ActionNode
DebugHub.AddValue(this, "view.ui", "Ẩn/hiện toàn bộ UI game.", () => UiVisible, SetUi);   // bool → switch
DebugHub.AddValue(this, "time.scale", "Time scale hiện tại.", () => Time.timeScale, SetTimeScale); // float → field
DebugHub.AddFolder(this, "info.app", "Bấm một dòng để copy.", AppInfoNodes);         // FolderNode
DebugHub.Remove(node);
DebugHub.Execute("level.goto 5", out var message);
```

- Tham số đầu là **owner**: owner bị `Destroy` thì node tự rụng. `null` = sống suốt phiên. Owner đã bị huỷ ngay lúc đăng ký thì node cũng rụng (không sống mãi).
- Tên tham số của `Add<T...>` lấy từ chính delegate, truyền thêm string chỉ khi muốn tên khác.
- Path phải để tên lá tự nói được nó làm gì: row chỉ hiện segment cuối.
- `Path` không phải key: hai `ActionNode` trùng path khi khác số tham số là hợp lệ (row kèm `(N args)`). `ValueNode`/`FolderNode` chiếm riêng path của mình. Trùng thật thì `Debug.LogError` (không throw) và node trả về không đăng ký.

### Hàm nối

```csharp
DebugHub.Add(this, "save.wipe", "Xoá toàn bộ save.", Wipe).Confirms();
DebugHub.AddValue(this, "view.ui", "...", get, set).HidesHub();
DebugHub.Add<float, float>(this, "time.skip", "...", FastForward).Defaults("1", "100");
```

| Hàm | Làm gì |
|---|---|
| `.Stays()` | giữ nguyên panel sau khi chạy |
| `.HidesHub()` | ẩn cả entry, cho ảnh chụp sạch |
| `.Confirms()` | chạy phải qua trang xác nhận (kể cả từ nút repeat) |
| `.Reports()` / `.Silent()` | luôn / không bao giờ hiện dòng kết quả |
| `.Defaults(params string[])` | seed tham số ban đầu, chỉ khi người dùng chưa nhập gì |
| `.Priority(int)` | thứ tự trong cây Commands như `MenuItem`: số nhỏ lên trước (mặc định 0, âm để lên đầu) |

Mặc định: `ActionNode` → đóng panel; `ValueNode` → ở lại; node do reflection sinh → ở lại.

### Row suy từ node — `NodeRenderer`

`NodeRenderer.Render` là nơi duy nhất biết node nào ra row nào. Xét từ trên xuống:

| Node | Điều kiện | Row |
|---|---|---|
| `ValueNode` | `Get()` ném exception | dòng lỗi |
| `ValueNode` | có `Options` (`Node.Choice`) và setter | row chọn trong danh sách lấy lúc dựng trang |
| `ValueNode` | `Get()` ra null (kể cả fake-null), trừ `string`/`int?`… có setter | row `null`, có nút `…` |
| `ValueNode` | giá trị đơn (số, bool, enum, vector, `int?`…), `Set == null` | giá trị căn phải, **bấm = copy** |
| `ValueNode` | `bool` có setter | switch |
| `ValueNode` | enum không `[Flags]` có setter | row chọn giá trị (page chọn) |
| `ValueNode` | giá trị đơn có setter | field tại chỗ, chốt bằng `onEndEdit` |
| `ValueNode` | còn lại (object, collection, `GameObject`…) | row nav → member/phần tử |
| `ActionNode` | không tham số | row chạy ngay |
| `ActionNode` | có tham số (hoặc awaitable) | row nav → page nhập liệu + nút **Chạy** |
| `FolderNode` | `Inline == false` | row nav, không gọi `Children` |
| `FolderNode` | `Inline == true` | tiêu đề + con dựng tại chỗ |
| `TextNode` | | chữ theo `Style` |

`FolderNode` chỉ gọi `Children` khi trang của nó đang mở. `Children` (hay bất kỳ trang nào) ném exception thì trang hiện một dòng lỗi thay vì vỡ panel; trang Live ném liên tục thì chỉ log một lần.

### `Execute`

| Node | 0 đối số | 1 đối số | khác |
|---|---|---|---|
| `ActionNode` | chạy nếu không tham số | chạy nếu arity khớp | theo arity |
| `ValueNode` | trả giá trị hiện tại | parse theo `Declared` rồi `Set` | lỗi |
| `FolderNode` | lỗi | lỗi | lỗi |

Tách tham số bằng `DebugValues.SplitArguments`: quote và ngoặc (lồng được) là một đối số.

`$ten` trong đối số là biến (xem Advanced); chuỗi thật bắt đầu bằng `$` viết `$$`.

### Ẩn nhanh cả hub

```csharp
DebugHub.Visible = false;   // đóng panel + ẩn entry (chỉ phiên này, không nhớ qua phiên)
DebugHub.Visible = true;    // hiện entry lại (chỉ ăn khi đã mở khoá)
```

## Panel

Một panel duy nhất điều hướng theo stack. Bấm nền ngoài = đóng (giữ stack, mở lại về đúng trang); `‹` = lùi một tầng; `×` = đóng. Gốc panel là cây command: path tách theo `.` thành cây trang (`prefs.set.int` → `Commands › prefs › set › int`).

- Entry bubble (như chat head Messenger): thả tay là bubble trôi theo đà rồi dính mép trái/phải gần điểm dừng — điểm dừng = chỗ nhấc tay + vận tốc × `momentum` (mặc định 0.18s, chỉnh ở Inspector). Thả nhẹ về mép gần, hất mạnh sang mép kia; dừng tay rồi mới nhấc thì không trôi. Kéo vào nút X ở đáy là bị hút vào, thả ra để ẩn hub.
- Header: kính lúp (tìm), thanh điều chỉnh (công cụ/Advanced), `?` (trợ giúp) và Log — ba nút sau chỉ ở Commands gốc. Tiêu đề tự co tới 80% cỡ chữ khi hẹp chỗ.
- Trợ giúp (`?`): ghi chú của game (`DebugHub.Notes.Add(...)`, chỉ hiện khi có), hai trang tra **Tất cả lệnh** (cùng thứ tự cây Commands, kèm kiểu tham số và mô tả; không gọi getter) và **Cú pháp address** (bấm một dòng là copy ví dụ), rồi cách mở / ẩn hub, cách đọc một dòng, header và nút nổi. Cách mở hub đọc từ chính các trigger đang gắn: trigger tự viết override `Hint` để có dòng của mình (null = không nhắc).
- Thứ tự mỗi tầng, như `MenuItem` của Unity: `.Priority(n)` nhỏ lên trước; thư mục đứng ở priority nhỏ nhất trong nó; thư mục và lệnh xen nhau theo priority. Trùng priority thì thư mục trước, thư mục theo tên, lệnh theo thứ tự đăng ký. Kết quả tìm không theo priority. Không có đường kẻ ngăn nhóm như menu Unity.
- Category gốc có nút sao: sao đặc = yêu thích, được đưa lên nhóm **Yêu thích** (cùng luật thứ tự ở trên), lưu PlayerPrefs.
- Tìm ở một trang command chỉ lọc nhánh đó; ở gốc là toàn registry. Không quét vào `FolderNode` động. Trang có list riêng (member, type, instance, Objects) tự lọc list của nó.
- Danh sách dài chia trang 40 mục.
- Giá trị hiển thị là dữ liệu của game nên được bọc `<noparse>`; description do code viết thì vẫn dùng rich text của TMP (`<size=80%>`…).

### `…` — thao tác trên một `ValueNode`

| Mục | Hiện khi |
|---|---|
| Copy giá trị | giá trị hiển thị được thành text |
| Gán giá trị | có setter **và** không có editor tại chỗ (reference, `null`) |
| Ghim / Bỏ ghim | có `Address`, không bắt đầu bằng `$` |
| Bỏ biến | chính biến `$ten` (trang Objects) |
| Lưu vào `$…` | giá trị khác null; tên biến chỉ gồm chữ, số, `_` |

### Nút repeat

`hub.repeat` bật một nút nổi cạnh entry: icon + **tên lá của lệnh cuối**, bấm là chạy lại đúng dòng lệnh đó.

- Lưu dòng lệnh (`path + args`, PlayerPrefs `DebugHub.LastCommand`). Luật ghi chỉ có một bản cho panel, `Execute` và nút repeat: `ActionNode` chạy xong là ghi (kể cả không tham số), `ValueNode` chỉ ghi khi gán. Đối số là reference Unity thì xoá bản lưu.
- Chạy lại qua đúng luồng của panel: xác nhận (`Confirms()` → mở trang xác nhận), dòng kết quả, `HidesHub`. Command đã mất đăng ký thì báo lỗi và nút tự ẩn.
- Ẩn khi kéo bubble, và khi hub bị ẩn.

### Dòng kết quả

Log mà command in ra trong lúc chạy hiện ở dòng nổi dưới đáy (ngoài panel), chỉ với node có `ShowsResult` (mặc định = `Dismiss == Stay`, ghi đè bằng `.Reports()`/`.Silent()`). Lỗi luôn hiện. Dài quá thì cắt (không cắt giữa một tag); bấm để mở trang log tại vạch của chính lần chạy đã tạo dòng đó (dòng không do command tạo — copy, lỗi nhập — mở ở cuối log).

### Font

Package **không mang font**. Mọi TMP để trống font nên TMP lấy `TMP_Settings.defaultFontAsset` của game. Điều kiện để hiện đúng:

- Font mặc định có tiếng Việt và `› ‹ … – — ×` (hub chỉ dùng đúng các ký tự này ngoài chữ cái; icon là hình vẽ vector, không phải glyph; trang log chỉ dùng thêm icon vector, không thêm glyph). Kiểm bằng `FontEngine.TryGetGlyphIndex` trên TTF/OTF nguồn, không phải `HasCharacter`.
- Font Dynamic thì nên bật **Clear Dynamic Data On Build**: không thì glyph sinh ra trong Editor (kể cả từ chữ của hub) được lưu vào asset và đi vào mọi build.

## Trang Log

Nút Log ở header Commands gốc (kèm chấm đỏ số lỗi chưa xem). Nút nổi (entry) là ô đếm log mới chưa xem theo loại — Log / Cảnh báo / Lỗi, như IngameDebugConsole: luôn hiện ba số (số 0 mờ đi), cỡ cố định đủ cho `99+` để không lệch khỏi mép đang dính; bấm vẫn mở cây command; mở trang log là về 0. Bộ ghi (`LogRecorder`) chạy từ lúc khởi động trên máy được ghi (xem **Mở khoá**), giữ tối đa 4 MB chuỗi trong RAM, không ghi file (4 MB là ngân sách **chuỗi**: mảng ring + một `LogItem` mỗi log nằm ngoài con số đó, nên với log rất ngắn bộ nhớ thật trên máy tester có thể lên ~15–20 MB); máy không được ghi thì không đăng ký callback nào, chi phí 0. Đầu danh sách luôn nói ghi từ lúc nào và đã bỏ bao nhiêu log cũ.

- Panel giữ chiều cao tối đa cố định ở trang log và trang chi tiết, khỏi nhảy mỗi lần có log mới.
- Chip Log / Cảnh báo / Lỗi bật tắt từng loại; số đếm là tổng, không đổi theo ô tìm. Chip Gộp (mặc định tắt) gộp log trùng (cùng loại, nội dung, stack) tại vị trí lần đầu, hàng ghi `×N`.
- Tìm bằng ô tìm của header: khớp nội dung và nơi gọi, phần khớp được tô. Bộ lọc ẩn hết log thì có nút **Bỏ lọc** (bật lại cả ba loại, xoá từ khoá và đóng ô tìm).
- Mỗi hàng: icon theo loại (khác hình, không chỉ khác màu), 2 dòng nội dung, `HH:mm:ss.fff – nơi gọi`; hàng lỗi nền đỏ nhạt. Nơi gọi = frame đầu tiên thuộc code game (không phải engine, `Debug`, `com.hlight.logging`); stack không có frame game nào thì lấy frame đầu tiên không thuộc lớp log. Vạch `› lệnh` đánh dấu lúc chạy command; bấm dòng kết quả mở trang log đúng tại vạch đó.
- Đang ở đáy thì bám theo log mới; cuộn lên thì hiện "N log mới".
- Bấm hàng → trang chi tiết: nội dung đầy đủ (quá 4000 ký tự thì cắt khi hiện, Copy vẫn lấy đủ), stack từng frame (game sáng, engine mờ; kiểu đối số rút gọn, bỏ namespace), dòng phụ `HH:mm:ss.fff` (log gộp: `HH:mm:ss.fff – ×N, cuối HH:mm:ss`). Thanh **‹ Trước / Copy / Sau ›** ghim ở đáy window, ngoài vùng cuộn: Copy lấy nội dung kèm stack gốc, Trước/Sau đi qua các log của bộ lọc hiện tại.
- `…`: Copy tất cả (theo bộ lọc, quá 500k ký tự giữ phần mới nhất), Gửi qua message (khi có Messenger, xem **Báo lỗi và message**), Xoá (ẩn mọi log tới lúc này; dòng đầu danh sách thành "đã ẩn N log, bấm để hiện lại", danh sách trống thì ghi "Đã xoá – chưa có log mới").
- Nội dung log là dữ liệu game: vào TMP qua `<noparse>`, và mọi `</` trong dữ liệu bị chèn ký tự zero-width để không tag đóng nào thoát ra được. Ký tự font không có (`→`, emoji…) hiện thành `?` — không thì TMP bắn warning mỗi lần vẽ và warning lại thành log mới; Copy và tìm vẫn trên chuỗi gốc.

### Logcat (Android)

Trên Android trang log có cả **logcat của chính tiến trình game**: log Java/native của SDK (AppLovin, Firebase, UnityAds…), lỗi Java. Từ Android 4.1 app chỉ đọc được log của nó — log của tiến trình khác (như Android Logcat trong Editor hiện) thì không app nào đọc được.

- Bắt đầu ghi (khởi động hoặc mở khoá giữa phiên): lấy lại phần buffer logcat còn giữ từ lúc tiến trình khởi động, kể cả log Unity từ trước lúc bắt đầu ghi — phiên đầu không hụt đầu, miễn buffer chưa trôi (vài phút trên máy bận). Dump này chạy đồng bộ (~50–150 ms, chỉ trên máy được ghi), rồi stream phần mới trên thread nền.
- V/D/I là Log, W là Cảnh báo, E/F/A là Lỗi. Dòng phụ hiện tag thay nơi gọi; tìm khớp cả tag; Copy ghi `Tag: nội dung`. Các dòng liên tiếp cùng header (stack Java) gộp một log.
- Log tag `Unity` trong logcat bỏ qua sau khi callback Unity gắn: callback có mọi log C# và log engine đi qua log handler, kèm stack chuẩn hơn. Vài dòng engine chỉ in ra logcat (`UnloadTime`, `Unloading N unused Assets`…) không qua callback nên hub không có — logcat không phân biệt được chúng với log đã có, nên không đoán.
- Chấm đỏ và ô đếm trên entry chỉ đếm log Unity: E của OS/SDK có ở mọi phiên. Dùng chung ngân sách 4 MB với log Unity.
- Chip **Unity** trên thanh lọc (hiện khi đã có log native, hoặc khi đang bật): bật thì chỉ còn log Unity, kể cả log native tới sau; **Bỏ lọc** tắt nó.

### Log hệ thống (iOS)

Trên iOS (15+) trang log có log hệ thống của chính tiến trình qua `OSLogStore`: NSLog / os_log của SDK (AppLovin, Firebase…), lỗi mạng của CFNetwork. Plugin `Plugins/iOS/DebugHubOsLog.mm` (link `OSLog.framework`).

- Tag là thư viện ghi log (`AppLovinSDK`, `CFNetwork`…). Debug/info/notice là Log, error/fault là Lỗi (os_log không có mức cảnh báo).
- Không stream được như logcat: đọc theo lượt mỗi 2 s trên thread nền. Lượt đầu (đồng bộ, trước khi gắn callback) lấy từ lúc tiến trình khởi động.
- Log Unity được nhận ra chính xác: từ SubsystemRegistration (mọi máy) hub thay handler os_log của Trampoline bằng bản y hệt ghi vào subsystem riêng `com.hlight.debughub.unity`, rồi bỏ các entry đó sau khi callback gắn. Log SDK link tĩnh (cũng ra từ `UnityFramework`) vẫn giữ, tag `UnityFramework`; vài dòng khởi tạo engine trước SubsystemRegistration cũng mang tag đó. Có debugger gắn thì Unity ghi ra stdout như Trampoline, không vào os_log.
- SDK log bằng os_log kiểu riêng tư thì giá trị hiện `<private>`; log mức debug thường không được hệ thống giữ.
- Chip **Unity** và luật đếm (chấm đỏ, ô đếm) giống Android.

## Báo lỗi và message

Chỗ cho project gửi lên API riêng: **báo lỗi** (tạo issue) và **message** (chữ, đẩy log, không file). Hub lo form và log; endpoint, payload, token, đăng nhập, thông tin game/máy là việc của class con.

1. Viết class con của `BugReporter` và/hoặc `MessageSender`.
2. Thêm component đó vào **chính object DebugHub** (object này `DontDestroyOnLoad`; đặt trên GameObject khác trong scene đầu thì nó bị unload sau boot và row `hub.report` / `hub.message` biến mất theo).
3. Kéo vào ô **Reporter** / **Messenger** của component DebugHub. Ô trống = không có row.

```csharp
public class ApiBugReporter : BugReporter
{
    private string title = "";
    private Priority priority = Priority.Normal;

    public override IEnumerable<DebugNode> Fields()
    {
        yield return Node.Value("title", () => title, v => title = v);
        yield return Node.Value("priority", () => priority, v => priority = v);
    }

    public override async Task<string> Send(BugReport report)
    {
        if (title.Length == 0) throw new Exception("Chưa có title");   // form còn mở, sửa rồi gửi lại
        var sent = title;
        var payload = new IssuePayload { title = sent, priority = priority.ToString(), logs = report.Logs,
            version = Application.version };
        // await POST payload…
        if (title == sent) title = "";   // trong lúc chờ QA đã gõ cho lỗi kế tiếp thì giữ nguyên
        return "BUG-123";
    }
}
```

| | `hub.report` | `hub.message` | Log › `…` › **Gửi qua message** |
|---|---|---|---|
| Form | `Fields()` + Gửi | `Fields()` + Gửi | "Kèm N log (theo bộ lọc)." + `Fields()` + Gửi |
| Hub đưa | `BugReport.Logs`: mọi log sau mốc Xoá, bỏ qua lọc / tìm / Gộp, chụp lúc bấm Gửi | `DebugMessage.Logs = null` | `DebugMessage.Logs`: theo bộ lọc như Copy tất cả, chụp lúc mở form |

- `Fields()`: mọi ô của form — `Node.Value`, `Node.Choice` (danh sách lấy lúc dựng trang: người nhận, assignee), `Node.Text`. Gọi lại mỗi lần trang dựng lại: rẻ, không side effect. `Node.Choice` đọc danh sách ngay lúc dựng trang nên danh sách từ API phải được class con tải/cache sẵn (ví dụ trong `Start`/`OnEnable`) — `Fields()` tự nó không có side effect. Giá trị do class con giữ, tự quyết giữ hay xoá sau khi gửi.
- `Send`: đọc field trước `await` đầu tiên; xoá field sau `await` thì chỉ xoá khi nó vẫn là giá trị vừa gửi (QA mở lại form gõ tiếp được trong lúc chờ). Trả chữ cho dòng kết quả (null = "Đã gửi báo lỗi." / "Đã gửi."). Ném = lỗi: hỏng ngay (validate) thì form còn mở; hỏng sau thì dòng kết quả đỏ, mở lại form vẫn còn giá trị. Phải tự có timeout.
- Log › `…` › **Gửi qua message** chỉ hiện khi đã gán Messenger **và** bộ lọc hiện tại còn ít nhất 1 log.
- Bấm Gửi: panel đóng, dòng kết quả "Đang gửi…" rồi kết quả. Mỗi kênh một lần gửi một lúc. Không vào nút repeat. Hub đang ẩn thì kết quả chỉ vào log.
- API thêm trường: sửa payload / `Fields()` / `Send` của class con. Package chỉ đổi khi API cần thứ chỉ hub có (ảnh/video) — lúc đó thêm field vào `BugReport`, class con cũ không vỡ.

## Advanced

Mở bằng nút công cụ: **Objects** (address đã ghim + biến `$`) và **Duyệt** (Assembly → Type → Instance → member).

### Address

```
address = root ( "." member | "[" args "]" | "." Method(args) )*

root = TypeName        full name tra thẳng từng assembly; tên ngắn phải quét và mơ hồ thì trả null
     | $var            giá trị hoặc type đã lưu trong Vars
     | #TypeName[i]    instance thứ i đang sống (kể cả inactive), đánh số theo InstanceID
     | @command.path   một ValueNode đã đăng ký
```

Ví dụ: `@economy.coin`, `#UnityEngine.Camera[0].fieldOfView`, `Namespace.Type.Items[2].Value`, `$player.Find<UnityEngine.Transform>("Arm")`, `Pick{1}(5)` (chọn overload thứ 1).

- Indexer: `List`, `Dictionary`, indexer tự viết, **và mảng** (kể cả mảng nhiều chiều `[1 2]`). Ghi qua struct lồng nhau được ghi ngược về chỗ cũ.
- Tách bước bỏ qua dấu `.` nằm trong ngoặc và trong nháy kép: `Map["a.b"]`, `Echo<System.Int32>(5)`.
- Lỗi (sai overload, sai số type argument, member không có…) luôn trả về dạng lỗi đọc được, không ném exception.
- `#Type[i]` ổn định trong phiên cho tới khi có object cùng type sinh ra/bị huỷ; không ổn định qua các phiên.

### Trang member

Mọi field + property, public lẫn private, instance lẫn static, đi hết chuỗi kế thừa, nhóm theo lớp khai báo (`lớp cha MonoBehaviour`…). Ghi được thì là ô sửa.

- Lọc vì không dùng được: `[Obsolete]`, backing field của auto-property, field `m_*` của **UnityEngine/.NET** (con trỏ C++, `Int32.m_value`). Field `m_*` của game vẫn hiện.
- **Không đọc** các getter chỉ cần đọc là tạo bản sao asset: `Renderer.material(s)`, `MeshFilter.mesh`, `Collider.material`, `TMP_Text.fontMaterial(s)` — row hiện lỗi, dùng bản `shared`.
- Collection thuần (mảng, `List`, `Dictionary`, `HashSet`…) mở ra là danh sách phần tử (100 phần tử đầu). Thứ chỉ implement `IEnumerable` (`Transform`, `Animation`, class tự viết) mở ra trang member, phần tử nằm ở row **Phần tử** riêng.
- Method ở row **Method N** cuối trang: đủ overload, nhãn kèm kiểu tham số. Method trả `Task`/`ValueTask`/`UniTask` có switch **Chờ kết quả** (log kết quả khi xong, bỏ cuộc sau 60 s).
- Dựng trang resolve address **một lần**, không phải một lần cho mỗi row. Trang member vẫn không Live (~1 ms mỗi row UI): mở lại, hoặc ghim rồi bấm **Làm mới giá trị** ở Objects.
- Cuối trang có nút ghim hai chiều (**+ Ghim vào Objects** / **Bỏ ghim**) khi address ghim được.

### Objects

| | Address ghim (`Watches`) | Biến `$` (`Vars`) |
|---|---|---|
| Là gì | address sống, resolve lại mỗi lần | ảnh chụp giá trị/type lúc lưu |
| Đọc lại | khi mở trang hoặc bấm **Làm mới giá trị** | không tự đổi |
| Lưu ở đâu | PlayerPrefs (`DebugHub.Watches`) | RAM, mất khi domain reload |

- Không ghim được address gốc `$` (biến chết theo domain reload) và address có gọi method (Objects đọc lại mọi mục mỗi lần làm mới). Bản lưu cũ vi phạm hai luật này bị bỏ qua khi đọc.
- **Nhập address…** chỉ **mở** address — kể cả gốc `$` và có gọi method, chính là hai loại bộ chọn không tới được. Ghim bằng nút ở cuối trang mở ra.

### Duyệt

- **Assembly**: `Mọi assembly` ở đầu (tìm type không cần biết assembly), `Assembly-CSharp` đứng trước, assembly của Unity/.NET ẩn (gõ là thấy).
- **Type**: tìm ở thread nền, bỏ type do compiler sinh, sắp theo tên rồi mới cắt ở 200 (100 với Mọi assembly) — bị cắt thì có dòng báo.
- **Instance**: `Member static` luôn có; `UnityEngine.Object` thì thêm instance đang sống, chia trang.

## SDK và plugin

Package không tham chiếu SDK nào; tất cả tìm bằng reflection lúc khởi động (tra thẳng tên assembly-qualified, không quét domain). Project không cài thì không có row.

| Row | Tìm | Gọi |
|---|---|---|
| `sdk.max` | `MaxSdk, MaxSdk.Scripts` | `ShowMediationDebugger()` (khai ở lớp cha theo platform) |
| `sdk.admob` | `GoogleMobileAds.Api.MobileAds, GoogleMobileAds` | `OpenAdInspector(Action<AdInspectorError>)` |
| `console.proxima` | `Proxima.ProximaInspector, Proxima` | bật = tạo inspector + `Run()`, tắt = huỷ hẳn; password kết nối là PIN mới mỗi phiên, hiện trong mô tả của row |

Debugger của SDK khác (vd `sdk.zego`) do game tự đăng ký.

## Trần đã biết

- **IL2CPP managed code stripping**: Advanced và SDK row chạy bằng reflection. Mức stripping mặc định (Minimal) không strip code của assembly game/SDK; ở Medium/High, member không ai gọi tĩnh có thể bị strip — Advanced báo "không tìm thấy", SDK row biến mất. Package cố ý không mang `link.xml` (giữ code = build nặng thêm); cần thì thêm qua `IUnityLinkerProcessor`.
- **Log trước khi mở khoá**: phiên mở khoá lần đầu trên máy không phải bản nội bộ chỉ có log từ lúc mở khoá.
- **Project tắt log Unity** (ví dụ `PRODUCTION` của com.hlight.logging) thì hub không có log để hiện, kể cả dòng kết quả của command — hub không còn bật tạm logger. Báo lỗi / message: `Logs` rỗng; "Đang gửi…", "Đang gửi lần trước…" và kết quả về ngay không hiện (đi qua log của lệnh) — bấm Gửi lúc kênh đang bận thì panel chỉ đóng. Kết quả về sau và lỗi ngay vẫn hiện.
- **Dấu hiệu mạng công ty** phụ thuộc trang/IP của văn phòng: đổi thì sửa Inspector, theo build kế tiếp.
- **Nơi gọi** ở build IL2CPP release chỉ có tên method (không số dòng) trừ khi bật IL2CPP Stacktrace Information có số dòng.
- **Gọi method tuỳ ý qua trang Method có thể làm hỏng state game** — bản chất công cụ.
- **Ngưỡng trigger chưa đo trên máy thật**: vẽ nguệch ngoạc và 4 góc tính theo tỉ lệ màn hình, lắc theo g (`shakeThreshold` là bình phương độ lớn: prefab đặt 20 ≈ 4,5 g, mặc định trong code 50 ≈ 7 g). Chỉnh trong Inspector sau khi thử trên điện thoại.
- **Không parse ngoặc cùng loại lồng nhau** trong address — bắc cầu qua `$var`.
- **Bảng monospace là xấp xỉ** — font khác nhau thì số ký tự vừa một dòng khác nhau.
- **`KeyPressDebuggerAuthenticationTrigger` serialize field khác nhau theo input backend** (`Key` vs `KeyCode`): đổi backend là mất phím đã chọn.
- **URL/token của class con gửi nằm trong bản store** (hub cố ý ship bản store): decompile là thấy.
- **`Send` không có timeout** thì kênh đó kẹt "Đang gửi…" tới khi tắt app. Hub cố ý không cắt: hết giờ mà request vẫn thành công thì QA gửi lại thành issue trùng.
- **Form báo lỗi / message chỉ có ô một dòng.**

## Test

```bash
unity cmd --project-path . run_tests --mode EditMode --filter "Hlight.Debug.Hub.Tests" --filter_type assembly
```

`AgentTestRunner` (chạy test đồng bộ cho agent) chỉ hỗ trợ `[SetUp]` / `[Test]` / `[TearDown]`, và không có log scope: test dùng `LogAssert.Expect` luôn đỏ ở đó dù đúng. Lệnh `run_tests` ở trên là chuẩn; runner này chỉ để dùng khi Test Runner treo.

## Ghi công

Bộ tách đối số và bảng parse theo kiểu trong `Scripts/Model/DebugValues.Parse.cs` mang từ [IngameDebugConsole](https://github.com/yasirkula/UnityIngameDebugConsole) của Süleyman Yasir KULA, giấy phép MIT; header bản quyền giữ nguyên trong file.
