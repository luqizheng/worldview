# WP-00 — Whole-product Capability Inventory（全能力盘点）

- 仓库：`github.com/luqizheng/worldview`，分支 `master`，HEAD `30527eb31d73452703b1bd53c22cea5cce112aa9`
- 审计日期：2026-11-23；审计方式：只读取证 + 本机实测 build/test
- 状态词汇：`IMPLEMENTED_VERIFIED`（代码+通过测试/运行证据）/ `IMPLEMENTED_UNVERIFIED`（有实现缺验证）/ `PARTIAL` / `DESIGNED_ONLY` / `MISSING` / `BLOCKED` / `NOT_APPLICABLE`
- 所有路径相对 `WorldView/`（除非另注）。`src/`=`WorldView/src`，`tests/`=`WorldView/tests`

**全局警示（适用于全部 Core Domain `IMPLEMENTED_VERIFIED` 条目）**：本 HEAD 全部正式领域写路径（对象/定义/Initial State/几何原子发布/Map/Branch/字段修订/资产验证）**只到 Domain 服务 + SQLite 端口，生产代码几乎无调用方——仅测试可达**（例外：项目创建播种 `ObjectTypeSeedingService` 经 `CoreDomainAdapters.cs:50` 生产可达）。Application 生产代码实际使用的只有 Project 生命周期与 3 个读投影端口。`IMPLEMENTED_VERIFIED` 表示"该层能力已正式化且经测试"，**不表示"用户可操作"**。

---

## 0. 工程与基线事实

| 事实 | 证据 |
|---|---|
| Solution：`WorldView/WorldView.sln`，7 项目，net11.0（`Directory.Build.props`：Version=0.1.0、IsPackable=false） | sln/csproj |
| 源码规模：Domain 39 .cs/6871 行；Application 41 .cs/5279 行；Infrastructure 36 .cs/14606 行；**Desktop 仅 6 .cs/580 行 + 3 AXAML**；测试 52 .cs/15720 行 | find 统计（排除 obj/bin） |
| 依赖方向：Domain ← Application ← Infrastructure（端口适配）← Desktop（组合根）；Infrastructure 不被 Desktop 的 View/VM 引用（唯一 using 在 `Program.cs:11-17`） | csproj + grep |
| 包：Avalonia 12.1.6、CommunityToolkit.Mvvm 8.4.2、MEDI 11.0.0、EF Core Sqlite 11.0.0、Microsoft.Data.Sqlite 11.0.0、MackySoft.Json.Canonicalization 0.1.0、Corvus.Text.Json 5.8.11、Newtonsoft.Json 13.0.4、xunit.v3 4.0.3 | csproj PackageReference |
| **否定性事实**：全仓无 SkiaSharp/NetTopologySuite/Mapsui/proj4/BruTile/SpatiaLite 等任何几何、投影、渲染、瓦片库；无 `Math.Sin/Cos/Sqrt` 等空间计算 | csproj + grep |
| EF Core 迁移 5 个（20261122_090235 初始 → 20261123_090000 Settings）+ 迁移外 `wv_bootstrap_metadata`；27 张 `core_*` 业务表 | `src/WorldView.Infrastructure/Persistence/CoreDomain/EfCore/Migrations/` |
| 本机验证：Debug 构建 0 错误；**700/700 测试通过**；CI HEAD run 45183823988 四 job 全绿 | `TEST_AND_PLATFORM_EVIDENCE.md` |

---

## 1. Desktop Application

