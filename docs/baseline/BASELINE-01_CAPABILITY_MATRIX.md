# BASELINE-01 功能实现状态矩阵

- **基线**：origin/master `6ec34ea`
- **状态定义**：IMPLEMENTED（代码+测试证据）／ PARTIAL ／ DESIGNED（仅有设计合同）／ PROPOSED ／ NOT_STARTED ／ BLOCKED ／ NEEDS_REVIEW

## A. 产品与 UI

| # | 能力 | 状态 | 关键证据 | 缺口 |
|---|---|---|---|---|
| A1 | 桌面主窗口与导航 | 🟡 PARTIAL | `Desktop/Views/StartWindow.axaml`(167行)、`ProjectWindow.axaml`(151行)、`Program.cs` 组合根；`StartViewModelTests`/`ProjectWorkspaceViewModelTests` | 菜单仅"关闭项目"一项；无停靠面板/完整导航 |
| A2 | 项目创建/打开/关闭 | ✅ IMPLEMENTED | `CreateProjectUseCase`/`OpenProjectUseCase`（fail-closed 分阶段校验）、`ProjectWorkspaceViewModel.CloseProjectLifetimeAsync:591-746`（CAS 幂等+sticky）；`SessionUseCaseTests`/`ProjectSessionCloseTests`/`ProjectCloseOrchestrationTests`(17)/`RealPackageFlowTests` | — |
| A3 | 地图工作区 | 🟡 PARTIAL | Session 层：`MapWorkspaceSession`（状态机/同图去重/ContextSwitched，PR #36）+ `MapWorkspaceSessionLifecycleTests`；**04C 窗口层 NOT_STARTED**（docs/implementation/wp-ui-04 README 自认） | 未实现独立窗口（现内嵌 ProjectWindow `ViewportHost`）；无窗口级管理 |
| A4 | 地图视口（pan/zoom） | 🟡 PARTIAL | 渲染通路：`MapViewportControl`/`MapViewportBinding`(564行)/`CaptureFrame`/`MapViewportBindingTests`(18) | **零输入**：无 Pointer/Wheel 事件（全 src grep 0）；相机标量占位 |
| A5 | 图层面板 | ⛔ NOT_STARTED | 仅 `SceneLayers.cs` 呈现域概念 | 无 UI |
| A6 | 属性检查器 | ⛔ NOT_STARTED | 仅 ME-01/roadmap 文档提及 | 无代码 |
| A7 | 工具栏与交互系统 | 📄 DESIGNED | ME-06 系列 v0.1~0.3 + a/b/d 子契约（docs/map-editor/）；代码仅 `WorkspaceViewState.ActiveTool` 占位 | 无工具类、无输入路由 |
| A8 | 建筑独立场景编辑器 | 📄 DESIGNED | ME-04 系列（含 b~h 子契约）；代码仅 `BuildingCaveAggregateValidator` 校验器 | 无编辑域服务、无 UI |
| A9 | Windows/macOS 兼容 | 🟡 PARTIAL | CI 四矩阵全绿；无 OS 条件编译；OS 感知点：`ProjectPackageStructure.cs:45`/`SettingsDocumentPaths.cs`/`FileAppLog.cs:22-24` | Windows 本机已验证；**macOS 本地执行 NOT_RUN**（WorldView/README.md:68-73、dev-environment.md:60-64 自认） |

## B. Core Domain

