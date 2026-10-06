# Báo lỗi và message qua API của project — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng chỗ mở rộng trong `com.hlight.debug-hub` để project viết một class con là QA báo lỗi / gửi message lên API riêng ngay từ hub (chưa viết code gửi thật).

**Architecture:** Hai abstract MonoBehaviour public (`BugReporter`, `MessageSender`) gắn vào hai ô mới trên `DebugHub`. Form của mỗi kênh là `Fields()` của class con + row Gửi, dựng bằng `NodeRenderer` sẵn có trong `FolderNode` `hub.report` / `hub.message`. `SendFlow` (internal, mỗi kênh một bản) lo khoá đang gửi và dòng kết quả; `DebugHub.Watch` chờ `Task` bằng coroutine. Log hub đưa: `LogModel.AllText()` cho báo lỗi, `CopyAll()` cho message đẩy từ trang Log.

**Tech Stack:** Unity 6000.3, C#, uGUI + TextMeshPro, NUnit EditMode, `unity cmd` (Unity CLI).

**Spec:** `Packages/com.hlight.debug-hub/Documentation~/specs/2026-10-05-report-message-design.md` — đọc cùng plan này.

## Global Constraints

- Chỉ sửa trong `Packages/com.hlight.debug-hub`. Gắn component vào DebugHub trong `Root.unity` là việc của user.
- **Không commit.** User tự commit. Mỗi task kết thúc bằng `git status` của package, không `git commit`.
- Package là git repo riêng (`Packages/com.hlight.debug-hub/.git`, nhánh `main`). Trước task đầu: `git -C Packages/com.hlight.debug-hub status -sb` phải sạch; user hay đổi nhánh giữa phiên — kiểm lại trước mỗi task.
- Version **3.1.0** (3.0.0 đã push `origin/main`). Chỉ thêm API, không đổi chữ ký nào đang có.
- Trong `namespace Hlight.Debug.Hub` chữ `Debug` là namespace `Hlight.Debug`: luôn viết `UnityEngine.Debug.Log…`. `Object` viết `using Object = UnityEngine.Object;`.
- Ký tự ngoài chữ cái trong UI chỉ dùng `› ‹ … – — ×`.
- Ngoại lệ có chủ đích của luật "không null-guard `[SerializeField]`": `reporter` / `messenger` trên DebugHub được phép trống (spec §2: trống = kênh không có row).
- EditMode test: `Awake` không chạy khi `Instantiate` prefab → `DebugHub.instance` null trong test; test gọi thẳng method internal.
- Comment theo giọng code hiện có: tiếng Việt, `///` nói *vì sao*.
- Chữ cố định: `"Gửi"`, `"Đang gửi…"`, `"Đang gửi lần trước…"`, `"Đã gửi báo lỗi."`, `"Đã gửi."`, `"Send trả về null."`, `"Kèm {N} log (theo bộ lọc)."`, `"Gửi qua message"`; path `hub.report`, `hub.message`.

## Lệnh dùng chung

Chạy từ root project `C:\Assets\UnityProjects\ig-harvest-jam`. Editor phải đang mở project.

**Build:**

```bash
unity cmd --project-path . recompile
unity cmd --project-path . recompile_status
```

Lặp `recompile_status` tới `completed` hoặc `up_to_date`. `up_to_date` vẫn có thể đã nạp code mới — nghi thì `eval` một symbol vừa thêm.

**Chạy test** (Test Runner thật — tôn trọng `LogAssert`):

```bash
unity cmd --project-path . run_tests --mode EditMode --filter "Hlight.Debug.Hub.Tests" --filter_type assembly
```

Treo thì dùng `AgentTestRunner` (tạo một lần `Temp/debughub/run-tests.cs`):

```csharp
System.Type.GetType("Hlight.Debug.Hub.Tests.AgentTestRunner, Hlight.Debug.Hub.Tests").GetMethod("Run").Invoke(null, null);
return System.IO.File.ReadAllText("Temp/debug-hub-tests.txt");
```

```bash
unity cmd --project-path . --timeout 300 eval_file --file Temp/debughub/run-tests.cs
```

`AgentTestRunner` không có scope cho `LogAssert`: test dùng `LogAssert.Expect` đỏ ở đó dù đúng (đã có sẵn vài test như vậy). So với baseline ở Task 1, không so với 0.

---

## File structure

| File | Trách nhiệm |
|---|---|
| `Scripts/Model/DebugNode.cs` (sửa) | `ValueNode.Options` |
| `Scripts/Model/Node.cs` (sửa) | `Node.Choice` |
| `Scripts/Panel/NodeRenderer.cs` (sửa) | `ValueNode` có `Options` → row chọn |
| `Scripts/Logs/LogModel.cs` (sửa) | `AllText()`: log cho báo lỗi |
| `Scripts/Report/SendFlow.cs` (mới) | một kênh gửi: khoá đang gửi, chạy `Send`, báo kết quả |
| `Scripts/Report/BugReporter.cs` (mới) | public `BugReporter` + `BugReport` |
| `Scripts/Report/MessageSender.cs` (mới) | public `MessageSender` + `DebugMessage` |
| `Scripts/Report/Sending.cs` (mới) | gắn hai kênh, đăng ký `hub.report` / `hub.message`, form, trang message từ log |
| `Scripts/DebugHub.cs` (sửa) | hai ô `reporter` / `messenger`, gọi `Sending.Initialize`, `Watch`, `ShowSent` |
| `Scripts/Pages/LogPage.cs` (sửa) | mục "Gửi qua message" |
| `Tests/Editor/NodeRendererTests.cs`, `LogModelTests.cs` (sửa); `SendFlowTests.cs`, `SendingTests.cs` (mới) | test |
| `README.md`, `CHANGELOG.md`, `package.json` (sửa) | docs, 3.1.0 |

