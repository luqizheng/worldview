# WP-RA-00 — 下一步实施切片建议（NEXT_IMPLEMENTATION_SLICE）

原则（用户裁决口径）：**先让地图真正能够从正式数据中正确显示，再逐步建立安全的制作能力；不为尽快看到编辑器界面而提前引入不完整的领域写入机制。**

---

## 0. 排序总览

| 顺序 | 切片 | 性质 | 验收独立 |
|---|---|---|---|
| 1 | 切片 A：端到端接线 | 纯集成，零领域变更 | ✅ |
| 2 | 切片 C：WP-30C 视口导航 + M1 双平台取证 | 既定 roadmap 收尾 | ✅ |
| 3 | 切片 B：time-zero 地图内容只读投影 + 最小几何渲染 | WP-40 首切片 | ✅ |
| 4 | （并行可选）领域工作包 E：Command/Event/Checkpoint | 正式领域能力 | ✅ |
| 之后 | WP-50 交互框架（只读先行）→ 完整 WP-40 → WP-50 Apply | — | — |

A→C→B 的顺序理由：A 极小且解锁一切；C 补齐 M1（既定顺序，且是唯一能产出首批 GUI 平台证据的机会）；B 需要 A 的管线真实运行后才可验收。

## 1. 切片 A — 端到端接线（Pipeline Closure）

**目标**：消除"管线被测试锁定但生产零调用"的断点；让打开地图工作区 → 捕获 Context → 发起场景请求 → 原子发布 → 渲染消费整条链真实运行。

**范围**：
1. `ProjectWorkspaceViewModel.CreateViewportBinding` 时机或 `MapWorkspaceSession` 打开流程中，用 `PresentationContextFactory.FromValidatedWorkspace(workspace.Context, ...)` 构建真实 `PresentationContext`（ViewportGeneration 取 Binding 计数器）。
2. 调用 `runtime.RequestSceneAsync(context)`（默认空白场景准备即可）。
3. `MapSceneRenderer.Draw` 消费帧输入：有快照 → 按快照绘制（当前等价于中性背景）；无快照 → loading/空态区分呈现（roadmap WP-30 范围内"Display basic loading/empty/error states"）。
4. 语义切换（`SwitchContextAsync`）时重新请求；关闭路径不变。

**明确非目标**：不渲染任何地图内容；不加输入处理；不改领域代码。

**验收**：
- 打开工作区后 `Publisher.PublishedCount ≥ 1`，`Current` 非空（诊断暴露）；
- 关闭时未完成请求 `RejectedClosed`、已发布快照保持可读（现有测试语义在真实宿主中成立）；
- 新增最小集成测试：真实 Binding + 默认准备 → 发布发生 → 渲染回调收到非 null Snapshot；
- Windows 实机：打开地图工作区窗口无异常、状态可见（首批 GUI 证据，可并入切片 C 的取证清单）。

## 2. 切片 C — WP-30C 视口导航与 M1 平台取证

**目标**：完成 roadmap 既定的 WP-30 第三步与 M1 十二项验收。

**范围**：
1. map-local ↔ viewport 双向显式变换服务（typed，M1 Local Cartesian，非地理投影——Profile §3.3 边界保持）；
2. `MapViewportControl` 指针输入 → `WorkspaceViewState` 相机推进（pan/zoom），`OnViewportChanged` → generation 推进 → 重新请求（latest-wins 天然收敛）；
3. 渲染器应用相机变换于背景绘制（resize/DPI 正确性由此验证）；
4. M1 十二项验收在 Windows 实机执行并留档；macOS 实机同流程。

**验收**：M1 acceptance #1–#12 全项 PASS 证据（截图/日志/平台版本/构建 SHA），Pan/Zoom 不写任何 Map 数据（#11 用现有领域测试+DB 哈希对比取证）。

## 3. 切片 B — time-zero 地图内容只读投影与最小几何渲染（WP-40 首切片）

**目标**：地图工作区显示该 Map 的 time-zero 正式内容（初始对象 + placement），渲染自 Core Domain 权威数据——用户的验收原则自此兑现。

**范围**：
1. **读投影端口**（Application）：新增 `IMapContentProjection`（命名可议），按 `ValidatedWorkspaceContext` 返回不可变快照输入：初始对象集合（membership/placement canonical JSON → typed 解析）、可选 Geometry Asset 引用。实现走 `ICoreDomainDatabase`（`ListMapObjectRefs` + `ListInitialObjects` + Definition/Asset 链），不新增 SQL 之外持久化。
2. **明确范围声明**：投影输出标定 `HistoricalTime = epoch/time-zero`，`WorldStateRevision = NotEvaluated`；任意历史时间渲染**显式不支持**（等待领域工作包 E），不得用 time-zero 数据冒充任意时间状态。
3. **Geometry Presentation 最小集**（Presentation）：新增几何绘制项 schema（点/折线/多边形最小集；Bézier 可延后），由投影输入装配进 `SceneSnapshot` 可选图层（`SceneAssembler` 显式可选层机制已备）。
4. **渲染**：`MapSceneRenderer` 按快照图层绘制；样式先用 M1 中性 token 单一样式。
5. 若 placement payload 中的几何 schema 尚不足以支撑渲染（审计发现 payload 为 canonical JSON，字段解析需在实施前核实），先做一个"放置点最小渲染"子切片，再扩几何。

**前置**：切片 A 完成；涉及 Geometry Asset 内容读取时核实 `GeometryAssetEnvelope` 信封与 CAS 存取路径（已有持久化测试）。

**验收**：
- 指定 Map 创建初始对象（走现有正式创建服务）→ 打开工作区 → 视口显示这些对象（在 time-zero）；
- 投影不可变性/指纹一致性测试；投影结果与 DB 内容一致性测试；
- 渲染输出回归测试（最小像素/命令断言机制首次建立）；
- 未变更任何领域语义、无第二事实源（架构评审点）。

## 4. 领域工作包 E — Command/Event/Checkpoint 与历史重建（独立，不并入切片 B）

**目标**：解锁"任意 Historical Time 一致地图投影"与 WP-50 Apply 的共同地基。
**要点**：ADR-002/003 已定 History 实体与 Command playback 框架；实施需独立预检、EF 迁移、重建读模型与测试；体量大，单列工作包。Map extent/bounds 的 ADR 决策可与本包同期立项（互不依赖）。

## 5. 纪律约束（对全部切片）

- 每切片：基线 SHA、范围/非目标、测试映射、Debug/Release 证据、`IMPLEMENTATION-LOG.md` 追写（append-only + anchor 行）；
- 禁止：通用调度器/事件总线、影子持久化、为渲染发明领域数据（PRA-01 Profile §1 红线）；
- Windows/macOS 证据随切片积累，不攒到最后；
- 本审计报告及后续实施均不修改 `WorldView/AGENTS.md` 与 docs/ 正式历史文档（修改须用户批准）。
