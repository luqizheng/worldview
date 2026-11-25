# WP-UI-04A：现有代码审查与目标组件架构

基线：master `8bdbad1906f7b0c5f3d573b65fc1076867452943`（merge PR #34）。
所有行号以该基线为准。

## 1. 三层职责总览（目标架构）

```
Project Workspace（项目工作区，已有）
  ├─ 职责：项目信息 / 资源管理 / 地图列表 / 地图创建与打开入口 / 项目级生命周期
  └─ 不负责：地图绘制、编辑器工具、地图渲染细节

Map Workspace（本阶段目标，窗口缺失）
  ├─ 职责：地图级上下文（Map/Branch/历史时间）、MapWorkspaceSession、
  │        独立窗口生命周期、地图级瞬态状态、未来 Editor 的加载/卸载边界
  └─ 不负责：项目级资源（数据库、Asset Store 归 ProjectSession）

Map Editor（未来，不实现）
  └─ 职责：地图渲染、地形/河流/道路/区域/对象/图层/选择交互、2D/3D 表现
```

## 2. 现有代码审查结果

### 2.1 Session 层（Application）

**`MapWorkspaceSession`**（`WorldView/src/WorldView.Application/Sessions/MapWorkspaceSession.cs`）

| 项 | 现状 | 证据 |
| --- | --- | --- |
| 生命周期 | 只有两个布尔 `_closed` / `_disposed`（L16-19），无状态机，无 Opening/Closing/CloseFailed 语义 | L16-19, L44-53 |
| 关闭 | `CloseAsync` 幂等、取消派生工作（L89-103）；`DisposeAsync` 幂等释放 CTS（L106-120）。当前**不可能失败**（只 Cancel/Dispose CTS） | L89-120 |
| 上下文 | `ValidatedWorkspaceContext`（MapId/BranchId/RootHistoryId/typed HistoricalTime），可经 `SwitchContextAsync` 替换（L56-86），晚到达保护完整 | L36, L56-86 |
| 瞬态状态 | 独立 `WorkspaceViewState`（相机/缩放/工具/选择，占位） | L127-143 |
| **缺陷 G1：无地图显示名** | 只有 `Context.MapId`（GUID）。`ProjectSession.OpenWorkspaceAsync` 内部已查到 `MapSummary.DisplayName` 却被丢弃（见下） | L30-36；`ProjectSession.cs` L165-170 |
| **缺陷 G2：无生命周期语义** | 无法表达"正在关闭/关闭失败待重试"，未来 Editor 资源加载失败无处安放 | L16-19 |

**`ProjectSession`**（`WorldView/src/WorldView.Application/Sessions/ProjectSession.cs`）

| 项 | 现状 | 证据 |
| --- | --- | --- |
| 项目状态机 | `Opening/Validating/Migrating/Ready/Failed/Cancelled/Closing/CloseFailed/Closed`，关闭失败可重试，`SessionClosed` 仅在真正 Closed 时触发一次 | L11-23, L220-312 |
| 子工作区持有 | `_workspaces` 列表，同一地图**可重复创建多个 Session**——**违反产品决策 3（同图去重）** | L36, L187-189 |
| 打开工作区 | Ready 守卫（L163）、地图存在性（L165-170）、Branch/时间解析（L172-177）、晚到达保护（L179-190）。`map.DisplayName` 获取后未保存（G1 根因） | L155-191 |
| **缺陷 G3：单工作区关闭的责任分裂** | `CloseWorkspaceAsync` 只做 `CloseAsync → _workspaces.Remove`，**不调用 `DisposeAsync`**；Dispose 由 ViewModel 补做（`ProjectWorkspaceViewModel.cs` L144）。释放责任分裂在两层；且"先 Remove 后（由他人）Dispose"与"成功释放后才移除"的既定原则相悖。当前 CTS Dispose 不会失败所以无实际泄漏，但接入 Editor 资源后该顺序必然出错 | L194-213 |
| 项目级关闭 | 子工作区 LIFO 逐个 `CloseAsync + DisposeAsync`，**两者都成功才移除**（正确）；Asset Store → 数据库逆序，成功标记防重复释放；异常 → `CloseFailed` + 诊断，可重试 | L220-312（子工作区 L237-259，资源 L261-290，失败 L292-301） |

**`ProjectSessionRegistry`**（`Sessions/ProjectSessionRegistry.cs`）

