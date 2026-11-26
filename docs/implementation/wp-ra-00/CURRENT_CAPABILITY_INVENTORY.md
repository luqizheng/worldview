# WP-RA-00 — 代码能力清单（CURRENT_CAPABILITY_INVENTORY）

基线：`20c6fcca3e13e42cd9a3decdcd1437506890daad`。全部结论以真实代码 + 测试为依据。
状态标记：✅ 已实现且有测试证据 · 🟡 部分实现 · 📄 仅设计文档 · ❌ 未实现 · ⛔ 被上游能力阻塞。

源文件统计（排除 obj/bin）：Domain 39 · Infrastructure 36 · Application 43 · Presentation 20 · Desktop 14。
测试声明（`[Fact]`/`[Theory]` 计数）：Domain 202 · Application 176 · Infrastructure 234 · Presentation 107 · Desktop 65，合计 **784 个测试声明**。

---

## A. Presentation 层（WP-30A / PRA-01，M1 范围）

| 能力 | 代码位置 | 实现事实 | 测试证据 | 状态 |
|---|---|---|---|---|
| 不可变 PresentationContext | `src/WorldView.Presentation/Identity/PresentationContext.cs` | 全部身份字段构造期校验；持有 typed `HistoricalTime`、`NotEvaluated`/`NotConfigured` typed 状态、样式/字体 token、ViewportGeneration | `PresentationContextTests` 19 项 | ✅ |
| 规范化指纹 | `Identity/ContextFingerprint.cs` + `PresentationContextCanonicalizer.cs` | 构造期确定性计算；`BlankScene` 装配版本 v1（`PresentationAssemblerVersions.BlankScene = 1`） | `ContextFingerprintTests` 18 项 | ✅ |
| 不可变 SceneSnapshot | `Scene/SceneSnapshot.cs` | `ImmutableArray` 私有拷贝；发布器分配严格递增版本；诊断不含指纹输入 | `SceneSnapshotTests` 8 项 | ✅ |
| 空白场景载荷 | `Scene/BlankSceneContent.cs` | 明确注释"不包含任何地图几何、bounds/extent、标签、地形、道路、建筑或装饰" | 同上 | ✅（按设计即空白） |
| 场景装配器 | `Assembly/SceneAssembler.cs` | 必填/可选输入整体校验：缺失、混合上下文、schema/装配版本不符一律整体拒绝；图层顺序权威按输入列表序 | `SceneAssemblerTests` 18 项 | ✅ |
| 快照发布器（唯一发布权威） | `Publication/SceneSnapshotPublisher.cs` | `Interlocked.Exchange` 写 / `Volatile.Read` 无锁读；六步发布校验（disposed→coordinator_closed→candidate→指纹→活动序号→单调版本）；语义切换原子清空 last-known-good；F-1 关闭屏障与发布决策同锁全序 | `SceneSnapshotPublisherTests` 15 项 | ✅ |
| 请求协调器（latest-request-wins） | `Publication/PresentationRequestCoordinator.cs` | 新请求立即取代旧活动请求；取消只省工作不作正确性依据；`RejectedClosed`/`RejectedStale`/`Superseded`/`Cancelled`/`Failed` 五态明确；64 容量有界诊断环 + 计数器 | `PresentationRequestCoordinatorTests` 14 项 | ✅ |
| 关闭/发布竞态正确性 | 同上 `CloseAsync`（置屏障于发布器锁内） | 线性化点注释明确；迟到完成一律 RejectedClosed | `CoordinatorClosePublicationRaceTests` 4 项（TCS 确定性交错） | ✅ |
| 每-视口运行时隔离 | `Publication/ViewportPresentationRuntime.cs` | 私有 Publisher+Coordinator+ViewportGeneration；关闭顺序 Coordinator→Publisher；文档明令不得全局共享 | 经 Publisher/Coordinator 测试覆盖 + 架构测试 | ✅ |
| 视口代计数 | `Publication/ViewportGenerationTracker.cs` | 单调递增；`OnViewportChanged` 推进 | 经 Binding 测试间接覆盖 | ✅ |
| 架构边界（无 UI/持久化依赖） | — | Presentation 程序集零 Avalonia/Skia/ORM 引用 | `Architecture/PresentationBoundaryTests` 2 项 | ✅ |
| 上下文工厂（Application→Presentation 映射） | `Identity/PresentationContextFactory.cs` `FromValidatedWorkspace` | 存在且完整 | `Mapping/PresentationContextFactoryTests` 9 项 | 🟡 **有实现有测试，但生产代码零调用点**（见 §D-1） |
| 场景请求入口 | `Coordinator.RequestSceneAsync` | 存在且完整 | 38 处测试引用 | 🟡 **生产代码零调用点**（见 §D-1） |
| 真实内容准备子系统 | — | 不存在；默认准备只产空白场景（`DefaultBlankScenePreparationAsync`，Coordinator.cs:375） | — | ❌（WP-40 范围） |

