# BASELINE-01 下一阶段设计与开发任务安排（EXECUTION_PROPOSAL）

> **状态说明（2026-11-27）**：本文是审计时的历史提案。PR #41 已合并；原文中的“并行泳道/可并行/全程并行”不再适用，禁止按并行任务执行。以 `BASELINE-01_POST_MERGE_UPDATE.md` 的顺序工作包和最新 `plan.md` 为准。

- **基线**：origin/master `6ec34ea`
- **原则**：以审计实况重排 plan.md 首执行队列；区分设计阶段与实现阶段；公共接口先冻结、单工作包顺序推进、逐包集成；单机桌面软件，不引入多人协作/分布式/不必要并发基础设施；异步 UI 只处理必要的生命周期与取消。

## 执行顺序总览

```
阶段 0（立即，串行）
  WP-RA-03 评审 PR #41 ──► （裁决：accept / amend / reject）
  BASELINE-01 本审计 ──► 独立审查
阶段 1（设计先行，可并行）
  DESIGN-SCENE 场景项类型体系+最小样式契约【接口冻结点，主线咽喉】
  DESIGN-EXTENT Map extent/bounds ADR
  DESIGN-CMD 命令/事件引擎设计（Lane A）
  DESIGN-DIAG 软失败诊断通道设计
阶段 2（实现，按依赖）
  WP-30C 视口导航（可与 UI-04C、DIAG-01 并行）
  UI-04C 独立地图工作区窗口（Lane C）
  DIAG-01 诊断通道实现（小）
阶段 3（集成串行）
  WP-40-B1 time-zero 内容投影+最小几何渲染（消费阶段1冻结接口）
阶段 4（验证基建，全程并行）
  SMOKE-01 Windows GUI 冒烟+pilot 验收记录
  MAC-VERIFY macOS 本机验收
  HEADLESS-01 Avalonia Headless 测试引入（可延后）
```

---

## 工作包清单

### WP-0 / BASELINE-01-RV：本审计的独立审查
1. **编号**：BASELINE-01-RV
2. **目标**：对本审计五份报告做独立复核，确认状态判定与证据无重大误判。
3. **设计工作**：无。
4. **代码开发工作**：无（只读复核）。
5. **前置依赖**：本审计完成。
6. **可并行工作**：WP-RA-03。
7. **文件所有权**：审计报告仅 reviewer 批注，不改判定。
8. **测试要求**：抽查 ≥5 个 IMPLEMENTED 判定的测试可复跑。
9. **平台验收**：不适用。
10. **完成标准**：审查意见落盘；判定修订（如有）合并入报告。

### WP-1 / WP-RA-03：评审 PR #41（Spatial Chunk & LOD Contract V0.1）
1. **编号**：WP-RA-03
2. **目标**：对未合并的 PR #41 契约（576 行、15 项冻结决策）做 accept / amend / reject 裁决，合并后使其成为 PRA-05 的正式设计输入。
3. **设计工作**：逐章评审契约（重点 §4 分区寻址、§5 Fragment 溯源与跨块正确性、§7 LOD 不变量、§10 七层缓存与失效矩阵、§14 冻结决策）；核对与 PRA-01/ME-05/ME-06/ME-07 的依赖一致性。
4. **代码开发工作**：无（纯文档评审+合并）。
5. **前置依赖**：BASELINE-01-RV 通过。
6. **可并行工作**：阶段 1 全部设计包。
7. **文件所有权**：`docs/map-engine/world-view-render-spatial-chunk-and-lod-contract-v0.1.md`、`docs/map-editor/world-view-me-07-*`（PR #41 分支）；不得触碰业务源代码。
8. **测试要求**：不适用（合并后 CI 全绿即可）。
9. **平台验收**：不适用。
10. **完成标准**：PR #41 合并或显式拒绝记录落盘；plan.md 执行队列第 4 项 closure。

