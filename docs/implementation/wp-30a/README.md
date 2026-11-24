# WP-30A — PRA-01 Presentation Foundation 实施报告

- **日期:** 2026-11-24
- **分支:** `feat/wp-30a-presentation-foundation`
- **基线:** `master` @ `c05ca48d66572a0080034f618637f8e7d4831435`
- **实施规范:** [PRA-01 M1 Implementation Profile V1](../../map-engine/PRA-01_M1_IMPLEMENTATION_PROFILE_v1.md)
- **前置审计:** [WP-30 PRE-FLIGHT](../wp-30-preflight/)
- **结论:** WP-30A 全部 Definition of Done（Profile §9 共 12 项）已实现并验证；未实现任何 WP-30B/WP-30C 内容；未修改 Domain 业务事实或引入伪造状态。

## 1. 本阶段目标

为后续地图渲染建立渲染宿主无关（renderer-host-independent）的 Presentation 基础设施：

- 不可变 PresentationContext 与确定性 ContextFingerprint（SHA-256 + 版本化 canonical 序列化）；
- M1 typed 语义态：WorldStateRevision = `NotEvaluated`、Projection = `NotConfigured`（不伪造）；
- 空白场景快照（中性背景；无任何虚构几何/bounds/extent）；
- SceneAssembler 兼容性校验（混合上下文/缺失必填整体拒绝）；
- 每-视口原子发布器（单调版本 + latest-request-wins）与轻量请求协调器；
- Application 边界拓宽：`ValidatedWorkspaceContext` 保留 RootHistoryId 与 typed HistoricalTime。

## 2. 实现范围

| 交付物 | 位置 |
|---|---|
| Presentation 项目 | `WorldView/src/WorldView.Presentation/`（新增，仅引用 Application） |
| Presentation 测试项目 | `WorldView/tests/WorldView.Presentation.Tests/`（新增，104 测试） |
| Application 边界拓宽 | `IMapQueryService.cs`（`ValidatedWorkspaceContext`） |
| Infrastructure 适配 | `CoreDomainAdapters.cs`（映射保留 RootHistoryId/typed 时间） |
| 存量测试更新 | `SessionUseCaseTests.cs`（FakeResolver 语义对齐）、`Cd01EndToEndTests.cs`（真实链路断言） |
| 新增聚焦测试 | `Application.Tests/Sessions/ValidatedWorkspaceContextMappingTests.cs`（4 测试） |

明确不在本阶段（Profile §7 out of scope，均未实现）：MapViewportControl、ICustomDrawOperation、Skia 绘制、Pan/Zoom、Resize/DPI UI、地理投影、Map bounds/extent、地形/道路/建筑、标签装饰、Map Authoring、通用调度框架。

## 3. 文件导航

| 文档 | 内容 |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 项目结构、依赖方向、Context→Snapshot 数据链、Publisher/Coordinator 职责边界 |
| [CONTEXT_AND_FINGERPRINT.md](CONTEXT_AND_FINGERPRINT.md) | 字段语义、typed NotEvaluated/NotConfigured、canonical 序列化字节级规范、指纹规则与 pinned 向量 |
| [SNAPSHOT_LIFECYCLE.md](SNAPSHOT_LIFECYCLE.md) | 不可变所有权、原子发布、latest-request-wins、last-known-good、close/shutdown |
| [TEST_REPORT.md](TEST_REPORT.md) | 测试类别映射、数量、确定性异步手段、未覆盖项 |
| [PLATFORM_VALIDATION.md](PLATFORM_VALIDATION.md) | Windows Debug/Release 本机验证、CI 四平台结果、CI 与真实 GUI 的边界 |
| [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) | 最终 HEAD、修改文件、设计取舍、偏离项、已知限制、WP-30B 交接 |

## 4. 验证结果摘要

- `dotnet restore`：通过。
- Debug：全 solution 构建 0 错误；5 个测试项目 **836/836 通过**（基线 728 + 新增 108）。
- Release：全 solution 构建 0 错误；**836/836 通过**。
- `WorldView.Presentation.csproj` 源码工程 0 警告（测试工程警告为仓库既有 xUnit1051/CTJ001 模式的延续）。
- 本机 Windows 验证；macOS 仅由 CI 覆盖（无本机 macOS GUI 验证，见 PLATFORM_VALIDATION）。

## 5. 最终结论

**WP-30A Definition of Done 全部满足**，逐项对照见 [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) §2。
经用户验收后可进入 WP-30B 规划；本任务不自动开始 WP-30B。