- 包路径占用 + `SessionClosed` 事件解除占用（L27-49）；`CloseAllAsync` 仅在真正 `Closed` 才 `Remove`（L72-84）。**PR #34 后语义正确，本阶段无需改动。**
- 注意：注册键是 `CanonicalPackagePath`，不是 ProjectId。同一项目包被复制到两个路径可产生两个
  ProjectSession（同 ProjectId）——见 `open-questions.md` OQ-3。

### 2.2 Desktop 层

| 文件 | 现状 | 关键证据 |
| --- | --- | --- |
| `StartWindow.axaml.cs` | 持有唯一 `_workspaceWindow`（项目级窗口）；重复打开 `IsVisible` 判定后 `Activate()`；工作区 `Closed` → 恢复首页 | L15, L51-81 |
| `ProjectWindow.axaml.cs` | 标题栏关闭拦截 → `CloseProjectCommand` → 仅 `ProjectClosed` 允许真关；`_closeInProgress` finally 复位；DataContext 切换时按订阅实例退订 | L19-54, L57-85 |
| `ProjectWindow.axaml` | 工作区列表以 ListBox 显示 `Context.MapId` GUID（L109）与占位文案"地图编辑会话"（L111）——**用户不可读，违反"禁止 GUID 代替可读名称"**；地图项副标题硬编码"世界地图"（L56-57，WP-UI-03 已记录的假数据） | L97-121 |
| `ProjectWorkspaceViewModel.cs` | `OpenWorkspaceAsync` 建 Session 后塞进 `Workspaces` 集合，状态文本输出两个 GUID（L122-123）——**用户路径在此断头，无窗口**。`CloseWorkspaceAsync` 忽略 Session 返回的 bool（L142）、无条件 Remove + Dispose（L143-144）——**UI 列表与 Session 列表可分叉**（G3 的 UI 侧体现） | L100-146 |
| `StartViewModel.cs` | `DuplicateOpen` 时重新触发 `ProjectOpened(ExistingSession)`（L191-194），由 StartWindow 去重激活 | L168-200 |
| `App.axaml.cs` | `ShutdownMode.OnMainWindowClose`，首页为 MainWindow | L30 |
| `Program.cs` | 组合根：会话绑定 ViewModel 工厂 `Func<ProjectSession, ProjectWorkspaceViewModel>`（L109-122）；停机同步 `CloseAllAsync`（L44-45） | L44-45, L107-122 |

### 2.3 现有测试

- Session 层：`WorldView/tests/WorldView.Application.Tests/Sessions/ProjectSessionCloseTests.cs`
  （216 行，失败注入、释放顺序断言、事件次数、Registry 占位边界——可复用其 `FakePackageDatabase`/
  `FakeAssetStore`/`FakeMapQuery`/`FakeResolver` 模式）。
- ViewModel 层：`ProjectWorkspaceViewModelCloseTests.cs`（117 行）、`StartViewModelTests.cs`（105 行）。
- 窗口层：无自动化（PR #34 已裁定 Avalonia Window 生命周期不做无头假测试，走人工验收）。

### 2.4 需要重构的现有代码（WP-UI-04B/04C 执行）

| # | 目标 | 位置 | 原因 |
| --- | --- | --- | --- |
| R1 | `MapWorkspaceSession` 增加生命周期枚举 `Created/Opening/Ready/Closing/CloseFailed/Closed` 与 `MapName` | `MapWorkspaceSession.cs` | G1、G2；状态机见 `lifecycle-state-machine.md` |
| R2 | `ProjectSession.OpenWorkspaceAsync` 增加同图去重（已存在同 `(ProjectId, MapId)` 活动工作区时返回既有 Session），并保存 `map.DisplayName` | `ProjectSession.cs` L155-191 | 产品决策 1/3；G1 |
| R3 | `ProjectSession.CloseWorkspaceAsync` 原子化：`CloseAsync → DisposeAsync → 成功才 Remove`，返回成功/诊断；失败保留在列表可重试 | `ProjectSession.cs` L194-213 | G3；"成功释放后才移除"原则 |
| R4 | `ProjectWorkspaceViewModel` 删除自做的 `DisposeAsync` 调用，改为消费 R3 的结果；仅成功才从 `Workspaces` 集合移除；失败写状态文本 | `ProjectWorkspaceViewModel.cs` L132-146 | G3 的 UI 侧；消除 UI/Session 分叉 |
| R5 | 新增 `MapWorkspaceViewModel` + `MapWorkspaceWindow` + `MapWorkspaceWindowRegistry`，`OpenWorkspaceCommand` 改走"先查注册表去重/激活，未命中才建 Session→开窗口" | Desktop 层新增 | 本工作包主体，见 `window-management.md` |

