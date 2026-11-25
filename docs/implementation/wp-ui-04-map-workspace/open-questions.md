# WP-UI-04A：开放问题与技术风险

本阶段已回答架构层面全部十项必答问题（`architecture.md` §4）；以下为实施前或
实施中需要进一步决策/观察的事项，均不阻塞 WP-UI-04B 开工。

## OQ-1：Editor 加载失败的状态语义（Editor 接入前决策）

`Opening → CloseFailed` 复用关闭失败状态在语义上含混（加载失败 ≠ 关闭失败）。
候选：a) 独立 `EditorFailed` 子状态（可重试加载，不影响关闭）；b) 打开期失败直接回滚
整个 Session（关闭并从 `_workspaces` 移除，用户重开）。
倾向：b（打开期失败 = 打开失败，回滚最简单、无中间态）；但 Editor 需求出现前不定案。
**不阻塞**：本阶段 `Opening` 不进入。

## OQ-2：`MapName` 的时效性

B2 在打开时缓存 `DisplayName`。之后地图被重命名（未来功能）→ 窗口标题陈旧。
当前无重命名功能，风险为零；重命名功能立项时需引入 `IMapQueryService` 变更通知或
窗口激活时刷新。**记录在案，不阻塞。**

## OQ-3：同 ProjectId 双包复制的身份边界

`ProjectSessionRegistry` 以包路径为键：同一项目包复制到两个路径可同时打开两个
ProjectSession（同 ProjectId）。此时窗口注册表键 `(ProjectId, MapId)` 会把两个会话中
的同 ID 地图判为同一工作区——错误地去重/激活到另一个项目的窗口。
现状影响：无（单一用户、无复制包场景记录）。
候选方案（需决策一次，建议 04C 实施时定）：
a) 窗口键改为 `(ProjectSession.SessionId, MapId)`——SessionId 进程内唯一，彻底消除歧义，
   但重启后不稳定（窗口本就不跨进程持久，可接受）；
b) 注册表额外校验 `handle.Session.ProjectSession == 当前 Session`（防御式）。
**推荐：04C 直接采用 a + b 组合**（成本极低），文档保持以 ProjectId 表述语义身份、
SessionId 作进程内区分。此项从"开放"降级为"04C 实施决定"，因其不改变任何对外契约。

## OQ-4：`Workspaces` UI 集合与 Session 列表的同步机制

R4 采用"结果返回后手动同步"，未做自动双向绑定（Session 列表是 `List` 快照而非
Observable）。项目级关闭路径中，项目窗口的 `Workspaces` 集合不会逐项收到移除通知——
但项目关闭成功后项目窗口本身关闭，集合随之丢弃，无可见分叉。
若未来出现"项目窗口常驻 + 地图窗口独立开关"的组合，需要 Session→VM 的集合变更事件。
**不阻塞**；04C 后在 UI 上核对一次（M-5）。

## OQ-5：Branch 可读名依赖 Core Domain 查询缺口

`ui-structure.md` §4 的"历史 Branch"行：`ValidatedWorkspaceContext.BranchId` 无可读名
投影（`MapSummary` 仅 MapId+DisplayName；`IWorkspaceContextResolver` 不返回 Branch 名）。
第一版 UI 按约定显示"默认历史 / 分支 {短 ID}"；可读分支名需 Core Domain 读服务扩展
（另立 Core Domain 工作包，不在 UI 侧发明语义）。**不阻塞。**

## OQ-6：窗口激活的平台差异（macOS）

`Window.Activate()` 在 macOS 对最小化窗口可能不还原。`acceptance-criteria.md` M-3
验收时确认；若需修复，补 `WindowState = WindowState.Normal`（一行，04D 处理）。
**不阻塞。**

## OQ-7：`Opening` 状态与"构造即 Ready"的过渡成本

04B 引入枚举后，构造函数直接置 `Ready`（跳过 `Opening`）。未来 Editor 接入需要
`Opening` 时，`OpenWorkspaceAsync` 的调用方（窗口打开入口）需从"同步返回 Session"改为
"等待 Ready 完成再开窗口"或"先开窗口再异步加载"（后者体验更好但窗口需有加载态）。
**倾向后者**（`ui-structure.md` §5 已预留加载态）。Editor 工作包决策；本阶段无成本。

## 风险登记

| 风险 | 等级 | 缓解 |
| --- | --- | --- |
| B4 改动 `CloseWorkspaceAsync` 签名（返回值语义变化）影响既有调用点 | 低 | 调用点仅 `ProjectWorkspaceViewModel`（L142）+ 测试；04B 同步更新，全量测试回归 |
| 生命周期枚举替换布尔后，`SwitchContextAsync` 晚到达保护的行为漂移 | 低 | B5 专项单测锁定现有行为（丢弃结果、不复活） |
| 多窗口并存的 UI 线程重入（打开命令执行中再次点击） | 低 | 命令防重入（`AsyncRelayCommand` IsRunning 判断，沿用本地 StartWindow WIP 的模式）+ 注册表入口去重兜底 |
| 窗口被动跟随关闭在项目 CloseFailed 重试期间的窗口/Session 对应关系 | 中 | A-13 不变量测试 + M-9 人工验收双重覆盖 |
