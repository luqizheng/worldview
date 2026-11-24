# PRA-01 当前实现审计（WP-30 PRE-FLIGHT）

- **审计日期:** 2026-11-24
- **仓库:** `luqizheng/worldview`
- **审计对象:** `master` @ `28897fc6c96a0e448a377950392f403956af0357`（与 GitHub origin/master 一致；PR #26 合并点）
- **审计方法:** 全量源码树直接检查（非文档推断）——类型名 grep（`PresentationContext|ContextFingerprint|PresentationResult|SceneSnapshot|SceneAssembler|SceneSnapshotPublisher|PresentationRequestCoordinator|Scene|Presentation|Skia|ICustomDrawOperation`）、解决方案/工程构成、DI 组合根、AXAML 控件树、NuGet 锁定文件（`project.assets.json`）、测试工程逐一核对。
- **判定词汇:** `IMPLEMENTED_VERIFIED`（有实现且有已运行的测试证据）/ `IMPLEMENTED_UNVERIFIED` / `PARTIAL` / `MISSING` / `NOT_APPLICABLE`
- **先验审计对照:** `docs/map-engine/PRA-01_IMPLEMENTATION_AUDIT_v0.1.md`（2026-11-22，审计点 `0d13b17`，自判 NOT IMPLEMENTED）。本审计在其后 126 个新增源文件（全部为 Core Domain 持久化/Field Package/WP-20a）之上重审。

## 总判定

**PRA-01 在当前 master 上 9 项审计对象 7 项 MISSING、1 项 NOT_APPLICABLE、1 项（专项测试）MISSING。零行 Presentation 代码存在。** 先验审计结论在 `28897fc` 上继续成立，无任何 PRA-01 相关代码在此期间被引入。

证据总纲（四方交叉，均在 `28897fc` 验证）：

1. **类型 grep 零命中:** `WorldView/src` + `WorldView/tests` 全部 `.cs` 文件中，9 个审计对象类型名与 `Scene`/`Presentation` 命名空间零命中；唯一的 "Fingerprint" 命中（206 处宽匹配中与本审计相关的全部为 `ContentFingerprint`/`DefinitionFingerprint`/`ExpectedFingerprint`）属 Field Dataset、Spatial Reference、Map Initial State 的**数据内容摘要**，与 PRA-01 的 Presentation Context 指纹（§2.4）用途、字段、构造规则完全不同，不构成任何复用基础。
2. **解决方案构成:** `WorldView/WorldView.sln` 恰为 4 生产工程（Desktop/Application/Domain/Infrastructure）+ 4 测试工程；无 Presentation/MapEngine/Renderer 工程。
3. **DI 组合根:** `WorldView/src/WorldView.Desktop/Program.cs` `BuildServices()` 未注册任何 Presentation Coordinator/SceneAssembler/Publisher/渲染后端。
4. **UI 控件树:** `WorldView/src/WorldView.Desktop/Views/ProjectWindow.axaml` 为纯文本管理界面，无 Canvas/Viewport/自定义控件；其注释自证"地图渲染属 PRA-01/PRA-02 之后的阶段"（显式阶段决策）。

## 逐项审计矩阵

### 1. PresentationContext — **MISSING**

- **源代码路径:** 无。全仓无该类型或等价物。
- **契约要求:** 契约 §2.1 的 11 个必填字段（MapId、BranchId、typed HistoricalTime、WorldStateRevision、ProjectionDefinitionId/Version、StyleRevision、ViewportGeneration、FontConfigurationRevision、条件性 TerrainRevision、SceneAssemblerVersion）。
- **最接近的现存类型（均不满足）:**
  - `WorldView.Application.ValidatedWorkspaceContext`（`WorldView/src/WorldView.Application/Abstractions/IMapQueryService.cs`）——仅 MapId/BranchId/CanonicalHistoricalTime(string) 三字段，契约文档自注"不透明占位"；
  - `WorldView.Domain.ResolvedWorkspaceContext`（`WorldView/src/WorldView.Domain/Workspace/WorkspaceContextResolution.cs`）——含 MapName/RootHistoryId/Lineage/typed HistoricalTime，但仍无 WorldStateRevision/投影/样式/视口代/字体/装配器版本任何一项。
