# BASELINE-01 审计报告：World View Master 基线审计

- **基线 SHA**：`6ec34ead1540b1c7a95f0076784452b0fbf20ae3`（origin/master，含 PR #40 与 PR #42 合并）
- **审计方式**：只读。独立 detached worktree 检出 `6ec34ea`，未修改任何业务源代码、未执行 Migration、未合并任何 PR。
- **审计日期**：2026-11-27
- **审计性质**：代码与文档证据对照审计，不采信文档自述；每个判定均附文件/类/测试/CI 证据。

---

## 0. 基线与工作树状态

| 项目 | 状态 |
|---|---|
| origin/master SHA | `6ec34ea`（Merge pull request #40 feat/wp-ra-02-a-desktop-presentation-integration） |
| 审计工作树 | `../worldview-baseline01`（detached @ 6ec34ea，clean） |
| 原工作树 | 分支 `feat/wp-ui-04b-map-session-lifecycle` @ `0479ff5`，无本地独有提交，落后 master 约 20 个提交；有未跟踪的审计/文档目录与一处 `WorldView/AGENTS.md` 本地修改（非本审计产生） |
| CI | `.github/workflows/dotnet-ci.yml`：windows-latest + macos-latest × Debug/Release 四矩阵，`dotnet build/test WorldView.sln`，全部绿（最近一次 R3-R2 复核 Run 45845579467） |

**PR 状态**

| PR | 内容 | 状态 |
|---|---|---|
| #40 | WP-RA-02-A 桌面呈现集成（切片 A 端到端接线） | ✅ 已合并（`6ec34ea`），含 R1/R2/R3 三轮整改与独立复核记录 |
| #42 | World View 开发路线图（`plan.md`，M0–M8 / Phase 0–12 / 17 项首执行队列） | ✅ 已合并（`0cb9ea6`） |
| #41 | WP-RA-03：Spatial Chunk & LOD Contract V0.1（分支 `docs/wp-ra-03-spatial-chunk-lod-contract`） | ⛔ **未合并**（`git merge-base --is-ancestor` 证实不在 master）。plan.md 明确要求先评审裁决后合并；该合同是 PRA-05（批渲染/Chunk/LOD）的设计输入 |

---

## 1. 解决方案结构

`WorldView/WorldView.sln`：5 个 src 项目 + 5 个测试项目，目标框架统一 `net11.0`（Directory.Build.props）。

| 项目 | 职责 |
|---|---|
| WorldView.Domain | 零依赖领域模型：Projects/Maps/Branch/Objects/Registry/Geometry/Assets/Fields/Time/Workspace/Aggregates |
| WorldView.Application | 用例（Create/Open Project、CreateMap、VerifyAssetIntegrity）、Session 层、Field Package 打包 |
| WorldView.Infrastructure | SQLite + EF Core 11 持久化、内容寻址 Asset Store、文件系统/日志/最近项目 |
| WorldView.Presentation | 呈现管线：PresentationContext 指纹、SceneAssembler、快照原子发布、请求协调器 |
| WorldView.Desktop | Avalonia 12.1.6 桌面壳（WinExe）：Views/ViewModels/Rendering |

测试合计 **823 个 `[Fact]/[Theory]`**（Domain 202 / Application 176 / Infrastructure 239 / Presentation 107 / Desktop 99），xunit.v3 + Microsoft.Testing.Platform。

---

## 2. 总体结论（结论先行）

本基线是「**生命周期骨架 + Core Domain 持久化 + 呈现管线脚手架**」扎实落地、编辑器本体几乎全部处于设计合同阶段的状态：

