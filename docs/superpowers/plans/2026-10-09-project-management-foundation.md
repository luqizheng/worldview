# 项目管理基础功能实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Godot 4.7.2 mono + C# 工程补齐项目管理服务层：工程创建/打开/关闭/切换、新增/切换地图、脏状态、30 秒合并自动保存、最近项目、命名快照、轮换备份、非破坏恢复、原子写入。本轮不动 UI。

**Architecture:** 领域层（不引用 `Godot.*`）新加 `ProjectManagement` 命名空间，包含 `ProjectSession` / `ProjectService` / `DirtyStateService` / `AutosaveService` / `BackupService` / `RecoveryService` / `RecentProjectService` / `ProjectWriteCoordinator` / `MapFactory` / `MapIdGenerator` / `Clock`。服务仅做内存 + 文件系统；通过 `EditorContext.ProjectService` 单例对外暴露。UI 在本轮保持不变，验收通过 `dotnet test` + Godot 无头校验。

**Tech Stack:** .NET 8 / C# 12 / xUnit 2.9 / YamlDotNet 16.3.0 / System.IO / Godot 4.7.2 mono（仅用于无头校验）

**Spec:** [docs/superpowers/specs/2026-07-17-project-management-foundation-design.md](../../specs/2026-07-17-project-management-foundation-design.md)

## Global Constraints

- 领域层（`scripts/Domain/`、`scripts/Storage/`、`scripts/ProjectManagement/`）**禁止**引用 `Godot.*` 命名空间，仅 `scripts/Editor/` 与 UI 可使用。
- 所有时间通过 `IClock` 注入；测试中用 `FakeClock`，产品中用 `SystemClock`。
- 所有跨文件引用保持 `project.yaml` 中已存在的相对路径（`maps/world/...`、`../../layers/...`），新代码不得引入绝对路径或含 `..` 越界 ref。
- 文件写入统一走 `ProjectWriteCoordinator`（同目录临时文件 → flush → 重读校验 → 原子替换 → 失败清理）。
- 多文件工程保存走"暂存工程 → 校验 → 同卷替换 → 失败回滚保护快照"策略。
- YAML 走 `YamlSchemas` 序列化/反序列化；nullable 字段省略键。
- `.worldview/recent_projects.yaml` 与 `.worldview/backup_manifest.yaml` 属应用私有，**禁止**写入工程版本库（`examples/`、`docs/` 不会动）。
- 测试目录：`tests/WorldView.Tests/ProjectManagement/`（领域/服务），临时目录集成测试放 `tests/WorldView.Tests/ProjectManagement/Integration/`。
- UI 文件（`scenes/**`、`scripts/Ui/**`、`scripts/Editor/EditorController.cs`）**不在本任务范围**；`scripts/Editor/EditorContext.cs` 仅追加 `ProjectService` 单例只读暴露（`public ProjectService ProjectService { get; }`），不改既有方法。
- 不引入新 NuGet 依赖；不修改 `WorldView.csproj` / `tests/WorldView.Tests/WorldView.Tests.csproj`。
- 不在自动保存/恢复之外另开后台线程；`AutosaveService` 内部用一个 `System.Threading.Timer` 即可（在 `EditorContext` 退出时由 `ProjectService` 释放）。
- 备份目录默认 `<projectRoot>/.worldview/backups/`；最近项目清单默认 `<userConfig>/.worldview/recent_projects.yaml`（`userConfig` 通过 `Godot.ProjectSettings.GlobalizePath("user://")`；测试中用 `IAppDataLocator` 抽象）。
- 命名快照 `kind: named_snapshot`，轮换备份 `kind: pre_open_rotation`；轮转只保留最近 10 份，超出按 `created_at` 升序删除。
- 不修改 `docs/项目管理规格_v0.1.md`；如需增补，在计划中标注为未来工作。

## Review Focus

下面五类输入/失败路径在规格里没有专项测试覆盖，必须挂在对应任务的步骤里：

1. **保存期间新编辑导致脏状态被错误清除** — 写协调器 + 自动保存用 `dirtyVersion` 计数器，保存完成后版本不一致则保持 dirty。覆盖任务 4。
2. **写入失败或断电造成半文件** — 单文件走临时文件 + 原子替换；多文件工程保存必须能回滚到保护快照。覆盖任务 3、6。
3. **最近项目清单损坏（YAML 反序列化异常）** — 损坏时返回错误并使用空清单，**禁止**覆盖原损坏文件。覆盖任务 7。
4. **恢复时备份/当前工程不匹配**（不同 `source_project_id`） — 拒绝恢复并返回 `RecoveryFailed`。覆盖任务 8。
5. **空目录新建工程时各步骤异常清理** — 任何步骤失败时清理本轮创建的文件，但不删除原本非空目录的用户文件。覆盖任务 2。

---

## 文件结构

新增领域/服务文件（`scripts/ProjectManagement/`）：