| # | 能力 | 状态 | 关键证据 | 缺口 |
|---|---|---|---|---|
| B1 | Project/Map/Branch | ✅ IMPLEMENTED | `ProjectRecord`/`MapRecords.cs`/`BranchLineage.cs`；`ProjectRulesTests`/`BranchLineageTests`(8)/`Cd01EndToEndTests`（含跨包隔离） | — |
| B2 | Object/Definition/Type Registry | ✅ IMPLEMENTED | `ObjectTypeRegistry`/`JsonSchemaSubset`/`ObjectCreationService`/`CanonicalJson`(RFC8785)；`ObjectTypeRegistryTests`(12)/`Cd02ObjectPersistenceTests` | — |
| B3 | Geometry 领域模型 | ✅ IMPLEMENTED（V1 最小集） | `GeometryRules`（10 规范字段/7 有界族/dim=2）+ `GeometryAssetEnvelope`；`GeometryAndAssetRulesTests`/`GeometryAtomicPublicationTests` | 仅校验零计算；`RejectSplitMerge()` 显式抛异常（P2-09-19 §7.5 延期）；无 core_geometry_component 表（有意） |
| B4 | Building/Cave | 🟡 PARTIAL | `BuildingCaveAggregateValidator`（三层校验、CD_BLD_*/CD_CAVE_* 错误码）；`Cd02AggregateVectorConformanceTests` | 无编辑服务、无 UI、子实体仅 JSON payload |
| B5 | Asset 模型 | ✅ IMPLEMENTED | `AssetService`（内容寻址幂等/typed link/原子发布）+ `FileAssetStore`（staging→hash→原子发布）+ `VerifyAssetIntegrityUseCase`；`AssetIntegrityVerificationTests` | — |
| B6 | History/Event | 🟡 PARTIAL | 结构层：`HistoricalTime`/core_history/core_branch/core_calendar/`WorkspaceContextResolution`；ADR-001~005 冻结 | **引擎层不存在**：`MapRecords.cs:152` "CD-01 无 Command 恒 null"；无回放/undo/时间线（DESIGNED） |
| B7 | Field Dataset | ✅ IMPLEMENTED | `FieldDatasetService.PublishRevision`（不可变 revision 链）+ WVFLDPK 打包（Writer/Reader/Verifier/Importer/Exporter）+ 3 golden fixtures；`FieldPackage*Tests` 全套 | — |
| B8 | Domain Commands | ⛔ NOT_STARTED | 应用层仅 4 个生命周期/查询用例；无 ICommand/路由/意图类型；设计：ME-06-d、ADR-003/004 | 全部缺失 |
| B9 | Schema 与 Migration | ✅ IMPLEMENTED | EF Core 11、5 迁移（`CoreDomainInitialSql` 手写 DDL 16 表+DEFERRABLE FK）、防降级守卫（`CoreDomainEfSession.cs:44-72`）、快照漂移测试（`CoreDomainModelSnapshotDiagnosticsTests`） | — |

## C. Map Engine

| # | 能力 | 状态 | 关键证据 | 缺口 |
|---|---|---|---|---|
| C1 | 坐标系/投影 | 🟡 PARTIAL + 📄 DESIGNED | `ProjectionDefinition`（M1 仅 NotConfigured，`Identity/ProjectionDefinition.cs`）、`SpatialReferenceService`（注册/绑定）+ 测试 | 无坐标变换数学、无 km 桥接、相机"占位单位"；设计：spatial-presentation 契约 §3、me-05-04 |
| C2 | 几何计算 | 🟡 PARTIAL | 仅 manifest/结构校验（见 B3） | `Math.Sqrt/Distance/Area/Intersect` grep 0 命中；设计：me-05-02/03、me-02-calculation-contracts |
| C3 | Spatial Index | ⛔ NOT_STARTED | grep rtree/quadtree 0；设计：契约 §6（每快照 packed R-tree + 两段式） | — |
| C4 | Hit Testing | ⛔ NOT_STARTED | `MapDrawOperation.HitTest()=>false`（Avalonia 桩，WP-30C 边界）；设计：契约 §5.3（hit radius 6–10px） | — |
| C5 | Snapping | ⛔ NOT_STARTED | 无代码；设计：契约 §4.2/§5.2（typed proposal，radius 8–12px） | — |
| C6 | Spatial Chunk | 💡 PROPOSED | 契约 V0.1（576 行）在**未合并 PR #41 分支**；plan.md:568 要求评审后合并 | 零代码 |
| C7 | Fragment | 💡 PROPOSED | 仅 PR #41 契约 §5（溯源/跨块正确性/不持久化） | — |
| C8 | Large Feature | 💡 PROPOSED | 仅 PR #41 契约术语（专用候选查询路径） | — |
| C9 | LOD | 💡 PROPOSED | PR #41 契约 §7（LOD-0~3 四概念层级、迟滞防抖）；plan.md Phase 6 | grep LOD 0 命中 |
| C10 | 缓存/失效 | ⛔ NOT_STARTED | 唯一机制：语义切换快照原子清空（`SceneSnapshotPublisher.ActivateRequest`）；`MapDrawOperation.Equals` 帧值相等语义 | 无 cache 基础设施；设计：契约 §10 七层缓存+13 行失效矩阵 |

