# M1 — Basic Map Application Gap Analysis

**M1 核心验收的唯一依据：`docs/map-editor/implementation-roadmap-v1.md` WP-30 的 12 项 M1 acceptance。** 本文件逐项编号、措辞与该清单一一对应，不另行定义或改动正式验收项。核心目标概述（与 roadmap 一致）：创建一个真实 Project 和 Map，打开地图进入真实工作界面，显示可操作的地图视口，保存并重新打开后数据仍然存在。

**总判定：NOT MET。** 当前代码上 M1 无法通过验收。以下情况各至少存在一项：地图视口完全不存在、Map 创建没有经过正式产品路径（没有用例层与 UI）、Pan/Zoom 不存在、macOS 无任何 GUI 运行证据。

> 编号勘误说明（2026-11-23 修订）：本文件初版按"任务定义措辞"自排了 1–12 序号，与 roadmap WP-30 正式编号在 8–12 段存在错位（初版 9=退出应用、10=重启并打开原 Map、11=Windows 流程、12=macOS 流程）。本次修订已全部改为正式编号；正式编号以 roadmap 为准，本文件不产生任何新的正式验收编号。

---

## 正式验收清单（逐项分析）

### 1. Launch World View on Windows — IMPLEMENTED_UNVERIFIED

- **已存在**：完整组合根（`Program.cs:64-122`，全接口注册、无启动即失败点）、`[STAThread]` 入口、Avalonia 12.1.6 生命周期、停机 `CloseAllAsync` 兜底（`Program.cs:44-45`）。
- **缺失**：本审计未执行 GUI 启动冒烟（只读纪律）；无 Desktop 测试工程；进程异常钩子只写 stderr（WinExe 下不可观测）。
- **通过条件**：双击/命令行启动进入 StartWindow，日志可查（Windows 证据；macOS 同一流程由第 10 项覆盖）。
- **验证方式**：手动冒烟 + 未来 Desktop 冒烟工程（Avalonia headless）。
- **阻塞依赖**：无。

### 2. Create a Project Package — IMPLEMENTED_VERIFIED ✅

- **已存在**：UI 入口（`StartViewModel.cs:70-101` + `StartWindow.axaml` 创建区）→ `CreateProjectUseCase.cs:103-179`（临时目录→manifest→bootstrap→12 类型播种→原子发布）→ 成功后自动打开。测试：`SessionUseCaseTests.cs:17-156`；端到端 `Cd01EndToEndTests.cs:61-115`。
- **缺失**：无。
- **通过条件**：输入名称→选目录→包落盘→自动进入项目工作区。
- **验证方式**：现有测试已覆盖；UI 冒烟补一次。

### 3. Create a Map through Core Domain — **BLOCKED**（M1 关键断点，GAP-P0-2）

- **已存在（领域侧完备）**：`MapRules.CreateMap`（Map+根 History+Calendar+day-zero 四件单事务，`MapCreation.cs:25-48`）；`MapService.CreateMap`（`MapBranchServices.cs:12-28`）；SQLite 实现（`SqliteCoreDomainDatabase.cs:194-264`）；测试齐备（`MapBranchRulesTests`、端到端 `Cd01EndToEndTests.cs:84-93`——但那是**测试代码手工调领域服务**，非生产路径）。
- **缺失**：① `CreateMapUseCase`（Application 用例层编排：输入校验→调 `IMapCreationService` 端口→返回摘要）；② Infrastructure 适配器 `CoreDomainMapCreationService`（会话绑定数据库→正式 `MapService`）；③ ProjectWindow UI 入口（无"新建地图"按钮/对话框）；④ 组合根注册。
- **阻塞依赖**：无硬阻塞——领域服务就绪，工作量集中在 Application 端口+Infrastructure 适配器+Desktop（WP-20a 切片）。
- **通过条件**：在工作区 UI 创建命名 Map，出现在列表，重启后仍在。
- **验证方式**：用例层单测（门槛/成功/重复名策略）+ 端到端。

### 4. See the Map in the Project's Map list — IMPLEMENTED_VERIFIED ✅

- **已存在**：`IMapQueryService.ListMaps`（`CoreDomainAdapters.cs:126-152`，跨项目隔离）→ `ProjectWorkspaceViewModel.LoadMapsAsync`（`ProjectWorkspaceViewModel.cs:66-90`）→ ListBox 真实数据（非硬编码）。
- **缺失**：VM 层单测；列表项目前只有 MapId+DisplayName 两字段元数据（`MapSummary`）。
- **通过条件**：列表来自 Core Domain 读投影。
- **验证方式**：已覆盖（`Cd01EndToEndTests.cs:147-152`）；VM 分支补测。