## B. Desktop / WP-30B（Avalonia Viewport + Skia）

| 能力 | 代码位置 | 实现事实 | 测试证据 | 状态 |
|---|---|---|---|---|
| 视口控件 | `src/WorldView.Desktop/Rendering/MapViewportControl.cs` | 只保留布局/输入转发/尺寸 DPI 观察/失效；`Render` 入口第一件事 volatile 读 `IsClosed`；Bind 拒绝已关闭 Binding；Detach 不关闭（生命周期 A） | 无控件级自动化（PR #34 裁定走人工验收） | ✅（代码层） |
| 唯一帧捕获点 | `Rendering/MapViewportBinding.cs` `CaptureFrame` | UI 线程同步值拷贝：快照引用一次原子读 + 缓存标量 + 相机标量 + 视口代 | `MapViewportBindingTests` 18 项 | ✅ |
| 关闭子序与重试 | 同上 `RequestClose`/`ClosePipelineAsync` | 1a 置终态 → 1b 协调器 → 1c 发布器 → 1d 渲染器；阶段级完成标记；进行中共享 Task（DEF-01）；失败重试只补未成功阶段；异常聚合延迟传播 | 同上 + `ProjectCloseOrchestrationTests` 17 项 | ✅ |
| Debug 故障注入 | `Rendering/DebugCloseFault.cs` + `DisposePublisherStep` | `#if DEBUG` 一次性 CAS 故障；Release 无分支无状态 | `DebugCloseFaultTests` 4 项 | ✅ |
| 绘制操作 | `Rendering/MapDrawOperation.cs` | `ICustomDrawOperation` 方案 A；Skia lease 仅在 Render 调用栈内 `using` 释放；相等语义=帧输入值 | 经 Binding 测试间接覆盖 | ✅（基础设施层） |
| 帧输入 | `Rendering/ViewportFrameInput.cs` | readonly record struct：Snapshot 引用 + 尺寸 + DPI + 相机 + 代；无 Skia/控件/session 引用 | 同上 | ✅ |
| 场景渲染器 | `Rendering/MapSceneRenderer.cs` | **无状态；唯一绘制命令 `canvas.Clear(0x202226)`；注释自认"完整地图绘制属后续阶段"；Dispose 幂等空操作** | 无像素级断言 | 🟡 仅为空态渲染；帧输入中 Snapshot/相机/缩放**不被消费** |
| Skia 长期资源所有权 | — | M1 零长期资源（ADR-09），无释放需求 | — | ✅（空集意义上的完成；真实资源管理未验证） |
| Pan/Zoom 输入与变换 | — | `WorkspaceViewState.CameraX/Y/Zoom` 存在（`MapWorkspaceSession.cs:292-301`），Binding 捕获进帧输入，但**无输入处理器、无坐标变换应用** | — | ❌（WP-30C 范围） |
| 工作区接线 | `ViewModels/ProjectWorkspaceViewModel.cs:199,319` | 工作区打开时 `CreateViewportBinding(workspace.ViewState)` 创建 Binding；关闭命令先关 Binding 再关 Session | `ProjectWorkspaceViewModelWorkspaceTests` 4 项 | ✅（生命周期）；🟡（呈现：从不发起场景请求，见 §D-1） |

## C. Core Domain / Infrastructure（权威数据）

### C-1 身份与上下文（✅ 完整）

| 能力 | 证据 |
|---|---|
| Map / Branch / History / Calendar 聚合 | `Domain/Maps/MapRecords.cs`（`MapRecord` 含 `MapSpaceId`/`SpatialReferenceId`，**无 extent/bounds 字段**）、`BranchRecord`（parent_at 闭边界，ADR-005） |
| typed HistoricalTime | `Domain/Time/HistoricalTime.cs`（day/offset 全精度）；Domain 测试 9 项 |
| 工作区上下文解析 | `Infrastructure/Persistence/CoreDomain/CoreDomainAdapters.cs` `CoreDomainWorkspaceContextResolver` → `ValidatedWorkspaceContext`（MapId/BranchId/RootHistoryId/typed 时间）；`ValidatedWorkspaceContextMappingTests` 4 项 |
| 地图摘要读服务 | `CoreDomainMapQueryService` 实现 `IMapQueryService`（仅 `MapSummary`：MapId+DisplayName） |

### C-2 写路径（Domain 服务为静态规则类，持久化经 SQLite 端到端测试）

