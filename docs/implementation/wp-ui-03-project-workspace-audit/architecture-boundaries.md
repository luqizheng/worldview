# WP-UI-03 架构边界分析：Project Workspace / Map Workspace / Map Editor

## 1. 现状分层图

```
WorldView.Desktop (Views + ViewModels)
  StartWindow ── StartViewModel ── CreateProject/OpenProject 用例
  ProjectWindow ── ProjectWorkspaceViewModel ── CreateMapUseCase
  CreateMapDialog ── CreateMapDialogViewModel ──┘
WorldView.Application
  Sessions/ProjectSession · MapWorkspaceSession · ProjectSessionRegistry
  UseCases/CreateMapUseCase · OpenProjectUseCase · CreateProjectUseCase
  Abstractions/IMapQueryService · IWorkspaceContextResolver · IMapCreationService（会话绑定端口）
WorldView.Infrastructure
  CoreDomainMapQueryService / CoreDomainWorkspaceContextResolver / CoreDomainMapCreationService（按 session.Database 构造）
WorldView.Presentation
  Scene/Presentation 层（已存在，但 UI 层完全未消费）
```

## 2. 三概念逐项对照

### 2.1 Project Workspace

任务书定义：项目级信息、项目内资源导航、地图列表与选择、进入地图编辑器；不负责地图内部绘制。

**现状：边界基本正确，两处萌芽偏差。**

| 检查项 | 结论 | 证据 |
| --- | --- | --- |
| 项目级信息 | ✅ 展示名/ID；❌ 路径、设置、状态未展示 | `ProjectWindow.axaml` L19-29 |
| 资源导航（地图列表/创建） | ✅ 经 Core Domain 端口 | `ProjectWorkspaceViewModel.LoadMapsAsync`、组合根 `Program.cs` L109-122 |
| 进入地图编辑器 | ⚠️ 语义已出现，能力不存在 | VM 命令名 `OpenWorkspaceCommand`，按钮文案"打开地图工作区，开始构建和编辑你的世界"（`ProjectWindow.axaml` L84）——承诺了编辑，实际只创建会话 |
| 不负责绘制 | ✅ 无任何绘制代码 | Desktop 层无 Canvas/渲染代码；`WorldView.Presentation` 未被 Desktop 引用 |

萌芽偏差的具体风险：右栏区域被命名为"EDITOR WORKSPACES"，承载的却是会话管理（打开/关闭 `MapWorkspaceSession`）。当真正的编辑器落地时，如果沿用这个区域就地展开工具栏/画布，Project Window 会滑向"项目首页 + 编辑器"混合体。**目标结构必须把"会话管理"从项目页内容区降级为导航入口**（见 §4）。

### 2.2 Map Workspace

任务书定义：一个打开的地图工作区，负责地图级上下文与编辑器会话；不取代 Project Workspace。

**现状：纯会话对象，无 UI 宿主。定位正确，缺壳。**

- `MapWorkspaceSession`（`Sessions/MapWorkspaceSession.cs`）：持有 `ValidatedWorkspaceContext`（MapId/BranchId/RootHistoryId/typed HistoricalTime）、独立 `WorkspaceViewState`（相机/工具/选择占位）、派生工作取消令牌。注释明确"不持有独立数据库连接、不复制权威地图模型"——与 AF-01 §6 一致。
- **UI 缺壳**：会话被打开后唯一的"存在形式"是 ProjectWindow ListBox 里的一行（`ProjectWindow.axaml` L98-122）。没有窗口承载它，`WorkspaceViewState` 无消费者，`Presentation` 层的 Scene 快照管线（`SceneSnapshotPublisher`/`ViewportPresentationRuntime`）无 UI 对接。
- **[PR34]** 修正了 Dispose 幂等性，使项目关闭路径可以安全地对每个工作区执行 Close+Dispose——这为"工作区窗口"生命周期铺平了道路。

### 2.3 Map Editor

任务书定义：负责地图实际编辑，独立扩展；不把编辑逻辑堆积进项目首页。

**现状：完全不存在，且尚未被污染。**

- 无 Editor View/ViewModel/Window；`WorkspaceViewState.ActiveTool` 等均为占位注释。
- 值得注意的正向事实：`WorldView.Presentation` 已有完整的 Scene 组装/发布/视口运行时抽象（含 `PresentationBoundaryTests` 架构测试），说明编辑器渲染已有下游设计，Desktop UI 尚未接入。这是在编辑器落地前先立"工作区窗口壳"的最佳时机。

## 3. 识别到的不合理耦合

