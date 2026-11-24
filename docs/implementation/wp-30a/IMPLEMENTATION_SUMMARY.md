# WP-30A 实施总结

- **日期:** 2026-11-24
- **分支:** `feat/wp-30a-presentation-foundation`
- **基线:** master @ `c05ca48d66572a0080034f618637f8e7d4831435`
- **代码提交:** `9e6d777`（feat(presentation): PRA-01 M1 presentation foundation — 37 文件，
  +4122/−9）
- **文档提交:** 本提交（`docs/implementation/wp-30a/` 7 文件）
- **PR:** 见最终回复 / 仓库 PR 列表（分支 → master）

## 1. 交付概览

| 项 | 值 |
|---|---|
| 新增项目 | `WorldView.Presentation`（24 源文件）、`WorldView.Presentation.Tests`（12 文件） |
| 新增测试 | 104（Presentation）+ 4（Application 映射）= 108 |
| 全量测试 | Debug 836/836、Release 836/836（基线 728） |
| 本机平台 | Windows（SDK 11.0.100；Avalonia 12.1.6 未动；无任何包版本变化） |
| macOS | 仅 CI 四平台矩阵覆盖（无本机 GUI 验证，见 PLATFORM_VALIDATION §3） |
| CI | PR #27 → run [45309401459](https://github.com/luqizheng/worldview/actions/runs/45309401459) 四 job 全绿（win/macos × Debug/Release） |

## 2. WP-30A Definition of Done 逐项对照（Profile §9）

| # | 要求 | 状态 | 证据 |
|---|---|---|---|
| 1 | Presentation + Tests 纳入 solution 与 CI build/test 流 | ✅ | sln 注册（含配置矩阵与嵌套）；CI sln 级构建/测试自动发现 |
| 2 | Application 校验上下文保留 typed HistoricalTime + RootHistoryId | ✅ | `ValidatedWorkspaceContext` 拓宽 + 适配器映射 + 会话/e2e/映射三层测试 |
| 3 | World State = NotEvaluated、Projection = NotConfigured，无伪造 | ✅ | typed 单例语义态；factory 固定注入；指纹可区分性测试；无任何伪 revision/CRS 代码路径 |
| 4 | Canonical 指纹向量通过且含全部 M1 身份字段 | ✅ | pinned SHA-256 向量 + 全字段敏感性 + 文化/时区无关测试 |
| 5 | 空白快照按所有权不可变且携带一致上下文身份 | ✅ | ImmutableArray 私有所有权（转型写回抛异常测试）；指纹/版本/装配版本一致测试 |
| 6 | 装配拒绝不完整或混合上下文必填输入 | ✅ | 五类拒绝（缺失/不完整/指纹/schema/子系统）+ 装配版本不支持 + 可选输入出现即须有效 |
| 7 | 每-视口发布原子、单调、latest-request-wins | ✅ | Interlocked.Exchange + Volatile.Read；六步校验；版本严格递增；超替/迟到/关闭全拒绝 |
| 8 | 取消、关闭、停机、迟到结果确定性测试 | ✅ | TCS 门确定性交错（无 sleep）；15 项协调器测试含全部终态路径 |
| 9 | Presentation 程序集无 Avalonia/Skia/数据库/ORM/ViewModel/Dispatcher 依赖 | ✅ | csproj 仅引用 Application；边界测试双轨锁定（程序集引用 + 公开 API 反射） |
| 10 | Debug/Release 构建与测试通过（net11.0） | ✅ | 836/836 × 2 配置；Presentation 源工程 0 警告 |
| 11 | 实施报告记录源文件/API/测试映射/命令结果/偏离/遗留 | ✅ | 本目录七文档 |
| 12 | 无关工作区内容（尤其 `docs/map-editor/implementation/`）未触碰 | ✅ | 该目录保持未跟踪原样；修改清单仅限 §3 所列 |

## 3. 修改/新增文件清单

**修改（5）**
- `WorldView/WorldView.sln` — 注册两个新项目（src/tests 文件夹 + 配置矩阵）
- `WorldView/src/WorldView.Application/Abstractions/IMapQueryService.cs` — `ValidatedWorkspaceContext` 拓宽
- `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/CoreDomainAdapters.cs` — 解析映射保留 RootHistoryId/typed 时间
- `WorldView/tests/WorldView.Application.Tests/UseCases/SessionUseCaseTests.cs` — FakeResolver 与真实 Domain 语义对齐（null→纪元、非法输入拒绝、可注入 RootHistoryId）
- `WorldView/tests/WorldView.Infrastructure.Tests/Persistence/CoreDomain/Cd01EndToEndTests.cs` — 真实链路新增两项断言

**新增（37，含上述 sln）**
- `WorldView/src/WorldView.Presentation/`：csproj；Identity 8 文件（Context/Fingerprint/Canonicalizer/Factory/Tokens/SemanticIdentity/两个语义态）；Scene 4 文件（Snapshot/Layers/Items/BlankSceneContent）；Results 1（PresentationResult）；Assembly 2（Assembler/Contracts）；Publication 5（Publisher/Coordinator/Request 契约/Runtime/GenerationTracker）
- `WorldView/tests/WorldView.Presentation.Tests/`：csproj + 9 测试文件 + TestSupport
- `WorldView/tests/WorldView.Application.Tests/Sessions/ValidatedWorkspaceContextMappingTests.cs`
- `docs/implementation/wp-30a/`：7 文档