| # | 能力 | 状态 | 代码证据 | 测试 | 缺口 |
|---|---|---|---|---|---|
| 1.1 | 应用入口/启动流程 | IMPLEMENTED_VERIFIED | `src/WorldView.Desktop/Program.cs:23-56`（[STAThread]、DI 组合根 `:64-122`、停机 `CloseAllAsync().GetAwaiter().GetResult()` `:44-45`） | 无直接测试；组合根装配经 `Cd01EndToEndTests` 镜像验证 | 进程异常钩子只写 `Console.Error`（`:29,34`）而 OutputType=WinExe → GUI 下致命异常不可观测；`ApplicationBootstrapper(log: null)`（`:77`） |
| 1.2 | 主窗口/窗口生命周期 | IMPLEMENTED_VERIFIED | `App.axaml.cs:24-29`（MainWindow=StartWindow）；Owner 链 `StartWindow.axaml.cs:38`；VM→窗口关闭联动 `ProjectWindow.axaml.cs:13-29` | `SessionUseCaseTests.cs:449-450`（Registry 关停） | **ProjectWindow 点 X 不关会话**（数据库句柄占用至停机兜底，重开同包会命中 DuplicateOpen 二开窗口） |
| 1.3 | 导航/菜单/工具栏/状态区 | PARTIAL | `ProjectWindow.axaml:19-23`（仅 1 菜单项"关闭项目"）；状态区为自绘 Border+TextBlock（`StartWindow.axaml:16-19`、`ProjectWindow.axaml:25-32`）；无 ToolBar | 无（纯 XAML） | 距"可用导航壳"差距大，但 M1 仅需最小壳 |
| 1.4 | Project 创建/打开/关闭 | IMPLEMENTED_VERIFIED | `CreateProjectUseCase.cs:103-179`（临时目录→manifest→bootstrap→种子→原子发布）；`OpenProjectUseCase.cs:124-281`（五道启动闸）；关闭 `ProjectSession.cs:208-253`（子工作区→AssetStore→DB 逆序、幂等） | `SessionUseCaseTests.cs`（20 例）；`Cd01EndToEndTests.cs:61-115`（真实 SQLite 全链：创建→重开→查询→工作区） | 无独立 CloseProjectUseCase（语义由 Session 承担，架构归类注记） |
| 1.5 | 最近打开项目 | IMPLEMENTED_VERIFIED | `JsonRecentProjectsStore.cs:34-108`（去重置顶、10 条上限、静默降级、三分支路径 `:110-126`）；UI `StartViewModel.cs:59-67,117-138` | **无**（tests 全域 grep 零命中） | 存储读写/截断/降级行为零测试 |
| 1.6 | 设置和配置 | PARTIAL | `SettingsDocumentService.cs:14`（不可变文件发布+SQLite 修订+SHA-256，**已完整实现**）；启动恢复闸已接线 `Program.cs:87` | `SettingsDocumentTests` 等 3 文件 | **生产未接线**：服务未注册 DI、无设置 UI；唯一生产消费者是打开时的恢复对账 |
| 1.7 | 文件选择器 | IMPLEMENTED_VERIFIED | `StartWindow.axaml.cs:44-48,59-63`（2 处 `StorageProvider.OpenFolderPickerAsync`） | 无 | 两个 handler 为无保护 `async void`（异常逃逸到 stderr 钩子） |
| 1.8 | 异常处理和日志 | PARTIAL | `FileAppLog.cs:14`（按日滚动、三分支路径）；UseCase 层 try/catch 完整（`CreateProjectUseCase.cs:168-178` 等）；VM 局部 catch | UseCase 异常路径有测试 | 进程级钩子未接 `FileAppLog`；无 `DispatcherUnhandledException`；窗口层 async void 无保护 |
| 1.9 | DPI/窗口缩放/多显示器 | PARTIAL | 窗口固定尺寸+Min（`StartWindow.axaml:10-11`、`ProjectWindow.axaml:11`）；`app.manifest` 仅 supportedOS 声明，**无 DPI 节点** | 无 | 全靠 Avalonia 默认行为；因无渲染表面，DPI 处理暂无适用对象 |
| 1.10 | Windows/macOS 原生差异 | PARTIAL | Desktop 零平台分支；差异沉在 Infrastructure（`JsonRecentProjectsStore.cs:110-126`、`FileAppLog.cs:22-26`、`SettingsDocumentPaths.cs:53-55` Windows 大小写语义） | CI 双 OS 矩阵 | 无 macOS 窗口行为适配代码；GUI 实际运行验证缺失（见 §7） |
| 1.11 | 安装包/发布包/运行时依赖 | MISSING | csproj 无 PublishSingleFile/SelfContained/RuntimeIdentifier；无 *.pubxml；CI 无发布 job；无 installer 工程 | 无 | 属桌面交付与发布准备，归 **WP-10 / GAP-P1-15**；不构成 M1 的独立阻塞项（M1 平台门是 GUI 流程证据，见 GAP-P0-4）。当前仅 framework-dependent 本地产物 |
| 1.12 | UI 层级合规 | IMPLEMENTED_VERIFIED | Desktop 全部 Infrastructure using 仅 `Program.cs:11-17`（组合根）；ViewModel 仅依赖 Application 抽象（`StartViewModel.cs:7-9`） | `Cd01EndToEndTests` 镜像组合根 | 无越层；AGENTS.md 约定一致 |
| 1.13 | 死代码 | — | `StartViewModel.cs:90-93`（CoreDomainProjectCreationUnavailable）与 `:142-145`（ProjectIdentityUnavailable）两分支**确认不可达**（`Program.cs:83-85` 无条件注册正式实现） | — | 防御性历史遗留，建议随 WP-10 清理 |

**直接回答**：Q1 能启动（代码角度，组合根完整、无启动即失败点；本审计未做 GUI 冒烟）。Q2 主窗口可用（StartWindow：项目创建区/打开区/最近列表+状态条；ProjectWindow：菜单+项目身份+地图列表+会话列表+状态条）。Q3 Project 生命周期完整（创建→打开→关闭→退出→重开全链，`Cd01EndToEndTests.cs:61-115` 端到端覆盖）。Q4 占位物：Settings 服务未接线、WorkspaceViewState 全占位、两个不可达死分支。Q5 Windows 有构建+测试+CI 证据（GUI 冒烟未做）；macOS 仅 CI build+test，GUI 运行证据为零。

---

## 2. Project / Map Management（20 操作逐项）

