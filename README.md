# Hlight Debug Hub

In-game debug hub: IngameDebugConsole + cheat commands theo cây path + diagnostics, tất cả sau một password gate.

## Cài đặt

Package này chứa submodule ([IngameDebugConsole](https://github.com/yasirkula/UnityIngameDebugConsole)) nên phải clone kèm submodule:

```bash
git clone --recurse-submodules <url> Packages/com.hlight.debug-hub
```

Với repo đã clone sẵn:

```bash
git submodule update --init --recursive
```

Thiếu bước này thì `ThirdParty/UnityIngameDebugConsole` rỗng và toàn bộ package không compile.

Lưu ý: sau khi Unity import, submodule sẽ ở trạng thái dirty vì Unity nâng version importer trong các file `.meta` của sprite. **Đừng commit vào submodule** — cứ để nguyên, hoặc `git -C ThirdParty/UnityIngameDebugConsole checkout -- .` (Unity sẽ ghi lại lần import sau).

## Sử dụng

Kéo `Prefabs/DebugHub.prefab` vào scene đầu tiên. Nhập password (field `password` trên component `DebugHub`) theo trigger của platform để mở panel.

### Bỏ qua password theo mạng

Field `networkReachabilityAuthenticationBypass` trên component `DebugHub` (mảng `checkUrls`, để trống mặc định) cho phép coi máy như đã xác thực nếu nó reachable (`HEAD` request thành công) tới bất kỳ URL nào trong danh sách — không cần biết password. Dùng khi muốn tester trong một mạng cụ thể (VPN, mạng nội bộ...) vào thẳng không cần gõ password.

**Trước khi điền `checkUrls`:** target phải thật sự bị chặn ở tầng mạng (firewall/security group theo IP nguồn) với bất kỳ ai ngoài mạng đó — domain/IP public không tự động có nghĩa là "không ai vào được", vì `HEAD` request coi cả trang redirect-sang-login là thành công. Nếu target không bị chặn đúng cách, bất kỳ ai có internet cũng được coi là đã xác thực, vĩnh viễn (state lưu qua cùng `PlayerPrefs` với password).

## Command

Command của hub nằm trong storage riêng (`DebugCommands`), **không** đăng ký vào IngameDebugConsole nữa. IDC ở lại làm log window và parser giá trị (`ParseArgument`, `GetTypeReadableName`, `FetchArgumentsFromCommand`), còn hub cần giữ thêm description, hình thái, state hiện tại và owner — những thứ `ConsoleMethodInfo` không có chỗ chứa.

```csharp
DebugCommands.Add(this, "level.next", "Sang level kế tiếp.", NextLevel);
DebugCommands.Add<int>(this, "level.goto", "Nhảy tới level bất kỳ.", GoToLevel);
DebugCommands.Add<BoosterType, int>(this, "economy.booster", "Cộng thêm booster.", AddBooster);

// Row hiện luôn trạng thái hiện tại, bấm là áp ngay.
DebugCommands.AddToggle(this, "view.ui", "Ẩn/hiện toàn bộ UI game.", () => UiVisible, SetUiVisible);
DebugCommands.AddValue(this, "time.scale", "Time scale hiện tại.", () => Time.timeScale, SetTimeScale);

// Tập phần tử chỉ biết được lúc mở page (canvas trong scene, entry trong save...).
DebugCommands.AddPage(this, "ui.canvases", "Ẩn/hiện từng canvas", page => { /* dựng row tại đây */ });
```

- Tham số đầu là **owner**: owner bị `Destroy` thì command tự rụng, không phải viết `OnDestroy` gỡ tay. `null` = sống suốt phiên.
- Tên tham số lấy từ chính delegate (`GoToLevel(int level)` -> field tên `level`), truyền thêm string chỉ khi muốn tên khác.
- Đăng ký bằng delegate nên sai tên method là lỗi compile, khác `AddCommandInstance(nameof(...))` của bản cũ.
- Path phải để tên lá tự nói được nó làm gì: row chỉ hiện segment cuối, nên `inspect.get` chứ không phải `get`, `view.ui` chứ không phải `ui.hud`.
- `Add` trả về `DebugCommand`, chỉnh tiếp bằng các hàm nối:

```csharp
DebugCommands.Add(this, "save.wipe", "Xoá toàn bộ save.", Wipe).Confirms();
DebugCommands.AddToggle(this, "view.ui", "...", get, set).HidesHub();
DebugCommands.Add<float, float>(this, "time.skip", "...", FastForward).Defaults("1", "100");
DebugCommands.Add<string>(this, "prefs.get", "...", GetPref).Stays();
```

`Stays()` / `HidesHub()` / `Confirms()` / `Defaults(...)` chỉ là setter của `Dismiss` / `Confirm` / `Args`, nối được với nhau.

`Path` **không phải key**: hai command trùng path là hợp lệ (row kèm `(N args)` để phân biệt), gỡ thì gọi `DebugCommands.Remove(command)` với chính object đã đăng ký.

### Hình thái row suy từ tham số

| Command | Row |
|---|---|
| 0 tham số | row tối + tên accent, bấm là chạy |
| 1 tham số **có** `Current` (`AddToggle` / `AddValue`) | row tại chỗ: bool ra switch, enum ra page chọn, còn lại là field áp lúc chốt |
| còn lại | row `›` mở page nhập liệu + nút `Run` |
| `AddPage` | row `›` mở page do command tự dựng |

`Current` (đọc state hiện tại) là thứ quyết định, không có field `Kind` nào để khai.

### Sau khi chạy: `DismissMode`

| Mode | Nghĩa | Dùng cho |
|---|---|---|
| `Stay` | giữ nguyên panel | mặc định của row bật/tắt và row nhập tại chỗ; đặt thêm cho command **đọc/ghi dữ liệu** (`prefs.*`, `inspect.*`) vì còn tra tiếp |
| `ClosePanel` | đóng panel, entry còn | mặc định của command dạng action và page nhập liệu: đổi state của game xong thì ra nhìn game |
| `HideHub` | ẩn entry + đóng panel + tắt dòng kết quả | khi muốn xem kết quả thì hub phải biến mất hẳn: ẩn UI game, mở debugger của SDK, chụp ảnh level |

`HideHub` tắt cả dòng kết quả vì nếu không thì nó dính vào ảnh chụp. Gọi lại bằng trigger (lắc / gõ 4 góc) và panel mở lại **đúng page đang xem**.

### Ẩn nhanh cả hub

```csharp
DebugHub.Visible = false;   // đóng panel + ẩn entry
DebugHub.Visible = true;    // hiện entry lại (chỉ ăn khi đã xác thực)
```

### Chạy bằng dòng lệnh

```csharp
DebugCommands.Execute("level.goto 5");
```

Tách tham số bằng parser của IDC nên quote/ngoặc giống console. Đây là đường cho Proxima (`exec` từ xa) và cho ô nhập lệnh của console qua command bridge duy nhất còn đăng ký vào IDC:

```
hub "level.goto 5"
```

## Panel

Cả hub là **một panel duy nhất** điều hướng theo stack. Bấm lớp background phía sau = đóng panel (bất kể đang ở page nào); nút `‹` ở header = lùi một tầng.

Path của command tách theo **mọi** dấu `.` thành cây page, không còn khái niệm "category" đặc biệt: `prefs.set.int` nằm ở `Commands › prefs › set › int`, command không có dấu `.` nằm ngay tầng đầu.

```
Debug Hub            Commands              Built-in              time
  Commands      ->     Recent      2   ->    inspect     7   ->    scale       1
  Help                 Search               prefs       4       Time scale hiện tại
  [x] Console          analytics   1        sdk         2         skip          ›
  [ ] Auto enable      economy     2        time        2       Tua nhanh sec giây
  [ ] Proxima          level       7
  [ ] Show entry       view        2
                       sdk         1
                       Built-in   15   <- đồ có sẵn của package, dồn xuống cuối
                     ─────────────────────────────────────────────────────────
                     > time.skip 1 100           <- dòng kết quả, bấm mở console
```

Command do package tự đăng ký (`prefs`, `time`, `inspect`, `sdk.max/admob`) nằm trong menu **Built-in** ở cuối, không chen vào giữa cheat của game — phân loại theo owner (owner thuộc assembly của hub) nên chỗ đăng ký không phải khai gì. Recent và Search vẫn dò cả hai nửa.

Mọi template chừa lề **36** so với mép row (ô input tính từ mép ô, không tính padding chữ bên trong nó), danh sách chừa 14 trên/dưới — thêm template mới thì giữ đúng mốc đó, có test canh.

Row hiện tên lá + description xám ở dòng dưới (dài quá thì cắt `…`, toàn văn ở page Help); row cao theo nội dung nên description hai dòng không bị cắt chữ. Trùng path trong cùng một chỗ thì kèm số lượng tham số để phân biệt.

Đường tắt ở gốc page Commands: **Recent** (5 command chạy gần nhất, lưu PlayerPrefs nên sống qua lần chạy sau) và **Search** (tìm theo path hoặc description, kết quả hiện đường dẫn đầy đủ).

Page nhập liệu: mỗi param một field theo đúng kiểu — bool ra toggle, số chỉ nhập được số, enum ra page chọn giá trị, còn lại là text với placeholder là tên kiểu. Giá trị prefill từ state hiện tại (hoặc `Args`), và giữ lại cái vừa nhập cho lần sau. Mọi giá trị validate bằng `DebugLogConsole.ParseArgument` (sai thì chữ đỏ, bấm Run báo lỗi và không chạy).

Enum dùng page chọn giá trị chứ không dùng `UI.Dropdown`: dropdown sinh canvas lồng + blocker bên trong `Mask` của scroll view nên list bị mờ và không bấm được. Cách này cũng giống `PickerEnumFieldGUI` của `Assets/Plugins/DebugPanel`.

Window cao đúng bằng nội dung, chặn trên bởi `maxWindowHeight` (mặc định 1500, quá thì scroll).

### Dòng kết quả

Chạy command xong, log mà nó in ra trong lúc chạy (bắt qua `Application.logMessageReceived`) hiện ở dòng nổi dưới đáy màn hình — **ngoài** panel, vì command mặc định đóng panel sau khi chạy. Dài quá thì cắt: bấm vào dòng đó để mở console log window xem toàn văn kèm stack trace.

Nghĩa là cheat chỉ cần `Debug.Log` như bình thường là tự nhiên có kết quả hiện lên, không phải khai thêm gì.

Nhưng **không phải command nào cũng hiện**: chỉ command có `Dismiss = Stay` (đang đọc dữ liệu, panel ở lại) mới hiện, và chỉ khi thật sự in ra gì. Command đổi state của game thì im — `level.goto` tự nó không log, nhưng luồng load level log đồng bộ trong lúc invoke, nếu chỉ xét "có log hay không" thì nó vẫn hiện một dòng vô nghĩa giữa màn hình. Ghi đè bằng `.Reports()` (luôn hiện) hoặc `.Silent()` (không bao giờ hiện).

Lỗi thì luôn hiện, chữ đỏ; log lỗi do chính command in ra cũng đỏ dù command vẫn coi là chạy xong.

### Đóng rồi mở lại

`Close()` giữ nguyên stack nên mở lại là về đúng page đang xem lúc đóng — nhất là với `DismissMode.HideHub` (ẩn hub để nhìn game rồi gọi lại tiếp tục việc đang làm). Muốn về gốc thật thì gọi `panel.ShowFromRoot(page)`.

### Ngôn ngữ hình ảnh

| Row | Nghĩa | API |
|---|---|---|
| row tối + `›` (+ số căn phải) | mở page khác | `AddNavigation(label, page, description, detail)` |
| row tối + **tên màu accent** | chạy ngay | `AddAction(label, onClick, description)` |
| row **nền accent** | hành động chính của page (Run) — mỗi page một cái | `AddPrimary(label, onClick)` |
| row tối + switch (có núm) | bật/tắt trạng thái | `AddToggle(label, value, onChanged, description)` |
| label + input | nhập giá trị | `AddField(label, type, current, onChanged, onSubmit, description)` |
| row tối trung tính | không ngụ ý gì | `AddButton(label, onClick, description)` |
| row tên màu vàng đất | điều hướng của chính hub, không phải nội dung do game đăng ký: Recent, Search, Built-in | `AddShortcut(label, page, detail)` |

Chỉ **một** row nền accent mỗi page: cả page toàn row nền accent thì thành tường màu và description trên nền đó đọc không nổi, nên command chạy ngay dùng row tối với tên màu accent. `description` là tham số, panel tự format thành dòng thứ hai (nhỏ, xám) — chỗ gọi không ghép markup.

Back có nút `‹` ở header (tự ẩn ở page gốc, lùi một tầng). Bấm ra vùng tối ngoài panel đóng panel, không lùi từng tầng (mở lại về đúng page đó).

Tự thêm page riêng bằng `DebugPage` + các hàm trên của `DebugHubPanel`; muốn cắm page đó vào cây Commands thì đăng ký bằng `DebugCommands.AddPage`.

Dev note ở page Help: thêm vào `DebugHub.Notes`.

## Đổi ở đợt refactor command

- Command của hub có storage riêng (`DebugCommands` / `DebugCommand`), **không** đăng ký vào IngameDebugConsole nữa. IDC còn lại: log window + parser giá trị + đúng một command bridge `hub "<dòng lệnh>"`.
- Bỏ `[ConsoleMethod]`, `AddCommandInstance`, `AddCommandStatic`: đăng ký bằng delegate, sai tên là lỗi compile.
- Path tách theo mọi dấu `.` thành cây page; không còn category `General` hay nhóm "Hub's built-in".
- Thêm: description hiện trong row, dòng kết quả, `DismissMode`, `Confirm`, `Recent`, `Search`, `Args` (default + nhớ giá trị đã nhập), `Owner` (tự rụng khi Destroy), `DebugHub.Visible`, `DebugCommands.Execute`.
- Command của package dồn vào menu `Built-in` ở cuối page Commands (phân loại theo owner), dòng kết quả chỉ hiện khi command có in log hoặc khi lỗi.
- Row: `AddPrimary` cho hành động chính (nền accent), `AddAction` đổi thành row tối + tên accent, số lượng command tách sang cột căn phải, switch có núm, header có hairline, row `AddButton` căn trái.
- `ConsoleController`: các overload chỉ để đặt sẵn một tham số (`get`/`set`/`reg.set`/`reg.type`/`time.skip`) gộp về một command, tham số đó thành giá trị prefill.
- Đổi path cho tên lá tự nói được nghĩa: `get`/`set`/`ans` → `inspect.get`/`inspect.set`/`inspect.last`, `reg.*` → `inspect.var.*`, `ad.*` → `sdk.*`. Phía game: `ui.toggle` → `view.ui`, `debug.fps` → `view.fps`, `debug.tracking` → `analytics.tracking`, `debug.sdk` → `sdk.zego`, `debug.screenshots` → `level.screenshots`, `economy.setcoin` → `economy.coin` (dạng value, hiện luôn số xu đang có), `economy.addbooster` → `economy.booster`.
- Proxima `exec` chạy qua `DebugCommands.Execute` thay vì `DebugLogConsole.ExecuteCommand`.

## Đổi so với `com.hlight.ingame-debugger`

- Package/namespace: `com.hlight.debug-hub` / `Hlight.Debug.Hub`, version về `1.0.0`.
- IngameDebugConsole không còn nhúng, thành submodule ở `ThirdParty/`.
- **Bỏ chức năng Debug Objects** (`DebugObject`, `RegisterDebugObject`, `UnregisterDebugObject`).
- UI dựng lại theo page stack; `ADebugOperation` và các `Show*` component không còn.
- Key PlayerPrefs của auth đổi thành `DebugHub.AuthenticationState` → phải nhập password lại một lần.

## Test

Cửa sổ Test Runner (EditMode), hoặc menu `Tools/Hlight/Run Debug Hub Tests` để ghi kết quả ra `Temp/debug-hub-tests.txt`.

## TODO của maintainer

Repo remote chưa được tạo. Sau khi tạo, chạy:

```bash
cd Packages/com.hlight.debug-hub
git remote add origin <url>
git push -u origin master
cd ../..
git submodule add <url> Packages/com.hlight.debug-hub
```