**未触碰**：`docs/map-editor/implementation/`（用户未跟踪目录）、Desktop 全部源码、Domain 全部源码、CI workflow、任何包版本。

## 4. 关键设计取舍

1. **PresentationContext 直接采用 Domain typed `HistoricalTime`**（而非再现一份结构体）：
   单一事实来源、类型不降级；Domain 类型不可变，不违反 PRA-01 §1.2"不得保留可变 Domain 实体"。
2. **语义切换立即原子清空快照**（Profile §6.4 clear/replace 二选一中的 clear）：
   单一权威、可测试、消除旧上下文误显窗口；同语义刷新保留 last-known-good（区分两种失败场景，任务 §9.3）。
3. **`RejectedClosed` 为契约 §7.1 状态机的显式扩展终态**：关闭/停机拒绝在诊断上可区分于
   Superseded/Failed（契约允许实现命名自由，Profile §6.1）。
4. **背景 = 场景内 `BlankBackgroundItem`（token 化）而非渲染器私有 clear**：单一背景权威，
   渲染中立；实际颜色解释归 WP-30B 后端。
5. **桌面组合根零改动**：无 viewport 消费者，避免死 DI 注册；以 `ViewportPresentationRuntime`
   提供 per-viewport 可测试接口（任务 §12），接线方式交接 WP-30B。
6. **版本校验顺序 = 指纹 → 序号 → 版本**：与 PRA-01 §6.2 一致；测试按顺序构造对应场景。
7. **`CanonicalHistoricalTime` 改为计算属性**：消除字符串/typed 双事实来源漂移风险，
   五处既有消费点全部保持兼容。

## 5. 偏离 Profile 的情况

**无语义偏离。** 实施过程中发现并修复的实现/测试缺陷（均为本阶段内部问题，非 Profile 冲突）：

- 发布器 `TryPublish` 的 `rejectionReason` out 参数曾被 helper 遗漏赋值（测试捕获后修复）；
- 门控测试两处自身竞态（门后设导致请求抢先完成→挂起锚点永不触发；门释放前阻塞读取
  `task.Result` 死锁）——修复为"先设门、后发起"，并把阻塞读改为 `await`；
- `dotnet sln add` 的 solution-folder 缺陷 → 手工编辑 .sln（等价结果）；
- pinned 指纹向量以真实实现输出落盘锁定（占位值已替换）。

Profile 与最新 Domain 的冲突检查：**无冲突**。Profile 预言的输入缺口（RootHistoryId/typed
时间在 Application 边界被丢弃）与前置审计完全一致，按 §4.1 拓宽解决；未发现需要
stop-and-report 的 Domain 语义冲突。

## 6. 已知限制

1. 无渲染消费者：读者契约由测试替身验证；真实 Avalonia 控件上的单引用捕获属 WP-30B。
2. macOS 仅有 CI headless 证据；无本机 GUI/字体/窗口验证。
3. `Evaluated`/`Configured` 仅有类型与指纹可区分性，无上游读模型（上游不存在时无法更多）。
4. 本机 `dotnet test`（sln）存在既知 MTP NamedPipe 环境问题（master 同现状），以直调测试
   DLL 等效完成本机验证；CI 不受影响。
5. 测试工程存在仓库既有惯例内的分析器警告（xUnit1051 等，与全部既有测试工程一致）；
   Presentation 源工程保持 0 警告。

## 7. WP-30B 交接事项（明确边界）

1. **接线**：在组合根 workspace 工厂内为每个 `MapWorkspaceSession` 创建
   `ViewportPresentationRuntime`；workspace 关闭/会话关闭时 `DisposeAsync`（先协调器后
   发布器顺序已内建）。上下文构建：`session.Context`（已含 RootHistoryId/typed 时间）→
   `PresentationContextFactory.FromValidatedWorkspace(ctx, runtime.ViewportGeneration.Current)`。
   视口输入变化（逻辑尺寸/DPI/相机/可见区域）→ `ViewportGeneration.Advance()` 后重新发起请求。
2. **绘制**：`MapViewportControl` + `ICustomDrawOperation`；draw 回调捕获一次
   `Publisher.Current`（可能为 null = 加载/空态）并全程复用；回调内禁止 DB/IO/Domain/ViewModel/
   mutation。SkiaSharp 按基线显式声明依赖（版本维持 3.119.4 传递来源，Profile §1.2）。
3. **中性背景解释**：渲染后端把 `builtin-neutral-background/v1` token 解析为具体颜色
   （Presentation 不定义颜色值）。
4. **默认相机**：确定性中性默认相机/变换在视口适配器建立（快照不含相机字段——契约不变）；
   Pan/Zoom 属局部 2D 相机变换，不写 Map 数据、不改 Domain revision、不引入地理投影。
5. **WP-30C 前置**：相机/变换与正式 Map 数据隔离的可测试性已由本阶段的语义身份机制保证
   （Pan/Zoom 只改 ViewportGeneration，不触发语义清空）。
6. **PRA-02 前置条件**（pre-flight §5）中，本阶段关闭 1/2/3（PRA-01 验收、边界拓宽、
   可测试读者接口）；自绘控件 vs CustomDrawOperation 的用户裁决与 WorkspaceViewState→
   不可变捕获点仍在 WP-30B 内落地。
