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

Thêm cheat = đăng ký command cho IngameDebugConsole. Đặt tên `<category>.<tên>` để nó tự vào category tương ứng trong page Commands:

```csharp
DebugLogConsole.AddCommand<int>("economy.addgold", "Cộng vàng", AddGold);
```

Command không có dấu `.` sẽ nằm trong category `General`.

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

Command không tham số: bấm là chạy. Có tham số: mở page nhập liệu, mỗi param một field theo đúng kiểu — enum ra dropdown, bool ra toggle, số chỉ nhập được số, còn lại là text với placeholder là tên kiểu. Giá trị được validate bằng `DebugLogConsole.ParseArgument` (sai thì chữ đỏ, bấm Run báo lỗi và không chạy). Khi chạy thì gọi thẳng `MethodInfo` nên overload cùng tên không bị chọn sai.

Tự thêm page riêng bằng `DebugPage` + các hàm `AddButton/AddToggle/AddField/AddText` của `DebugHubPanel`.

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
