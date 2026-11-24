# Map Workspace 上下文审计（WP-30 PRE-FLIGHT 架构检查）

- **核验日期:** 2026-11-24
- **基准:** `master` @ `28897fc6c96a0e448a377950392f403956af0357`
- **范围:** 任务第四节 6 项架构检查，全部基于当前 master 源码。

## 检查 1：Desktop 能否经 Application 层取得 Map 的正式上下文 —— **能（已验证）**

依赖链（全部真实接线，无占位）：

```
Desktop (Program.cs BuildServices 组合根)
  → Application 端口: IMapQueryService / IWorkspaceContextResolver / IMapCreationService
    → Infrastructure 适配: CoreDomainMapQueryService / CoreDomainWorkspaceContextResolver /
      CoreDomainMapCreationService   (WorldView.Infrastructure/Persistence/CoreDomain/CoreDomainAdapters.cs:127/161/225)
      → Domain 规则: WorkspaceContextRules.TryResolve / MapService (WorldView.Domain)
        → SqliteCoreDomainDatabase (会话绑定 IPackageDatabase)
```

关键证据：

- 组合根按**会话数据库**构造三个服务（`Program.cs` ViewModel 工厂：`new CoreDomainMapQueryService(session.Database)` 等），项目范围隔离正确；
- `ProjectSession.OpenWorkspaceAsync`（`Sessions/ProjectSession.cs:144-180`）：先 `GetMapAsync` 验证 Map 存在（null → 抛异常拒绝），再 `ResolveAsync` 解析上下文，晚到达保护（Ready 态检查）后才挂载工作区；
- 解析在 `SqliteCoreDomainDatabase.InSnapshotScope` 单一原子快照内完成（`CoreDomainAdapters.cs:242-251`，CD-01-F1 临界区）。

## 检查 2：Branch 与 Historical Time 是否已有可靠上下文来源 —— **有（Core Domain 独占，已验证）**

- **Domain 规则:** `WorldView.Domain/Workspace/WorkspaceContextResolution.cs` `WorkspaceContextRules.TryResolve` 四步：
  1. Map 存在且属于当前 Project（跨 Project 拒绝）；
  2. 根 History 经 `core_history.owner_map_id` 唯一解析（缺失 = 数据损坏 fail-closed）；
  3. Branch 世系解析（null = 根历史；环/跨 Map/悬空经 `BranchLineageResolver` 拒绝）；
  4. 规范历史时间：typed `HistoricalTime.TryParse`（"day:offset" 格式），null → 纪元。
- **typed HistoricalTime:** `WorldView.Domain/Time/HistoricalTime.cs`——不可变类（`DayNumber:long` + `OffsetSeconds:int` ≤86399）、`TryCreate/TryParse/Canonical()/IComparable/Equals`。**这是 PRA-01 PresentationContext §2.2 所需"规范类型化域时间"的直接供给源，且已有测试**（`WorldView.Domain.Tests/Time/HistoricalTimeTests.cs`）。
- **测试:** `WorldView.Domain.Tests/Workspace/WorkspaceContextRulesTests.cs`（归属/世系/非法 Branch/非法时间）。
- **边界收窄（见检查 6）:** Application 侧 `ValidatedWorkspaceContext` 只携带 MapId/BranchId/CanonicalHistoricalTime(string)；typed 时间、RootHistoryId、Lineage 在适配层被丢弃（`CoreDomainAdapters.cs:258-263`）。

## 检查 3：ViewModel 是否仍使用占位数据 —— **地图数据真实；瞬态视图状态为显式占位**

- **真实数据路径:** `ProjectWorkspaceViewModel.LoadMapsAsync`（`WorldView.Desktop/ViewModels/ProjectWorkspaceViewModel.cs:73-97`）经 `IMapQueryService` 列出真实 `MapSummary`；端口缺失时如实显示依赖门槛文本，**明确拒绝以占位数据冒充**（AF-02 §4.6 纪律，类头注释自证）。
- **显式占位（有意、已声明）:** `WorkspaceViewState`（`Sessions/MapWorkspaceSession.cs:109-125`）——CameraX/CameraY/Zoom/ActiveTool/SelectedItemIds(HashSet) 全部占位且可变；**UI 零消费**（WP-00 盘点核实：唯一消费是 `SessionUseCaseTests.cs:393,397` 的独立性测试）。
- **WP-30 含义:** Pan/Zoom 实施时 `WorkspaceViewState` 是视口状态的自然宿主，但其可变性（先验审计 PRA01-AUD-03）要求：进入后台演示管线的值必须在请求创建时拷贝为不可变输入，不得把该对象传入 worker。

## 检查 4：Map Workspace 打开时能否取得真实 Map identity —— **能（已验证）**