## D. Renderer

| # | 能力 | 状态 | 关键证据 | 缺口 |
|---|---|---|---|---|
| D1 | PRA-01 Presentation Context | ✅ IMPLEMENTED | `Presentation/PresentationContext.cs`（指纹/规范化/工厂）+ `PresentationBoundaryTests` 架构守卫；107 测试 | `WorldStateRevision.NotEvaluated` 显式占位（M1 契约） |
| D2 | Immutable Scene Snapshot | ✅ IMPLEMENTED | `SceneSnapshot`（ImmutableArray 副本/单调版本）+ `SceneAssembler`（逐项校验拒绝零产出）；`SceneSnapshotTests`/`SceneAssemblerTests`(12 拒绝路径) | 场景项仅 `BlankBackgroundItem`（M1 显式契约） |
| D3 | Scene Preparation | 🟡 PARTIAL | `PresentationRequestContracts`（状态机 Created→…→Published） | 真实准备函数=硬编码空白场景（`DefaultBlankScenePreparationAsync:375-384`）；无 Domain 查询 |
| D4 | Scene Publisher | ✅ IMPLEMENTED | `SceneSnapshotPublisher`（六步校验+Interlocked 原子发布）、`PresentationRequestCoordinator`（latest-request-wins/关闭屏障/诊断环 64）；竞态测试 | — |
| D5 | Viewport Binding | ✅ IMPLEMENTED | PR #40：`MapViewportBinding`（请求身份/`_stateGate` 原子化/三阶段关闭）、VM 接线（`ProjectWorkspaceViewModel.cs:241,425` 调 RequestSceneAsync）、A01–A10 集成测试 | 依赖内容的去重刷新路径属 WP-30C/B |
| D6 | MapSceneRenderer | 🟡 PARTIAL | 真实消费快照：`Draw = canvas.Clear(ResolveBackground(snapshot))`；像素级测试（`#202226`/`#E8E9EB`） | 不画任何地图内容；相机标量被捕获但未使用 |
| D7 | Avalonia/Skia 集成 | ✅ IMPLEMENTED | `MapDrawOperation`（ISkiaSharpApiLeaseFeature lease 模式，ADR-09）、`MapViewportControl.Render`、Avalonia 12.1.6 | 非 Skia 后端静默返回（M1 决策） |
| D8 | Geometry Rendering | ⛔ NOT_STARTED | 无 SKPath/SKPaint/任何图元代码；场景模型无几何项类型 | — |
| D9 | Text/Label | ⛔ NOT_STARTED + 📄 DESIGNED | `FontToken` 仅身份 token 无消费者；设计：PRODUCTION_RENDERER_ARCHITECTURE §8、PR #41 §8 | — |
| D10 | Pan/Zoom 渲染联动 | 🟡 PARTIAL | 数据通路在：`WorkspaceViewState`→`CaptureFrame`→`ViewportFrameInput`；尺寸/DPI→ViewportGeneration.Advance | 无输入事件；渲染器不读相机；`OnViewportChanged` 不触发场景请求（R1-P2-02 契约 B） |
| D11 | Chunk/LOD 渲染支持 | ⛔ NOT_STARTED | 渲染器为无状态单遍 clear | 依赖 PR #41 评审合并 |

## E. 数据与持久化