## 3. 目标组件关系

```
ProjectSessionRegistry（既有，不动）
  └─ ProjectSession（项目级生命周期唯一所有者）
       ├─ Database / AssetStore（项目级资源，项目关闭时逆序释放）
       └─ MapWorkspaceSession[1..N]（地图级会话，ProjectSession 唯一所有并执行 Close+Dispose）
            ├─ ValidatedWorkspaceContext（Map/Branch/历史时间，只读校验值）
            ├─ WorkspaceViewState（相机/工具/选择瞬态）
            └─ 未来：EditorHost 资源句柄（加载/卸载经 Session 生命周期驱动）

Desktop（UI，不拥有 Session 生命周期）
  ├─ MapWorkspaceWindowRegistry（组合根单例；key = (ProjectId, MapId)）
  │    └─ MapWorkspaceWindowHandle { Window, ViewModel }（去重/激活/移除）
  ├─ MapWorkspaceWindow（独立窗口；标题栏关闭 → 驱动 Session 关闭，成功才允许销毁）
  │    └─ MapWorkspaceViewModel（项目名/地图名/状态/Editor 宿主占位；不持有资源）
  ├─ ProjectWindow + ProjectWorkspaceViewModel（地图打开入口；Workspaces 集合是 Session 列表的 UI 镜像）
  └─ StartWindow（启动面；持有项目窗口去重，已有，不动）
```

所有权链条（单一所有者原则）：

- **Session 生命周期**：`ProjectSession` 是唯一执行者（Open 挂载、Close+Dispose+移除）。
  窗口、ViewModel、注册表都不得直接调用 `MapWorkspaceSession.DisposeAsync`。
- **窗口生命周期**：`MapWorkspaceWindowRegistry` 是唯一登记处；窗口销毁（`Closed`）只做
  事件退订与登记自清理，不做资源释放。
- **项目级资源**：`ProjectSession.CloseAsync` 既有顺序不变（子工作区 LIFO → AssetStore → DB）。

依赖方向保持单向：Desktop → Application（端口/Session）→ Domain；Infrastructure 只被组合根装配。

## 4. 十项必答问题（当前实现 / 问题 / 推荐方案 / 取舍理由）

**Q1 当前 MapWorkspaceSession 是否具备成为长期地图工作区会话的条件？**
- 现状：上下文模型（ValidatedWorkspaceContext + 独立 ViewState + 派生工作取消令牌）方向正确；
  但生命周期只有两个布尔，无失败重试语义，无地图显示名。
- 问题：接入 Editor 资源后，"关闭失败保留重试"无处表达（G2）；标题栏无法显示可读名称（G1）。
- 推荐：**保留该类作为长期会话核心**，WP-UI-04B 做增强（R1）：生命周期枚举 + MapName +
  失败诊断字段。不推倒重写。
- 取舍：上下文与瞬态状态分离的设计已通过 WP-30A/PR #34 测试验证；重写收益低、风险高。

**Q2 是否需要调整 Session 所有权？**
- 现状：`ProjectSession._workspaces` 持有；单关路径 Dispose 责任分裂到 VM（G3）。
- 问题：两个所有者 → 释放顺序不确定 → 未来 Editor 资源会双重释放或泄漏。
- 推荐：所有权不变（仍归 ProjectSession），但**执行权收敛**：`CloseWorkspaceAsync` 内完成
  Close+Dispose+移除（R3），VM/窗口只发起与消费结果（R4）。
- 取舍：项目关闭必须级联子工作区（产品决策 6），要求项目天然持有子工作区；独立全局
  Session 管理器会制造第二事实来源，违反"无理由不引入框架"。

**Q3 是否需要独立的 MapWorkspaceRegistry？**
- 现状：无。同图去重不存在（`OpenWorkspaceAsync` 每次新建，L187-189）。
- 问题：产品决策 1/3 要求 (ProjectId, MapId) 唯一活动工作区 + 窗口去重/激活。
- 推荐：**Application 层不建全局 MapWorkspaceRegistry**——去重由 `ProjectSession` 在
  `OpenWorkspaceAsync` 内实现（子工作区本来就在项目内，查询成本 O(N)，N 为打开的地图数，
  桌面场景个位数）；**Desktop 层建轻量 `MapWorkspaceWindowRegistry`**（窗口去重/激活，
  key=(ProjectId, MapId)），不承载生命周期。