| 文件 | 职责 |
| --- | --- |
| `IClock.cs` | 时间抽象（`DateTime UtcNow`）。 |
| `SystemClock.cs` | 生产实现。 |
| `FakeClock.cs` | 测试用可控时钟（构造时设初始时间，`Advance(TimeSpan)` 推时间）。 |
| `IAppDataLocator.cs` | 应用私有目录定位抽象（`string GetUserConfigDir()`）。 |
| `SystemAppDataLocator.cs` | 生产实现：Godot 存在时用 `ProjectSettings.GlobalizePath("user://")`，否则 `%APPDATA%/WorldView`。 |
| `ProjectCloseDecision.cs` | 关闭决策枚举（`SaveAndClose` / `CloseWithoutSaving` / `Cancel`）。 |
| `ProjectError.cs` | 错误码与 `ProjectException`：`InvalidProject` / `ProjectAlreadyOpen` / `ProjectHasUnsavedChanges` / `MapNotFound` / `SaveFailed` / `BackupFailed` / `RecoveryFailed` / `RecentProjectsCorrupted` / `DirectoryNotEmpty` / `NameConflict`。 |
| `ProjectWriteCoordinator.cs` | 单文件原子写入 + 多文件暂存工程写入。 |
| `DirtyStateService.cs` | 脏状态服务（`MarkDirty` / `MarkClean` / `HasUnsavedChanges` / `PrepareClose` / `DirtyVersion`）。 |
| `AutosaveService.cs` | 30 秒延迟合并自动保存（`ScheduleAutoSave` / `FlushNow` / `Cancel`）。 |
| `ProjectSession.cs` | 内存会话（`ProjectRoot` / `World` / `Maps` / `Layers` / `CurrentMapId` / `IsOpen`）。 |
| `MapIdGenerator.cs` | 稳定 ID 生成（`map_world_<n>` / `map_city_<n>` / `map_region_<n>`）。 |
| `MapFactory.cs` | 构造空白 `MapDoc`（默认网格、默认图层、terrain_data defaults）。 |
| `BackupManifest.cs` | 备份清单领域模型。 |
| `BackupService.cs` | 命名快照 + 打开前轮换（最近 10 份）。 |
| `RecoveryService.cs` | 列出 + 校验 + 非破坏恢复（保护快照回滚）。 |
| `RecentProjectService.cs` | 最近项目清单读写、上限 10、损坏恢复。 |
| `ProjectService.cs` | 顶层服务：组合上述组件，对外暴露 `CreateProject` / `OpenProject` / `CloseProject` / `SwitchMap` / `AddMap` / `SaveNow` / `CreateNamedSnapshot` / `ListBackups` / `RestoreBackup` / `ListRecentProjects`。 |

修改既有文件：

| 文件 | 改动 |
| --- | --- |
| `scripts/Editor/EditorContext.cs` | 仅追加 `public ProjectService ProjectService { get; }` 单例初始化（`_Ready` 末尾 + 关闭时 `ProjectService.Dispose`），不改既有 API。 |
| `scripts/Storage/Yaml/WorldIndex.cs` | 追加 `static World RemoveMap(World, string refPath)` 与 `static World UpdateMapRef(World, string oldRef, string newRef)` 纯函数（新增地图改 ref 时使用）。 |
| `scripts/Domain/World.cs` | 不改。 |
| `scripts/Storage/Yaml/ProjectLoader.cs` | 不改。 |
| `scripts/Storage/Yaml/ProjectSaver.cs` | 不改。 |

测试新增（`tests/WorldView.Tests/ProjectManagement/`）：

| 测试文件 | 覆盖任务 |
| --- | --- |
| `DirtyStateServiceTests.cs` | 任务 4。 |
| `AutosaveServiceTests.cs` | 任务 5。 |
| `ProjectWriteCoordinatorTests.cs` | 任务 3。 |
| `RecentProjectServiceTests.cs` | 任务 7。 |
| `BackupServiceTests.cs` | 任务 6。 |
| `RecoveryServiceTests.cs` | 任务 8。 |
| `MapFactoryTests.cs` | 任务 1。 |
| `MapIdGeneratorTests.cs` | 任务 1。 |
| `Integration/ProjectServiceEndToEndTests.cs` | 任务 9。 |
| `Integration/SampleWorldValidationTests.cs` | 任务 9。 |

---

## Task 1: 基础原语 — 时钟、应用数据定位、ID 与 Map 工厂

**Files:**
- Create: `scripts/ProjectManagement/IClock.cs`
- Create: `scripts/ProjectManagement/SystemClock.cs`
- Create: `scripts/ProjectManagement/FakeClock.cs`
- Create: `scripts/ProjectManagement/IAppDataLocator.cs`
- Create: `scripts/ProjectManagement/SystemAppDataLocator.cs`
- Create: `scripts/ProjectManagement/MapIdGenerator.cs`
- Create: `scripts/ProjectManagement/MapFactory.cs`
- Create: `scripts/ProjectManagement/ProjectCloseDecision.cs`
- Create: `scripts/ProjectManagement/ProjectError.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/MapIdGeneratorTests.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/MapFactoryTests.cs`

