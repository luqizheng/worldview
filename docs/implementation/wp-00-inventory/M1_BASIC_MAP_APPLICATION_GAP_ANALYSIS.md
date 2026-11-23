# M1 — Basic Map Application Gap Analysis

M1 核心验收（任务定义）：**创建一个真实 Project 和 Map，打开地图进入真实工作界面，显示可操作的地图视口，保存并重新打开后数据仍然存在。** 与 `docs/map-editor/implementation-roadmap-v1.md` WP-30 的 12 项 M1 acceptance 一致（措辞略有差异，本文件以任务定义的 12 项为准）。

**总判定：NOT MET。** 当前代码上 M1 无法通过验收。以下情况各至少存在一项：地图视口只是占位（实为完全不存在）、Map 创建没有经过正式产品路径（没有用例层与 UI）、Pan/Zoom 不存在、macOS 未验证。

---

## 逐项分析

### 1. 启动软件 — IMPLEMENTED_UNVERIFIED

- **已存在**：完整组合根（`Program.cs:64-122`，全接口注册、无启动即失败点）、`[STAThread]` 入口、Avalonia 12.1.6 生命周期、停机 `CloseAllAsync` 兜底（`Program.cs:44-45`）。
- **缺失**：本审计未执行 GUI 启动冒烟（只读纪律）；无 Desktop 测试工程；进程异常钩子只写 stderr（WinExe 下不可观测）。
- **验收标准**：双平台双击/命令行启动进入 StartWindow，日志可查。
- **测试方式**：手动冒烟 + 未来 Desktop 冒烟工程（Avalonia headless）。
- **阻塞依赖**：无。

### 2. 创建 Project — IMPLEMENTED_VERIFIED ✅

- **已存在**：UI 入口（`StartViewModel.cs:70-101` + `StartWindow.axaml` 创建区）→ `CreateProjectUseCase.cs:103-179`（临时目录→manifest→bootstrap→12 类型播种→原子发布）→ 成功后自动打开。测试：`SessionUseCaseTests.cs:17-156`；端到端 `Cd01EndToEndTests.cs:61-115`。
- **缺失**：无。
- **验收标准**：输入名称→选目录→包落盘→自动进入项目工作区。
- **测试方式**：现有测试已覆盖；UI 冒烟补一次。

### 3. 创建 Map — **BLOCKED**（M1 关键断点，GAP-P0-2）

- **已存在（领域侧完备）**：`MapRules.CreateMap`（Map+根 History+Calendar+day-zero 四件单事务，`MapCreation.cs:25-48`）；`MapService.CreateMap`（`MapBranchServices.cs:12-28`）；SQLite 实现（`SqliteCoreDomainDatabase.cs:194-264`）；测试齐备（`MapBranchRulesTests`、端到端 `Cd01EndToEndTests.cs:84-93`——但那是**测试代码手工调领域服务**，非生产路径）。
- **缺失**：① `CreateMapUseCase`（Application 用例层对 `MapService` 的编排：输入校验→调服务→返回摘要）；② ProjectWindow UI 入口（无"新建地图"按钮/对话框）；③ 组合根注册。
- **阻塞依赖**：无硬阻塞——领域零改动即可接线；工作量集中在 Application+Desktop。
- **验收标准**：在工作区 UI 创建命名 Map，出现在列表，重启后仍在。
- **测试方式**：用例层单测（门槛/成功/重复名策略）+ 端到端。

### 4. 查看 Map 列表 — IMPLEMENTED_VERIFIED ✅

- **已存在**：`IMapQueryService.ListMaps`（`CoreDomainAdapters.cs:126-152`，跨项目隔离）→ `ProjectWorkspaceViewModel.LoadMapsAsync`（`ProjectWorkspaceViewModel.cs:66-90`）→ ListBox 真实数据（非硬编码）。
- **缺失**：VM 层单测；列表项目前只有 MapId+DisplayName 两字段元数据（`MapSummary`）。
- **验收标准**：列表来自 Core Domain 读投影。
- **测试方式**：已覆盖（`Cd01EndToEndTests.cs:147-152`）；VM 分支补测。

### 5. 打开 Map — PARTIAL

