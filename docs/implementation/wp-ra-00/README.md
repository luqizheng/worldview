# WP-RA-00 — World View 地图主线现状与实施准备审计 · 总体结论

- **日期**：2026-11-26
- **性质**：只读审计；不包含任何生产代码实现
- **审计基线**：`master` @ `20c6fcca3e13e42cd9a3decdcd1437506890daad`（PR #31/#36/#38 合并后）
- **审计执行环境**：独立 worktree `D:/kudi-projects/worldview-wt-wp-ra-00`，分支 `docs/wp-ra-00-map-readiness-audit`
- **审计方法**：逐文件核实真实代码、EF 映射、测试与 CI 证据；设计文档仅用于定义目标状态，不作为实现完成的依据

---

## 1. 一句话结论

**地图呈现管线（PRA-01 + WP-30B）的"空白场景正确性"已经高质量完成并有充分测试锁定；但整条链路存在一个未接线的集成断点——生产代码从不发起场景请求，渲染器也从不消费快照内容——当前任何用户在任何平台看到的都只是 `#202226` 纯色背景。Core Domain 具备 time-zero 初始状态与 Field Dataset 的权威持久化/读取，但不具备按任意 Historical Time 重建完整地图状态的读取投影。因此：WP-40 不能直接启动完整只读渲染；ME-08 只能先建通用交互框架，正式领域写入全部处于设计状态。**

---

## 2. 三个必须区分的状态（审计中反复出现的混淆风险）

| 层 | 已完成 | 未完成 / 未接线 |
|---|---|---|
| Viewport 生命周期基础设施 | ✅ 控件/Binding/Runtime 关闭子序、故障注入、重试、视口隔离，65 项测试 | 无 |
| 空白场景渲染 | ⚠️ 渲染器只会画纯色背景；且生产代码从不调用 `RequestSceneAsync`，发布器运行时永远是空 | 场景请求管线未接入工作区打开流程；相机/缩放未应用（WP-30C 缺失） |
| 真实地图内容渲染 | ❌ 无任何实现 | 场景模型无几何绘制项；Core Domain 无地图内容读取投影 |

---

## 3. 十个最终问题的回答

### Q1 PRA-01 是否已经真正完成？还有哪些缺口？

**M1 Profile 范围内：完成。** 20 个源文件、107 个测试声明（含关闭/发布竞态、指纹规范向量、装配拒绝、边界架构测试）。已核实的关键不变量均有真实代码与测试支撑（详见 `CURRENT_CAPABILITY_INVENTORY.md` §A）。

**缺口（M1 范围之外、WP-40 范围之内）：**
- 默认准备函数 `DefaultBlankScenePreparationAsync`（`PresentationRequestCoordinator.cs:375`）只产空白场景；不存在任何"从权威数据准备真实内容"的子系统——这是 PRA-01 契约预留、由 WP-40 实现的部分。
- `WorldStateRevision.NotEvaluated` / `ProjectionDefinition.NotConfigured` 是显式占位，真实 World State revision 与 Projection 定义不存在。

### Q2 WP-30B 当前是否已经完成基础 Viewport 生命周期？

**是。** 关闭子序 1a→1b→1c→1d（volatile 终态 → 协调器关闭屏障 → 发布器释放 → 渲染器释放）、阶段级完成标记、失败重试只补未成功阶段、Debug 一次性故障注入（PR #38）、F-1 关闭/发布全序屏障，均有实现与测试（`MapViewportBindingTests` 18 项、`ProjectCloseOrchestrationTests` 17 项、`DebugCloseFaultTests` 4 项、`CoordinatorClosePublicationRaceTests` 4 项）。

**保留项**：`PHASE3A-MANUAL-ACCEPTANCE.md` 是清单而非执行记录——基线内没有 Windows/macOS 实机 PASS/FAIL 记录（见 `TEST_AND_PLATFORM_EVIDENCE.md` §3）。

### Q3 当前 Renderer 能否展示正式地图内容？

**不能。** `MapSceneRenderer.Draw`（`MapSceneRenderer.cs:28`）唯一绘制命令是 `canvas.Clear(new SKColor(0x20,0x22,0x26))`。帧输入中的 `Snapshot`、`CameraX/Y`、`Zoom` 被捕获但完全不被消费。更关键的是：**生产代码中 `RequestSceneAsync` 与 `PresentationContextFactory.FromValidatedWorkspace` 均无调用点**——运行时发布器从未发布过任何快照，`Publisher.Current` 恒为 null。渲染输出与地图内容无关。

### Q4 Core Domain 是否能按 Map / Branch / Historical Time 提供一致的完整地图读取投影？

**不能。** 已核实：
- `ICoreDomainDatabase` 只提供原始行读取（`ListMaps` / `ListMapObjectRefs` / `ListInitialObjects` / `ListAssets` / `ListFieldRevisions` / `ListFieldPayloads` 等），无任何按历史时间取状态的接口。
- 全仓源码无 Command/Event/Checkpoint 实体、无事件回放、无 as-of-time 读取（grep `AsOf|Checkpoint|Replay|EffectiveTime` 零命中）。`Map Initial State` 是每 Map 恰一个的 time-zero 基线（`MapInitialStateRecord`），不是事件序列。
- `HistoricalTime` 在 Infrastructure 中仅出现于工作区上下文解析与 Branch `parent_at` 校验，从不出现在地图内容读取路径。
- Application 层读投影只有 `MapSummary`（`IMapQueryService`）与 `ValidatedWorkspaceContext`；后者是身份投影，不是地图内容。
- Relationship 数据模型不存在（`CoreDomainInitialSql.cs:300` 注释明确"relationship payload 留待 Relationship 阶段"）。