---

### Task 1: `Node.Choice`

**Files:**
- Modify: `Scripts/Model/DebugNode.cs` (class `ValueNode`)
- Modify: `Scripts/Model/Node.cs`
- Modify: `Scripts/Panel/NodeRenderer.cs` (`RenderValue`)
- Test: `Tests/Editor/NodeRendererTests.cs`

**Interfaces:**
- Produces: `public Func<IReadOnlyList<string>> ValueNode.Options;` và `public static ValueNode Node.Choice(string label, Func<string> get, Action<string> set, Func<IReadOnlyList<string>> options, string description = null)`.

- [ ] **Step 1: Baseline**

```bash
git -C Packages/com.hlight.debug-hub status -sb
```

Phải sạch. Chạy test (Lệnh dùng chung), ghi lại số pass/fail và tên test đỏ sẵn làm baseline cho cả plan.

- [ ] **Step 2: Viết test đỏ** — thêm vào cuối class `NodeRendererTests` (trước helper `Render`):

```csharp
        /// Spec ④ §2.2: danh sách lấy lúc dựng trang (người nhận từ API). `$` đầu lựa chọn là dữ liệu, không phải biến.
        [Test]
        public void Choice_IsAPickerRow_AndPickingSetsTheValue()
        {
            var picked = "an";
            var node = Node.Choice("to", () => picked, v => picked = v, () => new[] { "an", "$bình" });
            panel.ShowFromRoot(new DebugPage("form",
                p => NodeRenderer.Render(p, node, (n, values) => NodeRenderer.RunInspect(p, n, values))));

            StringAssert.StartsWith("to:", TestPanel.LabelsOf(panel)[0]);
            TestPanel.ClickRowContaining(panel, "to:");
            TestPanel.ClickRowContaining(panel, "$bình");

            Assert.AreEqual("$bình", picked);
        }

        [Test]
        public void Choice_WhoseOptionsThrow_IsAnErrorRow()
        {
            Render(Node.Choice("to", () => "", _ => { }, () => throw new System.InvalidOperationException("chưa tải")));

            StringAssert.Contains("chưa tải", TestPanel.LabelsOf(panel)[0]);
        }
```

- [ ] **Step 3: Build** — phải lỗi compile `Node.Choice` không có.

- [ ] **Step 4: `ValueNode.Options`** — trong `Scripts/Model/DebugNode.cs`, class `ValueNode`, thêm sau field `Address`:

```csharp
        /// Có thì row là trang chọn trong danh sách này (lấy lúc dựng trang), không phải ô nhập. Xem Node.Choice.
        public Func<IReadOnlyList<string>> Options;
```

- [ ] **Step 5: `Node.Choice`** — trong `Scripts/Model/Node.cs`, thêm sau `Value<T>`:

```csharp
        /// Chọn một chuỗi trong danh sách lấy lúc dựng trang — người nhận, assignee… lấy từ API. Danh sách cố định
        /// trong code thì dùng Value với enum. Cần id thì class gọi tự map từ chuỗi hiển thị.
        public static ValueNode Choice(string label, Func<string> get, Action<string> set,
            Func<IReadOnlyList<string>> options, string description = null)
        {
            var node = Value(label, get, set, description);
            node.Options = options;
            return node;
        }
```

- [ ] **Step 6: Row chọn** — trong `Scripts/Panel/NodeRenderer.cs`, `RenderValue`, chèn ngay sau khối `try { current = node.Get(); } catch …` (trước dòng `var nullIsAValue = …`):

```csharp
            if (node.Options != null && node.Set != null)
            {
                IReadOnlyList<string> options;
                try { options = node.Options(); }
                catch (Exception exception)
                {
                    panel.AddError(label, exception.Unwrap().Message);
                    return;
                }
                // Chạy qua DebugRegistry.Run thì `$` đầu chuỗi là biến; lựa chọn là dữ liệu nên viết `$$`.
                panel.AddChoice(label, options, current as string ?? string.Empty,
                    value => run(node, new[] { value.StartsWith("$") ? "$" + value : value }), node.Description);
                return;
            }
```

- [ ] **Step 7: Build + chạy test** — 2 test mới pass, còn lại như baseline.

- [ ] **Step 8: Checkpoint**

```bash
git -C Packages/com.hlight.debug-hub status --short
```

Chỉ 4 file của task này đổi. Không commit.

---

### Task 2: `LogModel.AllText()`

**Files:**
- Modify: `Scripts/Logs/LogModel.cs` (sau `CopyAll()`)
- Test: `Tests/Editor/LogModelTests.cs`

**Interfaces:**
- Produces: `public string LogModel.AllText()` — mọi entry của `LogRecorder` có `Seq` > mốc Xoá của model, mỗi entry qua `LogModel.Describe`, nối bằng `\n`.

- [ ] **Step 1: Viết test đỏ** — thêm vào `LogModelTests`:

