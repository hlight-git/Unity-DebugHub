# Hlight Debug Hub

In-game debug hub: IngameDebugConsole + cheat command theo cây path + hệ inspect bằng reflection, tất cả sau một password gate.

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

## Node

Mọi thứ hiện lên panel — cheat của game lẫn đồ nghề của chính hub — đều là một trong bốn loại `DebugNode`. Không còn `DebugCommand`; storage tên `DebugRegistry` (`internal`), đăng ký qua `DebugHub`:

| Loại | Là gì | Field chính |
|---|---|---|
| `ActionNode` | một việc chạy được | `Parameters[]`, `Invoke` |
| `ValueNode` | một giá trị đọc được, ghi được nếu `Set != null` | `Declared`, `Get`, `Set`, `Address` |
| `FolderNode` | có con, liệt kê lúc mở (không giữ sẵn) | `Children`, `Inline`, `Live` |
| `TextNode` | chữ: mô tả, ghi chú, bảng đã format | `Text`, `Style` |

```csharp
DebugHub.Add(this, "level.next", "Sang level kế tiếp.", NextLevel);                 // ActionNode
DebugHub.Add<int>(this, "level.goto", "Nhảy tới level bất kỳ.", GoToLevel);          // ActionNode
DebugHub.AddValue(this, "view.ui", "Ẩn/hiện toàn bộ UI game.", () => UiVisible, SetUi);   // ValueNode, Declared=bool → switch
DebugHub.AddValue(this, "time.scale", "Time scale hiện tại.", () => Time.timeScale, SetTimeScale); // ValueNode, Declared=float → field
DebugHub.AddFolder(this, "info.app", "Bấm một dòng để copy.", AppInfoNodes);         // FolderNode
DebugHub.Remove(node);
DebugHub.Execute("level.goto 5", out var message);
```

- Tham số đầu là **owner**: owner bị `Destroy` thì node tự rụng, không phải viết `OnDestroy` gỡ tay. `null` = sống suốt phiên.
- Tên tham số của `Add<T...>` lấy từ chính delegate (`GoToLevel(int level)` → field tên `level`), truyền thêm string chỉ khi muốn tên khác.
- Đăng ký bằng delegate nên sai tên method là lỗi compile.
- Path phải để tên lá tự nói được nó làm gì: row chỉ hiện segment cuối, nên `view.ui` chứ không phải `ui.hud`.
- `AddToggle`/`AddPage` của bản cũ đã **xoá**: `AddValue<bool>` cho đúng cái switch đó, và không còn đường vòng qua model bằng closure `Action<DebugHubPanel>` — muốn nhiều con thì `AddFolder`.

### Hàm nối

`Add`/`AddValue`/`AddFolder` trả về chính node, chỉnh tiếp bằng extension generic (không mất kiểu cụ thể):

```csharp
DebugHub.Add(this, "save.wipe", "Xoá toàn bộ save.", Wipe).Confirms();
DebugHub.AddValue(this, "view.ui", "...", get, set).HidesHub();
DebugHub.Add<string>(this, "prefs.get", "...", GetPref).Stays();
DebugHub.Add<float, float>(this, "time.skip", "...", FastForward).Defaults("1", "100");
```

| Hàm | Làm gì |
|---|---|
| `.Stays()` | `Dismiss = Stay` — giữ nguyên panel sau khi chạy |
| `.HidesHub()` | `Dismiss = HideHub` — ẩn cả entry, cho ảnh chụp sạch |
| `.Confirms()` | `Confirm = true` — chạy phải qua trang xác nhận (kể cả từ nút repeat) |
| `.Reports()` | luôn hiện dòng kết quả dù `Dismiss != Stay` |
| `.Silent()` | không bao giờ hiện dòng kết quả |
| `.Defaults(params string[])` | seed tham số ban đầu cho `ActionNode`, chỉ khi chưa có giá trị người dùng đã nhập |

Mặc định `Dismiss`: `ActionNode` đăng ký → `ClosePanel`; `ValueNode` đăng ký → `Stay`; node do reflection sinh (Advanced) → `Stay`.

