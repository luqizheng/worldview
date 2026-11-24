# ADR WP-30B-11 — WorkspaceViewState 不可变捕获模型

## Status

Proposed（待独立架构审查；本阶段不实现）
- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— 捕获点明确为 `MapViewportBinding.CaptureFrame()`；控件 `Render` 只转发调用，不自行组装帧输入。
- **修订**: R2（2026-11-24）— Detach 不改变捕获点与快照引用；重新挂载后捕获最新快照；关闭标志的保证边界按 ADR-08/10 表述。
- **基线**: master `6879608785e5d23eb014e6f38235b739a92c978d`
- **相关**: `ADR-WP-30B-07` / `08` / `09` / `10`

## Context

现有 `WorkspaceViewState`（`WorldView/src/WorldView.Application/Sessions/MapWorkspaceSession.cs:109-125`）
是**可变类型**：

```csharp
public sealed class WorkspaceViewState
{
    public double CameraX { get; set; }
    public double CameraY { get; set; }
    public double Zoom { get; set; } = 1.0;
    public string ActiveTool { get; set; } = string.Empty;
    public HashSet<Guid> SelectedItemIds { get; } = new();
}
```

它由 `MapWorkspaceSession` 构造并独占持有，文档定性为"应用/编辑器的瞬态视图状态，不是正式
世界数据（AF-01 §8.3）"。现有测试 `SessionUseCaseTests.cs:393-400` 验证不同工作区的 ViewState
互不共享。

WP-30A 同时明确：`SceneSnapshot` **不含相机、尺寸、bounds、DPI、Avalonia/Skia 对象**
（`ARCHITECTURE.md` §3/§4；`PRA-01_M1_IMPLEMENTATION_PROFILE_v1.md` §5.2）。
相机属于视口局部二维变换，`ViewportGeneration` 是视口适配器拥有的单调计数，相机变化要推进
generation（`Profile` §3.5），但相机数值不进入领域语义身份。

`docs/implementation/wp-30a-review/WP-30B_READINESS.md` §5.5 要求定义
"WorkspaceViewState → 不可变捕获点"。本 ADR 在不修改 Application/Domain 代码的前提下裁决该捕获点。

## Decision Drivers

1. 绘制期间不得读取半更新状态；渲染回调不得访问可变 UI/Application 对象。
2. 不可变要落在**捕获点**，不必重写现有可变 ViewState（本阶段禁止修改 Application 业务代码）。
3. 不复制整个世界地图或深拷贝 `SceneSnapshot`：它本已不可变，只复制引用。
4. 领域数据、场景快照、视口瞬态、帧输入、Skia 临时资源必须分层清晰、依赖单向。
5. 保持 WP-30A 指纹语义：generation 变化 → 同语义刷新，不触发语义清空。

## Decision

**保留 `WorkspaceViewState` 现有可变类型不动；在 Desktop 视口边界引入不可变 `ViewportFrameInput`
（帧输入）作为唯一渲染输入。** 捕获点固定为 `MapViewportBinding.CaptureFrame()`（ADR-07 R1）：
控件 `Render(DrawingContext)` 在 UI 线程调用它，binding 同步读取快照引用与视口标量，一次性构造
帧输入并返回；控件把结果交给 draw operation，Renderer 只读该帧输入。**控件自身不读 Publisher、
不读 ViewState、不组装帧输入。**

### 五类对象与依赖方向

```
Core Domain 权威数据（不可变读模型）
        ▼
Application：ValidatedWorkspaceContext + WorkspaceViewState（可变，UI 单写者）
        ▼
Presentation：PresentationContext / SceneSnapshot（不可变，后端中立）
        ▼
Desktop：MapViewportBinding（捕获点：持有 Runtime / Renderer；读 ViewState 与视口标量）
        ▼
Desktop：ViewportFrameInput（不可变值：快照引用 + 尺寸 + DPI + 相机值 + generation）
        ▼
Skia 临时资源（回调内创建/释放；不属于以上任何一层）
```

**只有向下依赖**；不存在从 Presentation/Domain 回到 Desktop 的引用，也不存在 Skia 对象向上流入。

### ViewportFrameInput 的内容（实现期命名可调整，语义固定）