```csharp
        /// Spec ④ §2.1: log của báo lỗi không phụ thuộc chip QA quên tắt, nhưng tôn trọng Xoá ("tính từ đây").
        [Test]
        public void AllText_IgnoresFiltersSearchAndCollapse_ButStartsAfterClear()
        {
            Log("cũ");
            model.Pull();
            model.Clear();
            Log("một"); Log("cảnh-báo", LogType.Warning); Log("một");
            model.Pull();
            model.Toggle(LogGroup.Warning);
            model.Query = "zzz";
            model.Collapse = true;

            var text = model.AllText();

            StringAssert.DoesNotContain("cũ", text);
            StringAssert.Contains("cảnh-báo", text);
            Assert.AreEqual(3, text.Split('\n').Length, "không gộp hai dòng `một`");
        }

        /// Đọc thẳng bộ ghi: không được ăn mất "N log mới" của trang log.
        [Test]
        public void AllText_DoesNotPullIntoTheModel()
        {
            Log("x");
            model.AllText();
            Assert.AreEqual(1, model.Pull());
        }
```

- [ ] **Step 2: Build** — lỗi compile `AllText` không có.

- [ ] **Step 3: Viết `AllText`** — trong `Scripts/Logs/LogModel.cs`, ngay sau method `CopyAll()`:

```csharp
        /// Log cho báo lỗi (spec ④ §2.1): mọi entry còn giữ sau mốc Xoá, bỏ qua lọc / tìm / Gộp — báo lỗi không được
        /// thiếu log vì QA quên tắt một chip. Đọc thẳng bộ ghi, không kéo vào model: kéo ở đây là ăn mất "N log mới".
        public string AllText()
        {
            var entries = new List<LogEntry>();
            LogRecorder.CopySince(clearedAt, entries);
            var blocks = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++) blocks[i] = Describe(new LogItem(entries[i]));
            return string.Join("\n", blocks);
        }
```

- [ ] **Step 4: Build + chạy test** — 2 test mới pass, còn lại như baseline.

- [ ] **Step 5: Checkpoint** — `git -C Packages/com.hlight.debug-hub status --short`: thêm đúng 2 file. Không commit.

---

### Task 3: `SendFlow` + `DebugHub.Watch`

**Files:**
- Create: `Scripts/Report/SendFlow.cs`
- Modify: `Scripts/DebugHub.cs` (thêm `Watch`, `WaitFor` cạnh `Await`; `using System.Threading.Tasks;`)
- Test: `Tests/Editor/SendFlowTests.cs` (mới)

**Interfaces:**
- Produces:
  - `internal sealed class SendFlow { internal SendFlow(string doneText, Action<string, bool> show); internal bool Busy { get; } internal void Start(Func<Task<string>> send); internal void Finish(Task<string> task); }`
  - `internal static void DebugHub.Watch(Task task, Action done)`
- Hợp đồng: `Start` chạy bên trong `Invoke` của row Gửi (tức trong `DebugRegistry.Capture`) — log nó in là dòng kết quả, nó ném = row chạy hỏng (panel không đóng). `Finish` gọi khi Task xong sau đó, báo qua `show(text, error)`.

- [ ] **Step 1: Viết test đỏ** — tạo `Tests/Editor/SendFlowTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hlight.Debug.Hub.Tests
{
    /// Spec ④ §3: một kênh gửi. Bấm qua DebugRegistry.Run như row Gửi thật để thấy đúng dòng kết quả và
    /// row chạy được hay hỏng (hỏng = panel không đóng).
    public class SendFlowTests
    {
        private readonly List<(string text, bool error)> shown = new();
        private SendFlow flow;
        private int sends;

        [SetUp]
        public void SetUp()
        {
            shown.Clear();
            sends = 0;
            flow = new SendFlow("Đã gửi.", (text, error) => shown.Add((text, error)));
        }

        private bool Tap(Func<Task<string>> send, out string message)
        {
            var node = Node.Action("Gửi", () => flow.Start(() =>
            {
                sends++;
                return send();
            }));
            return DebugRegistry.Run(node, Array.Empty<string>(), out message);
        }

        [Test]
        public void AlreadyDone_ShowsTheResultAtOnce()
        {
            Assert.IsTrue(Tap(() => Task.FromResult("BUG-7"), out var message));
            Assert.AreEqual("BUG-7", message);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void EmptyResult_ShowsTheDefaultText()
        {
            Tap(() => Task.FromResult<string>(null), out var message);
            Assert.AreEqual("Đã gửi.", message);
        }

        /// Validate ném trước await đầu tiên ra Task lỗi sẵn: lỗi tại chỗ, form còn mở để sửa.
        [Test]
        public void AlreadyFaulted_FailsTheTap()
        {
            LogAssert.Expect(LogType.Exception, "Exception: chưa chọn assignee");
            Assert.IsFalse(Tap(() => Task.FromException<string>(new Exception("chưa chọn assignee")), out var message));
            Assert.AreEqual("chưa chọn assignee", message);
            Assert.IsFalse(flow.Busy);
        }

        /// Send không async mà ném thẳng: cùng đường với Task lỗi sẵn.
        [Test]
        public void ThrowingSend_FailsTheTap()
        {
            LogAssert.Expect(LogType.Exception, "Exception: không có mạng");
            Assert.IsFalse(Tap(() => throw new Exception("không có mạng"), out var message));
            Assert.AreEqual("không có mạng", message);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void NullTask_FailsTheTap()
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Send trả về null"));
            Assert.IsFalse(Tap(() => null, out _));
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void Pending_IsBusy_AndASecondTapDoesNotSendAgain()
        {
            var pending = new TaskCompletionSource<string>();
            Tap(() => pending.Task, out var first);
            Assert.AreEqual("Đang gửi…", first);
            Assert.IsTrue(flow.Busy);

            Tap(() => pending.Task, out var second);
            Assert.AreEqual(1, sends);
            Assert.AreEqual("Đang gửi lần trước…", second);
        }

        /// EditMode không có hub để chờ Task: gọi Finish thẳng như coroutine của DebugHub.Watch sẽ gọi.
        [Test]
        public void PendingThenDone_ShowsTheResult_AndFreesTheChannel()
        {
            var pending = new TaskCompletionSource<string>();
            Tap(() => pending.Task, out _);
            pending.SetResult("BUG-8");
            flow.Finish(pending.Task);

            Assert.AreEqual(("BUG-8", false), shown[0]);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void PendingThenFaulted_ShowsTheErrorInRed()
        {
            var pending = new TaskCompletionSource<string>();
            Tap(() => pending.Task, out _);
            LogAssert.Expect(LogType.Exception, "Exception: 500");
            pending.SetException(new Exception("500"));
            flow.Finish(pending.Task);

            Assert.AreEqual(("500", true), shown[0]);
            Assert.IsFalse(flow.Busy);
        }
    }
}
```

