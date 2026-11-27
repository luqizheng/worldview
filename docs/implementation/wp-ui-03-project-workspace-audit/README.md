# WP-UI-03：项目工作区 UI 全面审计（只读）

## 1. 审计版本

| 项目 | 值 |
| --- | --- |
| 审计 HEAD（master） | `44420589b65d9214034a74f97121fa7306393e95`（merge PR #32 `feat/ui-foundation-shell`） |
| PR #34 审查基准（merge-base） | `44420589b65d9214034a74f97121fa7306393e95` |
| PR #34 Head SHA | `35a16bac21977fe11de201d608040661e0b36663`（分支 `feat/ui-02-window-lifecycle-master`，9 个提交，18 个文件，+875 / -74） |
| 审计日期 | 2026-11-25 |
| 审计方式 | 只读代码走查（未修改业务代码 / 数据库 / UI；未合并任何 PR） |

## 2. 审计范围

- 项目身份与项目信息、地图列表与地图选择、工作区生命周期与导航、页面结构与视觉布局、代码结构与职责边界。
- 同时审计 master 现状与 PR #34（WP-UI-02 窗口/Session 生命周期修复）即将进入 master 的变更。
- 证据均来自 `WorldView/src/WorldView.Desktop`（UI 层）、`WorldView/src/WorldView.Application`（Session/用例层）、`WorldView/src/WorldView.Infrastructure`（实现层）及 `WorldView/tests`。

## 3. 结论摘要

1. **PR #34 必须先于任何 UI 迭代合入**。master 的项目关闭路径存在会话泄漏与状态分叉（P0，详见 `ui-issues.md` UI-01）；PR #34 的修复（`CloseFailed` 可重试状态机、标题栏关闭拦截、`ShutdownMode.OnMainWindowClose`、窗口 Hide/Show 切换）方向正确、有 216+117+105 行针对性测试，并已落盘设计文档 `WorldView/docs/architecture/project-session-close-lifecycle.md`。
2. **项目工作区的"骨架"已完整、职责总体清晰**：启动面（创建/打开/最近项目）→ 项目工作区（项目身份 + 地图列表 + 创建地图 + 打开工作区会话）→ ProjectSession 状态机，全部经 Application 用例与 Core Domain 端口，无 UI 直查 SQLite。
3. **用户路径断在"打开地图工作区"之后**：地图工作区只有会话对象（`MapWorkspaceSession`）被塞进一个 ListBox，没有任何编辑器 UI；列表项显示的是 `WorkspaceId`/`MapId` 两个 GUID，用户不可读。Map Editor 不存在（符合当前阶段边界，但这是下一阶段必须补的核心交互）。
4. **存在一处违反项目"不以占位数据冒充真实数据"纪律的 UI**：`ProjectWindow.axaml` 地图项副标题硬编码"世界地图"（master L56-57），不是来自 Core Domain 的真实数据。
5. **测试覆盖呈"两头强、中间空"**：Session/用例层与 ViewModel 层覆盖良好（PR #34 后更佳），但窗口层交互（窗口切换、标题栏关闭拦截、Hide/Show）完全依赖人工验收，无 Avalonia headless 测试。

## 4. 主要发现（Top 5）

| # | 发现 | 级别 | 证据 |
| --- | --- | --- | --- |
| 1 | master 上标题栏关闭项目窗口会遗留活动会话：`ProjectSession` 停在 `Closing`/继续占用注册表，同包再开走 `DuplicateOpen` 分支并为同一 `ExistingSession` 再造一个 ViewModel/窗口，UI 状态与会话真实工作区列表分叉。PR #34 已修复（关闭拦截 + `CloseFailed` 重试 + 窗口去重），**未合入前此问题在 master 上仍然存在** | P0 | `Views/ProjectWindow.axaml.cs`（master 无 Closing 拦截）、`ViewModels/StartViewModel.cs` L151-154、`Sessions/ProjectSession.cs` master L208-253 |
| 2 | 地图工作区没有 UI 终点：`OpenWorkspaceAsync` 成功后仅把 `MapWorkspaceSession` 加入 ListBox，展示 `WorkspaceId:N` 与 `Context.MapId` 两个 GUID；"打开地图 → 进入编辑"这条核心用户路径不存在 | P1 | `ViewModels/ProjectWorkspaceViewModel.cs` L100-129、`Views/ProjectWindow.axaml` L98-122 |
| 3 | 地图列表副标题硬编码"世界地图"，是装饰性假数据；`MapSummary` 只有 `MapId + DisplayName` 两个字段，无描述/元信息，也无重命名/删除等管理入口 | P1 | `Views/ProjectWindow.axaml` L56-57、`Abstractions/IMapQueryService.cs` L26-31 |
| 4 | 项目级信息展示不完整：包路径（`Session.CanonicalPackagePath`）从未在 UI 展示；无项目设置入口；VM 的 `SessionId` 属性未绑定属冗余；地图列表为空/失败仅一行状态文本，无专用空状态 | P1/P2 | `Views/ProjectWindow.axaml` L19-24、`ViewModels/ProjectWorkspaceViewModel.cs` L52-53、L73-97 |
| 5 | 窗口层生命周期无自动化测试：PR #34 设计文档明确"Window 生命周期在无头单元测试中不可靠，不写脆弱假测试"，改为人工验收清单——这是合理决策，但意味着 Hide/Show、Closing 拦截等关键交互在 CI 上零覆盖 | P2 | `WorldView/docs/architecture/project-session-close-lifecycle.md` §5（PR #34） |

