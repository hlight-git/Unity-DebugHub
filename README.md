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

## TODO của maintainer

Repo remote chưa được tạo. Sau khi tạo, chạy:

```bash
cd Packages/com.hlight.debug-hub
git remote add origin <url>
git push -u origin master
cd ../..
git submodule add <url> Packages/com.hlight.debug-hub
```