- [ ] **Step 2: Build** — lỗi compile `SendFlow` không có.

- [ ] **Step 3: Viết `SendFlow`** — tạo `Scripts/Report/SendFlow.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace Hlight.Debug.Hub
{
    /// Một kênh gửi (spec ④ §3): khoá "đang gửi", gọi Send, báo kết quả.
    ///
    /// Start chạy trong Invoke của row Gửi, tức trong DebugRegistry.Capture: log in ra ở đây thành dòng kết quả, ném
    /// ở đây thì row coi là chạy hỏng và panel không đóng — đúng cái cần khi Send validate hỏng ngay (QA sửa ô rồi
    /// gửi lại). Task xong sau đó thì panel đã đóng, chỉ còn dòng kết quả qua `show`.
    internal sealed class SendFlow
    {
        private readonly string doneText;
        private readonly Action<string, bool> show;

        internal SendFlow(string doneText, Action<string, bool> show)
        {
            this.doneText = doneText;
            this.show = show;
        }

        internal bool Busy { get; private set; }

        internal void Start(Func<Task<string>> send)
        {
            if (Busy)
            {
                UnityEngine.Debug.Log("Đang gửi lần trước…");
                return;
            }

            var task = send() ?? throw new InvalidOperationException("Send trả về null.");
            if (task.IsCompleted)
            {
                UnityEngine.Debug.Log(Result(task));
                return;
            }

            Busy = true;
            UnityEngine.Debug.Log("Đang gửi…");
            DebugHub.Watch(task, () => Finish(task));
        }

        internal void Finish(Task<string> task)
        {
            Busy = false;
            string text;
            try { text = Result(task); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                show(exception.Message, true);
                return;
            }
            UnityEngine.Debug.Log(text);
            show(text, false);
        }

        /// GetResult ném đúng exception gốc (không bọc AggregateException), kể cả TaskCanceledException.
        private string Result(Task<string> task)
        {
            var result = task.GetAwaiter().GetResult();
            return string.IsNullOrEmpty(result) ? doneText : result;
        }
    }
}
```

- [ ] **Step 4: `DebugHub.Watch`** — trong `Scripts/DebugHub.cs`: thêm `using System.Threading.Tasks;` vào khối using; thêm ngay sau method `Await(...)`:

```csharp
        /// Chờ Task của một kênh gửi (spec ④ §3): poll IsCompleted mỗi frame trên main thread, **không trần thời gian**
        /// — hết giờ báo lỗi trong khi request vẫn có thể thành công thì QA gửi lại thành issue trùng; timeout là việc
        /// của Send. Không có hub (EditMode) thì không chờ: test gọi SendFlow.Finish thẳng.
        internal static void Watch(Task task, Action done)
        {
            if (instance) instance.StartCoroutine(WaitFor(task, done));
        }

        private static IEnumerator WaitFor(Task task, Action done)
        {
            while (!task.IsCompleted) yield return null;
            done();
        }
```

- [ ] **Step 5: Build + chạy test** — 8 test `SendFlowTests` pass dưới `run_tests` (dưới `AgentTestRunner` 4 test có `LogAssert` đỏ là đúng luật); còn lại như baseline. Kiểm `Scripts/Report.meta` và `Scripts/Report/SendFlow.cs.meta` đã được Unity tạo.

- [ ] **Step 6: Checkpoint** — `git -C Packages/com.hlight.debug-hub status --short`. Không commit.

---

### Task 4: API public + `Sending` + gắn vào DebugHub và trang Log

**Files:**
- Create: `Scripts/Report/BugReporter.cs`, `Scripts/Report/MessageSender.cs`, `Scripts/Report/Sending.cs`
- Modify: `Scripts/DebugHub.cs` (hai field, `Awake`, `ShowSent`)
- Modify: `Scripts/Pages/LogPage.cs` (`Actions`)
- Test: `Tests/Editor/SendingTests.cs` (mới)