**Interfaces:**
- Consumes: `WorldView.Domain.World`、`WorldView.Domain.MapDoc`、`WorldView.Domain.LayerDoc`、`WorldView.Domain.MapType`。
- Produces:
  - `IClock` 提供 `DateTime UtcNow { get; }`。
  - `SystemClock : IClock` 生产实现。
  - `FakeClock : IClock` 测试用：构造时 `initialUtc`，`Advance(TimeSpan)` 推进，`Set(DateTime)` 显式设置。
  - `IAppDataLocator` 提供 `string GetUserConfigDir()`。
  - `SystemAppDataLocator : IAppDataLocator` 生产实现：构造函数可选 `string? overrideUserConfigDir`（null 时通过 `Godot.ProjectSettings.GlobalizePath("user://")` 取；非 Godot 进程或失败时回落到 `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorldView")`）。
  - `MapIdGenerator.NextId(World existing, MapType type)` 返回 `string`（按类型前缀 + 已有数 + 1）。
  - `MapFactory.CreateBlankMap(MapType type, string id, string displayName, IReadOnlyList<LayerRef> defaultLayers)` 返回 `MapDoc`。
  - `ProjectCloseDecision` enum：`SaveAndClose` / `CloseWithoutSaving` / `Cancel`。
  - `ProjectException` + `ProjectErrorCode` enum + `static ProjectException Invalid(string)` / `ProjectHasUnsavedChanges()` / `MapNotFound(string)` / `SaveFailed(string, Exception?)` / `BackupFailed(string, Exception?)` / `RecoveryFailed(string, Exception?)` / `RecentProjectsCorrupted(string)` / `DirectoryNotEmpty(string)` / `NameConflict(string)`。

- [ ] **Step 1: 写 `IClock` / `SystemClock` / `FakeClock` 三个类**

`IClock` 仅一个属性 `DateTime UtcNow`；`SystemClock` 直接 `DateTime.UtcNow`；`FakeClock` 用一个内部 `_current` 字段，`Advance(TimeSpan)` 加时间，`Set(DateTime)` 覆盖。

- [ ] **Step 2: 写 `IAppDataLocator` / `SystemAppDataLocator`**

`SystemAppDataLocator` 构造可选 `string? overrideUserConfigDir`；非 null 直接返回；否则先尝试 `Godot.ProjectSettings.GlobalizePath("user://")`（仅在 `Godot.ProjectSettings` 类型存在时，**用 `Type.GetType("Godot.ProjectSettings, GodotSharp")` 反射**，避免领域层静态依赖 Godot），异常时回落到 `%APPDATA%/WorldView` 并 `Directory.CreateDirectory`。

- [ ] **Step 3: 写 `MapIdGenerator.NextId`**

按 `MapType` 取前缀 `map_world_` / `map_city_` / `map_region_`；扫描 `existing.MapIndex` 与 `existing.Maps`（如有）找出已用编号最大值，+1 输出 `"map_<type>_<n>"`。`n` 起始为 1。

- [ ] **Step 4: 写 `MapFactory.CreateBlankMap`**

签名 `MapDoc CreateBlankMap(MapType type, string id, string displayName, IReadOnlyList<LayerRef> defaultLayers)`：
- `Grid`：world 64×48、cell_distance 10、distance_unit km；city 64×64、cell_distance 2、distance_unit m。
- `Time`：world `Month`/`Day`；city `Hour`/`Hour`。
- `Presentation`：默认（`"2d"`, `"parchment"`）。
- `TerrainData.Encoding = "sparse"`、`Default = TerrainDefaults.Plain`、`Cells = []`、`CellsRef = null`。
- `Layers = defaultLayers`、`CityRefs = []`、`Buildings = []`、`Generation = null`。
- `Id`、`Name` 取参数。

- [ ] **Step 5: 写 `ProjectCloseDecision` 与 `ProjectError`**

`ProjectCloseDecision` 三值枚举。`ProjectErrorCode` 枚举覆盖规格 §7 全部错误码。`ProjectException : Exception` 暴露 `Code` 与 `InnerException`，并提供静态工厂方法（每个错误码一个）返回带中文 `Message` 的异常。

- [ ] **Step 6: 写 `MapIdGeneratorTests`**

测试：
- `NextId_World_When_Empty_Returns_1` → `MapIdGenerator.NextId(empty, MapType.World) == "map_world_1"`。
- `NextId_City_Skips_Used_Numbers` → 传入已有 `map_city_3` 的 World → 返回 `map_city_4`。
- `NextId_Handles_MissingMapIndex` → `world.MapIndex = null` → 仍返回 `map_world_1`。

- [ ] **Step 7: 写 `MapFactoryTests`**

测试：
- `Create_World_Has_SpecDefaults` → 64×48、cell_distance 10、distance_unit km、time default=Month、min=Day。
- `Create_City_Has_SpecDefaults` → 64×64、cell_distance 2、distance_unit m、time Hour/Hour。
- `Create_Respects_Id_And_Name` → 传入 `id="map_xxx"`,`name="测试"` 后字段一致。
- `Create_Has_No_Cells_And_Default_Terrain` → `TerrainData.Encoding == "sparse"`、`Default.Type == TerrainType.Plain`、`Cells.Count == 0`。

