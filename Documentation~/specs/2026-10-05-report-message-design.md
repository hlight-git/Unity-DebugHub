# Spec ④ — Báo lỗi và message qua API của project

Ngày: 2026-10-05. User đã duyệt trong chat. Bản 3.1.0 (3.0.0 đã push `origin/main`), chỉ thêm API, không phá API cũ.

## 1. Mục tiêu

Project có (sau này) API riêng để tạo issue và nhận message. Hub dựng sẵn chỗ mở rộng: tới lúc API sẵn sàng, project chỉ viết một class con là QA báo lỗi / gửi message được ngay từ hub.

- Hai kênh tách hẳn: **báo lỗi** (tạo issue) và **message** (chữ, đẩy log; không file).
- Hub lo UI và thứ chỉ hub có (log). Project lo mọi thứ thuộc API (endpoint, payload, token, đăng nhập) và thông tin game/máy.
- Payload API có thêm trường nào thì chỉ sửa class con trong project. Package chỉ đổi khi API cần thứ mà chỉ hub mới có (ảnh/video ở đợt 2), và lúc đó class con cũ vẫn chạy.

Đợt này **không** viết code gửi thật, không gắn gì vào project. User tự gắn component vào DebugHub trong `Root.unity`.

## 2. API public

```csharp
public abstract class BugReporter : MonoBehaviour
{
    public abstract IEnumerable<DebugNode> Fields();
    public abstract Task<string> Send(BugReport report);
}

public sealed class BugReport
{
    public BugReport(string logs);
    public string Logs { get; }
}

public abstract class MessageSender : MonoBehaviour
{
    public abstract IEnumerable<DebugNode> Fields();
    public abstract Task<string> Send(DebugMessage message);
}

public sealed class DebugMessage
{
    public DebugMessage(string logs);
    public string Logs { get; }
}

// Node
public static ValueNode Choice(string label, Func<string> get, Action<string> set,
    Func<IReadOnlyList<string>> options, string description = null);
```

- **Abstract MonoBehaviour, không interface**: cùng lý do `DebuggerAuthenticationTrigger` — ô kéo-thả trên DebugHub. `DebugHub` có hai `[SerializeField]` `reporter`, `messenger`; để trống = kênh đó không có row. Component đặt **trên chính object DebugHub** (object đó `DontDestroyOnLoad`; GameObject khác trong `Root.unity` bị unload sau boot).
- **`Fields()`**: mọi ô của form, kể cả title/description — một chủ cho cả form. Class con giữ giá trị, tự quyết giữ hay xoá sau khi gửi (assignee nên giữ, bước tái hiện nên xoá). Dùng node sẵn có: `Node.Value` (string/số → ô nhập, bool → switch, enum → trang chọn), `Node.Choice`, `Node.Text`. Gọi lại mỗi lần trang dựng lại (kể cả lùi về từ trang chọn): phải rẻ, không side effect; danh sách lấy từ API thì class con tự cache. Ném exception thì trang hiện dòng lỗi như mọi `FolderNode`.
- **`Send`**: trả chữ hiện ở dòng kết quả (mã issue, "đã gửi tới …"); null/rỗng → "Đã gửi báo lỗi" / "Đã gửi". Ném (đồng bộ hay trong Task) = lỗi. Phải tự có timeout (`UnityWebRequest.timeout`): hub không cắt. Đọc field của mình trước `await` đầu tiên.
- **`BugReport` / `DebugMessage`**: chỉ chứa thứ hub sinh ra cho lần gửi đó. `sealed` vì hub không điền được field hub không biết. Constructor public để project test class con của mình; thêm field sau này = thêm overload constructor, không vỡ.
- **`Task`**, không UniTask: package không thêm phụ thuộc; project dùng UniTask thì `.AsTask()`.

### 2.1 Log hub đưa

| | `BugReport.Logs` | `DebugMessage.Logs` |
|---|---|---|
| Nguồn | đọc thẳng `LogRecorder` (không qua `LogModel`, khỏi lệch "N log mới") | `LogModel.Shared.CopyAll()` — y hệt "Copy tất cả" |
| Lọc | bỏ qua chip, ô tìm, chip Unity, Gộp | theo bộ lọc đang bật |
| Xoá | chỉ log sau mốc Xoá (QA xoá = "tính từ đây") | theo trang log (đã gồm Xoá) |
| Chụp lúc | bấm Gửi | mở form từ trang Log (đúng thứ QA đang thấy) |
| Không kèm | — (luôn có, có thể rỗng) | `null` khi gõ tay từ `hub.message` |
| Định dạng | `LogModel.Describe` từng entry | `LogModel.Describe` |

Giới hạn = ring 4 MB sẵn có; cắt thêm là việc class con.

### 2.2 `Node.Choice`

`ValueNode` thêm field public `Options` (`Func<IReadOnlyList<string>>`). `NodeRenderer`: `Options != null && Set != null` → row chọn (dùng `AddChoice` như enum), danh sách lấy lúc dựng trang. Giá trị là chuỗi hiển thị; cần id thì class con tự map.

## 3. Luồng UI

| Chỗ bấm | Hiện khi | Trang |
|---|---|---|
| `hub.report` (Commands › hub) | `reporter` có gán | `reporter.Fields()` + row **Gửi** |
| `hub.message` (Commands › hub) | `messenger` có gán | `messenger.Fields()` + row **Gửi** |
| Log › `…` › **Gửi qua message** | `messenger` có gán **và** bộ lọc còn ≥ 1 log | dòng "Kèm N log (theo bộ lọc)" + `messenger.Fields()` + row **Gửi** |

