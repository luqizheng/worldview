# AF-02 — Application Foundation Implementation Report V0.1

- **Date:** 2026-11-21
- **Status:** Implementation report — AF-02 executed; verdict **PASS WITH CONDITIONS**（Core Domain 集成为已登记依赖门槛）
- **Architecture authority:** [AF-01 Application Foundation Architecture V0.1](AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md)
- **Task:** [AF-02 Developer Implementation Prompt V0.1](AF-02_DEVELOPER_IMPLEMENTATION_PROMPT_v0.1.md)

---

## 1. Verdict

**PASS WITH CONDITIONS.**

- 已达成：桌面应用外壳（启动/停机/异常边界/启动面）、`.wvproject` 包格式与分阶段校验、
  Create/Open 生命周期、Project Session / Map Workspace Session 隔离与关闭、
  Application Bootstrap（ledger + 元数据 + 模块迁移编排）、内容寻址 Asset Store。
- 条件（已登记门槛）：正式 Core Domain 持久化（Project 身份创建/读取、Map 查询、
  Branch/历史时间解析）尚不存在。真实“打开包 → 项目身份 → 地图列表 → 工作区”读路径
  在运行时 **fail-closed** 并如实报告，不以占位数据、影子表或 UI 侧 SQL 冒充（AF-02 §4.6）。
  四个 Core Domain 边界端口（`IProjectIdentityReader` / `IMapQueryService` /
  `IWorkspaceContextResolver` / `IProjectCreationService`）已定义；正式实现落地后注册
  即可点亮真实读路径，无需改动会话层。

## 2. Source repository and branch

| 项 | 值 |
|---|---|
| 仓库 | `https://github.com/luqizheng/worldview`（文档与 `WorldView/` 应用源代码同仓库；preflight 确认不存在独立应用仓库） |
| 分支 | `master` |
| 实施基线 commit | `a12c4bf`（fix(WorldView): restore build and test baseline on .NET 11 SDK） |
| AF-02 实施 commit | `8b94122`（feat(WorldView): implement AF-02 application foundation） |
| 报告与索引 commit | 见文末 |
| 本地 SDK | .NET SDK 11.0.100（net11.0，`Directory.Build.props` 统一指定） |

Preflight 期间两仓库均落后远端（worldview 落后 112 commits、world-view-rs 落后 8），均已
fast-forward 拉取；`luqizheng/world-view-rs`（TV-01 Rust 原型仓库）仅拉取同步，未做任何修改。

## 3. Codebase Readiness Audit（实施前）

| AF-01 依赖 | 审计结论 |
|---|---|
| Avalonia 启动/主窗口/DI/MVVM | **Implemented but unverified**——`App.axaml.cs` 以裸 `Application` 继承触发 CS0118（与 `WorldView.Application` 命名空间冲突），Desktop 项目从未编译通过；preflight 修复后 7/7 项目首次全部构建成功 |
| Core Domain 正式实现 | **Missing**（`IMPLEMENTATION-LOG.md` §3 明确记录；代码中仅 `MapProject` 架构示例） |
| SQLite / 持久化适配 | **Missing**（唯一仓储为 `InMemoryMapProjectRepository`） |
| 迁移 ledger / Application Bootstrap | **Designed only**（P2-09-11 V0.11 §7 为提案，非可执行 DDL；P2-09-12 V0.6 为证明规范，未执行） |
| Project/Map 领域服务（创建/查询/Branch/时间解析） | **Missing** |
| Asset Store | **Missing** |
| Project Package 格式 | **Missing**（AF-01 §3.1 为推荐基线，属 AF-02 实现门槛，已按其落地为 manifest v1） |
| 测试 | **Implemented but unverified**——工作区存在未提交的测试包升级（xunit 2.9.3 → xunit.v3 4.0.3/Test.Sdk 18.12.0/runner 4.0.1/coverlet 10.1.1），但缺少 .NET 10+ SDK 的 MTP opt-in，`dotnet test` 报错；preflight 补 `global.json` 完成迁移 |

审计全文见本文档 §3 与对话记录；未把任何提案 schema 转为生产 DDL（AF-01 §2.2 / AF-02 §3 门槛遵守）。

## 4. Projects, classes and changed paths

全部改动位于 `WorldView/`（commit `8b94122`，58 文件）：