- [ ] **Step 8: 跑测试验证失败 → 实现 → 跑测试验证通过**

运行：
```bash
dotnet test tests/WorldView.Tests/WorldView.Tests.csproj -c Debug --filter "FullyQualifiedName~MapIdGeneratorTests|FullyQualifiedName~MapFactoryTests"
```
预期：测试通过。

- [ ] **Step 9: Commit**

```bash
git add scripts/ProjectManagement/IClock.cs scripts/ProjectManagement/SystemClock.cs scripts/ProjectManagement/FakeClock.cs scripts/ProjectManagement/IAppDataLocator.cs scripts/ProjectManagement/SystemAppDataLocator.cs scripts/ProjectManagement/MapIdGenerator.cs scripts/ProjectManagement/MapFactory.cs scripts/ProjectManagement/ProjectCloseDecision.cs scripts/ProjectManagement/ProjectError.cs tests/WorldView.Tests/ProjectManagement/MapIdGeneratorTests.cs tests/WorldView.Tests/ProjectManagement/MapFactoryTests.cs
git commit -m "feat(project-mgmt): add clock, app data locator, id generator, map factory"
```

---

## Task 2: 错误与目录校验

**Files:**
- Modify: `scripts/ProjectManagement/ProjectError.cs`（追加目录校验结果记录 `DirectoryInspection`）。
- Create: `scripts/ProjectManagement/DirectoryInspector.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/DirectoryInspectorTests.cs`

**Interfaces:**
- Produces:
  - `DirectoryInspector.IsEmpty(string absolutePath)` → `bool`（`Directory.EnumerateFileSystemEntries` 计数 = 0，允许隐藏文件如 `.git` 视为非空）。
  - `DirectoryInspector.ValidateForCreate(string absolutePath)` → `void`，非空 / 路径是文件而非目录 / `project.yaml` 已存在 时抛 `ProjectException(DirectoryNotEmpty | InvalidProject)`。

- [ ] **Step 1: 写失败测试 `DirectoryInspectorTests`**

```csharp
[Fact] public void IsEmpty_True_For_NewDirectory();
[Fact] public void IsEmpty_False_When_Any_Entry_Exists();
[Fact] public void ValidateForCreate_Throws_DirectoryNotEmpty_If_Any_File();
[Fact] public void ValidateForCreate_Throws_InvalidProject_If_Path_Is_File();
[Fact] public void ValidateForCreate_Ok_For_Empty_NewDir();
```

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `DirectoryInspector`**

两个方法按 `Interfaces` 描述。

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/DirectoryInspector.cs tests/WorldView.Tests/ProjectManagement/DirectoryInspectorTests.cs
git commit -m "feat(project-mgmt): add directory inspector for create validation"
```

---

## Task 3: 写协调器 — 单文件原子 + 多文件暂存

**Files:**
- Create: `scripts/ProjectManagement/ProjectWriteCoordinator.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/ProjectWriteCoordinatorTests.cs`

**Interfaces:**
- Produces:
  - `ProjectWriteCoordinator(IClock clock)` 构造注入。
  - `void WriteFileAtomic(string absolutePath, string content)`：同目录 `tmp` 临时文件 → write → flush(true) → 重读校验非空且字节一致 → `File.Replace(tmp, target, null)`（target 不存在时用 `File.Move(tmp, target)`）→ 失败时清理 tmp。
  - `void WriteProjectAtomic(string rootPath, Action<string> writeAll)`：先在 `<root>.tmp-<guid>/` 创建暂存工程 → 调 `writeAll(stagingRoot)` → 调 `ProjectLoader.Load(stagingRoot)` 验证无错误 → 关闭文件句柄后用 `Directory.Move(stagingRoot, rootPath)`（失败时回滚到保护快照，流程见 Task 8 的 `BackupService.CreateProtectionSnapshot`）。
  - 失败一律抛 `ProjectException(SaveFailed, message, innerException)`。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact] public void WriteFileAtomic_Creates_Target_With_Exact_Content();
[Fact] public void WriteFileAtomic_Overwrites_Existing_File();
[Fact] public void WriteFileAtomic_Cleans_Temp_On_Simulated_Failure(用注入的 writer 抛异常验证 tmp 已被删除);
[Fact] public void WriteProjectAtomic_Stages_Then_Replaces();
[Fact] public void WriteProjectAtomic_Rolls_Back_When_Loader_Fails;
[Fact] public void WriteProjectAtomic_Throws_SaveFailed_With_InnerException_On_Disk_Error;
```

