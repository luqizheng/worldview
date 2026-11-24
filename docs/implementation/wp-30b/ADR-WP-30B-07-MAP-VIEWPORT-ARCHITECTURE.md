# ADR WP-30B-07 — MapViewportControl 渲染架构

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
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

`MapViewportControl : Control` 承载布局、输入、焦点、生命周期与失效；`Render(DrawingContext)`
提交一个短生命周期 `ICustomDrawOperation`；在该操作的 `Render(ImmediateDrawingContext)` 内
通过 Skia lease 取得 `SKCanvas` 并完成绘制。真正的绘制逻辑放在 Desktop 内部一个无状态绘制
对象（下称 Renderer），不暴露为公共接口。

### 方案 B — Avalonia Control + 独立 Skia 渲染层（有独立 Render API 与生命周期）

控件只做输入/布局，通过显式接口调用独立 Renderer（例如 `IRenderer.Render(SKSurface, frame)`）。
需要额外定义接口、帧对象、生命周期边界与 surface 所有权；控件与 Renderer 之间多一层跨对象协调。

### 方案 C — 独立渲染宿主 / 专用 Native Surface

用平台原生窗口句柄（Win32 HWND / macOS NSView）或专用 Skia 宿主承载地图画布，Avalonia 只做外壳。

## Decision

**选择方案 A（ICustomDrawOperation 形式），不选 B、C。**

具体形状：

```
MapWorkspaceSession.ViewState（Application，可变、UI 线程独占）
        │ UI 线程同步读取相机等标量
        ▼
MapViewportControl : Control（Desktop）
        │ 捕获 SceneSnapshot + 尺寸/DPI/相机/generation 为一个不可变帧输入（ADR-11）
        │ Render(DrawingContext) → context.Custom(MapDrawOperation(frame))
        ▼
MapDrawOperation : ICustomDrawOperation（每次渲染短生命周期）
        │ Render(ImmediateDrawingContext)：TryGetFeature<ISkiaSharpApiLeaseFeature>
        │ → using var lease = feature.Lease() → lease.SkCanvas（借用）
        ▼
MapSceneRenderer（Desktop 内部，无公共接口、无长生命周期状态）
        │ 仅用传入的 SKCanvas + 不可变帧输入发绘制命令
        ▼
Avalonia 渲染管线 → 屏幕
```

### 十个必答题的裁决

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 控件的唯一职责 | `MapViewportControl` 是**视口宿主与输入边界**：参与布局、路由输入/焦点、观察尺寸与 DPI 变化、推进 generation、发起场景请求、调用 `InvalidateVisual()`、在视觉树分离时停止一切后续绘制。它不查询数据库、不装配场景、不直接创建 `SKCanvas`。 |
| 2 | Renderer 的职责 | 只把"一个已捕获快照 + 一个不可变帧输入"翻译成针对**传入 canvas** 的绘制命令；解释 `builtin-neutral-background/v1` 到具体颜色；不读 Publisher、不读 ViewState、不做 IO/Domain 查询、不保留帧状态。 |
| 3 | Camera 属于哪一层 | 视口局部呈现状态，**不是 Domain / Presentation 概念**。M1 不新增 Camera 类型：数值仍存于 `MapWorkspaceSession.ViewState`（Application 的瞬态编辑态），由 Desktop 视口适配器在 UI 线程读取并复制进不可变帧输入；Renderer 只见复制值。Pan/Zoom 属 WP-30C 范围，届时也不写 Map 数据、不改 Domain revision。 |
| 4 | SceneSnapshot 如何传入 | 控件在 `Render(DrawingContext)` 时执行一次 `Publisher.Current`（可能为 null = 加载/空态），把它与帧输入一起存进 draw operation；operation 的 `Render` 只使用这份捕获引用。发布继续走 WP-30A 的原子交换，读者契约不变。 |
| 5 | Renderer 是否持有快照 | **否**。Renderer 无 `SceneSnapshot` 字段；引用只存在于短生命周期 draw operation / 帧输入中，且仅在该帧内有效。 |
| 6 | Renderer 是否持有 Skia 资源 | **M1 否**。不缓存 `SKPaint` / `SKPath` / `SKBitmap` / `SKSurface`；所有原生对象在回调内创建、回调内 `using` 释放。`SKCanvas` 是 lease 借出的对象，绝不 Dispose；绝不跨线程传递（详见 ADR-09）。 |
| 7 | 是否需要单独的 MapViewportControl 公开 API | 只需组合根接线所需的最小面：控件类型本身 + 一个 `Attach(MapWorkspaceSession, ViewportPresentationRuntime)`（或在工厂内构造）。**不公开** Render/Draw/Snapshot/Camera 设置入口，不暴露 Skia 类型。 |
| 8 | 是否需要独立 Renderer 接口 | **不需要**。仅一个 `internal sealed` 绘制对象；没有第二个后端、没有替换需求。若未来出现第二种后端，再由新 ADR 裁决接口，不为"可能"提前抽象。 |
| 9 | Resize / DPI / 窗口重建 | 全部由 Avalonia 驱动：`Control.SizeChanged`、`TopLevel.ScalingChanged` / `RenderScaling`、`Visual.AttachedToVisualTree` / `DetachedFromVisualTree` 已在 12.1.6 核实存在。UI 线程收到变化 → 捕获新尺寸/缩放 → `ViewportGeneration.Advance()` → 重新发起请求 → `InvalidateVisual()`。draw operation 的 `Bounds` 用当前控件边界；**没有我方持有的 render target 需要重建**，lease 每次回调重新获取。窗口关闭不产生平台分支。 |
| 10 | 如何避免类型泄漏 | 工程引用层阻止：Avalonia/Skia 引用仅在 Desktop；`WorldView.Presentation.csproj` 仍只引用 Application；沿用 `PresentationBoundaryTests`（程序集引用 + 公共 API 反射）与新增的 Desktop-only 类型检查。Domain/Application/Presentation 均不出现 `SkiaSharp` / `Avalonia` using。 |