**Interfaces:**
- Consumes: `SendFlow` (Task 3), `LogModel.AllText()` (Task 2), `LogModel.CopyAll()`, `LogModel.EntryRowCount`, `DebugHub.AddFolder`, `Node.Action`, `Node.Text`, `.Reports()`, `NodeRenderer.Render/RunInspect`, `LogText.Count`, `DebugHubPanel.Replace`.
- Produces (public): `BugReporter { abstract IEnumerable<DebugNode> Fields(); abstract Task<string> Send(BugReport report); }`, `BugReport(string logs) { string Logs { get; } }`, `MessageSender { abstract IEnumerable<DebugNode> Fields(); abstract Task<string> Send(DebugMessage message); }`, `DebugMessage(string logs) { string Logs { get; } }`.
- Produces (internal): `static class Sending { static bool CanMessage; static void Initialize(Object owner, BugReporter reporter, MessageSender messenger, Action<string, bool> show); static void Reset(); static IEnumerable<DebugNode> ReportNodes(); static IEnumerable<DebugNode> MessageNodes(string logs, int count); static DebugPage ForLogs(LogModel model); }`.

- [ ] **Step 1: Viết test đỏ** — tạo `Tests/Editor/SendingTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub.Tests
{
    /// Spec ④ §2–3: hai kênh gửi của project, form và log hub đưa.
    public class SendingTests
    {
        private class FakeReporter : BugReporter
        {
            public string Title = "";
            public BugReport Last;

            public override IEnumerable<DebugNode> Fields()
            {
                yield return Node.Value("title", () => Title, v => Title = v);
            }

            public override Task<string> Send(BugReport report)
            {
                Last = report;
                return Task.FromResult("BUG-1");
            }
        }

        private class FakeMessenger : MessageSender
        {
            public DebugMessage Last;

            public override IEnumerable<DebugNode> Fields()
            {
                yield return Node.Value<string>("to", () => "qa", null);
            }

            public override Task<string> Send(DebugMessage message)
            {
                Last = message;
                return Task.FromResult<string>(null);
            }
        }

        private GameObject owner;
        private FakeReporter reporter;
        private FakeMessenger messenger;
        private DebugHubPanel panel;

        [SetUp]
        public void SetUp()
        {
            LogRecorder.Reset();
            LogModel.ResetShared();
            owner = new GameObject("sending test");
            reporter = owner.AddComponent<FakeReporter>();
            messenger = owner.AddComponent<FakeMessenger>();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Sending.Reset();
            // Node đăng ký với owner này: owner chết là node rụng khỏi registry.
            Object.DestroyImmediate(owner);
            TestPanel.Destroy(panel);
            LogRecorder.Reset();
            LogModel.ResetShared();
        }

        private static void Show(string text, bool error) { }

        private bool Registered(string path) => DebugRegistry.All.Any(entry => entry.Path == path && entry.Owner == owner);

        private static bool Tap(DebugNode send, out string message) =>
            DebugRegistry.Run(send, Array.Empty<string>(), out message);

        [Test]
        public void OnlyAssignedChannels_GetARow()
        {
            Sending.Initialize(owner, reporter, null, Show);
            Assert.IsTrue(Registered("hub.report"));
            Assert.IsFalse(Registered("hub.message"));
            Assert.IsFalse(Sending.CanMessage);
        }

        [Test]
        public void MessengerAlone_GetsItsRow()
        {
            Sending.Initialize(owner, null, messenger, Show);
            Assert.IsFalse(Registered("hub.report"));
            Assert.IsTrue(Registered("hub.message"));
            Assert.IsTrue(Sending.CanMessage);
        }

        /// Form = Fields() của class con rồi tới Gửi; log chụp lúc bấm Gửi, không phải lúc mở form.
        [Test]
        public void ReportForm_IsFieldsThenSend_AndCarriesLogsAtTap()
        {
            Sending.Initialize(owner, reporter, null, Show);
            LogRecorder.Receive("trước", null, LogType.Log);
            var nodes = Sending.ReportNodes().ToList();
            LogRecorder.Receive("sau", null, LogType.Log);

            Assert.AreEqual("title", nodes[0].Label);
            Assert.AreEqual("Gửi", nodes[nodes.Count - 1].Label);
            Assert.IsTrue(Tap(nodes[nodes.Count - 1], out var message));
            Assert.AreEqual("BUG-1", message);
            StringAssert.Contains("sau", reporter.Last.Logs);
        }

        [Test]
        public void MessageFromCommands_HasNoLogs()
        {
            Sending.Initialize(owner, null, messenger, Show);
            var nodes = Sending.MessageNodes(null, 0).ToList();

            Assert.AreEqual("to", nodes[0].Label);
            Assert.IsTrue(Tap(nodes[nodes.Count - 1], out var message));
            Assert.AreEqual("Đã gửi.", message);
            Assert.IsNull(messenger.Last.Logs);
        }

        [Test]
        public void MessageWithLogs_SaysHowMany()
        {
            Sending.Initialize(owner, null, messenger, Show);
            var first = Sending.MessageNodes("x", 1).First();

            Assert.IsInstanceOf<TextNode>(first);
            StringAssert.Contains("Kèm 1 log (theo bộ lọc).", ((TextNode)first).Text);
        }

        /// Đúng thứ QA đang thấy lúc mở form: theo bộ lọc, log tới sau không kèm.
        [Test]
        public void MessageFromLogPage_CarriesFilteredLogs_CapturedAtOpen()
        {
            Sending.Initialize(owner, null, messenger, Show);
            LogRecorder.Receive("giữ", null, LogType.Log);
            LogRecorder.Receive("cảnh-báo", null, LogType.Warning);
            LogModel.Shared.Pull();
            LogModel.Shared.Toggle(LogGroup.Warning);

            var page = Sending.ForLogs(LogModel.Shared);
            LogRecorder.Receive("tới-sau", null, LogType.Log);
            panel.ShowFromRoot(page);
            TestPanel.ClickRowContaining(panel, "Gửi");

            StringAssert.Contains("giữ", messenger.Last.Logs);
            StringAssert.DoesNotContain("cảnh-báo", messenger.Last.Logs);
            StringAssert.DoesNotContain("tới-sau", messenger.Last.Logs);
        }

        private List<string> LogActions()
        {
            panel.ShowFromRoot(new DebugPage("root", p => p.AddText("root")));
            LogPage.Build().More(panel);
            return TestPanel.LabelsOf(panel);
        }

        [Test]
        public void LogPageAction_NeedsAMessenger()
        {
            Sending.Initialize(owner, reporter, null, Show);
            LogRecorder.Receive("x", null, LogType.Log);
            LogModel.Shared.Pull();
            Assert.IsFalse(LogActions().Any(label => label.StartsWith("Gửi qua message")));
        }

        [Test]
        public void LogPageAction_NeedsAtLeastOneLog()
        {
            Sending.Initialize(owner, null, messenger, Show);
            Assert.IsFalse(LogActions().Any(label => label.StartsWith("Gửi qua message")));

            LogRecorder.Receive("x", null, LogType.Log);
            LogModel.Shared.Pull();
            Assert.IsTrue(LogActions().Any(label => label.StartsWith("Gửi qua message")));
        }

        /// Row Gửi chạy qua RunInspect (node không đăng ký): không thành lệnh của nút repeat.
        [Test]
        public void Sending_IsNotRecordedForRepeat()
        {
            var backup = DebugRegistry.LastCommand;
            DebugRegistry.ClearLastCommand();
            try
            {
                Sending.Initialize(owner, reporter, null, Show);
                var nodes = Sending.ReportNodes().ToList();
                panel.ShowFromRoot(new DebugPage("form", p =>
                {
                    foreach (var node in nodes) NodeRenderer.Render(p, node, (n, values) => NodeRenderer.RunInspect(p, n, values));
                }));
                TestPanel.ClickRowContaining(panel, "Gửi");

                Assert.AreEqual(string.Empty, DebugRegistry.LastCommand);
            }
            finally
            {
                if (backup.Length > 0) PlayerPrefs.SetString("DebugHub.LastCommand", backup);
            }
        }
    }
}
```