### 5. Open the Map workspace — PARTIAL

- **已存在**：`ProjectSession.OpenWorkspaceAsync`（`ProjectSession.cs:144-180`，晚到达保护）→ `WorkspaceContextRules.TryResolve`（Branch 世系+HistoricalTime 解析，端到端已测 `Cd01EndToEndTests.cs:90-93`）→ MapWorkspaceSession 挂载。
- **缺失**：① UI 打开时恒传 `branchId:null, historicalTimeInput:null`（`ProjectWorkspaceViewModel.cs:105-107`，无显式选择——M1 可接受默认值，但"显示 Branch/时间上下文"是 WP-20 exit criteria）；② 打开后的"工作界面"只是文本窗口（无任何视口）。
- **通过条件**：打开 Map 进入工作界面（M1 语境 = 含视口的界面）。
- **验证方式**：会话层已测；UI 集成待补。
- **阻塞依赖**：GAP-P0-3（工作区产品面）与 GAP-P0-1（视口）。

### 6. See the empty Map viewport (not a placeholder browser page) — **MISSING**（GAP-P0-1）

- **已存在**：无。Desktop 3 个 AXAML 无 Canvas/Viewport/自定义控件（注释自证"地图渲染属 PRA-01/PRA-02 之后的阶段"）。
- **缺失**：整条最小渲染切片：Presentation Context + Scene Snapshot（空白帧也是快照）+ Scene Builder/Publisher + Avalonia 视口宿主 + Skia 绘制路径 + 中性背景/map frame（来自正式读投影，不伪造几何）。
- **阻塞依赖**：PRA-01 契约已就绪（`docs/map-engine/PRA-01_*_CONTRACT_v0.1.md`）；PRA-01 官方审计验收矩阵 9/10 Not implemented。**设计侧无阻塞，纯实现工作。**
- **通过条件**：roadmap WP-30——"empty but real Map workspace in a functioning viewport"，不得是占位浏览器页/占位图。
- **验证方式**：快照一致性 + 视口行为自动化测试（目前零基础，需新建）。

### 7. Pan and zoom the viewport; resize the window and maintain correct transforms — **MISSING**（GAP-P0-1）

- **已存在**：`WorkspaceViewState.CameraX/CameraY/Zoom` 占位属性（`MapWorkspaceSession.cs:112-118`）——**唯一消费者是单元测试**（`SessionUseCaseTests.cs:393,397`，断言工作区间状态隔离）。
- **缺失**：map↔screen 双向变换、PointerWheel/拖拽输入路由、resize/DPI 下的变换保持、zoom 限幅策略。
- **阻塞依赖**：依赖第 6 项的视口存在。
- **通过条件**：Pan/Zoom 真实改变视口且 screen↔map 往返一致；resize 后变换正确。
- **验证方式**：变换数学单测（纯函数，易测）+ 视口集成测试。

### 8. Close the Map and Project, exit the application, restart and reopen the same Map — PARTIAL

- **已存在**：`ProjectSession.CloseAsync`（`ProjectSession.cs:208-253`：子工作区→AssetStore→数据库逆序、幂等、永不删包）+ `CloseWorkspaceAsync`（`:183-202`，兄弟会话不受影响）；Avalonia 生命周期退出 → `Program.cs:44-45` 同步 `CloseAllAsync`；重开端到端（`Cd01EndToEndTests.cs:102-113`：关闭→重开→身份一致→数据仍在）。测试：`SessionUseCaseTests.cs:327-381`。
- **缺失/注意**：① ProjectWindow 点 X（系统关闭按钮）**不**触发会话关闭（GAP-P1-02）——用户经菜单"关闭项目"是正确路径；M1 验收走菜单路径可通过；② 停机路径无测试（TCI-04）；③ `JsonRecentProjectsStore` 零测试（GAP-P1-05）；④ UI 级"关闭→退出→重启→重开同一 Map"全流程未演练。
- **通过条件**：关闭 Map 和 Project→退出→重启→重开同一 Map 的连续用户流程走通。
- **验证方式**：会话语义已有测试；全流程属平台冒烟清单（见平台子矩阵）。

### 9. Verify Map identity and persisted data are unchanged — IMPLEMENTED_VERIFIED ✅

- **已存在**：端到端自动化断言（`Cd01EndToEndTests.cs:104-113`：重开后 `ProjectId` 一致、Map 列表数据仍在——真实 SQLite 持久化，非内存回显）；会话关闭不删包语义有测试。
- **缺失**：人工执行第 8 项流程时的数据一致性核对记录（随平台冒烟补齐，不影响本项的自动化验证结论）。
- **通过条件**：重启重开后 Map 身份与持久化数据与关闭前一致。
- **验证方式**：`Cd01EndToEndTests` 已覆盖；冒烟记录补充。