### WP-2 / DESIGN-SCENE：场景项类型体系 + 最小样式契约【公共接口冻结点】
1. **编号**：DESIGN-SCENE
2. **目标**：设计 `SceneItem` 几何子类 schema（点/线/面最小集）、样式 token 语义、场景项→Skia 图元的绘制合同；**冻结后 Lane A（几何读取投影）与 Lane B（渲染）才能安全并行**。
3. **设计工作**：新增合同文档（对齐 PRA-01 快照不变量、PR #41 §5 Fragment 溯源字段、ME-05-02 geometry manifest 字段语义）；定义 SceneItem 不可变性、Fingerprint 参与规则、样式 token 最小集（背景已内置，需补 stroke/fill 最小语义）。
4. **代码开发工作**：无（纯设计；实现归 WP-40-B1）。
5. **前置依赖**：WP-RA-03 裁决结果（LOD/Fragment 术语若被采纳需对齐）。
6. **可并行工作**：DESIGN-EXTENT、DESIGN-CMD、WP-30C 实现。
7. **文件所有权**：`docs/map-engine/`（新合同）；`WorldView.Presentation/Scene/` 实现归 WP-40-B1。
8. **测试要求**：设计文档附测试锚定清单（每个不变量对应未来测试名）。
9. **平台验收**：不适用。
10. **完成标准**：合同评审通过并落盘；`SceneItem` 子类 schema 冻结声明写入文档头。

### WP-3 / DESIGN-EXTENT：Map extent/bounds ADR
1. **编号**：DESIGN-EXTENT
2. **目标**：裁决 Map 的空间边界语义（是否必填、坐标系、与 chunk 网格/视口裁剪的关系），补齐 wp-ra-00 Q8-③。
3. **设计工作**：ADR 起草（含候选方案对比：必填固定 extent / 可扩展稀疏 / 无界+惰性 bounds）。
4. **代码开发工作**：无（schema 变更归后续实现包，迁移随 pre-release 纪律直接改）。
5. **前置依赖**：无硬依赖；建议在 WP-40-B1 设计前完成。
6. **可并行工作**：全部。
7. **文件所有权**：`docs/adr/`（新 ADR）；`MapRecords.cs`/迁移归实现包。
8. **测试要求**：不适用。
9. **平台验收**：不适用。
10. **完成标准**：ADR Accepted。

### WP-4 / DESIGN-CMD：命令/事件引擎设计（Lane A）
1. **编号**：DESIGN-CMD
2. **目标**：把已冻结的 ADR-001~005 转化为可实现的 Command/Event/Checkpoint 设计合同，使"任意历史时间读取投影"与 undo/redo 有实现蓝图。
3. **设计工作**：表结构（command/event/checkpoint）、回放语义、time-zero+replay 读模型、与 Map Initial State 的关系、批量修复 undo/redo 的操作建模（对齐 ME-06-d typed intent）。
4. **代码开发工作**：无（实现归 CMD-01 实现包，不在本提案范围启动）。
5. **前置依赖**：无；不阻塞 time-zero 渲染。
6. **可并行工作**：全部（独立泳道）。
7. **文件所有权**：`docs/architecture/core-domain/`（新合同）。
8. **测试要求**：设计附证明测试规格（对齐 p2-09-12 事务证明测试风格）。
9. **平台验收**：不适用。
10. **完成标准**：合同评审通过；EDITOR-01 的前置门开启。

### WP-5 / WP-30C：视口导航（pan/zoom/DPI + 相机真实化）
1. **编号**：WP-30C
2. **目标**：打通用户与视口的输入通道：指针/滚轮事件→相机状态（真实世界单位）→`ViewportFrameInput`→渲染器消费相机；DPI/尺寸变化触发视口代推进（沿用现有 Generation 机制）。
3. **设计工作**：相机模型设计（camera→viewport 仿射变换、zoom 范围/步进、单位桥接——依赖 DESIGN-EXTENT 的 extent 语义）；latest-request-wins 与相机-only 变更的重请求去重策略（R1-P2-02 契约 B 需修订：内容无关变更走本地重绘而非全场景请求）。
4. **代码开发工作**：`MapViewportControl` 输入事件；`WorkspaceViewState` 相机去占位化（Application 层）；`MapViewportBinding` 相机变更通路；`MapSceneRenderer` 消费相机（canvas transform）。
5. **前置依赖**：管线接线（✅ 已完成）；DESIGN-EXTENT（相机单位语义）。
6. **可并行工作**：UI-04C、DIAG-01、全部设计包、SMOKE-01。
7. **文件所有权**：`WorldView.Desktop/Rendering/`（MapViewportControl/MapViewportBinding/MapSceneRenderer 的相机部分）、`WorldView.Application/Sessions/MapWorkspaceSession.cs`（ViewState）、`WorldView.Desktop/ViewModels/`；**禁改**：Presentation Publication/Identity、Infrastructure。
8. **测试要求**：相机变换数学单测（Domain/Presentation 层）；输入→状态→帧输入的 VM 级测试；关闭子序回归（现有 18 测试不回退）；像素测试扩展（同一快照不同相机产生可区分像素）。
9. **平台验收**：Windows 手工验收记录首次真实落盘（pan/zoom 流畅性、DPI 缩放、窗口缩放）；macOS CI 全绿。
10. **完成标准**：用户可 pan/zoom 空白场景且画面随相机变化；全量测试+CI 绿；手工验收记录落盘。

