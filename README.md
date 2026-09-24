# Hlight Debug Hub

In-game debug hub: cheat command theo cây path, hệ inspect bằng reflection, log window (IngameDebugConsole) — tất cả sau một password. Hub nằm được cả trong bản store: chưa mở khoá thì gần như không tốn gì.

## Cài đặt

Package chứa submodule [IngameDebugConsole](https://github.com/yasirkula/UnityIngameDebugConsole) nên phải clone kèm submodule:

```bash
git clone --recurse-submodules <url> Packages/com.hlight.debug-hub
```

Với repo đã clone sẵn:

```bash
git submodule update --init --recursive
```

Thiếu bước này thì `ThirdParty/UnityIngameDebugConsole` rỗng và package không compile. Sau khi Unity import, submodule có thể dirty vì Unity nâng version importer trong `.meta` của sprite — **đừng commit vào submodule**, cứ để nguyên hoặc `git -C ThirdParty/UnityIngameDebugConsole checkout -- .`.

## Thiết lập

1. Kéo `Prefabs/DebugHub.prefab` vào scene đầu tiên.
2. Chọn object `DebugHub` trong scene → component **DebugHub** → điền ô **Password** → lưu scene. Để trống thì hub không mở được (có log lỗi lúc chạy).

Người dùng mở hub bằng trigger (vẽ 4 góc, phím tắt…) → gõ password → máy nhớ trạng thái đã mở khoá (PlayerPrefs `DebugHub.AuthenticationState`).

### Symbol

| Symbol | Tác dụng |
|---|---|
| `DISABLE_DEBUG_HUB` | hub tự huỷ lúc khởi động, `RenameFolderOnBuild` loại folder `Resources` của package khỏi build. Đặt trong Player Settings (hook build đọc define lúc compile editor). |
| `ALWAYS_ENABLE_INGAME_DEBUGGER` | bỏ qua password — **chỉ dùng cho build nội bộ**. |

Hub **không** dùng `PRODUCTION`: symbol đó là công tắc tắt log của `com.hlight.logging`. Khi log bị tắt, hub vẫn bắt được log của command (bật tạm logger trong lúc chạy lệnh) và `console.show` bật lại log cho hết phiên.

### Bỏ qua password theo mạng

`networkReachabilityAuthenticationBypass.checkUrls` (trống mặc định): máy reachable (`HEAD` thành công) tới một URL trong danh sách được coi là đã xác thực. **Chỉ chạy ở build development** — ở bản store, mọi máy chưa mở khoá sẽ gửi request tới URL đó mỗi lần mở app (iOS hỏi quyền mạng cục bộ nếu là IP nội bộ, và mạng nào tình cờ có máy ở IP đó là mở khoá luôn). Target phải thật sự bị chặn ở tầng mạng với người ngoài: `HEAD` coi cả trang redirect-sang-login là thành công.

## Node

Mọi thứ hiện lên panel là một trong bốn loại `DebugNode`, đăng ký qua `DebugHub`:

| Loại | Là gì | Field chính |
|---|---|---|
| `ActionNode` | một việc chạy được | `Parameters[]`, `Invoke` |
| `ValueNode` | một giá trị đọc được, ghi được nếu `Set != null` | `Declared`, `Get`, `Set`, `Address` |
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

Mặc định: `ActionNode` → đóng panel; `ValueNode` → ở lại; node do reflection sinh → ở lại.

### Row suy từ node — `NodeRenderer`

`NodeRenderer.Render` là nơi duy nhất biết node nào ra row nào. Xét từ trên xuống:

| Node | Điều kiện | Row |
|---|---|---|
| `ValueNode` | `Get()` ném exception | dòng lỗi |
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

Tách tham số bằng parser của IDC (quote/ngoặc giống console). Ô nhập của console chạy được command của hub qua `hub "level.goto 5"`; gõ sai thì console in lý do.

`$ten` trong đối số là biến (xem Advanced); chuỗi thật bắt đầu bằng `$` viết `$$`.

### Ẩn nhanh cả hub

```csharp
DebugHub.Visible = false;   // đóng panel + ẩn entry
DebugHub.Visible = true;    // hiện entry lại (chỉ ăn khi đã mở khoá)
```

## Panel

Một panel duy nhất điều hướng theo stack. Bấm nền ngoài = đóng (giữ stack, mở lại về đúng trang); `‹` = lùi một tầng; `×` = đóng. Gốc panel là cây command: path tách theo `.` thành cây trang (`prefs.set.int` → `Commands › prefs › set › int`).

- Entry bubble (như chat head Messenger): thả tay là bubble trôi theo đà rồi dính mép trái/phải gần điểm dừng — điểm dừng = chỗ nhấc tay + vận tốc × `momentum` (mặc định 0.18s, chỉnh ở Inspector). Thả nhẹ về mép gần, hất mạnh sang mép kia; dừng tay rồi mới nhấc thì không trôi. Kéo vào nút X ở đáy là bị hút vào, thả ra để ẩn hub.
- Header: kính lúp (tìm), thanh điều chỉnh (công cụ/Advanced) và `?` (trợ giúp) — hai nút sau chỉ ở Commands gốc.
- Category gốc có nút sao: sao đặc = yêu thích, được đưa lên nhóm **Yêu thích**, lưu PlayerPrefs.
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

Log mà command in ra trong lúc chạy hiện ở dòng nổi dưới đáy (ngoài panel), chỉ với node có `ShowsResult` (mặc định = `Dismiss == Stay`, ghi đè bằng `.Reports()`/`.Silent()`). Lỗi luôn hiện. Dài quá thì cắt (không cắt giữa một tag); bấm để mở log window.

### Font

Package **không mang font**. Mọi TMP để trống font nên TMP lấy `TMP_Settings.defaultFontAsset` của game. Điều kiện để hiện đúng:

- Font mặc định có tiếng Việt và `› ‹ … – — ×` (hub chỉ dùng đúng các ký tự này ngoài chữ cái; icon là hình vẽ vector, không phải glyph). Kiểm bằng `FontEngine.TryGetGlyphIndex` trên TTF/OTF nguồn, không phải `HasCharacter`.
- Font Dynamic thì nên bật **Clear Dynamic Data On Build**: không thì glyph sinh ra trong Editor (kể cả từ chữ của hub) được lưu vào asset và đi vào mọi build.

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
- **IDC quét mọi assembly lúc khởi động** (`[ConsoleMethod]`, trong `RuntimeInitializeOnLoadMethod` của IDC, không có define tắt): ~66 ms trong Editor, mọi người chơi đều trả. Muốn bỏ thì phải fork IDC.
- **Gọi method tuỳ ý qua trang Method có thể làm hỏng state game** — bản chất công cụ.
- **Ngưỡng trigger tính bằng pixel thô** (vẽ ngoằn ngoèo, lắc): cảm giác khác nhau theo độ phân giải; chỉnh sau khi đo trên máy thật.
- **Không parse ngoặc cùng loại lồng nhau** trong address — bắc cầu qua `$var`.
- **Bảng monospace là xấp xỉ** — font khác nhau thì số ký tự vừa một dòng khác nhau.
- **`KeyPressDebuggerAuthenticationTrigger` serialize field khác nhau theo input backend** (`Key` vs `KeyCode`): đổi backend là mất phím đã chọn.

## Test

```bash
unity cmd --project-path . run_tests --mode EditMode --filter "Hlight.Debug.Hub.Tests" --filter_type assembly
```

`AgentTestRunner` (chạy test đồng bộ cho agent) chỉ hỗ trợ `[SetUp]` / `[Test]` / `[TearDown]`.