1. **项目生命周期（创建/打开/关闭）、Core Domain 持久化（16+ 表、EF Core 5 个迁移、事务与连接协调）、PRA-01 呈现管线（Context→Request→Prepare→Publish→Capture→Render）、视口生命周期与并发正确性**均已 IMPLEMENTED，fail-closed 纪律极强，且 PR #40 已把"管线生产零调用"的 P0 断点接通——用户打开地图工作区时管线真实运行，屏幕显示中性背景色。
2. **地图内容渲染的完整链路（数据→场景项→world/screen 变换→Skia 图元）整体尚未开始**。当前唯一能上屏的是背景色（空态 `#202226` / 已发布 `#E8E9EB`），且被像素级测试锁定。这不是局部 stub，而是链路前三环缺失。
3. **视口零交互**：全 src 无任何指针/滚轮事件处理；`WorkspaceViewState` 的相机与工具状态全部为显式声明占位。WP-30C（pan/zoom/DPI）未开始。
4. **命令层与历史引擎不存在**（ADR-001~005 已冻结但零实现）；Core Domain 只有 time-zero 初始状态读取，无按任意历史时间重建的读模型。
5. **文档诚实度经抽查验证为高**：7 个"声称完成"工作包逐一核对代码与测试，无虚报；"未完成项"登记（04C 未开始、manual acceptance 是清单非记录、macOS 未本地验证）与代码实况一致。
6. **最大平台风险**：GUI 平台人工验收证据为零。CI 四矩阵只证明编译+823 测试通过，不证明 GUI 在任一平台实际运行；macOS 仅有 CI 编译/测试证据，无本地运行验收。

---

## 3. 分项审计结论

### A. 产品与 UI（详表见 CAPABILITY_MATRIX）

- **A1 主窗口/导航 — PARTIAL**：`StartWindow`（启动面即主窗口）+ `ProjectWindow`（头部项目身份、左侧地图列表、右侧工作区列表、`ViewportHost`、状态栏）。菜单仅"文件→关闭项目"。无停靠面板、无完整菜单系统。
- **A2 项目创建/打开/关闭 — IMPLEMENTED**：`CreateProjectUseCase`（.wvproject 目录包）/`OpenProjectUseCase`（分阶段 fail-closed 校验、占位防双开）/`CloseProjectLifetimeAsync`（C2 唯一编排、CAS 幂等、sticky 不可逆）。测试覆盖全（Session/Close/Orchestration/RealPackageFlow）。
- **A3 地图工作区 — PARTIAL**：WP-UI-04B Session 层完成（`MapWorkspaceSession` 状态机、同图去重、ContextSwitched）；**04C 窗口层未开始**——工作区内嵌于 ProjectWindow 的 `ContentControl`，不是设计要求的独立窗口。
- **A4 视口 — PARTIAL**：渲染管线真实（`MapViewportControl`/`MapViewportBinding` 564 行/`CaptureFrame` 唯一捕获点/R3-01 原子发布守卫）；**无任何 pan/zoom 输入路径**。
- **A5 图层面板 / A6 属性检查器 — NOT_STARTED**：仅呈现域有 `SceneLayer` 概念；无任何 UI。
- **A7 工具栏/交互 — DESIGNED**：ME-06 系列契约文档齐全（v0.1~0.3 + a/b/d 子契约），代码仅 `WorkspaceViewState.ActiveTool` 占位。
- **A8 建筑独立场景编辑器 — DESIGNED**：ME-04 系列文档；代码仅 `BuildingCaveAggregateValidator` 校验器。
- **A9 Windows/macOS — PARTIAL**：CI 双平台四矩阵实证编译+测试；代码无 OS 条件编译（仅 DEBUG），OS 感知点集中在路径选择；**Windows 本机已验证构建/测试，macOS 无任何本地执行证据**。

### B. Core Domain

