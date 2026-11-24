# ADR WP-30B-09 — Skia 资源生命周期

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`
- **相关**: `ADR-WP-30B-07`（租约入口）、`ADR-WP-30B-08`（线程边界）、`ADR-WP-30B-10`（关闭顺序）

## Context

WP-30A 明确规定：后端资源不进入 `SceneSnapshot`，其所有权/释放/重建归 WP-30B 渲染管线
（`docs/implementation/wp-30a/SNAPSHOT_LIFECYCLE.md` §1；`PRA-01` §4.2/§8）。
`docs/implementation/wp-30a-review/WP-30B_READINESS.md` §5.2 把"Skia 资源租约/释放/重建"
列为 WP-30B 必答题。

当前仓库没有任何 Skia 绘制代码；SkiaSharp 3.119.4 是 Avalonia.Skia 12.1.6 的传递依赖，
Win32 / macOS / Linux 原生资产随包就位（`FRAMEWORK_AND_PACKAGE_MATRIX.md` §3）。
本 ADR 在**不声明任何未经核实的 Skia 线程/GC 行为**的前提下定义所有权。

### 已核实的 API 事实

| 资源 / API | 当前版本事实 | 证据 |
|---|---|---|
| `ISkiaSharpApiLeaseFeature.Lease()` → `ISkiaSharpApiLease` | 实现 `IDisposable`；提供 `SkCanvas`、`GrContext`、`SkSurface`、`CurrentOpacity`、`TryLeasePlatformGraphicsApi()` | 本地反射：`avalonia.skia/12.1.6/lib/net10.0/Avalonia.Skia.dll`；官方 interop 示例使用 `using var lease = feature.Lease()` |
| `SKCanvas` | 从 lease 取得；是 Avalonia 当前 render target 的借用视图，不由我方创建/销毁 | 同上；`docs.avaloniaui.net/docs/app-development/native-interop` |
| `SKPaint` / `SKPath` / `SKBitmap` / `SKObject` | 均实现 `IDisposable`；`SKNativeObject` 公开 `Handle` 与 `Dispose()`；`SKSurface.Create(...)` 有多个重载（含 `SKImageInfo`） | 本地反射：`skiasharp/3.119.4/lib/net10.0/SkiaSharp.dll` |
| `SKColor` / `SKPoint` / `SKRect` / `SKImageInfo` | 值类型/结构体，不是需要 Dispose 的原生对象 | SkiaSharp 3.119.4 程序集类型元数据（反射） |
| 快照类型 | `SceneSnapshot` / `SceneLayer` / `SceneItem` 不含任何 Skia / Avalonia 类型；由反射测试锁定 | `docs/implementation/wp-30a/ARCHITECTURE.md` §3；`docs/implementation/wp-30a/SNAPSHOT_LIFECYCLE.md` §1 |

**不假定**：SkiaSharp 对象的跨线程安全性、`Dispose` 后 `Handle` 的具体取值、GC 终结器何时运行。
本决策通过"回调内创建、回调内释放、不跨线程、不长期持有"绕开这些不确定行为。

## Decision Drivers

1. 原生资源必须有单一、显式、可审计的所有者；不允许双重拥有或静态全局持有。
2. 不把 Skia 资源放进逻辑快照，不把 `SKCanvas` 暴露给 Application / Domain。
3. 不依赖 GC 作为主要释放策略；不提前引入资源池或缓存框架。
4. 与 ADR-08 的线程边界一致：Skia 原生对象不跨执行上下文。
5. DPI/Resize/窗口重建由 Avalonia 驱动，我方不持有 render target。
6. 关闭/重开可重复，无 native leak、无释放后使用。

## Decision

**M1 采用"零长期 Skia 资源"策略**：除 Avalonia 借出的 canvas/surface 外，所有 Skia 原生对象
（当前只有 `SKPaint`、可能还有 `SKPath`）在 `ICustomDrawOperation.Render` 调用栈内创建、
使用、`using` 释放；Renderer 没有 Skia 字段；不缓存、不池化。

### 资源所有权表

| 对象 | 谁创建 | 谁拥有 | 谁 Dispose | 可缓存？ | 备注 |
|---|---|---|---|---|---|
| `ImmediateDrawingContext` / `ISkiaSharpApiLeaseFeature` | Avalonia 渲染管线 | Avalonia | Avalonia（回调外）；我方只 `TryGetFeature` | 否 | 不持有引用越过回调 |
| `ISkiaSharpApiLease` | `feature.Lease()`（回调内） | 本次回调 | 本次回调 `using` | 否 | 释放 lease 不等于释放 canvas |
| `SKCanvas` | Avalonia / lease 提供 | Avalonia | **我方永不 Dispose** | 否 | 借用对象；只用于本次绘制命令 |
| `SKSurface` / `GRContext` | Avalonia render target；我方 M1 不调用 `SKSurface.Create` | Avalonia | Avalonia | 否 | lease 暴露它们也不意味着可持有 |
| `SKPaint` | 绘制代码（回调内） | 本次回调 | 本次回调 `using` | **M1 否** | 颜色/线宽等来自帧输入；值相同也每帧新建，避免跨帧所有权 |
| `SKPath` | 绘制代码（回调内，如需） | 本次回调 | 本次回调 `using` | **M1 否** | 后续若缓存须新 ADR：按 shape key + DPI/backend generation 失效 |
| `SKBitmap` / `SKImage` / `SKTypeface` | M1 不创建 | — | — | 否 | 未在空白场景中使用；出现时按"创建者拥有、单一所有者、显式 Dispose"重新裁决 |
| `SKColor` / `SKRect` / `SKPoint` / `SKImageInfo` | 值类型 | 值语义 | 无需 Dispose | 可（作为不可变值） | 可放进帧输入/DrawOperation 字段 |
| `SceneSnapshot` | Presentation Coordinator/Publisher | Publisher 发布、读者借用引用 | 不适用（逻辑数据） | 是（不可变引用） | 不含任何 Skia 对象；读者一帧一引用 |

### 十二个必答题

| # | 问题 | 裁决 |
|---|---|---|
| 1 | 哪一层创建资源 | **Skia 原生资源**：只在 Desktop 层的 draw 回调中创建。`SKCanvas` / `SKSurface` 由 Avalonia 创建并通过 lease 借出。Presentation / Application / Domain 永不创建任何 Skia 对象。 |
| 2 | 谁拥有资源 | 回调内创建的 `SKPaint`/`SKPath` 由**本次回调**独占拥有；lease 由本次回调独占拥有；canvas/surface 归 Avalonia。每个对象恰有一个所有者。 |
| 3 | 谁负责 Dispose | 本次回调用 `using`（或等价 `try/finally`）释放自己创建的资源与 lease；**不 Dispose 借来的 `SKCanvas` / `SKSurface`**；Avalonia 负责自身 render target。 |
| 4 | 哪些资源可以缓存 | M1：**无**（没有长期 Skia 对象）。未来若实测需要，只允许由 Renderer 独占、按"内容身份 + DPI/backend generation"键控的不可变资源，并必须先修订本 ADR；禁止资源池、静态缓存、跨视口共享。 |
| 5 | 哪些只能一次绘制使用 | lease、`SKCanvas`、`SKPaint`、`SKPath` 以及任何从 lease 取得的平台图形对象，全部只在一次 `Render(ImmediateDrawingContext)` 调用栈内有效；draw operation 的字段只允许放值类型和快照引用。 |
| 6 | DPI 变化后哪些需要重建 | M1 我方无需重建任何 Skia 资源；DPI 值由 `TopLevel.RenderScaling` / `ScalingChanged` 捕获进帧输入，Avalonia 自行重建其 render target。若未来引入缓存资源，它们必须按 backend/DPI generation 整体失效。 |
| 7 | Resize 后哪些需要重建 | 同 DPI：我方无持久资源；draw operation 的 `Bounds` 由控件当前尺寸给出，lease 每次重新获取。不缓存与尺寸相关的 Skia 对象。 |
| 8 | Workspace 关闭后如何释放 | 控件视觉树分离 → 停止入队新帧 → `ViewportPresentationRuntime.DisposeAsync()`（coordinator → publisher，F-1 屏障保持）。回调内资源已由 `using` 自行释放；没有长期 Skia 对象需要"额外"释放。Renderer 的 `Dispose()` 是幂等空操作（M1）。 |
| 9 | Renderer Dispose 是否幂等 | **是**。M1 Renderer 无字段资源，重复 `Dispose()` 不抛异常、不产生副作用；重复释放 Viewport Runtime 的既有语义保持不变。 |
| 10 | 是否可能出现资源释放后仍被绘制使用 | M1 不会：绘制代码不读共享 Skia 字段；飞行中的帧只用自己的局部对象与已捕获的不可变快照；draw operation 的 `Dispose()` 不释放它不拥有的对象。若未来引入缓存资源，必须先解决与飞行中帧的同步问题（新 ADR）。 |
| 11 | 如何避免 native resource leak | 每个原生对象只有一个 `using` 作用域；不写静态 Skia 字段；不把原生对象放入 draw operation 字段/快照；不依赖 GC；不双重拥有；不使用资源池。边界测试反射检查类型引用。 |
| 12 | 如何测试资源释放与重建 | ① **边界测试**：反射断言 `SceneSnapshot` 及 Domain/Application/Presentation 公共 API 无 Skia/Avalonia 类型（沿用/扩展 `PresentationBoundaryTests`）。② **资源账本**：实现期在 draw operation 内部维护 `List<IDisposable>`（不是公共抽象），测试注入计数替身，断言一次 render 创建数与释放数相等、重复 dispose 不增加计数。③ **生命周期压测**：重复打开/关闭工作区与视口，观察进程内存/句柄趋势；④ DPI/Resize 测试只断言"帧输入变化 + 无我方资源重建"，不虚构 GPU 行为。本阶段不写这些测试。 |

## Consequences

**正面**

- 所有权图极简：除 Avalonia 外没有第二个 Skia 资源所有者。
- 与线程边界（ADR-08）天然兼容：无跨线程原生对象，无释放竞态。
- DPI/Resize/窗口重建不需要我方缓存失效逻辑。
- 逻辑快照保持可跨线程安全共享，不受后端重建影响。

**代价 / 约束**

- 每帧新建 `SKPaint`/`SKPath` 的分配成本在 M1 可接受；性能问题必须实测后才能引入缓存。
- 无 GPU 资源复用的优化空间；若未来引入，需重开资源所有权裁决。
- 无法用"释放后 `Handle == 0`"之类的未经核实断言作为测试依据。

## Rejected Alternatives

- **把 `SKPaint` / `SKPath` 做成 Renderer 的 long-lived 字段**：需要定义创建线程、读取线程、失效时机与关闭释放顺序；在 M1 只画空白背景时没有依据。
- **把 Skia 对象放进 `SceneSnapshot` 或 `PresentationResult<T>`**：直接违反 PRA-01 §4.2 与既有反射测试。
- **静态全局缓存 / 资源池 / `GC.Collect` 兜底**：禁止项；会让多视口互相污染并掩盖泄漏。
- **在 UI 线程 Dispose 回调里创建的资源**：所有权跨越执行上下文，违反 ADR-08。
- **在 macOS / Windows 分别持有原生 surface**：属方案 C（ADR-07 已拒绝）。

## Implementation Constraints

1. 本阶段禁止新增任何 Skia 绘制代码；本文只约束实现期。
2. 若按 `PRA-01` §1.2 在 Desktop 显式声明 `SkiaSharp` 依赖，版本必须保持 **3.119.4**，不改其他 NuGet。
3. draw operation 的 `Dispose()` 只清理自己拥有的对象；不得 Dispose lease 提供的 canvas/surface。
4. 不把 `SKCanvas` / `ImmediateDrawingContext` / lease 作为方法参数传向 Desktop 之外的任何层。
5. 任何未来缓存方案必须先满足：单一所有者、幂等 Dispose、明确失效键、与飞行中帧无冲突。
6. 不得触碰 `docs/map-editor/implementation/`。

## Open Questions

1. `SKPaint` 每帧新建的可观测开销尚无基准；需在实现期以实测决定是否重开缓存裁决。
2. 资源账本测试的注入方式（内部列表 vs 内部工厂）在实现期选择，但不升格为公共接口。
3. `SKTypeface` / 字形资源的长期所有权属于 Text & Label 阶段，本文只声明"M1 不使用"。
4. 后端/设备重建（backend generation）的检测信号在 Avalonia 12.1.6 中的公开入口尚未核实；M1 因无持久资源而不依赖它，未来需补证据。