- **已存在**：`ProjectSession.OpenWorkspaceAsync`（`ProjectSession.cs:144-180`，晚到达保护）→ `WorkspaceContextRules.TryResolve`（Branch 世系+HistoricalTime 解析，端到端已测 `Cd01EndToEndTests.cs:90-93`）→ MapWorkspaceSession 挂载。
- **缺失**：① UI 打开时恒传 `branchId:null, historicalTimeInput:null`（`ProjectWorkspaceViewModel.cs:105-107`，无显式选择——M1 可接受默认值，但"显示 Branch/时间上下文"是 WP-20 exit criteria）；② 打开后的"工作界面"只是文本窗口（无任何视口）。
- **验收标准**：打开 Map 进入工作界面（M1 语境 = 含视口的界面）。
- **测试方式**：会话层已测；UI 集成待补。
- **阻塞依赖**：GAP-P0-3（工作区产品面）与 GAP-P0-1（视口）。

### 6. 显示空白 Map Viewport — **MISSING**（GAP-P0-1）

- **已存在**：无。Desktop 3 个 AXAML 无 Canvas/Viewport/自定义控件（注释自证"地图渲染属 PRA-01/PRA-02 之后的阶段"）。
- **缺失**：整条最小渲染切片：Presentation Context + Scene Snapshot（空白帧也是快照）+ Scene Builder/Publisher + Avalonia 视口宿主 + Skia 绘制路径 + 中性背景/map frame（来自正式读投影，不伪造几何）。
- **阻塞依赖**：PRA-01 契约已就绪（`docs/map-engine/PRA-01_*_CONTRACT_v0.1.md`）；PRA-01 官方审计验收矩阵 9/10 Not implemented。**设计侧无阻塞，纯实现工作。**
- **验收标准**：roadmap WP-30——"empty but real Map workspace in a functioning viewport"，不得是占位浏览器页/占位图。
- **测试方式**：快照一致性 + 视口行为自动化测试（目前零基础，需新建）。

### 7. Pan / Zoom — **MISSING**（GAP-P0-1）

- **已存在**：`WorkspaceViewState.CameraX/CameraY/Zoom` 占位属性（`MapWorkspaceSession.cs:112-118`）——**唯一消费者是单元测试**（`SessionUseCaseTests.cs:393,397`，断言工作区间状态隔离）。
- **缺失**：map↔screen 双向变换、PointerWheel/拖拽输入路由、resize/DPI 下的变换保持、zoom 限幅策略。
- **阻塞依赖**：依赖第 6 项的视口存在。
- **验收标准**：Pan/Zoom 真实改变视口且 screen↔map 往返一致；resize 后变换正确。
- **测试方式**：变换数学单测（纯函数，易测）+ 视口集成测试。

### 8. 关闭 Map 和 Project — IMPLEMENTED_VERIFIED ✅

- **已存在**：`ProjectSession.CloseAsync`（`ProjectSession.cs:208-253`：子工作区→AssetStore→数据库逆序、幂等、永不删包）+ `CloseWorkspaceAsync`（`:183-202`，兄弟会话不受影响）。测试：`SessionUseCaseTests.cs:327-381`。
- **缺失/注意**：ProjectWindow 点 X（系统关闭按钮）**不**触发会话关闭（GAP-P1-02）——用户经菜单"关闭项目"是正确路径；M1 验收走菜单路径可通过。
- **验收标准**：关闭后包完好、可重开、兄弟会话不受影响。
- **测试方式**：已有测试覆盖语义层。

### 9. 退出应用 — IMPLEMENTED_VERIFIED ✅

- **已存在**：Avalonia 生命周期退出 → `Program.cs:44-45` 同步 `CloseAllAsync`（先 `_shutdown` 停止新登记再逐个关闭，`ProjectSessionRegistry.cs:71-79`）。
- **缺失**：停机路径无测试（TCI-04）。
- **验收标准**：退出时全部会话逆序关闭、无文件句柄残留。

### 10. 重新启动并打开原 Map — PARTIAL

- **已存在**：Project 重开链端到端已验证（`Cd01EndToEndTests.cs:61-115`：创建→关闭→重开→身份一致→数据仍在）；最近项目列表（`JsonRecentProjectsStore`，实现完整但零测试）；Map 从列表手动重开可行。
- **缺失**：① `JsonRecentProjectsStore` 零测试（GAP-P1-05）；② "最近打开 Map"恢复不存在（M1 只要求手动重开原 Map，不阻塞；roadmap WP-20 将 last-opened Map 列为应用偏好项）；③ UI 级"退出→重启→重开"全流程未演练。
- **验收标准**：重启后经最近项目→列表→重开原 Map，数据与身份一致。