**结论：按 Map+Branch+HistoricalTime 的一致完整地图投影不存在。但 time-zero 初始状态读取（`ListInitialObjects` + placement payload）是真实、已持久化、有测试的——WP-40 首切片不必等完整历史重建。**

### Q5 WP-40 的首个正确实施切片是什么？

见 `NEXT_IMPLEMENTATION_SLICE.md`。摘要：**切片 A（接线）→ 切片 B（time-zero 内容投影 + 最小几何渲染）**，二者边界明确、可独立验收，均不需要 World State 事件重建。

### Q6 WP-40 是否存在必须先解决的 Core Domain 阻塞项？

- **阻塞完整目标**：按任意 Historical Time 渲染——需 Command/Event/Checkpoint 持久化与重建读模型（ADR-002/003 已定框架，未实现）。
- **不阻塞首切片**：time-zero 渲染——Initial State 读取已具备。
- **部分阻塞**：Map extent/bounds 不存在（`MapRecord` 无边界字段），地图框/默认相机/尺度显示需先做正式设计；样式/符号系统只有 token 占位（`PresentationTokens.BuiltinNeutralStyle`）。
- **不阻塞**：投影服务可先支持 Local Cartesian（M1 已裁定视口变换不是地理投影）。

### Q7 ME-08 哪些交互框架可以先实现，哪些正式操作必须等待领域能力？

- **可先建（WP-50 通用框架，只读/暂存）**：Selection、只读 Inspector、Tool Registry 声明、Input/Gesture Routing、Transient Geometry、Preview/Staging、Freshness 规则——ME-06 合同（v0.3）已设计完毕，代码为零。
- **必须等待领域能力**：一切 Apply/Commit——typed Operation/Command 采纳、Relationship 数据模型、Road Endpoint 身份（ME-08 §10 明示开放阻塞）、Spatial Site/Group 与 Connection 类型的 Core Domain 采纳、Building Interior 正式 Command。当前全仓无一个 Relationship 实体、无一条正式编辑写入路径。

### Q8 是否需要修改现有架构或数据库设计？

**无需推翻任何现有设计**（架构评审未发现违反 Ports & Adapters / 单一权威源的实现）。需要的是**新增**而非修改：
1. Command/Event/Checkpoint 表与重建读模型（对齐 ADR-002/003）；
2. Relationship 阶段的数据模型与 Command；
3. Map extent/bounds 的正式设计决策（PRA-01 M1 Profile §5.2 明令 M1 不加，WP-40 需独立 ADR）；
4. Geometry 读取投影与 Presentation 层几何绘制项 schema。

项目 pre-release、无历史数据包袱（roadmap §2.1 明确允许开发期直接修正 schema）。

### Q9 Windows/macOS 的实际验收缺口是什么？

- 基线 `20c6fcca` CI：windows/macos × Debug/Release **4/4 check 全部成功**（build + test）。
- 但 CI 是 headless 构建+测试，**不能证明任何真实 GUI 行为**（WP-30A 平台验证记录 §3 自己也明确了这一边界）。
- Phase 3A 人工验收清单四项（UI-01~UI-04）在基线内**无执行记录**；macOS GUI 从未有任何实机验证记录；Windows blank viewport 的实机证据同样缺失。
- **结论：所有 GUI 层面的平台验收证据为零。**

### Q10 下一步是否可以正式启动 WP-40？

**不建议以"完整只读渲染"定义直接启动。** 建议路径：
1. 先补 WP-30C（pan/zoom/DPI transform）+ M1 十二项验收取证——这是已批准 roadmap 的既定顺序，也是唯一能产生首批 GUI 平台证据的机会；
2. WP-40 第一切片改为"端到端接线 + time-zero 内容投影"（见 `NEXT_IMPLEMENTATION_SLICE.md`），等价于 PRA-03 最小子集 + 最小 Geometry Presentation；
3. 完整历史时间渲染、Field 栅格可视化、动画分别等各自的领域能力门槛，不进入首切片。

---

## 4. 审计文档索引

| 文档 | 内容 |
|---|---|
| [CURRENT_CAPABILITY_INVENTORY.md](CURRENT_CAPABILITY_INVENTORY.md) | A/B/C 三层代码能力清单（每项附路径/类名/测试证据/状态） |
| [RENDERING_READINESS.md](RENDERING_READINESS.md) | WP-40 各能力项五级分级与阻塞项 |
| [AUTHORING_READINESS.md](AUTHORING_READINESS.md) | ME-08 就绪度与领域能力门槛 |
| [DEPENDENCY_GRAPH.md](DEPENDENCY_GRAPH.md) | 实施依赖关系图 |
| [TEST_AND_PLATFORM_EVIDENCE.md](TEST_AND_PLATFORM_EVIDENCE.md) | 测试数量、CI 证据、平台验收缺口 |
| [NEXT_IMPLEMENTATION_SLICE.md](NEXT_IMPLEMENTATION_SLICE.md) | 下一步实施切片建议（含验收标准） |

## 5. 审计边界声明

- 所有判断基于 `20c6fcca` 基线的真实代码；主工作区中未跟踪的 pr36-pr38 完成记录不在基线内，未采用。
- 未运行任何 GUI 程序；未虚构任何人工验收结果。
- 本审计不授权启动 WP-40 / ME-08 代码实现，等待独立复审裁决。