| 字段 | 来源 | 说明 |
|---|---|---|
| `SceneSnapshot? Snapshot` | `MapViewportBinding` 从 `ViewportPresentationRuntime.Publisher.Current` 读取（UI 线程一次 `Volatile.Read`） | 可能为 null = 加载/空态；只存引用，不深拷贝、不修改 |
| `double Width` / `double Height` | 控件 `Bounds` / `SizeChanged`（Avalonia 12.1.6 已核实）→ 经 `binding.OnViewportChanged()` 缓存 | 逻辑像素尺寸；放进来是为了让绘制不回读控件 |
| `double RenderScaling` | `TopLevel.RenderScaling` / `ScalingChanged`（12.1.6 已核实）→ 同上 | DPI 以数值表达，不传 Avalonia 对象 |
| `double CameraX` / `CameraY` / `Zoom` | `MapViewportBinding.CaptureFrame()` 从 `WorkspaceViewState` 复制标量 | M1 不新增 Camera 类型；复制值而非引用 |
| `long ViewportGeneration` | `MapViewportBinding` 持有的 `ViewportGenerationTracker.Current`（请求时捕获） | 用于诊断/一致性核对；请求仍由 PresentationContext 的 generation 参与指纹 |

该对象是 Desktop 内部不可变值（例如 `readonly record struct`）；不含 Avalonia 控件/绘图上下文、
不含任何 Skia 对象、不含 session/ViewModel/可变集合。

### 捕获点归 binding（R1）

`CaptureFrame()` 只做一次同步值拷贝，不保存"当前帧"状态，也不复制场景内容。它返回后，
`MapViewportControl` 只把帧输入交给 `MapDrawOperation`；Renderer 与 draw operation 都不回读
Publisher、ViewState 或控件。暂时 Detach 期间不调用 `CaptureFrame()`；重新 Attach 后下一帧
读取 `Publisher.Current` 的**最新**快照，无需重放中间帧。`binding.IsClosed` 置位后，
**新进入** draw operation 的 `Render` 若读到 true 会立即返回；已通过入口检查的在飞帧可继续
（精确边界见 ADR-08 问题 9 / ADR-10 §5）。`IsClosed` 是终态，不可重置。

### 十个必答题的裁决

| # | 问题 | 裁决 |
|---|---|---|
| 1 | WorkspaceViewState 的职责 | 每工作区的**瞬态编辑器状态**：相机数值、活动工具、选择集。它不是世界数据、不是 Presentation 输入本体、不是渲染缓存；不进入 `SceneSnapshot`。 |
| 2 | Camera 状态如何进入 ViewState | 沿用现有 `CameraX/CameraY/Zoom` setter，由 UI 线程（WP-30C 的 Pan/Zoom 交互）写入；本阶段不实现交互、不改类型。视口只在捕获点时读取这三个值。 |
| 3 | Viewport 尺寸如何进入 ViewState | **不进入**。尺寸是视口属性而非工作区状态；在本阶段禁止修改 Application 类型的前提下，尺寸只进入 `ViewportFrameInput`。将来若产品要求跨视图共享尺寸，须另立 ADR。 |
| 4 | DPI 如何表达 | `double RenderScaling`，从 `TopLevel.RenderScaling` / `ScalingChanged` 在 UI 线程读取，作为帧输入的数值字段；不传 `TopLevel` / `Window` / `Visual` 进绘制层。 |
| 5 | SceneSnapshot 如何与 ViewState 组合 | 由 `MapViewportBinding.CaptureFrame()` 在 UI 线程的一次同步捕获中组合成 `ViewportFrameInput`：快照引用来自 Publisher，视口标量来自 ViewState 与 binding 缓存的尺寸/DPI。两者都不被复制内容；组合只产生一个不可变值。控件只调用该方法，不自行组合。 |
| 6 | 绘制过程中是否允许 ViewState 变化 | **允许**。UI 线程可以继续修改 ViewState（单写者），但当前帧只看已捕获的 `ViewportFrameInput`；下一帧才看到新值。渲染回调绝不回读可变对象。 |
| 7 | UI 状态更新如何生成新 ViewState | M1 保持现有模式：UI 线程直接 set 现有对象的属性，随后生成**新的帧输入**并 `InvalidateVisual()`。本文所说的"不可变快照"指**帧输入**，不是声称 `WorkspaceViewState` 本身不可变。若将来要把它改为 immutable record，需独立 ADR（当前禁止修改 Application）。 |
| 8 | 如何避免 Renderer 读取半更新状态 | ① ViewState 单写者 = UI 线程；② `binding.CaptureFrame()` 同步读取全部标量到一个值拷贝，无 await 间隔；③ `CaptureFrame()` 是唯一捕获点，控件与 Renderer 都不直接读 Publisher/ViewState；④ draw operation 与 Renderer 的字段只接受帧输入；⑤ 不放可变集合引用（`SelectedItemIds` 从不进入渲染路径）。 |
| 9 | 是否复制整个世界地图 / SceneSnapshot | **否**。`SceneSnapshot` 的图层由 `ImmutableArray` 私有持有（WP-30A 已测试锁定），这里只复制一个引用；不深拷贝、不重建场景、不复制地图数据。 |
| 10 | 与 generation 的关系 | 相机/尺寸/DPI 变化由视口在 UI 线程调用 `ViewportGeneration.Advance()`，再用新 generation 构造 `PresentationContext` 发起请求（Profile §3.5）。generation 进入指纹 → 同语义刷新（不清空旧快照）；相机数值本身不进 Domain/Presentation 语义身份。 |

