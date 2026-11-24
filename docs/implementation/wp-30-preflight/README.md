# WP-30 PRE-FLIGHT — PRA-01 代码审计与技术栈核验

- **日期:** 2026-11-24
- **性质:** WP-30 Basic Map Rendering 实施前基线审计（严格只读——未改业务代码/Schema/NuGet/设计文档，仅新增本目录）
- **代码基准:** `master` @ **`28897fc6c96a0e448a377950392f403956af0357`**（本地 = GitHub origin/master，PR #26 合并点；工作区仅有未跟踪目录 `docs/map-editor/implementation/`，未触碰）

## 文档索引

| 文件 | 内容 |
|---|---|
| [PRA_01_CURRENT_IMPLEMENTATION_AUDIT.md](PRA_01_CURRENT_IMPLEMENTATION_AUDIT.md) | 9 个审计对象逐项判定（源路径/类型/测试/证据） |
| [FRAMEWORK_AND_PACKAGE_MATRIX.md](FRAMEWORK_AND_PACKAGE_MATRIX.md) | SDK/TFM/Avalonia/SkiaSharp/渲染后端/自绘控件核验 |
| [MAP_WORKSPACE_CONTEXT_AUDIT.md](MAP_WORKSPACE_CONTEXT_AUDIT.md) | 6 项架构检查 + Map 边界数据可得性 + 并发范围确认 |
| [PRA_01_GAP_AND_RECOMMENDATION.md](PRA_01_GAP_AND_RECOMMENDATION.md) | 缺口清单（GAP-A~F）/ 可复用 / 必须新增 / PRA-02 前置条件 |
| [TEST_AND_PLATFORM_EVIDENCE.md](TEST_AND_PLATFORM_EVIDENCE.md) | 本机 728×2 测试运行、CI 4 job 证据、平台证据缺口 |

## 当前真实实现状态（一句话）

**PRA-01 未实现（7×MISSING + 1×NOT_APPLICABLE + 测试 MISSING），渲染子系统在当前 HEAD 不存在于任何形态；但 Core Domain 会话/上下文/读路径与全部框架依赖已就绪且 728 测试双配置全绿——WP-30 是纯新增实现工作，设计侧无阻塞。**

与先验审计（`docs/map-engine/PRA-01_IMPLEMENTATION_AUDIT_v0.1.md` @ `0d13b17`，2026-11-22）的差异：此后 126 个新增源文件全部属 Core Domain 持久化/Field Package/WP-20a Map 创建，无一涉及渲染；先验 6 项发现逐条在 `28897fc` 仍成立。

## 关键事实速览

- **代码:** 无 Presentation/Scene/Skia 标识符；sln = 4 生产 + 4 测试工程；组合根未注册任何渲染服务；ProjectWindow 为显式的文本基线（注释自证阶段决策）。
- **栈:** net11.0（SDK 11.0.100 本机/CI 11.0.x）+ Avalonia 12.1.6 + Avalonia.Skia 12.1.6 + SkiaSharp 3.119.4（均传递就位，**包零改动即可开工**）；渲染后端 = 默认 Skia 无自定义；`ICustomDrawOperation` 零命中。
- **上下文:** Desktop→Application→Domain 链路真实可用；typed `HistoricalTime`/Branch 世系/根历史解析已有测试；Application 侧 `ValidatedWorkspaceContext` 为窄占位（typed 时间/RootHistoryId 在边界被丢弃）——WP-30 需拓宽该映射。
- **数据:** `MapRecord` 无 extent/bounds；`SpatialReferenceId` 可空——空白帧可渲染内容 = 中性背景 + 默认相机，map frame 项按 roadmap "when available" 措辞跳过并留痕。
- **并发:** 单机单用户无张力；PRA-01 契约与仓库现状同为进程内原子性（`Volatile.Read`/`Interlocked.Exchange`），无 broker/分布式依赖。
- **测试/CI:** 本机 Windows Debug+Release 728/728（2026-11-24）；CI run 45291249883（= 当前 HEAD 合并）4 job 全绿；macOS 有 CI 证据、**GUI 运行证据为零**。

## 最终结论

### 可以复用的代码
会话/工作区生命周期骨架（ProjectSession/MapWorkspaceSession，含停机编排与晚到达保护）、typed `HistoricalTime`、`WorkspaceContextRules` + `InSnapshotScope` 原子解析、`IMapQueryService`/`MapSummary` 读投影、`CreateMapUseCase`（WP-20a）、组合根模式、xunit.v3+MTP 测试基建、CI 矩阵。详见 GAP 文档 §2。

### 必须新增的代码
`WorldView.Presentation` + `WorldView.Presentation.Tests` 两个工程；`PresentationContext`/`ContextFingerprint`/`PresentationResult<T>`/`SceneSnapshot`/`SceneAssembler`/`SceneSnapshotPublisher`/`PresentationRequestCoordinator` + 请求生命周期/诊断 + 空白帧最小场景项；Application 边界拓宽（typed HistoricalTime + RootHistoryId）与视口适配器（ViewportGeneration）；契约 §10 全部 24 项测试。PRA-02 切片另需视口控件 + Skia 绘制 + 双向变换 + Pan/Zoom。详见 GAP 文档 §3–4。

### PRA-01 最小实施范围
S0–S5 六步（工程边界→上下文原语→不可变模型→装配→发布/协调→生命周期/诊断/集成），交接 §3 约束全量适用（TFM/包不动、无 Avalonia/Skia/DB/ViewModel 泄漏）；M1 裁剪项须显式记录：WorldStateRevision 以现有读路径要素派生（不伪造数据）、ProjectionDefinitionId 可空、Style/Font 常量版本 0、场景项最小集。依赖门：无 World State 读投影时以测试夹具驱动，渲染保持阻塞。

### PRA-02 的前置条件
① PRA-01 十条验收全过；② Application 边界拓宽落地；③ 视口消费者就位（真实控件上验证单引用捕获）；④ `WorkspaceViewState`→不可变输入捕获点实现；⑤ 包现状核验通过（已由本审计完成）；⑥ 自绘控件 vs `CustomDrawOperation` 技术决策落档（用户裁决项）。

### Windows/macOS 尚缺的验证证据
Windows：正式 M1 十二项流程记录（日常开发使用≠验收留痕）。macOS：实机 GUI 运行证据全部缺失（CI 不能替代）。共同：渲染类四类自动化测试（快照一致性/变换正确性/平台视口/会话生命周期）不存在、headless UI 测试基建为零、停机路径无覆盖、无打包形态定义。详见 TEST_AND_PLATFORM_EVIDENCE.md §4。

---

**停止点:** 本任务到此为止（pre-flight 完成）。未开始任何 PRA-01/WP-30 实施；未升级任何框架/包；`docs/map-editor/implementation/` 未跟踪内容未触碰。实施启动前待用户裁决事项：WP-00 既定的"自绘控件 vs CustomDrawOperation"框架矩阵决策，及 GAP-B 中 WorldStateRevision 的 M1 派生方式确认。
