# WP-UI-04A：验收标准

自动化测试设计在本阶段**只设计、不实施**（禁止事项）。测试替身沿用
`WorldView/tests/WorldView.Application.Tests` 既有的 `FakePackageDatabase` /
`FakeAssetStore` / `FakeMapQuery` / `FakeResolver` 模式与
`WorldView.Desktop.Tests` 的 ViewModel 测试模式。

## A. 自动化测试（Session / VM 层）

窗口对象生命周期不做无头测试（PR #34 既定裁定），窗口行为经 VM/Registry 抽象覆盖。

| # | 场景 | 层 | 关键断言 |
| --- | --- | --- | --- |
| A-1 | 同一地图重复打开只创建一个 Session | Session（B3） | 两次 `OpenWorkspaceAsync(mapId)` → 同一实例；`_workspaces.Count == 1` |
| A-2 | 不同地图可以同时打开 | Session | 两个 MapId → 两个 Session，互不影响 |
| A-3 | 不同项目中的同名/同 ID 地图不冲突 | Session + VM | 两个 ProjectSession 各自打开同 MapId 的地图 → 两个独立 Session；注册表键含 ProjectId 不互相命中 |
| A-4 | 重复打开激活已有窗口（不建 Session 不加载资源） | VM + Registry（C4） | 第二次调用：`OpenWorkspaceAsync` 未被调用（查询计数器 == 1）；注册表 Count 不变；`ActivateRequested == true` |
| A-5 | 关闭地图窗口不关闭项目 | VM | 关闭工作区成功后 `ProjectSession.State == Ready`，`SessionClosed` 未触发 |
| A-6 | 地图窗口关闭成功后 Session 被释放 | Session（B4） | `LifecycleState == Closed`；CTS 已释放（`IsDisposed`）；已从 `_workspaces` 移除 |
| A-7 | 关闭失败保留 Session（与列表注册） | Session | 注入 Dispose/Close 异常 → Session 仍在 `_workspaces`，`CloseFailed` + 诊断 |
| A-8 | 关闭失败后可重试且幂等 | Session | 重试成功 → Closed；`SessionClosed`/释放步骤各只发生一次（调用日志断言） |
| A-9 | 项目关闭会关闭全部地图工作区 | Session | 3 个工作区 → 项目 `CloseAsync` → 全部 Closed、列表清空、逆序释放（日志顺序） |
| A-10 | 子工作区关闭失败时项目不能进入 Closed | Session | 注入第 2 个工作区释放异常 → 项目 `CloseFailed`，该工作区保留，前一个已释放的不重复释放；重试后全部 Closed |
| A-11 | 不重复释放资源 | Session | 双成功标记：失败重试路径下 AssetStore/Database Dispose 调用次数 == 1 |
| A-12 | 无事件订阅泄漏 | VM + Registry | 关闭后：VM 对 Session 的订阅计数 == 0；注册表 Count 归零；`SessionClosed` 处理器已退订（GC 前 handler 计数） |
| A-13 | 窗口注册与 Session 列表不分叉 | VM + Registry | 任意操作序列后不变量：`registry 中每个 handle 的 workspaceId ∈ project.Workspaces`，且每个有窗口的 Session 在注册表中可查 |
| A-14 | 项目关闭触发地图窗口关闭指令 | VM | VM 收到 Session 关闭成功结果 → `RequestCloseCalled == true` |
| A-15 | 窗口创建失败回滚 | VM + Registry | 注入窗口构造异常 → Session 已释放、注册表无残留、状态文本报错 |

## M. 人工验收（Windows / macOS 双平台，04D 执行）

窗口真实生命周期（Avalonia headless 不可靠，沿用 PR #34 裁定走人工清单）：

| # | 场景 | 期望 |
| --- | --- | --- |
| M-1 | 打开地图 → 独立窗口出现，标题"项目名 — 地图名 — World View"，信息卡与状态栏正确 | 名称可读、无 GUID |
| M-2 | 同图再次点击"打开地图工作区" | 不弹新窗口，已有窗口前置获得焦点 |
| M-3 | 已有窗口最小化后重复打开 | 窗口恢复并前置（Windows）；macOS 若未还原最小化，接受 `WindowState=Normal` 修复 |
| M-4 | 三张地图同时打开 | 三个独立窗口，信息互不串扰 |
| M-5 | 关闭一个地图窗口 | 仅该窗口关闭；项目窗口与其余地图窗口不受影响 |
| M-6 | 关闭全部地图窗口后再关闭项目 | 正常 Closed，返回启动面 |
| M-7 | （注入途径：临时断点/调试开关制造释放失败）关闭地图工作区失败 | 窗口保留、状态栏诊断、可再次点击关闭成功 |
| M-8 | 有两个地图窗口时关闭项目 | 两个地图窗口全部自动关闭，随后项目窗口关闭回启动面 |
| M-9 | 制造子工作区关闭失败后关闭项目 | 项目窗口保留 + 诊断；对应地图窗口保留；重试后全部关闭成功 |
| M-10 | 多项目并存：项目 A、B 各开地图，关闭项目 A | 仅 A 的地图窗口关闭；B 的窗口与会话完好 |
| M-11 | 空状态/加载/错误三态视觉走查 | 与 `ui-structure.md` §5 一致，无虚构数据 |
| M-12 | 长时间运行泄漏走查：反复打开/关闭 20 次 | 窗口数、注册表 Count、内存无明显增长（任务管理器/Activity Monitor） |

## 完成定义

- A-1 ~ A-15 全部自动化通过（04B/04C 各自范围内）。
- M-1 ~ M-12 双平台签收（04D）。
- 既有测试全量无回归；`IMPLEMENTATION-LOG.md` 记录提交与结果。