## Consequences

**正面**

- 渲染路径完全不接触可变对象；绘制期间 UI 可继续响应输入。
- 与 `ICustomDrawOperation` 的生命周期吻合：draw operation 持有不可变帧输入，一帧一值。
- 不改动 Application / Domain / Presentation 任何公共 API 或测试。
- 不为"不可变"付出深拷贝世界数据的代价。

**代价 / 约束**

- 屏幕显示可能滞后一帧于 ViewState 的最新值；这是快照语义的正常结果。
- 双写风险：相机数值同时存在于 `WorkspaceViewState` 与帧输入；必须保证只有 UI 线程产生帧输入，且渲染只读帧输入。
- 尺寸/DPI 不归入工作区状态，跨视图共享需求出现时需重新裁决。
- `SelectedItemIds` 等可变集合永不进入帧输入；选择高亮（若将来需要）必须先定义自己的不可变投影。

## Rejected Alternatives

- **把 `WorkspaceViewState` 本身改成 immutable record**：本阶段禁止修改 Application 业务代码，且会波及既有测试与构造函数；收益在单写者模型下不存在。
- **把相机/尺寸/DPI 加进 `SceneSnapshot`**：直接违反 WP-30A 契约（快照不含相机/bounds/后端类型），会污染指纹与语义身份。
- **把可变 `WorkspaceViewState` 引用直接交给 Renderer**：渲染回调可能读到半更新状态，且会形成 Application → Desktop 渲染层的反向可读性泄漏。
- **每帧深拷贝场景/地图**：无必要且代价高；快照已不可变。
- **在帧输入里放 Avalonia `Size` / `Window` / Skia 对象**：会把 UI/后端类型推入绘制数据契约，违背 ADR-07/09 边界。

## Implementation Constraints

1. 不修改 `WorkspaceViewState`、`MapWorkspaceSession`、`SceneSnapshot` 及任何现有测试。
2. `ViewportFrameInput` 只能是 Desktop 内部类型；不公开为跨层 API。
3. 捕获必须发生在 UI 线程且同步完成；禁止在捕获过程中 `await`。
4. 帧输入不含 `HashSet`、事件订阅、委托闭包、`SKSurface` / `SKCanvas` 或 Avalonia 控件。
5. 快照为 null 时仍生成合法帧输入（由渲染器外显示加载/空态），不得伪造空白场景数据。
6. 相机/尺寸/DPI 变化后的 generation 推进与重发请求按 `Profile` §3.5，由 binding 在 UI 线程执行，不在渲染回调中执行。
7. `MapViewportBinding.CaptureFrame()` 是唯一捕获点：控件 `Render` 只转发调用；不得在控件、draw operation 或 Renderer 中再次读取 Publisher / ViewState 组装帧。

## Open Questions

1. `ViewportFrameInput` 的公开程度（仅 Desktop 内部 vs `internal` 测试可见）在实现期确定；默认内部。
2. 是否需要在帧输入中携带 `ActiveTool` / 选择集：M1 不需要；后续编辑预览应由独立不可变投影提供。
3. 相机数值与 generation 的一致性核对是否加断言/诊断：实现期决定，不在本 ADR 强制。
4. 未来若引入多视口，相机状态必须从工作区级迁移到视口级；触发条件与新模型需另立 ADR。
