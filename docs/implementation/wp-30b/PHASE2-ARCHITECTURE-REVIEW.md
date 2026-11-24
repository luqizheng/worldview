# WP-30B Phase 2 — 跨 ADR 架构一致性审查

- **Date**: 2026-11-24
- **修订**: R1（2026-11-24）— 针对实现成本的三项关注点做二次审查，补强控件边界与关闭后绘制语义，并同步 07/08/09/10/11。
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
| 6 | UI 与 Render Execution 线程边界是否一致 | ✅ 一致 | ADR-07/08/10/11 同口径：UI 线程写 ViewState、由 `binding.CaptureFrame()` 捕获帧输入、入队失效；绘制只在 Avalonia `ICustomDrawOperation.Render` 回调，入口先检查 `binding.IsClosed`；回调不回读控件/ViewModel/Publisher。 |
| 7 | Workspace 关闭是否与 F-1 关闭屏障兼容 | ✅ 兼容 | ADR-10 关闭全序复用 `Coordinator.CloseAsync` → `MarkCoordinatorClosed()`（publisher gate 线性化点）→ `Publisher.Dispose()`；不修改 `ViewportPresentationRuntime` 既有顺序。 |
| 8 | Viewport 关闭是否会错误关闭整个 Workspace | ✅ 不会 | ADR-10 明确：控件分离只停呈现管线；工作区由 `ProjectSession.CloseWorkspaceAsync` / `CloseAsync` 单独结束；拒绝"控件 Dispose 直接关会话"方案。 |
| 9 | 是否重复实现 Camera 或 Snapshot 生命周期 | ✅ 无 | 不新增 Camera 类型，相机仍是 ViewState 三个标量 + 帧输入复制值；快照仍只有一套 publisher/coordinator 生命周期；Renderer 不持有快照。 |
| 10 | 是否出现不必要接口 / 抽象工厂 / 事件总线 / 调度器 | ✅ 无 | 仅用框架接口 `ICustomDrawOperation` 与 `ISkiaSharpApiLeaseFeature`；Renderer 与 `MapViewportBinding` 均为 `internal sealed` **具体类**（binding 只为拆分职责，不是可替换后端/DI 抽象）；无事件总线、无队列、无调度器、无资源池。 |
| 11 | 是否与现有 WP-30A 公共 API 冲突 | ✅ 无冲突 | `git diff` 中无源文件变更；五份 ADR 仅描述未来接线；既有 `PresentationBoundaryTests` / `CoordinatorClosePublicationRaceTests` 未删除或放宽。 |
| 12 | Windows / macOS 是否都能使用选定方案 | 🟡 接口层可用，运行时未验证 | Avalonia.Skia 12.1.6 + SkiaSharp 3.119.4 原生资产覆盖 Win32/macOS；无 RID/平台分支。**macOS 实际窗口绘制尚未运行验证**（ADR-07 Open Question 3；`PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md` §19 亦标注 macOS runtime 未验证）。 |

## 3. ADR 之间的交叉引用一致性

| 主题 | ADR-07 | ADR-08 | ADR-09 | ADR-10 | ADR-11 | 一致性 |
|---|---|---|---|---|---|---|
| 绘制入口 | `ICustomDrawOperation` | 只在 Avalonia 回调 | lease 在回调内 | 关闭时停止入队 | 帧输入驱动 | ✅ |
| 快照引用 | 控件 `Render` 捕获一次 | 同一引用全程复用 | 快照无 Skia | publisher 不清空 Current | 引用放入帧输入，不深拷贝 | ✅ |
| Skia 资源 | lease 借用 canvas | 不跨线程 | 单所有者 / using | Renderer 空操作 Dispose | 不进帧输入 | ✅ |
| Camera | ViewState 标量 + 视口复制 | UI 单写者 | 不适用 | 随工作区结束 | 进入帧输入的复制值 | ✅ |
| 关闭 | 控件 Detach 停绘制 | 不开新帧 | 局部资源自释放 | 屏障 → publisher → workspace | 捕获点停止更新 | ✅ |
| Renderer 公开面 | internal sealed，无接口 | 无状态 | 无 Skia 字段 | Dispose 幂等 | 只读帧输入 | ✅ |

**原始五项裁决之间未发现相互冲突**，因此无需互相回改；R1 的实现成本补强见 §3A。
以下两项是"刻意保留的开放点"，不是冲突：
- ADR-07 允许实现期决定 draw operation 的 `Equals` 字段；ADR-11 的帧输入提供候选字段。
- ADR-09 的资源账本测试手段在实现期选择，不改变所有权裁决。

## 3A. 补充裁决（R1：实现成本关注点）

独立审查前，按"后续实现成本"的三项关注点做了二次核对。第 1 项已满足；第 2、3 项补强如下。

