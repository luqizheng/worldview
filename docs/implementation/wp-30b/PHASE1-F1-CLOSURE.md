# WP-30B Phase 1 — F-1 Closure 修订记录

状态：READY_FOR_REVIEW（待架构审查确认；Phase 2 未解锁）
分支：`feat/wp-30b-presentation-rendering` · PR #29 · 基线 master `63d7519`

## 1. F-1 根因（不变，经修订复审仍然成立）

WP-30A 审查 F-1（P2）：`PresentationRequestCoordinator` 完成门（读协调器 `_closed`）与最终发布决策（`TryPublish`）之间不是同一原子区。请求 R 在关闭前通过完成门后，可在"协调器已关闭、发布器尚未释放"的窗口内成功发布（关闭后无新请求激活 → 指纹/序号校验均通过），违反 SNAPSHOT_LIFECYCLE §5.3 / PRA-01 §8 的绝对迟到发布拒绝语义。

## 2. 核心修复（修订保留，未改动）

1. `SceneSnapshotPublisher` 内部关闭屏障：`MarkCoordinatorClosed()` / `IsCoordinatorClosed`（R1 修订：**internal**）。
2. **线性化点**：协调器 `CloseAsync()` 在协调器 gate 内调用 `MarkCoordinatorClosed()`，于**发布器锁内**置位屏障（锁序 coordinator-gate → publisher-gate，与 `ActivateRequest` 同向，无死锁）。
3. `TryPublish` 与 `AllocateCandidateVersion` 在发布器锁内检查屏障 → 关闭与发布决策在**同一把锁**上串行化，形成全序：发布决策要么先于屏障置位（合法，close 后保持可读），要么后于屏障（拒绝 `coordinator_closed`，落定 `RejectedClosed`）。
4. 生命周期类拒绝（`coordinator_closed`/`publisher_disposed`）落定 `RejectedClosed`；身份类（`candidate_missing`/`context_mismatch`/`stale_request`/`non_monotonic_version`）落定 `RejectedStale`。
5. `Runtime.DisposeAsync` 顺序不变（先关协调器、再释放发布器）；两步间隙不再承载正确性。

## 3. 修订内容（WP30B-F1-R1 / R2）

### R1 — `IsCoordinatorClosed` 改为 internal

`public bool IsCoordinatorClosed` → `internal bool IsCoordinatorClosed`。测试程序集经既有 `InternalsVisibleTo("WorldView.Presentation.Tests")` 访问（`WorldView.Presentation.csproj` 既有配置，WP-30A 起即存在），未新增任何可见性机制。生产公共面零新增。

### R2 — 撤销 `SceneAssembler` 公共继承扩展

恢复 `public sealed class SceneAssembler` + 非 virtual `public SceneAssemblyResult Assemble(...)`（与 WP-30A 基线逐字节一致的公共形态）；删除"未 sealed + virtual 测试缝"的 XML 段落。

## 4. 确定性测试注入点 — 方案比较与选择

### 方案 A（采用）：内部装配接口 + 协调器内部构造器

```csharp
// SceneAssemblyContracts.cs（程序集内不可见于外部）
internal interface ISceneAssembler
{
    SceneAssemblyResult Assemble(PresentationContext context,
        PresentationResult<BlankSceneContent> requiredBlankScene,
        IReadOnlyList<PresentationResult<SceneLayerDescriptor>>? explicitlyOptionalLayers,
        long candidateSnapshotVersion);
}

// SceneAssembler：sealed 实现（公共形态与基线一致，仅追加内部接口实现）
public sealed class SceneAssembler : ISceneAssembler { public SceneAssemblyResult Assemble(...) }

// PresentationRequestCoordinator：公共构造器签名逐字节不变，委托内部构造器
public PresentationRequestCoordinator(SceneSnapshotPublisher publisher, SceneAssembler assembler,
    ScenePreparation? preparationOverride = null)
    : this(publisher, (ISceneAssembler?)assembler, preparationOverride) { }   // null 检查行为与基线一致

internal PresentationRequestCoordinator(SceneSnapshotPublisher publisher,
    ISceneAssembler? assembler, ScenePreparation? preparationOverride = null) { ... }
```

测试以 `GatedSceneAssembler : ISceneAssembler`（**包装真实 `SceneAssembler`**，门控放行后委托真实装配——生产校验语义不被替身绕过）经内部构造器注入。

### 方案 B（未采用）：内部委托注入

协调器增加 `internal PresentationRequestCoordinator(..., Func<...> assembleOverride)` 之类内部委托参数。

### 比较与理由