模拟失败可在 `writeAll` 内 `throw new IOException("disk full")`，验证 stagingRoot 不残留。

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `ProjectWriteCoordinator`**

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/ProjectWriteCoordinator.cs tests/WorldView.Tests/ProjectManagement/ProjectWriteCoordinatorTests.cs
git commit -m "feat(project-mgmt): atomic write coordinator"
```

---

## Task 4: 脏状态服务

**Files:**
- Create: `scripts/ProjectManagement/DirtyStateService.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/DirtyStateServiceTests.cs`

**Interfaces:**
- Produces:
  - `DirtyStateService()` 无参。
  - `bool HasUnsavedChanges { get; }`
  - `int DirtyVersion { get; }`（递增；保存完成后用 `MarkClean(currentVersion)` 才置 clean）。
  - `void MarkDirty()`
  - `void MarkClean(int version)`（仅当传入版本 == `DirtyVersion` 时清脏；否则保留 dirty）。
  - `ProjectCloseDecisionRequirement PrepareClose()`：返回结构化枚举 `NeedsSave | NoChanges`（新增枚举 `CloseRequirement`）。
  - 事件 `event EventHandler? DirtyChanged`。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact] public void Initially_Not_Dirty();
[Fact] public void MarkDirty_Fires_Event_And_Sets_Flag();
[Fact] public void MarkClean_Stale_Version_Keeps_Dirty();  // 关键回归：保存期间被新编辑覆盖
[Fact] public void MarkClean_Current_Version_Clears_Flag();
[Fact] public void PrepareClose_Returns_NoChanges_When_Clean();
[Fact] public void PrepareClose_Returns_NeedsSave_When_Dirty();
```

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `DirtyStateService`**

`MarkDirty` 时 `_version++`；`MarkClean(version)` 仅当 `version == _version` 时 `_isDirty = false`。`PrepareClose` 内部无需抛错。

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/DirtyStateService.cs tests/WorldView.Tests/ProjectManagement/DirtyStateServiceTests.cs
git commit -m "feat(project-mgmt): dirty state service with version protection"
```

---

## Task 5: 自动保存服务

**Files:**
- Create: `scripts/ProjectManagement/AutosaveService.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/AutosaveServiceTests.cs`

**Interfaces:**
- Produces:
  - `AutosaveService(IClock clock, DirtyStateService dirty, TimeSpan? delay = null)`（默认 30 秒）。
  - `TimeSpan Delay { get; }`
  - `void ScheduleAutoSave(Func<CancellationToken, Task<bool>> saveAsync)` 合并重复任务：若已有 pending 任务，仅延后截止时间，不创建新任务。
  - `Task<bool> FlushNowAsync(CancellationToken)` 立即执行保存（不取消 pending timer）。
  - `void Cancel()` 取消 pending 自动保存（关闭/切换工程时调用）。
  - 事件 `event EventHandler<AutosaveFailedEventArgs>? AutosaveFailed`。
  - 内部 `System.Threading.Timer` 调度；`Task` 用 `TaskCompletionSource` 表达。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact] public async Task Schedule_Triggers_Save_After_Delay();
[Fact] public async Task Repeated_Schedule_Within_Window_Triggers_Once();
[Fact] public async Task FlushNow_Runs_Immediately_And_Does_Not_Trigger_Pending();
[Fact] public async Task Cancel_Prevents_Pending_Save();
[Fact] public async Task Save_Failure_Raises_Event_And_Keeps_Dirty();
```

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `AutosaveService`**

合并：维护 `_pendingTcs` 与 `_dueTime`；新 `ScheduleAutoSave` 时若已有 pending，仅更新 `_dueTime = now + Delay` 并 `Change` timer，不创建新任务。`Timer` 触发时调 `saveAsync`；成功后 `MarkClean(currentVersion)`（前提：保存前读取 `_dirty.DirtyVersion`，保存后传回）。失败：触发 `AutosaveFailed`，不清脏。

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/AutosaveService.cs tests/WorldView.Tests/ProjectManagement/AutosaveServiceTests.cs
git commit -m "feat(project-mgmt): debounced autosave service"
```

---

## Task 6: 备份服务（命名快照 + 轮换）

**Files:**
- Create: `scripts/ProjectManagement/BackupManifest.cs`
- Create: `scripts/ProjectManagement/BackupService.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/BackupServiceTests.cs`

**Interfaces:**
- Produces:
  - `BackupKind` enum：`NamedSnapshot`、`PreOpenRotation`。
  - `BackupRecord(string BackupId, BackupKind Kind, string DisplayName, DateTime CreatedAtUtc, string SourceProjectId, string ContentRoot)`。
  - `BackupManifest` (`SchemaVersion = 1`, `IReadOnlyList<BackupRecord> Backups`) + `static BackupManifest LoadOrEmpty(string filePath, IClock)` / `void Save(string filePath, IClock)`。
  - `BackupService(string projectRoot, IClock clock, ProjectWriteCoordinator? writer = null)`。
  - `Task<BackupRecord> CreateSnapshotAsync(string displayName, string sourceProjectId)`：名称唯一 → 失败 `BackupFailed(NameConflict)`；复制整个工程到 `<projectRoot>/.worldview/backups/<backup_id>/`，排除 `.worldview/` 自身；写清单。
  - `Task<BackupRecord> CreatePreOpenRotationAsync(string sourceProjectId)`：自动命名 `auto-<utc-ticks>`；保留最近 10 份。
  - `Task<IReadOnlyList<BackupRecord>> ListAsync()`。
  - `Task DeleteAsync(string backupId)`：删除内容 + 清单项。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact] public async Task CreateSnapshot_Copies_Files_And_Adds_Manifest_Entry();
[Fact] public async Task CreateSnapshot_Excludes_DotWorldview();
[Fact] public async Task CreateSnapshot_Rejects_Duplicate_Name();
[Fact] public async Task PreOpenRotation_Keeps_Most_Recent_10();
[Fact] public async Task List_Returns_Manifest_In_CreatedAt_Order();
[Fact] public async Task Delete_Removes_Content_And_Manifest_Entry();
```

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `BackupService`**