### WP-6 / UI-04C：独立地图工作区窗口（Lane C）
1. **编号**：UI-04C
2. **目标**：落实"一图一独立窗口"架构决策：工作区从 ProjectWindow 内嵌 `ContentControl` 迁出为独立 `MapWorkspaceWindow`，含窗口激活/去重/生命周期。
3. **设计工作**：窗口管理设计（对齐 `docs/implementation/wp-ui-04-map-workspace/` 既有 window-management/ui-structure 合同；不新造决策）。
4. **代码开发工作**：`MapWorkspaceWindow.axaml`；`ProjectWorkspaceViewModel` 窗口编排改造；绑定/会话生命周期迁移（现有 CaptureFrame/关闭子序语义不变）。
5. **前置依赖**：无硬依赖（Session 层 ✅）；建议在 WP-30C 后集成以减少合并冲突。
6. **可并行工作**：WP-30C（设计+实现可同期分支，集成串行）。
7. **文件所有权**：`WorldView.Desktop/Views/`（新窗口）、`ProjectWindow.axaml*`（移除内嵌）、`ProjectWorkspaceViewModel`；**禁改**：Presentation/Application Sessions。
8. **测试要求**：窗口编排 VM 测试；执行 wp-ui-04 手工清单 M-1~M-12 并落盘记录。
9. **平台验收**：Windows 手工验收（多窗口开关、同图去重提示、关闭项目时窗口全收）；macOS CI 全绿。
10. **完成标准**：每图独立窗口、重复打开被去重、项目关闭窗口全部关闭；M-1~M-12 记录落盘。

### WP-7 / DIAG-01：软失败诊断通道
1. **编号**：DIAG-01
2. **目标**：兑现软失败产品决策的用户侧语义：加载成功 + Error 级诊断可见 + 仍可编辑保存。
3. **设计工作**：诊断会话状态结构（诊断集合、级别、来源、去重）、UI 呈现位置（状态栏/诊断面板最小版）、与 `FieldPayloadIntegrityReconciler` 报告的对接。
4. **代码开发工作**：Application 诊断集合（Session 层）；Desktop 状态栏诊断呈现；OpenProjectUseCase 报告→诊断（替换仅 log）。
5. **前置依赖**：无。
6. **可并行工作**：全部。
7. **文件所有权**：`WorldView.Application/Sessions/`、`WorldView.Desktop/Views/ProjectWindow.axaml*`（状态栏）、`OpenProjectUseCase.cs`。
8. **测试要求**：缺失/损坏 payload 场景下：打开成功断言 + 诊断存在断言 + 保存路径不被阻断断言（补齐 G-06 缺失的测试锚定）。
9. **平台验收**：Windows 手工验收（构造损坏 payload 的开发用例）；macOS CI 全绿。
10. **完成标准**：软失败三要素有代码+测试锚定；方针成文（回填 docs）。

