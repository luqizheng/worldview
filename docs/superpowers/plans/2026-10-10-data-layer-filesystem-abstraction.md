# IFileSystem 抽象 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 抽 `IFileSystem` 抽象, 让 `RecentProjectService` 单元测试不依赖真实文件系统, 3 baseline 失败转绿。

**Architecture:** 新增 1 method 接口 (DirectoryExists), `RecentProjectService` 构造函数加可选 fileSystem 参数 (默认 SystemFileSystem 走 System.IO), 测试用 `FakeFileSystem` 注入。

**Tech Stack:** .NET 8 / C# 12 / xunit.v3 / Godot 4.7.2 mono (仅测试需要)

**Spec:** `D:\kudi-project\world-view\docs\superpowers\specs\2026-10-10-data-layer-filesystem-abstraction.md`

## Global Constraints

- C# 12 命名空间根 `WorldView.{ProjectManagement,...}` (AGENTS.md §6.1)
- 文件用 `public sealed class` 修饰, 接口用 `public interface` (沿用现有模式: `IAppDataLocator`, `IClock`, `IProjectOperations`)
- 默认参数兼容老调用方 (ProducerFactory / EditorController 调 `new RecentProjectService(_locator, _clock)` 不传 fileSystem 走 `SystemFileSystem` 默认)
- 路径比较用 `StringComparison.OrdinalIgnoreCase` (Windows 不区分大小写, 沿用 `RecentProjectService.PathsEqual` 风格)
- 测试断言: `Assert.Single(list)` / `Assert.False/True(list[0].IsMissing)` 沿用 xunit.v3 风格

## Review Focus

- **R1 — Default arg path 走 SystemFileSystem 兜底**: 调用方不传 `fileSystem` 时, `new SystemFileSystem()` 实例的 `DirectoryExists` 必须等价 `System.IO.Directory.Exists` (含网络盘/UNC 路径行为)。覆盖不到就回归。
- **R2 — FakeFileSystem.OrdinalIgnoreCase 匹配**: Windows 上 `C:\ProjA` vs `C:\proja` 应同 key。覆盖不到, 路径去重会失效。
- **R3 — `List_Marks_Missing_Paths` 仍走 SystemFileSystem**: 这个测试不传 FakeFileSystem, 传真的 `C:\does\not\exist`, 必须用真 System.IO 判定 IsMissing=true。覆盖不到, 接口替换破坏了原行为。

---

## File Structure

- `scripts/ProjectManagement/IFileSystem.cs` (Create) — 1 method 接口
- `scripts/ProjectManagement/SystemFileSystem.cs` (Create) — System.IO 实现
- `scripts/ProjectManagement/RecentProjectService.cs` (Modify) — 构造函数加 fileSystem 参数, List() 用 fs.DirectoryExists
- `tests/WorldView.Tests/ProjectManagement/FakeFileSystem.cs` (Create) — 测试用 fake
- `tests/WorldView.Tests/ProjectManagement/RecentProjectServiceTests.cs` (Modify) — 3 测试改注入 FakeFileSystem

---

## Task 1: IFileSystem 接口 + SystemFileSystem 实现 + FakeFileSystem 测试桩

**Files:**
- Create: `scripts/ProjectManagement/IFileSystem.cs`
- Create: `scripts/ProjectManagement/SystemFileSystem.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/FakeFileSystem.cs`
- Test: (无单测, 验证见 Task 2)

**Interfaces:**
- Consumes: (无)
- Produces:
  - `IFileSystem` interface with `bool DirectoryExists(string path)`
  - `SystemFileSystem : IFileSystem` — real impl
  - `FakeFileSystem : IFileSystem` — test stub with `Exists(params string[])` builder method

- [ ] **Step 1: Write `scripts/ProjectManagement/IFileSystem.cs`**

```csharp
namespace WorldView.ProjectManagement;

/// <summary>
/// 文件系统抽象。领域层依赖此接口而非 <c>System.IO</c>, 便于单元测试注入 fake。
/// </summary>
public interface IFileSystem
{
    /// <summary>检查目录是否存在。等价 <c>System.IO.Directory.Exists(string)</c>。</summary>
    bool DirectoryExists(string path);
}
```

- [ ] **Step 2: Write `scripts/ProjectManagement/SystemFileSystem.cs`**

```csharp
using System.IO;

namespace WorldView.ProjectManagement;

/// <summary>走真实文件系统的 <see cref="IFileSystem"/> 实现。生产环境默认。</summary>
public sealed class SystemFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
```

- [ ] **Step 3: Write `tests/WorldView.Tests/ProjectManagement/FakeFileSystem.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace WorldView.Tests.ProjectManagement;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册"存在"路径, 多次调用累积。返回 this 便于链式。</summary>
    public FakeFileSystem Exists(params string[] paths)
    {
        foreach (var p in paths) _existing.Add(p);
        return this;
    }

    public bool DirectoryExists(string path) => _existing.Contains(path);
}
```

- [ ] **Step 4: Build to verify compilation**

Run: `dotnet build WorldView.sln -c Debug --nologo 2>&1 | Select-String "error|0 个错误" | Select-Object -First 3`
Expected: `0 个错误`. 新增 3 文件不破坏编译。

- [ ] **Step 5: Commit**

```bash
cd D:\kudi-project\world-view
git add scripts/ProjectManagement/IFileSystem.cs scripts/ProjectManagement/SystemFileSystem.cs tests/WorldView.Tests/ProjectManagement/FakeFileSystem.cs
git -c core.autocrlf=false commit -m "feat(data): IFileSystem 接口 + SystemFileSystem + FakeFileSystem"
```

---

## Task 2: RecentProjectService 构造函数注入 + 3 测试改