- [ ] **Step 2: Build** — lỗi compile `BugReporter`, `Sending`… không có.

- [ ] **Step 3: `BugReporter`** — tạo `Scripts/Report/BugReporter.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Kênh báo lỗi của project (spec ④). Class con đặt trên **chính object DebugHub** (object đó DontDestroyOnLoad)
    /// rồi kéo vào ô Reporter. Abstract MonoBehaviour thay vì interface: như DebuggerAuthenticationTrigger, Inspector
    /// mới vẽ được ô kéo-thả.
    ///
    /// Hub lo form và log. Payload, endpoint, token, đăng nhập, thông tin game/máy là việc của class con.
    public abstract class BugReporter : MonoBehaviour
    {
        /// Mọi ô của form theo thứ tự hiện (title, priority, assignee…). Class con giữ giá trị và tự quyết giữ hay xoá
        /// sau khi gửi. Gọi lại mỗi lần trang dựng lại (kể cả lùi về từ trang chọn): rẻ, không side effect.
        public abstract IEnumerable<DebugNode> Fields();

        /// Đọc field của mình trước `await` đầu tiên: QA mở lại được form trong lúc đang gửi. Trả chữ hiện ở dòng kết
        /// quả (mã issue…), null = "Đã gửi báo lỗi.". Ném = lỗi. Phải tự có timeout: hub không cắt.
        public abstract Task<string> Send(BugReport report);
    }

    /// Thứ chỉ hub có cho một lần gửi. sealed: hub không điền được field nó không biết — payload là class của
    /// project. Thêm field sau này = thêm overload constructor, class con cũ không vỡ.
    public sealed class BugReport
    {
        public BugReport(string logs)
        {
            Logs = logs;
        }

        /// Mọi log đang giữ sau mốc Xoá, bỏ qua lọc / tìm / Gộp, chụp lúc bấm Gửi. Định dạng như Copy tất cả.
        public string Logs { get; }
    }
}
```

- [ ] **Step 4: `MessageSender`** — tạo `Scripts/Report/MessageSender.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Kênh message của project (spec ④): chữ, đẩy log, không file. Gắn như BugReporter, vào ô Messenger.
    public abstract class MessageSender : MonoBehaviour
    {
        /// Mọi ô của form (text, người nhận…). Luật như BugReporter.Fields.
        public abstract IEnumerable<DebugNode> Fields();

        /// Luật như BugReporter.Send; null = "Đã gửi.".
        public abstract Task<string> Send(DebugMessage message);
    }

    /// Thứ chỉ hub có cho một lần gửi message. Luật như BugReport.
    public sealed class DebugMessage
    {
        public DebugMessage(string logs)
        {
            Logs = logs;
        }

        /// Log theo bộ lọc khi đẩy từ trang Log (như Copy tất cả, chụp lúc mở form); null khi gõ tay từ hub.message.
        public string Logs { get; }
    }
}
```