### 11. Windows 流程通过 — **未达成**

- 组成项 1-10 中：4 项完成、3 项部分、2 项缺失（视口/Pan-Zoom）、1 项 BLOCKED（Map 创建）。**6、7 两项硬缺失使全流程无法通过。**
- 本审计未执行 GUI 冒烟（只读纪律）；Windows 证据=构建+700 测试+CI 绿（run 45183823988 windows × Debug/Release）。
- **验收标准**：12 项验收在 Windows 上逐项通过并留证据（截图/日志）。

### 12. macOS 流程通过 — **未达成**（GAP-P0-4）

- macOS 仅有 CI build+test 绿证据（run 45183823988 macos-latest Debug/Release success）；**无 GUI 运行证据、无发布产物**（无 pubxml/PublishSingleFile/CI 发布 job）。
- 代码层平台支持存在（路径/日志三分支、Avalonia 跨平台），但 roadmap 约束明确："Windows/macOS support is established by evidence on both platforms, not inferred from shared source code."
- **验收标准**：与第 11 项相同的全流程在 macOS 上通过并留证据。

---

## M1 状态矩阵与完成比例

| # | 验收项 | 状态 | 主要缺口 ID |
|---|---|---|---|
| 1 | 启动软件 | IMPLEMENTED_UNVERIFIED | GAP-P1-01 |
| 2 | 创建 Project | IMPLEMENTED_VERIFIED | — |
| 3 | 创建 Map | **BLOCKED** | GAP-P0-2 |
| 4 | 查看 Map 列表 | IMPLEMENTED_VERIFIED | — |
| 5 | 打开 Map | PARTIAL | GAP-P0-3 |
| 6 | 空白 Map Viewport | **MISSING** | GAP-P0-1 |
| 7 | Pan / Zoom | **MISSING** | GAP-P0-1 |
| 8 | 关闭 Map/Project | IMPLEMENTED_VERIFIED | — |
| 9 | 退出应用 | IMPLEMENTED_VERIFIED | — |
| 10 | 重启并打开原 Map | PARTIAL | GAP-P1-05 |
| 11 | Windows 流程通过 | **未达成** | P0-1/P0-2/P0-3 |
| 12 | macOS 流程通过 | **未达成** | GAP-P0-4 |

**真实完成比例：约 35%–45%。** 计算口径：12 项中 4 项完成（33%）计满、3 项部分各计 ~50%、2 项缺失计 0、BLOCKED 计 0（实现量已就绪但验收不可达）、2 项平台门计 0 → 加权约 4 + 1.5 = 5.5/12 ≈ 46%；考虑"部分"项的残余工作量与平台证据成本，保守取 35%–45%。**此数字为区间估计，验收以状态矩阵为准。**

## M1 剩余工作（按依赖顺序）

1. **WP-20 Map 创建切片**（解除 GAP-P0-2，纯数据侧，领域零改动）：`CreateMapUseCase` + UI 入口 + 用例层测试。建议为第一个实施任务。
2. **WP-30 渲染最小切片**（解除 GAP-P0-1/P0-3）：Presentation Context + Scene Snapshot 最小子集 + Avalonia 视口 + Skia 绘制 + 双向变换 + Pan/Zoom + 空白帧渲染 + 快照/变换测试。前置技术决策：Avalonia 自绘控件 vs `CustomDrawOperation`；PRA-01 契约最小子集裁剪。
3. **WP-10 Desktop 补强**（并行小项）：异常钩子接 `FileAppLog`；窗口关闭语义；`JsonRecentProjectsStore` 测试。
4. **平台门**：Windows GUI 冒烟清单执行；macOS 发布产物（`dotnet publish` 自包含）+ macOS GUI 冒烟。
5. **非阻塞但建议同批**：迁移防降级守卫测试；Branch/时间显式显示（WP-20 exit criteria）。

M1 明确不要求（确认无需纳入）：地形绘制、对象编辑、道路河流、建筑/室内/洞穴、Site/Group、Typed Connections、Templates、AI、3D、GIS 底图——当前这些能力全部为 MISSING/DESIGNED_ONLY 状态，**不构成 M1 阻塞**。