- **测试路径:** 无（无对应测试类型）。
- **证据:** 类型 grep 零命中；上两类型的完整字段清单见源文件。

### 2. ContextFingerprint — **MISSING**

- **源代码路径:** 无。
- **契约要求:** §2.4 确定性摘要——字段定序、不变式规范编码、null≠默认、schema 版本、SHA-256、测试向量。
- **同名干扰澄清:** 代码中存在的 `ContentFingerprint`（Field Revision/Initial State，`WorldView.Domain/Objects/CanonicalJson.cs:139` `InitialStateFingerprint` 等）是对**领域数据内容**的 SHA-256 摘要，服务持久化 CAS 与包完整性；不存在对**演示上下文字段集**的规范化序列化。两者不共享字段集与语义，不可复用实现（`CanonicalJson` 的规范化思想可供 PRA-01 设计时参考，但那是设计参考，不是实现复用）。
- **测试路径:** 无。
- **证据:** grep `Fingerprint` 全部命中逐一归类（见总纲第 1 条）；无 presentation 序列化器与测试向量。

### 3. PresentationResult（上下文印记不可变结果信封）— **MISSING**

- **源代码路径:** 无。契约 §3.2 要求的 `ContextFingerprint` + 子系统名/schema 版本 + 不可变 payload + 完成状态信封，及 §3.3 列举的 SpatialPresentationResult/AnimationPoseResult/LabelLayoutResult/DecorationLayoutResult/StyleResolutionResult 全部不存在。
- **测试路径:** 无。
- **证据:** 类型 grep 零命中。

### 4. SceneSnapshot — **MISSING**

- **源代码路径:** 无。`grep -i "\bScene"` 在 `src`/`tests` 零命中——无快照类型、无渲染中立场景项、无稳定排序键。
- **测试路径:** 无。
- **证据:** 类型 grep；WP-00 盘点 `WHOLE_PRODUCT_CAPABILITY_INVENTORY.md` 能力项 4.2 判 MISSING（DESIGNED_ONLY），与本审计一致。

### 5. SceneAssembler — **MISSING**

- **源代码路径:** 无。契约 §5 的兼容性校验（指纹一致/必填完整/schema 支持/可选显式/失败不产部分快照）无任何载体。
- **测试路径:** 无。
- **证据:** 类型 grep 零命中。

### 6. SceneSnapshotPublisher — **MISSING**

- **源代码路径:** 无。契约 §6.1–6.3 的当前引用持有、单调 SnapshotVersion、六步发布算法、`Volatile.Read`/`Interlocked.Exchange` 原子交换均不存在。
- **测试路径:** 无。
- **证据:** 类型 grep 零命中；`grep -E "Interlocked|Volatile"` 在 src 中无渲染路径用途命中（`Interlocked` 仅见于持久化并发测试基建）。

### 7. PresentationRequestCoordinator — **MISSING（有相邻生命周期原语，不构成部分实现）**

- **源代码路径:** 无协调器类型。相邻原语：
  - `WorldView.Application.Sessions.MapWorkspaceSession.SwitchContextAsync`（`WorldView/src/WorldView.Application/Sessions/MapWorkspaceSession.cs:49-79`）——经 Core Domain 重新解析替换上下文，取消 `_derivedWorkCts`；
  - 晚到达保护（`MapWorkspaceSession.cs:68-78` 关闭后丢弃解析结果）与 `ProjectSession.OpenWorkspaceAsync` 的 Ready 态门（`ProjectSession.cs:168-175`）。
