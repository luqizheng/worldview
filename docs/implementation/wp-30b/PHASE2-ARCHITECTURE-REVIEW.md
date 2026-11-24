# WP-30B Phase 2 — 跨 ADR 架构一致性审查

- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— 针对实现成本的三项关注点做二次审查，补强控件边界与关闭后绘制语义，并同步 07/08/09/10/11。
- **修订**: R2（2026-11-24）— 区分 Detach / Viewport Destroy / Workspace Close；修正关闭保证的过度表述；补充重新挂载与所有权唯一性核查。
- **修订**: R3（2026-11-24）— 把「Workspace Close」拆为 C1 单工作区关闭与 C2 项目关闭，按三个真实源文件核验语义；记录项目关闭未 Dispose 子工作区的既有缺口。
- **修订**: R4（2026-11-24）— C2 收敛为单一编排入口 `CloseProjectLifetimeAsync()`；核对现有 `CloseProjectAsync` → `ProjectClosed` → `Window.Close` 调用链并指出顺序必须反转。
- **Status**: READY_FOR_REVIEW（待独立架构审查；**不宣布架构验收通过**）
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`（PR #29 合并提交）
- **分支**: `feat/wp-30b-presentation-rendering`
- **审查范围**: ADR-WP-30B-07 / 08 / 09 / 10 / 11 及现有实现只读核验
- **排除范围**: 不实现渲染、不修改源代码、不合并 PR、不触碰 `docs/map-editor/implementation/`

## 1. 审查方法与实际证据

| 手段 | 结果 |
|---|---|
| `git status` / `git log` / `gh pr list` | 基线 Commit 与 PR #29（MERGED，URL 见交付报告）核实 |
| 逐文件阅读 `docs/implementation/wp-30a/SNAPSHOT_LIFECYCLE.md`、`docs/implementation/wp-30b/PHASE1-F1-CLOSURE.md`、`docs/implementation/wp-30a-review/WP-30B_READINESS.md`、`docs/implementation/wp-30a/ARCHITECTURE.md`、`docs/implementation/wp-30a/IMPLEMENTATION_SUMMARY.md` | 保留 F-1 屏障与读者契约；接线入口明确 |
| 阅读 `WorldView.Presentation` / `Desktop` / `Application` 实际代码 | 确认 `Publisher.Current` 为 `Volatile.Read`；`WorkspaceViewState` 可变；尚无 viewport 控件 |
| 检查各 `.csproj` / `obj/project.assets.json` | Avalonia 12.1.6、SkiaSharp 3.119.4（传递）来源明确 |
| 对本地 `Avalonia.Base.dll` / `Avalonia.Controls.dll` / `Avalonia.Skia.dll` / `SkiaSharp.dll` 做反射探测（独立临时工程，未在仓库落盘） | 核实 `ICustomDrawOperation`、`ImmediateDrawingContext.TryGetFeature`、`ISkiaSharpApiLease`、`SKSurface.Create` 重载、`Control.SizeChanged`、`TopLevel.ScalingChanged`、`Window.Closing` 等 API 真实存在 |
| 查阅 Avalonia 官方文档（Threading model / Architecture / Native interop / ICustomDrawOperation / Custom-drawn controls） | 核实 UI 线程模型与 lease 回调内释放模式；不虚构组合器内部线程 |
| `grep` 依赖引用与 Skia/Avalonia 标识符；`git diff --stat` | 无跨层引用；本次变更仅新增 5 份 ADR + 本审查文档 |
| R1 二次审查：按"实现成本"三项关注点逐条核对 ADR-07/08/10/11 与既有代码 | 第 1 项已满足；第 2、3 项补强并同步五份 ADR（见 §3A） |
| R2 生命周期审查：核对 Avalonia 12.1.6 视觉树/窗口事件语义与 ADR-10 的 Detach 处理 | 确认 Detach 不等于永久关闭；改为 A/B/C 三分并补重新挂载方案（见 §3B）；C 随后在 R3 拆为 C1/C2 |
| R3 关闭路径审查：逐行核验 `ProjectSession.cs` / `MapWorkspaceSession.cs` / `ProjectWorkspaceViewModel.cs` 的关闭方法 | 确认 `CloseWorkspaceAsync` 与 `CloseAsync` 作用域完全不同；拆为 C1 / C2 并记录子工作区 Dispose 缺口（见 §3C） |
| R4 C2 编排审查：核验 `CloseProjectAsync` → `ProjectClosed` → `Window.Close` 调用链与 `ProjectWindow` 事件处理器 | 确认现有顺序是"先关会话、后关窗口"且无 `Closed` 处理器；统一为单一编排入口并给出幂等契约（见 §3C） |

`docs/implementation/wp-30b/WP-30B_READINESS.md` **不存在**；仓库实际文件为
`docs/implementation/wp-30a-review/WP-30B_READINESS.md`。按要求以实际路径为准读取，未新建替身文件。

## 2. 跨 ADR 一致性检查（12 项）

| # | 检查项 | 结论 | 证据 / 说明 |
|---|---|---|---|
| 1 | 是否出现循环依赖 | ✅ 无 | 项目引用：`Application→Domain`、`Presentation→Application`、`Infrastructure→Application`、`Desktop→Application + Infrastructure`。无反向引用；ADR 中 Renderer 仅在 Desktop 内。 |
| 2 | Domain 是否引用 Avalonia / SkiaSharp | ✅ 否 | `WorldView.Domain` 源码与 csproj 零命中；ADR 未要求新增引用。 |
| 3 | Application 是否引用 Avalonia / SkiaSharp | ✅ 否 | `WorldView.Application` 源码与 csproj 零命中（仅注释提及边界）。`WorkspaceViewState` 仍只有 double/string/HashSet。 |
| 4 | SceneSnapshot 是否包含 Skia 对象 | ✅ 否 | `SceneSnapshot.cs` 字段仅 `ContextFingerprint` / `PresentationContext` / `long` / `ImmutableArray<SceneLayer>` / 诊断；既有反射测试锁定。ADR-09 明令禁止。 |
| 5 | Renderer 是否有明确资源所有权 | ✅ 有 | ADR-09 所有权表：lease 与自建 `SKPaint`/`SKPath` 归本次回调；canvas/surface 归 Avalonia 且只借用；Renderer 无 Skia 字段；M1 零缓存。 |
| 6 | UI 与 Render Execution 线程边界是否一致 | ✅ 一致 | ADR-07/08/10/11 同口径：UI 线程写 ViewState、由 `binding.CaptureFrame()` 捕获帧输入、入队失效；绘制只在 Avalonia `ICustomDrawOperation.Render` 回调，入口检查 `binding.IsClosed`；回调不回读控件/ViewModel/Publisher。Detach 只停失效，不跨线程、不置关闭。 |
| 7 | Workspace 关闭是否与 F-1 关闭屏障兼容 | ✅ 兼容 | ADR-10 §7.1（C1）与 §7.2（C2）都复用同一子序 `Coordinator.CloseAsync()` → `MarkCoordinatorClosed()`（publisher gate 线性化点）→ `Publisher.Dispose()`；C2 由单一编排入口 `CloseProjectLifetimeAsync()` 先串行关闭全部 Binding，再调用 `ProjectSession.CloseAsync()`；触发点排除可取消的 `Window.Closing`。 |
| 8 | Viewport 关闭是否会错误关闭整个 Workspace | ✅ 不会 | ADR-10 §1 四分：Detach 不销毁任何对象；永久销毁（B）只关该视口的 Binding/Runtime/Renderer；工作区由 C1（`CloseWorkspaceAsync`，仅目标工作区）或 C2（`CloseAsync`，全部工作区）结束。拒绝"控件 Dispose 直接关会话"与"单工作区关闭复用 `CloseAsync`"。 |
| 9 | 是否重复实现 Camera 或 Snapshot 生命周期 | ✅ 无 | 不新增 Camera 类型，相机仍是 ViewState 三个标量 + 帧输入复制值；快照仍只有一套 publisher/coordinator 生命周期；Renderer 不持有快照。 |
| 10 | 是否出现不必要接口 / 抽象工厂 / 事件总线 / 调度器 | ✅ 无 | 仅用框架接口 `ICustomDrawOperation` 与 `ISkiaSharpApiLeaseFeature`；Renderer 与 `MapViewportBinding` 均为 `internal sealed` **具体类**（binding 只为拆分职责，不是可替换后端/DI 抽象）；无事件总线、无队列、无调度器、无资源池。 |
| 11 | 是否与现有 WP-30A 公共 API 冲突 | ✅ 无冲突 | `git diff` 中无源文件变更；五份 ADR 仅描述未来接线；既有 `PresentationBoundaryTests` / `CoordinatorClosePublicationRaceTests` 未删除或放宽。 |
| 12 | Windows / macOS 是否都能使用选定方案 | 🟡 接口层可用，运行时未验证 | Avalonia.Skia 12.1.6 + SkiaSharp 3.119.4 原生资产覆盖 Win32/macOS；无 RID/平台分支。**macOS 实际窗口绘制尚未运行验证**（ADR-07 Open Question 3；`PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md` §19 亦标注 macOS runtime 未验证）。 |

## 3. ADR 之间的交叉引用一致性

| 主题 | ADR-07 | ADR-08 | ADR-09 | ADR-10 | ADR-11 | 一致性 |
|---|---|---|---|---|---|---|
| 绘制入口 | `ICustomDrawOperation` | 只在 Avalonia 回调；入口检查关闭标志 | lease 在回调内 | 永久销毁后入口返回；Detach 只停失效 | 帧输入驱动 | ✅ |
| 快照引用 | `binding.CaptureFrame()` 捕获一次 | 同一引用全程复用 | 快照无 Skia | publisher 不清空 Current；重新挂载读最新 | 引用放入帧输入，不深拷贝 | ✅ |
| Skia 资源 | lease 借用 canvas | 不跨线程 | 单所有者 / using | Renderer 空操作 Dispose | 不进帧输入 | ✅ |
| Camera | ViewState 标量 + 视口复制 | UI 单写者 | 不适用 | 随工作区结束 | 进入帧输入的复制值 | ✅ |
| 关闭 | Detach 不关闭；永久销毁才置标志 | 仅新进入的 Render 被抑制；在飞帧可继续 | 局部资源自释放；在飞帧用局部对象 | B：屏障 → publisher → renderer；C1：+`CloseWorkspaceAsync`；C2：单一编排入口先遍历全部 Binding 再 `CloseAsync` | 未关闭时捕获点持续可用 | ✅ |
| Renderer 公开面 | internal sealed，无接口 | 无状态 | 无 Skia 字段 | Dispose 幂等 | 只读帧输入 | ✅ |

**原始五项裁决之间未发现相互冲突**，因此无需互相回改；R1/R2 的补强见 §3A/§3B。
以下两项是"刻意保留的开放点"，不是冲突：
- ADR-07 允许实现期决定 draw operation 的 `Equals` 字段；ADR-11 的帧输入提供候选字段。
- ADR-09 的资源账本测试手段在实现期选择，不改变所有权裁决。

## 3A. 补充裁决（R1：实现成本关注点）

独立审查前，按"后续实现成本"的三项关注点做了二次核对。第 1 项已满足；第 2、3 项补强如下。

| # | 关注点 | R1 前的状态 | R1 补强 | 受影响 ADR |
|---|---|---|---|---|
| 1 | 是否提前建独立渲染线程 | ✅ 已满足 | 无改动：无 Render Thread、无调度器、绘制只用 Avalonia 回调 | 08 |
| 2 | 控件是否变成巨型对象 | 🟡 控件虽不"拥有"Camera/Snapshot/Skia，但同时持有 Runtime + Renderer，并负责 generation、请求、捕获，是唯一 wire point | 抽出 Desktop 内部具体类 `MapViewportBinding`，持有 Runtime / Renderer / generation / 捕获 / 关闭标志；控件只保留 Avalonia 本职，并只持有一个 binding 引用 | 07 / 10 / 11 |
| 3 | 关闭后绘制保证是否准确 | 🟡 文档写"在飞帧允许自然完成"，且 R1 的"关闭后不再启动命令"易被读成零 post-close 绘制 | draw operation `Render` 入口检查 `volatile bool IsClosed`：读到 true 立即返回；已读到 false 的在飞帧允许继续；明确不保证 Close 返回后零绘制调用 | 07 / 08 / 09 / 10 |

### 关闭后绘制的保证边界（精确定义）

允许的交错（R2 修正）：

```text
Render enters
  ↓