| # | 关注点 | R1 前的状态 | R1 补强 | 受影响 ADR |
|---|---|---|---|---|
| 1 | 是否提前建独立渲染线程 | ✅ 已满足 | 无改动：无 Render Thread、无调度器、绘制只用 Avalonia 回调 | 08 |
| 2 | 控件是否变成巨型对象 | 🟡 控件虽不"拥有"Camera/Snapshot/Skia，但同时持有 Runtime + Renderer，并负责 generation、请求、捕获，是唯一 wire point | 抽出 Desktop 内部具体类 `MapViewportBinding`，持有 Runtime / Renderer / generation / 捕获 / 关闭标志；控件只保留 Avalonia 本职，并只持有一个 binding 引用 | 07 / 10 / 11 |
| 3 | 关闭后是否仍会绘制 | 🟡 文档写"在飞帧允许自然完成"，未定义"关闭后不再启动绘制" | draw operation `Render` 第一条语句检查 `volatile bool IsClosed`；置位后不取 lease、不建资源、不发命令；并显式声明在飞帧不可中断的边界 | 07 / 08 / 09 / 10 |

### 关闭后绘制的保证边界（精确定义）

| 项 | 结论 |
|---|---|
| **保证** | 关闭命令置位 `IsClosed` 后，不再有新的绘制命令被启动：不获取 lease、不创建 `SKPaint`/`SKPath`、不向 canvas 发任何绘制调用。 |
| **不保证** | 已经进入 `Render(ImmediateDrawingContext)` 的回调被中断或回滚。Avalonia 12.1.6 未公开取消在飞回调的契约；ADR-08 又禁止 UI 等待渲染完成。该帧只使用已捕获的不可变帧输入与局部资源，自然结束后自行 `using` 释放。 |
| **判定点** | `MapDrawOperation.Render` 入口的 `if (binding.IsClosed) return;`。`volatile bool` 为 C# 语言特性，不引入依赖、不加锁。 |
| **与 F-1 的关系** | 两层互不替代：入口检查挡"关闭后启动绘制"；F-1 屏障挡"关闭后发布快照"。两者都保留。 |

### R1 专项一致性核查

| 检查项 | 结论 |
|---|---|
| `MapViewportBinding` 是否成为不必要抽象 | ✅ 否。具体类、无接口、无 DI、无事件；仅把非 Avalonia 职责移出控件，并为关闭顺序提供单一执行者。 |
| 控件是否仍持有 Runtime / Renderer / Skia 资源 | ✅ 否。控件只持有一个 binding 引用；binding 持有 Runtime/Renderer；Skia 仍在回调内。 |
| 关闭后是否可能启动新绘制 | ✅ 否。入口检查先于任何 lease/资源获取；在飞帧边界已在 ADR-08/10 明示。 |
| 是否与 ADR-09 的所有权冲突 | ✅ 无。Renderer 仍无 Skia 字段；binding 只是其持有者，不是第二所有者。 |

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
| 🟡 P2 | Avalonia 是否会对同一 draw operation 重复调用 `Render` 未实测 | 若重复调用，入口关闭检查仍会直接返回、不产生绘制命令；但无法从外部证明"零次调用" | 实现期记录 `Render` 调用次数与关闭标志命中数，仅作观测 |
| 🟢 P3 | M1 每帧新建 `SKPaint` 的分配成本无基准 | 未来大数据量可能成为瓶颈 | 实测后再决定是否重开缓存裁决（新 ADR） |
| 🟢 P3 | M1 限定一个 Workspace 一个 Viewport | 多视口需求需重构相机归属 | 需求出现时先拆分每视口状态 |

## 6. 本阶段变更审计

| 项 | 结果 |
|---|---|
| 新增文档 | `ADR-WP-30B-07` / `08` / `09` / `10` / `11` + 本文件（共 6 份）；R1 仅修订这 6 份，无新增文件 |
| 源代码变更 | **无**（`git diff` 空） |
| 新增依赖 / NuGet | **无** |
| 公共 API 变更 | **无** |
| 测试变更 | **无** |
| 禁止目录 | `docs/map-editor/implementation/` 未触碰（仍为原有未跟踪目录） |
| 提交目标 | 开发分支 `feat/wp-30b-presentation-rendering`，独立提交；不向 master 提交、不合并 PR |

## 7. 结论

五份 ADR 与既有 WP-30A / F-1 契约之间没有发现冲突；方案在接口与包级证据上支持 Windows 与 macOS。
线程、DPI 时序与 `Equals` 失效策略属于实现期必须补证据的开放点，已逐项记录在风险表。

R1 后，控件边界与关闭后绘制语义已收敛：控件只保留 Avalonia 职责并持有单个 binding；
Runtime/Renderer/关闭标志由 binding 单一持有；关闭后不再启动新绘制，在飞回调不可中断的边界
已在 ADR-08 / ADR-10 明确定义。未新增接口、依赖、调度器或源代码变更。

**状态：READY_FOR_REVIEW**。等待独立架构审查；本阶段不宣布架构验收通过，不解锁实现。