- **缺失的核心职责:** 请求序列号、latest-request-wins 门、期望上下文指纹跟踪、陈旧候选拒绝、发布决策——协调器的本体一项都不存在。取消是协作式优化（契约 §7.3），不能替代发布时正确性检查；先验审计 PRA01-AUD-04 同此判定。
- **测试路径:** 相邻测试 `WorldView/tests/WorldView.Application.Tests/UseCases/SessionUseCaseTests.cs`（`ClosingProject_ClosesAllChildWorkspaces_First`:349、`ClosingOneWorkspace_KeepsSiblingAlive`:366、`Workspaces_IndependentMutableState`:384、`OpenWorkspace_BeforeReady_Rejected`:427 等）；无任何 stale-publication/latest-wins 测试。
- **判定理由:** "存在接口或设计文档不判实现"——此处连接口都不存在；相邻原语只覆盖"取消/关闭"一侧生命周期。

### 8. 快照生命周期与资源释放 — **NOT_APPLICABLE（无快照；相邻会话生命周期齐备且已验证）**

- **源代码路径:** 无快照故无快照释放。相邻生命周期实现（真实存在、有测试）：
  - 工作区关闭：`MapWorkspaceSession.CloseAsync`（幂等、取消派生工作）+ `DisposeAsync`（释放 CTS），`ProjectSession.CloseWorkspaceAsync`（`ProjectSession.cs:183-202`）从注册表摘除；
  - 停机编排：`Program.Main` 在 UI 生命周期结束后 `registry.CloseAllAsync()`（先关子工作区、逆序释放、不放弃未完成事务，`Program.cs:26-45`）；
  - 会话状态门：`ProjectSession` `_stateGate` + Ready/Closing/Closed/Failed 状态机。
- **测试路径:** `SessionUseCaseTests.cs`（上列 4 项）。
- **判定理由:** 审计对象以"快照"存在为前提；当前无快照，该对象不适用。但相邻生命周期是 WP-30 实施时可直接依赖的骨架（契约 §8 shutdown 要求已有会话层对应物）。

### 9. 对应测试项目和测试覆盖 — **MISSING（PRA-01 专项为零；既有 728 测试健康但不涉演示一致性）**

- **测试工程:** 4 个——`WorldView.Domain.Tests`(266)/`WorldView.Application.Tests`(133)/`WorldView.Infrastructure.Tests`(318)/`WorldView.Desktop.Tests`(11)，合计 728，本审计当日 Windows 双配置全绿（见 TEST_AND_PLATFORM_EVIDENCE.md）。
- **PRA-01 契约 §10 六组必需测试的覆盖情况:** 上下文同一性 0/5、不可变性 0/3、装配 0/5、原子发布 0/6、生命周期 0/5（取消/关闭相邻原语有测试但非快照语义）、确定性 0/3。
- **证据:** 各测试工程文件清单逐一核对（见本目录 TEST_AND_PLATFORM_EVIDENCE.md）；无 Presentation 测试工程。

## 与先验审计（0d13b17）的差异复核

`git diff 0d13b17..HEAD -- WorldView/WorldView.sln`：仅新增 Desktop.Tests 工程；Desktop 源码变更仅 WP-20a（CreateMapDialog + ViewModel 接线 + Program.cs 注册）。126 个新增 src/tests 文件分布于 Domain/Infrastructure/Application 的 Core Domain 持久化、Field Package、设置文档、Map 创建——**无一涉及渲染/演示**。先验审计的 6 项发现（PRA01-AUD-01~06）在 `28897fc` 上逐条仍然成立，其中 AUD-03（可变 `WorkspaceViewState`）与 AUD-05（无渲染消费者）直接约束 WP-30 实施方式（见 PRA_01_GAP_AND_RECOMMENDATION.md）。

## 审计边界

- 本审计为源码树 + 构建配置 + 测试运行审计；未做运行时 GUI 验证（当前也无 GUI 渲染面可验证）。
- 未审计 `docs/map-editor/implementation/`（工作区未跟踪目录，非 master 内容，本任务不覆盖不引用）。
- `docs/map-engine/` 下 PRA-01 契约/交接/生产渲染架构三文档作为判据来源，其自身状态为"推荐契约、待实现审查"，不是实现证据。
