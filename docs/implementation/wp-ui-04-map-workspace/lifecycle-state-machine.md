# WP-UI-04A：Map Workspace 生命周期状态机

基线：master `8bdbad1906f7b0c5f3d573b65fc1076867452943`。
项目级状态机（`ProjectSessionState`）已在 PR #34 落盘
（`WorldView/docs/architecture/project-session-close-lifecycle.md`），本文不重复，
只定义 **MapWorkspaceSession 的状态机**及两级一致性约束。

## 1. MapWorkspaceSession 状态集

| 状态 | 语义 | 进入条件 |
| --- | --- | --- |
| `Created` | 对象已构造，上下文已校验，编辑器宿主未加载 | `OpenWorkspaceAsync` 内 `new MapWorkspaceSession(...)` |
| `Opening` | 编辑器宿主/地图工作资源加载中（**当前阶段不使用，为 Editor 预留**；无 Editor 时构造后直接 Ready） | `ReadyAsync`/宿主加载启动（未来） |
| `Ready` | 可正常使用；允许 SwitchContext、允许关闭 | 资源就绪（当前实现：构造即 Ready） |
| `Closing` | 关闭执行中：派生工作已取消、编辑器资源释放中 | `CloseAsync` 首次调用 |
| `CloseFailed` | 关闭中发生异常：资源可能部分释放；保留完整状态供重试 | `CloseAsync` 抛出异常 |
| `Closed` | 全部资源释放成功；终态 | 全部释放步骤成功 |

当前实现（`MapWorkspaceSession.cs` L16-19）只有 `_closed`/`_disposed` 两个布尔，
行为上等价于 `Ready → Closing → Closed` 直通路径且永不失败。WP-UI-04B（R1）将其
替换为显式枚举，语义不变、增加失败重试能力，为 Editor 资源释放失败预留位置。

## 2. 状态转换图

```mermaid
stateDiagram-v2
    [*] --> Created : OpenWorkspaceAsync（上下文校验通过、挂载 _workspaces）

    Created --> Ready : 无编辑器资源（当前实现，构造即就绪）
    Created --> Opening : 未来：编辑器宿主开始加载
    Opening --> Ready : 资源加载成功
    Opening --> CloseFailed : 加载失败（关闭路径接管清理；见 OQ-1）
    Opening --> Closing : 等待期间被关闭（晚到达保护）

    Ready --> Closing : CloseAsync（首次）
    Closing --> Closed : 派生工作取消 + 编辑器资源释放全部成功
    Closing --> CloseFailed : 任一释放步骤抛异常（记录诊断，保留状态）
    CloseFailed --> Closing : CloseAsync 重试（跳过已成功释放的资源，幂等）
    Closed --> Closing : 拒绝（终态，幂等返回，不重复释放）

    Closed --> [*]
```

说明：

- `Created → Ready` 当前是同步的：`OpenWorkspaceAsync` 在构造前已完成地图存在性检查与
  Branch/历史时间解析（`ProjectSession.cs` L165-177），Session 诞生即携带
  `ValidatedWorkspaceContext`。`Opening` 状态本阶段**定义但不进入**（禁止事项：不过早实现）。
- `Closed` 对任何再次关闭调用幂等返回（对齐 `ProjectSession.CloseAsync` L224-228 语义）。
- `CloseFailed` 的唯一出口是重试（`CloseAsync` / `DisposeAsync` 再调用），回到 `Closing`
  并从失败点继续——依赖每步释放的幂等性（§3）。

## 3. 各状态允许的操作

| 操作 | Created | Ready | Closing | CloseFailed | Closed |
| --- | --- | --- | --- | --- | --- |
| `SwitchContextAsync` | ⛔ | ✅（成功后取消旧派生工作） | ⛔（晚到达丢弃结果） | ⛔ | ⛔ |
| `CloseAsync` | ✅（跳过 Opening 语义直接关闭） | ✅ | ⛔ 幂等返回（已有一次关闭在执行） | ✅ 重试入口 | ✅ 幂等返回（不重复发事件） |
| `DisposeAsync` | ⛔ | ⛔（必须先 CloseAsync） | （由 CloseAsync 流程驱动） | ✅ 继续释放剩余资源 | ✅ 幂等返回 |
| 新编辑器挂载（未来） | ✅（Opening 期间） | ✅（热加载，另行决策） | ⛔ | ⛔ | ⛔ |