| # | 操作 | 状态 | 证据 | 测试 | 缺口 |
|---|---|---|---|---|---|
| A1 | Project 包创建 | 已实现且有测试 | `CreateProjectUseCase.cs:77-266`；正式创建服务 `CoreDomainAdapters.cs:20-75`；组合根 `Program.cs:83-84` | `SessionUseCaseTests.cs:17-156`；`CoreDomainPersistenceTests.cs:114-174` | 无 |
| A2 | Project 打开（五闸） | 已实现且有测试 | `OpenProjectUseCase.cs:124-281`（quick_check `:187`→bootstrap `:193-201`→身份 `:203-222`→Field `:225-241`→Settings `:243-258`；失败逆序释放 `:283-289`） | `SessionUseCaseTests.cs:159-315`；Settings 闸 `SettingsDocumentPersistenceTests.cs:88-225` | Field/Settings 闸为告警不阻断（设计语义） |
| A3 | Project 关闭 | 已实现且有测试 | `ProjectSession.cs:208-253`；`ProjectSessionRegistry.cs:71-79`；UI `ProjectWorkspaceViewModel.cs:144-149` | `SessionUseCaseTests.cs:327-346,449-464` | 无独立 UseCase 类（注记） |
| A4 | Project Session 生命周期 | 已实现且有测试 | `ProjectSession.cs:31`（八态状态机 `:11-21`）、`ProjectSessionRegistry.cs:16` | 晚完成保护 `:404-424` 等 | 无 |
| A5 | 最近打开项目恢复 | 已实现但缺测试 | `JsonRecentProjectsStore.cs:34-108` + `StartViewModel.cs:59-67,117-138` | **无** | 零测试 |
| B6 | **Map 创建** | **部分实现（用户不可达）** | 领域齐备：`MapBranchServices.cs:12-28`、`MapCreation.cs:25-48`、端口 `ICoreDomainDatabase.cs:55`、实现 `SqliteCoreDomainDatabase.cs:194`；**Application/Desktop 对 MapService 零调用**；UI 无入口 | `MapBranchRulesTests.cs`；端到端 `Cd01EndToEndTests.cs:84-93`（测试代码手工调服务） | **用例层+UI 两层缺失——产品内用户无法创建 Map（M1 关键断点）** |
| B7 | Map 列表读取 | 已实现且有测试 | `IMapQueryService.cs:15-22`；`CoreDomainAdapters.cs:126-152`（跨项目隔离）；UI `ProjectWorkspaceViewModel.cs:66-90` | `Cd01EndToEndTests.cs:90-93,147-152` | VM 层无单测（门槛/异常分支） |
| B8 | Map 打开（工作区） | 已实现且有测试 | `ProjectSession.cs:144-180`（EnsureReady→GetMap→Resolve→挂载；晚到达保护） | `SessionUseCaseTests.cs:327-346,366-424` | UI 恒传 `branchId:null, historicalTimeInput:null`（`ProjectWorkspaceViewModel.cs:105-107`） |
| B9 | Map 关闭（关工作区） | 已实现且有测试 | `ProjectSession.cs:183-202`；`MapWorkspaceSession.cs:82-96` | `SessionUseCaseTests.cs:366-381` | 无 |
| B10 | Map 重命名 | 完全缺失 | 端口无 RenameMap；`MapRecord.MapName` init-only（`MapRecords.cs:19-20`）；唯一 grep 命中是 Building payload 测试字符串 | 无 | 四层全无；历史不可变语义待正式化 |
| B11 | Map 复制 | 完全缺失 | 无 Duplicate/CopyMap API | 无 | 四层全无；涉 ADR-002/005 世系语义未设计 |
| B12 | Map 删除（实体级） | 完全缺失 | 端口无 DeleteMap；`UnregisterMapObject` 只删注册行；包级仅自建 `.tmp-` 清理（`CreateProjectUseCase.cs:251-265`）；`ProjectSession.cs:29` 明确"关闭永不删包" | 无 | 四层全无；受历史不可变语义约束 |
| B13 | Map Workspace Session 生命周期 | 部分实现 | `MapWorkspaceSession.cs`（Context 不透明占位 `:35`；`SwitchContextAsync` `:49-79` **零调用零测试死代码**；Close/Dispose 幂等） | `SessionUseCaseTests.cs:384-401`（经 ProjectSession 间接） | ViewState 全占位零消费者 |
| C14 | Branch 列表读取 | 完全缺失 | `ICoreDomainDatabase.cs:63-67` 仅 FindBranch/FindBranchByOwnHistory，无 List | 无 | 端口层即不存在 |
| C15 | Branch 选择 | 部分实现 | 领域解析支持 branchId（`WorkspaceContextResolution.cs:69-95`）；会话参数 `ProjectSession.cs:146` | `WorkspaceContextRulesTests.cs:47-59` | UI 恒传 null，无选择器 |
| C16 | Branch 切换 | 部分实现 | `MapWorkspaceSession.SwitchContextAsync`（`MapWorkspaceSession.cs:49-79`） | **无** | 零调用者零测试无 UI（死代码） |
| C17 | Historical Time 上下文 | 已实现且有测试 | `WorkspaceContextRules.TryResolve`（`WorkspaceContextResolution.cs:36-124`，null→纪元）；`CoreDomainAdapters.cs:160-200`（InSnapshotScope 原子快照） | `WorkspaceContextRulesTests.cs`；端到端 `Cd01EndToEndTests.cs:90-93` | UI 无时间输入入口（恒传 null→纪元） |
| C18 | 最近打开 Map 恢复 | 完全缺失 | grep LastMap/ActiveMap/recentmap 零命中；Settings 文档无此字段 | 无 | 无任何实现或字段 |
| D19 | UI 直接操作数据库 | **否（合规）** | csproj 依赖单向；Desktop 唯一 Infrastructure using 在组合根；无任何 Sqlite 类型 using | `Cd01EndToEndTests` 镜像组合根 | 无 |
| D20 | 绕过正式服务 | **否（合规）** | UI 调用清单：CreateProject/OpenProject/RecentProjectsStore/ProjectSession/IMapQueryService/IWorkspaceContextResolver 全部经 Application | `Cd02MapEditorIntegrationTests` 头注释声明 | 问题不在越界，在 Application 层缺 Map/Branch 用例 |

---

## 3. Core Infrastructure

分层标注：**D**=Domain Service 已实现，**I**=Infrastructure Adapter 已实现，**DB**=数据库结构已存在，**T**=端到端测试已通过，**UI**=UI 已接通。

### 3.1 已实现且经测试的核心能力（D+I+DB+T 齐备）

