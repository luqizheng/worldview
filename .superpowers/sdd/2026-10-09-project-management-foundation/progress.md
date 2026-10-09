# SDD ledger — plan: docs/superpowers/plans/2026-10-09-project-management-foundation.md

## Pre-flight scan

| 任务对/任务自身 | 接触 | 一个产出 vs 另一个消费 | 检查结果 |
| --- | --- | --- | --- |
| Task 1 (Clock/AppData) → Task 3 (WriteCoordinator) | IClock | WriteCoordinator 构造吃 IClock | 一致 |
| Task 1 (MapFactory) → Task 9 (AddMap) | MapFactory | AddMap 调 `MapFactory.CreateBlankMap` | 一致 |
| Task 1 (MapIdGenerator) → Task 9 (AddMap) | MapIdGenerator | AddMap 调 `MapIdGenerator.NextId` | 一致 |
| Task 1 (ProjectError) → Task 2/3/6/7/8/9 | ProjectException 工厂 | 多任务用同一工厂 | 一致 |
| Task 2 (DirectoryInspector) → Task 9 (CreateProject) | DirectoryInspector | CreateProject 调 `ValidateForCreate` | 一致 |
| Task 3 (WriteCoordinator) → Task 6 (Backup copy) | WriteFileAtomic | Backup 复制走自身 File.Copy（不调 WriteCoordinator） | 一致（解耦） |
| Task 3 (WriteCoordinator) → Task 4 (DirtyVersion) | WriteCoordinator 与版本号 | 写完后由 ProjectService 显式 MarkClean(version) | 一致 |
| Task 4 (DirtyState) → Task 5 (Autosave) | DirtyStateService | Autosave 注入 dirty；Flush 后 MarkClean | 一致 |
| Task 5 (Autosave) → Task 9 (ProjectService) | AutosaveService | ProjectService 调 ScheduleAutoSave/FlushNow | 一致 |
| Task 6 (Backup) → Task 8 (Recovery) | BackupService | Recovery 调 CreateSnapshotAsync 作保护快照 | 一致 |
| Task 6 (Backup) → Task 9 (Open/Restore) | BackupService | OpenProject 调 PreOpenRotationAsync；ProjectService 暴露 List/Restore | 一致 |
| Task 7 (Recent) → Task 9 (Open/Create) | RecentProjectService | 打开/创建时 RecordOpened | 一致 |
| Task 8 (Recovery) → Task 9 (ProjectService) | RecoveryService | ProjectService 委托 RestoreBackupAsync | 一致 |
| Task 9 (ProjectService) → EditorContext | EditorContext 单例追加 | EditorContext 仅追加单例字段 | 一致（不改既有方法） |
| Task 9 SampleWorldValidationTests 与 `examples/sample_world/project.yaml` | 实际 map_index 长度 | 测试断言 Maps.Count | 实际 2 项；测试断言 2，一致 |
| Task 9 AddMap 复用 `WorldIndex.AppendMap` | WorldIndex | AppendMap 接受非空 MapIndex；null 也吃 | 一致 |
| Task 1 MapIdGenerator.NextId 与 World.MapIndex 默认值 | MapIndex 默认 = `Array.Empty<>()`（非 null） | 测试 `Handles_MissingMapIndex` 故意传 null 测 null 路径 | 须实现 null safe |

## Rulings (pre-flight)