- **B1 Project/Map/Branch — IMPLEMENTED**：`ProjectRecord`（core_project 单行规则）、`MapRecord/HistoryRecord/BranchRecord`、`BranchLineage`（ParentAt 分叉时间）、`SpatialReferenceServices`；Cd01EndToEndTests 端到端锁定。
- **B2 Object/Definition/Type Registry — IMPLEMENTED**：`ObjectTypeRegistry`（编译期权威 descriptor、6 类字段、稳定错误码）+ `JsonSchemaSubset` fail-closed 执行器 + Object 三个领域服务 + RFC 8785 CanonicalJson。
- **B3 Geometry — IMPLEMENTED（V1 最小集）**：`GeometryRules`（10 规范字段 manifest、7 个有界族、dimension=2、校验）+ `GeometryAssetEnvelope` 结构校验。**注意：全是校验，零计算几何**（无距离/面积/相交）。V1 无 core_geometry_component 表，manifest 存 owner typed payload（有意决策）。
- **B4 Building/Cave — PARTIAL**：CD-02-E 三层聚合校验器已实现并有向量一致性测试；编辑域服务与 UI 为零，子实体以 JSON payload 存 typed aggregate。
- **B5 Asset — IMPLEMENTED**：`AssetService`（内容寻址幂等登记、typed link、原子发布约束）+ `FileAssetStore`（staging→hash→原子发布）+ `VerifyAssetIntegrityUseCase`。
- **B6 History/Event — PARTIAL**：结构层完整（`HistoricalTime` 规范时间、core_history/core_branch/core_calendar、Workspace 上下文解析）；**引擎层不存在**——`MapRecords.cs:152` 明注 "CD-01 无 Command，恒为 null"；无 undo/redo、无事件回放、无时间线 UI。
- **B7 Field Dataset — IMPLEMENTED**：不可变 revision 链 + current 指针原子推进、descriptor registry、WVFLDPK 1.0 打包（RFC 8785 manifest + 3 个 golden byte fixtures）。
- **B8 Domain Commands — NOT_STARTED**：应用层仅 4 个生命周期/查询用例，无任何编辑命令、无 `ICommand`、无操作路由。设计合同 ME-06-d（typed intent）纯文档。
- **B9 Schema/Migration — IMPLEMENTED**：EF Core 11，5 个迁移（CoreDomainInitial 手写 DDL 16 表含 DEFERRABLE FK / MapSpatialReferenceOwnership / FieldRevisionTypedMetadata / FieldPayloadStorage / ProjectMapSettingsDocuments），运行时 `Database.Migrate()` + 防降级守卫（未知迁移即 fail-closed），快照漂移防护测试。

### C. Map Engine

| 能力 | 状态 | 要点 |
|---|---|---|
| C1 坐标系/投影 | PARTIAL + DESIGNED | 仅身份/注册层（`ProjectionDefinition` M1 唯一合法状态 `NotConfigured`；`SpatialReferenceService` 注册与绑定）。**无任何坐标变换数学、无 km 桥接、无 Unit sight radius 实现**（src 与文档均无 sight radius；该概念来自另一项目 world-view 的决策，本仓库未采纳落地）。相机标量全部"占位单位" |
| C2 几何计算 | PARTIAL | 只有 manifest/结构校验，零计算几何（`Math.Sqrt/Distance/Area/Intersect` grep 0 命中） |
| C3 Spatial Index | NOT_STARTED | 无 R-tree/quadtree；设计为每快照一棵内存 packed R-tree + broad/narrow 两段式（spatial-presentation 契约 §6） |
| C4 Hit Testing | NOT_STARTED | 唯一同名方法是 Avalonia `ICustomDrawOperation` 桩（恒 false，WP-30C 边界） |
| C5 Snapping | NOT_STARTED | 设计：typed proposal，非拓扑变更 |
| C6 Spatial Chunk | **PROPOSED** | 契约文档在**未合并的 PR #41 分支**上，零代码；plan.md 要求先评审后合并 |
| C7 Fragment / C8 Large Feature / C9 LOD | PROPOSED | 同上，仅契约术语；LOD-0~3 四概念层级 |
| C10 缓存/失效 | NOT_STARTED | 唯一失效机制是语义切换时快照原子清空（`Interlocked.Exchange`）；无任何 cache 基础设施 |

### D. Renderer（重点）

**已实现且质量高**：PRA-01 Presentation Context（指纹/规范化/工厂/架构守卫测试）、Immutable Scene Snapshot（`ImmutableArray` 私有副本、单调版本、无 UI/DB 依赖）、Scene Publisher（六步校验 + `Interlocked.Exchange` 原子发布、latest-request-wins、关闭屏障）、Viewport Binding（PR #40：请求身份、`_stateGate` 原子化、三阶段关闭子序、ContextSwitched 重请求）、Avalonia/Skia 集成骨架（`ISkiaSharpApiLeaseFeature` lease 模式，A01–A10 集成测试全链验证）。