| # | 能力 | 分层 | 代码证据 | 测试 |
|---|---|---|---|---|
| 3.1 | Object Type Registry（封闭 12 类型） | D+I+DB+T | `Domain/Registry/ObjectTypeRegistry.cs`（fail-closed 加载 `:279-308`、pin 权威、`ValidateDefinition` `:170`）；`BuiltinObjectTypes.cs`（12 键）；播种 `ObjectTypeSeedingService.cs:17-40` **经项目创建生产可达**（`CoreDomainAdapters.cs:50`） | `ObjectTypeRegistryTests.cs`；`Cd02ObjectPersistenceTests.cs::ProjectCreation_SeedsTwelveTypes` |
| 3.2 | Object Identity/Reservation（Reserve→Bind→Insert 三段原子） | D+I+DB+T | `ObjectIdReservationService.cs`；`ObjectCreationService.cs`（TX-55 原子）；`SqliteCoreDomainDatabase.Objects.cs:67-155`（request_key UNIQUE 幂等+恢复） | `ObjectDomainServiceTests.cs`（25）；`Cd02ObjectPersistenceTests.cs`（27） |
| 3.3 | Shared Definition（CAS + canonical JSON 指纹） | D+I+DB+T | `CanonicalJson.cs`（RFC 8785）；`MapInitialStateService.cs:18-79`；`SqliteCoreDomainDatabase.Objects.cs:337-634` | `Definition_SingleRow_CasStaleRejected_HashTamperFailClosed` 等 |
| 3.4 | Descriptor Schemas（有界 Draft 2020-12，fail-closed） | D+I+T | `JsonSchemaSubset.cs`；嵌入资源 p2-09-38/-39 v0.5（building 22 defs/cave 14 defs）；`ObjectTypeRegistry.ValidateDefinition` **已接入** ObjectCreationService/ObjectDefinitionService/adapter | `JsonSchemaSubsetTests.cs`（15）；`Cd02AggregateVectorConformanceTests.cs`（55 向量全执行） |
| 3.5 | Building/Cave typed aggregates（四层校验+原子发布） | D+I+DB+T | `BuildingCaveAggregateValidator.cs`（~680 行，`STALE_DEFINITION_CAS` `:476-481`）；`PublishDefinitionWithGeometryAssets`（仅 building/cave_system，`MapInitialStateService.cs:114-118`）；发布事务内重读 Verified 门（`SqliteCoreDomainDatabase.Objects.cs:675-727`） | 55 向量；`GeometryAtomicPublicationTests`（8，全无部分写入）；`Cd02MapEditorIntegrationTests`（ME-04 全链） |
| 3.6 | Map/Branch/History 结构层 | D+I+DB+T | `MapRules.CreateMap`（Map+根 History+Calendar+day-zero 单事务）；`BranchService.CreateBranch`（祖先链预检）；`BranchLineageResolver`（环/悬空/跨 Map/深度 1000 拒绝）；写入点 `SqliteCoreDomainDatabase.cs:194-264,313-356` | `MapBranchRulesTests`；`BranchLineageTests`；`PersistenceDomainIntegrityTests`（Lineage_* 10） |
| 3.7 | Map Initial State（CAS 指纹基线） | D+I+DB+T | `MapInitialStateService.cs:222-328`；`InitialStateFingerprint`（`initial-state-fingerprint-v1` 顺序无关）；P3⇒P2 服务检查+复合 FK | `InitialState_*` 5 项 |
| 3.8 | Asset 所有权/完整性（不可伪造凭证） | D+I+DB+T | `AssetService.cs`（内容寻址幂等 `:39-43`；Verified-only 引用 `:95-99`）；`AssetByteDigest`（internal MeasureAsync+friend 白名单）；`FileAssetStore.cs`（staging→hash→原子发布）；孤儿只读上报 `ICoreDomainDatabase.cs:205-206` | `AssetIntegrityVerificationTests`（11，含反射探测防伪造 `:249`）；`Cd02AssetPersistenceTests` |
| 3.9 | Field Dataset/Revision（追加不可变） | D+I+DB+T | `FieldDatasetService.cs`（`PublishRevision` ~200 行 manifest 校验+CAS）；`CoreDomainV1ModelConfiguration.cs:230`（唯一 EF 并发令牌）；不可变触发器 | `Cd02FieldDatasetPersistenceTests`（43） |
| 3.10 | HistoricalTime / Calendar 占位 | D+I+DB+T（Calendar 为结构占位） | `HistoricalTime.cs`（day≥0/offset 全序）；`CalendarRecord.CalendarType=null` | `HistoricalTimeTests`（10） |
| 3.11 | Workspace 上下文解析 | D+I+T | `WorkspaceContextRules.TryResolve`（四步，`WorkspaceContextResolution.cs:36-124`；`:34` 注释显式声明"不做 Event Replay/World State 重建"） | `WorkspaceContextRulesTests`（8） |
| 3.12 | 事务协调器 | I+T | `SqliteConnectionAccess.cs:24-165`（单连接 Monitor 临界区；**全仓唯一原生 SqliteConnection 构造点 `:66`**；Dispose fail-closed）；PRAGMA foreign_keys=ON/busy_timeout=5000/Pooling=false | `PersistenceConcurrencyTests`（9：共享协调器/Dispose 后拒绝/飞行中 Dispose 文件完好） |
| 3.13 | 项目包格式与校验 | D+I+T | `PackageManifest.cs`（严格解析+版本门）；`ProjectPackageValidator.cs`（5 阶段）；`ProjectPackageFormat.cs`（Backups/ 目录占位） | `ProjectPackageValidatorTests`；`RealPackageFlowTests` |
| 3.14 | 启动恢复/对账 | D+I+T | Field payload 双步孤儿回收（`CoreDomainFieldPayloadRecovery.cs:11-45`）+ Settings 对账；**Backups/ 目录无任何写入者** | `ProjectOpenRecovery_*`；`StartupRecovery_FailsClosed*` |
| 3.15 | Field Package 全链（Writer/Reader/Importer/Exporter，fail-closed） | D+I+T | `FieldPackage*.cs`（三层 hash 分离、Profile Gate、owner 上下文重解析） | Authority 19 + Import 16 + Conformance 等 |

