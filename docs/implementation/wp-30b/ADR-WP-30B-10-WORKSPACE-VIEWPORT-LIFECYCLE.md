# ADR WP-30B-10 — Workspace / Viewport 生命周期接线

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— `MapViewportBinding` 成为 Runtime / Renderer 的唯一持有者与关闭入口；控件只持有引用。
- **修订**: R2（2026-11-24）— 区分「暂时脱离视觉树」「视口永久销毁」「工作区显式关闭」三种生命周期；Detach 不再等同于关闭；修正"关闭后零绘制"的过度表述。
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`
- **相关**: `ADR-WP-30B-07`、`ADR-WP-30B-08`、`ADR-WP-30B-09`、`ADR-WP-30B-11`

## Context

现有代码已定义应用侧生命周期，但尚未接入视口：

- `ProjectSession.OpenWorkspaceAsync` 创建 `MapWorkspaceSession` 并加入 `_workspaces`；
  `CloseWorkspaceAsync` 关闭单个工作区并移除；`CloseAsync` 先关全部子工作区，再逆序释放
  Asset Store → Database（`WorldView.Application/Sessions/ProjectSession.cs:144-253`）。
- `MapWorkspaceSession` 构造时创建 `WorkspaceViewState`，`CloseAsync()` 置 `_closed` 并取消
  `_derivedWorkCts`，`DisposeAsync()` 释放该 CTS（`MapWorkspaceSession.cs:20-26,82-102`）。
- `ProjectWorkspaceViewModel` 持有 `Workspaces` 集合，`CloseWorkspaceAsync` 命令依次为
  `Session.CloseWorkspaceAsync` → `Workspaces.Remove` → `workspace.DisposeAsync()`
  （`ProjectWorkspaceViewModel.cs:131-145`）。
- WP-30A 交接要求：每个 `MapWorkspaceSession` 对应一个 `ViewportPresentationRuntime`，关闭时
  `DisposeAsync`（`IMPLEMENTATION_SUMMARY.md` §7）。
- `ViewportPresentationRuntime.DisposeAsync()` **先** `Coordinator.DisposeAsync()`（其中
  `CloseAsync()` 在发布器锁内置位 F-1 屏障），**后** `Publisher.Dispose()`；顺序即契约
  （`ViewportPresentationRuntime.cs:56-60`；`PresentationRequestCoordinator.cs:393-418`；
  `SNAPSHOT_LIFECYCLE.md` §5.1/§7）。

R1 引入 Desktop 内部具体类 `MapViewportBinding`，持有 Runtime、Renderer、generation 与
`volatile bool IsClosed`，是唯一的关闭入口。

**R2 修正的生命周期矛盾**：R1 文本把 `DetachedFromVisualTree` 当作关闭信号。Avalonia 12.1.6 的
`DetachedFromVisualTree` 只表示控件脱离 rooted visual tree（重新挂载、切换标签页、模板重建、
移出布局等**都可能**触发，具体取决于外壳实现），并不表示用户永久关闭视口。若在 Detach 时置
`IsClosed`，同一个控件重新挂载后将永远无法恢复绘制，且可能误关工作区。因此必须把
「暂时脱离」与「永久销毁」分开。

本 ADR 只规定对象之间"谁创建谁、谁持有谁、按什么顺序释放"，不实现接线。

## Decision Drivers

1. 保留 WP-30A 的每视口独立 runtime 与 F-1 关闭屏障，不改写 Publisher 语义。
2. 工作区（Application 会话）与视口（Desktop 控件）是不同生命周期，不能互相隐式销毁。
3. **视觉树挂载状态不能决定业务生命周期**：Detach 既不是销毁，也不是工作区关闭。
4. Binding / Runtime 必须有唯一的创建者与唯一的销毁入口，不依赖控件析构。
5. 暂时脱离后重新挂载必须能恢复绘制；永久销毁后必须创建新的 Binding / Runtime，绝不复用已关闭对象。
6. 不引入通用生命周期管理器、事件总线、全局注册表或跨视口协调服务。
7. 关闭顺序必须有明确依据；关闭保证的措辞不得暗示"零 post-close 绘制"。

## Decision

### 1. 三种生命周期（不可混用）

| 事件 | 典型触发 | Binding / Runtime | Workspace | 绘制 |
|---|---|---|---|---|
| **A. Viewport 暂时脱离视觉树** | 可能由标签页切换、重新挂载（reparent）、模板重建、控件暂时移出布局等触发；具体外壳需实测 | **原样保留**，不置 `IsClosed`、不 Dispose | 不受影响 | 自然停止（不在树中，Avalonia 不再调用 `Render`）；重新挂载后恢复 |
| **B. Viewport 永久销毁** | 关闭该视口/标签/窗口；拥有者显式调用 `binding.CloseAsync()` | `IsClosed = true`（终态）；Coordinator → Publisher → Renderer 依次释放；Runtime 销毁 | **不自动关闭** | 关闭入口之后新进入的 `Render` 立即返回；已进入的在飞帧按 §5 边界处理 |
| **C. Workspace 显式关闭** | "关闭工作区 / 关闭项目"命令；宿主窗口 `Closed` | 先执行 B 的 binding 关闭 | 随后 `MapWorkspaceSession.CloseAsync` → `ProjectSession` 按需继续 | 同 B |

**判定规则**：`DetachedFromVisualTree` **只**对应 A，永远不触发 B 或 C。B 由视口宿主拥有者的
显式命令决定；C 由工作区/项目命令或窗口 `Closed` 决定。控件自身没有权限销毁 Binding。

### 2. Avalonia 12.1.6 生命周期事实（已核实）

| API | 12.1.6 语义 | 证据 |
|---|---|---|
| `Visual.AttachedToVisualTree` | "Raised when the control is attached to a **rooted visual tree**"；同一控件可多次触发 | `avalonia/12.1.6/lib/net10.0/Avalonia.Base.xml` `E:Avalonia.Visual.AttachedToVisualTree` |
| `Visual.DetachedFromVisualTree` | "Raised when the control is detached from a **rooted visual tree**"；未声明"永久"或"窗口关闭"，可再次 Attach | 同上 `E:Avalonia.Visual.DetachedFromVisualTree` |
| `Window.Closing` | "Fired **before** a window is closed"；`WindowClosingEventArgs` 继承 `System.ComponentModel.CancelEventArgs`，可用 `Cancel` 否决关闭；另有 `IsProgrammatic` / `CloseReason` | `Avalonia.Controls.xml` `E:Avalonia.Controls.Window.Closing`、`T:Avalonia.Controls.WindowClosingEventArgs`；反射确认基类与 `Cancel` 属性 |
| `Window.Closed` | 窗口关闭完成后触发（`EventHandler`，无取消参数） | `Avalonia.Controls.xml` `E:Avalonia.Controls.Window.Closed`；反射确认事件签名 |
| `Visual.IsAttachedToVisualTree` | 12.1.6 中为 **internal**，不作为公开契约使用 | 反射：`Avalonia.Visual` 上属性 `public=False` |

**结论**：不能把 `DetachedFromVisualTree` 当作永久关闭；也不能在 `Window.Closing` 里执行不可逆
销毁——其他处理器可能随后 `Cancel`。可靠的窗口级销毁点是 `Window.Closed`（已不可取消）。
`Window.Closing` 仅可用于诊断/提示，不做资源释放。

### 3. 暂时脱离与重新挂载（A 的具体方案）

**本 ADR 明确选择保留 Binding 与 Runtime**，不创建新对象、不改 `IsClosed`；不存在"Detach 时释放
Runtime"的分支（那等同于 §4 的永久销毁，必须由拥有者显式决定）。

1. **Detach 时**：控件置自身 `_isAttached = false`（仅控制是否入队失效），**不**调用
   `binding.RequestClose()`、不 Dispose Runtime、不关闭 Workspace、不停止已在活动的请求。
   已提交的请求可以继续完成并发布；此时没有控件绘制，发布是无害的。
2. **Attach 时**：控件置 `_isAttached = true`；若 `binding.IsClosed` 为 true，则**拒绝恢复绘制**
   并记录诊断——这是终态，拥有者必须创建新的 Binding 与控件，不能复用旧对象。
3. **正常恢复**：Attach 时不假定 `Bounds` 已有效。若当前尺寸 > 0，则 `binding.OnViewportChanged`
   推进 generation 并重发一次请求（重新挂载视为呈现状态变更，避免显示 Detach 期间的陈旧快照）；
   若尺寸仍为 0，则等待首个有效 `SizeChanged` 再请求，避免以 0 尺寸发起请求。随后
   `InvalidateVisual()`。
4. **下一帧**：`Render` 调用 `binding.CaptureFrame()`，读取 `Publisher.Current` 的**最新**快照与
   当前相机值；无需重放 Detach 期间的中间帧。
5. **不引入**生命周期管理器/事件总线：恢复只依赖控件自身的 `AttachedToVisualTree` 与
   `Render`，以及 binding 的两个方法。

**不变量**：一个控件实例只能绑定一个未关闭的 Binding；同一 Workspace 在 M1 至多有一个未关闭
Binding（工厂负责断言）；Binding 一旦 `IsClosed` 即为终态，不可"重开"。永久销毁必须同时丢弃
控件；新视口由工厂创建新的 Binding + Runtime。**临时 Detach 路径不会产生已关闭 Binding**；
若 Attach 时发现已关闭，说明拥有者在永久销毁后错误复用了控件，必须 fail-fast 并新建，
不得静默留下"无法恢复绘制"的状态。

### 4. 视口永久销毁（B 的具体方案）

- **谁判定**：Binding 的拥有者（视口宿主：窗口/工作区视图层），不是控件、不是可视树事件。
- **触发命令**：
  - 关闭视口/标签/承载窗口的显式命令；
  - 宿主窗口 `Closed`（非 `Closing`，避免被取消）；
  - 工作区关闭（C，先执行 B）。
- **顺序**：`binding.CloseAsync()`（内部**先**置 `IsClosed`，再 Coordinator → Publisher →
  Renderer）→ 控件停止失效并丢弃引用。置标志先于任何释放，与 §7 一致。
- **Workspace 保留**：B 不调用 `ProjectSession.CloseWorkspaceAsync`；工作区继续存活，可由新视口
  重新挂载（新 Binding + 新 Runtime）。
- **不可复用**：已关闭 Binding 不得再次 Attach；拥有者若需重新显示，必须走工厂创建。

### 5. 关闭保证的准确边界（B/C 共用）

允许的时序：

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

准确保证：

1. `Render` 在入口观察到 `IsClosed == true` 时立即返回：不获取 lease、不创建 Skia 资源、不发绘制命令。
2. 在关闭置位前已经观察到 `IsClosed == false` 的在飞帧，允许自然完成，可能继续创建资源并发命令；它只使用已捕获帧输入、局部 Skia 对象与 M1 无状态 Renderer，不读已释放的 Runtime / Publisher。
3. **不保证**关闭命令返回后绝对没有绘制调用——在飞帧可能仍在执行。
4. **不保证**中断已经进入的回调；Avalonia 12.1.6 未公开取消契约。
5. 不等待在飞帧结束。
6. UI 线程不等待渲染完成。
7. 不引入锁、信号量、帧屏障或独立 Render Thread。

### 6. 对象创建 / 持有 / 释放总表

| 对象 | 创建者（唯一） | 持有者 / 所有者 | 销毁入口（唯一） | 依据 |
|---|---|---|---|---|
| `ProjectSession` | `OpenProjectUseCase`（组合根注册） | `ProjectSessionRegistry` + `ProjectWorkspaceViewModel` | `CloseAsync` / `DisposeAsync`（Program 生命周期结束后统一编排） | `Program.cs:37-45`；`ProjectSession.cs:208-264` |
| `MapWorkspaceSession` | `ProjectSession.OpenWorkspaceAsync` | `ProjectSession._workspaces` + `ViewModel.Workspaces` | `ProjectSession.CloseWorkspaceAsync` → `CloseAsync` → `DisposeAsync` | `ProjectSession.cs:168-179,183-202`；`ProjectWorkspaceViewModel.cs:131-145` |
| `WorkspaceViewState` | `MapWorkspaceSession` 构造函数 | `MapWorkspaceSession.ViewState` | 随工作区结束（无独立 Dispose） | `MapWorkspaceSession.cs:25,38,109-125` |
| `MapViewportBinding` | Desktop 组合根的 workspace 工厂（单一工厂方法） | **视口宿主拥有者**（窗口/工作区视图层）；控件只持一个引用 | 拥有者调用 `binding.CloseAsync()`（单一入口）；`Window.Closed` 仅是该拥有者的兜底调用点 | ADR-07 R1/R2；本 ADR §3/§4 |
| `ViewportPresentationRuntime` | `MapViewportBinding` 构造函数（`ViewportPresentationRuntime.Create()`） | `MapViewportBinding` 私有字段 | `binding.CloseAsync()` 内 `DisposeAsync()` | `IMPLEMENTATION_SUMMARY.md` §7；`ViewportPresentationRuntime.cs:30-35` |
| `Renderer`（Desktop 内部绘制对象） | `MapViewportBinding` 构造函数，`internal sealed` | `MapViewportBinding` 私有字段 | `binding.CloseAsync()` 内、Publisher 释放后 `Dispose()` | ADR-07；ADR-09 §8/§9；本 ADR §7 |
| `MapViewportControl` | Desktop 窗口/视图层（Binding 已存在后创建） | 所在 `Window` 的可视树 | 随可视树/窗口销毁；**Detach 不销毁、不关闭** | ADR-07；Avalonia 12.1.6 `Visual.*VisualTree` |
| `Camera`（M1 无独立类型） | 数值由 `WorkspaceViewState` 初始化 | `WorkspaceViewState`（Application 瞬态） | 随工作区；视口只持有不可变复制值 | ADR-07 问题 3；ADR-11 |
| Skia 资源 | draw 回调内（除 Avalonia 借出的 canvas/surface） | 本次回调 | 本次回调 `using` | ADR-09 |

**唯一性说明**：Binding 只由工厂方法创建；Runtime 与 Renderer 只由该 Binding 创建并销毁；
控件从不创建或销毁三者。`Window.Closed` 是**事件**而非销毁者：它只调用拥有者的同一清理方法
（最终仍是 `binding.CloseAsync()`），不构成第二个销毁入口，且必须幂等。

### 7. 关闭顺序（C：Workspace 显式关闭）

```text
0. 触发：显式"关闭工作区/项目"命令，或宿主 Window.Closed（非 Closing）
1. binding.IsClosed = true（volatile）
2. Coordinator.CloseAsync()：停新请求、取消活动请求，在 publisher gate 内置位 F-1 屏障
3. Publisher.Dispose()：拒绝一切发布与版本分配；Current 不清空
4. Renderer.Dispose()（M1 幂等空操作）
5. MapWorkspaceSession.CloseAsync()：取消 _derivedWorkCts
6. ProjectSession.CloseAsync()：关闭其余工作区 → AssetStore → Database（按需继续）
```

**依据说明**：
- 步骤 1 先于任何释放：新进入的 `Render` 会在获取 lease 前返回；但不承诺在飞帧被中断（§5）。
- 步骤 2→3 是 WP-30A/Phase 1 的既有契约：F-1 屏障必须先于 Publisher 释放。
- 步骤 3→4：Renderer 无抢在 Publisher 之前释放的资源（ADR-09）。
- 步骤 2→5：presentation 请求的取消/迟到拒绝由 Coordinator 独立完成，不能依赖 workspace 的
  `_derivedWorkCts`"顺带"保证正确性。
- 步骤 5→6：工作区先于项目级资源释放，与现有 `ProjectSession` 顺序一致。
- 控件析构、`DetachedFromVisualTree`、`Window.Closing` 都**不**触发上述序列。

### 8. 十个必答题

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 谁创建 Workspace | `ProjectSession.OpenWorkspaceAsync`（Application），经 Core Domain 解析上下文后构造；Desktop 不自行 new。 |
| 2 | 谁创建 MapViewportControl | Desktop 窗口/视图层，在 Workspace 与 Binding 均已存在后创建；构造/Attach 时接收 Binding。控件不创建 Binding。 |
| 3 | 谁创建 Renderer | `MapViewportBinding` 构造函数创建 `internal sealed` 绘制对象作为私有字段；不注册 DI、不暴露接口、不归控件持有。 |
| 4 | Workspace 和 Viewport 是什么关系 | 组合关系：Viewport 是 Workspace 的可视宿主；Binding 引用的上下文与 ViewState 来自 Workspace，Workspace 不依赖 Viewport 的存在（会话可无视图存活）。M1 为 1:1。 |
| 5 | 一个 Workspace 是否允许多个 Viewport | **M1 不允许**（相机/工具状态仍在工作区级）；将来拆分每视口状态后可重新裁决。多个工作区/窗口仍可并存且 runtime 彼此隔离。 |
| 6 | Viewport 销毁是否意味着 Workspace 销毁 | **否**。暂时 Detach 什么都不销毁；永久销毁只关闭该视口的 presentation（Binding/Runtime/Renderer），Workspace 仍归 Application 生命周期。 |
| 7 | Workspace 关闭时如何处理未完成 Presentation Request | `Coordinator.CloseAsync()` 停新请求、取消活动请求、在 publisher 锁内置屏障；其后任何完成路径（包括发布器未释放窗口内的迟到完成）一律 `RejectedClosed`。workspace 的 `_derivedWorkCts` 取消是额外一层，不承担正确性。 |
| 8 | Renderer 释放早于或晚于 Publisher | M1 Renderer 无资源，`Dispose()` 幂等空操作；由 Binding 在 Publisher 释放**之后**调用（步骤 3→4）。不允许把 Skia 资源放进 Renderer 改变这一顺序（ADR-09）。 |
| 9 | 如何保证关闭之后没有迟到绘制 | 精确边界见 §5：入口读到 `IsClosed == true` 立即返回；此前已读到 `false` 的在飞帧允许完成；不保证关闭返回后零绘制调用、不保证中断在飞回调、不等待。另由 F-1 屏障拒绝关闭后的发布。 |
| 10 | 如何保证绘制过程中 ViewState 一致 | 只有 UI 线程写 `WorkspaceViewState`（ADR-08）；控件 `Render` 调用 `binding.CaptureFrame()`，由 Binding 同步复制成不可变帧输入（ADR-11）；Renderer 与 draw operation 不回读可变对象或控件；快照由 publisher 原子交换。 |

## Consequences

**正面**

- Detach 与销毁解耦：标签切换/重新挂载不会误关 Binding 或 Workspace，重新挂载可恢复绘制。
- 永久销毁只影响该视口，工作区可继续存活并由新 Binding 重新挂载。
- Binding 创建者唯一、销毁入口唯一；不依赖控件析构或可视树事件的偶然顺序。
- F-1 屏障与 WP-30A 读者契约保持不变。
- 关闭保证措辞与真实时序一致，不再暗示"零 post-close 绘制"。

**代价 / 约束**

- 拥有者必须显式调用 `binding.CloseAsync()`；窗口级销毁只能放在 `Closed`，不能放在可取消的
  `Closing`。这是有意的：`Closing` 被取消时不能留下已销毁的视口。
- 暂时 Detach 期间 Runtime、Publisher 与最后快照继续驻留；这是恢复绘制的代价，M1 可接受。
- 已关闭 Binding 不可复用；重新显示必须新建 Binding + Runtime，旧控件必须丢弃。
- M1 不能在一个工作区开多个地图视口；UI 不应提供该入口。
- 组合根需提供可对同一 Workspace 再次调用的 Binding 工厂方法（旧 Binding 关闭后）。

## Rejected Alternatives

- **Detach 即关闭（R1 原方案）**：同一控件重新挂载后 Binding 已终态，无法恢复绘制；还会把标签切换误判为销毁。已废弃。
- **Detach 时释放 Runtime，Attach 时新建**：同一视口出现两套 generation/publisher 生命周期，切换标签即丢失请求连续性；所有权在控件与拥有者之间摇摆。
- **在 `Window.Closing` 执行销毁**：`Closing` 可被其他处理器 `Cancel`，会留下已销毁但窗口仍开的状态。销毁放 `Closed`。
- **只靠 `Window.Closed`，不提供显式关闭视口命令**：无法支持"关视口但保留工作区"（B）；B 必须可由拥有者显式触发。
- **让 `MapViewportControl.Dispose()` 关闭 Workspace 或 Dispose Binding**：视口销毁会误关工作区；且把资源释放交给可视树析构的偶然顺序。
- **通用生命周期管理器 / 事件总线**：把两个事件与一个显式命令能解决的事抽象成框架，违反最小机制。
- **等待所有在飞帧完成后再关闭**：UI 线程等待渲染，且 Avalonia 未提供该同步契约。
- **把 runtime 做成 DI Singleton / 全局注册表**：违反每视口私有与"无全局状态"。
- **先释放 Publisher 再关 Coordinator**：颠倒 F-1 契约。
- **多视口共享 runtime 或相机**：generation/序号/快照互相覆盖，ViewState 是工作区级可变状态。

## Implementation Constraints

1. 不修改 `ProjectSession`、`MapWorkspaceSession`、`ProjectWorkspaceViewModel`、`ViewportPresentationRuntime`、`PresentationRequestCoordinator`、`SceneSnapshotPublisher` 的既有公共 API 与语义。
2. 不删除/放宽既有测试（含 `CoordinatorClosePublicationRaceTests`）。
3. `DetachedFromVisualTree` **不得**调用 `RequestClose()` / `CloseAsync()` / `DisposeAsync()`；只置控件自身 `_isAttached`。
4. `AttachedToVisualTree` 必须检查 `binding.IsClosed`：为 true 时拒绝绘制并报诊断，不得静默重开。
5. `binding.CloseAsync()` 只由拥有者在 B/C 命令中调用；必须幂等；`Window.Closed` 可作兜底但不得成为第二套逻辑。
6. 不在 `Window.Closing` 释放任何资源；`Closing` 仅可用于诊断/状态提示。
7. Binding 构造时创建 Runtime 与 Renderer；工厂只创建 Binding。禁止其他代码创建/替换 Runtime。
8. `MapDrawOperation.Render` 入口先检查 `IsClosed`，再获取 lease；保证边界按 §5，不得写成"零 post-close 绘制"。
9. 不触碰 `docs/map-editor/implementation/`；本阶段只写文档。

## Open Questions

1. 当前 Desktop 外壳是否真的会在标签切换时触发 Detach/Attach，需在实现期用真实窗口验证；若某些布局不触发，A 仍按"保留"处理，不影响契约。
2. 窗口 `Closed` 与 ViewModel 工作区清理命令的先后顺序需在接线时确认；两者都调用同一个 `CloseAsync()`，但需保证只执行一次。
3. 暂时 Detach 期间是否需要在 UI 上显示"已暂停"状态，属产品决定；不影响 Binding 保留策略。
4. 多视口需求若出现，必须先拆分每视口相机状态并另立 ADR；本条不预先设计。
5. 在飞帧的诊断计数（close 后完成帧数）是否具名记录未定；仅观测，不改变 §5 边界。