**关键判定**：`MapSceneRenderer` **真实消费已发布快照**（非请求记录器），但 `Draw` 的全部实现是 `canvas.Clear(ResolveBackground(frame.Snapshot))`——**不画任何地图内容**；相机标量被捕获但完全未使用。

**距离"真正显示地图内容"缺失的链路环节（按管线序）**：

| 环节 | 状态 |
|---|---|
| 1. Domain 数据 → 场景项（内容读取投影） | ❌ 缺失。准备函数是硬编码空白场景；无 Core Domain 对象/几何读路径；场景项类型只有 `BlankBackgroundItem`（M1 显式契约） |
| 2. 场景项类型体系（几何/样式/标签） | ❌ 缺失。无几何场景项、无样式系统（未知 token 按内置基线解释，注释明言"M1 无正式样式系统"） |
| 3. world→screen 变换 | ❌ 缺失。`ProjectionDefinition.NotConfigured` 唯一合法；相机占位且渲染器不读 |
| 4. Skia 图元绘制 | ❌ 缺失。无 SKPath/SKPaint/任何图元代码 |
| 5. Skia lease → 控件呈现 → 可视树挂载 | ✅ 已实现（A01–A10 + 像素级测试锁定） |
| 6. 输入（pan/zoom/选择） | ❌ 缺失。控件无指针/滚轮事件；`HitTest` 恒 false |

即：**渲染集成骨架（环 5）已完全就绪并有测试锁定，链路前三环（环 1–4）整体尚未开始**——对应 plan.md Phase 6 序列中 PRA-02 完成后、PRA-03 起点之前的状态。

### E. 数据与持久化

- **E1 SQLite — IMPLEMENTED**：Infrastructure 唯一引用 `Microsoft.Data.Sqlite 11.0.0` + `EFCore.Sqlite 11.0.0`；双层实现（包库原生 ADO.NET + Core Domain EF Core 短生命周期 DbContext 复用同一物理连接）。
- **E2 项目格式 — IMPLEMENTED**：目录式 `.wvproject/`（manifest.json + project.sqlite + Assets/sha256 + Staging + Backups），格式版本 1 fail-closed。docs 下 3 个 `.wvfld` 是 Field Package V1 golden fixtures（魔数 WVFLDPK），非项目文件。
- **E3 Asset Store — IMPLEMENTED（含一条双轨风险）**：`FileAssetStore`（Assets/sha256）与 `FieldPayloadFileStore`（FieldPayloads/sha256/）两条并行通道，包布局常量与实际目录命名并存两套，需裁决（见 GAP_REGISTER G-11）。
- **E4 Migration — IMPLEMENTED**：见 B9。防降级守卫 + 快照漂移防护是亮点。
- **E5 数据一致性 — PARTIAL**：28 处 FK + `PRAGMA foreign_keys=ON` + CHECK/UNIQUE + 读取侧 fail-closed 解析 + 启动时 payload 完整性调和。**软失败方针落地不完整**：缺失/损坏 payload 仅 `_log?.Warn` 后继续打开（符合"不阻断"），但无面向用户的 Error 诊断通道，"仍可保存"无测试锚定，方针本身未成文（见 GAP_REGISTER G-06）。
- **E6 事务与失败恢复 — IMPLEMENTED**：结构写各自单事务、事务内校验、`quick_check`、staging 清理、孤儿收集；**WAL 有意不启用**（open gate，注释明言，非遗漏）。
- **E7 连接生命周期 — IMPLEMENTED**：单物理连接 + `SqliteConnectionAccess` Monitor 临界区统一协调、`Pooling=false`、DisposeAsync 锁内翻转锁外等待、EF 永不拥有连接、拒绝测试替身。

### F. 测试与平台