### 3.2 设计态/缺失的大块

| # | 能力 | 状态 | 证据 |
|---|---|---|---|
| 3.16 | Command/Event 写入路径 | **DESIGNED_ONLY** | `core_command_asset` 仅注释预留（`CoreDomainInitialSql.cs:374`）；descriptor `CommandCapabilities` 已解析入内存（`ObjectTypeRegistry.cs:107,474-476,515`）但**消费方为零**；无 CommandBus/Dispatcher/Record |
| 3.17 | Checkpoint | **DESIGNED_ONLY** | src+tests `checkpoint` 零命中；设计在 `docs/P2-01-D01/18-checkpoint-topic-d01-101-143.md` + `p2-09-11 v0.10:290-294`（core_checkpoint 表规格四态） |
| 3.18 | History 状态重建 | **DESIGNED_ONLY**（写入点已实现） | core_history 仅 map_root/branch_owned 两处创建写入；无按时间点重建读路径；`WorkspaceContextResolution.cs:34` 显式免责 |
| 3.19 | Relationship 层 | **MISSING** | src 唯一命中 `CoreDomainInitialSql.cs:300` 注释；descriptor `core.relationship.establish/end` 纸面声明（p2-09-15 v0.5:92-93）vs 执行器缺位 |
| 3.20 | 几何校验器接入 | **PARTIAL（结构性脱节）** | `GeometryAssetEnvelope.Validate`/`GeometryRules.Validate*` 生产零调用（仅测试引用；src 命中仅 `GeometryAssetEnvelope.cs:70,144,146` 引用 SupportedDimension 常量）；生产发布走 registry schema + `ValidateGeometryPublicationContext`（**不校验 envelope JSON 内容**） |
| 3.21 | 应用层读模型 | **PARTIAL（3/8）** | 已接通 UI：地图摘要/工作区上下文/项目身份；缺失：对象列表、几何、资产、关系、Field 读模型（Domain 端口裸露于 `ICoreDomainDatabase.cs:144,164,183,231`，无 Application 投影封装） |
| 3.22 | 对象 retire / runtime attribute change / split-merge | **MISSING（设计内拒绝）** | `GeometryOperationRules.RejectSplitMerge()` 恒抛；retire 无服务入口 |
| 3.23 | Spatial Site / Group | **MISSING** | Domain 内 "site/group" 仅校验器局部变量（`BuildingCaveAggregateValidator.cs:667,669`） |
| 3.24 | WAL / 备份 | **DESIGNED_ONLY（已声明 open gate）** | WAL：`SqlitePackageDatabase.cs:32-33` 注释（依赖备份/恢复证明）；Backups：目录占位无写入者 |

**重点回答**：① Command/Event 正式写入路径**不可用**（纯设计态）。② History **不能**重建状态（只有世系解析与结构写入）。③ Branch 有完整结构行为（创建/世系/闭边界继承，测试充分），但**无运行时切换的任何消费**。④ 几何**可以**从正式数据读取——但只有字节级（`worldview.geometry-json` Asset），从未解析成结构化对象。⑤ 资产引用与文件一致性机制完备（三层+凭证+对账），但 `VerifyAssetIntegrityUseCase`/`FindOrphanAssetIds` 生产零调用。⑥ 关系**不**持久化（层不存在）。⑦ 只有表没有服务路径的功能：`core_command_asset`（预留）、relationship（无表）、checkpoint（无表）——三者均为"设计内缺席"，无"有表无服务"的半成品。

---

## 4. Map Rendering（独立于 Map Authoring 审计）

**总判定：渲染子系统在当前 HEAD 不存在于任何形态。** 21 项中 19 项 MISSING、2 项不适用。四方交叉证实：AXAML 控件树（无 Canvas/Viewport/自定义控件）、csproj（无 SkiaSharp）、solution 构成（无 Presentation/Renderer 项目）、官方审计 `docs/map-engine/PRA-01_IMPLEMENTATION_AUDIT_v0.1.md`（自判 NOT IMPLEMENTED，验收矩阵 10 项中 9 项 Not implemented）。代码注释自证为**显式阶段决策**（`ProjectWindow.axaml:15`"本窗口为文本基线：地图渲染属 PRA-01/PRA-02 之后的阶段"）。