Reads IsClosed == false
  ↓
Close sets IsClosed = true
  ↓
Close returns
  ↓
Already-entered Render may continue
```

| 项 | 结论 |
|---|---|
| **保证 1** | `Render` 在入口读到 `IsClosed == true` 时立即返回：不取 lease、不建 Skia 资源、不发绘制命令。 |
| **保证 2** | 关闭置位前已读到 `IsClosed == false` 的在飞帧允许自然完成，可能继续创建资源并发命令。 |
| **不保证 1** | 关闭命令返回后绝对没有绘制调用——在飞帧可能仍在执行。 |
| **不保证 2** | 中断已经进入的回调或回滚其绘制。Avalonia 12.1.6 未公开取消契约。 |
| **不等待** | 不等待在飞帧结束；UI 线程不等待渲染完成。 |
| **不引入** | 锁、信号量、帧屏障、独立 Render Thread。 |
| **与 F-1 的关系** | 两层互不替代：入口检查抑制新进入的绘制；F-1 屏障拒绝关闭后的快照发布。 |

### R1 专项一致性核查

| 检查项 | 结论 |
|---|---|
| `MapViewportBinding` 是否成为不必要抽象 | ✅ 否。具体类、无接口、无 DI、无事件；仅把非 Avalonia 职责移出控件，并为关闭顺序提供单一执行者。 |
| 控件是否仍持有 Runtime / Renderer / Skia 资源 | ✅ 否。控件只持有一个 binding 引用；binding 持有 Runtime/Renderer；Skia 仍在回调内。 |
| 关闭置位后新进入的绘制是否被抑制 | ✅ 是。入口读到 true 即返回，先于任何 lease/资源获取；已通过入口检查的在飞帧可继续（ADR-08/10 已明示）。 |
| 是否与 ADR-09 的所有权冲突 | ✅ 无。Renderer 仍无 Skia 字段；binding 只是其持有者，不是第二所有者。 |

## 3B. 生命周期专项审查（R2）

### 四种生命周期（最终裁决）

| 事件 | 触发 | Binding / Runtime | Workspace | 绘制 |
|---|---|---|---|---|
| **A. 暂时脱离** | `DetachedFromVisualTree`（可能由标签切换、reparent、模板重建、移出布局触发；外壳需实测） | 原样保留；不置 `IsClosed`、不 Dispose | 不受影响 | 停止（不在树中）；重新 Attach 后恢复 |
| **B. 永久销毁** | 拥有者显式命令（只关视口） | `IsClosed=true` 终态；Runtime / Renderer 释放 | 不自动关闭 | 关闭后新进入的 `Render` 返回；在飞帧按 §3A 边界 |
| **C1. 单个 Workspace 关闭** | 单工作区"关闭"命令 | 先执行 B（仅该视口） | `CloseWorkspaceAsync(workspaceId)` → `Workspaces.Remove` → `workspace.DisposeAsync()`；**不得**调 `CloseAsync` | 同 B |
| **C2. ProjectSession / 窗口关闭** | 唯一编排入口 `CloseProjectLifetimeAsync()`，触发点只有两个：关闭项目命令、窗口 `Closed` | 先遍历关闭**全部**存活 Binding（串行、同子序） | 随后 `ProjectSession.CloseAsync()`：全部子工作区 → AssetStore → Database → 清理视图集合并通知窗口 | 同 B |

**Detach 不再被等同于 Workspace.Close，也不再被等同于永久销毁。**

### Avalonia 12.1.6 语义核验

| API | 语义 | 对方案的含义 |
|---|---|---|
| `AttachedToVisualTree` | 控件附着到 rooted visual tree，可多次触发 | 重新挂载是正常路径，不是异常 |
| `DetachedFromVisualTree` | 控件脱离 rooted visual tree，未声明永久性 | 不能据此销毁 Binding 或关闭 Workspace |
| `Window.Closing` | 关闭前触发，`WindowClosingEventArgs : CancelEventArgs`，可 `Cancel` | 不可在此做不可逆销毁 |
| `Window.Closed` | 关闭完成后触发，`EventHandler` 无取消参数 | 窗口级销毁的可靠触发点 |

`Visual.IsAttachedToVisualTree` 在 12.1.6 为 internal，未作为公开契约使用。

### 重新挂载方案

1. Detach：控件只置 `_isAttached=false`，不调用关闭、不释放 Runtime。
2. Attach：若 `binding.IsClosed` 为 true → 拒绝绘制并报诊断（终态，需新建 Binding）；否则复用原 Binding。
3. 恢复：尺寸 > 0 时推进 generation 并重发一次请求（Detach 期间可能错过数据变化）；尺寸为 0 时等首个有效 `SizeChanged`；随后 `InvalidateVisual()`。
4. 下一帧：`CaptureFrame()` 读取 `Publisher.Current` 最新快照；不重放中间帧。
5. 永久销毁后要重新显示：工厂创建**新的** Binding + Runtime；旧控件与旧 Binding 丢弃，绝不复用。

### 所有权唯一性

| 对象 | 唯一创建者 | 唯一销毁入口 |
|---|---|---|
| `MapViewportBinding` | Desktop 组合根 workspace 工厂 | 拥有者 `binding.CloseAsync()`；`Window.Closed` 仅兜底调用同一入口 |
| `ViewportPresentationRuntime` | Binding 构造函数 | `binding.CloseAsync()` |
| `Renderer` | Binding 构造函数 | `binding.CloseAsync()`（Publisher 之后） |
| `MapViewportControl` | Desktop 视图层 | 随可视树/窗口销毁；Detach 不销毁 |

### R2 专项一致性核查

| 检查项 | 结论 |
|---|---|
| Detach 是否仍被等同于 Workspace.Close | ✅ 否。Detach 不调用 `CloseWorkspaceAsync`，不置 `IsClosed`，不 Dispose Runtime。 |
| 重新挂载是否有明确方案 | ✅ 有。保留 Binding/Runtime；Attach 检查终态；`InvalidateVisual()` 后读最新快照。 |
| Binding 的创建者与销毁者是否唯一 | ✅ 创建者唯一（工厂）；销毁入口唯一（`CloseAsync`），`Window.Closed` 不是第二套逻辑。 |
| Runtime 创建/销毁是否有明确所有权 | ✅ Binding 构造时创建，`CloseAsync` 中销毁。 |
| Workspace 关闭是否保留 F-1 屏障 | ✅ 保留：`Coordinator.CloseAsync()` → `Publisher.Dispose()` 顺序未变。 |
| 在飞帧是否可能访问已释放的共享资源 | ✅ 否。在飞帧只用局部 Skia 对象与不可变快照，不读 Runtime/Publisher 状态；M1 Renderer 无状态且 `Dispose()` 空操作，调用其绘制方法安全。 |
| 六份文档对关闭保证是否一致 | ✅ 07/08/09/10/11 与本文均按 §3A 的时序与 7 条边界表述。 |

## 3C. 关闭路径专项审查（R3）

R2 的第三种生命周期把“关闭工作区”和“关闭项目”写成一条链（末尾接 `ProjectSession.CloseAsync()`），
在语义上会让“关闭一个标签页”等于“关闭整个项目”。R3 逐行核验三个真实源文件后拆为 C1 / C2。

### 三个真实源文件的核验结果

| # | 事实 | 代码位置 | 结论 |
|---|---|---|---|
| 1 | `CloseWorkspaceAsync(workspaceId)` 只找到并关闭目标工作区，从 `_workspaces` 移除后返回 `bool`；不触碰兄弟工作区、会话状态、`AssetStore`、`Database` | `ProjectSession.cs:182-202` | ✅ 与 R3 前提一致 |
| 2 | `ProjectWorkspaceViewModel.CloseWorkspaceAsync` 依次执行 `Session.CloseWorkspaceAsync` → `Workspaces.Remove` → `workspace.DisposeAsync()` | `ProjectWorkspaceViewModel.cs:131-145` | ✅ 三连顺序确认 |
| 3 | `CloseAsync()` 是项目级入口：置 `Closing` → 循环关闭并移除**全部**子工作区 → `AssetStore.DisposeAsync` → `Database.DisposeAsync` → 置 `Closed` → 触发 `SessionClosed` | `ProjectSession.cs:204-253` | ✅ 与单工作区关闭语义完全不同 |
| 4 | `CloseAsync()` 的子工作区循环**只**调用 `workspace.CloseAsync()`，**没有** `workspace.DisposeAsync()` | `ProjectSession.cs:223-239` | 🟡 **既有缺口**，见下 |
| 5 | `ProjectSession.DisposeAsync()` 是 `CloseAsync()` 的等价入口；状态已 `Closing`/`Closed` 时直接返回 | `ProjectSession.cs:255-264` | 🟡 不会补上第 4 项的 Dispose |
| 6 | `MapWorkspaceSession.CloseAsync()` 置 `_closed` 并取消 `_derivedWorkCts`；`DisposeAsync()` 释放该 CTS。二者职责不同，且必须先 Close 后 Dispose | `MapWorkspaceSession.cs:81-102` | ✅ 两件事不可合并 |
| 7 | `ProjectWindow` **没有** `Closing` / `Closed` 处理器；会话关闭由 `CloseProjectAsync` → `ProjectClosed` → `Window.Close` 驱动（`fresh.ProjectClosed += Close`） | `ProjectWindow.axaml.cs:9-40`（尤其 26-29） | ✅ 引入 `Closed` 兜底属新增接线，必须汇聚到同一编排方法 |
| 8 | `CloseProjectAsync` 当前是 `await Session.CloseAsync(ct)` → `ProjectClosed?.Invoke()`：**先关会话、后关窗口** | `ProjectWorkspaceViewModel.cs:150-156` | 🟡 接入视口后顺序错误：Binding 仍活动而包级资源已释放。R4 要求改为"关 Binding → 关会话 → 通知窗口" |
| 9 | 项目级当前没有"存活 Binding 集合"；关闭也没有单次启动状态 | `ProjectWorkspaceViewModel.cs` 全文 | 🟡 C2 需要一个可枚举集合与一个"已启动"状态，属 Phase 3 接线，不是零成本 |

### R4：C2 双入口的统一

R3 只说"`Window.Closed` 归 C2"，没有回答它与"关闭项目命令"如何共存。核验后确认现状是
**两个可能的触发点 + 一条会先关会话的调用链**：

- `CloseProjectAsync`：`Session.CloseAsync(ct)` → `ProjectClosed` → `Window.Close()`
  （`ProjectWorkspaceViewModel.cs:150-156`、`ProjectWindow.axaml.cs:26-29`）；
- `ProjectWindow` 无 `Closing` / `Closed` 处理器，用户点 X 时今天没有任何编排。

因此 R4 在 ADR-10 §7.2 定义唯一编排入口 `CloseProjectLifetimeAsync()`，两个触发点都只调它，
并给出"最多启动一次 / 重复调用等待首次 / 不重复释放"的幂等契约；`binding.CloseAsync()` 的幂等性
被明确判定为**不能**替代该契约。

### C1 / C2 流程对照

| 维度 | C1 单个 Workspace 关闭 | C2 ProjectSession / ProjectWindow 关闭 |
|---|---|---|
| 触发 | 单工作区“关闭”命令 | “关闭项目”命令；窗口 `Closed` 兜底 |
| 编排入口 | 无独立方法，在 `CloseWorkspaceAsync` 命令体内内联 | **唯一编排方法** `CloseProjectLifetimeAsync()`；两个触发点都只调它 |
| 幂等单位 | 单个 `binding.CloseAsync()` + `CloseWorkspaceAsync` 自身 | **C2 整体最多启动一次**；单 Binding 幂等不足以替代 |
| Binding 范围 | 仅该工作区对应的一个 | 该会话下**全部**存活 Binding，逐个关闭 |
| Binding 子序 | `IsClosed` → Coordinator（F-1 屏障）→ Publisher → Renderer | 与 C1 完全相同，对每个 Binding 各执行一次 |
| Application 调用 | `ProjectSession.CloseWorkspaceAsync(workspaceId)` | `ProjectSession.CloseAsync()` |
| 集合处理 | `Workspaces.Remove(workspace)` | `CloseAsync` 内部移除全部子工作区 |
| 工作区 Dispose | `workspace.DisposeAsync()` 显式调用 | **当前未调用**（缺口，不虚构） |
| 兄弟工作区 | 不受影响 | 全部关闭 |
| Asset Store / Database | **不得释放** | 在全部子工作区关闭后释放 |
| 会话状态 | 保持 `Ready` | `Closing` → `Closed` |

### 关闭流程核查项

| 检查项 | 结论 |
|---|---|
| Detach 是否仍被错误等同于 Workspace.Close | ✅ 否。Detach 不调用 `CloseWorkspaceAsync`、不调用 `CloseAsync`、不置 `IsClosed`。 |
| 单工作区关闭是否可能关闭兄弟工作区或整个会话 | ✅ 不会。C1 硬约束只允许 `CloseWorkspaceAsync(workspaceId)`，禁止出现 `CloseAsync()`。 |
| 项目关闭是否先关闭它拥有的全部 Binding | ✅ 是。C2 步骤 1 显式遍历全部存活 Binding，之后才调用 `ProjectSession.CloseAsync()`。 |
| C2 是否有唯一编排入口 | ✅ 是。`CloseProjectLifetimeAsync()`；不得在 `ProjectWindow` 或其它位置复制"关 Binding + 关会话"的组合。 |
| 正常命令与 `Window.Closed` 是否汇聚到同一入口 | ✅ 是。两者都只调用该编排方法；`Closed` 不维护独立清理流程。 |
| 幂等是否覆盖整条 C2（而非仅单个 Binding） | ✅ 契约明确：最多启动一次、重复调用等待首次、不重复关会话与释放资源。 |
| 关闭顺序是否"先 presentation、后 Application" | ✅ 契约是；但现有 `CloseProjectAsync` 是反序（`Session.CloseAsync` 在前），**接线时必须改写**，已记为待办风险。 |
| `Window.Closed` 归属是否正确 | ✅ 归 C2，不归 C1，也不归“单个 Tab 关闭”。 |
| Binding 创建者与销毁者是否唯一 | ✅ 创建者唯一（工厂）；销毁入口唯一（`CloseAsync`）。C2 是同一入口的多次调用，不是第二套逻辑。 |
| Runtime 创建/销毁所有权 | ✅ Binding 构造时创建，`binding.CloseAsync()` 中销毁。 |
| F-1 屏障是否保留 | ✅ `Coordinator.CloseAsync()` → `Publisher.Dispose()` 顺序在 C1/C2 均未改变。 |
| 在飞帧是否可能访问已释放的共享资源 | ✅ 否。在飞帧只用局部 Skia 对象、不可变快照与无状态 Renderer。 |
| 项目关闭时子工作区 CTS 是否已释放 | 🟡 **未释放**。`CloseAsync` 不调用 `DisposeAsync`，已记为既有缺口，本轮不改代码。 |
| 六份文档对两条关闭路径的表述是否一致 | ✅ ADR-10 §1/§4/§6/§7/§8A/§8B 与本文 §3B/§3C 同口径；07/08/09 只引用 C2 的单一编排入口，11 不涉及路径选择，无冲突表述。 |

## 4. 与既有文档的关系

| 既有文档 | 关系 | 处置 |
|---|---|---|
| `docs/map-engine/PRA-01_M1_IMPLEMENTATION_PROFILE_v1.md` §2.1 | 已定"Avalonia Control + ICustomDrawOperation + Skia"组合架构 | ✅ ADR-07 是其收窄落地，不推翻 |
| `docs/implementation/wp-30a/IMPLEMENTATION_SUMMARY.md` §7 | 交接：工厂内创建 runtime、draw 回调捕获 `Current`、显式 Skia 引用 | ✅ ADR-07/10/11 沿用；显式依赖若做仍锁 3.119.4 |
| `docs/map-engine/PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md` | 旧生产架构基线，文本写 ".NET 10" | 🟡 Profile §1.2 已声明 net11.0 取代该表述；本文不引用其框架版本，仅沿用其"自定义绘制点 + 租约 + 后端资源不回写快照"原则 |
| `docs/map-editor/implementation/` | 用户明确禁止触碰 | ✅ 未读取内容以外用途，未修改、未纳入提交 |

## 5. 遗留风险（不可隐藏）

| 严重度 | 风险 | 影响 | 建议 |
|---|---|---|---|
| 🟡 P2 | `ICustomDrawOperation.Render` 的实际 OS 线程未在本仓库运行验证；官方文档只固定控件侧 UI 线程模型 | 若实现期误把回调当 UI 线程或误起后台绘制，会违反 ADR-08 | 以"线程不敏感"约束为准；实现期记录实测线程，不据此放宽纪律 |
| 🟡 P2 | macOS 上真实窗口绘制/DPI/关闭尚未运行验证（CI 仅构建） | 跨平台承诺目前只有包级证据 | 实现阶段补 macOS 运行时证据，未完成前不得宣称跨平台验收通过 |
| 🟡 P2 | DPI 变化与 generation 推进的精确时序未实测 | 可能出现重复失效或滞后一帧 | 用 `TopLevel.ScalingChanged` + 真实窗口测量后确认 |
| 🟡 P2 | draw operation `Equals` 与失效策略未定 | 错误定义会导致不重绘或每帧重绘 | 实现期以测试锁定；候选字段见 ADR-11 帧输入 |
| 🟡 P2 | 关闭命令返回后仍可能有在飞帧的绘制调用（已声明为不保证） | 不是缺陷，但需产品/测试接受"不是零 post-close 调用" | 用 `Render` 调用计数与关闭标志命中数观测；不得据此改成等待渲染 |
| 🟢 P3 | 暂时 Detach 期间 Runtime / Publisher / 最后快照继续驻留 | 长期隐藏的视口会持有内存与快照 | M1 可接受；若出现大量长期隐藏视口，再评估显式 Suspend（需新 ADR） |
| 🟢 P3 | M1 每帧新建 `SKPaint` 的分配成本无基准 | 未来大数据量可能成为瓶颈 | 实测后再决定是否重开缓存裁决（新 ADR） |
| 🟢 P3 | M1 限定一个 Workspace 一个 Viewport | 多视口需求需重构相机归属 | 需求出现时先拆分每视口状态 |
| 🟡 P2 | **既有缺口**：`ProjectSession.CloseAsync()` 不调用子工作区的 `DisposeAsync()` | C2 路径上 `MapWorkspaceSession._derivedWorkCts` 未被释放；文档不能声称已释放 | 非本轮引入。实现阶段单独裁决修复方式（循环内补 `DisposeAsync`，或让 `CloseAsync` 承担释放）并补幂等测试；补齐前文档保持“未释放”表述 |
| 🟡 P2 | `ProjectWindow` 当前无 `Closing`/`Closed` 处理器；`CloseProjectAsync` 直接 `Session.CloseAsync()` → `ProjectClosed` → `Window.Close` | 用户直接点窗口 X 时无人关 Binding；接入 `Closed` 后若不汇聚则会有两套清理 | 接线时把 `Closed` 只接到 `CloseProjectLifetimeAsync()`，并改写命令体改为委托同一方法；不得接到 C1 |
| 🟡 P2 | 现有项目关闭顺序是"先关会话、后关窗口"，与 R4 要求的"先关 Binding、后关会话"相反 | 照现状接线会在 presentation 仍活动时释放 `AssetStore` / `Database` | 这是 R4 明确要求改变的一处顺序（仅 Desktop ViewModel 层）；实现时必须先插入 Binding 关闭再调 `CloseAsync` |
| 🟡 P2 | C2 幂等只写了行为契约，未定实现类型 | 若各调用方自行判断"已关闭就返回"，可能提前结束而留下未完成关闭 | 实现用单个私有字段记忆首次调用；测试必须覆盖"命令与 `Closed` 连续触发只跑一套" |
| 🟡 P2 | C1 / C2 共用前段（Binding 关闭）但末段不同，实现时容易误接 | 误接一步即“关标签页 = 关项目”或“项目关闭漏关视口” | 以 ADR-10 §7.1/§7.2 的步骤编号逐一对照实现；用测试锁定 C1 不影响兄弟工作区 |

## 6. 本阶段变更审计

| 项 | 结果 |
|---|---|
| 新增文档 | `ADR-WP-30B-07` / `08` / `09` / `10` / `11` + 本文件（共 6 份）；R1–R4 仅修订这 6 份，无新增文件 |
| 源代码变更 | **无**（`git diff` 空） |
| 新增依赖 / NuGet | **无** |
| 公共 API 变更 | **无** |
| 测试变更 | **无** |
| 禁止目录 | `docs/map-editor/implementation/` 未触碰（仍为原有未跟踪目录） |
| 提交目标 | 开发分支 `feat/wp-30b-presentation-rendering`，独立提交；不向 master 提交、不合并 PR |

## 7. 结论

五份 ADR 与既有 WP-30A / F-1 契约之间没有发现冲突；方案在接口与包级证据上支持 Windows 与 macOS。
线程、DPI 时序与 `Equals` 失效策略属于实现期必须补证据的开放点，已逐项记录在风险表。

R1/R2 后，控件边界与生命周期语义已收敛：控件只保留 Avalonia 职责并持有单个 binding；
Runtime/Renderer/关闭标志由 binding 单一持有；Detach 与永久销毁、Workspace 关闭分离，
重新挂载可恢复绘制；关闭保证按"入口命中即返回、在飞帧可继续"的真实时序定义，
不再声称零 post-close 绘制。未新增接口、依赖、调度器或源代码变更。

R3 后，关闭路径也不再混淆：C1（单工作区）与 C2（项目会话/窗口）按三个真实源文件的作用域分开，
C1 禁止出现 `ProjectSession.CloseAsync()`，C2 必须先遍历关闭全部 Binding 再关会话。
同时如实记录一个既有缺口——项目关闭时子工作区的 `DisposeAsync()` 未被调用；本轮不改代码，
只把它标为后续待处理项，未在文档中虚构为已完成。

R4 后，C2 的两个触发点收敛为一个入口：`CloseProjectLifetimeAsync()` 是唯一业务关闭方法，
`CloseProjectAsync` 与 `Window.Closed` 都只调它；关闭顺序被明确固定为
"关闭全部 Binding → `ProjectSession.CloseAsync` → 清理视图集合 → 通知窗口"，并指出这与当前
`CloseProjectAsync` 的实际顺序相反，接线时必须改写该命令体。幂等契约写在行为层（最多启动一次、
重复调用等待首次、不重复释放），不引入锁、调度器或生命周期管理器。

**状态：READY_FOR_REVIEW**。等待独立架构审查；本阶段不宣布架构验收通过，不解锁实现。