| 维度 | 方案 A（内部接口） | 方案 B（内部委托） |
|---|---|---|
| 依赖形状 | 与既有 `ScenePreparation` 委托缝对称；协调器持有具名依赖 | 协调器同时持有具体 assembler 与委托，二选一逻辑分支 |
| 生产行为 | 零变化（公共 ctor → 同一内部 ctor；字段类型变化不影响调用路径） | 零变化，但新增"委托优先还是 assembler 优先"的隐式规则 |
| 抽象层级 | 一层具名接口（任务允许："内部装配抽象"） | 一层委托，语义弱化（调用点签名更长、意图不明） |
| 契约一致性 | `Assemble` 契约（整体拒绝、零部分产出）在接口 XML 显式声明 | 契约只能写在委托 XML 或调用点注释 |

**选择 A**：与生产依赖方向（协调器 → 装配）一致、与既有 `ScenePreparation` 注入模式对称、契约声明有固定落点；`Assemble` 是同步四参数调用，具名接口比裸委托更可审计。

### 为什么不改变生产行为

- 公共构造器（`Runtime.Create` 与任何外部调用者的唯一路径）签名与函数体语义逐字节等价：同样的参数、同样的 null 抛出、委托至的内部构造器执行与基线相同的赋值。
- 装配调用点 `_assembler.Assemble(...)` 语义不变——生产路径传入的是真实 `SceneAssembler` 实例。
- `SceneAssembler` 公共形态与基线一致（sealed、非 virtual、`Assemble` 同签名）；追加的接口实现不可见于程序集外。

### 为什么不扩大公共 API

见 §6 审计：新增成员全部 `internal`；`InternalsVisibleTo` 为既有机制。

## 5. 测试注入结构变化说明

| 项 | 修订前 | 修订后 |
|---|---|---|
| 门控替身 | `GateAssemblingSceneAssembler : SceneAssembler`（override virtual） | `GatedSceneAssembler : ISceneAssembler`（内部接口实现，内部包装真实 assembler） |
| 构造路径 | 公共构造器 | 内部构造器（`InternalsVisibleTo`） |
| 等待保护 | 装配门 `Wait()` 无界、测试 await 无界 | 全部等待带 `WaitAsync`/`Wait(TimeSpan)` 10s 上限；装配门超时抛 `TimeoutException`（确定性锚点未满足即失败，不静默继续） |
| 7 项测试场景 | 保留 | 保留，断言零放宽 |

F-1 窗口测试的程序序锚点不变：替身 `Assemble` 内发信号 → 测试观察到信号即代码序证明完成门已通过；随后 close（屏障生效、Publisher 未 Dispose）→ 放行 → `RejectedClosed`。

## 6. 公共 API 审计（基线 63d7519 vs 修订后）

方法：`git grep` 提取两棵树的 public 类型声明全量比对（36 vs 36）+ `git diff 63d7519 -- src/WorldView.Presentation` 中所有含 `public`/`internal` 的增删行逐条核对。

| 审计项 | 结论 |
|---|---|
| 新增公共类型数量 | **0**（新增 `ISceneAssembler` 为 internal） |
| 新增公共成员数量 | **0**（新增 6 处均为 internal：1 接口、1 构造器、2 常量、1 属性、1 方法） |
| 现有类型继承语义变化 | **无**（`SceneAssembler` 保持 sealed、无 virtual 成员；仅追加程序集内不可见的接口实现） |
| 现有公共方法签名变化 | **无**（公共构造器签名不变；`Assemble` 公共签名不变） |
| 已有公共 API 行为变化 | 仅两处、均为 F-1 契约要求的强化：① close 后发布/分配被拒（原为缺陷窗口）；② 生命周期类拒绝落定 `RejectedClosed`（原 `RejectedStale`，无既有断言依赖）。既有 836 项测试全部原样通过 |

## 7. 验证证据

- 本地 Windows：全 solution Debug 构建 0 错误 + **843/843**（Domain 266 / Application 137 / Presentation 111 / Infrastructure 318 / Desktop 11）；Release 同 **843/843**。
- CI（PR #29 分支推送）：Windows/macOS × Debug/Release 四 job（run 链接见交付报告）。
- 基线：WP-30A 836 + Phase 1 新增 7 = 843，数量无变化。

## 8. 文档同步

- `docs/implementation/wp-30a/SNAPSHOT_LIFECYCLE.md`：§2 校验清单与分类口径；§5.1 线性化论证 + 注入机制描述（内部抽象，非公共扩展点）；§7 关闭语义。
- 本文件：`docs/implementation/wp-30b/PHASE1-F1-CLOSURE.md`（Phase 1 修订记录，后续阶段文档在此目录演进）。