**Files:**
- Modify: `scripts/ProjectManagement/RecentProjectService.cs:71-75` (构造函数签名)
- Modify: `scripts/ProjectManagement/RecentProjectService.cs:105-107` (List() 用 fs.DirectoryExists)
- Modify: `tests/WorldView.Tests/ProjectManagement/RecentProjectServiceTests.cs:31-68` (3 测试注入 FakeFileSystem)

**Interfaces:**
- Consumes: Task 1's `IFileSystem` / `SystemFileSystem` / `FakeFileSystem`
- Produces: `RecentProjectService(IAppDataLocator, IClock, IFileSystem? = null)`

- [ ] **Step 1: Write the failing test**

在 `RecentProjectServiceTests` 改 3 测试, 加 `new FakeFileSystem().Exists(...)`:

```csharp
[Fact]
public void RecordOpened_Appends_New_Entry()
{
    var fs = new FakeFileSystem().Exists(@"C:\projA");
    _sut = new RecentProjectService(_locator, _clock, fs);  // 显式注入
    _sut.RecordOpened(@"C:\projA", "A");
    var list = _sut.List();
    Assert.Single(list);
    Assert.Equal(@"C:\projA", list[0].ProjectRoot);
    Assert.Equal("A", list[0].DisplayName);
    Assert.False(list[0].IsMissing);
}
```

`RecordOpened_Dedupes_By_Path` 和 `RecordOpened_Caps_At_10_Oldest_Evicted` 同样注入对应 1/12 个 fake 路径。

- [ ] **Step 2: Run to verify test fails (RED)**

Run: `dotnet run --project tests/WorldView.Tests -c Debug --no-build -- --filter "WorldView.Tests.ProjectManagement.RecentProjectServiceTests.RecordOpened_Appends_New_Entry" 2>&1 | Select-String "失败|Failed" | Select-Object -First 5`
Expected: 编译失败 (CS1729 "RecentProjectService 不接受 3 个参数") 或测试失败 (Assert.False 期望 False 实际 True)。

- [ ] **Step 3: Modify `RecentProjectService.cs` 构造函数 + List()**

第 71-75 行构造函数改为:
```csharp
public RecentProjectService(IAppDataLocator locator, IClock clock, IFileSystem? fileSystem = null)
{
    _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    _fileSystem = fileSystem ?? new SystemFileSystem();
}
```

新增 private readonly field:
```csharp
private readonly IFileSystem _fileSystem;
```

第 105-107 行 List() 改为:
```csharp
return entries
    .Select(e => e with { IsMissing = !_fileSystem.DirectoryExists(e.ProjectRoot) })
    .ToList();
```

- [ ] **Step 4: Run test to verify it passes (GREEN)**

Run: `dotnet run --project tests/WorldView.Tests -c Debug --no-build -- --filter "WorldView.Tests.ProjectManagement.RecentProjectServiceTests" 2>&1 | Select-String "失败|总计|成功" | Select-Object -First 5`
Expected: `总计: 6  失败: 0  成功: 6` (3 baseline 转绿 + 3 原本绿的仍绿)。

- [ ] **Step 5: Verify R3 (List_Marks_Missing_Paths 走 SystemFileSystem)**

测试 `List_Marks_Missing_Paths` 不传 FakeFileSystem, 仍走默认 SystemFileSystem。Run 同上, 全 6 测试应过。

- [ ] **Step 6: Run full UI test suite to confirm no regression**

Run: `dotnet run --project tests/WorldView.Tests -c Debug --no-build -- --filter "WorldView.Tests.Ui" 2>&1 | Select-String "总计|失败|成功" | Select-Object -First 3`
Expected: 100/100 通过 (UI 测试不动)。

- [ ] **Step 7: Commit**

```bash
cd D:\kudi-project\world-view
git add scripts/ProjectManagement/RecentProjectService.cs tests/WorldView.Tests/ProjectManagement/RecentProjectServiceTests.cs
git -c core.autocrlf=false commit -m "feat(data): RecentProjectService 注入 IFileSystem, 3 baseline 转绿"
```

---

## Self-Review

**1. Spec coverage:**
- §3 接口设计 — Task 1 Step 1 ✅
- §3 SystemFileSystem 默认 — Task 1 Step 2 ✅
- §3 RecentProjectService 构造函数加可选参数 — Task 2 Step 3 ✅
- §4 FakeFileSystem.Exists builder — Task 1 Step 3 ✅
- §4 3 测试改注入 — Task 2 Step 1 ✅
- §5 验收: 0 错 0 警告, 3 baseline 转绿, 25 baseline 不变 — Task 2 Step 4 + 6 ✅

**2. Step scan:** 每个 step 只一个动作 + checkable result, 无 TBD/handle edge cases/appropriate validation。

**3. Type consistency:** `IFileSystem.DirectoryExists(string)` 在 Task 1 定义, Task 2 `_fileSystem.DirectoryExists` 签名匹配。`FakeFileSystem` 内部 `_existing.Contains` 走 `OrdinalIgnoreCase`, 与 `RecentProjectService.PathsEqual` 一致 (R2 覆盖)。

**4. Review Focus:**
- R1 Default arg path — Task 2 Step 5 (List_Marks_Missing_Paths 走默认 SystemFileSystem, 测真 C:\does\not\exist) ✅
- R2 OrdinalIgnoreCase — Task 1 Step 3 (FakeFileSystem 用 StringComparer.OrdinalIgnoreCase) ✅
- R3 List_Marks_Missing_Paths — Task 2 Step 5 ✅

**5. Proportion:** Spec 81 行, Plan 130 行, 比例合理 (Plan 多在 task 步骤)。

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-10-10-data-layer-filesystem-abstraction.md`. Please review the plan. Does it capture what you want, and which approach should we use?
