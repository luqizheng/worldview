# WP-30B 接入准备度评估（Readiness Review）

只评估，不实施。结论：**WP-30A 已提供 WP-30B（Avalonia 视口 + Skia 绘制空白场景）所需的全部核心契约；可进入 WP-30B**。需在 WP-30B 内解决的接口/线程事项如下（均不要求反向修改 WP-30A 的既有契约，仅 F-1 建议先以小修复关闭）。

## 1. 已就绪的接入面

| WP-30B 需求 | WP-30A 已提供 | 证据 |
|---|---|---|
| 单次捕获 SceneSnapshot | `SceneSnapshotPublisher.Current`（`Volatile.Read`，无锁）；读者契约（一次捕获、全程复用、null=加载/空态）由测试锁定 | SceneSnapshotPublisher.cs:39；ReaderCaptures/ConcurrentReaders 测试 |
| 视口 resize/generation 变化 | `ViewportGenerationTracker.Advance()`（Interlocked 单调）+ "变化即新请求"的既定模式 | Profile §3.5；测试锁定代数参与指纹 |
| Camera transform | **快照不含相机**（类型层无此字段）——相机明确归视口适配器，Pan/Zoom 不触发语义清空（同语义刷新） | Profile §5.2/§10；WorkspaceSemanticIdentity 不含相机 |
| 渲染资源生命周期边界 | 快照与后端资源分离；释放序列（先协调器后发布器）已内建；"已发布快照在被引用期间有效"语义明确 | Runtime.DisposeAsync:56-60；Publisher.Dispose 语义测试 |
| 准备管线扩展点 | `ScenePreparation` 可注入委托（WP-30B/C 的真实准备在此插槽接入）；结果信封已带指纹印记/schema | Coord:216；PresentationResult |
| 每视口私有、绝不全局共享 | `ViewportPresentationRuntime.Create()` 三件套封装；实例隔离测试 | Runtime:30-35；IndependentViewports 测试 |
| 组合根接线指引 | IMPLEMENTATION_SUMMARY §7 给出明确接线方式（workspace 工厂内创建 runtime、关闭时 DisposeAsync、上下文构建与代数推进的调用序） | 文档级，合理（M1 无消费者，提前注册死 DI 反而违规） |

## 2. WP-30B 能否只依赖 Presentation 契约、不反向修改 Domain？

**能。** 渲染所需全部输入（指纹、语义身份、图层/项、背景 token）已在快照契约内；WP-30B 的 Avalonia/Skia 类型全部落在 Desktop/控件层，Presentation 契约保持渲染中立（`ICustomDrawOperation` 只消费 `Current` 引用）。无任何需要改 Domain 才能绘制的缺口。

## 3. 是否存在迫使 WP-30B 绕过 Publisher 的设计？

**不存在。** `TryPublish`/`ActivateRequest` internal + `SceneSnapshot` internal 构造 + 协调器唯一发布路径——WP-30B 无法也无需绕过；`Current` 读取已覆盖全部读者需求。

## 4. 抽象合理性

`ScenePreparation` 注入、`SceneAssemblyResult` 拒绝阶段、可选图层机制均为最小必要抽象（服务于确定性测试与未来子系统），未见过度设计；亦无 M1 不需要的通用调度/队列/事件总线（Profile §6.2 禁令保持）。

## 5. WP-30B 必须解决的事项清单（按 Profile §10 交付边界）

1. **UI 线程 / 渲染线程边界**（当前未定义，属 WP-30B 范围）：`MapViewportControl` 的失效请求在 UI 线程、`ICustomDrawOperation` 执行在渲染线程；必须规定：draw 回调内只允许 `Current` 捕获 + 纯绘制；`Advance()`+`RequestSceneAsync` 只在 UI 线程发起；runtime 的 DisposeAsync 与控件分离的顺序约定。
2. **Skia 资源租约/释放/重建**（Profile §2.2 明确归 WP-30B）：背景色画笔等后端资源的所有权、失效重建（DPI 变化）策略；不得进入快照。
3. **SkiaSharp 依赖显式声明**：按 Profile §1.2 在拥有绘制代码的 Desktop 工程显式声明（维持 3.119.4 传递解析版本），不再依赖偶然传递引用。
4. **workspace 生命周期接线**：`MapWorkspaceSession.CloseAsync`/会话关闭 → `ViewportPresentationRuntime.DisposeAsync` 的编排（§7 指引落地）；**注意 F-1**：协调器关闭后应尽快释放发布器（当前 Runtime 已按此顺序实现，接线时不得延迟两步之间的间隔）。
5. **WorkspaceViewState → 不可变捕获点**：Pan/Zoom 改 `WorkspaceViewState`（CameraX/Y/Zoom）后 `Advance()` 并以捕获的代数发起新请求——视口适配器需定义该捕获时机（Profile §3.5"不传可变引用"在 WP-30B 首次落地）。
6. **背景 token → 具体颜色解释**：`builtin-neutral-background/v1` 在 Skia 后端的解释表（Presentation 不定义颜色值——契约边界，WP-30B 首个消费者）。
7. **自绘控件 vs CustomDrawOperation 的用户裁决**：pre-flight 遗留裁决项（快照契约对两者均兼容，不阻塞）。
8. **加载/空态 UI**：`Current == null` 的呈现（loading indicator 在渲染器外）。
9. **（建议）F-1 小修复先行**：真实读者进入后，"关闭后仍可发布"的窗口从纯理论问题变为可观察行为；修复小（把协调器 closed 状态纳入发布决策），建议在 WP-30B 首批改动前或随首批改动落地。

## 6. 线程边界现状声明

Presentation 层自身无 UI 线程概念（正确——渲染中立契约不应有）；这不是缺陷，但意味着线程纪律是 WP-30B 的**必答题**而非可选项：M1 的测试替身读者不经历真实渲染线程，PRA-01 §2.2 的"Avalonia UI/render thread boundaries"要求自 WP-30B 起可执行验证。