`Path` **không phải key**: hai `ActionNode` trùng path khi khác số tham số là hợp lệ (row kèm `(N args)` để phân biệt). `ValueNode`/`FolderNode` chiếm riêng exact path của mình. Trùng thật thì `Debug.LogError` (không throw — một cheat không có quyền làm sập game), gỡ node thì gọi `DebugHub.Remove(node)` với chính object đã đăng ký.

### Row suy từ node — bảng quyết định của `NodeRenderer`

`NodeRenderer.Render` là nơi **duy nhất** biết node nào ra row nào — trang command, trang folder do game đăng ký, trang member của Advanced và trang Watch đều đi qua đây. Xét từ trên xuống:

| Node | Điều kiện | Row |
|---|---|---|
| `ValueNode` | `Get()` ném exception | dòng lỗi, không gọi setter |
| `ValueNode` | giá trị đơn (`DebugValues.IsInlineValue`), `Set == null` | label trái + giá trị căn phải, **bấm = copy** |
| `ValueNode` | `Declared == bool`, `Set != null` | switch |
| `ValueNode` | enum không `[Flags]`, `Set != null` | row chọn giá trị (page chọn) |
| `ValueNode` | giá trị đơn, `Set != null` | field tại chỗ, chốt bằng `onEndEdit` |
| `ValueNode` | `Get()` ra null (kể cả fake-null của `UnityEngine.Object`) | row chữ `null`, không mở được |
| `ValueNode` | giá trị là collection (trừ string) | row nav → member/phần tử của nó |
| `ValueNode` | còn lại (`GameObject`, `Component`, object/struct khác), kể cả `Set == null` | row nav → member của giá trị hiện tại |
| `ActionNode` | `Parameters.Length == 0` | row chạy ngay (tên màu accent) |
| `ActionNode` | có tham số | row nav → page nhập liệu + nút `Run` |
| `FolderNode` | `Inline == false` | row nav chỉ tên + `›`, không đếm con động |
| `FolderNode` | `Inline == true` | dòng tiêu đề + con dựng thẳng tại chỗ |
| `TextNode` | | chữ theo `Style`, bấm = copy |

`FolderNode` không-inline chỉ gọi `Children` khi trang của nó đang mở (kể cả lúc refresh) — dựng/refresh trang cha hoặc search **không** gọi `Children`, vì đó là side effect của code game.

### `Execute` chạy được cả `ValueNode`

| Node tại path | 0 đối số | 1 đối số | khác |
|---|---|---|---|
| `ActionNode` | chạy nếu `Parameters.Length == 0` | chạy nếu arity khớp | theo arity |
| `ValueNode` | log giá trị hiện tại | `TryParse` theo `Declared` rồi `Set` | lỗi |
| `FolderNode` | lỗi "là thư mục, không chạy được" | lỗi | lỗi |

### Chạy bằng dòng lệnh

```csharp
DebugHub.Execute("level.goto 5");
```

Tách tham số bằng parser của IDC nên quote/ngoặc giống console. Đây là đường cho Proxima (`exec` từ xa) và cho ô nhập lệnh của console qua command bridge duy nhất còn đăng ký vào IDC:

```
hub "level.goto 5"
```

### Ẩn nhanh cả hub

```csharp
DebugHub.Visible = false;   // đóng panel + ẩn entry
DebugHub.Visible = true;    // hiện entry lại (chỉ ăn khi đã xác thực)
```

## Panel

Cả hub là **một panel duy nhất** điều hướng theo stack. Bấm lớp background phía sau = đóng panel (bất kể đang ở page nào); nút `‹` ở header = lùi một tầng.

Gốc panel là **chính cây command** — không còn trang menu trung gian, không có **Built-in**, không có **Recent**. Path tách theo mọi dấu `.` thành cây page: `prefs.set.int` nằm ở `Commands › prefs › set › int`. Mọi command bình đẳng, kể cả command do package tự đăng ký (`console.*`, `hub.*`, `prefs.*`, `time.*`, `sdk.*`) — chúng nằm lẫn với cheat của game theo đúng thứ tự chữ cái.

