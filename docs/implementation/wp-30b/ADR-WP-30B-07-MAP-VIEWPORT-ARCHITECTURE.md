# ADR WP-30B-07 — MapViewportControl 渲染架构

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— 抽出 Desktop 内部 `MapViewportBinding` 收窄控件职责；draw operation 入口增加关闭检查。
- **修订**: R2（2026-11-24）— Detach 不再等于关闭；重新挂载恢复绘制；关闭保证改为"入口命中即返回，在飞帧可继续"，不再声称零 post-close 绘制。
- **修订**: R3（2026-11-24）— 同步 ADR-10 的 C1/C2 关闭路径拆分：控件不区分两种关闭，只由拥有者决定；`Window.Closed` 归 C2。
- **修订**: R4（2026-11-24）— C2 收敛为单一编排入口 `CloseProjectLifetimeAsync()`（正常命令与 `Window.Closed` 汇聚）；控件的职责与关闭检查不受影响。
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`（PR #29 合并提交）
- **范围**: WP-30B Phase 2 架构裁决；不含任何渲染实现
- **相关**: `ADR-WP-30B-08`、`ADR-WP-30B-09`、`ADR-WP-30B-10`、`ADR-WP-30B-11`

## Context

WP-30A 已交付渲染宿主无关的 Presentation：不可变 `SceneSnapshot`、单写者原子发布、每视口
`ViewportPresentationRuntime`（publisher + coordinator + generation tracker）、F-1 关闭屏障。
但仓库中尚无地图视口控件：Desktop 仅有 `StartWindow` / `ProjectWindow` / `CreateMapDialog`
三个文本基线窗口，`ProjectWindow.axaml:15` 明确注明"地图渲染属 PRA-01/PRA-02 之后的阶段"。

必须在现有分层内补上"从 `SceneSnapshot` 到屏幕像素"的宿主，同时不把 Avalonia / SkiaSharp
类型带入 Domain、Application 或 Presentation。

### 已核实的技术事实（非假设）

| 事实 | 证据 |
|---|---|
| Avalonia **12.1.6**（Avalonia / Desktop / Themes.Fluent / Fonts.Inter）为直接依赖；`Avalonia.Skia` **12.1.6** 与 `SkiaSharp` **3.119.4** 为传递依赖；Win32 / Native(macOS) 后端随包就位；无 RID、无平台条件编译 | `WorldView.Desktop.csproj`；`docs/implementation/wp-30-preflight/FRAMEWORK_AND_PACKAGE_MATRIX.md` §2–§5；`obj/project.assets.json` |
| `Control` 通过覆写 `Visual.Render(DrawingContext)` 自绘；`DrawingContext.Custom(ICustomDrawOperation)` 是受支持的自定义绘制扩展点 | Avalonia 官方 [Custom-drawn controls](https://docs.avaloniaui.net/docs/custom-controls/custom-control-class)、[Native platform interop](https://docs.avaloniaui.net/docs/app-development/native-interop)；本地 `Avalonia.Base.xml` `M:Avalonia.Media.DrawingContext.Custom` |
| Avalonia 12.1.6 的 `ICustomDrawOperation` = `Rect Bounds { get; set; }` + `bool HitTest(Point)` + `void Render(ImmediateDrawingContext)` + `IDisposable` + `IEquatable<ICustomDrawOperation>`；该程序集公开类型中**没有** `FuncCustomDrawOperation` 帮助类 | 本地包反射：`avalonia/12.1.6/lib/net10.0/Avalonia.Base.dll`；官方文档 [ICustomDrawOperation](https://docs.avaloniaui.net/api/avalonia/rendering/scenegraph/icustomdrawoperation) |
| `ImmediateDrawingContext.TryGetFeature(Type)` → `Avalonia.Skia.ISkiaSharpApiLeaseFeature.Lease()` → `ISkiaSharpApiLease.SkCanvas` / `SkSurface` / `GrContext`；lease 实现 `IDisposable` 且官方文档示例用 `using` 在回调内释放 | 本地包反射：`Avalonia.Skia.dll` 类型 `ISkiaSharpApiLeaseFeature`、`ISkiaSharpApiLease`、`DrawingContextImpl+SkiaLeaseFeature+ApiLease`；[官方 interop 示例](https://docs.avaloniaui.net/docs/app-development/native-interop) |
| `Control.SizeChanged`、`TopLevel.ScalingChanged`、`TopLevel.RenderScaling`、`Visual.AttachedToVisualTree` / `DetachedFromVisualTree`、`Visual.InvalidateVisual()` 均存在于 12.1.6 | 本地包反射：`Avalonia.Controls.dll` / `Avalonia.Base.dll`；XML 文档 `P:Avalonia.Controls.TopLevel.RenderScaling` |
| `SceneSnapshot` 明确不含相机、bounds、Avalonia/Skia 类型；相机为视口局部二维变换，不写 Domain | `docs/implementation/wp-30a/ARCHITECTURE.md` §3/§4；`docs/map-engine/PRA-01_M1_IMPLEMENTATION_PROFILE_v1.md` §2.2/§3.5/§5.2；`docs/implementation/wp-30a/SNAPSHOT_LIFECYCLE.md` §1 |

## Decision Drivers

1. **单一权威**：WP-30A 的 `SceneSnapshotPublisher.Current` 是唯一场景来源；不得出现第二条绘制数据通道。
2. **分层不泄漏**：Avalonia / SkiaSharp 只出现在 Desktop 工程；Presentation 保持渲染宿主中立（既有 `PresentationBoundaryTests` 锁定）。
3. **最小机制**：单机桌面、无历史数据、无多用户；不引入通用调度器、事件总线、独立渲染线程或通用 Renderer 接口。
4. **跨平台同构**：Windows 与 macOS 走同一条 Avalonia Skia 路径，不写平台分支、不自建 Native Surface。
5. **按框架生命周期**：控件生命周期、布局、输入、失效与绘制全部交给 Avalonia；自绘点只用它公开的扩展点。
6. **可验证**：所有外部 API 行为必须有当前依赖版本证据；不确定处标为 Open Question，不虚构。

## Considered Options

### 方案 A — Avalonia Custom Control + Skia 自绘（选定，收窄为 ICustomDrawOperation）

`MapViewportControl : Control` 只承载 Avalonia 本职：布局、输入/焦点路由、尺寸与 DPI 事件、
失效与生命周期。`Render(DrawingContext)` 委托一个 Desktop 内部具体类 `MapViewportBinding`
捕获不可变帧输入并提交一个短生命周期 `ICustomDrawOperation`；在该操作的
`Render(ImmediateDrawingContext)` 内先检查关闭标志，再通过 Skia lease 取得 `SKCanvas` 并完成
绘制。Runtime、generation、Renderer 与捕获逻辑归 `MapViewportBinding`，绘制逻辑归一个无状态
绘制对象（下称 Renderer），两者都不暴露为公共接口。

### 方案 B — Avalonia Control + 独立 Skia 渲染层（有独立 Render API 与生命周期）

控件只做输入/布局，通过显式接口调用独立 Renderer（例如 `IRenderer.Render(SKSurface, frame)`）。
需要额外定义接口、帧对象、生命周期边界与 surface 所有权；控件与 Renderer 之间多一层跨对象协调。

### 方案 C — 独立渲染宿主 / 专用 Native Surface

用平台原生窗口句柄（Win32 HWND / macOS NSView）或专用 Skia 宿主承载地图画布，Avalonia 只做外壳。

## Decision

**选择方案 A（ICustomDrawOperation 形式），不选 B、C。**

具体形状：

```
Desktop 组合根：创建 MapViewportBinding（per workspace，内部具体类，非 Control / 非接口）
        │ 持有：ViewportPresentationRuntime（publisher + coordinator + generation tracker）
        │       + MapSceneRenderer（无公共接口、无 Skia 字段）+ volatile bool IsClosed
        │ 提供：CaptureFrame() / OnViewportChanged() / RequestClose()
        ▼