### 10. Repeat the same user flow on macOS — **MISSING**（GAP-P0-4）

- **已存在**：macOS CI build+test 绿（run 45183823988 macos-latest Debug/Release success）；代码层平台分支存在（路径/日志三分支、Avalonia 跨平台）。
- **缺失**：**macOS GUI 运行证据为零**——无 UI 冒烟、无 headless Avalonia 测试、无发布产物可装。CI 的编译+测试进程不能等同 GUI 实际运行。roadmap 约束明确："Windows/macOS support is established by evidence on both platforms, not inferred from shared source code."
- **通过条件**：第 1–9 项同一用户流程在 macOS 实机执行并留证（设备、系统版本、应用版本/commit、逐项结果）。
- **验证方式**：macOS 冒烟清单（依赖 GAP-P0-4 关闭；发布产物见 GAP-P1-15，属 WP-10，不构成 M1 独立阻塞）。

### 11. Verify no map data is created or changed by pan/zoom/rendering — **MISSING**（GAP-P0-1 的伴生验证项）

- **已存在**：无对应实现可验证——pan/zoom/渲染本身不存在（第 6/7 项 MISSING），因此"不改数据"的证明对象当前不存在。
- **缺失**：视口实现后须证明 draw/pan/zoom 路径只读（PRA-01 契约"draw callback 无 DB/无 mutation"）；当前 `WorkspaceViewState` 占位属性无任何持久化写入方（现状无违规路径，但也无可验证的管线）。
- **通过条件**：视口存在且 Pan/Zoom 渲染操作前后 Core Domain 数据（身份/Map/History/对象）逐字节不变。
- **验证方式**：WP-30 的自动化只读性测试 + 冒烟核对；本项在渲染实现前保持 MISSING（不可验证 ≠ 已通过）。

### 12. Run automated tests for snapshot consistency, transform correctness, session lifecycle and platform-specific viewport behavior — PARTIAL

- **已存在（仅 session lifecycle 象限）**：700/700 测试中含会话生命周期全链（`SessionUseCaseTests` 20 例、`Cd01EndToEndTests` 真实 SQLite 端到端、`PersistenceConcurrencyTests` 事务并发）。
- **缺失**：snapshot consistency（快照一致性）、transform correctness（map↔screen 变换数学）、platform-specific viewport behavior（平台视口行为）三类测试零基础——随 WP-30 视口实现从零新建；UI 测试工程不存在（GAP-P1-04）。
- **通过条件**：四类自动化测试齐备且通过（roadmap："A successful CI build alone is insufficient"）。
- **验证方式**：WP-30 测试切片。

---

## M1 状态矩阵（正式 12 项）

| # | 正式验收项（roadmap WP-30 原文） | 状态 | 主要缺口 ID |
|---|---|---|---|
| 1 | Launch World View on Windows | IMPLEMENTED_UNVERIFIED | GAP-P1-01（冒烟未执行） |
| 2 | Create a Project Package | IMPLEMENTED_VERIFIED | — |
| 3 | Create a Map through Core Domain | **BLOCKED** | GAP-P0-2 |
| 4 | See the Map in the Project's Map list | IMPLEMENTED_VERIFIED | — |
| 5 | Open the Map workspace | PARTIAL | GAP-P0-3 |
| 6 | See the empty Map viewport (not a placeholder browser page) | **MISSING** | GAP-P0-1 |
| 7 | Pan and zoom the viewport; resize the window and maintain correct transforms | **MISSING** | GAP-P0-1 |
| 8 | Close the Map and Project, exit the application, restart and reopen the same Map | PARTIAL | GAP-P1-02/05 |
| 9 | Verify Map identity and persisted data are unchanged | IMPLEMENTED_VERIFIED | —（冒烟记录随平台门补） |
| 10 | Repeat the same user flow on macOS | **MISSING** | GAP-P0-4 |
| 11 | Verify no map data is created or changed by pan/zoom/rendering | **MISSING** | GAP-P0-1（验证对象尚不存在） |
| 12 | Run automated tests for snapshot consistency, transform correctness, session lifecycle and platform-specific viewport behavior | PARTIAL | GAP-P0-1（渲染测试）/GAP-P1-04 |