| 能力 | 代码 | 测试 | 状态 |
|---|---|---|---|
| Project / Map 创建 | `CoreDomainProjectCreationService` / `CoreDomainMapCreationService` | `Wp20aMapCreationTests` 5 项、`CoreDomainPersistenceTests` 19 项 | ✅ |
| Object / ObjectType / Definition / Reservation / Initial State | `Domain/Objects/`（ObjectCreationService、ObjectDefinitionService、MapRegistrationService、MapInitialStateService、ObjectTypeSeedingService、ObjectIdReservationService） | `ObjectDomainServiceTests` 25、`ObjectRulesTests` 19、`Cd02ObjectPersistenceTests` 27、`Cd01EndToEndTests` 2、`Cd02MapEditorIntegrationTests` 2 | ✅ |
| Geometry 规则 / Asset 信封 | `Domain/Geometry/GeometryRules.cs`、`GeometryAssetEnvelope.Validate`；几何经 Asset 附着到 Definition（`CoreObjectDefinitionAssetEntity`） | `GeometryAndAssetRulesTests` 33、`Cd02AssetPersistenceTests` 9、`GeometryAtomicPublicationTests` 8 | ✅（规则/持久化层） |
| Field Dataset / Revision / Payload / 包格式 | `Domain/Fields/` + `Application/Fields/` + `Application/Packaging/` | `Cd02FieldDatasetPersistenceTests` 43、FieldPackage 系列 47、`FieldPackageImportExportAuthorityTests` 19 | ✅ |
| Building / Cave 聚合校验 | `Domain/Aggregates/BuildingCaveAggregateValidator.cs`（publication 层 typed link/Asset hash 对账） | 经 Infrastructure 聚合向量一致性测试覆盖 | ✅（校验层）；❌ 无 Building 专属 Command/编辑能力 |
| Spatial Reference | `Domain/Maps/SpatialReferenceServices.cs`；`MapSpatialReferenceOwnership` 迁移（20261122140144） | `SpatialReferenceServiceTests` 3、`SpatialReferenceRulesTests` 5 | ✅ |
| EF 持久化 | `Infrastructure/Persistence/CoreDomain/EfCore/`：4 个迁移（CoreDomainInitial → FieldPayloadStorage，最新 20261123090000） | `CoreDomainInitialMigrationTests` 3、`CoreDomainModelSnapshotDiagnosticsTests` 1、`PersistenceConcurrencyTests` 12、`PersistenceDomainIntegrityTests` 15 | ✅ |

### C-3 读取投影（**关键缺口区**）

| 能力 | 事实 | 状态 |
|---|---|---|
| 按 Map+Branch+HistoricalTime 的一致完整地图投影 | 不存在。`ICoreDomainDatabase` 仅原始行读取；无 as-of-time 语义 | ❌ |
| Command / Event / Checkpoint / 状态重建 | 全仓零实体、零回放。`HistoryRecord` 只是 Command/Event 的归属作用域声明（ADR-002），无事件存储 | ❌ |
| Relationship 数据模型 | 不存在（`CoreDomainInitialSql.cs:300`："relationship payload 留待 Relationship 阶段"） | ❌ |
| Object 当前定义读取 | `ListDefinitionAssetLinks` 等原始行可用；无不可变聚合投影服务 | 🟡 |
| time-zero 初始内容读取 | `ListMapObjectRefs` + `ListInitialObjects`（membership/placement canonical JSON payload）真实可用 | ✅（原始行级；无投影服务） |
| Map extent / bounds | `MapRecord` 无任何边界字段 | ❌ |
| World State revision | 不存在；Presentation 以 `NotEvaluated` 显式占位 | ❌ |

## D. 集成断点（审计最重要发现）

### D-1 场景请求管线未接线

- 全仓生产代码（`src/`）中 `RequestSceneAsync` 的调用点：**0**（仅 Presentation 内部定义）。
- `PresentationContextFactory.FromValidatedWorkspace` 生产调用点：**0**。
- 推论：运行时 `SceneSnapshotPublisher.Current` 恒为 null；`MapSceneRenderer` 的输出（纯色背景）与发布管线完全无关——即使接通，M1 渲染输出也不会改变。
- 影响判定：**"空白场景管线被测试锁定" 与 "桌面应用实际使用该管线" 是两件未完成衔接的事。**

### D-2 桌面宿主现状

- `ProjectWindow.axaml` 已有视口宿主区（ContentControl），`SelectedWorkspace → 视口宿主接线`（PR #31 报告）。
- WP-UI-04 的"一图一独立窗口"架构（MapWorkspaceWindow）尚未开始（04C 未启动）。

## E. ME-05~ME-08 相关代码能力对照

| ME 工作流 | 代码现状 |
|---|---|
| ME-05 几何/字段/包 | ✅ 持久化与包读写导入导出完成（见 §C-2）；**Map Engine 侧空间谓词/度量/索引零代码** |
| ME-06 交互框架 | 📄 仅文档（v0.3 合同）；Desktop 无 Selection/Inspector/ToolRegistry 任何代码 |
| ME-07 渲染架构 | 📄 映射 PRA 序列的规划文档；PRA-01/02 落地，PRA-03~08 无代码 |
| ME-08 空间运营 | 📄 v0.1 working baseline，自述"不授权实现"；无任何对应代码 |
