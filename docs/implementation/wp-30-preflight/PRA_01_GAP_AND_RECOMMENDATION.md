# PRA-01 缺口与建议（WP-30 PRE-FLIGHT）

- **日期:** 2026-11-24
- **基准:** `master` @ `28897fc6c96a0e448a377950392f403956af0357`
- **前置阅读:** PRA_01_CURRENT_IMPLEMENTATION_AUDIT.md（逐项判定）、MAP_WORKSPACE_CONTEXT_AUDIT.md（架构检查）

## 1. 缺口清单（按实施阻塞排序）

### GAP-A：Presentation 基础设施整体缺失（P0，唯一"从零开始"的 M1 组件）

7 个核心类型 + 测试工程全部不存在（审计文档逐项判定 7×MISSING）。设计契约已冻结（PRA-01 v0.1 + PRODUCTION_RENDERER_ARCHITECTURE v0.2），**纯实现工作，无设计侧阻塞**。

### GAP-B：Application 边界上下文供给不足（P0 输入缺口）

`ValidatedWorkspaceContext` 仅 MapId/BranchId/时间字符串；PresentationContext 必需的 typed `HistoricalTime`、RootHistoryId、以及 `WorldStateRevision`/`ProjectionDefinitionId`/`StyleRevision`/`ViewportGeneration`/`FontConfigurationRevision`/`SceneAssemblerVersion` 无权威来源。

- typed HistoricalTime + RootHistoryId：Domain 已有（`ResolvedWorkspaceContext`），只需拓宽端口映射（推荐方案，见上下文审计检查 6）；
- **WorldStateRevision：真正的上游缺口**——当前 Domain 明确"本阶段不做 Event Replay/World State 重建"（`WorkspaceContextResolution.cs` 文档自证）。M1 空白图渲染需要的最小语义是"本次读取的状态基线标识"（例如根历史 + 当前时间点的稳定指纹/世代号），**不是**完整 World State。建议 WP-30 以"空白但真实"裁剪：M1 阶段 WorldStateRevision 由现有读路径的可验证要素派生（如 history id + canonical time + 读取快照代），并在契约允许范围内显式记录该裁剪——不得伪造几何内容。
- ProjectionDefinitionId：`MapRecord.SpatialReferenceId` 可为 null（未配置），M1 无投影变换需求（Pan/Zoom 只是视口仿射），可按契约"字段可空/哨兵值+文档化"处理，需在裁剪清单中显式声明；
- StyleRevision/FontConfigurationRevision：M1 无样式系统，用常量版本 0 + schema 版本化即可；
- ViewportGeneration：由视口适配器新造（单调代计数），属 WP-30 新增代码。

### GAP-C：可变 `WorkspaceViewState` 与不可变管线的接缝（P1 正确性边界）

先验审计 PRA01-AUD-03 在 `28897fc` 仍成立：相机/缩放为可变占位属性。WP-30 接线 Pan/Zoom 时必须在请求创建点把相关值捕获为不可变输入（PresentationContext 的 ViewportGeneration 语义，契约 §2.3），不得把 `WorkspaceViewState` 引用传入后台工作。

### GAP-D：无渲染消费者（P1，PRA-02 切片）

ProjectWindow 无视口控件——读者捕获契约（§6.4）在 PRA-01 阶段只能以测试替身验证；真实 Avalonia 视口 + Skia 绘制属 WP-30 内的 PRA-02 切片。先验审计 PRA01-AUD-05 建议继续有效：**PRA-01 阶段不拉入后端绘制**。

### GAP-E：测试基建缺口（P1 质量门）

- PRA-01 六组必需测试 0/24（审计文档第 9 项）；
- 无 Presentation 测试工程（需新建）；
- Avalonia headless UI 测试为零（WP-00 GAP-P1-04 未关闭）——视口行为测试（验收 12 的 platform-specific viewport behavior）需要新建 headless 冒烟工程或渲染中立测试双轨。

### GAP-F：Map 边界/范围数据不存在（P2，M1 空白帧范围裁定）

`MapRecord` 无 extent/bounds（上下文审计附加核验）。roadmap 措辞"when those are available from formal read projections"允许在不可得时跳过 map frame——实施时按"中性背景 + 默认相机"落地，并在实现报告记录该项不可验证的原因，避免验收争议。

## 2. 可复用代码（已验证存在且测试健康）

| 资产 | 路径 | 复用方式 |
|---|---|---|
| 会话/工作区生命周期骨架 | `Application/Sessions/ProjectSession.cs`、`MapWorkspaceSession.cs`（关闭幂等/晚到达保护/停机编排/派生工作取消，均有测试） | 直接依赖；`SwitchContextAsync` 是将来上下文切换→重发演示请求的接线点 |
| typed HistoricalTime | `Domain/Time/HistoricalTime.cs`（TryParse/Canonical/IComparable + 测试） | PresentationContext 字段直接采用 |
| Workspace 上下文解析 | `Domain/Workspace/WorkspaceContextResolution.cs` + `Infrastructure/CoreDomainAdapters.cs:225`（InSnapshotScope 原子快照） | 拓宽映射后作为 PresentationContext 的身份来源 |
| Map 读投影 | `IMapQueryService`/`MapSummary` + `CoreDomainMapQueryService` | 视口打开校验、验收 4/8/9 的身份对照 |
| 组合根模式 | `Desktop/Program.cs`（会话绑定端口注入、停机 CloseAllAsync） | 新 Presentation 服务按同模式注册 |
| 测试基建 | xunit.v3 4.0.3 + MTP + global.json runner 钉扎；4 测试工程 728 全绿 | 新 Presentation.Tests 照搬工程模板（Application.Tests 模式） |
| CI 矩阵 | `.github/workflows/dotnet-ci.yml`（win/macos × Debug/Release） | 新工程自动纳入（sln 级 build/test） |
| 持久化只读路径 | `SqliteCoreDomainDatabase`（FindMap/FindRootHistoryByMap/FindBranch/FindHistory） | "渲染只读"约束的数据侧已具备（渲染回调不得触达，契约 §1.2） |