| # | 能力 | 状态 | 关键证据 | 缺口 |
|---|---|---|---|---|
| E1 | SQLite | ✅ IMPLEMENTED | Infrastructure：`Microsoft.Data.Sqlite 11.0.0` + `EFCore.Sqlite 11.0.0`；双层（包库 ADO.NET + EF 短生命周期 DbContext 复用物理连接） | — |
| E2 | 项目文件格式 | ✅ IMPLEMENTED | 目录式 `.wvproject/`（manifest+project.sqlite+Assets/sha256+Staging+Backups），`SupportedPackageFormatVersion=1` fail-closed；`ProjectPackageValidator` | — |
| E3 | Asset Store | ✅ IMPLEMENTED | `FileAssetStore`（staging→hash→原子发布）+ `FieldPayloadFileStore`（同盘 File.Move 原子+reparse point 防御）+ `VerifyAssetIntegrityUseCase` | ⚠️ `Assets/sha256` vs `FieldPayloads/sha256/` 双轨目录命名并存（见 GAP G-11） |
| E4 | Migration | ✅ IMPLEMENTED | 5 个 EF Core 迁移 + 手写 provider 专属 DDL（DEFERRABLE FK/immutability triggers）+ 防降级守卫 + 快照漂移防护测试 | — |
| E5 | 数据一致性 | 🟡 PARTIAL | 28 处 FK、`PRAGMA foreign_keys=ON`、CHECK/UNIQUE、读取侧 fail-closed（`CoreDomainDataException`）、启动时 payload 调和（`FieldPayloadIntegrityReconciler`） | **软失败落地不完整**：payload 缺失仅 `_log?.Warn`（`OpenProjectUseCase.cs:225-241`），无用户可见 Error 诊断通道，"仍可保存"无测试锚定 |
| E6 | 事务与失败恢复 | ✅ IMPLEMENTED | 单事务边界+事务内校验+`quick_check`+staging 清理+孤儿收集；`PersistenceConcurrencyTests`/`GeometryAtomicPublicationTests` | WAL 有意不启用（open gate，`SqlitePackageDatabase.cs:32-33` 注释，记录在案的决策） |
| E7 | 连接生命周期 | ✅ IMPLEMENTED | `SqliteConnectionAccess`（单连接+Monitor 临界区+DisposeAsync 锁内翻转锁外等待）、`Pooling=false`、EF `contextOwnsConnection:false`、拒绝测试替身 | — |

## F. 测试与平台

| # | 能力 | 状态 | 关键证据 | 缺口 |
|---|---|---|---|---|
| F1 | 单元测试 | ✅ IMPLEMENTED | 5 工程 823 Fact/Theory（Domain 202/App 176/Infra 239/Pres 107/Desktop 99），xunit.v3+MTP | csproj 残留 VSTest 适配器冗余（GAP-P1-13 自记） |
| F2 | 集成测试 | ✅ IMPLEMENTED | Infrastructure 22 类全真实 SQLite+文件系统；`DesktopPresentationPipelineIntegrationTests` A01–A10 真实生产对象全链 | Core Domain 查询端口在桌面测试中用替身；GUI 启停无覆盖 |
| F3 | UI 测试 | 🟡 PARTIAL | 仅 ViewModel 层单测 | 无 Avalonia Headless（grep 0；GAP-P1-04 未关闭） |
| F4 | Renderer 测试 | ✅ IMPLEMENTED（M1） | `MapSceneRendererPixelTests`（Skia CPU Surface 真实像素断言）+ 快照/装配/发布/竞态全套 | 几何绘制测试属后续 B 阶段（文件自述） |
| F5 | CI 工作流 | ✅ IMPLEMENTED | `dotnet-ci.yml`：win/macos × Debug/Release 四矩阵、dotnet 11.0.x、`--show-slowest-tests 20`、最小权限 | 无 publish、无 GUI 冒烟 job |
| F6 | Windows/macOS Debug/Release | ✅ IMPLEMENTED（CI 实证） | 四矩阵编译+测试全绿（Run 45845579467）；单 TFM net11.0、无平台条件编译 | CI 证明≠GUI 运行验收；publish NOT_RUN（GAP-P1-15） |
| F7 | 实际 GUI 验收证据 | ⚠️ NEEDS_REVIEW | 验收契约/自评文档丰富（ACCEPTANCE_REPORT/TEST_AND_PLATFORM_EVIDENCE/PLATFORM_VALIDATION） | **已执行的手工验收记录为零**：WP-30B manual acceptance 是清单非记录；WP-UI-04 M-1~M-12 untouched；macOS 本地 NOT_RUN；仅 1 张设计期示意图 |

## 汇总统计

| 状态 | 数量（按子项计，共 43 项） |
|---|---|
| ✅ IMPLEMENTED | 20 |
| 🟡 PARTIAL | 12 |
| 📄 DESIGNED（仅文档） | 4 |
| 💡 PROPOSED | 4 |
| ⛔ NOT_STARTED | 13（含 DESIGNED+NOT_STARTED 复合项按主状态计） |
| ⚠️ NEEDS_REVIEW | 1 |

**一句话**：持久化与生命周期层 80% IMPLEMENTED；呈现管线骨架 IMPLEMENTED 但内容链路 NOT_STARTED；Map Engine 与编辑器交互层主体 NOT_STARTED/DESIGNED；GUI 平台验收证据整体缺失。