- **F1 单元测试 — IMPLEMENTED**：5 工程 823 Fact/Theory，xunit.v3 + MTP。遗留冗余：csproj 同时保留 VSTest 适配器与 MTP（GAP-P1-13 已自记）。
- **F2 集成测试 — IMPLEMENTED**：Infrastructure 22 类全真实 SQLite + 真实文件系统（Cd01EndToEnd 等）；桌面侧 `DesktopPresentationPipelineIntegrationTests` A01–A10 真实生产对象全链（仅 Core Domain 查询端口用替身）。边界：GUI 启动/停机无覆盖。
- **F3 UI 测试 — PARTIAL**：无 Avalonia Headless（grep 0 命中）；仅 ViewModel 层单测。GAP-P1-04 未关闭。
- **F4 Renderer 测试 — IMPLEMENTED（M1 范围）**：`MapSceneRendererPixelTests` 在 Skia CPU Surface 真实光栅断言像素；快照/装配/发布/协调器竞态测试齐备。
- **F5/F6 CI 与双平台 — IMPLEMENTED**：唯一 workflow，四矩阵（win/macos × Debug/Release），dotnet 11.0.x，无 publish、无 GUI 冒烟 job。macOS 证据=CI 编译+测试通过，**不是 GUI 运行验收**。
- **F7 GUI 验收证据 — NEEDS_REVIEW**：验收契约与自评文档丰富（ACCEPTANCE_REPORT、TEST_AND_PLATFORM_EVIDENCE、PLATFORM_VALIDATION），但**无任何已执行的手工 GUI 验收记录**：WP-30B PHASE3A-MANUAL-ACCEPTANCE 是清单非记录；WP-UI-04 M-1~M-12 明确 untouched；macOS 本地执行 NOT_RUN；全仓库仅 1 张设计期界面示意图。

---

## 4. 主要架构问题（按严重度）

| 级别 | 问题 |
|---|---|
| P0 | ① 地图内容渲染链路环 1–4 整体缺失（详见 §3-D）；② 视口零交互（无输入事件、相机占位）；③ 命令/历史引擎不存在，任何编辑写入都无正式路径 |
| P1 | ④ WP-UI-04C 独立工作区窗口未实现（违背"一图一窗"既定决策）；⑤ 软失败诊断无用户可见通道；⑥ PR #41 未合并，PRA-05 被阻塞；⑦ Map extent/bounds 未设计；⑧ GUI 平台人工验收证据为零；⑨ 无样式系统 |
| P2 | ⑩ Spatial Index/Hit Test/Snapping/缓存全部未开始；⑪ Asset 目录双轨命名；⑫ README 测试数字过时（139 vs 823）；⑬ macOS 本地验证缺失；⑭ 三套 ADR 编号体系并行易混淆、无统一 GLOSSARY |

---

## 5. 文档 vs 代码对照结论

抽查 7 个"声称完成"的工作包（WP-30B 3A、WP-RA-02-A、CD-02、WP-UI-04B、PRA-01、ME-05 parser/writer、反向验证项），**全部属实或与文档如实一致，未发现虚报**。测试计数可逐项复核（18/17/4、A01–A10、823）。文档体系最大价值在 `docs/implementation/wp-ra-00/`（PR #39）：已钉死"管线已建但曾未接线、领域只到 time-zero、GUI 平台证据为零"三事实，PR #40 即其切片 A 的落地。

## 6. 审计使用的证据来源

- 源码：`WorldView/src`（153 .cs）、`WorldView/tests`（81 .cs）@ `6ec34ea`
- 文档：`docs/`（460 文件）、`plan.md`（PR #42）、`docs/implementation/wp-ra-00/`（PR #39）、PR #41 分支 diff（`git diff origin/master...origin/docs/wp-ra-03-spatial-chunk-lod-contract`）
- CI：`.github/workflows/dotnet-ci.yml`；CI Run 45845579467（R3-R2 复核，四矩阵 success）


## Post-audit update — 2026-11-27

审计时 PR #41 尚未合并；该状态已由 PR #41 merge commit `73da06f0ef143c9a2df9a690aaed0acf1268e6e6` 更新。PR #41 只合并了 Spatial Chunk & LOD 设计合同与 ME-07 引用，没有实现 Chunk、LOD、Spatial Index 或渲染内容。源代码审计基线仍为 `6ec34ea`；当前 master 为 `73da06f`，两者之间是 PR #41 的文档合并，因此本报告的源代码观察仍适用于当前 master。

项目执行规则更正：本项目采用单机单用户、单工作包顺序推进，不要求多 Agent/多分支并行开发。运行时 UI 异步、取消、资源/窗口生命周期和线程安全仍需正确处理。