| # | 能力 | 状态 | 关键证据 |
|---|---|---|---|
| 4.1 | Presentation Context | MISSING | `grep PresentationContext|RenderContext` 零命中；最近似物 `ValidatedWorkspaceContext`（`IMapQueryService.cs:104-115`）自标"不透明占位"，无 WorldStateRevision/投影身份/指纹 |
| 4.2 | Immutable Scene Snapshot | MISSING（文档 DESIGNED_ONLY） | grep 零命中；设计在 `PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md` + PRA-01 契约文档 |
| 4.3 | Scene Builder/Assembler | MISSING | grep 零命中 |
| 4.4 | Snapshot Publisher | MISSING | grep 零命中（`FieldPayloadPublisher` 与场景无关） |
| 4.5 | Avalonia Viewport 控件 | MISSING | Desktop 3 个 AXAML 全文：无 Canvas/Viewport/UserControl/DrawOperation；ProjectWindow=菜单+列表+状态条 |
| 4.6 | Skia 绘制实现 | MISSING | 全仓 Skia 标识符零命中；无 SkiaSharp 包（Avalonia 内部引擎 ≠ 本仓代码） |
| 4.7 | Map-to-screen 变换 | MISSING | `WorldToScreen/MapToScreen/Matrix` 零命中；`WorkspaceViewState.CameraX/Y/Zoom`（`MapWorkspaceSession.cs:112-118`）自标"占位"，**唯一消费者是单元测试**（`SessionUseCaseTests.cs:393,397`） |
| 4.8 | Screen-to-map 变换 | MISSING | `ScreenToWorld` 零命中 |
| 4.9 | Pan/Zoom | MISSING | 无 PointerWheel/地图缩放；唯一 Scroll 命中是启动页表单 ScrollViewer |
| 4.10 | Resize/DPI | MISSING | `SizeChanged|Scaling|Dpi` 零命中 |
| 4.11 | Layer visibility/ordering | MISSING | `Layer` 于 src 零命中（仅 docs 命令目录约束） |
| 4.12 | Render item | MISSING | `RenderItem|DrawOperation` 零命中 |
| 4.13 | Geometry drawing | MISSING | Desktop 无任何形状绘制 API（Path 命中均为 System.IO.Path） |
| 4.14 | Text/Label 渲染 | MISSING | TextBlock 均为窗口 UI 文本；地图标注渲染零实现 |
| 4.15 | Symbol/Style | MISSING | 唯一 Style 是 Avalonia FluentTheme |
| 4.16 | Raster/Field rendering | MISSING | tile 字节仅存在于打包/存储层，无渲染消费 |
| 4.17 | LOD | MISSING（文档约束 DESIGNED_ONLY） | 代码零命中；docs 反复声明 LOD 为派生非权威数据 |
| 4.18 | Cache/Invalidation | MISSING | `Cache|Invalidate` 于 Desktop/Application 零命中 |
| 4.19 | Hit testing | MISSING | `HitTest|Pointer*` 全 src 零命中 |
| 4.20 | 渲染资源生命周期 | NOT_APPLICABLE | 无渲染资源；现存 Dispose 均为会话生命周期 |
| 4.21 | Renderer error handling | NOT_APPLICABLE（应用级边界存在） | `Program.cs:25-33` 全局钩子 + VM catch；不覆盖绘制路径 |

**重点回答**：Q1 无可运行 Map Viewport。Q2 无 Skia 绘制代码。Q3 现状**优于**空白页——是纯文本管理界面（地图列表为真实数据）。Q4 Pan/Zoom 不工作（占位属性零 UI 消费）。Q5 无 Scene Snapshot 流程（一个环节都没有）。Q6 无端到端路径（真实数据链止步于 `MapSummary` 两字段元数据 → 文本行）。Q7 UI 直读数据库：无违规（会话绑定端口注入，`Program.cs:112-113` 为组合根合法职责）；Renderer 修改 Domain：客体不存在。相邻已知风险：PRA-01 官方审计 PRA01-AUD-03（可变 `WorkspaceViewState` 接入后台渲染前必须防御性拷贝）、PRA01-AUD-05（ProjectWindow 无渲染消费者）。

---

## 5. Map Authoring

**总判定：编辑链路 19 项中 17 项 MISSING；领域侧 CAS 三机制与 Building/Cave 采纳链 IMPLEMENTED_VERIFIED。** UI 唯一能触达的写路径是 Project 创建。任何空间编辑操作（临时或正式）均不存在。

| # | 能力 | 状态 | 关键证据 |
|---|---|---|---|
| 5.1 | Selection | MISSING（占位） | `MapWorkspaceSession.cs:124` `SelectedItemIds` 占位 HashSet 零消费 |
| 5.2 | Inspector | MISSING | grep `Inspector/PropertyGrid/DetailsPanel` 零命中 |
| 5.3 | Tool Registry | MISSING（占位） | `MapWorkspaceSession.cs:121` `ActiveTool` 占位字符串零读写方 |
| 5.4 | Gesture/Input Routing | MISSING | `Pointer*` 于 Desktop 零命中 |
| 5.5 | Temporary Geometry | MISSING | 无编辑暂存几何类型（`Draft` 是对象数据生命周期状态，勿混淆） |
| 5.6 | Staging（编辑暂存） | MISSING | 现有 Staging 全部为文件发布暂存（`ProjectPackageFormat.cs:33` 等） |
| 5.7 | Preview | MISSING | grep 零命中——连"临时预览"本身都不存在 |
| 5.8 | 编辑期验证 UI | MISSING | `ValidationResult/DiagnosticsPanel` 零命中（领域校验密集存在，是另一回事） |
| 5.9 | Diagnostics | PARTIAL | `Diagnostic` 命中为错误字符串/日志字段（`MapWorkspaceSession.cs:65` 等），无诊断面板 |
| 5.10 | Candidate/Change Set | MISSING | grep 零命中 |
| 5.11 | Apply/Cancel（编辑器语义） | MISSING | 仅有会话关闭命令，无修改集提交/回滚 |
| 5.12 | Undo/Redo | MISSING | 穷尽 grep 仅标识符子串误命中；ADR-004 纯设计 |
| 5.13 | Freshness Check | 领域侧 IMPLEMENTED_VERIFIED；编辑器侧 MISSING | 三类 CAS：Definition hash（`BuildingCaveAggregateValidator.cs:29-31,476-481`）、Initial-state 指纹（`MapInitialStateService.cs:229-274`）、`CurrentFieldRevisionId` 并发令牌（`CoreDomainV1ModelConfiguration.cs:230`）；操作前会话新鲜度闸不存在 |
| 5.14 | Formal Domain Operation Adapter | MISSING | grep `OperationAdapter/DomainOperation/AuthoringOperation` 零命中；正式写路径无用例层（全局警示） |
| 5.15 | Outcome Reconciliation | MISSING | 现有 Reconcile 均为启动恢复（Field/Settings），非编辑结果对账 |
| 5.16 | Building/Cave 编辑 UI | **BLOCKED**（领域侧完备） | UI grep `building|cave` 零命中；领域采纳链全备（3.5）；阻塞点在编辑链路（5.1-5.15）而非领域 |
| 5.17 | Spatial Site/Group | MISSING | 零概念命中 |
| 5.18 | Typed Connections | MISSING | Relationship 层不存在（3.19）；descriptor 纸面声明 vs 执行器缺位 |
| 5.19 | Templates/Modules | MISSING | `Module/Recipe` 零命中；Template 命中均为 Avalonia UI 模板 |