```
  ‹  Commands                    Tìm  Adv  ?
  ───────────────────────────────────────────
     analytics                            1 ›
     console                              3 ›
     economy                              2 ›
     level                                7 ›
  ───────────────────────────────────────────
  > level.goto 5                      ← dòng kết quả, ngoài panel

        [ ↻ level.goto ]   ( ⬤ )      ← nút repeat + entry
```

Ba nút header là **chữ** (`Tìm` / `Adv` / `?`), không phải sprite — package không mang icon.

### `Tìm` (search)

Bấm `Tìm` → title nhường chỗ cho ô nhập (nằm trong Header, **ngoài** `content`, vì rebuild destroy sạch `content`). Gõ tới đâu lọc tới đó, query sống khi đổi page. Nút đổi thành `Đóng` khi search đang mở.

- Ở mọi trang command (không tự khai `Search`) → tìm **toàn cục** trong registry theo path + description; kết quả hiện **full path**, render bằng chính node đó nên bật/tắt, chạy ngay được ngay trên dòng kết quả.
- Trang có list riêng (member, type, instance, watch, var) tự lọc list của nó.
- Trang nhập tham số và trang xác nhận không có nút `Tìm` (`Searchable = false`).
- **Không** quét vào trong `FolderNode` động — gọi `Children` để index là gọi side effect của game ở thời điểm không ai yêu cầu.

### `?` (Help)

Push trang Help: `DebugHub.Notes` + toàn văn mọi entry trong registry (path, kiểu và tên từng tham số, description đầy đủ — row trong list cắt description nên đây là chỗ duy nhất đọc được cả câu).

### `…` — thao tác trên một `ValueNode`

Mỗi row của `ValueNode` có đúng một nút `…` bên phải, mở page **Thao tác**:

| Mục | Hiện khi |
|---|---|
| `Copy giá trị` | luôn, với giá trị hiển thị được thành text |
| `Gán giá trị` | `Set != null` **và** giá trị không có editor tại chỗ — bool/enum/số/vector đã sửa ngay trên row rồi |
| `Watch` | `Address != null`; đã watch rồi thì hiện `Đã watch` |
| `Lưu vào $…` | `Get()` không null |

Không rải hai–ba nút nhỏ lên mỗi row (bấm nhầm trên bề ngang điện thoại) — một nút mở một page, giống mọi chỗ khác của hub.

### Nút repeat

Command `hub.repeat` (`ValueNode bool`, lưu PlayerPrefs) bật/tắt một nút nổi cạnh entry, nhãn = tên lá của lệnh cuối, bấm là chạy lại.

- Lưu **dòng lệnh** (`path + args`, PlayerPrefs `DebugHub.LastCommand`), không lưu tham chiếu node — chạy lại đúng tham số đã chạy và sống qua lần chạy sau. Reference Unity/collection chứa reference không có biểu diễn độc lập với session: lần chạy đó xoá `LastCommand` và ẩn nút, không giữ nút trỏ vào lệnh cũ.
- Bật `hub.repeat` mà chưa có lệnh nào (lần đầu, hoặc vừa bị xoá vì lý do trên) → **ẩn nút**, không hiện nút rỗng bấm ra lỗi.
- Node có `.Confirms()` → nút **mở panel tới trang xác nhận**, không chạy thẳng.
- Ẩn cùng entry khi `HideHub` (mục đích của `HideHub` là màn hình sạch).
- Chỉ chạy lại được node **đã đăng ký**; lời gọi qua Advanced (reflection) không vào đây.

### Text: TextMeshPro

Toàn bộ UI của package dùng TextMeshPro (`TMP_Text` / `TMP_InputField`), không còn `UnityEngine.UI.Text` / `InputField`.

Không gán font: để trống thì TMP tự lấy `TMP_Settings.defaultFontAsset` lúc `Awake`. Package không mang font riêng và không tham chiếu asset nào trong `Assets/` — đúng nghĩa dùng font của game, và không thêm gì vào build.

Điều kiện để cách này chạy đúng, kiểm trước khi đổi font default của project:

- Font default phải ở chế độ **Dynamic** và TTF nguồn có dấu tiếng Việt cùng `›` `‹` (description trong hub là tiếng Việt, row nav nào cũng có chevron). Kiểm bằng `TryAddCharacters` trên TTF nguồn, **không** phải `HasCharacter` — cái sau chỉ nói atlas đã bake hay chưa.
- Font phải bật **Multi Atlas Textures**. Atlas một texture có sức chứa hữu hạn; Dynamic mà atlas đầy thì TMP **âm thầm thay ký tự bằng dấu cách**, không báo lỗi gì ngoài một warning. Bật cờ này thì atlas phụ sinh lúc runtime nên không vào build.

Khác biệt markup cần biết khi viết description: TMP nhận `<size=80%>` (phần trăm), legacy Text thì không.

### Dòng kết quả

Chạy command xong, log mà nó in ra trong lúc chạy (bắt qua `Application.logMessageReceived`) hiện ở dòng nổi dưới đáy màn hình — **ngoài** panel. Dài quá thì cắt: bấm vào dòng đó để mở console log window xem toàn văn kèm stack trace.

Nghĩa là cheat chỉ cần `Debug.Log` như bình thường là tự nhiên có kết quả hiện lên, không phải khai thêm gì.

Nhưng **không phải node nào cũng hiện**: chỉ node có `ShowsResult == true` (mặc định suy từ `Dismiss == Stay`) mới hiện, và chỉ khi thật sự in ra gì. Ghi đè bằng `.Reports()` (luôn hiện) hoặc `.Silent()` (không bao giờ hiện). Lỗi thì luôn hiện, chữ đỏ.

### Đóng rồi mở lại

`Close()` giữ nguyên stack nên mở lại là về đúng page đang xem lúc đóng — nhất là với `DismissMode.HideHub`. Muốn về gốc thật thì gọi `panel.ShowFromRoot(page)`.

Window cao đúng bằng nội dung, chặn trên bởi `maxWindowHeight` (mặc định 1500, quá thì scroll). Lề chừa **36** so với mép row, danh sách chừa **14** trên/dưới.

## Vốn từ trình bày

Một chỗ dựng row chung cho cả package — trang info không tự chế lại màu, monospace, `PadRight` nữa:

```csharp
Node.Value(label, get, set = null, description = null)   // ValueNode
Node.Action(label, run, description = null)               // ActionNode
Node.Folder(label, children, description = null, live: false)   // FolderNode
Node.Section(title, children)               // FolderNode { Inline = true } — dựng ngay trong trang cha
Node.Text(text, TextStyle style = Normal)   // Normal | Note | Good | Warn | Bad | Table
Node.Table(headers, rows)                   // → TextNode(Style.Table)
```

Palette (`Hlight.Debug.Hub.Palette`): `GOOD #5FD068`, `WARN #E8B04B`, `BAD #E5484D`, `DIM #8A929C`.

### `Node.Table`

Căn cột bằng monospace + `PadRight` — không bằng `<pos=NN%>` (`<pos>` lùi được về sau, ô dài đè chữ lên nhau). Độ rộng mỗi cột = ô dài nhất trong cột; có ngân sách ký tự tối đa cho cả hàng, vượt thì cắt cột rộng nhất và thêm `…`. `TextStyle.Table` tắt word wrap trên row đó, nên hàng quá rộng bị cắt chứ không xuống dòng làm lệch cột. Là hàm thuần string, test bằng assert.

### Ví dụ: `level.info`

```csharp
DebugHub.AddFolder(this, "level.info", "Bàn đang chơi: quả theo màu, cụm/tầng, số để dò với doc.",
    () => new DebugNode[]
    {
        Node.Text(StatusLine(info, board), broken ? TextStyle.Bad : TextStyle.Good),
        Node.Text(Headline(info, board)),
        Node.Section("Màu",  new[] { Node.Table(ColorHeaders, ColorRows(info, board)) }),
        Node.Section("Tầng", new[] { Node.Table(LayerHeaders, LayerRows(board)) }),
        Node.Section("Spec", SpecNodes(info, board)),     // Node.Value × 6..9
        Node.Text(GroupNote, TextStyle.Note),
    });
```

Giá trị hai cột (`Spec`) dùng `Node.Value` thay vì bảng — vừa thẳng cột vừa bấm-copy được từng dòng.

## Advanced