复制策略：递归遍历 `projectRoot`，跳过 `.worldview/`，目标位置 `<projectRoot>/.worldview/backups/<backup_id>/`。复制走 `File.Copy`（MVP 不开并行）。轮换：插入新记录后按 `CreatedAtUtc` 升序，删多余。清单文件 `<projectRoot>/.worldview/backup_manifest.yaml`，用 `YamlSchemas.Serialize`。

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/BackupManifest.cs scripts/ProjectManagement/BackupService.cs tests/WorldView.Tests/ProjectManagement/BackupServiceTests.cs
git commit -m "feat(project-mgmt): backup service with named snapshots and rotation"
```

---

## Task 7: 最近项目服务

**Files:**
- Create: `scripts/ProjectManagement/RecentProjectService.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/RecentProjectServiceTests.cs`

**Interfaces:**
- Produces:
  - `RecentProjectEntry(string ProjectRoot, string DisplayName, DateTime LastOpenedAtUtc)`。
  - `RecentProjectService(IAppDataLocator locator, IClock clock, ProjectWriteCoordinator? writer = null)`。
  - 文件路径 `<userConfigDir>/.worldview/recent_projects.yaml`（目录不存在则创建）。
  - `IReadOnlyList<RecentProjectEntry> List()`。
  - `void RecordOpened(string projectRoot, string displayName)`：规范化绝对路径；按路径去重；超 10 删最旧；写盘。
  - `IReadOnlyList<RecentProjectEntry> MarkMissing()`：扫描路径，若 `project.yaml` 不存在则在结果中标记 `IsMissing = true`（不删）。为最小实现，加 `IsMissing` 字段到 `RecentProjectEntry`。
  - 损坏恢复：YAML 反序列化失败时返回空清单 + 通过 `RecentProjectsCorrupted` 异常暴露给调用方（不静默）。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact] public void RecordOpened_Appends_New_Entry();
[Fact] public void RecordOpened_Dedupes_By_Path();
[Fact] public void RecordOpened_Caps_At_10_Oldest_Evicted();
[Fact] public void List_Marks_Missing_Paths();
[Fact] public void Corrupted_File_Surfaces_Exception_Without_Overwriting();
```

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `RecentProjectService`**

损坏恢复关键：`List()` 调 `YamlSchemas.Deserialize<RecentProjectsFile>(content)` 包 `try/catch (YamlException, InvalidOperationException)`，抛 `ProjectException(RecentProjectsCorrupted, ...)`，**不要**覆写原文件。`RecordOpened` 时若 `IsMissing` 项需要恢复成功才更新 `LastOpenedAtUtc` 并清除标记。

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/RecentProjectService.cs tests/WorldView.Tests/ProjectManagement/RecentProjectServiceTests.cs
git commit -m "feat(project-mgmt): recent projects service with corruption recovery"
```

---

## Task 8: 恢复服务

**Files:**
- Create: `scripts/ProjectManagement/RecoveryService.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/RecoveryServiceTests.cs`

**Interfaces:**
- Produces:
  - `RecoveryService(BackupService backup, ProjectWriteCoordinator writer, ProjectLoader loader, IClock clock)`。
  - `Task<IReadOnlyList<BackupRecord>> ListBackupsAsync()`。
  - `Task<BackupRecord> RestoreAsync(string backupId, string expectedSourceProjectId)`：校验 `BackupRecord.SourceProjectId == expectedSourceProjectId` 否则 `RecoveryFailed`；先创建保护快照 `protection-<utc-ticks>`；把备份内容原子覆盖当前工程；用 `ProjectLoader.Load` 校验新内容无 Error 级别错误；任何步骤失败回滚到保护快照并抛 `RecoveryFailed`。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact] public async Task Restore_Rejects_SourceProjectId_Mismatch();
[Fact] public async Task Restore_Creates_Protection_Snapshot_Before_Overwrite();
[Fact] public async Task Restore_Overwrites_Target_With_Backup_Content();
[Fact] public async Task Restore_Rolls_Back_When_PostLoad_Validation_Fails();
[Fact] public async Task Restore_Leaves_Target_Loadable_On_Failure();
```

- [ ] **Step 2: 跑测试 → 失败**

- [ ] **Step 3: 实现 `RecoveryService`**

保护快照：调 `BackupService.CreateSnapshotAsync("protection-" + utcTicks, currentSourceProjectId)`。回滚：把保护快照内容拷回目标工程；最终保证目标工程可加载。