- 取舍：两层各管各的关注点（Session 唯一性归 Session 层、窗口唯一性归窗口层），
  避免一个全局注册表同时管 Session 与窗口造成耦合；无需并发框架（`lock` + 单 UI 线程足够）。

**Q4 是否可以由 ProjectSession 管理所有子地图工作区？**
- 推荐：**可以且应当**。产品决策 6（项目关闭必须关全部子工作区、全部成功才 Closed）直接
  要求项目持有子工作区清单。PR #34 的 `CloseAsync` 已按此实现且测试覆盖（L237-259 +
  `ProjectSessionCloseTests.cs`）。
- 取舍：无合理替代方案；唯一代价是 `OpenWorkspaceAsync` 需要晚到达保护（已有，L179-190）。

**Q5 窗口管理是否应该与 Session 管理分离？**
- 推荐：**分离**。Session 管理在 Application 层（可单元测试、无 UI 依赖）；窗口管理在
  Desktop 层（`MapWorkspaceWindowRegistry` + Window/VM）。两者以"Session 关闭成功"为
  唯一同步点：窗口销毁前必须确认 Session 已释放；Session 被项目级关闭后窗口被动跟随关闭。
- 取舍：合并（窗口对象直接内嵌 Session 生命周期）会让 Session 状态机依赖 Avalonia
  Window，破坏 Application 层可测试性（PR #34 已裁定窗口生命周期不进无头测试）。

**Q6 多项目同时打开时如何避免身份冲突？**
- 现状：项目间隔离已由 `ProjectSessionRegistry`（包路径占用）+ 会话绑定端口
  （`CoreDomainMapQueryService(session.Database)`，`Program.cs` L112-114）保证；
  `OpenWorkspaceAsync` 要求 `RequireProjectId()`（L331-339），ProjectId 必非空。
- 推荐：窗口注册表 key = `(ProjectId, MapId)` 全局查重。不同项目 ProjectId 不同 → 天然不冲突；
  同项目内靠 MapId 区分。**不用显示名、不用 WorkspaceId、不用 MapId 局部值**（产品决策 4）。
- 取舍：ProjectId 来自 Core Domain 身份读取，是唯一稳定身份；包路径会随文件移动变化，
  不适合作窗口身份键。同 ProjectId 双包复制的病理场景见 `open-questions.md` OQ-3。

**Q7 项目关闭时如何确保所有地图窗口关闭？**
- 推荐：**窗口被动跟随 Session**。`MapWorkspaceViewModel` 订阅所属工作区关闭结果
  （Session 关闭成功事件/状态回调）：Session 被 `ProjectSession.CloseAsync`（项目级路径）
  释放成功后，VM 驱动窗口 `_allowClose = true; Close()`。窗口从不参与"决定何时关闭
  其他窗口"，只保证"自己依附的 Session 没了就退出"。
- 一致性保证：`ProjectSession.CloseAsync` 只有子工作区全部 Close+Dispose 成功才进入
  `Closed`（L237-259）→ 任何残留窗口必然对应 CloseFailed 的子工作区 → 项目 CloseFailed →
  窗口保留可重试。UI 注册表以窗口 `Closed` 事件自清理 → 不遗留注册（详见
  `window-management.md` §4）。
- 取舍：反向方案（项目关闭主动遍历窗口注册表逐个 Close 窗口）要求项目层知道窗口层，
  违反依赖方向；被动跟随以事件即可达成且分叉可测。

**Q8 MapWorkspaceWindow 是否需要独立 ViewModel？**
- 推荐：**需要**。职责：项目名、地图名、生命周期状态、状态/错误文本、关闭命令、
  Editor 宿主占位可见性。不持有任何资源。与 ProjectWorkspaceViewModel 平级，
  经组合根工厂 `Func<ProjectSession, MapWorkspaceSession, MapWorkspaceViewModel>` 创建
  （沿用既有工厂模式，`Program.cs` L109-122）。
- 取舍：无 VM 直接 code-behind 会让窗口关闭流程（必须可重试、必须与 Session 同步）
  不可测；PR #34 的 ProjectWindow 经验表明 VM 层拦截 + 事件驱动是本仓库已验证的模式。

**Q9 地图资源加载属于 Session 还是 Editor？**
- 推荐：**元数据级上下文归 Session，编辑器工作资源归 Editor**。Session 只持有
  `ValidatedWorkspaceContext`（已校验的 Map/Branch/时间身份）与取消令牌；地形网格、
  资产位图、对象几何等重资源由未来 Editor 在 `Ready` 之后按需加载、在 `Closing` 收到
  取消信号后停止、在 Session `Closed` 前全部落盘/释放。