- [ ] **Step 5: `Sending`** — tạo `Scripts/Report/Sending.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Hai kênh gửi của project (spec ④): `hub.report`, `hub.message`, và Log › … › Gửi qua message. DebugHub gắn
    /// lúc Awake; test gắn thẳng. Form = Fields() của class con + row Gửi, dựng bằng NodeRenderer như mọi folder.
    internal static class Sending
    {
        private static BugReporter reporter;
        private static MessageSender messenger;
        private static SendFlow reportFlow;
        private static SendFlow messageFlow;

        internal static bool CanMessage => messenger;

        /// Static giữ nguyên giữa các lần Play khi tắt domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            reporter = null;
            messenger = null;
            reportFlow = null;
            messageFlow = null;
        }

        /// Ô trống = kênh đó không có row (spec ④ §2).
        internal static void Initialize(Object owner, BugReporter reporter, MessageSender messenger, Action<string, bool> show)
        {
            Sending.reporter = reporter;
            Sending.messenger = messenger;
            reportFlow = new SendFlow("Đã gửi báo lỗi.", show);
            messageFlow = new SendFlow("Đã gửi.", show);

            if (reporter) DebugHub.AddFolder(owner, "hub.report", "Báo lỗi lên nền tảng issue của project.", ReportNodes);
            if (messenger) DebugHub.AddFolder(owner, "hub.message", "Gửi message qua API của project.", () => MessageNodes(null, 0));
        }

        /// Log chụp trong lambda của row Gửi, tức lúc bấm, không phải lúc mở form.
        internal static IEnumerable<DebugNode> ReportNodes()
        {
            foreach (var node in reporter.Fields()) yield return node;
            yield return SendNode(() => reportFlow.Start(() => reporter.Send(new BugReport(LogModel.Shared.AllText()))));
        }

        internal static IEnumerable<DebugNode> MessageNodes(string logs, int count)
        {
            if (logs != null) yield return Node.Text($"Kèm {LogText.Count(count)} log (theo bộ lọc).", TextStyle.Note);
            foreach (var node in messenger.Fields()) yield return node;
            yield return SendNode(() => messageFlow.Start(() => messenger.Send(new DebugMessage(logs))));
        }

        /// Log theo bộ lọc chụp lúc mở form: đúng thứ QA đang thấy, log tới sau không kèm.
        internal static DebugPage ForLogs(LogModel model)
        {
            var logs = model.CopyAll();
            var count = model.EntryRowCount;
            return new DebugPage("message", panel =>
            {
                foreach (var node in MessageNodes(logs, count))
                    NodeRenderer.Render(panel, node, (n, values) => NodeRenderer.RunInspect(panel, n, values));
            }, searchable: false);
        }

        /// Reports(): Node.Action mặc định không hiện dòng kết quả, mà "Đang gửi…" phải hiện sau khi panel đóng.
        private static ActionNode SendNode(System.Action send)
        {
            return Node.Action("Gửi", () =>
            {
                // AddField chỉ ghi giá trị lúc onEndEdit: gõ xong bấm Gửi ngay thì bỏ chọn ô trước để giá trị cuối vào
                // class con.
                var events = EventSystem.current;
                if (events) events.SetSelectedGameObject(null);
                send();
            }).Reports();
        }
    }
}
```

- [ ] **Step 6: Gắn vào DebugHub** — trong `Scripts/DebugHub.cs`:

Thêm sau field `autoUnlock`:

```csharp
        [Tooltip("Kênh báo lỗi của project: class con của BugReporter đặt trên chính object này. Trống = không có hub.report.")]
        [SerializeField] private BugReporter reporter;
        [Tooltip("Kênh message của project: class con của MessageSender đặt trên chính object này. Trống = không có hub.message.")]
        [SerializeField] private MessageSender messenger;
```

Trong `Awake`, ngay sau `repeat.Initialize();`:

```csharp
            Sending.Initialize(this, reporter, messenger, ShowSent);
```

Thêm method (cạnh `OpenLog`):

```csharp
        /// Kết quả gửi về sau khi panel đã đóng. Hub đang ẩn (QA ẩn để quay màn hình) thì chỉ còn log, không chèn dòng
        /// chữ vào video.
        private void ShowSent(string text, bool error)
        {
            if (Visible) panel.ShowResult(text, error);
        }
```

- [ ] **Step 7: Mục ở trang Log** — trong `Scripts/Pages/LogPage.cs`, `Actions`, ngay sau khối `panel.AddAction("Copy tất cả", …);`:

```csharp
                if (Sending.CanMessage && model.EntryRowCount > 0)
                {
                    panel.AddAction("Gửi qua message", () => panel.Replace(Sending.ForLogs(model)),
                        "Theo bộ lọc đang bật, kèm vào một message.");
                }
```

- [ ] **Step 8: Build + chạy test** — 9 test `SendingTests` pass; còn lại như baseline. Kiểm `.meta` của 3 file mới đã có.