| # | 耦合问题 | 证据 | 影响 |
| --- | --- | --- | --- |
| C-1 | `ProjectWorkspaceViewModel` 同时管理地图列表、会话集合、项目关闭三个职责，且 `Workspaces` 集合是 `Session._workspaces` 的手工镜像（Add/Remove 两处同步） | VM L56-59、L121、L142；`ProjectSession.cs` L177、L198 | 状态分叉风险（master P0 已实证）；未来加"工作区窗口"后镜像同步面更大 |
| C-2 | master 上窗口重建导致镜像分叉：`DuplicateOpen` 为同一 `ExistingSession` 新建 VM，新 VM `Workspaces` 为空 | `StartViewModel.cs` L151-154（master）；PR34 通过单窗口规避 | PR34 合并后缓解，但"VM 镜像会话集合"模式本身仍是脆弱点 |
| C-3 | 会话绑定端口在 VM 工厂闭包内 `new` 具体实现类 | `Program.cs` L112-114（`new CoreDomainMapQueryService(...)`） | 严格说违背"各层仅通过接口注册"的组合根自述（同文件 L59）；实际是 Ports & Adapters 允许的组合根装配位置，**判为可接受，但建议收敛为工厂接口**，避免后续端口构造依赖膨胀时闭包失控 |
| C-4 | 工作区会话的 UI 展示直接绑定 `MapWorkspaceSession` 实体类型（`x:DataType="session:MapWorkspaceSession"`），Display 文本依赖 `Context.MapId` | `ProjectWindow.axaml` L100、L110 | Session（Application 层对象）直接成为 View 的数据模板类型；工作区窗口落地时应引入工作区级 VM 持有显示名，而不是让视图解析领域对象 |
| C-5 | `CreateMapUseCase` 实例经 VM 公开属性 `CreateMap` 暴露，由窗口层构造对话框 VM | `ProjectWorkspaceViewModel` L44；`ProjectWindow.axaml.cs` L57 | 用例对象穿透到窗口层。当前无害（对话框 VM 需要它），但它使"VM→窗口→另一 VM"的传递链成立；若创建入口增多（右键菜单等）应改为 VM 直接暴露"创建地图"命令，窗口只弹壳 |

## 4. 目标结构（下一阶段方向，非本期实施）

```
Desktop
  StartWindow / StartViewModel                      —— 项目选择与打开（现状保留）
  ProjectWindow / ProjectWorkspaceViewModel         —— 收窄为：项目身份 + 地图导航 + 创建地图
        │ 命令：OpenMapWorkspace(mapId)             —— 不再持有 Workspaces 集合
        ▼
  MapWorkspaceWindow / MapWorkspaceViewModel(新增)  —— 每个工作区一个窗口
        · 显示名来自 MapSummary（解决 GUID 展示问题，C-4）
        · 持有 MapWorkspaceSession + 未来 Editor 内容区（工具栏/画布占位）
        · 关闭 = Session.CloseWorkspaceAsync（单窗口关闭不影响项目会话）
  Session → VM 同步：由 ProjectSession 暴露 WorkspacesChanged 事件，
  订阅方（项目 VM 或工作区窗口管理器）单向刷新，消除手工镜像（C-1/C-2）
Presentation（未来）
  MapWorkspaceWindow 内容区宿主 Scene 视图，消费 WorkspaceViewState 与
  ViewportPresentationRuntime —— 编辑能力进入工作区窗口，绝不回流 ProjectWindow
```

边界判据（目标态验收）：

1. `ProjectWorkspaceViewModel` 不再引用 `MapWorkspaceSession` 集合（只发打开请求 + 接收结果事件）。
2. `ProjectWindow` 不含任何编辑器词汇的布局区域（"EDITOR WORKSPACES"标题随区域移除）。
3. `MapWorkspaceViewModel` 是唯一持有 `MapWorkspaceSession` 的 VM；其关闭仅影响自身会话。
4. Desktop 不新增对 SQLite/EF 的任何引用（现有纪律延续）。

## 5. 与既有架构文档的一致性

- AF-01（会话/生命周期）、AF-02（fail-closed 门槛）、AF-02 §4.6（不以占位数据冒充真实数据）：Session 层与用例层完全一致；**唯一违反点是 `ProjectWindow.axaml` L56-57 硬编码"世界地图"副标题**（P1 UI-03）。
- PR34 新增 `WorldView/docs/architecture/project-session-close-lifecycle.md`：与代码一致（逐条核对 §2 状态机表、§3 释放顺序、§4 UI 契约、§5 测试边界，全部能在 `ProjectSession.cs`/`ProjectWorkspaceViewModel.cs`/`ProjectWindow.axaml.cs` 的 PR34 版本找到对应实现）。
- 组合根单例/会话绑定注册描述（`Program.cs` L59-62 注释）与实际注册一致。