### 与 ICustomDrawOperation API 的实际适配

- 必须实现 `Bounds`、`HitTest`、`Render`、`Equals`、`Dispose`；`Bounds` 必须是准确的全局坐标可见范围，否则会被裁剪。
- `HitTest` 在 M1 仅做控件边界命中（把事件交给控件路由）；地图要素命中属后续 Spatial Presentation 职责，不用 Avalonia 场景图命中代替。
- `Equals` 需让 Avalonia 能判断操作是否变化；具体的相等定义在实现阶段确定（至少包含 generation / snapshot 版本 / 尺寸 / DPI），不得用常量 `false` 掩盖失效问题。
- `TryGetFeature` 返回 null（例如无 Skia 后端/无头测试）时安静跳过绘制并记诊断；不得抛异常替代加载态。
- `ISkiaSharpApiLease` 及其提供的 `SKCanvas` / `SKSurface` 只在 `Render` 调用栈内存活；释放由 `using` 完成，不交给 GC。

## Consequences

**正面**

- 与 WP-30A 读者契约完全一致：一帧一个快照引用，无新锁、无新通道。
- Windows / macOS 自动使用同一套 Avalonia Skia 后端；无原生互操作代码、无 RID/发布分支。
- 无独立 Render Thread、无跨线程 Skia 共享，资源生命周期退化为"每帧获取 + 每帧释放"。
- 控件仍是普通 Avalonia Control，可参与标准布局、样式、焦点与窗口生命周期。

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

## Open Questions

1. **`Equals` 的精确定义**：实现期需决定用哪些字段（快照版本、generation、尺寸、DPI、相机值）判定 draw operation 变化；错误定义会导致不重绘或每帧重绘。
2. **无 Skia feature 时的诊断落点**：是控件状态文本还是进程内诊断计数；需与"加载态在渲染器外"一起裁决。
3. **macOS 实际运行验证**：Avalonia.Native/Skia 路径在 CI 仅构建、未做运行时绘制验证；需在实现阶段补真实窗口证据。
4. **DPI 变化与 generation 的精确时序**：`TopLevel.ScalingChanged` 触发时是否总在布局之前、是否需要二次失效；实现期用真实窗口测量后确认，本文不假定。
5. **背景 token 的颜色表**：`builtin-neutral-background/v1` 的具体 RGB 值在 Desktop 渲染后端定义，本阶段不落数值。