**重点回答**：Q1 只有设计的：5.1-5.12、5.14-5.19（ME-01-06/ME-02~08 系列工作基线文档 vs 代码零实现）。Q2 已存在的 UI：两个文本窗口。Q3 有真实 Domain 写入路径的"工具"：仅 Project 创建（UI 可达）与 Field Package 链（程序化，无 UI）；其余写服务仅测试可达。Q4 只能临时预览的操作：无（预览本身不存在）。Q5 被 Core Domain 阻塞的：Relationship 执行器、Command/Event 层、Site/Group 持久化、Initial State typed schema（OD-1 开放）。Q6 绕过 Core Domain 的直接写入：**无**（依赖方向单向 + UI 调用清单全经 Application；`Cd02MapEditorIntegrationTests` 头注释声明"无 UI 直写 SQLite"）。

---

## 6. Tests / CI

| 项 | 状态 | 证据 |
|---|---|---|
| Unit tests（Domain/Application，内存替身） | IMPLEMENTED_VERIFIED | 700/700（本机实测）；`FakeCoreDomainDatabase`（920 行）、`FakePackageDatabase`、`InMemoryFileSystem` |
| 真实 SQLite 集成/端到端 | IMPLEMENTED_VERIFIED | Infrastructure.Tests 全部 21 类用真实 SQLite（每类独立临时目录）；主端到端：`Cd01EndToEndTests`（创建→重开→查询→工作区全链，镜像组合根）、`RealPackageFlowTests`、`Cd02MapEditorIntegrationTests` |
| UI Tests | **MISSING** | 3 个测试工程均不引用 WorldView.Desktop；无 Avalonia headless 测试 |
| 应用启动/关闭测试 | MISSING | 停机逻辑 `Program.cs:44-45` 无覆盖 |
| Renderer 生命周期测试 | NOT_APPLICABLE | 渲染器不存在 |
| Windows Debug/Release | IMPLEMENTED_VERIFIED | 本机 Debug 实测 + CI 四矩阵 |
| macOS Debug/Release | IMPLEMENTED_UNVERIFIED（本地 NOT_RUN） | 仅 CI build+test 绿；本机为 Windows |
| GitHub Actions | IMPLEMENTED_VERIFIED | `.github/workflows/dotnet-ci.yml`：push(master)+PR+dispatch；矩阵 windows-latest/macos-latest × Debug/Release；`working-directory: WorldView`；MTP `--show-slowest-tests 20`；无发布 job、无 Linux、无覆盖率上传 |
| 本地构建脚本 | IMPLEMENTED_VERIFIED | `WorldView/scripts/build|run|test|verify-environment.{ps1,sh}`（薄封装） |
| 打包/安装 | MISSING | 同 1.11 |
| 测试栈 | — | xunit.v3 4.0.3 + MTP（global.json）+ VSTest 适配器并存（冗余配置，`Microsoft.NET.Test.Sdk` 18.12.0 + `xunit.runner.visualstudio` 4.0.1） |
| 规模 | — | 49 测试类 / 515 [Fact] / 28 [Theory]（Theory 展开后更高，如 55 条聚合向量 JSON 驱动） |

**已知的覆盖缺口清单**：Desktop 工程 0 覆盖；`JsonRecentProjectsStore` 0 覆盖；`MapWorkspaceSession.SwitchContextAsync` 0 覆盖（死代码）；EF 迁移防降级守卫（`__EFMigrationsHistory` 未知版本拒绝）无专门测试；VM 层（门槛/异常分支）无单测。

---

## 7. Windows / macOS Platform Evidence

详细证据见 `TEST_AND_PLATFORM_EVIDENCE.md`。摘要：

| 项 | Windows | macOS |
|---|---|---|
| 构建+测试 | ✅ 本机实测 700/700（Debug）+ CI 4 job 绿 | ✅ 仅 CI（run 45183823988 macos Debug/Release success）；本地 NOT_RUN |
| GUI 应用实际运行 | ❌ 本审计未执行 UI 冒烟（历史会话亦无正式冒烟记录） | ❌ **零证据**（无 macOS UI 冒烟/无头 Avalonia 测试；macOS job 仅测试进程） |
| 平台代码分支 | `JsonRecentProjectsStore.cs:110-126`、`FileAppLog.cs:22-26`、`SettingsDocumentPaths.cs:53-55`（Windows 大小写语义）、`app.manifest`（仅 Windows 生效） | 同左三分支的 macOS 路径；无窗口行为适配 |
| 打包发布 | ❌ 无 | ❌ 无 |