计数：3 项完成、3 项部分、1 项 BLOCKED（Map 创建）、5 项缺失/未验证。**真实完成比例：约 35%–45%（区间估计，非精确指标）。** 计算口径：3 项完成计满（25%）、3 项部分各计 ~50%（12.5%）、BLOCKED 计 0（实现量已就绪但验收不可达）、缺失/未验证计 0 → 加权约 37.5%；考虑"部分"项残余工作量与平台证据成本，保守取 35%–45%。**验收判定以状态矩阵逐项状态为准，不以百分比为准。**

---

## 平台执行证据子矩阵

映射到上表正式验收编号（**不产生新的正式验收编号**）。`Windows` 列 = 第 1–9、11、12 项在 Windows 上的执行证据；`macOS` 列 = 第 10 项（第 1–9、11、12 项在 macOS 的重复执行）。未执行的一律 **NOT_RUN**；**CI build/test 绿只证明编译+测试进程通过，不作为任一平台 GUI 运行证据。**

| 正式验收项 | Windows 证据 | macOS 证据 |
|---|---|---|
| 1 启动 | NOT_RUN（本审计未执行 GUI 冒烟；历史会话亦无正式冒烟记录） | NOT_RUN |
| 2 创建 Project | 测试覆盖 ✅；GUI 冒烟 NOT_RUN | NOT_RUN |
| 3 创建 Map | BLOCKED（无用例/UI，任何平台均不可执行） | NOT_RUN |
| 4 查看 Map 列表 | 测试覆盖 ✅；GUI 冒烟 NOT_RUN | NOT_RUN |
| 5 打开 Map 工作区 | 会话层测试 ✅；GUI 冒烟 NOT_RUN | NOT_RUN |
| 6 空白视口 | MISSING（实现不存在） | NOT_RUN |
| 7 Pan/Zoom + resize | MISSING（实现不存在） | NOT_RUN |
| 8 关闭→退出→重启→重开 | 会话/端到端测试 ✅；UI 级全流程 NOT_RUN | NOT_RUN |
| 9 身份与数据不变 | 自动化断言 ✅（`Cd01EndToEndTests`）；人工核对记录随冒烟补 | NOT_RUN |
| 10 macOS 全流程 | —（不适用） | **MISSING（零 GUI 证据，GAP-P0-4）** |
| 11 渲染不改数据 | MISSING（验证对象尚不存在） | NOT_RUN |
| 12 自动化测试（四类） | session lifecycle ✅（700/700 含真实 SQLite）；snapshot/transform/viewport 三类零基础 | macOS 侧构建+测试 CI 绿（run 45183823988 Debug/Release）——**仅测试进程证据，非 GUI 运行** |

**平台结论**：M1 的平台门（第 1–9 项的 GUI 冒烟 + 第 10 项）在两个平台都未关闭；macOS 侧差距更大（连"能启动的发布产物"都不存在——发布链归 GAP-P1-15/WP-10，不是 M1 独立阻塞项）。

---

## M1 剩余工作（按依赖顺序）

1. **WP-20a Map 创建切片**（解除 GAP-P0-2，纯数据侧）：`IMapCreationService` 端口 + `CoreDomainMapCreationService` 适配器 + `CreateMapUseCase` + ProjectWindow 创建入口 + Map 名称唯一性规则（Domain 最小修改）+ 用例/适配器/UI 测试。建议为第一个实施任务。
2. **WP-30 渲染最小切片**（解除 GAP-P0-1/P0-3 及第 11 项的可验证性、第 12 项渲染测试）：Presentation Context + Scene Snapshot 最小子集 + Avalonia 视口 + Skia 绘制 + 双向变换 + Pan/Zoom + 空白帧渲染 + 快照一致性/变换正确性/只读性测试。前置技术决策：Avalonia 自绘控件 vs `CustomDrawOperation`；PRA-01 契约最小子集裁剪。
3. **WP-10 Desktop 补强**（并行小项）：异常钩子接 `FileAppLog`；窗口关闭语义；`JsonRecentProjectsStore` 测试；**打包发布链（GAP-P1-15，WP-10 范围）**。
4. **平台门**：Windows GUI 冒烟清单执行（第 1–9、11、12 项逐项留证）；macOS 实机同一清单（第 10 项，GAP-P0-4）。
5. **非阻塞但建议同批**：迁移防降级守卫测试；Branch/时间上下文显式显示（WP-20b，roadmap WP-20 exit criteria）。

M1 明确不要求（确认无需纳入）：地形绘制、对象编辑、道路河流、建筑/室内/洞穴、Site/Group、Typed Connections、Templates、AI、3D、GIS 底图——当前这些能力全部为 MISSING/DESIGNED_ONLY 状态，**不构成 M1 阻塞**。