- **Application 层**（`src/WorldView.Application/`）
  - `Abstractions/`：`IPackageDatabase(+Factory)`、`IProjectIdentityReader(+Result)`、
    `IMapQueryService(+MapSummary)`、`IWorkspaceContextResolver(+ValidatedWorkspaceContext)`、
    `IProjectCreationService(+Request/Result)`、`IAssetStore(+Factory/+AssetPutResult)`、
    `IFileSystem`、`IRecentProjectsStore`、`IAppLog`
  - `Bootstrap/`：`ApplicationBootstrapper`（ledger 编排/校验/迁移执行，fail-closed）、
    `BootstrapContracts`（`BootstrapMetadata` / `MigrationLedgerEntry` / `ModuleMigration` /
    `IMigrationSource` / `BootstrapResult`）
  - `Packaging/`：`ProjectPackageFormat`（常量）、`ProjectPackageStructure`（规范路径/路径圈禁）、
    `PackageManifest`（v1 模型 + 解析/序列化）、`PackageValidation`（Issue/Report 分类）、
    `ProjectPackageValidator`（六阶段校验）
  - `Sessions/`：`ProjectSession`（八态生命周期）、`MapWorkspaceSession`（独立瞬态状态 + 派生工作取消）、
    `ProjectSessionRegistry`（防同包双开 + 关闭自动解登记 + 停机拒绝新工作）
  - `UseCases/`：`CreateProjectUseCase`（临时兄弟目录 → manifest → bootstrap → Asset 根 →
    Core Domain 门槛 → 同卷重命名发布；失败只清理自身临时目录）、`OpenProjectUseCase`
    （校验 → 注册占位 → 建立会话 → Ready；失败逆序释放并解登记）
- **Infrastructure 层**（`src/WorldView.Infrastructure/`，新增 `Microsoft.Data.Sqlite 11.0.0`）
  - `Persistence/SqlitePackageDatabase(+Factory)`：`Pool=False`、每连接 `foreign_keys=ON`、
    `quick_check`、元数据/ledger 读写、单事务迁移执行（脚本 + ledger 同事务，失败回滚）
  - `Assets/FileAssetStore(+Factory)`：SHA-256 内容寻址不可变字节（临时文件 → 哈希 → 原子发布；
    已存在文件哈希不符即拒绝；缺失 Asset 返回 null 绝不替换）
  - `IO/FileSystem`、`RecentProjects/JsonRecentProjectsStore`、`Logging/FileAppLog`
- **Desktop 层**（`src/WorldView.Desktop/`）
  - `Program.cs`：组合根（Core Domain 端口显式不注册）+ 进程级异常边界 + 停机关闭全部会话
  - `Views/StartWindow(.axaml/.cs)`：创建/打开/最近项目启动面（StorageProvider 选路）
  - `Views/ProjectWindow(.axaml/.cs)`：项目身份 + Core Domain 地图摘要列表 + 工作区会话（文本基线）
  - `ViewModels/StartViewModel`、`ProjectWorkspaceViewModel`；旧示例 `MainWindow`/`MainViewModel` 删除
  - `App.axaml.cs`：CS0118 修复（`Avalonia.Application` 完整限定）
- **测试**：`tests/WorldView.Application.Tests/Packaging/`（InMemoryFileSystem、FakePackageDatabase、
  manifest/validator 测试）、`UseCases/SessionUseCaseTests.cs`（create/open/session 生命周期与隔离）、
  `tests/WorldView.Infrastructure.Tests/Persistence/`（SqlitePackageDatabaseTests、RealPackageFlowTests）
- **工程文档**：`WorldView/docs/decisions/ADR-0002-application-foundation-package-bootstrap.md`、
  `WorldView/README.md`、`WorldView/docs/architecture/initial-tech-baseline.md`、`global.json`
- **preflight 提交（`a12c4bf`）**：App.axaml.cs CS0118 修复、测试栈升级完成（含 `global.json` MTP opt-in）、
  baseline 文档同步

## 5. Package format and manifest version

```text
<MyWorld>.wvproject/
  manifest.json          # v1: packageFormatVersion=1, database.relativePath, createdAtUtc
  data/project.sqlite         # 单逻辑项目数据库（schema_migration + wv_bootstrap_metadata 由 Bootstrap 拥有）
  Assets/sha256/         # 内容寻址不可变 Asset
  Staging/               # 临时、非权威
  Backups/               # 显式备份产物
```

- manifest v1 未知字段向前兼容忽略；`packageFormatVersion > 1` 拒绝打开（不写入）。
- manifest 路径校验：拒绝绝对路径、`..` 遍历、规范化后逃逸包根。
- 库内仅 Bootstrap 元数据（`wv_bootstrap_metadata`：database_format_version/created_at_utc）与
  共享 `schema_migration` ledger（列名对齐 P2-09-11 V0.11 §7）；**未创建任何领域表**。

## 6. Core Domain services and migrations reused

