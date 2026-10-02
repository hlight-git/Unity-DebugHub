# Console nội bộ thay IngameDebugConsole — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bỏ IngameDebugConsole khỏi `com.hlight.debug-hub`, thay bằng bộ ghi log chạy từ lúc khởi động + trang log ảo hoá trong panel hub, kèm cơ chế mở khoá mới (cờ / bản nội bộ / mạng công ty).

**Architecture:** `LogRecorder` (static, ring 4 MB) nghe `logMessageReceivedThreaded` từ `SubsystemRegistration` trên máy được ghi log. `LogModel` (thuần C#, test được) kéo entry theo `Seq`, lọc/tìm/gộp/xoá. `LogView` (MonoBehaviour) là một trang trong `DebugHubPanel`, mượn `ScrollRect` của panel, pool ~15 hàng cao cố định. Mở khoá tách hai quyền: ghi log (cờ ∨ bản nội bộ) và mở hub (chỉ cờ); cờ ghi bởi password hoặc dấu hiệu mạng công ty kiểm song song với ô password. Parser đối số mang từ IDC (MIT) vào `DebugValues`.

**Tech Stack:** Unity 6000.3, C#, uGUI + TextMeshPro (`com.unity.ugui` 2.0), Input System, NUnit EditMode qua `AgentTestRunner`, `unity cmd` (Unity CLI) để recompile / chạy test / sửa prefab.

**Spec:** `Packages/com.hlight.debug-hub/Documentation~/specs/2026-10-01-in-house-console-design.md` — đọc cùng plan này.

## Global Constraints

- Chỉ sửa trong `Packages/com.hlight.debug-hub`. Cấu hình project (password, dấu hiệu mạng trên `Root.unity`) là việc của user — chỉ báo chỗ.
- **Không commit.** User tự commit. Mỗi task kết thúc bằng checkpoint `git status`, không `git commit`.
- Package không mang font: TMP mới trong prefab phải **clone từ TMP có sẵn** (`m_fontAsset: {fileID: 0}`), không `AddComponent<TextMeshProUGUI>()`.
- Ký tự ngoài chữ cái trong UI chỉ được dùng `› ‹ … – — ×` (font game). Không `·`, `▶`, `↓`, `→`.
- Dữ liệu game đưa vào TMP phải qua `LogText.Escape` (`<noparse>`).
- Không null-guard `[SerializeField]`; couple state + side effect bằng property setter.
- EditMode test: `Awake` không chắc chạy khi `Instantiate` prefab → listener phải gắn trong method gọi tường minh (`Open`, `Wire`…), không dựa vào `Awake`. `AgentTestRunner` chỉ hỗ trợ `[SetUp]` / `[Test]` / `[TearDown]`.
- Hằng số: ngân sách ring `4 * 1024 * 1024`; `LogView.ROW_HEIGHT = 176f`; `LogView.BAR_HEIGHT = 112f`; timeout mạng 2 s; `TRACE_URL = "https://www.cloudflare.com/cdn-cgi/trace"`; App Tester `"dev.firebase.appdistribution"`; key `DebugHub.AuthenticationState` = 2 (giữ nguyên), `DebugHub.EntryVisible`.
- Comment theo giọng code hiện có: tiếng Việt, `///` nói *vì sao*, `ponytail:` cho đơn giản hoá có trần.

## Lệnh dùng chung

Chạy từ root project `C:\Assets\UnityProjects\ig-harvest-jam`. Editor phải đang mở project.

Tạo một lần `Temp/debughub/run-tests.cs`:

```csharp
System.Type.GetType("Hlight.Debug.Hub.Tests.AgentTestRunner, Hlight.Debug.Hub.Tests").GetMethod("Run").Invoke(null, null);
return System.IO.File.ReadAllText("Temp/debug-hub-tests.txt");
```

**Build + test** (gọi là "chạy test" ở các task):

```bash
unity cmd --project-path . recompile
unity cmd --project-path . recompile_status
unity cmd --project-path . --timeout 300 eval_file --file Temp/debughub/run-tests.cs
```

Lặp `recompile_status` tới `completed` hoặc `up_to_date`. `up_to_date` vẫn có thể đã nạp code mới — nếu nghi, `eval` một symbol vừa thêm để chắc. Kết quả: dòng đầu `PASS=n FAIL=m`, sau đó từng test. Lỗi compile → `eval_file` báo assembly test không có.

Script sửa prefab (task 8–11) chạy bằng `unity cmd --project-path . --timeout 120 eval_file --file Temp/debughub/<tên>.cs`. Code trong `eval` không nhận `using` → mọi kiểu viết tên đầy đủ.

---

## File structure

| File | Trách nhiệm |
|---|---|
| `Scripts/Model/DebugValues.Parse.cs` (mới) | parser đối số mang từ IDC: tách đối số, bảng parse theo kiểu, tên kiểu dễ đọc |
| `Scripts/Access/HubAccess.cs` (mới) | cờ mở khoá, luật "được ghi log" |
| `Scripts/Access/InstallSource.cs` (mới) | bản nội bộ: Editor / iOS sandbox receipt / Android App Tester |
| `Plugins/iOS/DebugHubInstallSource.mm` (mới) | đọc `appStoreReceiptURL` |
| `Scripts/Access/CompanyNetwork.cs` (mới) | dấu hiệu mạng công ty: trang nội bộ + IP công khai |
| `Scripts/Editor/LocalNetworkUsage.cs` (mới) | thêm `NSLocalNetworkUsageDescription` khi build iOS |
| `Scripts/Logs/LogEntry.cs`, `LogRecorder.cs` (mới) | entry + bộ ghi ring |
| `Scripts/Logs/StackFrames.cs`, `LogText.cs`, `LogModel.cs` (mới) | đọc stack, định dạng chữ, view model trang log |
| `Scripts/Logs/LogRowView.cs`, `LogView.cs` (mới) | hàng log + danh sách ảo hoá |
| `Scripts/Pages/LogPage.cs`, `LogDetailPage.cs` (mới) | trang log, trang chi tiết |
| `Scripts/Panel/DebugHubBar.cs`, `EmbeddedEventSystem.cs` (mới) | hàng nút ngang; thay `EventSystemHandler` của IDC |
| `Scripts/Features/BuiltinCommands.cs` (đổi tên từ `ConsoleController.cs`, giữ GUID) | command có sẵn |
| `Scripts/DebugHub.cs`, `Scripts/Panel/DebugHubPanel.cs`, `DebugPage.cs`, `DebugHubIcon.cs`, `DebugHubEntry.cs`, `DebugHubToast.cs`, `Model/DebugRegistry.cs`, `Model/DebugValues.cs`, `Inspect/Address.cs`, `Inspect/Reflect.cs`, `Pages/HelpPage.cs` | sửa |
| `Prefabs/DebugHub.prefab` | thêm UI log, nút header, BarRow, badge, đổi component EventSystem |
| xoá | `Scripts/Editor/RenameFolderOnBuild.cs`, `Scripts/DebuggerAuthenticationTriggers/NetworkReachabilityAuthenticationBypass.cs`, `Resources/`, `ThirdParty/` (submodule) |

---

### Task 1: Gỡ symbol và xử lý `PRODUCTION`

**Files:**
- Modify: `Scripts/DebugHub.cs:92-102`, `:116-144`
- Modify: `Scripts/Model/DebugRegistry.cs:260-285`
- Modify: `Scripts/Features/ConsoleController.cs:25-27`
- Modify: `Tests/Editor/DebugRegistryTests.cs` (xoá một test)
- Delete: `Scripts/Editor/RenameFolderOnBuild.cs` + `.meta`

**Interfaces:**
- Consumes: —
- Produces: `DebugHub.Unlocked` chỉ còn đọc field `unlocked`.

- [ ] **Step 1: Xoá test của hành vi bị gỡ**

Trong `Tests/Editor/DebugRegistryTests.cs` xoá nguyên test `Run_CapturesLogs_EvenWhenUnityLoggingIsSilenced` (khối `[Test] public void Run_CapturesLogs_EvenWhenUnityLoggingIsSilenced() { … }`, quanh dòng 207–219).

- [ ] **Step 2: Bỏ bật tạm logger trong `Capture`**

Trong `Scripts/Model/DebugRegistry.cs`, thay:

```csharp
            // Build tắt log của Unity (com.hlight.logging dưới PRODUCTION) thì không có log nào để bắt —
            // bật tạm trong lúc lệnh chạy, xong trả lại như cũ.
            var logger = UnityEngine.Debug.unityLogger;
            var logEnabled = logger.logEnabled;
            logger.logEnabled = true;

            Exception failure = null;
            Application.logMessageReceived += capture;
            try { run(); }
            catch (Exception exception) { failure = exception.Unwrap(); }
            finally
            {
                Application.logMessageReceived -= capture;
                logger.logEnabled = logEnabled;
            }
```

bằng:

```csharp
            Exception failure = null;
            Application.logMessageReceived += capture;
            try { run(); }
            catch (Exception exception) { failure = exception.Unwrap(); }
            finally { Application.logMessageReceived -= capture; }
```

- [ ] **Step 3: Bỏ bật logger khi mở console**

Trong `Scripts/Features/ConsoleController.cs` xoá ba dòng:

```csharp
                // Build tắt log của Unity (com.hlight.logging dưới PRODUCTION) thì cửa sổ log trống trơn.
                // ponytail: bật lại cho hết phiên, không tắt lại khi đóng — chỉ máy tester mới tới được đây.
                if (value) UnityEngine.Debug.unityLogger.logEnabled = true;
```

- [ ] **Step 4: Bỏ `ALWAYS_ENABLE_INGAME_DEBUGGER` và `DISABLE_DEBUG_HUB` trong `DebugHub.cs`**

Thay property `Unlocked` (dòng 92–102) bằng:

```csharp
        private bool Unlocked => unlocked;
```

Thay đầu `Awake` (dòng 118–124):

```csharp
#if !DISABLE_DEBUG_HUB
            if (instance)
#endif
            {
                Destroy(gameObject);
                return;
            }
```

bằng:

```csharp
            if (instance)
            {
                Destroy(gameObject);
                return;
            }
```

Thay comment dòng 128–129 bằng:

```csharp
            // Đăng ký command từ đây chứ không từ Awake của console: bản trùng bị huỷ ngay trên kia thì
            // không đăng ký gì.
```

Thay dòng 141–144:

```csharp
#if !ALWAYS_ENABLE_INGAME_DEBUGGER
            if (string.IsNullOrEmpty(password))
                UnityEngine.Debug.LogError("[DebugHub] Chưa điền password (Inspector của component DebugHub) — hub không mở được.");
#endif
```

bằng:

```csharp
            if (string.IsNullOrEmpty(password))
                UnityEngine.Debug.LogError("[DebugHub] Chưa điền password (Inspector của component DebugHub) — hub không mở được.");
```

- [ ] **Step 5: Xoá `RenameFolderOnBuild`**

```bash
git -C Packages/com.hlight.debug-hub rm -q Scripts/Editor/RenameFolderOnBuild.cs Scripts/Editor/RenameFolderOnBuild.cs.meta
```

`Scripts/Editor/Hlight.Debug.Hub.Editor.asmdef` giữ lại (task 4 thêm script vào). Asmdef tạm không có script là bình thường.

- [ ] **Step 6: Kiểm không còn symbol trong code**

```bash
grep -rn "ALWAYS_ENABLE_INGAME_DEBUGGER\|DISABLE_DEBUG_HUB\|logEnabled" Packages/com.hlight.debug-hub/Scripts Packages/com.hlight.debug-hub/Tests
```

Expected: không có dòng nào. (README/CHANGELOG sửa ở task 13.)

- [ ] **Step 7: Chạy test**

Expected: `FAIL=0`.

- [ ] **Step 8: Checkpoint** — `git -C Packages/com.hlight.debug-hub status --short`. Không commit.

---

### Task 2: Parser đối số tự giữ

**Files:**
- Create: `Scripts/Model/DebugValues.Parse.cs`
- Modify: `Scripts/Model/DebugValues.cs`, `Scripts/Inspect/Address.cs:6,502,604,625`, `Scripts/Model/DebugRegistry.cs:4,384,395`, `Scripts/Pages/HelpPage.cs:4,45`, `Scripts/Panel/DebugHubPanel.cs:3,715,751`, `Scripts/Inspect/Reflect.cs:349-352`
- Test: `Tests/Editor/ArgumentParserTests.cs`

**Interfaces:**
- Produces (đều `internal static` trên `DebugValues`):
  - `void SplitArguments(string command, List<string> into)` — đúng hành vi `DebugLogConsole.FetchArgumentsFromCommand`.
  - `bool ParseArgument(string input, Type type, out object output)` — đúng `DebugLogConsole.ParseArgument`.
  - `string ReadableName(Type type)` — đúng `DebugLogConsole.GetTypeReadableName`.

- [ ] **Step 1: Viết test**

`Tests/Editor/ArgumentParserTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Parser mang từ IDC phải giữ đúng hành vi cũ: dòng lệnh đã lưu (nút repeat, Watches) parse lại y như trước.
    public class ArgumentParserTests
    {
        [Flags]
        public enum TestFlags { A = 1, B = 2 }

        private static List<string> Split(string line)
        {
            var into = new List<string>();
            DebugValues.SplitArguments(line, into);
            return into;
        }

        private static object Parse(string text, Type type)
        {
            Assert.IsTrue(DebugValues.ParseArgument(text, type, out var value), $"'{text}' → {type.Name}");
            return value;
        }

        [Test]
        public void Split_OnSpaces()
        {
            CollectionAssert.AreEqual(new[] { "level.goto", "5" }, Split("level.goto 5"));
        }

        [Test]
        public void Split_KeepsQuotedSpaces()
        {
            CollectionAssert.AreEqual(new[] { "a", "b c", "d" }, Split("a \"b c\" d"));
        }

        [Test]
        public void Split_Brackets_KeepNesting()
        {
            CollectionAssert.AreEqual(new[] { "1 2", "3 4" }, Split("[1 2] [3 4]"));
            CollectionAssert.AreEqual(new[] { "[1 2] [3 4]" }, Split("[[1 2] [3 4]]"));
        }

        [Test]
        public void Split_DropsTrailingComma()
        {
            CollectionAssert.AreEqual(new[] { "1", "2" }, Split("1, 2"));
        }

        [Test]
        public void Parse_Numbers_InvariantCulture_AndSuffixes()
        {
            Assert.AreEqual(0.5f, Parse("0.5f", typeof(float)));
            Assert.AreEqual(12L, Parse("12L", typeof(long)));
            Assert.AreEqual(true, Parse("1", typeof(bool)));
            Assert.AreEqual(false, Parse("FALSE", typeof(bool)));
        }

        [Test]
        public void Parse_Vector3_FillsMissingWithZero()
        {
            Assert.AreEqual(new Vector3(1f, 2f, 0f), Parse("1, 2", typeof(Vector3)));
        }

        [Test]
        public void Parse_FlagsEnum_CaseInsensitive()
        {
            Assert.AreEqual(TestFlags.A | TestFlags.B, Parse("a|B", typeof(TestFlags)));
        }

        [Test]
        public void Parse_ArrayAndList()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, (int[])Parse("1 2 3", typeof(int[])));
            CollectionAssert.AreEqual(new List<int> { 4, 5 }, (List<int>)Parse("4 5", typeof(List<int>)));
        }

        [Test]
        public void Parse_UnsupportedType_ReturnsFalse()
        {
            Assert.IsFalse(DebugValues.ParseArgument("x", typeof(Uri), out _));
        }

        [Test]
        public void ReadableName_UsesFriendlyNames()
        {
            Assert.AreEqual("Integer", DebugValues.ReadableName(typeof(int)));
            Assert.AreEqual("Integer[]", DebugValues.ReadableName(typeof(int[])));
            Assert.AreEqual("Vector3", DebugValues.ReadableName(typeof(Vector3)));
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile (`SplitArguments`, `ParseArgument`, `ReadableName` chưa có).

- [ ] **Step 3: Viết `Scripts/Model/DebugValues.Parse.cs`**

```csharp
// Mang từ IngameDebugConsole (Plugins/IngameDebugConsole/Scripts/DebugLogConsole.cs), đã cắt phần hub không dùng.
//
// The MIT License (MIT)
//
// Copyright (c) 2016 Süleyman Yasir KULA
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    internal static partial class DebugValues
    {
        private delegate bool ParseFunction(string input, out object output);

        /// Bảng parse duy nhất của hub: kiểu có ở đây là "parse được" (CanParse / IsInlineValue đọc thẳng
        /// bảng này, không còn bản chép tay).
        private static readonly Dictionary<Type, ParseFunction> Parsers = new()
        {
            { typeof(string), ParseString },
            { typeof(bool), ParseBool },
            { typeof(int), ParseInt },
            { typeof(uint), ParseUInt },
            { typeof(long), ParseLong },
            { typeof(ulong), ParseULong },
            { typeof(byte), ParseByte },
            { typeof(sbyte), ParseSByte },
            { typeof(short), ParseShort },
            { typeof(ushort), ParseUShort },
            { typeof(char), ParseChar },
            { typeof(float), ParseFloat },
            { typeof(double), ParseDouble },
            { typeof(decimal), ParseDecimal },
            { typeof(Vector2), (string s, out object o) => ParseVector(s, typeof(Vector2), out o) },
            { typeof(Vector3), (string s, out object o) => ParseVector(s, typeof(Vector3), out o) },
            { typeof(Vector4), (string s, out object o) => ParseVector(s, typeof(Vector4), out o) },
            { typeof(Quaternion), (string s, out object o) => ParseVector(s, typeof(Quaternion), out o) },
            { typeof(Color), (string s, out object o) => ParseVector(s, typeof(Color), out o) },
            { typeof(Color32), (string s, out object o) => ParseVector(s, typeof(Color32), out o) },
            { typeof(Rect), (string s, out object o) => ParseVector(s, typeof(Rect), out o) },
            { typeof(RectOffset), (string s, out object o) => ParseVector(s, typeof(RectOffset), out o) },
            { typeof(Bounds), (string s, out object o) => ParseVector(s, typeof(Bounds), out o) },
            { typeof(GameObject), ParseGameObject },
            { typeof(Vector2Int), (string s, out object o) => ParseVector(s, typeof(Vector2Int), out o) },
            { typeof(Vector3Int), (string s, out object o) => ParseVector(s, typeof(Vector3Int), out o) },
            { typeof(RectInt), (string s, out object o) => ParseVector(s, typeof(RectInt), out o) },
            { typeof(BoundsInt), (string s, out object o) => ParseVector(s, typeof(BoundsInt), out o) },
        };

        private static readonly Dictionary<Type, string> ReadableNames = new()
        {
            { typeof(string), "String" },
            { typeof(bool), "Boolean" },
            { typeof(int), "Integer" },
            { typeof(uint), "Unsigned Integer" },
            { typeof(long), "Long" },
            { typeof(ulong), "Unsigned Long" },
            { typeof(byte), "Byte" },
            { typeof(sbyte), "Short Byte" },
            { typeof(short), "Short" },
            { typeof(ushort), "Unsigned Short" },
            { typeof(char), "Char" },
            { typeof(float), "Float" },
            { typeof(double), "Double" },
            { typeof(decimal), "Decimal" },
        };

        /// Nhóm bao một đối số: nháy và ngoặc. Ngoặc lồng được (`[[1 2] [3 4]]` cho mảng Vector2).
        private static readonly string[] Delimiters = { "\"\"", "''", "{}", "()", "[]" };

        /// Tách dòng lệnh thành đối số: cách bằng khoảng trắng, nhóm trong nháy/ngoặc là một đối số (bỏ vỏ),
        /// dấu phẩy ngay sau đối số bị bỏ.
        internal static void SplitArguments(string command, List<string> into)
        {
            for (var i = 0; i < command.Length; i++)
            {
                if (char.IsWhiteSpace(command[i])) continue;

                var delimiter = IndexOfDelimiterGroup(command[i]);
                if (delimiter >= 0)
                {
                    var end = IndexOfDelimiterGroupEnd(command, delimiter, i + 1);
                    into.Add(command.Substring(i + 1, end - i - 1));
                    i = end < command.Length - 1 && command[end + 1] == ',' ? end + 1 : end;
                }
                else
                {
                    var end = IndexOfChar(command, ' ', i + 1);
                    into.Add(command.Substring(i, command[end - 1] == ',' ? end - 1 - i : end - i));
                    i = end;
                }
            }
        }

        internal static bool ParseArgument(string input, Type type, out object output)
        {
            if (Parsers.TryGetValue(type, out var parse)) return parse(input, out output);
            if (typeof(Component).IsAssignableFrom(type)) return ParseComponent(input, type, out output);
            if (type.IsEnum) return ParseEnum(input, type, out output);
            if (IsSupportedCollection(type)) return ParseCollection(input, type, out output);
            output = null;
            return false;
        }

        /// "Integer", "Float[]"… — placeholder ô nhập và trang Help.
        internal static string ReadableName(Type type)
        {
            if (ReadableNames.TryGetValue(type, out var name)) return name;
            if (IsSupportedCollection(type))
            {
                var element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                return (ReadableNames.TryGetValue(element, out var elementName) ? elementName : element.Name) + "[]";
            }
            return type.Name;
        }

        private static bool IsSupportedCollection(Type type)
        {
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1) return false;
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() != typeof(List<>)) return false;
                type = type.GetGenericArguments()[0];
            }
            else return false;

            return Parsers.ContainsKey(type) || typeof(Component).IsAssignableFrom(type) || type.IsEnum;
        }

        private static int IndexOfDelimiterGroup(char c)
        {
            for (var i = 0; i < Delimiters.Length; i++)
            {
                if (c == Delimiters[i][0]) return i;
            }
            return -1;
        }

        private static int IndexOfDelimiterGroupEnd(string command, int delimiter, int start)
        {
            var open = Delimiters[delimiter][0];
            var close = Delimiters[delimiter][1];
            var depth = 1;
            for (var i = start; i < command.Length; i++)
            {
                var c = command[i];
                if (c == close && --depth <= 0) return i;
                if (c == open) depth++;
            }
            return command.Length;
        }

        private static int IndexOfChar(string command, char c, int start)
        {
            var index = command.IndexOf(c, start);
            return index < 0 ? command.Length : index;
        }

        private static bool ParseString(string input, out object output)
        {
            output = input;
            return true;
        }

        private static bool ParseBool(string input, out object output)
        {
            if (input == "1" || input.ToLowerInvariant() == "true")
            {
                output = true;
                return true;
            }
            if (input == "0" || input.ToLowerInvariant() == "false")
            {
                output = false;
                return true;
            }
            output = false;
            return false;
        }

        private static bool ParseInt(string input, out object output)
        {
            var ok = int.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseUInt(string input, out object output)
        {
            var ok = uint.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseLong(string input, out object output)
        {
            var ok = long.TryParse(TrimSuffix(input, "L"), out var value);
            output = value;
            return ok;
        }

        private static bool ParseULong(string input, out object output)
        {
            var ok = ulong.TryParse(TrimSuffix(input, "L"), out var value);
            output = value;
            return ok;
        }

        private static bool ParseByte(string input, out object output)
        {
            var ok = byte.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseSByte(string input, out object output)
        {
            var ok = sbyte.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseShort(string input, out object output)
        {
            var ok = short.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseUShort(string input, out object output)
        {
            var ok = ushort.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseChar(string input, out object output)
        {
            var ok = char.TryParse(input, out var value);
            output = value;
            return ok;
        }

        private static bool ParseFloat(string input, out object output)
        {
            var ok = float.TryParse(TrimSuffix(input, "f"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            output = value;
            return ok;
        }

        private static bool ParseDouble(string input, out object output)
        {
            var ok = double.TryParse(TrimSuffix(input, "f"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            output = value;
            return ok;
        }

        private static bool ParseDecimal(string input, out object output)
        {
            var ok = decimal.TryParse(TrimSuffix(input, "f"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            output = value;
            return ok;
        }

        private static string TrimSuffix(string input, string suffix) =>
            input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? input.Substring(0, input.Length - 1) : input;

        /// Trả true kể cả khi không tìm thấy (output null) — TryParse phía trên mới từ chối null cho kiểu Unity.
        private static bool ParseGameObject(string input, out object output)
        {
            output = input == "null" ? null : GameObject.Find(input);
            return true;
        }

        private static bool ParseComponent(string input, Type type, out object output)
        {
            var gameObject = input == "null" ? null : GameObject.Find(input);
            output = gameObject ? gameObject.GetComponent(type) : null;
            return true;
        }

        /// Tên hoặc số, nối bằng `|` (OR) / `&` (AND), không phân biệt hoa thường.
        private static bool ParseEnum(string input, Type type, out object output)
        {
            const int NONE = 0, OR = 1, AND = 2;
            var result = 0;
            var operation = NONE;
            for (var i = 0; i < input.Length; i++)
            {
                var orIndex = input.IndexOf('|', i);
                var andIndex = input.IndexOf('&', i);
                var token = orIndex < 0
                    ? input.Substring(i, (andIndex < 0 ? input.Length : andIndex) - i).Trim()
                    : input.Substring(i, (andIndex < 0 ? orIndex : Mathf.Min(andIndex, orIndex)) - i).Trim();

                if (!int.TryParse(token, out var value))
                {
                    try
                    {
                        value = Convert.ToInt32(Enum.Parse(type, token, true));
                    }
                    catch
                    {
                        output = null;
                        return false;
                    }
                }

                if (operation == NONE) result = value;
                else if (operation == OR) result |= value;
                else result &= value;

                if (orIndex >= 0)
                {
                    if (andIndex > orIndex)
                    {
                        operation = AND;
                        i = andIndex;
                    }
                    else
                    {
                        operation = OR;
                        i = orIndex;
                    }
                }
                else if (andIndex >= 0)
                {
                    operation = AND;
                    i = andIndex;
                }
                else i = input.Length;
            }

            output = Enum.ToObject(type, result);
            return true;
        }

        private static bool ParseCollection(string input, Type type, out object output)
        {
            var values = new List<string>(2);
            SplitArguments(input, values);

            var result = (IList)Activator.CreateInstance(type, new object[] { values.Count });
            output = result;
            var element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            for (var i = 0; i < values.Count; i++)
            {
                if (!ParseArgument(values[i], element, out var item)) return false;
                if (type.IsArray) result[i] = item;
                else result.Add(item);
            }
            return true;
        }

        /// Thiếu thành phần thì điền 0 (Color: đen, Quaternion: identity), thừa thì bỏ.
        private static bool ParseVector(string input, Type type, out object output)
        {
            var tokens = new List<string>(input.Replace(',', ' ').Trim().Split(' '));
            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                tokens[i] = tokens[i].Trim();
                if (tokens[i].Length == 0) tokens.RemoveAt(i);
            }

            var v = new float[tokens.Count];
            for (var i = 0; i < tokens.Count; i++)
            {
                if (!ParseFloat(tokens[i], out var parsed))
                {
                    output = type == typeof(Vector3) ? Vector3.zero : type == typeof(Vector2) ? Vector2.zero : (object)Vector4.zero;
                    return false;
                }
                v[i] = (float)parsed;
            }

            float At(int i) => i < v.Length ? v[i] : 0f;
            int Round(int i) => Mathf.RoundToInt(At(i));

            if (type == typeof(Vector2)) output = new Vector2(At(0), At(1));
            else if (type == typeof(Vector3)) output = new Vector3(At(0), At(1), At(2));
            else if (type == typeof(Vector4)) output = new Vector4(At(0), At(1), At(2), At(3));
            else if (type == typeof(Quaternion))
            {
                var q = Quaternion.identity;
                for (var i = 0; i < v.Length && i < 4; i++) q[i] = v[i];
                output = q;
            }
            else if (type == typeof(Color))
            {
                var c = Color.black;
                for (var i = 0; i < v.Length && i < 4; i++) c[i] = v[i];
                output = c;
            }
            else if (type == typeof(Color32))
            {
                var c = new Color32(0, 0, 0, 255);
                if (v.Length > 0) c.r = (byte)Round(0);
                if (v.Length > 1) c.g = (byte)Round(1);
                if (v.Length > 2) c.b = (byte)Round(2);
                if (v.Length > 3) c.a = (byte)Round(3);
                output = c;
            }
            else if (type == typeof(Rect)) output = new Rect(At(0), At(1), At(2), At(3));
            else if (type == typeof(RectOffset)) output = new RectOffset(Round(0), Round(1), Round(2), Round(3));
            else if (type == typeof(Bounds)) output = new Bounds(new Vector3(At(0), At(1), At(2)), new Vector3(At(3), At(4), At(5)));
            else if (type == typeof(Vector2Int)) output = new Vector2Int(Round(0), Round(1));
            else if (type == typeof(Vector3Int)) output = new Vector3Int(Round(0), Round(1), Round(2));
            else if (type == typeof(RectInt)) output = new RectInt(Round(0), Round(1), Round(2), Round(3));
            else if (type == typeof(BoundsInt))
                output = new BoundsInt(new Vector3Int(Round(0), Round(1), Round(2)), new Vector3Int(Round(3), Round(4), Round(5)));
            else
            {
                output = null;
                return false;
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: Nối `DebugValues.cs` vào bảng mới**

Trong `Scripts/Model/DebugValues.cs`:
- Xoá `using IngameDebugConsole;`.
- Thay doc comment lớp + khai báo:

```csharp
    /// Parser của hub (bảng parse ở DebugValues.Parse.cs, mang từ IngameDebugConsole) cộng hai câu hỏi
    /// mà parser không tự trả lời: "kiểu này có đường parse không" và "kiểu này có editor gõ tại chỗ không".
    internal static partial class DebugValues
```

- Xoá nguyên field `Parseable` (comment + `HashSet<Type>` dòng 17–28).
- Trong `CanParse`: `Parseable.Contains(type)` → `Parsers.ContainsKey(type)`, `Parseable.Contains(element)` → `Parsers.ContainsKey(element)`.
- Trong `IsInlineValue`: `return Parseable.Contains(type) && type != typeof(GameObject);` → `return Parsers.ContainsKey(type) && type != typeof(GameObject);`.
- Trong `TryParse`, thay:

```csharp
            if (!DebugLogConsole.ParseArgument(text, declared, out value))
            {
                error = $"'{text}' không phải {DebugLogConsole.GetTypeReadableName(declared)}";
                return false;
            }

            // ParseGameObject/ParseComponent của IDC trả true kể cả khi Find không thấy gì.
```

bằng:

```csharp
            if (!ParseArgument(text, declared, out value))
            {
                error = $"'{text}' không phải {ReadableName(declared)}";
                return false;
            }

            // ParseGameObject/ParseComponent trả true kể cả khi Find không thấy gì.
```

- Comment của `TryToArgument` nhắc `FetchArgumentsFromCommand` (2 chỗ) → đổi thành `SplitArguments`.

- [ ] **Step 5: Đổi các chỗ gọi IDC**

- `Scripts/Inspect/Address.cs`: xoá `using IngameDebugConsole;`; ba chỗ `DebugLogConsole.FetchArgumentsFromCommand(` → `DebugValues.SplitArguments(`.
- `Scripts/Model/DebugRegistry.cs`: xoá `using IngameDebugConsole;`; hai chỗ `DebugLogConsole.FetchArgumentsFromCommand(` → `DebugValues.SplitArguments(`; comment "Tách bằng parser của IDC nên quote xử lý y như console." → "Tách bằng DebugValues.SplitArguments: quote và ngoặc xử lý như mọi ô nhập khác."; comment đầu file dòng 11 nhắc `ConsoleMethodInfo` giữ nguyên ý nhưng bỏ tên IDC: "…thì hub tự giữ để có description, hình thái, owner."
- `Scripts/Pages/HelpPage.cs`: xoá `using IngameDebugConsole;`; `DebugLogConsole.GetTypeReadableName(parameter.Type)` → `DebugValues.ReadableName(parameter.Type)`.
- `Scripts/Panel/DebugHubPanel.cs`: xoá `using IngameDebugConsole;`; dòng 751 `DebugLogConsole.GetTypeReadableName(shape)` → `DebugValues.ReadableName(shape)`; comment dòng 715 "DebugLogConsole.ParseArgument" → "DebugValues.ParseArgument".
- `Scripts/Inspect/Reflect.cs`: thay hàm cuối

```csharp
        private static string DebugLogConsoleName(Type type)
        {
            return type == typeof(void) ? null : IngameDebugConsole.DebugLogConsole.GetTypeReadableName(type);
        }
```

bằng

```csharp
        private static string ReturnTypeName(Type type)
        {
            return type == typeof(void) ? null : DebugValues.ReadableName(type);
        }
```

và chỗ gọi dòng 262 `DebugLogConsoleName(method.ReturnType)` → `ReturnTypeName(method.ReturnType)`.

- [ ] **Step 6: Kiểm chỉ còn ConsoleController dùng IDC**

```bash
grep -rln "IngameDebugConsole\|DebugLogConsole" Packages/com.hlight.debug-hub/Scripts
```

Expected: chỉ `Scripts/Features/ConsoleController.cs` (lệnh cầu `hub`, gỡ ở task 12) và `Scripts/Model/DebugValues.Parse.cs` (comment bản quyền).

- [ ] **Step 7: Chạy test** — Expected: `FAIL=0`, có 10 test `ArgumentParserTests` pass; `DebugValuesTests`, `AddressTests`, `DebugRegistryTests` vẫn pass không sửa.

- [ ] **Step 8: Checkpoint** — `git status`, không commit.

---

### Task 3: Quyền truy cập, nguồn cài, bong bóng nhớ trạng thái

**Files:**
- Create: `Scripts/Access/HubAccess.cs`, `Scripts/Access/InstallSource.cs`, `Plugins/iOS/DebugHubInstallSource.mm`
- Modify: `Scripts/DebugHub.cs`, `Scripts/Features/ConsoleController.cs` (`hub.entry`)
- Test: `Tests/Editor/HubAccessTests.cs`

**Interfaces:**
- Produces:
  - `HubAccess.UNLOCK_KEY` (`"DebugHub.AuthenticationState"`), `bool HubAccess.ReadUnlocked()`, `void HubAccess.SaveUnlocked()`, `bool HubAccess.MayRecord(bool unlocked, bool internalBuild)`, `bool HubAccess.MayRecordNow()`.
  - `InstallSource.APP_TESTER`, `bool InstallSource.IsInternal`, `bool InstallSource.IsAppTester(string initiating, string installing)`.
  - `DebugHub.ENTRY_VISIBLE_KEY` (`"DebugHub.EntryVisible"`).

- [ ] **Step 1: Viết test**

`Tests/Editor/HubAccessTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class HubAccessTests
    {
        private bool hadKey;
        private int savedValue;

        [SetUp]
        public void SetUp()
        {
            hadKey = PlayerPrefs.HasKey(HubAccess.UNLOCK_KEY);
            savedValue = PlayerPrefs.GetInt(HubAccess.UNLOCK_KEY);
            PlayerPrefs.DeleteKey(HubAccess.UNLOCK_KEY);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadKey) PlayerPrefs.SetInt(HubAccess.UNLOCK_KEY, savedValue);
            else PlayerPrefs.DeleteKey(HubAccess.UNLOCK_KEY);
        }

        /// Máy đã mở khoá ở bản cũ phải vẫn mở: cùng key, cùng giá trị 2.
        [Test]
        public void Unlock_IsRememberedUnderTheOldKey()
        {
            Assert.IsFalse(HubAccess.ReadUnlocked());
            HubAccess.SaveUnlocked();
            Assert.IsTrue(HubAccess.ReadUnlocked());
            Assert.AreEqual(2, PlayerPrefs.GetInt("DebugHub.AuthenticationState"));
        }

        [Test]
        public void Record_WhenUnlockedOrInternal_Only()
        {
            Assert.IsTrue(HubAccess.MayRecord(true, false));
            Assert.IsTrue(HubAccess.MayRecord(false, true));
            Assert.IsFalse(HubAccess.MayRecord(false, false));
        }

        /// App Tester có thể tự cài (installing) hoặc nhờ trình cài hệ thống (initiating).
        [Test]
        public void AppTester_MatchesEitherField()
        {
            Assert.IsTrue(InstallSource.IsAppTester(InstallSource.APP_TESTER, "com.google.android.packageinstaller"));
            Assert.IsTrue(InstallSource.IsAppTester(null, InstallSource.APP_TESTER));
            Assert.IsFalse(InstallSource.IsAppTester("com.android.vending", "com.android.vending"));
            Assert.IsFalse(InstallSource.IsAppTester(null, null));
        }

        [Test]
        public void Editor_IsInternal()
        {
            Assert.IsTrue(InstallSource.IsInternal);
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile.

- [ ] **Step 3: Viết `Scripts/Access/HubAccess.cs`**

```csharp
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Hai quyền tách bạch (spec ① §3.1). Ghi log vô hình, chỉ nằm trong RAM, nên cấp rộng hơn mở hub:
    /// reviewer Apple cài sandbox giống hệt TestFlight — bị ghi log cũng không thấy gì, nhưng mở được hub
    /// (bong bóng, lắc) thì lộ.
    internal static class HubAccess
    {
        /// Cùng key và giá trị với bản trước: máy đã mở khoá thì vẫn mở.
        internal const string UNLOCK_KEY = "DebugHub.AuthenticationState";
        private const int UNLOCKED = 2;

        /// Cờ mở hub: password hoặc dấu hiệu mạng công ty đã ghi.
        internal static bool ReadUnlocked() => PlayerPrefs.GetInt(UNLOCK_KEY) == UNLOCKED;

        /// Save ngay: mở khoá là việc một lần, app bị giết trước lần auto-save là mất.
        internal static void SaveUnlocked()
        {
            PlayerPrefs.SetInt(UNLOCK_KEY, UNLOCKED);
            PlayerPrefs.Save();
        }

        internal static bool MayRecord(bool unlocked, bool internalBuild) => unlocked || internalBuild;

        /// Hỏi nguồn cài trước: bản nội bộ thì khỏi đọc PlayerPrefs ở pha khởi động sớm nhất.
        internal static bool MayRecordNow() => InstallSource.IsInternal || ReadUnlocked();
    }
}
```

- [ ] **Step 4: Viết `Scripts/Access/InstallSource.cs`**

```csharp
using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bản phát qua kênh nội bộ (spec ① §3.3): chỉ cấp quyền ghi log, không cấp quyền mở hub.
    ///
    /// Kiểm thẳng bằng native: `Application.installerName` chỉ là app *thực hiện* cài (App Tester có thể
    /// nhờ trình cài hệ thống), `installMode` báo TestFlight là Store. APK cài tay không tính — file lộ
    /// ra ngoài là ai cũng cài được. Giả được bằng `adb install -i`: làm được vậy là dev.
    internal static class InstallSource
    {
        internal const string APP_TESTER = "dev.firebase.appdistribution";

        private static bool? isInternal;

        internal static bool IsInternal => isInternal ??= Detect();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => isInternal = null;

        internal static bool IsAppTester(string initiating, string installing) =>
            initiating == APP_TESTER || installing == APP_TESTER;

        private static bool Detect()
        {
#if UNITY_EDITOR
            return true;
#elif UNITY_IOS
            // TestFlight, ad-hoc, Xcode: biên lai sandboxReceipt. App Store: receipt.
            return DebugHub_IsSandboxReceipt() != 0;
#elif UNITY_ANDROID
            return DetectAndroid();
#else
            return false;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        // int chứ không bool: bool của C++ 1 byte, marshal mặc định của C# là BOOL 4 byte.
        [DllImport("__Internal")]
        private static extern int DebugHub_IsSandboxReceipt();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// getInstallSourceInfo có từ API 30; máy cũ hơn ném NoSuchMethodError → không phải nội bộ.
        private static bool DetectAndroid()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var packages = activity.Call<AndroidJavaObject>("getPackageManager");
                using var info = packages.Call<AndroidJavaObject>("getInstallSourceInfo", Application.identifier);
                return IsAppTester(info.Call<string>("getInitiatingPackageName"), info.Call<string>("getInstallingPackageName"));
            }
            catch (Exception)
            {
                return false;
            }
        }
#endif
    }
}
```

- [ ] **Step 5: Viết `Plugins/iOS/DebugHubInstallSource.mm`**

```objc
#import <Foundation/Foundation.h>

// InstallSource.cs: TestFlight, ad-hoc và build từ Xcode có biên lai tên sandboxReceipt; bản App Store là receipt.
extern "C" int DebugHub_IsSandboxReceipt()
{
    NSURL *receipt = [[NSBundle mainBundle] appStoreReceiptURL];
    return receipt != nil && [[receipt lastPathComponent] isEqualToString:@"sandboxReceipt"] ? 1 : 0;
}
```

Sau recompile, đặt plugin chỉ cho iOS — `Temp/debughub/ios-plugin.cs`:

```csharp
var path = "Packages/com.hlight.debug-hub/Plugins/iOS/DebugHubInstallSource.mm";
var importer = (UnityEditor.PluginImporter)UnityEditor.AssetImporter.GetAtPath(path);
importer.SetCompatibleWithAnyPlatform(false);
importer.SetCompatibleWithEditor(false);
importer.SetCompatibleWithPlatform(UnityEditor.BuildTarget.iOS, true);
importer.SaveAndReimport();
return importer.GetCompatibleWithPlatform(UnityEditor.BuildTarget.iOS) + " " + importer.GetCompatibleWithPlatform(UnityEditor.BuildTarget.Android);
```

Expected: `True False`.

- [ ] **Step 6: `DebugHub.cs` dùng `HubAccess` và nhớ trạng thái bong bóng**

- Xoá hai hằng `AUTHENTICATION_KEY`, `AUTHENTICATED` (dòng 22–24) và thay bằng:

```csharp
        /// Bong bóng hiện hay ẩn, nhớ qua phiên (spec ① §3.5). Mặc định ẩn: máy reviewer không bao giờ thấy.
        internal const string ENTRY_VISIBLE_KEY = "DebugHub.EntryVisible";
```

- Trong setter `Visible`, sau `instance.entry.Activating = true;` thêm `PlayerPrefs.SetInt(ENTRY_VISIBLE_KEY, 1);`; sau `instance.entry.Activating = false;` thêm `PlayerPrefs.SetInt(ENTRY_VISIBLE_KEY, 0);`.
- Trong `Awake`: `unlocked = PlayerPrefs.GetInt(AUTHENTICATION_KEY) == AUTHENTICATED;` → `unlocked = HubAccess.ReadUnlocked();`.
- Cuối `Awake` (sau khối kiểm mạng hiện có) thêm:

```csharp
            // Mở khoá rồi và lần trước để bong bóng hiện thì hiện lại, khỏi làm cử chỉ mỗi phiên.
            if (unlocked && PlayerPrefs.GetInt(ENTRY_VISIBLE_KEY) == 1) Visible = true;
```

- `Remember()` thành:

```csharp
        private void Remember()
        {
            unlocked = true;
            HubAccess.SaveUnlocked();
        }
```

- [ ] **Step 7: `hub.entry` cũng nhớ trạng thái**

Trong `Scripts/Features/ConsoleController.cs`, setter của `hub.entry`:

```csharp
            DebugHub.AddValue(this, "hub.entry", "Hiện/ẩn nút bấm mở hub.", () => entry.Activating, value =>
            {
                entry.Activating = value;
                PlayerPrefs.SetInt(DebugHub.ENTRY_VISIBLE_KEY, value ? 1 : 0);
                repeat.Refresh();
            });
```

- [ ] **Step 8: Chạy test** — Expected: `FAIL=0`, 4 test `HubAccessTests` pass.

- [ ] **Step 9: Checkpoint** — `git status`, không commit.

---

### Task 4: Dấu hiệu mạng công ty

**Files:**
- Create: `Scripts/Access/CompanyNetwork.cs`, `Scripts/Editor/LocalNetworkUsage.cs`
- Modify: `Scripts/DebugHub.cs` (field, `Awake`, `Update`)
- Delete: `Scripts/DebuggerAuthenticationTriggers/NetworkReachabilityAuthenticationBypass.cs` + `.meta`, `Tests/Editor/NetworkReachabilityAuthenticationBypassTests.cs` + `.meta`
- Test: `Tests/Editor/CompanyNetworkTests.cs`

**Interfaces:**
- Produces: `public class CompanyNetwork` (Serializable): `IEnumerator Check(Action onMatch)`, `internal bool Configured`, `internal const string TRACE_URL`, `internal static bool PageMatches(UnityWebRequest.Result, long, string, string)`, `internal static string IpFromTrace(string)`, `internal static bool IpListed(string, IReadOnlyList<string>)`.

- [ ] **Step 1: Viết test**

`Tests/Editor/CompanyNetworkTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine.Networking;

namespace Hlight.Debug.Hub.Tests
{
    public class CompanyNetworkTests
    {
        private const string TITLE = "<title>Zego Dashboard</title>";

        [Test]
        public void Page_MatchesOnlyOk200_WithTheMarker()
        {
            Assert.IsTrue(CompanyNetwork.PageMatches(UnityWebRequest.Result.Success, 200, "<html>" + TITLE + "</html>", TITLE));
            Assert.IsFalse(CompanyNetwork.PageMatches(UnityWebRequest.Result.Success, 200, "<title>Router</title>", TITLE),
                "máy khác trùng IP nội bộ không được tính");
            Assert.IsFalse(CompanyNetwork.PageMatches(UnityWebRequest.Result.Success, 204, TITLE, TITLE));
            Assert.IsFalse(CompanyNetwork.PageMatches(UnityWebRequest.Result.ProtocolError, 302, TITLE, TITLE),
                "redirect sang trang đăng nhập không được tính");
            Assert.IsFalse(CompanyNetwork.PageMatches(UnityWebRequest.Result.ConnectionError, 0, null, TITLE));
            Assert.IsFalse(CompanyNetwork.PageMatches(UnityWebRequest.Result.Success, 200, TITLE, ""),
                "chưa cấu hình chuỗi thì không bao giờ khớp");
        }

        [Test]
        public void Trace_ReadsTheIpLine()
        {
            Assert.AreEqual("123.24.205.180",
                CompanyNetwork.IpFromTrace("fl=582f21\r\nh=www.cloudflare.com\r\nip=123.24.205.180\r\nts=1\r\n"));
            Assert.IsNull(CompanyNetwork.IpFromTrace("fl=1\nh=x\n"));
            Assert.IsNull(CompanyNetwork.IpFromTrace(null));
        }

        [Test]
        public void Ip_ListedAfterTrim()
        {
            Assert.IsTrue(CompanyNetwork.IpListed("123.24.205.180", new[] { " 123.24.205.180 " }));
            Assert.IsFalse(CompanyNetwork.IpListed("1.2.3.4", new[] { "123.24.205.180" }));
            Assert.IsFalse(CompanyNetwork.IpListed(null, new[] { "123.24.205.180" }));
        }

        [Test]
        public void Unconfigured_SendsNothing_AndNeverMatches()
        {
            var network = new CompanyNetwork();
            var matched = false;
            Assert.IsFalse(network.Configured);
            Assert.IsFalse(network.Check(() => matched = true).MoveNext());
            Assert.IsFalse(matched);
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile.

- [ ] **Step 3: Viết `Scripts/Access/CompanyNetwork.cs`**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlight.Debug.Hub
{
    /// Dấu hiệu "máy đang ở mạng công ty" (spec ① §3.4). Khớp một dấu hiệu = coi như đã gõ đúng password.
    ///
    /// IP nội bộ không phải danh tính (mạng nào cũng tự đặt được 10.x), nên kiểu trang phải trả đúng một
    /// chuỗi biết trước. Chỉ kiểm khi ô password đang mở: người chơi không làm cử chỉ bí mật nên không bao
    /// giờ gửi request, và iOS không hỏi quyền mạng cục bộ họ.
    [Serializable]
    public class CompanyNetwork
    {
        internal const string TRACE_URL = "https://www.cloudflare.com/cdn-cgi/trace";

        [Serializable]
        public struct Page
        {
            [Tooltip("Trang chỉ mở được trong mạng công ty, ví dụ http://10.10.0.204/")]
            public string url;

            [Tooltip("Chuỗi bắt buộc có trong trang, ví dụ <title>Zego Dashboard</title>. Dùng tiêu đề, đừng dùng ETag/hash: hai thứ đó đổi mỗi lần deploy.")]
            public string mustContain;
        }

        [SerializeField] private Page[] pages = Array.Empty<Page>();

        [Tooltip("IP công khai của văn phòng: dòng ip= ở https://www.cloudflare.com/cdn-cgi/trace khi đang ở mạng công ty.")]
        [SerializeField] private string[] publicIps = Array.Empty<string>();

        [Tooltip("Server trong LAN trả lời trong vài chục ms; quá thời gian này coi như không ở công ty.")]
        [SerializeField, Min(1)] private int timeoutSeconds = 2;

        internal bool Configured => (pages != null && pages.Length > 0) || (publicIps != null && publicIps.Length > 0);

        /// Gửi mọi request song song; dấu hiệu đầu tiên khớp thì gọi onMatch đúng một lần rồi dừng.
        public IEnumerator Check(Action onMatch)
        {
            var requests = new List<UnityWebRequest>();
            var markers = new List<string>();   // null = request lấy IP công khai
            foreach (var page in pages ?? Array.Empty<Page>())
            {
                if (string.IsNullOrWhiteSpace(page.url) || string.IsNullOrEmpty(page.mustContain)) continue;
                requests.Add(Send(page.url));
                markers.Add(page.mustContain);
            }
            if (publicIps != null && publicIps.Length > 0)
            {
                requests.Add(Send(TRACE_URL));
                markers.Add(null);
            }

            try
            {
                var pending = new List<int>();
                for (var i = 0; i < requests.Count; i++) pending.Add(i);
                while (pending.Count > 0)
                {
                    for (var k = pending.Count - 1; k >= 0; k--)
                    {
                        var request = requests[pending[k]];
                        if (!request.isDone) continue;
                        var marker = markers[pending[k]];
                        pending.RemoveAt(k);

                        var body = request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null;
                        var matched = marker != null
                            ? PageMatches(request.result, request.responseCode, body, marker)
                            : IpListed(IpFromTrace(body), publicIps);
                        if (!matched) continue;
                        onMatch?.Invoke();
                        yield break;
                    }
                    yield return null;
                }
            }
            finally
            {
                foreach (var request in requests) request.Dispose();
            }
        }

        private UnityWebRequest Send(string url)
        {
            var request = UnityWebRequest.Get(url);
            // Redirect sang trang đăng nhập Wi-Fi không được tính là "tới được".
            request.redirectLimit = 0;
            request.timeout = Mathf.Max(1, timeoutSeconds);
            request.SendWebRequest();
            return request;
        }

        internal static bool PageMatches(UnityWebRequest.Result result, long code, string body, string mustContain) =>
            result == UnityWebRequest.Result.Success && code == 200 && !string.IsNullOrEmpty(mustContain) &&
            body != null && body.Contains(mustContain);

        internal static string IpFromTrace(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            foreach (var raw in body.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("ip=", StringComparison.Ordinal)) return line.Substring(3);
            }
            return null;
        }

        internal static bool IpListed(string ip, IReadOnlyList<string> ips)
        {
            if (string.IsNullOrEmpty(ip) || ips == null) return false;
            foreach (var candidate in ips)
            {
                if (candidate != null && candidate.Trim() == ip) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: Nối vào `DebugHub.cs`**

- Thay field `networkReachabilityAuthenticationBypass` bằng:

```csharp
        [Tooltip("Dấu hiệu máy đang ở mạng công ty: khớp một cái = coi như đã gõ đúng password. Chỉ kiểm khi ô password đang mở.")]
        [SerializeField] private CompanyNetwork companyNetwork = new();
```

- Xoá nguyên khối kiểm mạng cuối `Awake` (comment "EditMode test không tick player loop…" tới hết `if (Application.isPlaying && UnityEngine.Debug.isDebugBuild && !Unlocked) { … }`).
- Trong `Update`, case `AskPassword`, sau `StartCoroutine(FocusNextFrame(authenticationInputField));` thêm:

```csharp
                    // Kiểm song song với ô password: ở công ty ô tự đóng, ngoài công ty ô vẫn đó để gõ — UI không
                    // bao giờ chờ mạng. Chỉ Play thật: EditMode test không tick coroutine, không bắn request thật.
                    if (Application.isPlaying && companyNetwork.Configured)
                        StartCoroutine(companyNetwork.Check(() =>
                        {
                            if (askingPassword) AcceptAuthentication();
                        }));
```

- [ ] **Step 5: Xoá bản cũ**

```bash
git -C Packages/com.hlight.debug-hub rm -q Scripts/DebuggerAuthenticationTriggers/NetworkReachabilityAuthenticationBypass.cs Scripts/DebuggerAuthenticationTriggers/NetworkReachabilityAuthenticationBypass.cs.meta Tests/Editor/NetworkReachabilityAuthenticationBypassTests.cs Tests/Editor/NetworkReachabilityAuthenticationBypassTests.cs.meta
```

- [ ] **Step 6: Viết `Scripts/Editor/LocalNetworkUsage.cs`**

```csharp
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace Hlight.Debug.Hub.Editor
{
    /// Dấu hiệu kiểu trang gọi một IP trong LAN → iOS hỏi quyền mạng cục bộ (chỉ tester: request chỉ gửi
    /// sau cử chỉ bí mật). Thiếu khoá này thì hộp thoại không có lời giải thích.
    public class LocalNetworkUsage : IPostprocessBuildWithReport
    {
        private const string KEY = "NSLocalNetworkUsageDescription";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var path = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            // Game đã tự khai thì giữ lời của game.
            if (plist.root.values.ContainsKey(KEY)) return;
            plist.root.SetString(KEY, "Dùng để nhận diện mạng nội bộ khi mở công cụ kiểm thử.");
            plist.WriteToFile(path);
        }
    }
}
#endif
```

- [ ] **Step 7: Chạy test** — Expected: `FAIL=0`, 4 test `CompanyNetworkTests` pass.

- [ ] **Step 8: Báo user cấu hình** (không tự sửa `Root.unity`): trên instance `DebugHub` trong `Assets/0_DevRoot/Scene/Root.unity`, mục **Company Network**: `Pages` = 1 dòng (`url` = `http://10.10.0.204/`, `mustContain` = `<title>Zego Dashboard</title>`), `Public Ips` = `123.24.205.180`. Override cũ `networkReachabilityAuthenticationBypass.checkUrls` trong scene là rác, Unity bỏ khi lưu scene.

- [ ] **Step 9: Checkpoint** — `git status`, không commit.

---

### Task 5: Bộ ghi `LogRecorder`

**Files:**
- Create: `Scripts/Logs/LogEntry.cs`, `Scripts/Logs/LogRecorder.cs`
- Modify: `Scripts/DebugHub.cs` (`Remember`), `Scripts/Model/DebugRegistry.cs` (`RunEntry`)
- Test: `Tests/Editor/LogRecorderTests.cs`

**Interfaces:**
- Consumes: `HubAccess.MayRecordNow()`.
- Produces:
  - `enum LogKind { Log, Command }`, `enum LogSource { Unity, Native }`.
  - `readonly struct LogEntry` — `long Seq`, `DateTime Time`, `LogType Type`, `string Message`, `string Stack`, `LogKind Kind`, `LogSource Source`, `bool IsError`, `int Cost`.
  - `static class LogRecorder` — `DEFAULT_BUDGET`, `int Budget {get;set;}`, `bool Recording`, `DateTime StartedAt`, `long OldestSeq`, `long LastSeq`, `long Dropped`, `long ErrorCount`, `long LastMarkSeq`, `void Start()`, `void Reset()`, `void Receive(string message, string stack, LogType type)`, `long Mark(string line)`, `int CopySince(long seq, List<LogEntry> into)`.

- [ ] **Step 1: Viết test**

`Tests/Editor/LogRecorderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogRecorderTests
    {
        private readonly List<LogEntry> got = new();

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            got.Clear();
        }

        [TearDown]
        public void TearDown() => LogRecorder.Reset();

        [Test]
        public void Receive_NumbersEntriesInOrder()
        {
            LogRecorder.Receive("a", "", LogType.Log);
            LogRecorder.Receive("b", "", LogType.Warning);
            Assert.AreEqual(2, LogRecorder.CopySince(0, got));
            Assert.AreEqual(1, got[0].Seq);
            Assert.AreEqual(2, got[1].Seq);
            Assert.AreEqual("b", got[1].Message);
            Assert.AreEqual(LogType.Warning, got[1].Type);
        }

        [Test]
        public void CopySince_ReturnsOnlyNewer_AndNothingWhenUpToDate()
        {
            for (var i = 0; i < 5; i++) LogRecorder.Receive("x" + i, null, LogType.Log);
            Assert.AreEqual(2, LogRecorder.CopySince(3, got));
            Assert.AreEqual(4, got[0].Seq);
            got.Clear();
            Assert.AreEqual(0, LogRecorder.CopySince(LogRecorder.LastSeq, got));
            Assert.AreEqual(0, got.Count);
        }

        [Test]
        public void OverBudget_DropsOldestFirst()
        {
            var one = new LogEntry(0, default, LogType.Log, new string('x', 10), null, LogKind.Log, LogSource.Unity).Cost;
            LogRecorder.Budget = one * 3;
            for (var i = 0; i < 5; i++) LogRecorder.Receive(new string('x', 10), null, LogType.Log);
            Assert.AreEqual(2, LogRecorder.Dropped);
            Assert.AreEqual(3, LogRecorder.OldestSeq);
            Assert.AreEqual(3, LogRecorder.CopySince(0, got));
            Assert.AreEqual(3, got[0].Seq);
        }

        [Test]
        public void KeepsTheNewest_EvenIfItAloneIsOverBudget()
        {
            LogRecorder.Budget = 1;
            LogRecorder.Receive(new string('x', 1000), null, LogType.Log);
            Assert.AreEqual(0, LogRecorder.Dropped);
            LogRecorder.Receive("y", null, LogType.Log);
            Assert.AreEqual(1, LogRecorder.Dropped);
            Assert.AreEqual(1, LogRecorder.CopySince(0, got));
            Assert.AreEqual("y", got[0].Message);
        }

        [Test]
        public void ErrorCount_CountsErrorExceptionAssert()
        {
            LogRecorder.Receive("e", null, LogType.Error);
            LogRecorder.Receive("x", null, LogType.Exception);
            LogRecorder.Receive("a", null, LogType.Assert);
            LogRecorder.Receive("w", null, LogType.Warning);
            Assert.AreEqual(3, LogRecorder.ErrorCount);
        }

        [Test]
        public void Mark_OnlyWhileRecording()
        {
            Assert.AreEqual(0, LogRecorder.Mark("x"));
            LogRecorder.Start();
            var seq = LogRecorder.Mark("level.goto 5");
            Assert.Greater(seq, 0);
            Assert.AreEqual(seq, LogRecorder.LastMarkSeq);
            LogRecorder.CopySince(0, got);
            Assert.IsTrue(got.Exists(e => e.Seq == seq && e.Kind == LogKind.Command && e.Message == "level.goto 5"));
        }

        [Test]
        public void Receive_FromManyThreads_KeepsEveryEntry()
        {
            var threads = new Thread[4];
            for (var t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    for (var i = 0; i < 500; i++) LogRecorder.Receive("t", null, LogType.Log);
                });
                threads[t].Start();
            }
            foreach (var thread in threads) thread.Join();

            LogRecorder.CopySince(0, got);
            Assert.AreEqual(2000, got.Count + LogRecorder.Dropped);
            for (var i = 1; i < got.Count; i++) Assert.AreEqual(got[i - 1].Seq + 1, got[i].Seq);
        }

        [Test]
        public void RunEntry_MarksTheCommandLine()
        {
            LogRecorder.Start();
            var node = DebugHub.Add(null, "recordertest.ping", "d", () => { });
            try
            {
                Assert.IsTrue(DebugHub.Execute("recordertest.ping", out _));
                LogRecorder.CopySince(0, got);
                Assert.IsTrue(got.Exists(e => e.Kind == LogKind.Command && e.Message == "recordertest.ping"));
            }
            finally { DebugHub.Remove(node); }
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile.

- [ ] **Step 3: Viết `Scripts/Logs/LogEntry.cs`**

```csharp
using System;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    internal enum LogKind { Log, Command }

    /// Native cho spec ② (logcat) / ③ (OSLogStore): cùng ring, ghép theo Time.
    internal enum LogSource { Unity, Native }

    internal readonly struct LogEntry
    {
        public readonly long Seq;
        public readonly DateTime Time;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string Stack;
        public readonly LogKind Kind;
        public readonly LogSource Source;

        public LogEntry(long seq, DateTime time, LogType type, string message, string stack, LogKind kind, LogSource source)
        {
            Seq = seq;
            Time = time;
            Type = type;
            Message = message;
            Stack = stack;
            Kind = kind;
            Source = source;
        }

        public bool IsError => Type == LogType.Error || Type == LogType.Exception || Type == LogType.Assert;

        /// Byte giữ trong RAM: chuỗi .NET là UTF-16, cộng 64 cho chính entry — log rỗng cũng phải tốn chỗ,
        /// không thì ring nở vô hạn.
        public int Cost => 64 + ((Message?.Length ?? 0) + (Stack?.Length ?? 0)) * 2;
    }
}
```

- [ ] **Step 4: Viết `Scripts/Logs/LogRecorder.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Bộ ghi log (spec ① §4): chạy ngầm từ lúc khởi động, không UI. Ring nằm ở đây chứ không ở UI, nên
    /// trang log mở muộn vẫn đọc được cả phiên — đúng chỗ IngameDebugConsole hỏng.
    ///
    /// Máy không được ghi thì không đăng ký callback: Unity không marshal chuỗi log sang C#, chi phí 0.
    internal static class LogRecorder
    {
        /// ponytail: 4 MB chuỗi, chỉnh khi đo trên máy thật. Thiếu chỗ thì dùng chung instance cho stack giống hệt.
        internal const int DEFAULT_BUDGET = 4 * 1024 * 1024;

        private static readonly object Gate = new();
        private static LogEntry[] ring = new LogEntry[256];
        private static int head;
        private static int count;
        private static long bytes;
        private static long nextSeq = 1;
        private static long dropped;
        private static long errorCount;
        private static long lastMarkSeq;

        internal static int Budget { get; set; } = DEFAULT_BUDGET;
        internal static bool Recording { get; private set; }
        internal static DateTime StartedAt { get; private set; }

        internal static long Dropped { get { lock (Gate) return dropped; } }
        internal static long ErrorCount { get { lock (Gate) return errorCount; } }
        internal static long LastMarkSeq { get { lock (Gate) return lastMarkSeq; } }
        internal static long LastSeq { get { lock (Gate) return nextSeq - 1; } }

        /// Seq của entry cũ nhất còn giữ; ring rỗng thì là seq kế tiếp.
        internal static long OldestSeq { get { lock (Gate) return count > 0 ? ring[head].Seq : nextSeq; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Boot()
        {
            Reset();
            // ponytail: PlayerPrefs ở SubsystemRegistration chưa kiểm trên máy thật (spec §11.1);
            // hỏng thì lùi sang AfterAssembliesLoaded.
            if (HubAccess.MayRecordNow()) Start();
        }

        /// Gọi lại được: mở khoá giữa phiên (DebugHub.Remember) cũng đi qua đây.
        internal static void Start()
        {
            lock (Gate)
            {
                if (Recording) return;
                Recording = true;
                StartedAt = DateTime.Now;
            }
            Application.logMessageReceivedThreaded += Receive;
        }

        /// Test và "Enter Play Mode without domain reload": static sống qua các lần Play.
        internal static void Reset()
        {
            Application.logMessageReceivedThreaded -= Receive;
            lock (Gate)
            {
                Recording = false;
                StartedAt = default;
                ring = new LogEntry[256];
                head = count = 0;
                bytes = 0;
                nextSeq = 1;
                dropped = errorCount = lastMarkSeq = 0;
                Budget = DEFAULT_BUDGET;
            }
        }

        /// Callback của Unity, chạy trên thread đã log. Không gọi Unity API ở đây.
        internal static void Receive(string message, string stack, LogType type)
        {
            lock (Gate)
            {
                var entry = new LogEntry(nextSeq++, DateTime.Now, type, message, stack, LogKind.Log, LogSource.Unity);
                if (entry.IsError) errorCount++;
                Append(entry);
            }
        }

        /// Vạch command trong dòng thời gian. Không ghi thì không đánh dấu: không ai đọc.
        internal static long Mark(string line)
        {
            lock (Gate)
            {
                if (!Recording) return 0;
                var entry = new LogEntry(nextSeq++, DateTime.Now, LogType.Log, line, null, LogKind.Command, LogSource.Unity);
                Append(entry);
                return lastMarkSeq = entry.Seq;
            }
        }

        /// Thêm vào into mọi entry có Seq > seq, theo thứ tự. Không có gì mới thì không cấp phát.
        internal static int CopySince(long seq, List<LogEntry> into)
        {
            lock (Gate)
            {
                if (count == 0 || seq >= nextSeq - 1) return 0;
                // Seq liên tục trong ring nên vị trí của seq + 1 tính thẳng, không quét.
                var skip = (int)Math.Max(0, seq + 1 - ring[head].Seq);
                for (var i = skip; i < count; i++) into.Add(ring[(head + i) % ring.Length]);
                return count - skip;
            }
        }

        private static void Append(LogEntry entry)
        {
            if (count == ring.Length) Grow();
            ring[(head + count) % ring.Length] = entry;
            count++;
            bytes += entry.Cost;
            // Luôn giữ entry mới nhất, kể cả khi một mình nó vượt ngân sách.
            while (bytes > Budget && count > 1)
            {
                bytes -= ring[head].Cost;
                ring[head] = default;
                head = (head + 1) % ring.Length;
                count--;
                dropped++;
            }
        }

        private static void Grow()
        {
            var bigger = new LogEntry[ring.Length * 2];
            for (var i = 0; i < count; i++) bigger[i] = ring[(head + i) % ring.Length];
            ring = bigger;
            head = 0;
        }
    }
}
```

- [ ] **Step 5: Mở khoá giữa phiên bắt đầu ghi** — `DebugHub.Remember()`:

```csharp
        private void Remember()
        {
            unlocked = true;
            HubAccess.SaveUnlocked();
            LogRecorder.Start();
        }
```

- [ ] **Step 6: Vạch command** — trong `DebugRegistry.RunEntry`, ngay sau khối `if (!entry.Alive) { … }`:

```csharp
            // Vạch trong trang log: log nào in ra sau thao tác nào. Cùng luật với Record bên dưới — đọc một
            // ValueNode không phải thao tác.
            if (entry.Node is ActionNode || values.Length > 0)
                LogRecorder.Mark(values.Length == 0 ? entry.Path : $"{entry.Path} {string.Join(" ", values)}");
```

- [ ] **Step 7: Chạy test** — Expected: `FAIL=0`, 8 test `LogRecorderTests` pass.

- [ ] **Step 8: Checkpoint** — `git status`, không commit.

---

### Task 6: View model trang log

**Files:**
- Create: `Scripts/Logs/StackFrames.cs`, `Scripts/Logs/LogText.cs`, `Scripts/Logs/LogModel.cs`
- Test: `Tests/Editor/StackFramesTests.cs`, `Tests/Editor/LogTextTests.cs`, `Tests/Editor/LogModelTests.cs`

**Interfaces:**
- Consumes: `LogRecorder.*`, `LogEntry`.
- Produces:
  - `StackFrames.Frame` (`Qualified`, `Method`, `Location`, `Game`, `Caller`), `List<Frame> StackFrames.Split(string)`, `string StackFrames.Caller(string)` (null nếu không có stack).
  - `LogText.Escape(string)`, `Highlight(string text, string query)`, `RowPreview(string)`, `DetailPreview(string)`, `Time(DateTime)`, `TypeLabel(LogType)`, `ColorOf(LogType)` (hex của `Palette`), `Count(long)`, `Badge(long)`.
  - `enum LogGroup { Log, Warning, Error }`.
  - `LogItem` — `Entry`, `Repeat`, `Last`, `IsMarker`, `Group`, `Time`, `Caller`, `Preview`.
  - `LogModel` — `static Shared`, `static ResetShared()`, `Follow`, `SelectedSeq`, `SeenErrors`, `Version`, `RowCount`, `RowAt(int)` (0 = dòng ghi chú → null), `Count(LogGroup)`, `Total`, `IsShown(LogGroup)`, `Toggle(LogGroup)`, `Collapse`, `Query`, `ResetFilters()`, `Clear()`, `Unclear()`, `Cleared`, `Pull()`, `NoteText()`, `IndexOfSeq(long)`, `Neighbour(LogItem, int)`, `CopyAll()`, `static Describe(LogItem)`, `EntryRowCount`.

- [ ] **Step 1: Viết test**

`Tests/Editor/StackFramesTests.cs`:

```csharp
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class StackFramesTests
    {
        private const string LOG_STACK =
            "UnityEngine.Debug:Log (object)\n" +
            "Log:Info (object[]) (at ./Packages/com.hlight.logging/Runtime/Log.cs:34)\n" +
            "Harvest.Gameplay.ItemPiece:OnPointerUp (UnityEngine.EventSystems.PointerEventData) (at Assets/0_DevRoot/Scripts/ItemPiece.cs:212)\n" +
            "UnityEngine.EventSystems.ExecuteEvents:Execute (UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.BaseEventData) (at ./Library/PackageCache/com.unity.ugui/Runtime/EventSystem/ExecuteEvents.cs:58)\n";

        [Test]
        public void Caller_SkipsDebugAndTheLoggingWrapper()
        {
            Assert.AreEqual("ItemPiece.OnPointerUp:212", StackFrames.Caller(LOG_STACK));
        }

        [Test]
        public void Caller_ReadsExceptionFormat()
        {
            Assert.AreEqual("TrayController.TryPush:61",
                StackFrames.Caller("Harvest.Gameplay.TrayController.TryPush (Harvest.ItemPiece p) (at Assets/TrayController.cs:61)"));
        }

        /// IL2CPP release mặc định chỉ có tên method, không có `(at …)`.
        [Test]
        public void Caller_Il2CppWithoutLine()
        {
            Assert.AreEqual("ItemPiece.OnPointerUp", StackFrames.Caller("Harvest.ItemPiece:OnPointerUp(PointerEventData)"));
        }

        [Test]
        public void Caller_NoStack_IsNull()
        {
            Assert.IsNull(StackFrames.Caller(""));
            Assert.IsNull(StackFrames.Caller(null));
        }

        [Test]
        public void Split_MarksEngineAndLoggingFramesAsNotGame()
        {
            var frames = StackFrames.Split(LOG_STACK);
            Assert.AreEqual(4, frames.Count);
            Assert.IsFalse(frames[0].Game);
            Assert.IsFalse(frames[1].Game);
            Assert.IsTrue(frames[2].Game);
            Assert.AreEqual("ItemPiece.cs:212", frames[2].Location);
            StringAssert.StartsWith("ItemPiece.OnPointerUp", frames[2].Method);
            Assert.IsFalse(frames[3].Game);
        }
    }
}
```

`Tests/Editor/LogTextTests.cs`:

```csharp
using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class LogTextTests
    {
        private static int Occurrences(string text, string part)
        {
            var n = 0;
            for (var i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + 1, System.StringComparison.Ordinal)) n++;
            return n;
        }

        /// Log chứa đúng chuỗi đóng noparse không được thoát ra để TMP đọc phần sau thành tag.
        [Test]
        public void Escape_CannotBeClosedFromInside()
        {
            var escaped = LogText.Escape("a</noparse><b>x");
            Assert.AreEqual(1, Occurrences(escaped, "</noparse>"));
            StringAssert.EndsWith("</noparse>", escaped);
        }

        [Test]
        public void Highlight_WrapsEveryMatch_IgnoringCase()
        {
            Assert.AreEqual(2, Occurrences(LogText.Highlight("Load level LEVEL", "level"), "<mark="));
            Assert.AreEqual(LogText.Escape("abc"), LogText.Highlight("abc", ""));
        }

        [Test]
        public void RowPreview_CutsLongMessages()
        {
            Assert.AreEqual(LogText.ROW_CHARS, LogText.RowPreview(new string('x', 1000)).Length);
            Assert.AreEqual("short", LogText.RowPreview("short"));
        }

        [Test]
        public void Count_GroupsThousandsWithDots()
        {
            Assert.AreEqual("999", LogText.Count(999));
            Assert.AreEqual("1.204", LogText.Count(1204));
            Assert.AreEqual("1.234.567", LogText.Count(1234567));
        }

        [Test]
        public void Badge_CapsAt99()
        {
            Assert.AreEqual("3", LogText.Badge(3));
            Assert.AreEqual("99+", LogText.Badge(120));
        }
    }
}
```

`Tests/Editor/LogModelTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogModelTests
    {
        private LogModel model;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            model = new LogModel();
        }

        [TearDown]
        public void TearDown() => LogRecorder.Reset();

        private static void Log(string message, LogType type = LogType.Log, string stack = null) =>
            LogRecorder.Receive(message, stack, type);

        [Test]
        public void Pull_TurnsEntriesIntoRows_AfterTheNote()
        {
            Log("a"); Log("b"); Log("c");
            Assert.AreEqual(3, model.Pull());
            Assert.AreEqual(4, model.RowCount);
            Assert.IsNull(model.RowAt(0));
            Assert.AreEqual("a", model.RowAt(1).Entry.Message);
            Assert.AreEqual(0, model.Pull(), "kéo lần hai không có gì mới");
        }

        [Test]
        public void Counts_StayTotal_WhenFilteringAndSearching()
        {
            Log("l1"); Log("l2"); Log("w", LogType.Warning); Log("e", LogType.Error); Log("x", LogType.Exception);
            model.Pull();
            Assert.AreEqual(2, model.Count(LogGroup.Log));
            Assert.AreEqual(1, model.Count(LogGroup.Warning));
            Assert.AreEqual(2, model.Count(LogGroup.Error));

            model.Toggle(LogGroup.Error);
            Assert.IsFalse(model.IsShown(LogGroup.Error));
            Assert.AreEqual(1 + 3, model.RowCount);
            Assert.AreEqual(2, model.Count(LogGroup.Error));

            model.Query = "w";
            Assert.AreEqual(1 + 1, model.RowCount);
            Assert.AreEqual(5, model.Total);
        }

        [Test]
        public void Search_MatchesMessageOrCaller_IgnoringCase()
        {
            Log("tap", LogType.Log, "Harvest.ItemPiece:OnPointerUp () (at Assets/ItemPiece.cs:212)");
            Log("other");
            model.Pull();
            model.Query = "itempiece";
            Assert.AreEqual(2, model.RowCount);
            Assert.AreEqual("tap", model.RowAt(1).Entry.Message);
            model.Query = "zzz";
            Assert.AreEqual(1, model.RowCount);
        }

        [Test]
        public void Collapse_KeepsTheFirstPosition_AndCountsRepeats()
        {
            Log("a"); Log("b"); Log("a"); Log("a");
            model.Pull();
            model.Collapse = true;
            Assert.AreEqual(3, model.RowCount);
            Assert.AreEqual("a", model.RowAt(1).Entry.Message);
            Assert.AreEqual(3, model.RowAt(1).Repeat);

            var version = model.Version;
            Log("a");
            Assert.AreEqual(0, model.Pull(), "log trùng không thêm hàng");
            Assert.AreEqual(4, model.RowAt(1).Repeat);
            Assert.AreNotEqual(version, model.Version, "hàng đã có đổi nội dung → view phải dựng lại");
        }

        [Test]
        public void Eviction_DropsRowsTheRecorderNoLongerHas()
        {
            LogRecorder.Budget = new LogEntry(0, default, LogType.Log, "x0", null, LogKind.Log, LogSource.Unity).Cost * 3;
            for (var i = 0; i < 6; i++) Log("x" + i);
            model.Pull();
            Assert.AreEqual(1 + 3, model.RowCount);
            Assert.AreEqual(LogRecorder.OldestSeq, model.RowAt(1).Entry.Seq);
        }

        [Test]
        public void Clear_HidesEverythingSoFar_UnclearBringsItBack()
        {
            Log("a"); Log("b"); Log("c");
            model.Pull();
            model.Clear();
            Assert.AreEqual(1, model.RowCount);
            Assert.IsTrue(model.Cleared);
            StringAssert.Contains("đã ẩn 3", model.NoteText());
            Assert.AreEqual(0, model.Total);

            Log("d");
            model.Pull();
            Assert.AreEqual(2, model.RowCount);

            model.Unclear();
            Assert.AreEqual(5, model.RowCount);
        }

        [Test]
        public void Markers_IgnoreTypeFilters_AndAreFoundBySeq()
        {
            LogRecorder.Start();
            Log("before");
            var seq = LogRecorder.Mark("level.goto 5");
            Log("after");
            model.Pull();
            model.Toggle(LogGroup.Log);
            var index = model.IndexOfSeq(seq);
            Assert.Greater(index, 0);
            Assert.IsTrue(model.RowAt(index).IsMarker);
            Assert.AreEqual("level.goto 5", model.RowAt(index).Entry.Message);
        }

        [Test]
        public void Neighbour_SkipsMarkers()
        {
            LogRecorder.Start();
            Log("first");
            LogRecorder.Mark("cmd");
            Log("second");
            model.Pull();
            LogItem first = null;
            for (var i = 1; i < model.RowCount; i++)
            {
                if (model.RowAt(i).Entry.Message == "first") first = model.RowAt(i);
            }
            Assert.IsNotNull(first);
            Assert.AreEqual("second", model.Neighbour(first, 1).Entry.Message);
            Assert.IsNull(model.Neighbour(first, -1));
        }

        [Test]
        public void Describe_HasTimeTypeMessageAndStack()
        {
            Log("boom", LogType.Error, "A:B () (at A.cs:1)\n");
            model.Pull();
            var text = LogModel.Describe(model.RowAt(1));
            StringAssert.Contains("[Lỗi]", text);
            StringAssert.Contains("boom", text);
            StringAssert.Contains("A.cs:1", text);
        }

        [Test]
        public void FollowsByDefault()
        {
            Assert.IsTrue(model.Follow);
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile.

- [ ] **Step 3: Viết `Scripts/Logs/StackFrames.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Đọc stack trace của Unity. Hai dạng:
    /// log `Ns.Type:Method (Args) (at Assets/X.cs:12)`, exception `Ns.Type.Method (Args) (at Assets/X.cs:12)`.
    /// IL2CPP release (mặc định) chỉ có tên method, không có `(at …)`.
    internal static class StackFrames
    {
        internal readonly struct Frame
        {
            public readonly string Qualified;   // "Harvest.Gameplay.ItemPiece:OnPointerUp"
            public readonly string Method;      // "ItemPiece.OnPointerUp (PointerEventData)"
            public readonly string Location;    // "ItemPiece.cs:212" hoặc ""
            public readonly bool Game;          // false = engine/thư viện/lớp log → tô mờ
            public readonly string Caller;      // "ItemPiece.OnPointerUp:212"

            public Frame(string qualified, string method, string location, bool game, string caller)
            {
                Qualified = qualified;
                Method = method;
                Location = location;
                Game = game;
                Caller = caller;
            }
        }

        /// Frame của chính lớp log (Debug.Log, lớp bọc com.hlight.logging): không phải "nơi gọi". Log, LogTag,
        /// LogAlways nằm ở global namespace.
        private static readonly string[] LoggingPrefixes =
        {
            "UnityEngine.Debug", "UnityEngine.Logger", "Hlight.Logging.",
            "Log:", "Log.", "LogTag:", "LogTag.", "LogAlways:", "LogAlways.",
        };

        private static readonly string[] EnginePrefixes = { "UnityEngine.", "UnityEditor.", "System.", "Unity.", "Mono.", "TMPro." };

        internal static List<Frame> Split(string stack)
        {
            var frames = new List<Frame>();
            if (string.IsNullOrEmpty(stack)) return frames;
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0) frames.Add(Parse(line));
            }
            return frames;
        }

        /// Frame đầu tiên không thuộc lớp log. null = không có stack.
        internal static string Caller(string stack)
        {
            foreach (var frame in Split(stack))
            {
                if (!StartsWithAny(frame.Qualified, LoggingPrefixes)) return frame.Caller;
            }
            return null;
        }

        private static Frame Parse(string line)
        {
            var head = line;
            var location = string.Empty;
            var at = line.LastIndexOf("(at ", StringComparison.Ordinal);
            if (at >= 0 && line.EndsWith(")", StringComparison.Ordinal))
            {
                var path = line.Substring(at + 4, line.Length - at - 5);
                var slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
                location = slash >= 0 ? path.Substring(slash + 1) : path;
                head = line.Substring(0, at).TrimEnd();
            }

            var paren = head.IndexOf('(');
            var qualified = (paren >= 0 ? head.Substring(0, paren) : head).Trim();
            var arguments = paren >= 0 ? head.Substring(paren).Trim() : string.Empty;
            var name = ShortName(qualified);

            var colon = location.LastIndexOf(':');
            var caller = colon >= 0 ? $"{name}:{location.Substring(colon + 1)}" : name;
            var game = !StartsWithAny(qualified, LoggingPrefixes) && !StartsWithAny(qualified, EnginePrefixes);
            return new Frame(qualified, $"{name} {arguments}".TrimEnd(), location, game, caller);
        }

        /// "Harvest.Gameplay.ItemPiece:OnPointerUp" / "Harvest.Gameplay.ItemPiece.OnPointerUp" → "ItemPiece.OnPointerUp".
        private static string ShortName(string qualified)
        {
            var split = qualified.LastIndexOf(':');
            if (split < 0) split = qualified.LastIndexOf('.');
            if (split < 0) return qualified;
            var type = qualified.Substring(0, split);
            var method = qualified.Substring(split + 1);
            var dot = type.LastIndexOf('.');
            return (dot >= 0 ? type.Substring(dot + 1) : type) + "." + method;
        }

        private static bool StartsWithAny(string text, string[] prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (text.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: Viết `Scripts/Logs/LogText.cs`**

```csharp
using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Chữ của trang log. Chỉ dùng ký tự ngoài chữ cái mà font game chắc có (`› ‹ … – — ×`).
    internal static class LogText
    {
        internal const int ROW_CHARS = 300;
        internal const int DETAIL_CHARS = 4000;
        private const string MARK = "<mark=#E8B04B66>";

        /// Dữ liệu game qua TMP: `<noparse>`, và chuỗi `</noparse>` trong chính dữ liệu bị chèn zero-width
        /// space để không thoát ra được.
        internal static string Escape(string text) =>
            "<noparse>" + (text ?? string.Empty).Replace("</noparse>", "</no\u200Bparse>") + "</noparse>";

        /// Escape + tô các đoạn khớp (không phân biệt hoa thường). Tag mark nằm ngoài noparse nên vẫn có tác dụng.
        internal static string Highlight(string text, string query)
        {
            text ??= string.Empty;
            if (string.IsNullOrEmpty(query)) return Escape(text);

            var builder = new StringBuilder(text.Length + 64);
            var start = 0;
            while (true)
            {
                var hit = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                if (hit < 0) break;
                if (hit > start) builder.Append(Escape(text.Substring(start, hit - start)));
                builder.Append(MARK).Append(Escape(text.Substring(hit, query.Length))).Append("</mark>");
                start = hit + query.Length;
            }
            if (start < text.Length) builder.Append(Escape(text.Substring(start)));
            return builder.ToString();
        }

        /// Hàng log chỉ hiện 2 dòng: đưa cả chuỗi 50 KB vào TMP là dựng mesh cho 50 KB rồi mới cắt.
        internal static string RowPreview(string message) =>
            message == null ? string.Empty : message.Length <= ROW_CHARS ? message : message.Substring(0, ROW_CHARS);

        internal static string DetailPreview(string message) =>
            message == null ? string.Empty
            : message.Length <= DETAIL_CHARS ? message
            : message.Substring(0, DETAIL_CHARS) + "… (Copy để lấy đủ)";

        internal static string Time(DateTime time) => time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

        internal static string TypeLabel(LogType type) => type switch
        {
            LogType.Log => "Log",
            LogType.Warning => "Cảnh báo",
            _ => "Lỗi",
        };

        internal static string ColorOf(LogType type) => type switch
        {
            LogType.Log => Palette.DIM,
            LogType.Warning => Palette.WARN,
            _ => Palette.BAD,
        };

        /// 1204 → "1.204". Tự định dạng, không dựa vào dữ liệu culture vi-VN (IL2CPP có thể không mang theo).
        internal static string Count(long n)
        {
            var digits = n.ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= 3) return digits;
            var builder = new StringBuilder(digits.Length + digits.Length / 3);
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) builder.Append('.');
                builder.Append(digits[i]);
            }
            return builder.ToString();
        }

        /// Chấm đỏ: chỗ nhỏ, quá 99 thì "99+".
        internal static string Badge(long n) => n > 99 ? "99+" : n.ToString(CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 5: Viết `Scripts/Logs/LogModel.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    internal enum LogGroup { Log, Warning, Error }

    /// Một entry đã kéo về trang log, kèm những gì tính một lần rồi giữ: cuộn qua lại không cấp phát lại.
    internal sealed class LogItem
    {
        public readonly LogEntry Entry;
        public int Repeat = 1;
        public DateTime Last;

        private string time;
        private string caller;
        private string preview;

        public LogItem(LogEntry entry)
        {
            Entry = entry;
            Last = entry.Time;
        }

        public bool IsMarker => Entry.Kind == LogKind.Command;

        public LogGroup Group => Entry.Type == LogType.Log ? LogGroup.Log
            : Entry.Type == LogType.Warning ? LogGroup.Warning
            : LogGroup.Error;

        public string Time => time ??= LogText.Time(Entry.Time);
        public string Caller => caller ??= StackFrames.Caller(Entry.Stack) ?? string.Empty;
        public string Preview => preview ??= LogText.RowPreview(Entry.Message);
    }

    /// Trạng thái trang log, tách khỏi UI để test được (spec ① §5.2–5.3). Hàng 0 luôn là dòng ghi chú.
    internal sealed class LogModel
    {
        internal const int COPY_LIMIT = 500_000;

        /// Sống suốt phiên: lọc / gộp / xoá giữ nguyên qua các lần mở trang.
        internal static LogModel Shared { get; private set; } = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetShared() => Shared = new LogModel();

        private readonly List<LogItem> items = new();
        private readonly List<LogItem> rows = new();
        private readonly Dictionary<(LogType, string, string), LogItem> firsts = new();
        private readonly List<LogEntry> incoming = new();
        private readonly bool[] shown = { true, true, true };
        private readonly int[] counts = new int[3];
        private bool collapse;
        private string query = string.Empty;
        private long lastSeq;
        private long clearedAt;

        /// Đang bám đáy. View đặt khi người dùng cuộn; giữ ở model để lùi từ trang chi tiết về vẫn đúng.
        public bool Follow = true;

        /// Hàng có viền mint: log vừa xem chi tiết, hoặc vạch command vừa nhảy tới.
        public long SelectedSeq;

        /// Chấm đỏ = LogRecorder.ErrorCount - SeenErrors. View đặt mỗi frame khi trang log đang mở.
        public long SeenErrors;

        /// Tăng khi nội dung một hàng đã có đổi (lọc lại, gộp thêm, bỏ log cũ): view gán lại hàng đang hiện.
        public int Version { get; private set; }

        public int RowCount => rows.Count + 1;
        public LogItem RowAt(int index) => index <= 0 ? null : rows[index - 1];
        public int Count(LogGroup group) => counts[(int)group];
        public int Total => counts[0] + counts[1] + counts[2];
        public bool IsShown(LogGroup group) => shown[(int)group];
        public bool Cleared => clearedAt > 0;

        public int EntryRowCount
        {
            get
            {
                var n = 0;
                foreach (var row in rows)
                {
                    if (!row.IsMarker) n++;
                }
                return n;
            }
        }

        public bool Collapse
        {
            get => collapse;
            set
            {
                if (collapse == value) return;
                collapse = value;
                Rebuild();
            }
        }

        public string Query
        {
            get => query;
            set
            {
                value ??= string.Empty;
                if (query == value) return;
                query = value;
                Rebuild();
            }
        }

        public void Toggle(LogGroup group)
        {
            shown[(int)group] = !shown[(int)group];
            Rebuild();
        }

        public void ResetFilters()
        {
            shown[0] = shown[1] = shown[2] = true;
            query = string.Empty;
            Rebuild();
        }

        public void Clear()
        {
            clearedAt = lastSeq;
            Rebuild();
        }

        public void Unclear()
        {
            clearedAt = 0;
            Rebuild();
        }

        /// Kéo entry mới từ bộ ghi. Trả về số hàng mới nối vào cuối — view dùng để bám đáy / đếm "N log mới".
        public int Pull()
        {
            incoming.Clear();
            LogRecorder.CopySince(lastSeq, incoming);
            var evicted = DropEvicted();
            if (incoming.Count == 0 && !evicted) return 0;

            var before = rows.Count;
            foreach (var entry in incoming)
            {
                lastSeq = entry.Seq;
                var item = new LogItem(entry);
                items.Add(item);
                if (!evicted) Admit(item);
            }
            if (!evicted) return rows.Count - before;

            Rebuild();
            // ponytail: sau khi bỏ log cũ thì đếm xấp xỉ bằng số entry vừa tới — con số chỉ để hiện "N log mới".
            return incoming.Count;
        }

        public string NoteText()
        {
            var text = LogRecorder.Recording
                ? $"Ghi từ {LogRecorder.StartedAt.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}"
                : "Máy này chưa ghi log";
            var dropped = LogRecorder.Dropped;
            if (dropped > 0) text += $" – đã bỏ {LogText.Count(dropped)} log cũ nhất";
            if (Cleared) text += $" – đã ẩn {LogText.Count(HiddenByClear())} log, bấm để hiện lại";
            return text;
        }

        /// Chỉ số hàng của một Seq (rows tăng dần theo Seq nên tìm nhị phân). -1 = không hiện.
        public int IndexOfSeq(long seq)
        {
            int low = 0, high = rows.Count - 1;
            while (low <= high)
            {
                var mid = (low + high) / 2;
                var at = rows[mid].Entry.Seq;
                if (at == seq) return mid + 1;
                if (at < seq) low = mid + 1;
                else high = mid - 1;
            }
            return -1;
        }

        /// Hàng log kế trước/sau theo bộ lọc hiện tại, bỏ qua vạch command — nút Trước/Sau ở trang chi tiết.
        public LogItem Neighbour(LogItem item, int direction)
        {
            var index = IndexOfSeq(item.Entry.Seq);
            if (index < 0) return null;
            for (var i = index + direction; i >= 1 && i < RowCount; i += direction)
            {
                if (!RowAt(i).IsMarker) return RowAt(i);
            }
            return null;
        }

        /// Text của mọi hàng đang hiện. Quá COPY_LIMIT thì giữ phần mới nhất.
        public string CopyAll()
        {
            var blocks = new List<string>();
            var length = 0;
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var block = Describe(rows[i]);
                if (length + block.Length > COPY_LIMIT) break;
                blocks.Add(block);
                length += block.Length + 1;
            }
            blocks.Reverse();
            return string.Join("\n", blocks);
        }

        internal static string Describe(LogItem item)
        {
            if (item.IsMarker) return $"[{item.Time}] › {item.Entry.Message}";
            var repeat = item.Repeat > 1 ? $" ×{item.Repeat}" : string.Empty;
            var stack = string.IsNullOrEmpty(item.Entry.Stack) ? string.Empty : "\n" + item.Entry.Stack.TrimEnd();
            return $"[{item.Time}] [{LogText.TypeLabel(item.Entry.Type)}]{repeat} {item.Entry.Message}{stack}";
        }

        private bool DropEvicted()
        {
            var oldest = LogRecorder.OldestSeq;
            var drop = 0;
            while (drop < items.Count && items[drop].Entry.Seq < oldest) drop++;
            if (drop == 0) return false;
            items.RemoveRange(0, drop);
            return true;
        }

        private void Rebuild()
        {
            rows.Clear();
            firsts.Clear();
            Array.Clear(counts, 0, counts.Length);
            foreach (var item in items)
            {
                item.Repeat = 1;
                item.Last = item.Entry.Time;
            }
            foreach (var item in items) Admit(item);
            Version++;
        }

        private void Admit(LogItem item)
        {
            if (item.Entry.Seq <= clearedAt) return;
            if (item.IsMarker)
            {
                rows.Add(item);
                return;
            }

            counts[(int)item.Group]++;
            if (!IsShown(item.Group) || !Matches(item)) return;

            if (collapse)
            {
                var key = (item.Entry.Type, item.Entry.Message, item.Entry.Stack);
                if (firsts.TryGetValue(key, out var first))
                {
                    first.Repeat++;
                    first.Last = item.Entry.Time;
                    Version++;
                    return;
                }
                firsts[key] = item;
            }
            rows.Add(item);
        }

        private bool Matches(LogItem item)
        {
            if (query.Length == 0) return true;
            return (item.Entry.Message ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.Caller.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int HiddenByClear()
        {
            var n = 0;
            foreach (var item in items)
            {
                if (item.Entry.Seq <= clearedAt && !item.IsMarker) n++;
            }
            return n;
        }
    }
}
```

- [ ] **Step 6: Chạy test** — Expected: `FAIL=0`; 5 + 5 + 10 test mới pass.

- [ ] **Step 7: Checkpoint** — `git status`, không commit.

---

### Task 7: Icon vector cho trang log

**Files:**
- Modify: `Scripts/Panel/DebugHubIcon.cs`
- Test: `Tests/Editor/DebugHubIconTests.cs`

**Interfaces:**
- Produces: `DebugHubIcon.Symbol` thêm (nối cuối, giữ giá trị cũ): `Log, More, Info, Warning, Error, Collapse, Down`.

- [ ] **Step 1: Viết test** — `Tests/Editor/DebugHubIconTests.cs`:

```csharp
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    public class DebugHubIconTests
    {
        /// Thiếu nhánh vẽ thì symbol rơi vào hình mặc định (đĩa tròn) hoặc trống — icon đọc sai nghĩa.
        [Test]
        public void EverySymbol_DrawsSomething()
        {
            var go = new GameObject("icon", typeof(RectTransform));
            try
            {
                ((RectTransform)go.transform).sizeDelta = new Vector2(100f, 100f);
                var icon = go.AddComponent<DebugHubIcon>();
                var populate = typeof(DebugHubIcon).GetMethod("OnPopulateMesh",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
                foreach (DebugHubIcon.Symbol symbol in Enum.GetValues(typeof(DebugHubIcon.Symbol)))
                {
                    icon.symbol = symbol;
                    using var mesh = new VertexHelper();
                    populate.Invoke(icon, new object[] { mesh });
                    Assert.Greater(mesh.currentVertCount, 0, symbol.ToString());
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: pass với 7 symbol cũ (test này là lưới an toàn; Step 3 thêm symbol mới, test phải vẫn pass).

- [ ] **Step 3: Thêm symbol**

Đổi enum:

```csharp
        public enum Symbol { Search, Tools, Close, Star, Disc, Replay, StarFilled, Log, More, Info, Warning, Error, Collapse, Down }
```

Trong `OnPopulateMesh`, chèn trước dòng cuối `else FilledCircle(mesh, Vector2.zero, 0.48f);`:

```csharp
            else if (symbol == Symbol.Log)
            {
                for (var i = 0; i < 3; i++)
                {
                    var y = 0.24f - i * 0.24f;
                    FilledCircle(mesh, new Vector2(-0.30f, y), 0.05f);
                    Line(mesh, new Vector2(-0.14f, y), new Vector2(0.36f, y));
                }
            }
            else if (symbol == Symbol.More)
            {
                for (var i = -1; i <= 1; i++) FilledCircle(mesh, new Vector2(i * 0.26f, 0f), 0.06f);
            }
            else if (symbol == Symbol.Info) FilledCircle(mesh, Vector2.zero, 0.16f);
            else if (symbol == Symbol.Warning)
            {
                var top = new Vector2(0f, 0.36f);
                var right = new Vector2(0.38f, -0.30f);
                var left = new Vector2(-0.38f, -0.30f);
                Line(mesh, top, right);
                Line(mesh, right, left);
                Line(mesh, left, top);
                Line(mesh, new Vector2(0f, 0.12f), new Vector2(0f, -0.08f));
                FilledCircle(mesh, new Vector2(0f, -0.19f), 0.035f);
            }
            else if (symbol == Symbol.Error)
            {
                Circle(mesh, Vector2.zero, 0.36f);
                Line(mesh, new Vector2(-0.14f, -0.14f), new Vector2(0.14f, 0.14f));
                Line(mesh, new Vector2(-0.14f, 0.14f), new Vector2(0.14f, -0.14f));
            }
            else if (symbol == Symbol.Collapse)
            {
                // Hai tấm chồng nhau: tấm trước đủ bốn cạnh, tấm sau chỉ lộ góc trên-phải.
                Box(mesh, new Vector2(-0.34f, -0.34f), new Vector2(0.14f, 0.10f));
                Line(mesh, new Vector2(-0.18f, 0.10f), new Vector2(-0.18f, 0.26f));
                Line(mesh, new Vector2(-0.18f, 0.26f), new Vector2(0.30f, 0.26f));
                Line(mesh, new Vector2(0.30f, 0.26f), new Vector2(0.30f, -0.18f));
                Line(mesh, new Vector2(0.30f, -0.18f), new Vector2(0.14f, -0.18f));
            }
            else if (symbol == Symbol.Down)
            {
                Line(mesh, new Vector2(0f, 0.32f), new Vector2(0f, -0.30f));
                Line(mesh, new Vector2(-0.22f, -0.08f), new Vector2(0f, -0.30f));
                Line(mesh, new Vector2(0.22f, -0.08f), new Vector2(0f, -0.30f));
            }
```

Thêm helper cạnh `Circle`:

```csharp
        private void Box(VertexHelper mesh, Vector2 min, Vector2 max)
        {
            Line(mesh, min, new Vector2(max.x, min.y));
            Line(mesh, new Vector2(max.x, min.y), max);
            Line(mesh, max, new Vector2(min.x, max.y));
            Line(mesh, new Vector2(min.x, max.y), min);
        }
```

- [ ] **Step 4: Chạy test** — Expected: `FAIL=0`.

- [ ] **Step 5: Checkpoint** — `git status`, không commit.

---

### Task 8: `EmbeddedEventSystem` thay `EventSystemHandler` của IDC

**Files:**
- Create: `Scripts/Panel/EmbeddedEventSystem.cs`
- Modify: `Prefabs/DebugHub.prefab` (qua script), `Tests/Editor/DebugHubPanelTests.cs` (`Prefab_ShipsWithInactiveEventSystem_WiredToHandler`)

**Interfaces:**
- Produces: `public class EmbeddedEventSystem : MonoBehaviour` với field `embeddedEventSystem`.

- [ ] **Step 1: Sửa test** — trong `Prefab_ShipsWithInactiveEventSystem_WiredToHandler` thay:

```csharp
            var handler = instance.GetComponent<IngameDebugConsole.EventSystemHandler>();
            Assert.IsNotNull(handler, "EventSystemHandler missing on prefab root");
```

bằng:

```csharp
            var handler = instance.GetComponent<EmbeddedEventSystem>();
            Assert.IsNotNull(handler, "EmbeddedEventSystem missing on prefab root");
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile (`EmbeddedEventSystem` chưa có).

- [ ] **Step 3: Viết `Scripts/Panel/EmbeddedEventSystem.cs`**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

namespace Hlight.Debug.Hub
{
    /// Prefab mang sẵn một EventSystem (tắt) để scene không có EventSystem vẫn gõ được password; chỉ bật
    /// nó khi scene chưa có cái nào. Thay EventSystemHandler của IngameDebugConsole.
    [DefaultExecutionOrder(1000)]
    public class EmbeddedEventSystem : MonoBehaviour
    {
        [SerializeField] private GameObject embeddedEventSystem;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private void Awake()
        {
            // Project chỉ bật Input System thì module cũ ném mỗi frame.
            if (embeddedEventSystem.TryGetComponent<StandaloneInputModule>(out var legacy))
            {
                DestroyImmediate(legacy);
                embeddedEventSystem.AddComponent<InputSystemUIInputModule>();
            }
        }
#endif

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            ActivateIfNeeded();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            embeddedEventSystem.SetActive(false);
        }

        /// Tắt trước rồi mới hỏi: scene mới mang EventSystem riêng thì EventSystem.current là cái của scene.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            embeddedEventSystem.SetActive(false);
            ActivateIfNeeded();
        }

        private void OnSceneUnloaded(Scene scene) => embeddedEventSystem.SetActive(false);

        private void ActivateIfNeeded()
        {
            if (!EventSystem.current) embeddedEventSystem.SetActive(true);
        }
    }
}
```

- [ ] **Step 4: Recompile**, rồi đổi component trên prefab — `Temp/debughub/swap-event-system.cs`:

```csharp
var path = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    UnityEngine.MonoBehaviour old = null;
    foreach (var component in root.GetComponents<UnityEngine.MonoBehaviour>())
        if (component != null && component.GetType().Name == "EventSystemHandler") old = component;
    if (old == null) return "không thấy EventSystemHandler — đã đổi rồi?";

    var target = new UnityEditor.SerializedObject(old).FindProperty("embeddedEventSystem").objectReferenceValue;
    var next = root.AddComponent<Hlight.Debug.Hub.EmbeddedEventSystem>();
    var serialized = new UnityEditor.SerializedObject(next);
    serialized.FindProperty("embeddedEventSystem").objectReferenceValue = target;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    UnityEngine.Object.DestroyImmediate(old);
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    return "ok: " + target.name;
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
```

Expected: `ok: EventSystem`.

- [ ] **Step 5: Chạy test** — Expected: `FAIL=0`.

- [ ] **Step 6: Checkpoint** — `git status`, không commit.

---

### Task 9: Trang log trong panel

**Files:**
- Create: `Scripts/Logs/LogRowView.cs`, `Scripts/Logs/LogView.cs`, `Scripts/Pages/LogPage.cs`, `Tests/Editor/LogShots.cs`
- Modify: `Scripts/Panel/DebugPage.cs`, `Scripts/Panel/DebugHubPanel.cs`, `Prefabs/DebugHub.prefab` (qua script)
- Test: `Tests/Editor/LogViewTests.cs`

**Interfaces:**
- Consumes: `LogModel`, `LogItem`, `LogText`, `LogRecorder`, `DebugHubIcon.Symbol.{Log, More, Info, Warning, Error, Collapse, Down}`.
- Produces:
  - `DebugPage(…, Action<DebugHubPanel> more = null, bool isLog = false)`, field `More`, `IsLog`.
  - `DebugHubPanel.ShowLog(LogModel model, long focusSeq)`, `bool TopIsLog`, `void Replace(DebugPage page)`.
  - `LogView`: `ROW_HEIGHT`, `BAR_HEIGHT`, `RectTransform Content`, `bool IsOpen`, `int ActiveRowCount`, `bool PillVisible`, `int NewRows`, `Open(DebugHubPanel, ScrollRect, LogModel, long)`, `Close()`, `Tick()`, `PlaceBar(float headerHeight)`.
  - `LogRowView`: `button`, `Index`, `Version`, `Selected`, `BindEntry(...)`, `BindMarker(...)`.
  - `LogPage.Build(long focusSeq = 0)`.
  - Prefab: `Window/LogBar/{ChipLog,ChipWarning,ChipError,Spacer,ChipCollapse,Divider}`, `Window/LogPill`, `Window/Scroll View/Viewport/LogContent/LogRow`, `Window/Scroll View/Viewport/LogEmpty`, `Window/Header/Buttons/{Log,More}`, `Log/Badge/Count`.

- [ ] **Step 1: Viết test** — `Tests/Editor/LogViewTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    public class LogViewTests
    {
        private DebugHubPanel panel;
        private LogView view;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            panel = TestPanel.Build();
            view = (LogView)TestPanel.Field(panel, "logView");
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
        }

        private void Open(long focusSeq = 0)
        {
            panel.Show(CommandsPage.Root());
            panel.Push(LogPage.Build(focusSeq));
            view.Tick();
        }

        private static void Logs(int n, LogType type = LogType.Log)
        {
            for (var i = 0; i < n; i++) LogRecorder.Receive($"log {i}", null, type);
        }

        private RectTransform Viewport => TestPanel.ScrollOf(panel).viewport;

        [Test]
        public void LogPage_SwapsTheScrollContent_AndBackRestoresIt()
        {
            Open();
            var scroll = TestPanel.ScrollOf(panel);
            Assert.IsTrue(view.IsOpen);
            Assert.AreSame(view.Content, scroll.content);
            Assert.IsFalse(panel.transform.Find("Window/Scroll View/Viewport/Content").gameObject.activeSelf);

            panel.Pop();
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual("Content", scroll.content.name);
            Assert.IsTrue(scroll.content.gameObject.activeSelf);
        }

        [Test]
        public void ThousandsOfLogs_OnlyBuildTheVisibleRows()
        {
            Logs(2000);
            Open();
            Assert.AreEqual(LogModel.Shared.RowCount * LogView.ROW_HEIGHT, view.Content.rect.height, 0.5f);
            var expected = Mathf.CeilToInt(Viewport.rect.height / LogView.ROW_HEIGHT) + 2;
            Assert.Greater(view.ActiveRowCount, 0);
            Assert.LessOrEqual(view.ActiveRowCount, expected);
        }

        [Test]
        public void LogPage_UsesTheFullPanelHeight_EvenWhenEmpty()
        {
            Open();
            var window = (RectTransform)TestPanel.Field(panel, "window");
            Assert.AreEqual(panel.MaxWindowHeight, window.rect.height, 1f);
        }

        [Test]
        public void Header_ShowsLogOnRoot_AndMoreOnTheLogPage()
        {
            var log = (Button)TestPanel.Field(panel, "logButton");
            var more = (Button)TestPanel.Field(panel, "moreButton");
            panel.Show(CommandsPage.Root());
            Assert.IsTrue(log.gameObject.activeSelf);
            Assert.IsFalse(more.gameObject.activeSelf);

            panel.Push(LogPage.Build());
            Assert.IsFalse(log.gameObject.activeSelf);
            Assert.IsTrue(more.gameObject.activeSelf);
        }

        [Test]
        public void ErrorChip_HidesErrors_ButKeepsTheCount()
        {
            Logs(3);
            Logs(2, LogType.Error);
            Open();
            panel.transform.Find("Window/LogBar/ChipError").GetComponent<Button>().onClick.Invoke();
            view.Tick();
            Assert.AreEqual(1 + 3, LogModel.Shared.RowCount);
            Assert.AreEqual(2, LogModel.Shared.Count(LogGroup.Error));
        }

        [Test]
        public void Following_StaysAtTheBottom_AsLogsArrive()
        {
            Logs(200);
            Open();
            Logs(50);
            view.Tick();
            var max = view.Content.rect.height - Viewport.rect.height;
            Assert.AreEqual(max, view.Content.anchoredPosition.y, 1f);
            Assert.IsFalse(view.PillVisible);
        }

        [Test]
        public void ScrolledUp_ShowsTheNewLogsPill_InsteadOfJumping()
        {
            Logs(200);
            Open();
            view.Content.anchoredPosition = Vector2.zero;
            TestPanel.ScrollOf(panel).onValueChanged.Invoke(Vector2.one);
            Assert.IsFalse(LogModel.Shared.Follow);

            Logs(5);
            view.Tick();
            Assert.AreEqual(0f, view.Content.anchoredPosition.y, 0.5f);
            Assert.IsTrue(view.PillVisible);
            Assert.AreEqual(5, view.NewRows);

            panel.transform.Find("Window/LogPill").GetComponent<Button>().onClick.Invoke();
            view.Tick();
            Assert.IsFalse(view.PillVisible);
            Assert.AreEqual(view.Content.rect.height - Viewport.rect.height, view.Content.anchoredPosition.y, 1f);
        }

        [Test]
        public void FocusSeq_ScrollsTheMarkerToTheTop_AndSelectsIt()
        {
            LogRecorder.Start();
            Logs(100);
            var seq = LogRecorder.Mark("level.goto 5");
            Logs(100);
            Open(seq);
            var index = LogModel.Shared.IndexOfSeq(seq);
            Assert.AreEqual(index * LogView.ROW_HEIGHT, view.Content.anchoredPosition.y, 1f);
            Assert.AreEqual(seq, LogModel.Shared.SelectedSeq);
            Assert.IsFalse(LogModel.Shared.Follow);
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile.

- [ ] **Step 3: `DebugPage` thêm `More` và `IsLog`** — thay toàn bộ `Scripts/Panel/DebugPage.cs`:

```csharp
using System;

namespace Hlight.Debug.Hub
{
    /// Một page không phải object: chỉ là tiêu đề + cách dựng nội dung vào panel.
    internal readonly struct DebugPage
    {
        public readonly string Title;
        public readonly Action<DebugHubPanel> Build;

        /// Lọc nội dung của chính trang này. null = không có tìm kiếm.
        public readonly Action<DebugHubPanel, string> Search;

        /// false thì header ẩn nút Tìm — page nhập tham số và page xác nhận không có gì để tìm.
        public readonly bool Searchable;

        /// Tự dựng lại 4 lần/giây (bỏ nhịp khi đang gõ).
        public readonly bool Live;

        /// Dòng nhỏ dưới tiêu đề — address của trang inspect. Drill 4 tầng thì tiêu đề chỉ còn tên lá,
        /// dòng này nói mình đang ở đâu; bấm vào là copy.
        public readonly string Subtitle;

        /// Hiện nút công cụ (Advanced), `?` và Log ở header — chỉ Commands gốc.
        public readonly bool ShowTools;

        /// Nút `…` ở header mở thao tác của cả trang. null = ẩn.
        public readonly Action<DebugHubPanel> More;

        /// Trang log: panel cao tối đa cố định (không co theo nội dung), chừa chỗ thanh lọc dưới header.
        public readonly bool IsLog;

        public DebugPage(string title, Action<DebugHubPanel> build,
            Action<DebugHubPanel, string> search = null, bool searchable = true, bool live = false,
            string subtitle = null, bool showTools = false, Action<DebugHubPanel> more = null, bool isLog = false)
        {
            Title = title;
            Build = build;
            Search = search;
            Searchable = searchable && search != null;
            Live = live;
            Subtitle = subtitle;
            ShowTools = showTools;
            More = more;
            IsLog = isLog;
        }
    }
}
```

- [ ] **Step 4: Viết `Scripts/Logs/LogRowView.cs`**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Một hàng của trang log. LogView giữ ~15 cái và gán lại khi cuộn — Bind chỉ gán chuỗi đã tính sẵn ở
    /// LogItem, không dựng gì mới.
    public class LogRowView : MonoBehaviour
    {
        private static readonly Color ErrorTint = new(0.898f, 0.282f, 0.302f, 0.08f);
        private static readonly Color ErrorText = new(1f, 0.79f, 0.80f);
        private static readonly Color Mint = new(0.60f, 0.87f, 0.83f);
        private static readonly Color Muted = Palette.ToColor(Palette.MUTED);
        private static readonly Color Dim = Palette.ToColor(Palette.DIM);
        private static readonly Color Warn = Palette.ToColor(Palette.WARN);
        private static readonly Color Bad = Palette.ToColor(Palette.BAD);

        [SerializeField] internal Button button;
        [SerializeField] private Image background;
        [SerializeField] private GameObject selected;
        [SerializeField] private GameObject entryGroup;
        [SerializeField] private DebugHubIcon icon;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private GameObject repeatGroup;
        [SerializeField] private TMP_Text repeat;
        [SerializeField] private GameObject markerGroup;
        [SerializeField] private GameObject markerLeft;
        [SerializeField] private GameObject markerRight;
        [SerializeField] private TMP_Text marker;

        /// Hàng mô hình đang hiện ở ô này, cùng Version và trạng thái chọn lúc gán: trùng hết thì khỏi gán lại.
        internal int Index = -1;
        internal int Version = -1;
        internal bool Selected;

        internal void BindEntry(LogItem item, string query, bool isSelected, bool showRepeat)
        {
            entryGroup.SetActive(true);
            markerGroup.SetActive(false);
            var error = item.Group == LogGroup.Error;
            background.color = error ? ErrorTint : Color.clear;
            icon.symbol = item.Group == LogGroup.Error ? DebugHubIcon.Symbol.Error
                : item.Group == LogGroup.Warning ? DebugHubIcon.Symbol.Warning
                : DebugHubIcon.Symbol.Info;
            icon.color = error ? Bad : item.Group == LogGroup.Warning ? Warn : Dim;
            message.color = error ? ErrorText : Color.white;
            message.text = LogText.Highlight(item.Preview, query);
            meta.text = item.Caller.Length == 0 ? item.Time : $"{item.Time} – {LogText.Escape(item.Caller)}";
            var repeated = showRepeat && item.Repeat > 1;
            repeatGroup.SetActive(repeated);
            if (repeated) repeat.text = "×" + LogText.Count(item.Repeat);
            selected.SetActive(isSelected);
        }

        /// Vạch command (mint, có hai đường kẻ) hoặc dòng ghi chú đầu danh sách (xám, không kẻ).
        internal void BindMarker(string text, bool command, bool isSelected)
        {
            entryGroup.SetActive(false);
            markerGroup.SetActive(true);
            background.color = Color.clear;
            markerLeft.SetActive(command);
            markerRight.SetActive(command);
            marker.color = command ? Mint : Muted;
            marker.text = command ? "› " + LogText.Escape(text) : LogText.Escape(text);
            selected.SetActive(isSelected);
        }
    }
}
```

- [ ] **Step 5: Viết `Scripts/Logs/LogView.cs`**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Trang log trong panel (spec ① §5). Mượn ScrollRect của panel; danh sách ảo hoá: hàng cao cố định,
    /// chỉ ~15 hàng UI thật, đặt theo chỉ số. Panel thường dựng lại ~0,9 ms/hàng nên không chứa nổi vài
    /// nghìn log.
    public class LogView : MonoBehaviour
    {
        internal const float ROW_HEIGHT = 176f;
        internal const float BAR_HEIGHT = 112f;
        private const int EXTRA_ROWS = 2;

        [Serializable]
        internal struct Chip
        {
            public Button button;
            public Image background;
            public CanvasGroup group;
            public TMP_Text label;
        }

        private static readonly Color ChipOn = new(0.145f, 0.196f, 0.275f);
        private static readonly Color WarningOn = new(0.227f, 0.184f, 0.102f);
        private static readonly Color ErrorOn = new(0.227f, 0.110f, 0.133f);

        [SerializeField] private RectTransform bar;
        [SerializeField] private Chip chipLog;
        [SerializeField] private Chip chipWarning;
        [SerializeField] private Chip chipError;
        [SerializeField] private Chip chipCollapse;
        [SerializeField] private Button pill;
        [SerializeField] private TMP_Text pillLabel;
        [SerializeField] private GameObject empty;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button emptyReset;
        [SerializeField] private LogRowView rowTemplate;

        private readonly List<LogRowView> pool = new();
        private readonly int[] shownCounts = { -1, -1, -1 };
        private DebugHubPanel panel;
        private ScrollRect scrollRect;
        private RectTransform previousContent;
        private LogModel model;
        private bool wired;
        private bool adjusting;
        private bool pendingBottom;
        private long pendingFocus;
        private int newRows;
        private int shownNewRows = -1;
        private int shownEmpty = -1;

        internal RectTransform Content => (RectTransform)transform;
        internal bool IsOpen => gameObject.activeSelf;
        internal bool PillVisible => pill.gameObject.activeSelf;
        internal int NewRows => newRows;

        internal int ActiveRowCount
        {
            get
            {
                var n = 0;
                foreach (var row in pool)
                {
                    if (row.gameObject.activeSelf) n++;
                }
                return n;
            }
        }

        private RectTransform Viewport => scrollRect.viewport ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        /// Panel gọi từ Build của trang log. Không cuộn ở đây: panel còn RestoreScroll sau Build, nên cuộn
        /// (bám đáy / nhảy tới vạch) để Tick ở LateUpdate làm.
        internal void Open(DebugHubPanel owner, ScrollRect scroll, LogModel logModel, long focusSeq)
        {
            panel = owner;
            scrollRect = scroll;
            model = logModel;
            Wire();
            if (!IsOpen)
            {
                previousContent = scrollRect.content;
                previousContent.gameObject.SetActive(false);
                gameObject.SetActive(true);
                bar.gameObject.SetActive(true);
                scrollRect.content = Content;
                foreach (var row in pool) row.Index = -1;
                shownNewRows = shownEmpty = -1;
                for (var i = 0; i < shownCounts.Length; i++) shownCounts[i] = -1;
            }
            if (focusSeq > 0)
            {
                pendingFocus = focusSeq;
                model.SelectedSeq = focusSeq;
                model.Follow = false;
            }
            else if (model.Follow) pendingBottom = true;
        }

        internal void Close()
        {
            if (!IsOpen) return;
            scrollRect.content = previousContent;
            previousContent.gameObject.SetActive(true);
            gameObject.SetActive(false);
            bar.gameObject.SetActive(false);
            pill.gameObject.SetActive(false);
            empty.SetActive(false);
        }

        /// Thanh lọc nằm ngay dưới header, ngoài vùng cuộn nên không trôi theo danh sách.
        internal void PlaceBar(float headerHeight) => bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, -headerHeight);

        private void LateUpdate() => Tick();

        internal void Tick()
        {
            if (!IsOpen || model == null) return;

            var version = model.Version;
            var appended = model.Pull();
            var changed = appended > 0 || model.Version != version;
            // Cao đúng số hàng trước khi cuộn: ScrollTo kẹp theo chiều cao content.
            if (changed || pendingFocus > 0 || pendingBottom)
                Content.sizeDelta = new Vector2(Content.sizeDelta.x, model.RowCount * ROW_HEIGHT);

            if (pendingFocus > 0)
            {
                var index = model.IndexOfSeq(pendingFocus);
                if (index > 0) ScrollTo(index * ROW_HEIGHT);
                pendingFocus = 0;
                pendingBottom = false;
                newRows = 0;
            }
            else if (pendingBottom || (changed && model.Follow))
            {
                ScrollToBottom();
                pendingBottom = false;
            }
            else if (appended > 0) newRows += appended;
            if (model.Follow) newRows = 0;

            model.SeenErrors = LogRecorder.ErrorCount;
            BindRows();
            UpdateChrome();
        }

        /// Gắn ở đây chứ không ở Awake: EditMode test instantiate prefab không chắc gọi Awake.
        private void Wire()
        {
            if (wired) return;
            wired = true;
            chipLog.button.onClick.AddListener(() => model.Toggle(LogGroup.Log));
            chipWarning.button.onClick.AddListener(() => model.Toggle(LogGroup.Warning));
            chipError.button.onClick.AddListener(() => model.Toggle(LogGroup.Error));
            chipCollapse.button.onClick.AddListener(() => model.Collapse = !model.Collapse);
            pill.onClick.AddListener(() =>
            {
                model.Follow = true;
                pendingBottom = true;
            });
            emptyReset.onClick.AddListener(() => model.ResetFilters());
            scrollRect.onValueChanged.AddListener(_ => OnScrolled());
        }

        private void OnScrolled()
        {
            if (!IsOpen || adjusting || model == null) return;
            var max = Mathf.Max(0f, Content.rect.height - Viewport.rect.height);
            model.Follow = Content.anchoredPosition.y >= max - ROW_HEIGHT * 0.5f;
        }

        private void ScrollTo(float y)
        {
            adjusting = true;
            scrollRect.StopMovement();
            var max = Mathf.Max(0f, Content.rect.height - Viewport.rect.height);
            Content.anchoredPosition = new Vector2(Content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
            adjusting = false;
        }

        private void ScrollToBottom()
        {
            ScrollTo(float.MaxValue);
            model.Follow = true;
        }

        private void BindRows()
        {
            var needed = Mathf.CeilToInt(Viewport.rect.height / ROW_HEIGHT) + EXTRA_ROWS;
            while (pool.Count < needed) pool.Add(CreateRow());

            var count = pool.Count;
            var first = Mathf.Max(0, Mathf.FloorToInt(Content.anchoredPosition.y / ROW_HEIGHT));
            for (var k = 0; k < count; k++)
            {
                // Hàng chỉ số i luôn ở ô i % count: cuộn một hàng chỉ phải gán lại một ô.
                var index = first + ((k - first) % count + count) % count;
                var row = pool[k];
                if (index >= model.RowCount)
                {
                    if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                    row.Index = -1;
                    continue;
                }
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);

                var item = model.RowAt(index);
                var isSelected = item != null && item.Entry.Seq == model.SelectedSeq;
                if (row.Index == index && row.Version == model.Version && row.Selected == isSelected) continue;
                row.Index = index;
                row.Version = model.Version;
                row.Selected = isSelected;
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0f, -index * ROW_HEIGHT);

                if (item == null) row.BindMarker(model.NoteText(), false, false);
                else if (item.IsMarker) row.BindMarker(item.Entry.Message, true, isSelected);
                else row.BindEntry(item, model.Query, isSelected, model.Collapse);
            }
        }

        private LogRowView CreateRow()
        {
            var row = Instantiate(rowTemplate, Content);
            row.gameObject.SetActive(false);
            row.button.onClick.AddListener(() => OnRowClicked(row));
            return row;
        }

        private void OnRowClicked(LogRowView row)
        {
            if (row.Index == 0)
            {
                if (model.Cleared) model.Unclear();
                return;
            }
            var item = model.RowAt(row.Index);
            if (item == null || item.IsMarker) return;
            model.SelectedSeq = item.Entry.Seq;
        }

        /// Chỉ gán chữ khi con số đổi: trang đang mở mà không có log mới thì không cấp phát gì mỗi frame.
        private void UpdateChrome()
        {
            SetChip(chipLog, 0, model.IsShown(LogGroup.Log), ChipOn, model.Count(LogGroup.Log));
            SetChip(chipWarning, 1, model.IsShown(LogGroup.Warning), WarningOn, model.Count(LogGroup.Warning));
            SetChip(chipError, 2, model.IsShown(LogGroup.Error), ErrorOn, model.Count(LogGroup.Error));
            SetLook(chipCollapse, model.Collapse, ChipOn);

            var showPill = newRows > 0 && !model.Follow;
            if (pill.gameObject.activeSelf != showPill) pill.gameObject.SetActive(showPill);
            if (showPill && shownNewRows != newRows)
            {
                shownNewRows = newRows;
                pillLabel.text = $"{LogText.Count(newRows)} log mới";
            }

            // 0 = có hàng, 1 = chưa có log nào, 2 = có log nhưng bộ lọc ẩn hết.
            var state = model.RowCount > 1 ? 0 : model.Total == 0 && !model.Cleared ? 1 : 2;
            if (state == shownEmpty) return;
            shownEmpty = state;
            empty.SetActive(state != 0);
            emptyLabel.text = state == 1 ? "Chưa có log" : "Không có log khớp bộ lọc";
            emptyReset.gameObject.SetActive(state == 2 && !model.Cleared);
        }

        private void SetChip(Chip chip, int slot, bool on, Color onColor, int count)
        {
            SetLook(chip, on, onColor);
            if (shownCounts[slot] == count) return;
            shownCounts[slot] = count;
            chip.label.text = LogText.Count(count);
        }

        /// So trước khi gán: gán alpha của CanvasGroup mỗi frame có thể làm canvas dựng lại dù không đổi gì.
        private static void SetLook(Chip chip, bool on, Color onColor)
        {
            var color = on ? onColor : Color.clear;
            if (chip.background.color != color) chip.background.color = color;
            var alpha = on ? 1f : 0.45f;
            if (!Mathf.Approximately(chip.group.alpha, alpha)) chip.group.alpha = alpha;
        }
    }
}
```

- [ ] **Step 6: Viết `Scripts/Pages/LogPage.cs`**

```csharp
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Trang log (spec ① §5). Model sống suốt phiên (LogModel.Shared) nên lọc / gộp / xoá giữ nguyên qua
    /// các lần mở. Tìm dùng ô tìm của header như mọi trang khác.
    internal static class LogPage
    {
        internal static DebugPage Build(long focusSeq = 0)
        {
            var model = LogModel.Shared;
            return new DebugPage("Log",
                panel =>
                {
                    model.Query = string.Empty;
                    panel.ShowLog(model, focusSeq);
                    // Chỉ nhảy lần đầu; lùi từ trang chi tiết về thì giữ chỗ đang đọc.
                    focusSeq = 0;
                },
                (panel, query) =>
                {
                    model.Query = query;
                    panel.ShowLog(model, 0);
                },
                more: panel => panel.Push(Actions(model)),
                isLog: true);
        }

        private static DebugPage Actions(LogModel model)
        {
            return new DebugPage("Thao tác", panel =>
            {
                panel.AddAction("Copy tất cả", () =>
                {
                    GUIUtility.systemCopyBuffer = model.CopyAll();
                    panel.ShowResult($"đã copy {LogText.Count(model.EntryRowCount)} log", false);
                    panel.Pop();
                }, "Theo bộ lọc đang bật. Quá dài thì giữ phần mới nhất.");
                panel.AddAction("Xoá", () =>
                {
                    model.Clear();
                    panel.Pop();
                }, "Ẩn mọi log tới lúc này. Bấm dòng đầu danh sách để hiện lại.");
            }, searchable: false);
        }
    }
}
```

- [ ] **Step 7: Nối vào `DebugHubPanel.cs`**

Thêm field, ngay sau `[SerializeField] private DebugHubToast toast;`:

```csharp
        [Header("Log")]
        [SerializeField] private LogView logView;
        [SerializeField] private Button logButton;
        [Tooltip("Số lỗi chưa xem trên nút Log. Ẩn/hiện qua object cha (Badge).")]
        [SerializeField] private TMP_Text logBadge;
        [SerializeField] private Button moreButton;
```

Thêm property cạnh `IsOpen`:

```csharp
        internal bool TopIsLog => stack.Count > 0 && stack.Peek().Page.IsLog;
```

Trong `Awake`, sau `advancedButton.onClick.AddListener(...)`:

```csharp
            logButton.onClick.AddListener(() => Push(LogPage.Build()));
            moreButton.onClick.AddListener(() =>
            {
                if (stack.Count > 0) stack.Peek().Page.More?.Invoke(this);
            });
```

Thêm method sau `Pop()`:

```csharp
        /// Thay trang trên cùng (Trước/Sau ở trang chi tiết): đi qua mười log không chất mười tầng stack.
        internal void Replace(DebugPage page)
        {
            SavePageState();
            if (stack.Count > 0) stack.Pop();
            stack.Push(new PageState { Page = page });
            query = string.Empty;
            Rebuild();
            RestoreScroll(1f);
        }

        /// Trang log gọi từ Build của nó: LogView mượn ScrollRect, giấu Content của row.
        internal void ShowLog(LogModel model, long focusSeq) => logView.Open(this, scrollRect, model, focusSeq);
```

Trong `Rebuild()`, ngay sau `refreshLater = false;`:

```csharp
            // Mọi trang đều qua đây: rời trang log thì trả ScrollRect về Content của row.
            logView.Close();
```

và sau `helpButton.gameObject.SetActive(page.ShowTools);`:

```csharp
            logButton.gameObject.SetActive(page.ShowTools);
            moreButton.gameObject.SetActive(page.More != null);
            var unseen = LogRecorder.ErrorCount - LogModel.Shared.SeenErrors;
            logBadge.transform.parent.gameObject.SetActive(unseen > 0);
            if (unseen > 0) logBadge.text = LogText.Badge(unseen);
```

Trong `LayoutHeader`, thay dòng cuối:

```csharp
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -height - 12f);
```

bằng:

```csharp
            var logBar = page.IsLog ? LogView.BAR_HEIGHT : 0f;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -height - 12f - logBar);
            logView.PlaceBar(height);
```

Trong `FitWindowToContent`, thay:

```csharp
            var desired = LayoutUtility.GetPreferredHeight(content) + chrome;
```

bằng:

```csharp
            // Trang log cao tối đa cố định: co theo số log thì panel nhảy mỗi lần có log mới.
            var desired = TopIsLog ? MaxWindowHeight : LayoutUtility.GetPreferredHeight(content) + chrome;
```

- [ ] **Step 8: Recompile.** Sửa prefab — `Temp/debughub/build-log-ui.cs`:

```csharp
var path = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    var panel = root.GetComponentInChildren<Hlight.Debug.Hub.DebugHubPanel>(true);
    var window = panel.transform.Find("Window");
    if (window.Find("LogBar") != null) return "đã dựng rồi, bỏ qua";
    var buttons = window.Find("Header/Buttons");
    var advanced = buttons.Find("Advanced");
    var viewport = window.Find("Scroll View/Viewport");
    var buttonRow = viewport.Find("Content/ButtonRow");
    var label = buttonRow.Find("Label").gameObject;
    var rounded = buttonRow.GetComponent<UnityEngine.UI.Image>().sprite;

    UnityEngine.Color Hex(string hex)
    {
        UnityEngine.ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }

    Hlight.Debug.Hub.DebugHubIcon.Symbol Sym(string name) =>
        (Hlight.Debug.Hub.DebugHubIcon.Symbol)System.Enum.Parse(typeof(Hlight.Debug.Hub.DebugHubIcon.Symbol), name);

    UnityEngine.RectTransform Node(string name, UnityEngine.Transform parent)
    {
        var go = new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform));
        go.transform.SetParent(parent, false);
        return (UnityEngine.RectTransform)go.transform;
    }

    void Place(UnityEngine.RectTransform r, float minX, float minY, float maxX, float maxY, float pivotX, float pivotY, float x, float y, float w, float h)
    {
        r.anchorMin = new UnityEngine.Vector2(minX, minY);
        r.anchorMax = new UnityEngine.Vector2(maxX, maxY);
        r.pivot = new UnityEngine.Vector2(pivotX, pivotY);
        r.anchoredPosition = new UnityEngine.Vector2(x, y);
        r.sizeDelta = new UnityEngine.Vector2(w, h);
    }

    UnityEngine.UI.Image Fill(UnityEngine.GameObject go, string hex, float radius)
    {
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = Hex(hex);
        if (radius > 0f)
        {
            // Sprite "Hub Rounded" viền 10 px: bán kính hiện ra = 10 / pixelsPerUnitMultiplier.
            image.sprite = rounded;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 10f / radius;
        }
        return image;
    }

    TMPro.TMP_Text Text(UnityEngine.Transform parent, string name, float size, string hex, TMPro.TextAlignmentOptions align, bool wrap)
    {
        // Clone từ label có sẵn, không AddComponent: AddComponent gán font mặc định của project vào prefab,
        // mà package không được mang font.
        var go = UnityEngine.Object.Instantiate(label, parent);
        go.name = name;
        var text = go.GetComponent<TMPro.TMP_Text>();
        text.text = string.Empty;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.color = Hex(hex);
        text.alignment = align;
        text.textWrappingMode = wrap ? TMPro.TextWrappingModes.Normal : TMPro.TextWrappingModes.NoWrap;
        text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    Hlight.Debug.Hub.DebugHubIcon Icon(UnityEngine.Transform parent, string symbol, string hex, float size)
    {
        var rect = Node("Icon", parent);
        rect.sizeDelta = new UnityEngine.Vector2(size, size);
        var icon = rect.gameObject.AddComponent<Hlight.Debug.Hub.DebugHubIcon>();
        icon.symbol = Sym(symbol);
        icon.color = Hex(hex);
        icon.raycastTarget = false;
        var layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.preferredWidth = size;
        layout.preferredHeight = size;
        return icon;
    }

    UnityEngine.UI.HorizontalLayoutGroup Row(UnityEngine.GameObject go, int left, int right, int top, int bottom, float spacing, UnityEngine.TextAnchor align)
    {
        var row = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        row.padding = new UnityEngine.RectOffset(left, right, top, bottom);
        row.spacing = spacing;
        row.childAlignment = align;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return row;
    }

    UnityEngine.UI.Button Press(UnityEngine.GameObject go, UnityEngine.UI.Graphic target)
    {
        var button = go.AddComponent<UnityEngine.UI.Button>();
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        button.targetGraphic = target;
        return button;
    }

    // --- Header: nút Log (kèm chấm đỏ) và nút … ---
    UnityEngine.Transform HeaderButton(string name, string symbol, int sibling)
    {
        var button = UnityEngine.Object.Instantiate(advanced.gameObject, buttons).transform;
        button.name = name;
        button.SetSiblingIndex(sibling);
        button.Find("Icon").GetComponent<Hlight.Debug.Hub.DebugHubIcon>().symbol = Sym(symbol);
        return button;
    }
    var logButton = HeaderButton("Log", "Log", 1);
    var badge = Node("Badge", logButton);
    Place(badge, 1, 1, 1, 1, 1, 1, -4, -12, 48, 48);
    Fill(badge.gameObject, "#E5484D", 24f).raycastTarget = false;
    var badgeCount = Text(badge, "Count", 28, "#FFFFFF", TMPro.TextAlignmentOptions.Center, false);
    Place(badgeCount.rectTransform, 0, 0, 1, 1, 0.5f, 0.5f, 0, 0, 0, 0);
    badge.gameObject.SetActive(false);
    var moreButton = HeaderButton("More", "More", 2);

    // --- Thanh lọc ---
    var bar = Node("LogBar", window);
    Place(bar, 0, 1, 1, 1, 0.5f, 1, 0, -148, -56, 112);
    Row(bar.gameObject, 0, 0, 0, 12, 16, UnityEngine.TextAnchor.MiddleLeft);
    UnityEngine.GameObject Chip(string name, string symbol, string hex, string caption)
    {
        var rect = Node(name, bar);
        var image = Fill(rect.gameObject, "#253246", 38f);
        Press(rect.gameObject, image);
        rect.gameObject.AddComponent<UnityEngine.CanvasGroup>();
        Row(rect.gameObject, 22, 24, 0, 0, 10, UnityEngine.TextAnchor.MiddleCenter);
        var layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.minHeight = 76;
        layout.preferredHeight = 76;
        Icon(rect, symbol, hex, 40);
        Text(rect, "Label", 38, hex, TMPro.TextAlignmentOptions.Center, false).text = caption;
        return rect.gameObject;
    }
    var chipLog = Chip("ChipLog", "Info", "#A7B6CA", "0");
    var chipWarning = Chip("ChipWarning", "Warning", "#E8B04B", "0");
    var chipError = Chip("ChipError", "Error", "#E5484D", "0");
    Node("Spacer", bar).gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
    var chipCollapse = Chip("ChipCollapse", "Collapse", "#D9E4F1", "Gộp");
    var divider = Node("Divider", bar);
    divider.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
    Place(divider, 0, 0, 1, 0, 0.5f, 0, 0, 0, 0, 2);
    Fill(divider.gameObject, "#2A3446", 0f).raycastTarget = false;
    bar.gameObject.SetActive(false);

    // --- Viên "N log mới" ---
    var pill = Node("LogPill", window);
    Place(pill, 0.5f, 0, 0.5f, 0, 0.5f, 0, 0, 40, 0, 0);
    var pillButton = Press(pill.gameObject, Fill(pill.gameObject, "#99DED4", 34f));
    Row(pill.gameObject, 28, 32, 14, 14, 12, UnityEngine.TextAnchor.MiddleCenter);
    var fitter = pill.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
    fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    Icon(pill, "Down", "#0E2A26", 40);
    var pillLabel = Text(pill, "Label", 38, "#0E2A26", TMPro.TextAlignmentOptions.Center, false);
    pillLabel.text = "0 log mới";
    pill.gameObject.SetActive(false);

    // --- Danh sách + template hàng ---
    var content = Node("LogContent", viewport);
    Place(content, 0, 1, 1, 1, 0.5f, 1, 0, 0, 0, 0);
    var view = content.gameObject.AddComponent<Hlight.Debug.Hub.LogView>();

    var rowRect = Node("LogRow", content);
    Place(rowRect, 0, 1, 1, 1, 0.5f, 1, 0, 0, 0, 176);
    var rowImage = Fill(rowRect.gameObject, "#00000000", 0f);
    var rowButton = Press(rowRect.gameObject, rowImage);
    var rowView = rowRect.gameObject.AddComponent<Hlight.Debug.Hub.LogRowView>();

    var selected = Node("Selected", rowRect);
    Place(selected, 0, 0, 0, 1, 0, 0.5f, 0, 0, 8, 0);
    Fill(selected.gameObject, "#99DED4", 0f).raycastTarget = false;
    selected.gameObject.SetActive(false);

    var line = Node("Divider", rowRect);
    Place(line, 0, 0, 1, 0, 0.5f, 0, 0, 0, 0, 2);
    Fill(line.gameObject, "#222C3D", 0f).raycastTarget = false;

    var entry = Node("Entry", rowRect);
    Place(entry, 0, 0, 1, 1, 0.5f, 0.5f, 0, 0, 0, 0);
    var icon = Icon(entry, "Info", "#A7B6CA", 44);
    UnityEngine.Object.DestroyImmediate(icon.GetComponent<UnityEngine.UI.LayoutElement>());
    Place((UnityEngine.RectTransform)icon.transform, 0, 1, 0, 1, 0.5f, 0.5f, 52, -46, 44, 44);

    var message = Text(entry, "Message", 40, "#FFFFFF", TMPro.TextAlignmentOptions.TopLeft, true);
    message.maxVisibleLines = 2;
    Place(message.rectTransform, 0, 1, 1, 1, 0.5f, 1, 0, 0, 0, 0);
    message.rectTransform.offsetMin = new UnityEngine.Vector2(92, -116);
    message.rectTransform.offsetMax = new UnityEngine.Vector2(-28, -16);

    var meta = Text(entry, "Meta", 32, "#91A4BC", TMPro.TextAlignmentOptions.MidlineLeft, false);
    Place(meta.rectTransform, 0, 0, 1, 0, 0.5f, 0, 0, 0, 0, 0);
    meta.rectTransform.offsetMin = new UnityEngine.Vector2(92, 14);
    meta.rectTransform.offsetMax = new UnityEngine.Vector2(-150, 54);

    var repeatGroup = Node("Repeat", entry);
    Place(repeatGroup, 1, 0, 1, 0, 1, 0, -28, 14, 110, 40);
    Fill(repeatGroup.gameObject, "#2F3B50", 20f).raycastTarget = false;
    var repeat = Text(repeatGroup, "Label", 28, "#A7B6CA", TMPro.TextAlignmentOptions.Center, false);
    Place(repeat.rectTransform, 0, 0, 1, 1, 0.5f, 0.5f, 0, 0, 0, 0);
    repeatGroup.gameObject.SetActive(false);

    var markerGroup = Node("Marker", rowRect);
    Place(markerGroup, 0, 0, 1, 1, 0.5f, 0.5f, 0, 0, 0, 0);
    Row(markerGroup.gameObject, 28, 28, 0, 0, 20, UnityEngine.TextAnchor.MiddleCenter).childControlHeight = false;
    UnityEngine.GameObject Rule(string name)
    {
        var rule = Node(name, markerGroup);
        rule.sizeDelta = new UnityEngine.Vector2(0, 2);
        Fill(rule.gameObject, "#2F4A4A", 0f).raycastTarget = false;
        rule.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        return rule.gameObject;
    }
    var markerLeft = Rule("Left");
    var marker = Text(markerGroup, "Label", 34, "#99DED4", TMPro.TextAlignmentOptions.Center, false);
    marker.rectTransform.sizeDelta = new UnityEngine.Vector2(0, 48);
    var markerRight = Rule("Right");
    markerGroup.gameObject.SetActive(false);
    rowRect.gameObject.SetActive(false);

    // --- Trạng thái rỗng ---
    var empty = Node("LogEmpty", viewport);
    Place(empty, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 800, 300);
    var column = empty.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
    column.spacing = 28;
    column.childAlignment = UnityEngine.TextAnchor.MiddleCenter;
    column.childControlWidth = true;
    column.childControlHeight = true;
    column.childForceExpandWidth = false;
    column.childForceExpandHeight = false;
    var emptyLabel = Text(empty, "Label", 40, "#A7B6CA", TMPro.TextAlignmentOptions.Center, true);
    emptyLabel.text = "Chưa có log";
    emptyLabel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 760;
    var reset = Node("Reset", empty);
    var resetButton = Press(reset.gameObject, Fill(reset.gameObject, "#253246", 34f));
    Row(reset.gameObject, 36, 36, 16, 16, 0, UnityEngine.TextAnchor.MiddleCenter);
    Text(reset, "Label", 38, "#99DED4", TMPro.TextAlignmentOptions.Center, false).text = "Bỏ lọc";
    empty.gameObject.SetActive(false);

    // --- Nối field ---
    void Set(UnityEditor.SerializedObject target, string property, UnityEngine.Object value) =>
        target.FindProperty(property).objectReferenceValue = value;

    var viewSo = new UnityEditor.SerializedObject(view);
    void SetChip(string field, UnityEngine.GameObject chip)
    {
        Set(viewSo, field + ".button", chip.GetComponent<UnityEngine.UI.Button>());
        Set(viewSo, field + ".background", chip.GetComponent<UnityEngine.UI.Image>());
        Set(viewSo, field + ".group", chip.GetComponent<UnityEngine.CanvasGroup>());
        Set(viewSo, field + ".label", chip.transform.Find("Label").GetComponent<TMPro.TMP_Text>());
    }
    Set(viewSo, "bar", bar);
    SetChip("chipLog", chipLog);
    SetChip("chipWarning", chipWarning);
    SetChip("chipError", chipError);
    SetChip("chipCollapse", chipCollapse);
    Set(viewSo, "pill", pillButton);
    Set(viewSo, "pillLabel", pillLabel);
    Set(viewSo, "empty", empty.gameObject);
    Set(viewSo, "emptyLabel", emptyLabel);
    Set(viewSo, "emptyReset", resetButton);
    Set(viewSo, "rowTemplate", rowView);
    viewSo.ApplyModifiedPropertiesWithoutUndo();

    var rowSo = new UnityEditor.SerializedObject(rowView);
    Set(rowSo, "button", rowButton);
    Set(rowSo, "background", rowImage);
    Set(rowSo, "selected", selected.gameObject);
    Set(rowSo, "entryGroup", entry.gameObject);
    Set(rowSo, "icon", icon);
    Set(rowSo, "message", message);
    Set(rowSo, "meta", meta);
    Set(rowSo, "repeatGroup", repeatGroup.gameObject);
    Set(rowSo, "repeat", repeat);
    Set(rowSo, "markerGroup", markerGroup.gameObject);
    Set(rowSo, "markerLeft", markerLeft);
    Set(rowSo, "markerRight", markerRight);
    Set(rowSo, "marker", marker);
    rowSo.ApplyModifiedPropertiesWithoutUndo();

    var panelSo = new UnityEditor.SerializedObject(panel);
    Set(panelSo, "logView", view);
    Set(panelSo, "logButton", logButton.GetComponent<UnityEngine.UI.Button>());
    Set(panelSo, "logBadge", badgeCount);
    Set(panelSo, "moreButton", moreButton.GetComponent<UnityEngine.UI.Button>());
    panelSo.ApplyModifiedPropertiesWithoutUndo();

    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    return "ok";
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
```

Expected: `ok`. Kiểm prefab không mang font:

```bash
grep -o "m_fontAsset: {[^}]*}" Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab | sort | uniq -c
```

Expected: chỉ một dòng `m_fontAsset: {fileID: 0}`.

- [ ] **Step 9: Chạy test** — Expected: `FAIL=0`, 8 test `LogViewTests` pass, các test panel cũ vẫn pass.

- [ ] **Step 10: Viết `Tests/Editor/LogShots.cs`** (công cụ chụp, không phải test):

```csharp
using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    /// Chụp panel ra PNG để so với mockup đã duyệt — không phải test. Gọi qua `unity cmd eval_file`:
    /// System.Type.GetType("Hlight.Debug.Hub.Tests.LogShots, Hlight.Debug.Hub.Tests").GetMethod("CaptureAll").Invoke(null, null)
    public static class LogShots
    {
        private const string STACK =
            "UnityEngine.Debug:LogError (object)\n" +
            "Harvest.Gameplay.ItemPiece:OnPointerUp (UnityEngine.EventSystems.PointerEventData) (at Assets/0_DevRoot/Scripts/ItemPiece.cs:212)\n" +
            "Harvest.Gameplay.TrayController:TryPush (Harvest.Gameplay.ItemPiece) (at Assets/0_DevRoot/Scripts/TrayController.cs:61)\n" +
            "UnityEngine.EventSystems.ExecuteEvents:Execute (UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.BaseEventData)\n";

        public static string CaptureAll()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            LogRecorder.Start();
            Seed();
            try
            {
                return Shoot("Temp/debughub-log.png", panel => panel.Push(LogPage.Build()));
            }
            finally
            {
                LogRecorder.Reset();
                LogModel.ResetShared();
            }
        }

        private static void Seed()
        {
            LogRecorder.Receive("[Boot] RootScope ready in 412 ms", "Harvest.RootScope:Awake () (at Assets/RootScope.cs:38)", LogType.Log);
            LogRecorder.Receive("[Firebase] Remote config fetched, 12 keys", "Harvest.RemoteConfigService:OnFetch () (at Assets/RemoteConfigService.cs:91)", LogType.Log);
            for (var i = 0; i < 4; i++)
                LogRecorder.Receive("[MAX] Interstitial not ready, skipped", "Harvest.AdsManager:ShowInterstitial () (at Assets/AdsManager.cs:144)", LogType.Warning);
            LogRecorder.Mark("level.goto 5");
            LogRecorder.Receive("Load level 5 (preset Level_005)", "Harvest.LevelLoader:Load (int) (at Assets/LevelLoader.cs:57)", LogType.Log);
            LogRecorder.Receive("NullReferenceException: Object reference not set to an instance of an object", STACK, LogType.Error);
            LogRecorder.Receive("Match 3 x Tomato, combo 2", "Harvest.MatchBuffer:Resolve () (at Assets/MatchBuffer.cs:88)", LogType.Log);
            LogRecorder.Receive("Slot full, waiting for match slide", "Harvest.TrayController:Push () (at Assets/TrayController.cs:61)", LogType.Warning);
            LogRecorder.Receive("Failed to load addressable 'Level_006': InvalidKeyException", "Harvest.LevelPreset:LoadAsync (string) (at Assets/LevelPreset.cs:40)", LogType.Error);
            LogRecorder.Receive("[Ads] Banner loaded 320x50", "Harvest.BannerView:OnLoaded () (at Assets/BannerView.cs:23)", LogType.Log);
        }

        internal static string Shoot(string path, Action<DebugHubPanel> arrange)
        {
            var panel = TestPanel.Build();
            var canvas = panel.GetComponentInParent<Canvas>();
            var camera = new GameObject("shot camera").AddComponent<Camera>();
            var texture = new RenderTexture(1080, 2160, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.227f, 0.2f);
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();

                panel.Show(CommandsPage.Root());
                arrange(panel);
                var view = (LogView)TestPanel.Field(panel, "logView");
                view.Tick();
                Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>()) text.ForceMeshUpdate();

                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(1080, 2160, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1080, 2160), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                TestPanel.Destroy(panel);
            }
        }
    }
}
```

- [ ] **Step 11: Chụp và so với mockup**

`Temp/debughub/shots.cs`:

```csharp
return System.Type.GetType("Hlight.Debug.Hub.Tests.LogShots, Hlight.Debug.Hub.Tests").GetMethod("CaptureAll").Invoke(null, null);
```

Recompile, chạy, mở `Temp/debughub-log.png` (Read tool). So với mockup đã duyệt (spec §5, màn danh sách). Phải thấy: header `‹ Log` + kính lúp + `…` + `×`; thanh chip có số đếm; dòng ghi chú "Ghi từ …"; vạch mint `› level.goto 5`; dòng lỗi nền đỏ nhạt, icon tròn ×; dòng phụ `giờ – nơi gọi`; không có ô vuông thiếu glyph. Lệch thì chỉnh số trong `build-log-ui.cs` (xoá các object `LogBar`, `LogPill`, `LogContent`, `LogEmpty`, `Header/Buttons/Log`, `Header/Buttons/More` rồi chạy lại) hoặc chỉnh trực tiếp trên prefab, chụp lại. Gửi ảnh cho user xem.

- [ ] **Step 12: Checkpoint** — `git status`, không commit.

---

### Task 10: Trang chi tiết log

**Files:**
- Create: `Scripts/Panel/DebugHubBar.cs`, `Scripts/Pages/LogDetailPage.cs`
- Modify: `Scripts/Panel/DebugHubPanel.cs` (`barTemplate`, `AddBar`, `ReleaseListeners`), `Scripts/Logs/LogView.cs` (`OnRowClicked`), `Tests/Editor/LogShots.cs`, `Prefabs/DebugHub.prefab` (qua script)
- Test: `Tests/Editor/LogDetailPageTests.cs`

**Interfaces:**
- Consumes: `LogModel.Neighbour`, `LogModel.Describe`, `StackFrames.Split`, `LogText.*`, `DebugHubPanel.Replace`.
- Produces: `DebugHubBar.Set(string previous, Action onPrevious, string next, Action onNext)`, `DebugHubBar.Release()`; `DebugHubPanel.AddBar(string previous, Action onPrevious, string primary, Action onPrimary, string next, Action onNext)`; `LogDetailPage.For(LogModel, LogItem)`. Prefab: `Content/BarRow/{Previous,Primary,Next}`.

- [ ] **Step 1: Viết test** — `Tests/Editor/LogDetailPageTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub.Tests
{
    public class LogDetailPageTests
    {
        private const string STACK =
            "UnityEngine.Debug:LogError (object)\n" +
            "Harvest.ItemPiece:OnPointerUp (UnityEngine.EventSystems.PointerEventData) (at Assets/ItemPiece.cs:212)\n";

        private DebugHubPanel panel;
        private LogView view;
        private string clipboard;

        [SetUp]
        public void SetUp()
        {
            clipboard = GUIUtility.systemCopyBuffer;
            LogRecorder.Reset();
            LogModel.ResetShared();
            panel = TestPanel.Build();
            view = (LogView)TestPanel.Field(panel, "logView");
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
            GUIUtility.systemCopyBuffer = clipboard;
        }

        private void Open()
        {
            panel.Show(CommandsPage.Root());
            panel.Push(LogPage.Build());
            view.Tick();
        }

        private DebugHubBar Bar() => TestPanel.Rows(panel).Select(r => r.GetComponent<DebugHubBar>()).First(b => b);

        private LogItem Item(string message)
        {
            var model = LogModel.Shared;
            for (var i = 1; i < model.RowCount; i++)
            {
                if (model.RowAt(i).Entry.Message == message) return model.RowAt(i);
            }
            Assert.Fail($"không có log '{message}'");
            return null;
        }

        [Test]
        public void RowClick_OpensTheDetail_WithMessageFramesAndBar()
        {
            LogRecorder.Receive("boom", STACK, LogType.Error);
            Open();
            view.GetComponentsInChildren<LogRowView>(false).First(r => r.Index == 1).button.onClick.Invoke();

            Assert.AreEqual(3, panel.StackDepth);
            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Any(l => l.Contains("boom")));
            Assert.IsTrue(labels.Any(l => l.Contains("ItemPiece.OnPointerUp")));
            Assert.IsNotNull(Bar());
        }

        [Test]
        public void Next_ReplacesThePage_AndSkipsMarkers()
        {
            LogRecorder.Start();
            LogRecorder.Receive("first", null, LogType.Log);
            LogRecorder.Mark("cmd");
            LogRecorder.Receive("second", null, LogType.Log);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("first")));
            var depth = panel.StackDepth;

            Bar().transform.Find("Next").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(depth, panel.StackDepth);
            Assert.IsTrue(TestPanel.LabelsOf(panel).Any(l => l.Contains("second")));
        }

        [Test]
        public void FirstLog_HasNoPrevious()
        {
            LogRecorder.Receive("only", null, LogType.Log);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("only")));
            Assert.IsFalse(Bar().transform.Find("Previous").GetComponent<Button>().interactable);
        }

        [Test]
        public void Copy_PutsMessageAndStackOnTheClipboard()
        {
            LogRecorder.Receive("boom", STACK, LogType.Error);
            Open();
            panel.Push(LogDetailPage.For(LogModel.Shared, Item("boom")));
            TestPanel.Rows(panel).First(r => r.GetComponent<DebugHubBar>()).button.onClick.Invoke();
            StringAssert.Contains("boom", GUIUtility.systemCopyBuffer);
            StringAssert.Contains("ItemPiece.cs:212", GUIUtility.systemCopyBuffer);
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile.

- [ ] **Step 3: Viết `Scripts/Panel/DebugHubBar.cs`**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Debug.Hub
{
    /// Hàng nút ngang: nút chính ở giữa (DebugHubRow.button/label của row), hai nút phụ hai bên.
    /// Trang chi tiết log dùng cho Trước / Copy / Sau.
    public class DebugHubBar : MonoBehaviour
    {
        [SerializeField] private Button previous;
        [SerializeField] private TMP_Text previousLabel;
        [SerializeField] private CanvasGroup previousGroup;
        [SerializeField] private Button next;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private CanvasGroup nextGroup;

        internal void Set(string previousText, Action onPrevious, string nextText, Action onNext)
        {
            Side(previous, previousLabel, previousGroup, previousText, onPrevious);
            Side(next, nextLabel, nextGroup, nextText, onNext);
        }

        internal void Release()
        {
            previous.onClick.RemoveAllListeners();
            next.onClick.RemoveAllListeners();
        }

        /// Không có log kế bên thì nút mờ đi chứ không biến mất: nút chính giữ nguyên chỗ, ngón tay khỏi hụt.
        private static void Side(Button button, TMP_Text label, CanvasGroup group, string text, Action action)
        {
            label.text = text;
            button.interactable = action != null;
            group.alpha = action != null ? 1f : 0.35f;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }
    }
}
```

- [ ] **Step 4: `DebugHubPanel` thêm `AddBar`**

Field, sau `[SerializeField] private DebugHubRow textTemplate;`:

```csharp
        [SerializeField] private DebugHubRow barTemplate;
```

Method, sau `AddPrimary`:

```csharp
        /// Hàng nút ngang ở cuối trang: nút chính ở giữa, hai nút phụ hai bên (action null = mờ).
        internal void AddBar(string previous, Action onPrevious, string primary, Action onPrimary, string next, Action onNext)
        {
            var row = Spawn(barTemplate, primary);
            row.GetComponent<DebugHubBar>().Set(previous, onPrevious, next, onNext);
            row.button.onClick.AddListener(() => onPrimary?.Invoke());
        }
```

Trong `ReleaseListeners`, sau `if (row.more) row.more.onClick.RemoveAllListeners();`:

```csharp
            if (row.TryGetComponent<DebugHubBar>(out var bar)) bar.Release();
```

- [ ] **Step 5: Viết `Scripts/Pages/LogDetailPage.cs`**

```csharp
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Trang chi tiết một log (spec ① §5.4): đọc lỗi — nội dung đầy đủ, stack từng frame (frame game sáng,
    /// engine mờ), một nút chính Copy, Trước/Sau đi qua các log của bộ lọc hiện tại.
    internal static class LogDetailPage
    {
        private const string ENGINE_FRAME = "#6F7F96";

        internal static DebugPage For(LogModel model, LogItem item)
        {
            model.SelectedSeq = item.Entry.Seq;
            var type = item.Entry.Type;
            return new DebugPage($"<color={LogText.ColorOf(type)}>{LogText.TypeLabel(type)}</color>", panel =>
            {
                panel.AddButton(LogText.Escape(LogText.DetailPreview(item.Entry.Message)),
                    () => Copy(panel, item.Entry.Message, "nội dung"));

                panel.AddText($"<color={Palette.DIM}>Stack trace</color>");
                var frames = StackFrames.Split(item.Entry.Stack);
                if (frames.Count == 0) panel.AddText($"<color={Palette.MUTED}>Không có stack trace.</color>");
                foreach (var frame in frames) panel.AddText(Describe(frame));

                var previous = model.Neighbour(item, -1);
                var next = model.Neighbour(item, 1);
                panel.AddBar(
                    "‹ Trước", previous == null ? null : () => panel.Replace(For(model, previous)),
                    "Copy", () => Copy(panel, LogModel.Describe(item), "log"),
                    "Sau ›", next == null ? null : () => panel.Replace(For(model, next)));
            }, searchable: false, subtitle: Subtitle(item));
        }

        /// Dòng phụ dưới tiêu đề bị cắt ở 40 ký tự (TailOf) nên giờ lần cuối chỉ tới giây.
        private static string Subtitle(LogItem item) => item.Repeat > 1
            ? $"{item.Time} – lặp {LogText.Count(item.Repeat)} lần, cuối {item.Last:HH:mm:ss}"
            : item.Time;

        private static string Describe(StackFrames.Frame frame)
        {
            if (!frame.Game) return $"<color={ENGINE_FRAME}>{LogText.Escape(frame.Method)}</color>";
            if (string.IsNullOrEmpty(frame.Location)) return LogText.Escape(frame.Method);
            return $"{LogText.Escape(frame.Method)}\n<size=85%><color={Palette.MUTED}>{LogText.Escape(frame.Location)}</color></size>";
        }

        private static void Copy(DebugHubPanel panel, string text, string what)
        {
            GUIUtility.systemCopyBuffer = text;
            panel.ShowResult($"đã copy {what}", false);
        }
    }
}
```

- [ ] **Step 6: Bấm hàng log mở chi tiết** — trong `LogView.OnRowClicked` thay dòng cuối `model.SelectedSeq = item.Entry.Seq;` bằng:

```csharp
            panel.Push(LogDetailPage.For(model, item));
```

- [ ] **Step 7: Recompile.** Dựng `BarRow` — `Temp/debughub/build-log-bar.cs`:

```csharp
var path = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    var panel = root.GetComponentInChildren<Hlight.Debug.Hub.DebugHubPanel>(true);
    var content = panel.transform.Find("Window/Scroll View/Viewport/Content");
    if (content.Find("BarRow") != null) return "đã dựng rồi, bỏ qua";
    var buttonRow = content.Find("ButtonRow").gameObject;
    var actionRow = content.Find("ActionRow").gameObject;

    var bar = new UnityEngine.GameObject("BarRow", typeof(UnityEngine.RectTransform));
    bar.transform.SetParent(content, false);
    ((UnityEngine.RectTransform)bar.transform).sizeDelta = new UnityEngine.Vector2(0, 132);
    var layout = bar.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
    layout.spacing = 16;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;
    var element = bar.AddComponent<UnityEngine.UI.LayoutElement>();
    element.minHeight = 132;
    element.preferredHeight = 132;

    UnityEngine.GameObject Part(UnityEngine.GameObject source, string name, string caption, float width, float flexible)
    {
        var part = UnityEngine.Object.Instantiate(source, bar.transform);
        part.name = name;
        part.SetActive(true);
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Hlight.Debug.Hub.DebugHubRow>());
        foreach (var child in new[] { "Detail", "More", "Chevron" })
        {
            var extra = part.transform.Find(child);
            if (extra != null) UnityEngine.Object.DestroyImmediate(extra.gameObject);
        }
        var partLayout = part.GetComponent<UnityEngine.UI.LayoutElement>();
        partLayout.preferredWidth = width;
        partLayout.flexibleWidth = flexible;
        var label = part.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.text = caption;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = UnityEngine.Vector2.zero;
        labelRect.anchorMax = UnityEngine.Vector2.one;
        labelRect.offsetMin = new UnityEngine.Vector2(16, 8);
        labelRect.offsetMax = new UnityEngine.Vector2(-16, -8);
        return part;
    }

    var previous = Part(buttonRow, "Previous", "‹ Trước", 260, 0);
    previous.AddComponent<UnityEngine.CanvasGroup>();
    var primary = Part(actionRow, "Primary", "Copy", -1, 1);
    var next = Part(buttonRow, "Next", "Sau ›", 260, 0);
    next.AddComponent<UnityEngine.CanvasGroup>();

    var row = bar.AddComponent<Hlight.Debug.Hub.DebugHubRow>();
    row.label = primary.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
    row.button = primary.GetComponent<UnityEngine.UI.Button>();
    var hubBar = bar.AddComponent<Hlight.Debug.Hub.DebugHubBar>();
    var so = new UnityEditor.SerializedObject(hubBar);
    so.FindProperty("previous").objectReferenceValue = previous.GetComponent<UnityEngine.UI.Button>();
    so.FindProperty("previousLabel").objectReferenceValue = previous.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
    so.FindProperty("previousGroup").objectReferenceValue = previous.GetComponent<UnityEngine.CanvasGroup>();
    so.FindProperty("next").objectReferenceValue = next.GetComponent<UnityEngine.UI.Button>();
    so.FindProperty("nextLabel").objectReferenceValue = next.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
    so.FindProperty("nextGroup").objectReferenceValue = next.GetComponent<UnityEngine.CanvasGroup>();
    so.ApplyModifiedPropertiesWithoutUndo();
    bar.SetActive(false);

    var panelSo = new UnityEditor.SerializedObject(panel);
    panelSo.FindProperty("barTemplate").objectReferenceValue = row;
    panelSo.ApplyModifiedPropertiesWithoutUndo();

    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    return "ok";
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
```

Expected: `ok`. Kiểm lại font như task 9 Step 8.

- [ ] **Step 8: Chạy test** — Expected: `FAIL=0`, 4 test `LogDetailPageTests` pass.

- [ ] **Step 9: Chụp trang chi tiết** — trong `LogShots.CaptureAll`, thay khối `try { return Shoot(...); }` bằng:

```csharp
            try
            {
                var list = Shoot("Temp/debughub-log.png", panel => panel.Push(LogPage.Build()));
                var detail = Shoot("Temp/debughub-log-detail.png", panel =>
                {
                    panel.Push(LogPage.Build());
                    var model = LogModel.Shared;
                    model.Pull();
                    for (var i = 1; i < model.RowCount; i++)
                    {
                        if (model.RowAt(i).Entry.Type != LogType.Error) continue;
                        panel.Push(LogDetailPage.For(model, model.RowAt(i)));
                        break;
                    }
                });
                return list + "\n" + detail;
            }
```

Chạy `shots.cs`, mở hai ảnh, so với mockup (màn chi tiết): tiêu đề "Lỗi" màu đỏ, khối nội dung, "Stack trace", frame game sáng kèm `ItemPiece.cs:212`, frame `UnityEngine.*` mờ, hàng `‹ Trước | Copy | Sau ›`. Gửi ảnh cho user.

- [ ] **Step 10: Checkpoint** — `git status`, không commit.

---

### Task 11: Chấm đỏ trên bong bóng, bấm dòng kết quả mở log

**Files:**
- Modify: `Scripts/DebugHubEntry.cs`, `Scripts/DebugHub.cs` (`Update`, `Awake`, `OpenLog`, comment `Await`), `Scripts/Panel/DebugHubToast.cs` (comment), `Scripts/Panel/DebugHubPanel.cs:85` (comment), `Prefabs/DebugHub.prefab` (qua script)
- Test: `Tests/Editor/LogBadgeTests.cs`

**Interfaces:**
- Consumes: `LogRecorder.ErrorCount`, `LogRecorder.LastMarkSeq`, `LogModel.Shared.SeenErrors`, `LogText.Badge`, `DebugHubPanel.TopIsLog/Replace/Push`.
- Produces: `DebugHubEntry.Badge { set; }` (long).

- [ ] **Step 1: Viết test** — `Tests/Editor/LogBadgeTests.cs`:

```csharp
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class LogBadgeTests
    {
        private DebugHubPanel panel;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
        }

        [Test]
        public void EntryBadge_ShowsCount_CapsAt99_HidesAtZero()
        {
            var entry = panel.transform.root.GetComponentInChildren<DebugHubEntry>(true);
            var badge = entry.transform.Find("Badge").gameObject;
            var count = badge.transform.Find("Count").GetComponent<TMP_Text>();

            entry.Badge = 3;
            Assert.IsTrue(badge.activeSelf);
            Assert.AreEqual("3", count.text);
            entry.Badge = 120;
            Assert.AreEqual("99+", count.text);
            entry.Badge = 0;
            Assert.IsFalse(badge.activeSelf);
        }

        /// Mở trang log là "đã xem": quay về gốc thì chấm đỏ trên nút Log tắt.
        [Test]
        public void LogButtonBadge_ClearsOnceTheLogPageWasSeen()
        {
            LogRecorder.Receive("e1", null, LogType.Error);
            LogRecorder.Receive("e2", null, LogType.Error);
            var badge = ((TMP_Text)TestPanel.Field(panel, "logBadge")).transform.parent.gameObject;

            panel.Show(CommandsPage.Root());
            Assert.IsTrue(badge.activeSelf);
            Assert.AreEqual("2", ((TMP_Text)TestPanel.Field(panel, "logBadge")).text);

            panel.Push(LogPage.Build());
            ((LogView)TestPanel.Field(panel, "logView")).Tick();
            panel.Pop();
            Assert.IsFalse(badge.activeSelf);
        }
    }
}
```

- [ ] **Step 2: Chạy test** — Expected: lỗi compile (`DebugHubEntry.Badge` chưa có).

- [ ] **Step 3: `DebugHubEntry` thêm chấm đỏ**

Thêm `using TMPro;`. Thêm field và property:

```csharp
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeCount;

        /// Số lỗi ghi được mà chưa mở trang log xem (spec ① §5.1). 0 = ẩn.
        public long Badge
        {
            set
            {
                badge.SetActive(value > 0);
                if (value > 0) badgeCount.text = LogText.Badge(value);
            }
        }
```

- [ ] **Step 4: `DebugHub` nối chấm đỏ và dòng kết quả**

- Field: `private long badgeShown = -1;`
- Đầu `Update()`:

```csharp
            // Chấm đỏ = lỗi chưa xem. So số nguyên mỗi frame, chỉ gán chữ khi đổi.
            var unseen = LogRecorder.ErrorCount - LogModel.Shared.SeenErrors;
            if (unseen != badgeShown)
            {
                badgeShown = unseen;
                entry.Badge = unseen;
            }
```

- Trong `Awake`, thay:

```csharp
            // Kết quả dài bị cắt ở dòng nổi; toàn văn kèm stack trace nằm ở log window.
            panel.ResultClicked += () => console.Enabled = true;
```

bằng:

```csharp
            // Kết quả dài bị cắt ở dòng nổi; toàn văn kèm stack nằm ở trang log, tại vạch của command vừa chạy.
            panel.ResultClicked += () => OpenLog(LogRecorder.LastMarkSeq);
```

- Thêm method sau `OpenCommandTree()`:

```csharp
        private void OpenLog(long focusSeq)
        {
            if (!panel.IsOpen) panel.Show(CommandsPage.Root());
            if (panel.TopIsLog) panel.Replace(LogPage.Build(focusSeq));
            else panel.Push(LogPage.Build(focusSeq));
        }
```

- Comment của `Await`: "Kết quả về **sau** khi DebugRegistry.Run đã trả nên nó vào console, không vào dòng kết quả của lần chạy đó — dòng kết quả bấm được để mở console." → "Kết quả về **sau** khi DebugRegistry.Run đã trả nên nó vào trang log, không vào dòng kết quả của lần chạy đó — bấm dòng kết quả là mở trang log tại command vừa chạy."
- `DebugHubToast` comment lớp: "Bấm vào nó = mở console log window, nơi có toàn văn kèm stack trace" → "Bấm vào nó = mở trang log tại vạch của command vừa chạy, nơi có toàn văn kèm stack trace"; tooltip `characterLimit` "toàn văn xem ở console" → "toàn văn xem ở trang log".
- `DebugHubPanel` comment `ResultClicked`: "DebugHub nối vào đây để mở console log window (toàn văn ở đó)." → "DebugHub nối vào đây để mở trang log tại command vừa chạy."

- [ ] **Step 5: Recompile.** Dựng chấm đỏ trên bong bóng — `Temp/debughub/build-entry-badge.cs`:

```csharp
var path = "Packages/com.hlight.debug-hub/Prefabs/DebugHub.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    var entry = root.GetComponentInChildren<Hlight.Debug.Hub.DebugHubEntry>(true);
    if (entry.transform.Find("Badge") != null) return "đã dựng rồi, bỏ qua";
    var panel = root.GetComponentInChildren<Hlight.Debug.Hub.DebugHubPanel>(true);
    var label = panel.transform.Find("Window/Scroll View/Viewport/Content/ButtonRow/Label").gameObject;
    var rounded = panel.transform.Find("Window/Scroll View/Viewport/Content/ButtonRow").GetComponent<UnityEngine.UI.Image>().sprite;

    var badge = new UnityEngine.GameObject("Badge", typeof(UnityEngine.RectTransform));
    badge.transform.SetParent(entry.transform, false);
    var rect = (UnityEngine.RectTransform)badge.transform;
    rect.anchorMin = rect.anchorMax = new UnityEngine.Vector2(1, 1);
    rect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    rect.anchoredPosition = new UnityEngine.Vector2(-22, -22);
    rect.sizeDelta = new UnityEngine.Vector2(60, 60);
    var image = badge.AddComponent<UnityEngine.UI.Image>();
    UnityEngine.ColorUtility.TryParseHtmlString("#E5484D", out var red);
    image.color = red;
    image.sprite = rounded;
    image.type = UnityEngine.UI.Image.Type.Sliced;
    image.pixelsPerUnitMultiplier = 10f / 30f;
    image.raycastTarget = false;

    // Clone label có sẵn: giữ font rỗng (TMP dùng font mặc định của game), package không mang font.
    var count = UnityEngine.Object.Instantiate(label, badge.transform);
    count.name = "Count";
    var text = count.GetComponent<TMPro.TMP_Text>();
    text.text = string.Empty;
    text.fontSize = 32;
    text.enableAutoSizing = false;
    text.color = UnityEngine.Color.white;
    text.alignment = TMPro.TextAlignmentOptions.Center;
    text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
    text.raycastTarget = false;
    var countRect = text.rectTransform;
    countRect.anchorMin = UnityEngine.Vector2.zero;
    countRect.anchorMax = UnityEngine.Vector2.one;
    countRect.offsetMin = countRect.offsetMax = UnityEngine.Vector2.zero;
    badge.SetActive(false);

    var so = new UnityEditor.SerializedObject(entry);
    so.FindProperty("badge").objectReferenceValue = badge;
    so.FindProperty("badgeCount").objectReferenceValue = text;
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    return "ok";
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
```

Expected: `ok`. Kiểm font như task 9.

- [ ] **Step 6: Chạy test** — Expected: `FAIL=0`, 2 test `LogBadgeTests` pass.

- [ ] **Step 7: Kiểm tay trong Play mode** (không có test tự động vì `DebugHub.Awake` không chạy trong EditMode): vào Play, mở hub (máy Editor là bản nội bộ nên đang ghi; mở khoá bằng password nếu chưa), chạy một command có log (ví dụ `prefs.get`), bấm dòng kết quả → trang log mở, vạch `› prefs.get …` ở đầu khung, có viền mint. `Debug.LogError("x")` qua command bất kỳ → bong bóng có chấm đỏ; mở trang log → về gốc, chấm đỏ tắt.

- [ ] **Step 8: Checkpoint** — `git status`, không commit.

---

### Task 12: Gỡ IngameDebugConsole

**Files:**
- Rename: `Scripts/Features/ConsoleController.cs` → `Scripts/Features/BuiltinCommands.cs` (+ `.meta`, giữ GUID)
- Modify: `Scripts/DebugHub.cs`, `Hlight.Debug.Hub.asmdef`, `Tests/Editor/Hlight.Debug.Hub.Tests.asmdef`
- Delete: `Resources/` (+ `.meta`), submodule `ThirdParty/UnityIngameDebugConsole`, `ThirdParty/` (+ `.meta`), `.gitmodules`

**Interfaces:**
- Produces: `public class BuiltinCommands : MonoBehaviour` với `internal void Initialize()`; `DebugHub.commands` (FormerlySerializedAs `console`).

- [ ] **Step 1: Đổi tên, giữ GUID**

```bash
cd Packages/com.hlight.debug-hub
git mv Scripts/Features/ConsoleController.cs Scripts/Features/BuiltinCommands.cs
git mv Scripts/Features/ConsoleController.cs.meta Scripts/Features/BuiltinCommands.cs.meta
cd ../..
```

- [ ] **Step 2: Viết lại phần đầu lớp** — trong `Scripts/Features/BuiltinCommands.cs`:
- Xoá `using IngameDebugConsole;` và `using UnityEngine.UI;`.
- Thay từ `public class ConsoleController : MonoBehaviour` tới hết `TryEnableConsole()` bằng:

```csharp
    /// Command có sẵn của hub (prefs, time, sdk, hub.*). Trước đây là ConsoleController — GUID của .meta
    /// giữ nguyên để prefab không mất reference.
    public class BuiltinCommands : MonoBehaviour
    {
        [SerializeField] private RepeatButton repeat;
        [SerializeField] private DebugHubEntry entry;
```

- Trong `Initialize()` xoá khối lệnh cầu (comment "Command duy nhất còn đăng ký vào IDC…" + `DebugLogConsole.AddCommand<string>("hub", …);`) và hai dòng:

```csharp
            DebugHub.AddValue(this, "console.show", "Hiện cửa sổ log.", () => Enabled, value => Enabled = value);
            DebugHub.AddValue(this, "console.auto", "Tự bật console ở lần chạy sau.", () => AutoEnable, value => AutoEnable = value);
```

- Comment cuối hàm nhắc "Root page cũ có toggle…" giữ nguyên.

- [ ] **Step 3: `DebugHub.cs` trỏ sang `BuiltinCommands`**

- Thêm `using UnityEngine.Serialization;`.
- Thay `[SerializeField] private ConsoleController console;` bằng `[FormerlySerializedAs("console")] [SerializeField] private BuiltinCommands commands;`.
- Trong `Awake`: `console.Initialize();` → `commands.Initialize();`; xoá `console.TryEnableConsole();`; comment "không từ Awake của console" → "không từ Awake của BuiltinCommands".
- Comment `OpenCommandTree`: "Console/Auto/Proxima là command console.*" → "Proxima là command console.proxima", "(ConsoleController.Initialize())" → "(BuiltinCommands.Initialize())".

- [ ] **Step 4: Bỏ reference asmdef** — xoá dòng `"IngameDebugConsole.Runtime",` trong `Hlight.Debug.Hub.asmdef` và `Tests/Editor/Hlight.Debug.Hub.Tests.asmdef`.

- [ ] **Step 5: Xoá `Resources` và submodule**

```bash
cd Packages/com.hlight.debug-hub
git rm -q -r Resources Resources.meta
git submodule deinit -f ThirdParty/UnityIngameDebugConsole
git rm -q -f ThirdParty/UnityIngameDebugConsole
rm -rf "$(git rev-parse --git-dir)/modules/ThirdParty/UnityIngameDebugConsole"
git rm -q -f --ignore-unmatch ThirdParty/UnityIngameDebugConsole.meta ThirdParty.meta .gitmodules
rm -rf ThirdParty ThirdParty.meta
cd ../..
```

`.git` của package là file trỏ về `.git/modules/Packages/com.hlight.debug-hub` của repo chính — dùng `git rev-parse --git-dir`, đừng đoán đường.

- [ ] **Step 6: Kiểm không còn dấu vết**

```bash
grep -rn "IngameDebugConsole\|DebugLogConsole\|ConsoleController\|EventSystemHandler" Packages/com.hlight.debug-hub --include=*.cs --include=*.asmdef --include=*.prefab --include=*.json
```

Expected chỉ còn: comment bản quyền trong `DebugValues.Parse.cs`, comment trong `EmbeddedEventSystem.cs` và `BuiltinCommands.cs`. Prefab còn object tên `Console` (đúng script, tên cũ) và field rác `resourcePath` — vô hại, Unity bỏ khi lưu lại prefab.

- [ ] **Step 7: Chạy test** — Expected: `FAIL=0`. Console Editor không có lỗi missing script khi mở prefab.

- [ ] **Step 8: Kiểm tay** — vào Play: không còn log "[ConsoleMethod]" quét assembly; hub mở bình thường; `hub.entry`, `time.skip`, `prefs.get` chạy; trang Help liệt kê tham số với tên kiểu ("Integer"…).

- [ ] **Step 9: Checkpoint** — `git status` (thấy rename, xoá submodule, xoá Resources), không commit.

---

### Task 13: README, CHANGELOG, phiên bản

**Files:**
- Modify: `README.md`, `CHANGELOG.md`, `package.json`

- [ ] **Step 1: `package.json`** — `"version": "3.0.0"`, `"description": "In-game debug hub for Hlight Unity projects: cheat command tree, reflection inspector and log viewer behind a password gate."`.

- [ ] **Step 2: `CHANGELOG.md`** — thêm trên `## 2.0.0`:

```markdown
## 3.0.0

### Phá vỡ tương thích
- Bỏ IngameDebugConsole (submodule, prefab console, `[ConsoleMethod]`, lệnh cầu `hub "..."`, `console.show`, `console.auto`). Log xem ở trang **Log** của hub.
- Bỏ symbol `DISABLE_DEBUG_HUB`, `ALWAYS_ENABLE_INGAME_DEBUGGER` và `RenameFolderOnBuild`.
- Hub không còn xử lý `PRODUCTION`: project tắt log Unity thì hub không có log để hiện.
- `networkReachabilityAuthenticationBypass` → `companyNetwork` (trang nội bộ + chuỗi bắt buộc, IP công khai). Chỉ kiểm khi ô password đang mở, mọi build.
- `ConsoleController` đổi tên `BuiltinCommands` (cùng GUID).

### Thêm
- Bộ ghi log chạy từ lúc khởi động trên máy được ghi (đã mở khoá hoặc bản nội bộ: TestFlight/ad-hoc/Xcode, Firebase App Tester, Editor): mở trang log muộn vẫn đủ log của phiên.
- Trang Log: danh sách ảo hoá, lọc Log/Cảnh báo/Lỗi kèm bộ đếm, tìm có tô sáng, gộp log trùng, vạch command trong dòng thời gian, bám đáy + "N log mới", trang chi tiết (stack từng frame, Copy, Trước/Sau), Copy tất cả, Xoá có hiện lại.
- Chấm đỏ số lỗi chưa xem trên bong bóng và nút Log. Bấm dòng kết quả mở trang log tại command vừa chạy.
- Bong bóng nhớ trạng thái hiện/ẩn qua phiên.

### Sửa
- Hết ~66 ms quét mọi assembly lúc khởi động (của IngameDebugConsole) trên máy mọi người chơi.
- Parser đối số thuộc hub (mang từ IngameDebugConsole, MIT): hết bảng chép tay.
```

- [ ] **Step 3: `README.md`**

- Đoạn mở đầu: "log window (IngameDebugConsole)" → "trang log".
- Thay mục **Cài đặt** bằng: "Thêm package vào `Packages/` (embedded) hoặc qua git URL. Không còn submodule."
- Thay mục **Symbol** và **Bỏ qua password theo mạng** bằng:

```markdown
### Mở khoá

| Quyền | Điều kiện |
|---|---|
| Được ghi log (vô hình, chỉ RAM) | đã mở khoá **hoặc** bản nội bộ: iOS không cài từ App Store (TestFlight, ad-hoc, Xcode), Android cài qua app **App Tester** của Firebase App Distribution, Editor. APK cài tay / exe **không** tính. |
| Được mở hub | chỉ khi đã mở khoá (PlayerPrefs `DebugHub.AuthenticationState`) |

Mở khoá bằng password, hoặc tự động khi ô password mở mà máy ở **mạng công ty** (`companyNetwork` trên component DebugHub, khớp một dấu hiệu là đủ, kiểm song song — UI không chờ):

- **Pages**: `url` chỉ mở được trong mạng công ty + `mustContain` là chuỗi bắt buộc có trong trang (dùng tiêu đề, không dùng ETag/hash). Không theo redirect.
- **Public Ips**: IP công khai của văn phòng — xem dòng `ip=` ở `https://www.cloudflare.com/cdn-cgi/trace` khi đang ở công ty.

Request chỉ gửi sau cử chỉ bí mật nên người chơi không bao giờ gửi. `http://` cần "Allow downloads over HTTP" = Always. iOS: package tự thêm `NSLocalNetworkUsageDescription` khi build (tester được hỏi quyền mạng cục bộ một lần).

Bong bóng mặc định ẩn, nhớ trạng thái qua phiên; lắc chỉ có tác dụng khi đã mở khoá — reviewer Apple (cài sandbox như TestFlight) không thấy gì.
```

- Mục **Dòng kết quả**: "bấm để mở log window" → "bấm để mở trang log tại vạch của command vừa chạy".
- Mục **Execute**: "Tách tham số bằng parser của IDC (quote/ngoặc giống console). Ô nhập của console chạy được command của hub qua `hub "level.goto 5"`; gõ sai thì console in lý do." → "Tách tham số bằng `DebugValues.SplitArguments`: quote và ngoặc (lồng được) là một đối số."
- Thêm mục mới sau **Panel**:

```markdown
## Trang Log

Nút Log ở header Commands gốc (có số lỗi chưa xem). Bộ ghi giữ tối đa 4 MB chuỗi trong RAM, không ghi file; đầu danh sách luôn nói ghi từ lúc nào và đã bỏ bao nhiêu log cũ.

- Chip Log / Cảnh báo / Lỗi bật tắt từng loại; số đếm là tổng, không đổi theo ô tìm. Chip Gộp (mặc định tắt) gộp log trùng tại vị trí lần đầu.
- Tìm bằng ô tìm của header: khớp nội dung và nơi gọi.
- Mỗi hàng: icon theo loại, 2 dòng nội dung, `giờ – nơi gọi` (frame đầu tiên thuộc code game, bỏ `Debug`/`com.hlight.logging`). Vạch `› lệnh` đánh dấu lúc chạy command.
- Đang ở đáy thì bám theo log mới; cuộn lên thì hiện "N log mới".
- Bấm hàng → trang chi tiết: nội dung đầy đủ, stack từng frame (game sáng, engine mờ), Copy, Trước/Sau.
- `…`: Copy tất cả (theo bộ lọc, quá 500k ký tự giữ phần mới nhất), Xoá (ẩn; bấm dòng đầu danh sách để hiện lại).
```

- Mục **Font**: thêm `– ×` đã có; ghi chú "trang log chỉ dùng thêm icon vector, không thêm glyph".
- Mục **Trần đã biết**: xoá gạch "IDC quét mọi assembly lúc khởi động…"; thêm:
  - "**Log trước khi mở khoá**: phiên mở khoá lần đầu trên máy không phải bản nội bộ chỉ có log từ lúc mở khoá."
  - "**Dấu hiệu mạng công ty** phụ thuộc trang/IP của văn phòng: đổi thì sửa Inspector, theo build kế tiếp."
  - "**Nơi gọi** ở build IL2CPP release chỉ có tên method (không số dòng) trừ khi bật IL2CPP Stacktrace Information có số dòng."

- [ ] **Step 4: Đối chiếu spec** — đọc lại spec §3–§10, tick từng mục đã có trong code/README. Mục chưa làm thì báo user.

- [ ] **Step 5: Chạy test lần cuối** — Expected: `FAIL=0`.

- [ ] **Step 6: Checkpoint cuối** — `git -C Packages/com.hlight.debug-hub status`, tóm tắt thay đổi cho user. Không commit. Nhắc user việc cấu hình `Root.unity` (task 4 Step 8) và 5 điểm phải kiểm trên máy thật (spec §11).