### WP-8 / WP-40-B1：time-zero 内容投影 + 最小几何渲染【集成串行点】
1. **编号**：WP-40-B1
2. **目标**：打通"Core Domain time-zero 初始状态 → 场景项 → Skia 图元 → 屏幕"完整链路，用户首次看到真实地图对象。
3. **设计工作**：几何读取投影合同（`ICoreDomainDatabase` 读路径扩展、Object+Definition+Geometry manifest→场景项映射）、缓存策略最小版（M2 范围内可不做 chunk 缓存）。
4. **代码开发工作**：Core Domain 内容读取投影（Infrastructure/Application）；`SceneItem` 几何子类+SceneAssembler 扩展（消费 DESIGN-SCENE 冻结 schema）；真实 `ScenePreparation` 替换 `DefaultBlankScenePreparationAsync`；`MapSceneRenderer` SKPath 绘制（stroke/fill 最小语义）。
5. **前置依赖**：WP-30C（相机真实化）、DESIGN-SCENE（接口冻结）、DESIGN-EXTENT（Accepted）。
6. **可并行工作**：前置满足后，TERRAIN-01 与 CMD-01 设计可并行。
7. **文件所有权**：`WorldView.Infrastructure/Persistence/CoreDomain/`（读投影）、`WorldView.Application/`（投影端口）、`WorldView.Presentation/Scene|Assembly`、`WorldView.Desktop/Rendering/MapSceneRenderer.cs`；**禁改**：迁移文件（如需读路径外的 schema 变更走独立裁决）。
8. **测试要求**：投影单测（对象→场景项映射正确性、悬挂引用软失败语义）；渲染像素级测试（真实几何上屏断言）；端到端集成测试（创建含几何对象的 map→打开工作区→快照含几何项）；全量 823+ 回归。
9. **平台验收**：Windows 双平台手工验收：打开含几何的样例项目可见对象、pan/zoom 下几何正确变换；macOS 本机验证（G-08 首个实质窗口）。
10. **完成标准**：plan.md 里程碑 M2"可用地图工作区"实质达成；全量测试+CI 绿；验收记录落盘。

### WP-9 / SMOKE-01 + MAC-VERIFY：GUI 验收基建（Lane E，全程并行）
1. **编号**：SMOKE-01 / MAC-VERIFY
2. **目标**：关闭 G-08：建立"实际 GUI 运行证据"通道。
3. **设计工作**：冒烟脚本清单（启动→创建项目→打开→关闭→退出）、验收记录模板。
4. **代码开发工作**：`dotnet publish` 打包验证；CI 可选冒烟 job（Windows 先行）。
5. **前置依赖**：无（但建议在 WP-30C 后执行，使冒烟覆盖视口）。
6. **可并行工作**：全部。
7. **文件所有权**：`.github/workflows/`（可选新 job）、`docs/implementation/`（验收记录）。
8. **测试要求**：冒烟清单每项有执行记录（日期/平台/结果）。
9. **平台验收**：Windows 冒烟通过；macOS 本机 Debug/Release 至少各一次启动+项目创建记录。
10. **完成标准**：G-08 降级为 P2；后续工作包的平台验收均有真实记录可依。

## 串行/并行裁决表

| 工作包 | 串行链 | 可并行窗口 |
|---|---|---|
| BASELINE-01-RV → WP-RA-03 | 串行（裁决门） | 与阶段 1 设计并行 |
| DESIGN-SCENE | **主线咽喉**：冻结后才能并行 Lane A/B 实现 | 与 DESIGN-CMD/EXTENT、WP-30C 并行 |
| WP-30C → WP-40-B1 | 串行（相机→内容） | WP-30C 与 UI-04C/DIAG-01/SMOKE-01 并行 |
| UI-04C | 与 WP-30C 集成串行（避免 View/VM 冲突） | 设计/开发分支可并行 |
| DESIGN-CMD | 独立泳道，全并行 | 全程 |
| DIAG-01 | 独立，全并行 | 全程 |
| SMOKE-01/MAC-VERIFY | 独立，全并行 | 全程 |

**推荐第一个设计任务**：`WP-2 / DESIGN-SCENE`（场景项类型体系+最小样式契约）。理由：它是唯一同时被 Lane A（Core Domain 读投影产出物）与 Lane B（渲染消费物）共享的公共接口，冻结越早并行越安全；且不依赖 PR #41 裁决的数值参数（可对齐其术语框架），可在 WP-RA-03 评审期间先行起草。

## 验收纪律（对本提案全部工作包生效）

- 每包交付报告含：基线 SHA、测试命令与结果、CI 证据、失败路径证据、偏差与风险（对齐 plan.md §9 质量门）。
- 单元测试通过 ≠ 桌面应用验收通过；涉及 UI 的工作包必须附手工验收记录。
- pre-release 纪律：schema/表结构修正可直接进行（plan.md §2.3），但迁移文件变更需在包内声明文件所有权。


## Post-merge disposition — 2026-11-27

PR #41 已合并（`73da06f0ef143c9a2df9a690aaed0acf1268e6e6`），WP-RA-03 完成。本文的并行泳道与并行窗口仅保留为审计时草案，不构成项目执行方式。当前顺序及 PRA-05 准入门槛以 `BASELINE-01_POST_MERGE_UPDATE.md` 和根目录 `plan.md` 为准。
