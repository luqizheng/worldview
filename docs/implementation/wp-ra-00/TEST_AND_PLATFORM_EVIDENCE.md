# WP-RA-00 — 测试与平台证据（TEST_AND_PLATFORM_EVIDENCE）

基线：`20c6fcca3e13e42cd9a3decdcd1437506890daad`。

---

## 1. 自动化测试证据（代码层核实，2026-11-26）

### 1.1 测试声明计数（`[Fact]`/`[Theory]`，排除 obj/bin）

| 测试项目 | 文件数 | 声明数 | 关键覆盖 |
|---|---|---|---|
| WorldView.Domain.Tests | 19 | 202 | 对象/几何/分支规则、HistoricalTime、CanonicalJson、Registry |
| WorldView.Application.Tests | 17 | 176 | 会话生命周期（MapWorkspaceSessionLifecycle 22、ProjectSessionClose 14）、FieldPackage、UseCases |
| WorldView.Infrastructure.Tests | 22 | 234 | CoreDomain 持久化端到端（Cd01/Cd02）、迁移、并发、FieldPackage 权威 |
| WorldView.Presentation.Tests | 10 | 107 | 指纹/上下文/装配/发布/竞态/边界架构 |
| WorldView.Desktop.Tests | 9 | 65 | Binding 关闭子序（18）、C1/C2 编排（17）、DebugCloseFault（4）、ViewModel |
| **合计** | **77** | **784** | |

注：784 为声明数；Theory 展开后实际执行数更高（实施记录中出现过 836、900 的运行计数，随提交演进增长）。本审计以声明计数为准，运行计数以 CI 日志为权威。

### 1.2 关键不变量的测试锁定核实

| 不变量 | 测试（文件：声明数） |
|---|---|
| 关闭后一切迟到结果不得发布（F-1 全序） | `CoordinatorClosePublicationRaceTests`(4) + `SceneSnapshotPublisherTests`(15) |
| latest-request-wins / 过期上下文拒绝 | `PresentationRequestCoordinatorTests`(14) |
| 快照不可变性与原子发布 | `SceneSnapshotTests`(8) + `SceneSnapshotPublisherTests` |
| 上下文指纹等价/差异 | `ContextFingerprintTests`(18) + `PresentationContextTests`(19) |
| 装配整体拒绝 | `SceneAssemblerTests`(18) |
| Presentation 程序集无 Avalonia/Skia/ORM 依赖 | `PresentationBoundaryTests`(2) |
| Binding 关闭阶段序 + 失败重试只补未成功阶段 | `MapViewportBindingTests`(18) |
| C1/C2 编排、ProjectClosed 恰一次 | `ProjectCloseOrchestrationTests`(17) |
| Debug 故障一次性消费/重试 | `DebugCloseFaultTests`(4) |

### 1.3 测试空白区（诚实清单）

- `MapSceneRenderer` 无任何像素/绘制命令断言（当前仅 `canvas.Clear`，无测试价值是合理的，但接通内容渲染后必须补）。
- `MapViewportControl`、`ProjectWindow` 窗口层零自动化（PR #34 裁定：Avalonia Window 生命周期不做无头假测试，走人工验收）。
- 无任何渲染输出回归/快照测试机制。

## 2. CI 证据（基线 commit 对应的运行，非历史提交）

- Workflow：`.github/workflows/dotnet-ci.yml`（基线内）：windows-latest / macos-latest × Debug / Release，`dotnet build` + `dotnet test`（sln 级）。
- 基线 commit `20c6fcca` 的 check-runs（GitHub API，2026-11-26 查询）：

| Check run | 状态 | 结论 |
|---|---|---|
| windows-latest / Debug | completed | ✅ success |
| windows-latest / Release | completed | ✅ success |
| macos-latest / Debug | completed | ✅ success |
| macos-latest / Release | completed | ✅ success |

**4/4 全部成功。** 这是当前 HEAD（master 基线）的 CI 证据，不是历史提交的结果。

## 3. 平台人工验收证据（**缺口为零**）

| 项 | 证据状态 |
|---|---|
| Windows 实机 GUI 运行（blank viewport 打开/关闭/缩放窗口） | ❌ 基线内无执行记录。`PHASE3A-MANUAL-ACCEPTANCE.md` 是四项清单（UI-01 重复点 X、UI-02 并发关闭、UI-03 未挂载窗口、UI-04 原生窗口关闭），要求录屏/截图/日志与 PASS/FAIL 判定——**均为待执行** |
| macOS 实机 GUI 运行 | ❌ 从未有任何实机记录；WP-30A 平台验证记录 §3 明示"macOS 上的窗口呈现/字体/渲染"属后续阶段 |
| Debug/Release 行为差异 | 🟡 设计上已有隔离（`DebugCloseFault` 仅 Debug；Release DLL 经符号检查无 `DebugCloseFault`——记录在 MANUAL-ACCEPTANCE §9），人工对比未执行 |
| M1 十二项验收（roadmap WP-30） | ❌ 0/12 有证据。#1–#5（项目/地图创建打开）有代码与测试但无实机流程记录；#6–#7（空视口、pan/zoom）#6 有代码 #7 无代码；#10（macOS 重复流程）无任何证据 |
| 本机 Debug/Release 全量测试 | ✅ WP-30A 记录：836/836 × Debug/Release（2026-11-24，PR #27 时代）；基线时代本机重跑未执行（CI 已覆盖，但注意 CI 与本机 MTP 桥接差异已记录） |

## 4. 证据等级结论

```text
证据等级            状态
──────────────────────────────────────────────
CI 编译+测试通过    ✅ 基线 4/4
单元/集成不变量锁定  ✅ 784 声明，关键竞态确定性覆盖
真实 GUI 行为       ❌ 零证据（Windows/macOS 均无）
M1 端到端用户流程    ❌ 零证据
性能基线            ❌ 无（仅 TV-01 警示数据，被明令不得当 SLA）
```

**结论：当前 master 的全部"完成"都属于代码+CI 等级。任何"地图视口已在真实窗口正确运行"的判断目前没有证据支撑；这正是下一批工作（切片 A/C）必须优先产出的证据类型。**