MapViewportControl : Control（Desktop）
        │ 只做 Avalonia 本职：布局、输入/焦点、尺寸/DPI 事件、失效、生命周期转发
        │ Render(DrawingContext)：frame = binding.CaptureFrame()（ADR-11，UI 线程同步）
        │ → context.Custom(new MapDrawOperation(binding, frame))
        ▼
MapDrawOperation : ICustomDrawOperation（每次渲染短生命周期）
        │ Render(ImmediateDrawingContext)：先 if (binding.IsClosed) return;
        │ → TryGetFeature<ISkiaSharpApiLeaseFeature> → using var lease = feature.Lease()
        │ → lease.SkCanvas（借用）
        ▼
MapSceneRenderer（binding 私有字段；仅用传入 canvas + 不可变帧输入发绘制命令）
        ▼
Avalonia 渲染管线 → 屏幕
```

### 十个必答题的裁决

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 控件的唯一职责 | `MapViewportControl` 是 **Avalonia 视口宿主**：参与布局、路由输入/焦点、把尺寸与 DPI 事件转发给 binding、调用 `InvalidateVisual()`、在 Attach/Detach 时切换自身挂载状态。Detach **只**停止失效与绘制机会，**不**关闭 Binding、**不**转发 `RequestClose()`；永久销毁（B）由拥有者的显式命令决定，工作区/项目关闭（C1/C2）由拥有者在自身流程前先关闭对应 Binding（ADR-10 §1）。**不**持有 Runtime / Renderer、**不**读取 Publisher、**不**推进 generation、**不**组装帧输入、**不**创建 `SKCanvas`。 |
| 2 | Renderer 的职责 | 只把"一个已捕获快照 + 一个不可变帧输入"翻译成针对**传入 canvas** 的绘制命令；解释 `builtin-neutral-background/v1` 到具体颜色；不读 Publisher、不读 ViewState、不做 IO/Domain 查询、不保留帧状态。Renderer 由 `MapViewportBinding` 持有（不归控件）。 |
| 3 | Camera 属于哪一层 | 视口局部呈现状态，**不是 Domain / Presentation 概念**。M1 不新增 Camera 类型：数值仍存于 `MapWorkspaceSession.ViewState`（Application 的瞬态编辑态），由 `MapViewportBinding.CaptureFrame()` 在 UI 线程读取并复制进不可变帧输入；Renderer 只见复制值。Pan/Zoom 属 WP-30C 范围，届时也不写 Map 数据、不改 Domain revision。 |
| 4 | SceneSnapshot 如何传入 | `MapViewportBinding.CaptureFrame()` 在 `Render(DrawingContext)` 调用栈中同步执行一次 `Publisher.Current`（可能为 null = 加载/空态），与视口标量一起构造不可变帧输入并放进 draw operation；operation 的 `Render` 只使用这份捕获引用。发布继续走 WP-30A 的原子交换，读者契约不变。 |
| 5 | Renderer 是否持有快照 | **否**。Renderer 无 `SceneSnapshot` 字段；引用只存在于短生命周期 draw operation / 帧输入中，且仅在该帧内有效。 |
| 6 | Renderer 是否持有 Skia 资源 | **M1 否**。不缓存 `SKPaint` / `SKPath` / `SKBitmap` / `SKSurface`；所有原生对象在回调内创建、回调内 `using` 释放。`SKCanvas` 是 lease 借出的对象，绝不 Dispose；绝不跨线程传递（详见 ADR-09）。 |
| 7 | 是否需要单独的 MapViewportControl 公开 API | 只需组合根接线所需的最小面：控件类型本身 + 一个 `Attach(MapViewportBinding)`（或在工厂内构造）。`Attach` 必须拒绝已 `IsClosed` 的 binding（终态，需新建 Binding + Runtime），因此同一控件可在暂时 Detach 后重新 Attach 未关闭的 binding。`MapViewportBinding` 不升格为公共 API；**不公开** Render/Draw/Snapshot/Camera 设置入口，不暴露 Skia 类型。 |
| 8 | 是否需要独立 Renderer 接口 | **不需要**。仅一个 `internal sealed` 绘制对象，由 binding 私有持有；没有第二个后端、没有替换需求。`MapViewportBinding` 同样**不是接口**——它只是一个具体协作者，用来防止控件膨胀，不引入可替换后端或 DI 抽象。若未来出现第二种后端，再由新 ADR 裁决接口。 |
| 9 | Resize / DPI / 窗口重建 | 全部由 Avalonia 驱动：`Control.SizeChanged`、`TopLevel.ScalingChanged` / `RenderScaling`、`Visual.AttachedToVisualTree` / `DetachedFromVisualTree` 已在 12.1.6 核实存在。控件观察到变化 → 转发 `binding.OnViewportChanged(size, scaling)` → binding 推进 `ViewportGeneration.Advance()` 并发起请求 → 控件 `InvalidateVisual()`。暂时 Detach 后重新 Attach：binding 未关闭则复用；尺寸 > 0 时推进 generation 并重发一次请求（避免 Detach 期间的陈旧快照），尺寸为 0 时等待首个有效 `SizeChanged`；随后 `InvalidateVisual()`，下一帧读取最新快照。draw operation 的 `Bounds` 用当前控件边界；**没有我方持有的 render target 需要重建**，lease 每次回调重新获取。窗口关闭不产生平台分支。 |
| 10 | 如何避免类型泄漏 | 工程引用层阻止：Avalonia/Skia 引用仅在 Desktop；`WorldView.Presentation.csproj` 仍只引用 Application；沿用 `PresentationBoundaryTests`（程序集引用 + 公共 API 反射）与新增的 Desktop-only 类型检查。Domain/Application/Presentation 均不出现 `SkiaSharp` / `Avalonia` using。 |

### 控件 / Binding / Renderer 边界（R1）

| 对象 | 允许做 | 禁止做 |
|---|---|---|
| `MapViewportControl` | Avalonia 布局、输入/焦点、尺寸/DPI 观察与转发、`InvalidateVisual()`、生命周期转发 | 持有 Runtime / Renderer、读 Publisher、推进 generation、组装帧输入、触碰 Skia |
| `MapViewportBinding` | 持有 Runtime 与 Renderer、`CaptureFrame()`、`OnViewportChanged()`、`RequestClose()`、置 `volatile bool IsClosed`、编排显式关闭 | 创建 Avalonia 控件、执行 Skia 绘制、持有 `SKCanvas` / lease、把自身做成接口/DI 服务/全局单例 |
| `MapSceneRenderer` | 用传入 canvas + 不可变帧输入发绘制命令 | 读 Publisher / ViewState / 控件、持有字段状态或 Skia 资源 |

拆出 binding 的目的不是抽象，而是**让非 UI 职责可以脱离 Avalonia 窗口测试，并让关闭顺序只有
一个执行者**（见 ADR-10）。控件只保留一个 `MapViewportBinding` 引用，引用方向是单向的
（控件 → binding），binding 不持有控件——因此窗口销毁后仍可从 ViewModel 侧安全关闭 binding。

### 与 ICustomDrawOperation API 的实际适配

- 必须实现 `Bounds`、`HitTest`、`Render`、`Equals`、`Dispose`；`Bounds` 必须是准确的全局坐标可见范围，否则会被裁剪。
- `HitTest` 在 M1 仅做控件边界命中（把事件交给控件路由）；地图要素命中属后续 Spatial Presentation 职责，不用 Avalonia 场景图命中代替。
- `Equals` 需让 Avalonia 能判断操作是否变化；具体的相等定义在实现阶段确定（至少包含 generation / snapshot 版本 / 尺寸 / DPI），不得用常量 `false` 掩盖失效问题。
- `TryGetFeature` 返回 null（例如无 Skia 后端/无头测试）时安静跳过绘制并记诊断；不得抛异常替代加载态。
- `ISkiaSharpApiLease` 及其提供的 `SKCanvas` / `SKSurface` 只在 `Render` 调用栈内存活；释放由 `using` 完成，不交给 GC。
- **关闭检查必须先是 `Render` 的第一件事**：`if (binding.IsClosed) return;`。`IsClosed` 为 C# `volatile bool`（语言特性，无锁、无新依赖），由 binding 在 B / C1 / C2 的关闭命令中置位（ADR-10 §7）。**读到 true 时**不获取 lease、不创建 `SKPaint`/`SKPath`、不发绘制命令；**已在置位前读到 false 的在飞帧允许继续**，可能在 `Close` 返回后仍创建资源并发命令。精确时序与 7 条保证见 ADR-08 问题 9 / ADR-10 §5。
- **暂时 Detach 不得改写 `IsClosed`**：Detach 只停止控件失效；重新 Attach 时若 binding 仍未关闭，可恢复绘制。`IsClosed` 是终态，不可重置。
- draw operation 在 `Equals` 中应包含 binding identity（或 generation），避免关闭后的旧 operation 与新 binding 混淆；精确字段仍为 Open Question。

## Consequences

**正面**

- 与 WP-30A 读者契约完全一致：一帧一个快照引用，无新锁、无新通道。
- Windows / macOS 自动使用同一套 Avalonia Skia 后端；无原生互操作代码、无 RID/发布分支。
- 无独立 Render Thread、无跨线程 Skia 共享，资源生命周期退化为"每帧获取 + 每帧释放"。
- 控件仍是普通 Avalonia Control，可参与标准布局、样式、焦点与窗口生命周期。
- R1 后控件只持有 binding 一个引用，Runtime / generation / Renderer 的职责可脱离窗口做单元测试；关闭顺序有单一执行者。

**代价 / 约束**

- 绘制时机由 Avalonia 控制，控件不能同步"画完再返回"；加载态需在渲染器之外表达。
- 每次渲染都要重新获取 lease；未来若引入重型缓存，必须另立 ADR 定义所有权与线程约束。
- `ICustomDrawOperation` 的相等语义与失效正确性需要实现期测试锁定（见 Open Questions）。
- Avalonia 官方文档声明控件创建/布局/渲染/输入在 UI 线程，但组合器内部 OS 线程不是我方契约；绘制代码不得依赖任一具体线程（ADR-08）。

## Rejected Alternatives

### 方案 B（独立 Skia 渲染层 + 明确 Render API）

**不选择**：在 M1 只有一个空白场景绘制、没有第二种后端的前提下，它引入 `IRenderer`、帧对象、
surface/canvas 所有权与两套生命周期；控件与渲染层之间新增同步点，反而更容易出现迟到绘制与
资源双重释放。Avalonia 已提供 `ICustomDrawOperation` 这个受支持扩展点，额外接口没有消费者。
"Render API"继续以 Desktop 内部方法调用的形式存在，但不升格为跨层接口。

### 方案 C（独立 Native Surface / 专用 Skia 宿主）

**不选择**：需分别处理 Win32 HWND 与 macOS NSView 的创建、缩放、窗口重建和发布打包；当前
工程无 RID、无平台条件编译（pre-flight §4），自建宿主会把平台分支引入 Desktop 并把 DPI/
焦点/输入路由从 Avalonia 手中抢走。收益（理论上的渲染线程隔离）在 M1 无实测依据，违反
"不为理论性能提前引入线程系统"。

### 方案 A 的两种误读（同样排除）

- 在控件 `Render` 中直接持有长生命周期 `SKCanvas`：违反 lease 生命周期，canvas 由 Avalonia 拥有。
- 每个地图对象建一个 Avalonia Control：M1 Profile §2.2 明确禁止。

## Implementation Constraints

1. 所有新增 Avalonia/Skia 类型只在 `WorldView.Desktop`；不得新增 NuGet 包（SkiaSharp 3.119.4 已传递就位，若按 Profile §1.2 显式声明，版本必须保持 3.119.4）。
2. 不改 `SceneSnapshot`、Publisher、Coordinator、WP-30A 公共 API 与既有测试。
3. draw 回调内禁止 DB/IO/Domain/ViewModel 访问、禁止阻塞等待、禁止修改可变 ViewState。
4. 不实现 Pan/Zoom、不实现 Skia 绘制业务内容（本阶段只裁决）。
5. 不触碰 `docs/map-editor/implementation/`。
6. 渲染器的绘制入口应是纯函数式：相同帧输入 + 相同 canvas 状态 → 相同绘制命令序列。
7. `MapViewportBinding` 必须是 Desktop 内部具体类：不实现接口、不注册 DI、不做全局单例；控件只持有它一个引用。
8. `MapDrawOperation.Render` 的第一条语句必须是关闭标志检查；该检查先于任何 lease / Skia 资源获取。
9. 控件不得在 `DetachedFromVisualTree` / `Window.Closing` 中释放 Runtime、关闭 Workspace 或调用 `RequestClose()`；Detach 只置自身挂载状态，永久销毁由拥有者的显式命令或 `Window.Closed` 触发（ADR-10）。
10. 重新 Attach 已关闭 binding 必须失败并记诊断；不得通过重置 `IsClosed` 实现"重新打开"。

## Open Questions

1. **`Equals` 的精确定义**：实现期需决定用哪些字段（快照版本、generation、尺寸、DPI、相机值）判定 draw operation 变化；错误定义会导致不重绘或每帧重绘。
2. **无 Skia feature 时的诊断落点**：是控件状态文本还是进程内诊断计数；需与"加载态在渲染器外"一起裁决。
3. **macOS 实际运行验证**：Avalonia.Native/Skia 路径在 CI 仅构建、未做运行时绘制验证；需在实现阶段补真实窗口证据。
4. **DPI 变化与 generation 的精确时序**：`TopLevel.ScalingChanged` 触发时是否总在布局之前、是否需要二次失效；实现期用真实窗口测量后确认，本文不假定。
5. **背景 token 的颜色表**：`builtin-neutral-background/v1` 的具体 RGB 值在 Desktop 渲染后端定义，本阶段不落数值。
6. **`MapViewportBinding` 的测试接缝**：保持具体类即可；若测试需要，可用内部构造函数注入 Publisher，但不升格为接口或工厂。
7. **关闭标志与 draw operation 的可见性**：`volatile bool` 足以保证"新进入 `Render` 的调用读到 true 后立即返回"；是否需要更强的帧编号核对由实现期诊断决定，不改变本裁决。