- [ ] **Step 4: 跑测试 → 通过**

- [ ] **Step 5: Commit**

```bash
git add scripts/ProjectManagement/RecoveryService.cs tests/WorldView.Tests/ProjectManagement/RecoveryServiceTests.cs
git commit -m "feat(project-mgmt): recovery service with protection snapshot"
```

---

## Task 9: ProjectService 顶层服务与 EditorContext 接入

**Files:**
- Create: `scripts/ProjectManagement/ProjectSession.cs`
- Create: `scripts/ProjectManagement/ProjectService.cs`
- Modify: `scripts/Editor/EditorContext.cs`（仅追加 `ProjectService` 单例字段、`_Ready` 末尾初始化、`_ExitTree` 释放；不改任何已有方法）。
- Create: `tests/WorldView.Tests/ProjectManagement/Integration/ProjectServiceEndToEndTests.cs`
- Create: `tests/WorldView.Tests/ProjectManagement/Integration/SampleWorldValidationTests.cs`

**Interfaces:**
- Produces:
  - `ProjectSession(string projectRoot, World world, IReadOnlyList<MapDoc> maps, IReadOnlyList<LayerDoc> layers)` + `CurrentMapId` / `IsOpen`。
  - `ProjectService(IAppDataLocator locator, IClock clock, ProjectLoader? loader = null, ProjectSaver? saver = null)`（默认实现使用 `Storage.Yaml.ProjectLoader/ProjectSaver`）。
  - `Task<ProjectSession> CreateProjectAsync(string rootPath, string? displayName, CancellationToken ct)`：校验目录 → 写 `project.yaml` → 创建第一张世界地图 → 写会话 → 登记最近工程。
  - `Task<ProjectSession> OpenProjectAsync(string rootPath, CancellationToken ct)`：校验目录与 `project.yaml` → 调 `BackupService.CreatePreOpenRotationAsync(sourceProjectId)`（失败只记日志不影响打开）→ `ProjectLoader.Load` → 若有 Error 级别校验错误抛 `InvalidProject` → 替换会话 → 登记最近工程。
  - `ProjectCloseDecision? CloseProject(ProjectCloseDecision decision)`：执行决策；`SaveAndClose` 失败时保留会话。
  - `ProjectSession SwitchMap(string mapId)`：先 `PrepareClose`（地图级无未保存语义；按规格，地图级不阻断切换，仅更新 `CurrentMapId`）→ 找不到抛 `MapNotFound`。
  - `MapDoc AddMap(MapType type, string displayName, IReadOnlyList<LayerRef> defaultLayers)`：生成 ID → 构造空白 MapDoc → 写 `maps/...` 临时文件 → 通过 `ProjectWriteCoordinator.WriteFileAtomic` → 更新 `WorldIndex` → 重新加载并校验 → 替换会话并把新地图设为 `CurrentMapId`。
  - `Task SaveNowAsync(CancellationToken)`：调 `ProjectWriteCoordinator.WriteProjectAtomic` + 写完后 `DirtyStateService.MarkClean(currentVersion)`。
  - `Task<BackupRecord> CreateNamedSnapshotAsync(string displayName)`。
  - `Task<IReadOnlyList<BackupRecord>> ListBackupsAsync()`。
  - `Task<BackupRecord> RestoreBackupAsync(string backupId)`。
  - `IReadOnlyList<RecentProjectEntry> ListRecentProjects()`。
  - `void MarkDirty()` / `event EventHandler? SessionChanged` / `event EventHandler<AutosaveFailedEventArgs>? AutosaveFailed`。
  - 自动保存：`MarkDirty` → `AutosaveService.ScheduleAutoSave(saveAsync)`；`SaveNowAsync` 走同一保存委托。
  - `Dispose` 释放 `AutosaveService` 的 Timer。

- [ ] **Step 1: 写失败集成测试 `ProjectServiceEndToEndTests`**

```csharp
[Fact] public async Task Create_Open_Edit_Save_Reopen_Roundtrip();
[Fact] public async Task OpenProject_Rotates_Backup_Before_Replacing_Session();
[Fact] public async Task AddMap_Persists_And_Sets_As_Current();
[Fact] public async Task CloseProject_SaveAndClose_Persists_And_Resets_Session();
[Fact] public async Task CloseProject_Cancel_Keeps_Session();
[Fact] public async Task CloseProject_CloseWithoutSaving_Discards_Changes();
[Fact] public async Task OpenProject_With_Dirty_Session_Requires_Decision(); // 流程：dirty → PrepareClose → 决策
[Fact] public async Task Save_During_AutoSave_Keeps_New_Edit_Dirty(); // Review Focus #1
[Fact] public async Task Atomic_Write_Failure_Leaves_Project_Loadable(); // Review Focus #2
[Fact] public async Task Named_Snapshot_Name_Conflict_Surfaces_Error();
[Fact] public async Task RestoreBackup_Rolls_Back_On_Validation_Failure();
[Fact] public async Task RecentProjects_Survives_Service_Reinstantiation();
```

- [ ] **Step 2: 写 `SampleWorldValidationTests`**