## 3. 必须新增的代码（PRA-01 最小实施范围）

**新工程 2 个**：`WorldView.Presentation`（renderer-host-independent，禁 Avalonia/Skia/DB/ViewModel 依赖，契约 §1.2）+ `WorldView.Presentation.Tests`。

**核心类型（名称取交接 §4 建议，允许实现者改名，不变量不可减）**：

1. `PresentationContext`（不可变值对象，§2.1 字段集 + §2.5 规范相等）；
2. `ContextFingerprint`（§2.4 规范化序列化 + SHA-256 + 显式 schema 版本 + 测试向量）;
3. `PresentationResult<T>` 结果信封（§3.2：指纹印记 + schema 版本 + 不可变 payload + 完成状态）；
4. `SceneSnapshot`（§4：指纹 + 上下文投影 + 发布器分配的单调版本 + 显式稳定排序的不可变场景内容）；
5. `SceneAssembler`（§5：必填/显式可选校验、指纹/schema 不匹配整体拒绝、失败零产出）；
6. `SceneSnapshotPublisher`（§6：单调版本、六步发布校验、`Volatile.Read`/`Interlocked.Exchange` 原子交换、dispose 后拒绝发布）；
7. `PresentationRequestCoordinator`（§7：请求序列、latest-request-wins、每视口独立实例）；
8. 请求生命周期状态机 + 诊断计数（§7.1/§9）；
9. M1 场景项最小集：空白帧所需的 layer/frame 描述与稳定来源 ID（无需几何/标签/位图项）；
10. Application 侧窄适配：`ValidatedWorkspaceContext` 拓宽（typed HistoricalTime + RootHistoryId）+ 视口适配器（ViewportGeneration 计数）。

**测试（契约 §10 全矩阵，确定性——barrier/TaskCompletionSource 强制交错，禁 sleep）**：指纹稳定性/字段敏感性/文化无关；不可变所有权（builder 变更不渗透）；混合上下文/缺必填拒绝；并发读者旧-新完整可见；版本严格递增；慢旧请求不可覆盖新上下文；取消/停机/迟到完成/独立视口；完成顺序不影响场景排序。

**范围裁剪须显式记录**（交接 §2 第 10 项实现报告）：M1 的 WorldStateRevision 派生方式、ProjectionDefinitionId 可空语义、StyleRevision/FontRevision 常量版本、场景项最小集边界。

## 4. PRA-01 之外、WP-30 还必须新增（PRA-02 切片 + 视口集成）

- Avalonia 视口宿主控件（自绘控件 vs `CustomDrawOperation` 的框架矩阵决策——WP-00 遗留决策点，实施前须落）；
- Skia 绘制路径 + SkiaSharp 引用显式化（版本维持 3.119.4 传递来源）；
- map↔screen / screen↔map 仿射变换 + Pan/Zoom/resize/DPI 行为（消费 `WorkspaceViewState`，捕获时不可变化）；
- 加载/空/错误状态显示；每帧恰好捕获一个快照引用；draw 回调只读（无 DB/IO/Domain/ViewModel/mutation）；
- 快照一致性/变换正确性/平台视口行为/会话生命周期四类自动化测试（roadmap 验收 12）。

## 5. PRA-02 的前置条件（由本审计裁定）

1. **PRA-01 全部验收标准（契约 §11 十条）通过**——特别是：指纹测试向量、混合上下文拒绝、原子发布并发测试、无依赖泄漏（§1.2 清单）；
2. Application 边界拓宽落地（typed HistoricalTime/RootHistoryId 可达 Presentation）；
3. 视口渲染消费者就位（GAP-D 关闭），读者单引用捕获可在真实控件上验证；
4. `WorkspaceViewState`→不可变请求输入的捕获点实现（GAP-C 关闭）；
5. Skia/Avalonia 包现状核验通过（本目录 FRAMEWORK_AND_PACKAGE_MATRIX.md §7：框架在位、包零改动即可开工）；
6. 自绘控件 vs `CustomDrawOperation` 技术决策已落档（WP-00 既定决策点，属用户裁决范围）。

## 6. 建议实施顺序（与先验审计 §6 一致，按当前 HEAD 复核后维持）

S0 工程边界+上下文原语 → S1 不可变结果/场景模型 → S2 装配 → S3 发布器/协调器 → S4 生命周期/诊断 → S5 集成+实现报告。依赖门：若 World State 读投影在实施时仍未就绪，按交接 §6 依赖门以测试夹具驱动 PRA-01，**渲染保持阻塞，不连伪造数据**（与 roadmap"空白但真实"一致）。