异常与取消行为：

- `CloseAsync` 执行中收到外部 `cancellationToken` 取消：与异常同路处理——已完成步骤
  保持"已释放"标记，未完成步骤留待重试，状态进 `CloseFailed`（诊断注明取消）。
  不允许半途静默吞掉取消。
- 重复 `CloseAsync` 并发调用：`Closing` 幂等短路（单机桌面程序无并发关闭入口，
  对齐 `ProjectSession.cs` L224 注释的既有裁定）。

## 4. 资源释放顺序（Session 级）与"成功后才移除"原则

固定顺序（单 Session 内）：

```
① 派生工作取消（_derivedWorkCts.Cancel —— 通知 Editor/后台工作停止）
② 编辑器资源释放（未来；当前无）
③ 派生工作取消源释放（_derivedWorkCts.Dispose）
```

每步**成功后**才置位对应"已释放"标记；重试时跳过已标记步骤。
Session 级别的资源**没有**独立于 Close 流程的第二释放入口——
`DisposeAsync` 只是 `CloseAsync` 的续段（对齐 `ProjectSession.DisposeAsync` L318 的既有模式）。

**注册移除规则（本阶段最关键的不变量）**：

> `ProjectSession.CloseWorkspaceAsync` / `ProjectSession.CloseAsync` 必须在该子工作区
> `CloseAsync` **且** `DisposeAsync` 都成功返回后，才把它从 `_workspaces` 移除。
> 任何失败 → 工作区保留在列表中 → 状态 `CloseFailed` → 可重试。

- 现状缺陷：单关路径 `ProjectSession.CloseWorkspaceAsync`（L194-213）只 Close + Remove，
  Dispose 散落在 ViewModel（`ProjectWorkspaceViewModel.cs` L144）——属于"先移除、
  他人补释放"，违反本不变量。WP-UI-04B（R3）修复。
- 项目级路径已符合：`ProjectSession.CloseAsync` L237-259（双成功才 RemoveAt，失败保留
  在列表，重试幂等）——本阶段**不改**，仅新增状态机语义。
- UI 镜像规则：`ProjectWorkspaceViewModel.Workspaces` 集合只在 Session 列表移除成功的
  **结果返回后**同步移除（R4）；禁止"先删 UI 再试释放"。

## 5. Session 与 Window 的一致性

不变量（按优先级）：

1. **每个存活的 MapWorkspaceWindow 必须恰好对应一个 `_workspaces` 中的活动 Session**；
   反之不要求（Session 可暂无窗口——04B 阶段、或窗口创建失败的瞬间）。
2. **窗口销毁 ⇒ Session 已释放**：窗口 `Closing` 拦截 → 驱动
   `ProjectSession.CloseWorkspaceAsync` → 只有成功返回才允许真关闭（详见
   `window-management.md` §3）。
3. **Session 被项目级关闭释放 ⇒ 窗口随后销毁**：VM 订阅 Session 关闭结果，驱动
   `_allowClose → Close()`（被动跟随，见 `architecture.md` §6）。
4. **窗口注册表与 Session 列表不分叉**：注册表以窗口 `Closed` 事件自清理；任何时刻
   断言 `registry.Count == 活跃地图窗口数` 且每个 handle 的 Session 可在所属
   `ProjectSession.Workspaces` 中找到（验收项，见 `acceptance-criteria.md` §A-13）。
5. **重复关闭幂等**：窗口层 `_closeInProgress` 防重入（沿用 ProjectWindow L27-30 模式），
   Session 层状态机幂等——两层各自幂等，组合仍幂等。

## 6. 项目级一致性（引用既有结论，不改设计）

`ProjectSessionState` 状态机、`SessionClosed` 单次触发、Registry 成功才解除占用、
`CloseAllAsync` 仅 `Closed` 才 `Remove`——全部沿用 PR #34 设计文档
（`WorldView/docs/architecture/project-session-close-lifecycle.md` §2-§3）。
本阶段唯一新增约束：**`Closed` 前提除"子工作区全部成功释放"外，隐含"对应窗口全部
销毁"**——由不变量 2/3 推导（Session 全部释放 ⇒ VM 全部收到关闭结果 ⇒ 窗口全部关闭），
无需项目层感知窗口。