- 取舍：Session 持有重资源会让"打开地图列表页签"级别的轻操作背上完整资源成本，
  且关闭失败的回滚面扩大；Editor 级资源管理可在 Editor 接入时定义自己的失败语义，
  不污染 Session 状态机。

**Q10 未来 Map Editor 如何在不破坏当前架构的情况下接入？**
- 推荐：三步边界，均不需要改 Session 对外契约：
  1. **宿主占位**（04C）：`MapWorkspaceWindow` 中央区域为 `ContentControl`（Editor 宿主），
     当前为空状态占位。
  2. **生命周期钩子**（Editor 接入时）：定义 `IMapWorkspaceEditor` 接口（LoadAsync/UnloadAsync，
     接收 Session 的取消令牌），由 Session 在 `Opening→Ready`、`Closing` 转换时驱动；
     Editor 加载失败 → Session 进入 `CloseFailed` 同款可重试语义（或独立的
     `EditorFailed` 子状态，见 `open-questions.md` OQ-1）。
  3. **资源释放顺序**：Editor 资源 → Session 派生工作 → （项目级关闭时）兄弟工作区 →
     AssetStore → DB，与既有逆序原则一致。
- 取舍：现在只做第 1 步；钩子接口在 Editor 真实需求出现前定义属投机抽象（禁止事项
  "不引入不必要的抽象"）。

## 5. 地图打开流程（目标，04C 后）

```
用户在 ProjectWindow 选中地图 → "打开地图工作区"
  │
  ├─① 查 MapWorkspaceWindowRegistry[(ProjectId, MapId)]
  │     命中且窗口存活 → Activate() 已有窗口，结束（不建 Session，不加载资源）
  │
  ├─② ProjectSession.OpenWorkspaceAsync(mapId, …)
  │     Ready 守卫 → 查地图（取 DisplayName）→ 解析 Branch/时间
  │     → Session 层同图去重（并发兜底）→ 挂载 _workspaces
  │
  ├─③ 组合根工厂创建 MapWorkspaceViewModel(session)
  │     new MapWorkspaceWindow { DataContext = vm } → 注册表登记 → Show()
  │
  └─④ 失败任意步：无窗口、无登记、无 Session 残留，状态文本报错
```

## 6. 地图/项目关闭流程（目标，04C 后）

单窗口关闭（用户点地图窗口标题栏 X 或菜单"关闭工作区"）：

```
Window.Closing 拦截（e.Cancel = true）
  → vm.CloseWorkspaceCommand → ProjectSession.CloseWorkspaceAsync(workspaceId)
      → session.CloseAsync → session.DisposeAsync → 双成功才 _workspaces.Remove
  → 成功：注册表 RemoveBySession → _allowClose = true → 窗口真销毁（Closed 只退订事件）
  → 失败：保留窗口 + 状态文本 + 诊断，可再次点击关闭（幂等重试）
```

项目关闭（沿用 PR #34 模式，地图窗口被动跟随）：

```
ProjectWindow 标题栏 X → CloseProjectCommand → ProjectSession.CloseAsync
  → 子工作区 LIFO：Close + Dispose（MapWorkspaceViewModel 收到关闭结果 → 关闭自己的窗口）
  → 全部成功：AssetStore → DB → Closed → ProjectClosed 事件 → 项目窗口关闭
  → 任一子工作区失败：_workspaces 保留该项 → CloseFailed → 对应地图窗口保留
    → 项目窗口保留 + 诊断 → 重试（幂等，跳过已释放资源）
```

资源释放唯一所有者与不重复释放的逐层证据见 `lifecycle-state-machine.md` §4。

## 7. 与既有架构原则的一致性核对

| 原则 | 结论 |
| --- | --- |
| 地图渲染与地图制作分离 | ✅ Workspace 只承载宿主；渲染归未来 Editor |
| 编辑器不堆积在 Project Window | ✅ 独立 MapWorkspaceWindow |
| 不引入独立渲染线程 | ✅ 本阶段无渲染；Editor 接入时仍走 Avalonia 渲染生命周期 |
| 不引入自研调度器/Dispatcher 抽象 | ✅ 仅 lock + 单 UI 线程 + async/await |
| 绘制遵循 Avalonia 渲染生命周期 | ✅ 不涉及 |
| 保留 `.agents/`，不引入 `.claude/` | ✅ 本阶段只新增文档 |