结论：**"CI 在两个平台编译+测试通过"不能等同"两个平台 GUI 流程运行通过"。** M1 验收的平台门（GUI 冒烟 + macOS 全流程，正式验收项 10）当前在两个平台都未正式关闭，macOS 侧差距更大（GAP-P0-4）。打包发布链缺失是性质不同的交付准备问题，归 WP-10（GAP-P1-15），不与 GUI 运行证据混为一个问题。

---

## 8. Security / Data Integrity / Recovery

| # | 机制 | 状态 | 证据 |
|---|---|---|---|
| 8.1 | 不可伪造验证凭证（AssetByteDigest） | IMPLEMENTED_VERIFIED | `AssetByteDigest.cs:13-16`（internal MeasureAsync）；反射探测防伪造测试 `AssetIntegrityVerificationTests.cs:249` |
| 8.2 | 三类写路径 CAS（Definition hash / Initial-state 指纹 / Field revision 令牌） | IMPLEMENTED_VERIFIED | 见 5.13；固定时间比较 `FieldPackageIntegrityVerifier.cs:21-55` |
| 8.3 | 事务整体性（协调器临界区+事务内重读+回滚） | IMPLEMENTED_VERIFIED | 3.12；`PersistenceConcurrencyTests`（读不观察半成品 `:248`） |
| 8.4 | DB 级防线（9 触发器 + CHECK/UNIQUE/FK + 复合 FK P3⇒P2） | IMPLEMENTED_VERIFIED | scope 触发器 `CoreDomainInitialSql.cs:267-291`；settings 触发器 ×4；`PersistenceDomainIntegrityTests`（15） |
| 8.5 | 打开五闸（损坏 fail-closed 拒绝打开，无自动修复） | IMPLEMENTED_VERIFIED | `OpenProjectUseCase.cs:124-281`；`CorruptDatabase_RejectedByValidator`（`RealPackageFlowTests.cs:178`） |
| 8.6 | 双孤儿回收（staging 清理+内容寻址两步防误删） | IMPLEMENTED_VERIFIED | `CoreDomainFieldPayloadRecovery.cs:11-45`；**Asset 孤儿仅只读上报无回收** |
| 8.7 | 防降级（迁移历史未知版本拒绝打开） | IMPLEMENTED_UNVERIFIED | `CoreDomainEfSession.ApplyMigrations` fail-closed；**无专门测试** |
| 8.8 | WAL / 备份 | DESIGNED_ONLY | 3.24（两个已声明 open gate） |
| 8.9 | Profile Gate（golden_fixture_only 默认拒绝） | IMPLEMENTED_VERIFIED | `FieldPackagePolicy.cs`；`Import_DefaultPolicyRejectsGoldenFixtureOnlyProfile` |
| 8.10 | 单机边界（无分布式锁/协作/云同步） | 合规 | 无任何相关代码；`SqlitePackageDatabase.cs:38` 注释承认跨进程仅文件锁兜底（单机 V1 可接受） |

---

## 9. Documentation Consistency

| # | 检查 | 结果 |
|---|---|---|
| 9.1 | 必读 10 文档存在性 | 全部存在，无 Missing |
| 9.2 | ME-01 工作计划 | **文件名 v3.19、内容 v3.23**（"Supersedes: …v3.19"）——文件名与内容版本不一致，建议重命名或注明 |
| 9.3 | PRA-01 实施审计 vs 代码 | **一致**：文档自判 NOT IMPLEMENTED（验收矩阵 9/10 Not implemented），与本审计渲染层零实现结论吻合 |
| 9.4 | roadmap v1 vs ME-01 | 一致：ME-01 v3.23 明确"whole-product 序列由 roadmap V1 治理；前 ME-08 WP-00~09 保留为 specialist authoring 路线" |
| 9.5 | IMPLEMENTATION-LOG 已登记的 deferred vs 代码 | 一致：Command/Event（CD-03+）、Relationship、Checkpoint、macOS 验证、WAL 均已登记为后续阶段，代码中确无半成品冒充 |
| 9.6 | Command descriptor 目录 vs 代码 | **声明与实现断裂**（已知且已登记）：`core.relationship.establish/end` 声明 enabled（p2-09-15 v0.5:92-93）但执行器零实现 |
| 9.7 | CD-02 文档"十类型" vs 代码 12 类型 | 文档漂移残留（前序盘点 G-P1-8）：测试名 `ProjectCreation_SeedsTenTypesAndAllocators` 已改 Twelve 断言，但 `ICoreDomainDatabase.cs:30` 注释"十内置"等处仍为旧口径 |
| 9.8 | AF-02 报告的历史门槛 vs 现状 | AF-02 时代的 Core Domain 门槛已被后续 CD-01/CD-02 实施解除；`StartViewModel` 两个不可达死分支的文案仍引用"AF-02 实施报告"（过时引用） |
| 9.9 | roadmap v1 §3 自我声明 | roadmap 明示自身为"planning input, not a fresh code audit"并要求 WP-00 验证——本文件即该验证的交付 |

---

## 附录：能力状态总计

对 118 项能力（见 JSON，与本章各表一一对应）按状态统计：IMPLEMENTED_VERIFIED 45 / IMPLEMENTED_UNVERIFIED 4 / PARTIAL 13 / DESIGNED_ONLY 5 / MISSING 46 / NOT_APPLICABLE 4 / BLOCKED 1（Building/Cave 编辑 UI）。
