# Hlight Debug Hub

In-game debug hub: IngameDebugConsole + cheat commands theo category + diagnostics, tất cả sau một password gate.

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

## Sử dụng

Kéo `Prefabs/DebugHub.prefab` vào scene đầu tiên. Nhập password (field `password` trên component `DebugHub`) theo trigger của platform để mở panel.

Thêm cheat = đăng ký command cho IngameDebugConsole. Đặt tên `<category>.<tên>` để nó tự vào category tương ứng trong page Commands. Command không có dấu `.` nằm trong category `General`.

Cách gọn nhất, không cần generic và không cần gọi hàm đăng ký — chỉ cần `public static` trên một type public:

```csharp
[ConsoleMethod("economy.addgold", "Cộng vàng")]
public static void AddGold(int amount) { }
```

`[ConsoleMethod]` được scan trong `DebugLogConsole.ResetStatics()` (`[RuntimeInitializeOnLoadMethod]`) nên tự đăng ký lúc game start, không phụ thuộc việc console UI có được bật hay không.

Với method instance hoặc static của type khác, cũng không cần generic:

```csharp
DebugLogConsole.AddCommandInstance("player.heal", "Hồi máu", nameof(Heal), this);
DebugLogConsole.AddCommandStatic("save.wipe", "Xoá save", nameof(Wipe), typeof(SaveSystem));
```

Dạng generic `AddCommand<T1, T2>(...)` chỉ cần khi muốn truyền delegate trực tiếp.

## Panel

Cả hub là **một panel duy nhất** điều hướng theo stack. Bấm lớp background phía sau = back một tầng; ở page gốc = đóng panel.

```
Debug Hub                 Commands              time                  time.scale [Float speed]
  [x] Console        ->     time  (3)      ->     time.scale     ->      Float speed [ 0.5 ]
  [ ] Auto enable           prefs (4)             time.skip             (status line)
  [ ] Proxima               reg   (5)             time.skip                  [ Run ]
  [ ] Show entry            ...
      Commands
      Help
```

Row chỉ hiện tên command; overload cùng tên trong một category thì kèm số lượng tham số để phân biệt.

Command không tham số: bấm là chạy. Có tham số: mở page nhập liệu, mỗi param một field theo đúng kiểu — bool ra toggle, số chỉ nhập được số, enum ra page chọn giá trị, còn lại là text với placeholder là tên kiểu. Giá trị được validate bằng `DebugLogConsole.ParseArgument` (sai thì chữ đỏ, bấm Run báo lỗi và không chạy). Khi chạy thì gọi thẳng `MethodInfo` nên overload cùng tên không bị chọn sai.

Enum dùng page chọn giá trị chứ không dùng `UI.Dropdown`: dropdown sinh canvas lồng + blocker bên trong `Mask` của scroll view nên list bị mờ và không bấm được. Cách này cũng giống `PickerEnumFieldGUI` của `Assets/Plugins/DebugPanel`.

Window cao đúng bằng nội dung, chặn trên bởi `maxWindowHeight` (mặc định 1500, quá thì scroll).

Tự thêm page riêng bằng `DebugPage` + các hàm `AddButton/AddToggle/AddField/AddChoice/AddText` của `DebugHubPanel`.

Dev note ở page Help: thêm vào `DebugHub.Notes`.

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