Mặt thứ hai của hub, mở bằng nút `Adv` ở header — **đồ nghề của chính hub**, cứng trong code package, khác với Commands (mọi node game/package đăng ký qua `DebugHub.Add*`). Đây là hệ inspect bằng reflection, thay thế hoàn toàn `inspect.*` gõ tay và `Executor.cs` cũ.

```
  ‹  Advanced                        Tìm   ?
  ───────────────────────────────────────────
     Objects                              6 ›
     Duyệt                                  ›
```

- **Objects** — một danh sách: address đã ghim + biến `$` (biến `$` ghi rõ "chỉ trong phiên này"). `+ Thêm address` để gõ address tay.
- **Duyệt** — chọn từng bước Assembly → Type → Instance, rồi tới trang member của nó.

### Address — trạng thái điều hướng duy nhất

Trang inspect không giữ tham chiếu object nào cả, chỉ giữ **một chuỗi địa chỉ** và resolve lại mỗi lần dựng. Drill vào member = nối thêm `.tên`.

```
address = root ( "." member | "[" args "]" | "." Method(args) )*

root = TypeName        static context; full name tra thẳng từng assembly, tên ngắn mới phải quét
     | $var            giá trị hoặc type đã lưu trong Vars
     | #TypeName[i]    instance thứ i đang sống trong scene (mặc định [0])
     | @command.path   một ValueNode đã đăng ký — watch được cả cheat của game
```

Ví dụ: `@economy.coin`, `Harvest.GameplayCheats.SomeStaticField`, `#Camera[0]`, `#UnityEngine.Camera[0]` (type có namespace resolve đúng — short name mơ hồ giữa nhiều namespace thì phải gõ full name, xem mục Trần đã biết).

Giới hạn có chủ ý:

- **Không parse ngoặc lồng** — bắc cầu qua `$var` (`…` › `Lưu vào $…` trên kết quả, rồi drill vào biến).
- **`#TypeName[i]` không ổn định qua các phiên** — thứ tự `FindObjectsByType` không có bảo đảm, watch vào instance có thể trỏ sang object khác sau khi load lại scene.
- **Reflect.Elements cắt ở 100 phần tử** + một dòng "còn N…" — collection lớn hoặc `IEnumerable` tự sinh vô hạn không được giết panel.
- Trang member **hiện hết** (xem mục dưới).

### Trang member: hiện hết, không ghi được thì read-only

Mọi field + property, public lẫn private, instance lẫn static, đi hết chuỗi kế thừa (lớp dẫn xuất trước). Ghi được thì là ô sửa, không thì dòng read-only — không có bộ lọc. Hai ngoại lệ: backing field của auto-property (trùng ô nhớ với property), và `[Obsolete]` (13 property `rigidbody`/`camera`/… của mọi Component đọc là ném "deprecated").

Method nằm sau một row riêng `Method  N ›` ở cuối trang — `RootScope` có 239 method, `Transform` 319, trộn chung là mất dấu giá trị. Method trả `Task`/`ValueTask`/`UniTask` (nhận theo mẫu awaiter, không tham chiếu UniTask) có switch **`Chờ kết quả`** ở trang tham số: bật (mặc định) thì log `Tên xong: kết quả` khi xong.

Lý do bỏ luật cũ "chỉ member lớp cuối": `ProfileEntry` không khai member nào (tất cả ở `DataEntry<T>`), mở ra là trang trống.

### Objects: address ghim vs biến `$`

Cùng một trang, hai nguồn khác nhau ở chỗ sống bao lâu:

| | Address ghim (`Watches`) | Biến `$` (`Vars`) |
|---|---|---|
| Là gì | một **address sống**, resolve lại mỗi lần | một **ảnh chụp** — giá trị hoặc type tại thời điểm lưu |
| Đọc lại | 4 lần/giây (trang `Live`, bỏ nhịp khi đang gõ), giá trị đổi theo game | không tự đổi — đúng object/giá trị đã bind |
| Lưu ở đâu | PlayerPrefs (`DebugHub.Watches`), sống qua lần chạy sau | chỉ RAM, xoá lúc domain reload |
| Dùng để | theo dõi một giá trị đổi theo thời gian thực | truyền đúng **reference** (`$var`) vào tham số/địa chỉ mà text không biểu diễn được, hoặc tham số generic |