- **复用的正式服务：无**——正式 Core Domain 实现不存在（审计结论）。这是如实报告的门槛，不是绕过。
- 复用的设计契约：P2-09-11 V0.11 §7 ledger 列结构与 bootstrap 顺序（步骤 1）、P2-09-13 §2
  Application Bootstrap 所有权边界、P2-09-12 V0.6 provider 目标（Microsoft.Data.Sqlite 11.x +
  bundled SQLitePCLRaw e_sqlite3）与 TX-03（每连接外键开启）。
- `IMigrationSource` 机制就绪：Core Domain / AI Assistant 迁移登记后即被编排执行；当前登记为空，
  因此新建数据库仅含 Bootstrap 结构（AF-02 §4.3 明确允许的“只运行已实现且获授权的迁移”）。

## 7. Real read-path evidence

**真实读路径未打通——这是本报告的条件项。** 当前证据链：

1. **真实文件端到端（Infrastructure.Tests `RealPackageFlowTests`，真实 Microsoft.Data.Sqlite + 真实文件系统）**：
   - `CreateWithCoreDomainService_PublishesPackage_AndOpensToReadySession`：真实创建
     `.wvproject`（临时目录 → 发布）→ 包布局断言 → 真实 SQLite 打开/校验 → 会话 Ready →
     关闭（数据库句柄释放）→ 同包再次打开成功。
   - `OpenWithoutIdentityReader_ReportsGate`：无身份服务时打开返回 `ProjectIdentityUnavailable`
     门槛且不暴露会话。
   - `CorruptDatabase_RejectedByValidator`：垃圾字节库被 `DatabaseCorrupt` 拒绝且包未被改写。
   - `AssetStore_ContentAddressed_Immutable_MissingAssetReportedNotSubstituted`：内容寻址、
     幂等复用、缺失返回 null、路径注入拒绝。
2. **运行时 UI 行为**：真实运行中“创建项目包”在 bootstrap 边界停止并显示
   “Core Domain 项目创建服务尚未实现…”；“打开”对通过结构/数据库校验的包返回
   “Core Domain 项目身份读取服务尚未实现…”（fail-closed，见 §10 运行证据）。
3. **测试替身边界**：会话生命周期测试中的身份/地图替身仅用于单元验证会话编排
   （AF-02 §4.6 允许范围），不作为真实集成证据。

Core Domain 持久化落地后点亮读路径所需工作：实现四个端口的真实适配器并在组合根注册；
Application/Session 层无需改动。

## 8. Session ownership and lifecycle

- **ProjectSession**（AF-01 §5）：`Opening → Validating → Migrating → Ready`；
  失败/取消 `Failed/Cancelled`；关闭 `Closing → Closed`。部分建立绝不暴露 Ready；
  资源逆序释放（子工作区 → Asset Store → 数据库）；关闭永不删除包；
  `TryCompleteOpen` 晚完成保护（被取消/关闭后不得复活）；`SessionClosed` 事件驱动注册表解登记。
- **MapWorkspaceSession**（AF-01 §6）：每 Map/Branch/时间上下文一个；独立
  `WorkspaceViewState`（相机/缩放/工具/选择）与独立取消源；上下文切换经 Core Domain 解析端口；
  关闭兄弟与项目会话不受影响；解析等待期间关闭会话 → 晚到达结果丢弃。
- **ProjectSessionRegistry**：进程内同包仅一个会话（建立中亦占位）；停机 `CloseAllAsync`
  先置 shutdown 拒绝新登记再逐个关闭；无任何静态全局状态。
- **Create Project**（AF-01 §3.4 顺序）：目标校验/不覆盖 → 兄弟临时目录（`.tmp-` 标记）→
  manifest → 数据库 bootstrap → Asset 根 → **Core Domain 创建门槛**（未实现即停止，清理临时目录，
  不发布）→ 同卷重命名发布（目标竞态再校验）→ 仅成功后建立会话。

## 9. Tests, commands, counts, results

```text
命令：dotnet test WorldView.sln            （SDK 11.0.100 / net11.0 / x64 / Windows）
结果：总计 70，失败 0，成功 70，跳过 0
  - WorldView.Domain.Tests          5 通过（既有示例基线未回归）
  - WorldView.Application.Tests    48 通过（新增 43：manifest/路径圈禁/校验器/create/open/
                                     会话生命周期/隔离/取消/晚到达/注册表/停机）
  - WorldView.Infrastructure.Tests 17 通过（新增 14：真实 SQLite bootstrap/迁移事务/校验和
                                     与未知迁移 fail-closed/损坏库/真实 FS 端到端/Asset Store）
```