- [ ] **Step 9: Thử tay trong Editor** (Play, hub đã mở khoá) — cần một class con giả gắn tạm trên object DebugHub của scene đang mở, **không lưu scene**; hỏi user trước khi đụng scene. Kiểm: `hub.report` mở form; gõ ô rồi bấm Gửi ngay → class con nhận giá trị cuối; Send chờ 3 s (`await Task.Delay(3000)`) → "Đang gửi…" rồi kết quả; `hub.hide` trong lúc chờ → kết quả chỉ vào log; Log › … › Gửi qua message mở form có dòng "Kèm N log". Gỡ class con giả, `git status` sạch phía project.

- [ ] **Step 10: Checkpoint** — `git -C Packages/com.hlight.debug-hub status --short`. Không commit.

---

### Task 5: README, CHANGELOG, version

**Files:**
- Modify: `README.md`, `CHANGELOG.md`, `package.json`

- [ ] **Step 1: README — bảng "Row suy từ node"**: chèn ngay trên dòng `| ValueNode | enum không [Flags] có setter | …`:

```markdown
| `ValueNode` | có `Options` (`Node.Choice`) và setter | row chọn trong danh sách lấy lúc dựng trang |
```

- [ ] **Step 2: README — mục mới** chèn ngay trước `## Advanced`:

````markdown
## Báo lỗi và message

Chỗ cho project gửi lên API riêng: **báo lỗi** (tạo issue) và **message** (chữ, đẩy log, không file). Hub lo form và log; endpoint, payload, token, đăng nhập, thông tin game/máy là việc của class con.

1. Viết class con của `BugReporter` và/hoặc `MessageSender`.
2. Thêm component đó vào **chính object DebugHub** (object này `DontDestroyOnLoad`; GameObject khác trong scene đầu bị unload sau boot thì ô mất tham chiếu).
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
        var payload = new IssuePayload { title = title, priority = priority.ToString(), logs = report.Logs,
            version = Application.version };
        // POST payload…
        title = "";
        return "BUG-123";
    }
}
```

| | `hub.report` | `hub.message` | Log › `…` › **Gửi qua message** |
|---|---|---|---|
| Form | `Fields()` + Gửi | `Fields()` + Gửi | "Kèm N log (theo bộ lọc)." + `Fields()` + Gửi |
| Hub đưa | `BugReport.Logs`: mọi log sau mốc Xoá, bỏ qua lọc / tìm / Gộp, chụp lúc bấm Gửi | `DebugMessage.Logs = null` | `DebugMessage.Logs`: theo bộ lọc như Copy tất cả, chụp lúc mở form |

- `Fields()`: mọi ô của form — `Node.Value`, `Node.Choice` (danh sách lấy lúc dựng trang: người nhận, assignee), `Node.Text`. Gọi lại mỗi lần trang dựng lại: rẻ, không side effect. Giá trị do class con giữ, tự quyết giữ hay xoá sau khi gửi.
- `Send`: đọc field trước `await` đầu tiên. Trả chữ cho dòng kết quả (null = "Đã gửi báo lỗi." / "Đã gửi."). Ném = lỗi: hỏng ngay (validate) thì form còn mở; hỏng sau thì dòng kết quả đỏ, mở lại form vẫn còn giá trị. Phải tự có timeout.
- Bấm Gửi: panel đóng, dòng kết quả "Đang gửi…" rồi kết quả. Mỗi kênh một lần gửi một lúc. Không vào nút repeat. Hub đang ẩn thì kết quả chỉ vào log.
- API thêm trường: sửa payload / `Fields()` / `Send` của class con. Package chỉ đổi khi API cần thứ chỉ hub có (ảnh/video) — lúc đó thêm field vào `BugReport`, class con cũ không vỡ.
````

- [ ] **Step 3: README — "Trần đã biết"**: thêm cuối danh sách:

```markdown
- **URL/token của class con gửi nằm trong bản store** (hub cố ý ship bản store): decompile là thấy.
- **`Send` không có timeout** thì kênh đó kẹt "Đang gửi…" tới khi tắt app. Hub cố ý không cắt: hết giờ mà request vẫn thành công thì QA gửi lại thành issue trùng.
- **Form báo lỗi / message chỉ có ô một dòng.**
```

- [ ] **Step 4: CHANGELOG** — chèn ngay dưới `# Changelog`:

```markdown
## 3.1.0

### Thêm
- Báo lỗi và message qua API của project: class con của `BugReporter` / `MessageSender` đặt trên object DebugHub, kéo vào ô **Reporter** / **Messenger**. Form là `Fields()` của class con; hub đưa log (`BugReport.Logs`, `DebugMessage.Logs`). Row `hub.report`, `hub.message`, và Log › … › **Gửi qua message**.
- `Node.Choice`: chọn một chuỗi trong danh sách lấy lúc dựng trang (`ValueNode.Options`).

```

- [ ] **Step 5: `package.json`** — `"version": "3.0.0"` → `"version": "3.1.0"`.

- [ ] **Step 6: Chạy toàn bộ test lần cuối** — so với baseline Task 1: thêm 21 test pass (2 + 2 + 8 + 9), không test cũ nào đổi trạng thái.

- [ ] **Step 7: Checkpoint**

```bash
git -C Packages/com.hlight.debug-hub status --short
git status --short
```

Package: chỉ các file trong bảng File structure (+ `.meta` mới). Repo chính: không file nào của project đổi do plan này (chạy test có thể đổi `ProjectSettings` — xem memory RunAll; nếu đổi thì hoàn tác và báo). Không commit. Báo user: gắn component vào DebugHub trong `Root.unity` là việc của user.
