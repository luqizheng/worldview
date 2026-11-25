# WP-UI-04A：分阶段实施计划（WP-UI-04B / 04C / 04D）

基线：master `8bdbad1906f7b0c5f3d573b65fc1076867452943`，分支 `feat/wp-ui-04-map-workspace`。
每阶段独立提交、独立可测；04B 完成前不进入 04C。

> **04B 完成记录**：B1-B5 已全部实现并通过测试（895/895，基线 859 + 新增 36），
> 分支 `feat/wp-ui-04b-map-session-lifecycle`。实现与设计的差异（枚举命名
> `MapWorkspaceState`、`CloseWorkspaceAsync` 保持 `Task<bool>` 签名、`OpenWorkspaceAsync`
> 保持返回 Session 而非结果对象——去重命中可经 VM 集合成员资格观察）见
> `lifecycle-state-machine.md` §7。04B 未触碰窗口/Registry/Editor（按禁止范围）。

## WP-UI-04B：Session 层增强（无 UI 改动）——✅ 已完成

目标：`MapWorkspaceSession` / `ProjectSession` 具备支撑独立窗口的全部会话级能力。

| # | 任务 | 位置 | 验收 |
| --- | --- | --- | --- |
| B1 | `MapWorkspaceSession` 生命周期枚举 `MapWorkspaceLifecycle`（Created/Opening/Ready/Closing/CloseFailed/Closed）+ `LifecycleState` 属性 + `CloseFailureDiagnostic`；替换 `_closed`/`_disposed` 布尔；关闭/释放幂等语义保持 | `MapWorkspaceSession.cs` | 状态机单测（`lifecycle-state-machine.md` §2 全部边） |
| B2 | `MapName` 属性：`OpenWorkspaceAsync` 保存 `map.DisplayName`（`ProjectSession.cs` L165-170 现丢弃该值） | `ProjectSession.cs`, `MapWorkspaceSession.cs` | 打开成功后 `MapName == DisplayName` |
| B3 | `OpenWorkspaceAsync` 同图去重：挂载前检查 `_workspaces` 中同 `Context.MapId` 的活动工作区，命中返回既有 Session（结果对象区分 `Opened/New`/`Existing`） | `ProjectSession.cs` L155-191 | 同图双开只产生一个 Session；不同图不受影响 |
| B4 | `CloseWorkspaceAsync` 原子化：Close → Dispose → 双成功才 Remove；返回 `(bool Success, string? Diagnostic)`；失败保留在 `_workspaces` | `ProjectSession.cs` L194-213 | 失败注入测试：工作区保留、重试成功、不重复释放 |
| B5 | `SwitchContextAsync` 增加状态守卫（仅 Ready 允许；Closing/CloseFailed/Closed 晚到达丢弃——保留现有 L77-81 行为并纳入状态机） | `MapWorkspaceSession.cs` L56-86 | 状态守卫单测 |

不做：窗口、VM、XAML、编辑器钩子（`Opening` 状态定义但不进入）。

## WP-UI-04C：窗口层实现

目标：地图打开路径拥有 UI 终点（独立窗口），窗口生命周期与 Session 强一致。

| # | 任务 | 验收 |
| --- | --- | --- |
| C1 | `MapWorkspaceViewModel`（标题字段、状态徽章、StatusText、CloseWorkspaceCommand、Session 关闭结果订阅） | VM 单测：关闭成功/失败/重试/幂等；事件退订计数 |
| C2 | `MapWorkspaceWindow`（`ui-structure.md` 全部布局；`Closing` 拦截 + `_allowClose` + `_closeInProgress`；`Closed` 退订） | 人工验收 §M 全部；XAML 编译通过 |
| C3 | `MapWorkspaceWindowRegistry` + 句柄（`(ProjectId, MapId)` 键；Find/Activate/Remove/Count；`Closed` 自清理） | 单测：去重命中、移除、陈旧句柄、Count 一致性 |
| C4 | `ProjectWorkspaceViewModel.OpenWorkspaceCommand` 改造：先查注册表去重/激活 → 未命中才 `OpenWorkspaceAsync` → 创建 VM+窗口+登记；失败回滚（释放 Session、清登记） | 单测：重复打开激活；创建失败无孤儿 Session |
| C5 | `ProjectWorkspaceViewModel.CloseWorkspaceAsync` 改造（R4）：消费 B4 结果；仅成功才移除 UI 集合项；删除自做 `DisposeAsync` | 单测：UI/Session 不分叉；失败保留 |
| C6 | 组合根：注册 `MapWorkspaceWindowRegistry` 单例 + `Func<ProjectSession, MapWorkspaceSession, MapWorkspaceViewModel>` 工厂（沿用 `Program.cs` L109-122 模式） | 应用可启动，DI 解析正常 |
| C7 | 项目窗口 `Workspaces` 列表项显示可读地图名（替换 L109 的 GUID 绑定），同时修复副标题"世界地图"假数据（移除或换成真实来源） | UI 检查 |

不做：地图绘制、Editor 内容、`Opening` 加载路径。

## WP-UI-04D：联动、打磨与验收

| # | 任务 | 验收 |
| --- | --- | --- |
| D1 | 项目关闭 → 地图窗口被动跟随（VM 订阅关闭结果驱动窗口关闭；失败窗口保留）——`window-management.md` §4 时序落地 | VM/Session 集成测试 + 人工验收 §M-8/9/10 |
| D2 | 窗口创建失败回滚路径实测（C4 的异常分支） | 注入测试 |
| D3 | 诊断与日志：打开/激活/关闭/失败全程 `IAppLog`（沿用 `ProjectWorkspaceViewModel` 模式） | 日志检查 |
| D4 | Windows / macOS 人工验收清单执行（`acceptance-criteria.md` §M） | 双平台签收记录 |
| D5 | 更新 `docs/implementation/IMPLEMENTATION-LOG.md` 与本目录 README 状态 | 文档提交 |

## 依赖与顺序

```
04B（B1→B2→B3→B4→B5 可并行开发，B4 依赖 B1）
 └→ 04C（C1/C3 可先行；C2 依赖 C1；C4 依赖 C1+C3+B3；C5 依赖 B4）
     └→ 04D
```

每阶段完成的定义：全部验收项通过 + 既有测试（281+）无回归 + 文档更新。