`hub.report` / `hub.message` là `FolderNode` (children dựng lúc mở, tìm ở gốc không quét vào). Row Gửi là `ActionNode` không đăng ký.

Bấm **Gửi**:

1. Bỏ chọn ô đang gõ (`EventSystem.SetSelectedGameObject(null)`): `AddField` chỉ ghi giá trị lúc `onEndEdit`, gõ xong bấm ngay vẫn phải có giá trị cuối.
2. Kênh đang gửi → dòng kết quả "Đang gửi lần trước…", không gọi `Send` lần nữa. Khoá theo kênh: báo lỗi và message chạy song song được.
3. Dựng `BugReport` / `DebugMessage`, gọi `Send`. Task **đã xong ngay** (method `async` ném trước `await` đầu tiên ra Task lỗi sẵn, không ném đồng bộ — validate của class con rơi vào đây), ném đồng bộ, hoặc trả `null`: báo tại chỗ — thành công thì như bước 4 nhưng hiện luôn kết quả; lỗi thì row Gửi coi là chạy hỏng: **panel giữ form**, dòng đỏ, `LogException`.
4. Chưa xong: đóng panel (giữ stack: mở lại về đúng form), dòng kết quả "Đang gửi…" (row Gửi `.Reports()`, không thì `RunInspectNow` ẩn ngay). Riêng form mở từ trang Log: gửi đi rồi (bước 3 thành công hoặc bước 4) thì form rời stack — để lại thì mở hub lần sau là về form cũ, Gửi là gửi lại bộ log cũ. `SendFlow.Start` trả `false` khi kênh đang bận nên lúc đó form ở lại.
5. Coroutine trên DebugHub chờ qua `Awaitables.Wait` — cùng vòng chờ với method async của trang Method — nhưng **không trần thời gian** (trần 60 s báo lỗi trong khi request có thể vẫn thành công → QA gửi lại → issue trùng). Xong:
   - Thành công: `Debug.Log` chữ kết quả, dòng kết quả hiện chữ đó.
   - Lỗi (faulted / cancelled): `Debug.LogException` (đủ stack), dòng kết quả đỏ hiện message. Hub không đụng field của class con: mở lại là form còn nguyên.
   - Hub đang ẩn (`DebugHub.Visible == false`, ví dụ QA ẩn để quay màn hình): chỉ ghi log, không hiện dòng kết quả.
6. Không vào nút repeat: Gửi chạy qua `RunInspect` (node không đăng ký) — đường này không ghi `LastCommand`. Mở `FolderNode` cũng không ghi.

## 4. Trần đã biết (ghi README)

- URL/token trong class con nằm trong bản store (hub cố ý ship bản store); lấy ra được bằng decompile.
- `Send` không có timeout thì kênh đó kẹt "Đang gửi…" tới khi tắt app.
- Ô nhập một dòng (ô nhập tại chỗ của hub). Nhiều dòng để đợt 2.

## 5. Ngoài đợt này

- **Đợt 2 — đính kèm**: `BugReport.Attachments` (`Path` file hub chép vào cache, `MimeType`, `Bytes` — class con upload bằng `UploadHandlerFile`), `BugReport.Progress` (`IProgress<float>`), bộ chọn native (Photo Picker Android, `PHPickerViewController` iOS — không xin quyền đọc thư viện), row "+ Ảnh/Video" trên form, xoá bản chép sau khi gửi xong / lần mở app sau, ô nhiều dòng. Chỉ thêm field/overload: class con đợt 1 không vỡ.
- **Đăng nhập một lần**: phiên/token ở project (PlayerPrefs), `Fields()` đổi theo trạng thái đăng nhập, `Send` ném khi chưa đăng nhập/hết phiên. Khi làm thật có thể cần node async có phản hồi (`Node.Task`) — thêm lúc đó, không vỡ gì.
- Class gửi thật, API, cấu trúc payload, gắn component trong project.

## 6. Kiểm

EditMode (`AgentTestRunner`: chỉ `[SetUp]`/`[Test]`/`[TearDown]`, không tick player loop → dùng `Task.FromResult` / `Task.FromException` / `TaskCompletionSource` chưa xong, phần xử lý kết quả tách hàm gọi được đồng bộ):

- `hub.report` / `hub.message` chỉ đăng ký khi ô có gán; mục "Gửi qua message" chỉ hiện khi có messenger và còn log.
- `BugReport.Logs` tôn trọng Xoá, bỏ qua lọc loại / tìm / Unity / Gộp.
- `DebugMessage.Logs` = `CopyAll()` lúc mở form; `null` khi đi từ `hub.message`.
- Đang gửi thì Gửi lần hai không gọi `Send`.
- Thành công: kết quả rỗng → chữ mặc định. Lỗi: Task lỗi sẵn, ném đồng bộ, trả `null` → row hỏng (form còn mở); Task lỗi về sau → dòng đỏ + `LogException`.
- `Node.Choice` ra row chọn, chọn xong gọi `set`; danh sách ném → dòng lỗi.
- Gửi không đổi `LastCommand`.

Editor (Play) / máy thật: gõ ô rồi bấm Gửi ngay (bàn phím Android / iOS) → class con nhận giá trị cuối; trang chọn của `Node.Choice`; dòng kết quả khi panel đóng; hub đang ẩn thì không hiện dòng kết quả, log vẫn có (cần `DebugHub` đã `Awake` — EditMode không có).

## 7. Docs, version

- README: mục "Báo lỗi và message" (gắn component trên object DebugHub, ví dụ class con, bảng log), thêm 3 dòng ở "Trần đã biết".
- CHANGELOG `## 3.1.0` › Thêm; `package.json` 3.1.0.
- Chỉ sửa trong `Packages/com.hlight.debug-hub`.