覆盖对照（AF-02 §6）：包有效性、manifest 缺失/畸形、路径遍历与绝对路径、库缺失/损坏、
更高版本拒绝且不写入、目标不覆盖、失败清理临时目录、不预载 Asset、双项目资源隔离、
同包双开阻止、双工作区状态独立、关一工作区兄弟存活、关项目先关子工作区、
逆序释放、取消不暴露 Ready、停机拒绝新工作、晚完成不复活、迁移失败保原始包可恢复
（回滚验证）、身份缺失如实报告、真实 provider 创建/打开/查询（经端口）/打开工作区/关闭。

## 10. Debug/Release build and runtime evidence

| 项 | 结果 |
|---|---|
| `dotnet build WorldView.sln`（Debug） | 成功，7/7 项目，0 警告 0 错误 |
| `dotnet build WorldView.sln -c Release` | 成功，7/7 项目 |
| Windows Release 运行 | 启动正常（进程 23764，窗口 “World View”）；启动面截图
  `docs/application/AF-02-runtime-windows-start-surface.png`（创建项目/打开项目/最近项目入口均渲染）；
  `WM_CLOSE` → 干净退出，exit code 0 |
| Windows Debug 运行 | 启动正常（PID 9664），`WM_CLOSE` → 干净退出 |
| macOS 运行 | **未验证**（本机无 macOS 环境；按 AF-02 §6 不得以构建/发布代运行，不声称任何 macOS 结论） |

运行环境：Windows 10.0.26300 x64（RDP 会话）；Smart App Control 已于 2026-11-21 确认解除拦截。

## 11. Deviations from AF-01 and reasons

| 偏差 | 原因与处理 |
|---|---|
| AF-01/P2-09-12 标注 “.NET 10”，仓库实际为 **net11.0**（ADR-0001，SDK 11.0.100） | 按 AF-02 §5“以仓库实际为准”执行：保留 net11.0，不降级目标框架。差异登记待文档侧勘误 |
| AF-01 §3.4 第 6 步（Core Domain 项目创建）未执行 | 正式服务不存在；按 AF-01 预设的“停在 bootstrap 边界并报告阻塞项”分支实施 |
| AF-01 §4.3 单写者协调器 | V1 唯一写路径为 bootstrap 迁移（会话内单实例串行）；正式领域写入协调器随 Core Domain 持久化落地，未提前发明 |
| 包校验阶段 5（Asset 根）对既有包缺失即失败 | AF-01 §3.3-7 允许“存在或新建时初始化”；对既有包缺失按完整性问题 fail-closed（新包由创建流程保证） |
| 备份/迁移升级路径 | 当前仅 ledger 一致性校验；跨版本迁移升级流程（备份 → 迁移 → 校验）待存在真实迁移时实现（AF-01 开放门槛） |

## 12. Unresolved blockers and risks

1. **Core Domain 持久化（最高优先门槛）**：`core_project`/`core_map` 等 20 张领域表的实现授权、
   领域迁移内容与四个边界端口的真实适配器均不存在；真实读路径在此处 fail-closed。
2. **P2-09-12 V0.6 证明测试未执行**：共享事务/外键/原生库版本矩阵仍待 Core Domain 实现后运行。
3. **macOS 全部未验证**（构建与运行）。
4. **最近项目存储**、**备份机制**、**UI 偏好格式**为 AF-01 开放门槛的 V1 最小实现，后续按需演进。
5. **事务证明范围**：本阶段仅证明迁移执行的单事务性；领域写入的协调语义（P2-09-12 TX-01/02）不在 AF-02 范围。

## 13. Commit SHAs and links

| 仓库 | Commit | 内容 |
|---|---|---|
| `luqizheng/worldview` @ master | [`a12c4bf`](https://github.com/luqizheng/worldview/commit/a12c4bf) | preflight：构建/测试基线修复（CS0118、测试栈迁移 + global.json） |
| `luqizheng/worldview` @ master | [`8b94122`](https://github.com/luqizheng/worldview/commit/8b94122) | AF-02 应用基础实现（58 文件：src/tests/工程文档） |
| `luqizheng/worldview` @ master | 本报告提交（见 Git 历史） | AF-02 报告 + docs/README 索引 + 运行证据截图 |

`luqizheng/world-view-rs`：仅 fast-forward 同步（c2e2bfa → 94f8067），无任何提交；其工作区中
未跟踪的 `docs/map-replacement/`（Phase B 评估交付物 8/8 + exp 材料）为用户所有，其自身 INDEX
注明文档家族分处两个 checkout、“合并位置待用户指定”且 B1 比较书与 Phase A 基线不在本 checkout，
故按任务规则保留原状、未擅自提交。