## 5. 问题数量统计

| 级别 | 数量 | 说明 |
| --- | --- | --- |
| P0 | 1 | master 会话泄漏与状态分叉（PR #34 已修复，待合并） |
| P1 | 5 | 工作区无 UI 终点、地图假数据/无管理、项目信息缺失、错误呈现单一、"工作区"区域语义混乱 |
| P2 | 7 | 装饰性导航、空状态薄弱、无重试入口、重复装饰面板、fire-and-forget 模式不一致等 |
| P3 | 3 | macOS dock 行为待验证、Unicode 图标跨平台渲染、状态文本风格 |

明细见 `ui-issues.md`。

## 6. 职责边界评估（Project Workspace / Map Workspace / Map Editor）

- **Project Workspace**：`ProjectWindow` + `ProjectWorkspaceViewModel` 目前只承担项目身份、地图列表、地图创建与工作区会话管理，未越界到绘制/编辑——边界基本正确。但"EDITOR WORKSPACES"区域的文案（"打开地图工作区，开始构建和编辑你的世界"）已经在暗示编辑职责，实际它只是会话列表，存在语义越界萌芽。
- **Map Workspace**：`MapWorkspaceSession` 是纯会话对象（无 UI），持有 `ValidatedWorkspaceContext` 与瞬态视图状态，定位正确。
- **Map Editor**：完全不存在。没有编辑器 View/ViewModel，`WorkspaceViewState`（相机/工具/选择）是占位。边界尚未被污染，是落地前的最好时机。

详细分析见 `architecture-boundaries.md`。

## 7. 推荐的下一阶段

**WP-UI-04：地图工作区窗口（Map Workspace Window）最小实现**，理由：

1. P0 依赖 PR #34 合并即可消除，不构成独立阶段。
2. 当前用户路径的最大断裂是"打开地图工作区"后没有可感知的结果（P1 UI-02）；而 Map Editor 的绘制能力尚在更远的阶段（且明确要求不要提前开发地图绘制），所以下一阶段的正确产物是一个**只承载会话上下文、不承载绘制**的工作区窗口。
3. 该窗口将是未来 Map Editor 的宿主容器，先立骨架可以避免编辑能力日后直接堆积进 Project Window（任务书第 5 节边界要求）。
4. 同阶段可顺带清除三个低成本 P1/P2：移除"世界地图"假副标题、把"关闭项目"从 primary 位降级、在项目头部展示包路径。

具体分期见 `implementation-roadmap.md`。

## 8. 阻断问题

- **存在一项**：PR #34 未合并。P0（UI-01）的修复全部在其中；在它合入前，任何基于 master 的 UI 迭代都会在已知的损坏关闭路径上叠加功能。
- 无其他阻断：Core Domain 查询/创建/上下文解析端口均已有正式实现并接入组合根（`Program.cs` L82-122），地图列表与创建链路可用。

## 9. 文档清单

| 文件 | 内容 |
| --- | --- |
| `README.md` | 本文件：结论摘要、版本、Top 5 发现、统计、推荐 |
| `current-implementation.md` | 现有功能逐项清单（已完整实现 / 部分实现 / UI 占位 / 未实现） |
| `user-journey-audit.md` | 启动 → 首页 → 打开项目 → 浏览地图 → 打开地图 → 返回/关闭 全路径审计 |
| `architecture-boundaries.md` | 三概念职责边界分析与目标结构 |
| `ui-issues.md` | P0–P3 问题清单（16 项，含证据/影响/建议/验收标准） |
| `implementation-roadmap.md` | 后续实施路线（按依赖排序，4 个阶段） |