- `ProjectWorkspaceViewModel.OpenWorkspaceAsync`（`ProjectWorkspaceViewModel.cs:100-129`）取 `SelectedMap.MapId`（来自 Core Domain 读投影，非 UI 造数）→ `Session.OpenWorkspaceAsync(mapId, ...)`；
- `MapWorkspaceSession.WorkspaceId` 为进程内临时 GUID，与 MapId 严格分离；UI 状态栏分别显示二者（`ProjectWindow.axaml` 底栏"进程内会话 ID（临时，非 Project ID）"）；
- 创建路径同为真实：`CreateMapUseCase`（WP-20a，`Application/UseCases/CreateMapUseCase.cs`）经 `CoreDomainMapCreationService` 写入，名称唯一性在创建事务内复检；`MapRecord` 携带不可变 `MapSpaceId`（`Domain/Maps/MapRecords.cs`）。
- 测试：`WorldView.Desktop.Tests/ViewModels/ProjectWorkspaceViewModelTests.cs`（4 测试）+ `WorldView.Infrastructure.Tests/Persistence/CoreDomain/Wp20aMapCreationTests.cs`（5 测试）。

## 检查 5：是否存在 Presentation 项目或可复用场景模型 —— **均不存在**

- 无 `WorldView.Presentation`（或任何 MapEngine/Renderer）工程；solution 构成见 FRAMEWORK_AND_PACKAGE_MATRIX.md；
- 无渲染中立场景模型/绘制描述/图层元数据——全仓 `Scene` 零命中；
- 可向 Presentation 层供给的**原材料**（非场景模型）：typed `HistoricalTime`、`MapRecord`（含 MapSpaceId）、`BranchRecord`/`BranchLineage`、`WorkspaceContextRules`、以及 `Domain/Geometry/` 的 GeometryAsset 规则（数据侧，非渲染侧）。

## 检查 6：是否存在不必要的重复模型 —— **有一处需 WP-30 决策的边界收窄（非冗余复制）**

| 模型 | 层 | 字段 | 角色 |
|---|---|---|---|
| `ResolvedWorkspaceContext` | Domain | ProjectId/MapId/MapName/RootHistoryId/BranchId/Lineage/**typed HistoricalTime** | Core Domain 解析结果（富） |
| `ValidatedWorkspaceContext` | Application | MapId/BranchId/**CanonicalHistoricalTime(string)** | AF-02 有意的不透明占位（窄） |

- **判定:** 这不是"不必要的重复"——它是刻意的防腐层（适配器只做映射，规则归 Domain，`CoreDomainAdapters.cs:219-223` 注释自证）。但它构成 WP-30 的**关键输入缺口**：PresentationContext 需要 typed HistoricalTime 与 revision 身份，而 Application 边界当前只给字符串；Lineage/RootHistoryId 同样丢失。WP-30 实施时二选一：
  1. **拓宽 `ValidatedWorkspaceContext`**（携带 typed HistoricalTime + RootHistoryId，AF-02 占位注释本就预告"正式类型随领域实现落地"）——推荐；
  2. 在 Presentation 层新增第二个适配（Domain→Presentation 直连）——会绕开 Application 边界，不推荐。
- **其余排查:** 无第二地图模型（UI 直接绑定 `MapSummary`/`MapWorkspaceSession`，无 DTO 复制）；无渲染相关重复（渲染侧为零）；Field/Initial State 指纹体系与 PRA-01 指纹用途不同，非重复。

## 附加核验（任务第三节关联）：Map 边界数据可得性

- `MapRecord` 字段：MapId/ProjectId/MapSpaceId/SpatialReferenceId/MapName/LifecycleStatus/时间戳——**无 extent/bounds/width/height**（Schema 与迁移 `20261122090235_CoreDomainInitial` 同构）。
- **对 WP-30 的直接影响:** roadmap WP-30 要求"render a neutral background and map frame/bounds **when those are available from formal read projections**"——当前正式读投影不可提供任何边界（新图无对象、无范围），空白帧的可渲染内容 = 中性背景 + 视口原点默认相机；map frame/bounds 项在数据侧尚无供给源，属 WP-30 实施时按契约措辞跳过或另行供给的项。
- `SpatialReferenceId` 可为 null（未配置时禁止发布空间数据）——PresentationContext 的 ProjectionDefinitionId 字段在 M1 没有权威来源，见 GAP 文档。

## 并发范围确认（任务第五节）

本仓现状与"单机单用户桌面"完全一致：无分布式锁/消息队列/分布式缓存/多用户协调/通用调度框架的任何依赖（全部 8 个 csproj 核对）。现存并发原语均为进程内必需项，WP-30 应保留并沿袭：

- `ProjectSession._stateGate` 会话状态锁 + 晚到达保护；
- `SqliteConnectionAccess` 单连接协调器 + `InSnapshotScope` 可重入临界区（读快照原子性）；
- `MapWorkspaceSession._derivedWorkCts` 派生工作取消；
- Program.Main 同步停机编排。

PRA-01 契约自身（§0/§12）明确排除 broker/分布式缓存/调度框架，要求 `Volatile.Read`/`Interlocked.Exchange` 级别的进程内原子性——与本仓约束一致，无张力。