- **R1 — Task 3 写协调器对 `ProjectLoader.Load` 的错误处理**: `ProjectLoader.Load` 在目录或 yaml 缺失时直接抛 `FileNotFoundException`/`DirectoryNotFoundException`/`YamlException`，不返回 ValidationResult 含错。Task 3 `WriteProjectAtomic` 在 staging 写完调 `ProjectLoader.Load(stagingRoot)` 校验时，catch `Exception` 包装成 `ProjectException(SaveFailed, ..., innerException)`，同时删除残留 staging 目录。Cost if wrong: 集成测试误判半文件场景，临时目录残留。
- **R2 — sample_world `map_index` 长度**: 已核 `examples/sample_world/project.yaml:33-35` 实际仅 2 项（world_kael + city_kael），`character_index` 单项。Task 9 测试断言 `Maps.Count == 2` 与实际一致。无需调整。Cost if wrong: 无。
- **R3 — 备份/自动保存串行化**: 计划未规定。MVP 不加 lock；`BackupService` 与 `AutosaveService` 都经 `ProjectService` 单线程调用（Godot 主线程）。Cost if wrong: 并发极小概率产生半成品 manifest；规格未强制要求。
- **R4 — EditorContext `_ExitTree` 顺序**: 已确认 `_ExitTree` 中 `Instance == this` 时 `Instance = null`。追加 `ProjectService?.Dispose()` 在 `Instance` 置空后调用，无副作用。Cost if wrong: 无。
- **R5 — `Maps` 与 `World.MapIndex` 顺序一致**: 复用 `WorldIndex.AppendMap`，实现时遍历 `World.MapIndex` 加载地图保证 i 对 i。Cost if wrong: 重复 ID 或缺地图。
- **R6 — `IAppDataLocator` Godot 反射**: `SystemAppDataLocator` 用 `Type.GetType("Godot.ProjectSettings, GodotSharp")` 反射，**禁止** `using Godot;` 静态依赖。**禁止** 在 `SystemAppDataLocator.cs` 文件顶部 `using Godot;`。Cost if wrong: 领域层污染 Godot.* 引用，破坏全局约束。
- **R7 — ProjectSession 不应包含 `ProjectService` 循环引用**: `ProjectSession` 仅持有数据 + CurrentMapId；`ProjectService` 持 `_session` 与各服务。Cost if wrong: 循环依赖测试难以构造。
- **R8 — 备份名称唯一**: Task 6 命名快照要求 "名称唯一" → `BackupFailed(NameConflict)`。实现用 `ListAsync` 扫描重名；PreOpenRotation 用 `auto-<utcTicks>` 不会冲突。Cost if wrong: 用户能覆写旧快照。
- **R9 — Task 9 `OpenProjectAsync` 失败 try/catch**: `CreatePreOpenRotationAsync` 失败只 `Debug.WriteLine`；目录非法 / Load 失败仍走 `InvalidProject`。Cost if wrong: 用户丢失备份但工程仍可开（MVP 妥协）。
- **R10 — 沙箱内 `dotnet test` 不可用**: 本会话 sandbox spawn child 时 `ProcessHelper.SetExitCallback` 调 `OpenProcess(parentPid, PROCESS_ALL_ACCESS)` 失败 Win32 错误 5，xUnit vstest testhost 无法启动（NU1900 也拒网）。计划禁改 .csproj、不引新 NuGet。Ruling：每个 implementer 在 sandbox 内通过已就位的 `xunit.console.exe`（项目里 xunit.runner.console 2.9.2 自带）跑测试。命令链：
  ```
  cd D:\kudi-project\world-view
  dotnet build WorldView.csproj -c Debug -nologo
  dotnet build tests\WorldView.Tests\WorldView.Tests.csproj -c Debug -nologo
  $rc='tests\WorldView.Tests\bin\Debug\net8.0\xunit.console.runtimeconfig.json'
  (Get-Content $rc -Raw | ConvertFrom-Json) | % {
    $_.runtimeOptions.framework = @{name='Microsoft.NETCore.App'; version='8.0.0'}
    $_ | ConvertTo-Json -Depth 8 | Set-Content $rc
  }
  dotnet exec --runtimeconfig $rc tests\WorldView.Tests\bin\Debug\net8.0\xunit.console.dll tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll -nologo -parallel none
  ```
  baseline 已验：184 tests / 20 failed（全部预存在：`LocateSampleWorld` 路径查找与 `UndoStackTests.Apply_Undo_Loop_100_Times*` 计数对称）。新增 ProjectManagement 测试不与现有失败重名。Cost if wrong: 没有失败信号，盲目推进。

## Sandbox-friendly test command (passed to every implementer)

```
$root = 'D:\kudi-project\world-view'
Set-Location $root
dotnet build $root\WorldView.csproj -c Debug -nologo | Select-Object -Last 5
dotnet build $root\tests\WorldView.Tests\WorldView.Tests.csproj -c Debug -nologo | Select-Object -Last 5
$rc = Join-Path $root 'tests\WorldView.Tests\bin\Debug\net8.0\xunit.console.runtimeconfig.json'
$j = Get-Content $rc -Raw | ConvertFrom-Json
$j.runtimeOptions.framework = @{ name='Microsoft.NETCore.App'; version='8.0.0' }
$j | ConvertTo-Json -Depth 8 | Set-Content $rc
dotnet exec --runtimeconfig $rc (Join-Path $root 'tests\WorldView.Tests\bin\Debug\net8.0\xunit.console.dll') (Join-Path $root 'tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll') -nologo -parallel none
```

Filter to a single class (saves noise):
```
& dotnet exec --runtimeconfig $rc <xunit.console.dll> <WorldView.Tests.dll> -nologo -parallel none -class WorldView.Tests.ProjectManagement.MapIdGeneratorTests
```

## R11 — subagent dispatch stuck/lost in this session

Dispatched Task 1 implementer as subagent 6d955d11 — agent never produced any file, never wrote a report, never replied to status pings, remained "running" for many rounds. Glob/git log confirmed zero work. Interrupted. **Ruling**: switch execution model — the controller (this session) implements each task directly. Reasoning: this sandbox loses subagent agents silently; the implementer prompt is mechanical transcription; controller has all the spec/plan/brief context. Cost if wrong: slower (controller context grows), but deterministic. Future dispatches skipped.

## Ledger

- Task 1: dispatch aborted (R11); controller will implement directly in this session.

## Ledger

- Setup: workspace created at `<root>/.superpowers/sdd/2026-10-09-project-management-foundation/`; marker `docs/superpowers/plans/2026-10-09-project-management-foundation.md`; `.gitignore` updated.
- BASE: d58325c21899c835c76e1617188070a4cea70bc1 (branch: deepseek-build).
- Pre-flight scan complete; 9 rulings recorded above.