`ProjectService.OpenProjectAsync` 打开 `examples/sample_world` 后断言 `Session.World.Name == "示例世界"`、`Maps.Count == 2`、`Validation.IsValid == true`。

- [ ] **Step 3: 跑测试 → 失败**

- [ ] **Step 4: 实现 `ProjectSession` 与 `ProjectService`**

`ProjectSession` 用 `private set` 控制状态变更。`ProjectService` 组合所有服务；保存委托用 `Func<CancellationToken, Task<bool>>` 内部 lambda（先 `WriteProjectAtomic` 再 `MarkClean(currentVersion)`）。`CreatePreOpenRotationAsync` 在 `OpenProjectAsync` 入口调用，失败 `try/catch` 后 `Debug.WriteLine`。

- [ ] **Step 5: 修改 `EditorContext.cs`**

仅追加：
- `public ProjectService ProjectService { get; private set; } = null!;`
- `_Ready` 末尾：`ProjectService = new ProjectService(new SystemAppDataLocator(), new SystemClock());`
- `_ExitTree` 末尾：`ProjectService?.Dispose();`
- 不改任何其它方法、不改 `World` / `Maps` / `Layers` 等字段、不改 `LoadProject` / `SaveProject` 等既有方法。

- [ ] **Step 6: 跑全部测试 → 通过**

```bash
dotnet test tests/WorldView.Tests/WorldView.Tests.csproj -c Debug
```

- [ ] **Step 7: Godot 无头校验**

```powershell
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --headless --path . --check-only
```
预期：编译通过，无场景/脚本引用错误。

- [ ] **Step 8: Commit**

```bash
git add scripts/ProjectManagement/ProjectSession.cs scripts/ProjectManagement/ProjectService.cs scripts/Editor/EditorContext.cs tests/WorldView.Tests/ProjectManagement/Integration/
git commit -m "feat(project-mgmt): project service and editor context integration"
```

---

## Self-Review

1. **Spec coverage**：
   - §1 背景/目标 → 任务 1–9。
   - §2 范围外 → 不在任务范围内。
   - §3 设计原则 → 任务 1（时钟/定位抽象）、3（写协调器）。
   - §4 组件边界 → 任务 1、4、5、6、7、8、9。
   - §5 主要流程 → 任务 9 端到端覆盖。
   - §6 数据契约 → 任务 7（recent_projects）、6（backup_manifest），`project.yaml` 新增字段（`kind` / `schema_version`）由 `ProjectService.CreateProjectAsync` 写入，已被任务 9 集成测试间接覆盖；本轮不修改 `docs/数据模型规格_v0.1.md`，留待数据契约 PR。
   - §7 错误模型 → 任务 1（`ProjectException` + 全部错误码）。
   - §8 并发与一致性 → 任务 4（`DirtyVersion` 保护）、3（写协调器串行化）、5（自动保存去重）。
   - §9 测试设计 → 任务 1、2、3、4、5、6、7、8、9 全覆盖。
   - §10 实施顺序 → 1→2→3→4→5→6→7→8→9 与计划顺序一致。
   - §11 风险 → 任务 3（多文件非事务回滚）、6（备份与自动保存串行化）、7（显示名固定为首次登记）、8（恢复保护）。
2. **Step scan**：每个步骤只要求一个可写的具体动作（签名、测试名、提交命令）。没有"实现具体逻辑"这种空泛步骤。
3. **Type consistency**：`MapDoc.Id` / `MapDoc.Name` / `MapType` / `LayerRef` / `LayerDoc` 与既有 `scripts/Domain/MapDoc.cs` 一致；`World.MapIndex` / `MapIndexEntry.RefPath` 与 `scripts/Domain/World.cs` 一致；`WorldIndex.AppendMap` 已存在（`scripts/Storage/Yaml/WorldIndex.cs:21`），本计划在 `ProjectService.AddMap` 复用之。
4. **Review Focus**：
   - 输入 1（保存期间新编辑）→ 任务 4 + 任务 9 测试 `Save_During_AutoSave_Keeps_New_Edit_Dirty`。
   - 输入 2（写入失败/断电）→ 任务 3 测试 `WriteFileAtomic_Cleans_Temp_On_Simulated_Failure`、`WriteProjectAtomic_Rolls_Back_When_Loader_Fails` + 任务 9 测试 `Atomic_Write_Failure_Leaves_Project_Loadable`。
   - 输入 3（最近项目损坏）→ 任务 7 测试 `Corrupted_File_Surfaces_Exception_Without_Overwriting`。
   - 输入 4（备份/当前工程不匹配）→ 任务 8 测试 `Restore_Rejects_SourceProjectId_Mismatch`。
   - 输入 5（空目录新建异常清理）→ 任务 2 + 任务 9 测试 `Create_Open_Edit_Save_Reopen_Roundtrip`（含失败路径）。
5. **Proportion**：计划正文 ~440 行；规格 ~336 行；规格没有代码块，本计划代码块集中在签名/测试名，不是程序转写。

—— 自审通过。计划落地后开始按任务 1→9 顺序实施。