**Watch từ chối address chứa bước gọi method** trước khi resolve (xét từng step đã parse, không chỉ tìm dấu ngoặc trong chuỗi — literal của indexer có thể chứa ngoặc) — nếu không, một watch vào `Factory.Spawn()` sẽ gọi nó 4 lần/giây. Luật áp dụng lúc thêm, lúc đọc lại từ PlayerPrefs, và trước mỗi lần resolve để refresh; address có method đã lưu từ trước hiện lỗi kèm nút `Gỡ`, không được thực thi.

Các đường ghim: nút `…` trên một row giá trị (kể cả scalar không có trang riêng), nút `+ Ghim trang này` ở cuối trang member/phần tử, `+ Ghim vào Objects` ở trang của `Duyệt`.

### Duyệt

- **Assembly** — bấm `Tìm`, gõ một phần tên (`Assembly-CSharp` — code game nằm ở đây, `Hlight`).
- **Type** — type trong đúng assembly đó.
- **Instance** — `Member static` luôn có; là `UnityEngine.Object` thì thêm instance đang sống (kể cả inactive), mỗi dòng một address `#Type[i]`.
- **Trang member** — dòng copy address, danh sách member, `+ Ghim vào Objects`.

Gợi ý chạy thread nền (debounce 0.15 s) nên gõ không giật. Lý do làm lại:

| | Trước | Sau |
|---|---|---|
| Dựng index type | 4142 ms trên main thread, 135.358 key | không có index |
| `Find` full name | tra index | `Assembly.GetType` từng assembly, ~0 ms |
| Tìm type mỗi ký tự gõ | 32 ms, quét cả domain | ~6 ms lần đầu, trong một assembly, ở thread nền |

Address gõ tay (method generic `Find<$T>(...)`, overload `Ten{0}(...)`, bắc cầu `$var`): `Objects` › `+ Thêm address`.

## Trần đã biết

- **IL2CPP managed code stripping** — Advanced/`Reflect`/`Address` chạy bằng reflection; trên build IL2CPP, member không ai gọi tĩnh sẽ bị strip và inspect báo "không tìm thấy" dù code có thật. Hub là đồ dev (`RenameFolderOnBuild` đổi tên folder `Resources` khi build PRODUCTION) nên package **không** mang `link.xml` chống stripping — đây là quyết định có chủ ý, không phải thiếu sót.
- **Gọi method tuỳ ý qua trang Method có thể làm hỏng state game.** Đó là bản chất công cụ, không phải lỗi.
- **`#TypeName[i]` không ổn định qua phiên** (mục Advanced ở trên).
- **Address gõ tay với tên type ngắn** phải quét mọi assembly (lần đầu, ~20 ms), và tên ngắn khớp ≥ 2 type (`Camera` ở hai namespace) thì trả `null` — gõ full name. Address do `Duyệt` sinh ra luôn mang full name.
- **Gợi ý của `Duyệt` có độ trễ** (debounce + thread nền).
- **`IEnumerator` (coroutine Unity) không await được** — switch `Chờ kết quả` chỉ có cho kiểu theo mẫu awaiter.
- **Search không quét trong `FolderNode` động.**
- **Tên Unity object không phải định danh** — text lookup (`GameObject.Find`/`GetComponent`) không bảo đảm round-trip; dùng `$var` để giữ đúng reference trong session. Nút repeat không lưu được reference/collection chứa reference.
- **Không parse ngoặc lồng** trong address.
- **Bảng monospace là xấp xỉ** — font khác nhau thì số ký tự vừa một dòng khác nhau.

## Test

Chạy bằng CLI (không có menu item nào chạy test trong Editor):

```bash
unity cmd --project-path . run_tests --mode EditMode --filter "Hlight.Debug.Hub.Tests" --filter_type assembly
```

`AgentTestRunner` chỉ hỗ trợ `[SetUp]` / `[Test]` / `[TearDown]` — không hỗ trợ `[OneTimeSetUp]`, `[TestCase]` hay các attribute NUnit khác.
