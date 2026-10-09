# 数据层 IFileSystem 抽象 — RecentProjectService 3 baseline + Storage sandbox 拒路径

> 背景: 26 baseline 失败中, 12 个 `Storage.ProjectLoaderTests` + 5 个 `Storage.ProjectSaverTests` 全因 sandbox 拒盘符路径 (`C:\Users\leo\AppData\Local\Temp\...`)。`ProjectManagement.RecentProjectServiceTests` 因 `Directory.Exists(@"C:\projA")` 在 List() 副作用, 单元测试传 fake 路径 → IsMissing=true → 期望 false → FAIL。
> AGENTS.md §2 原则: "领域层纯逻辑**不引用** `Godot.*` 命名空间, 便于脱离引擎单测"。当前 ProjectManagement 已经不引 Godot, 但**直接引 System.IO**, 一样把"路径是否存在"耦合到了真实文件系统。

## 1. 目标

抽 `IFileSystem` 抽象, 隔离领域层对真实文件系统的依赖, 单元测试可注入 fake fs。

## 2. 范围

- 新增 `scripts/ProjectManagement/IFileSystem.cs` — 6 method 接口
- 新增 `scripts/ProjectManagement/SystemFileSystem.cs` — System.IO 实现
- 改 `RecentProjectService` 注入 IFileSystem, `Directory.Exists` → `fs.DirectoryExists`
- 改 3 个 `RecentProjectServiceTests` 注入 `FakeFileSystem`

**不**做(留给后续轮):
- BackupService / BackupManifest / ProjectWriteCoordinator / DirectoryInspector / ProjectManagementService / SystemAppDataLocator 改造 — 26 baseline 中这 6 处的失败多是 "sandbox 拒 sample_world 路径" 而非逻辑错, 单独处置
- 完整 IFileSystem 抽象 (现在只覆盖 RecentProjectService 用到的 1 个方法)

## 3. 接口设计

```csharp
namespace WorldView.ProjectManagement;

public interface IFileSystem
{
    /// <summary>检查目录是否存在。等价 <see cref="System.IO.Directory.Exists(string)"/>。</summary>
    bool DirectoryExists(string path);
}

public sealed class SystemFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
```

`RecentProjectService` 构造函数加可选参数 `IFileSystem? fileSystem = null`, 默认 `new SystemFileSystem()`, 兼容老调用方 (ProducerFactory / EditorController)。

## 4. 测试设计

新增 `tests/WorldView.Tests/ProjectManagement/FakeFileSystem.cs`:

```csharp
internal sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);
    public FakeFileSystem Exists(params string[] paths) {
        foreach (var p in paths) _existing.Add(p);
        return this;
    }
    public bool DirectoryExists(string path) => _existing.Contains(path);
}
```

`RecentProjectServiceTests` 3 测试改:

| 测试 | 改动 |
|---|---|
| `RecordOpened_Appends_New_Entry` | `new RecentProjectService(_locator, _clock, new FakeFileSystem().Exists(@"C:\projA"))` |
| `RecordOpened_Dedupes_By_Path` | 同上 |
| `RecordOpened_Caps_At_10_Oldest_Evicted` | 用 `Enumerable.Range(0,12).Select(i => $@"C:\proj{i:D2}")` 12 个 path 全 Exists |
| `List_Marks_Missing_Paths` | 不传 FakeFileSystem (默认 SystemFileSystem, 真 C:\does\not\exist 不存在) |
| `RecordOpened_Normalizes_Path` | FakeFileSystem 不需要 Exists (测试不查 IsMissing) |

## 5. 验收

- `dotnet build` 0 错 0 警告
- 3 baseline RecentProjectServiceTests 转绿
- 其他 25 baseline 不变 (本轮不动, 不计回归)
- 全仓 ≥ 321/344 通过 (原 318 + 3)

## 6. 风险

- `ProducerFactory` / `EditorController` 调 `new RecentProjectService(_locator, _clock)` — 不传 fileSystem 走 SystemFileSystem 默认, 无破坏
- IFileSystem 默认 impl 行为 = System.IO, 无行为变化

## 7. 不在范围

- BackupService / ProjectWriteCoordinator 等其他服务的 IFileSystem 改造 (后续轮)
- AGENTS.md §2 原则进一步硬化 (引 Godot 仍禁, 但引 System.IO 是历史债, 不在本 spec 范围)
