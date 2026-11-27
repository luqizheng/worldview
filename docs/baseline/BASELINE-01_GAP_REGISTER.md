# BASELINE-01 缺失能力、技术缺陷与风险清单（GAP Register）

- **基线**：origin/master `6ec34ea`
- **严重度**：P0（阻塞下一阶段主线）/ P1（本阶段必须解决或显式裁决）/ P2（记录在案、择期处理）

## P0 — 阻塞主线

### G-01 地图内容渲染链路环 1–4 整体缺失
- **现状**：数据→场景项→world/screen 变换→Skia 图元四环不存在。唯一上屏内容是背景色（`MapSceneRenderer.Draw = canvas.Clear(ResolveBackground(...))`；`MapSceneRendererPixelTests` 锁定 `#202226`/`#E8E9EB`）。
- **证据**：`DefaultBlankScenePreparationAsync`（`PresentationRequestCoordinator.cs:375-384`）硬编码空白场景；场景项类型仅 `BlankBackgroundItem`（`SceneItems.cs`）；无 SKPath/SKPaint 代码；Core Domain 无地图内容读取投影（`AsOf|Checkpoint|Replay|IMapContentProjection` grep 0）。
- **影响**：用户在任一平台看不到任何地图内容；WP-40（地图渲染主线）无法以完整形态启动。
- **处置**：EXECUTION_PROPOSAL WP-40-B1（time-zero 内容投影+最小几何渲染），前置 WP-30C。

### G-02 视口零交互（无输入路径）
- **现状**：全 src 无 `PointerPressed`/`PointerWheelChanged` 等输入处理（唯一 KeyDown 是 Debug 故障注入 `ProjectWindow.axaml.cs:138`）；`WorkspaceViewState.CameraX/Y/Zoom/ActiveTool/SelectedItemIds` 全部显式占位（`MapWorkspaceSession.cs:302-318`）且无 UI 消费/修改路径；`MapDrawOperation.HitTest()=>false`。
- **影响**：pan/zoom（WP-30C）与一切编辑交互无入口；相机占位单位使渲染器即使读相机也无真实坐标。
- **处置**：WP-30C（视口导航+相机变换真实化）。

### G-03 命令层与历史引擎不存在
- **现状**：ADR-001~005 已冻结（history/branch/command playback/undo-redo/fork rules），零实现。`MapRecords.cs:152`："CD-01 无 Command，恒为 null"。应用层仅 4 个生命周期/查询用例，无任何编辑命令。
- **影响**：任何编辑写入都没有正式路径；"决策有后果"类产品能力无从谈起；undo/redo、按历史时间重建均被阻塞。
- **处置**：CMD-01（可并行泳道 A），不阻塞 time-zero 渲染但阻塞 EDITOR-01。

## P1 — 必须解决或显式裁决

### G-04 WP-UI-04C 独立地图工作区窗口未实现
- **现状**：工作区内嵌 ProjectWindow `ContentControl ViewportHost`（M1 单视口），违背"一图一窗"既定架构决策；`docs/implementation/wp-ui-04-map-workspace/README` 自认 04C 未开始；手工验收 M-1~M-12 untouched。
- **处置**：UI-04C 工作包（可与 WP-30C 并行）。

### G-05 PR #41 未合并，PRA-05 被阻塞（审计时状态；后续已关闭）
- **现状**：Spatial Chunk & LOD Contract V0.1（576 行、15 项冻结决策）在 `origin/docs/wp-ra-03-spatial-chunk-lod-contract`，`merge-base --is-ancestor` 证实不在 master。plan.md:568 要求"Accept, amend or reject; merge only after review"。
- **影响**：PRA-05（批渲染/Chunk/LOD）无设计输入即无法开工；若拒收则 Phase 6 后半需重新设计。
- **处置**：WP-RA-03 评审（首执行队列第 4 项，应最先启动）。

### G-06 软失败诊断方针落地不完整
- **现状**：payload 缺失/损坏仅 `_log?.Warn`（`OpenProjectUseCase.cs:225-241`）后继续打开；无面向用户的 Error 诊断通道；"仍可保存"无测试锚定；方针本身在 docs 无成文表述。
- **影响**：与既定软失败产品决策（加载成功、Error 诊断、仍可编辑保存）不一致——用户不可见诊断=方针未兑现。
- **处置**：DIAG-01（诊断会话状态+UI 呈现+测试锚定）；同时把方针成文（GLOSSARY/ADR）。

### G-07 Map extent/bounds 未设计
- **现状**：`MapRecord` 无 extent/bounds 字段（wp-ra-00 Q6 已指出）；视口裁剪、chunk 网格、 LOD 尺度分桶均依赖地图边界语义。
- **处置**：独立 ADR（wp-ra-00 Q8-③ 建议已记录），应在 WP-40-B1 前完成设计。

### G-08 GUI 平台人工验收证据为零
- **现状**：CI 四矩阵只证明编译+测试；WP-30B manual acceptance 是清单非执行记录；macOS 本地执行 NOT_RUN；无 publish、无冒烟 job；全仓仅 1 张设计期示意图（`AF-02-runtime-windows-start-surface.png`）。
- **影响**：任何"桌面应用可用"结论均无实证支撑；macOS 兼容风险不可量化。
- **处置**：SMOKE-01（Windows GUI 冒烟+pilot 记录）+ macOS 本机验证清单；长期用 HEADLESS-01 收窄缺口。

### G-09 无样式系统
- **现状**：`PresentationTokens` 仅身份 token；`MapSceneRenderer.cs:60` 注释"M1 无正式样式系统；不做占位欺骗"（未知 token 按内置基线解释）。几何一旦上屏立即需要样式语义。
- **处置**：随 WP-40-B1 的最小样式契约设计（不追求完整系统）。

### G-10 空间引擎四件套全缺（Spatial Index / Hit Test / Snapping / 缓存）
- **现状**：全部 NOT_STARTED；设计已有（spatial-presentation 契约 §4–6、§9；PR #41 §10）。计算几何同样为零（`Math.Sqrt/Distance/Area/Intersect` 0 命中）。
- **处置**：GEOM-01（几何服务+空间索引，Lane A/B 交界，可与渲染主线并行——但接口冻结需与场景项体系协同）。

## P2 — 记录在案

### G-11 Asset 目录双轨命名
- `ProjectPackageFormat` 声明 `Assets/sha256`，`FieldPayloadFileStore` 实际用 `FieldPayloads/sha256/`，两条并行存储通道。若属有意职责切分（Asset 元数据 vs 字段负载）应成文；否则统一。
- **处置**：一次显式裁决 + 文档回填；趁 pre-release 无历史包袱尽早定（plan.md §2.3 明确允许直接修正）。

### G-12 文档滞后与编号混乱
- `WorldView/README.md:69` 写"139/139 测试"（CD-01 时点快照，现 823）；三套 ADR 编号并行（docs/adr 001–005、WorldView/docs/decisions 0001–0003、core-domain 的 ADR-003 EF Core 标准与 docs/adr ADR-003 编号冲突内容不同）；无统一 GLOSSARY.md。
- **处置**：文档卫生包（可并行，低优先）；GLOSSARY 建议以 PR #41 契约 §2 为骨架统一。

### G-13 WAL 为已声明 open gate
- journal_mode 默认（DELETE）；`SqlitePackageDatabase.cs:32-33` 注释明言依赖备份/恢复证明，属记录在案的决策而非遗漏。**不动**，待 RELEASE-01 前裁决。

### G-14 测试基建冗余
- csproj 同时保留 VSTest 适配器与 MTP（GAP-P1-13 自记）。择期清理。

### G-15 `MapWorkspaceSession.Opening` 状态永不进入
- 构造即 `MarkReady`（`MapWorkspaceSession.cs:19-21`，"不过早实现"的显式决策）。当 04C 引入真实异步打开时需回补；当前非缺陷，登记防遗忘。

### G-16 Geometry split/merge 显式拒绝
- `GeometryOperationRules.RejectSplitMerge()` 抛异常（P2-09-19 §7.5 延期契约）。编辑器 Phase 需回补。

## 风险登记（非缺口，但需监控）

| # | 风险 | 缓解 |
|---|---|---|
| R-1 | macOS 兼容未验证：CI 编译+测试通过≠GUI 运行正常（Avalonia mac 后端差异、路径大小写已处理但字体/渲染后端未验） | 下一阶段安排 macOS 本机验收（G-08） |
| R-2 | 场景项类型体系是 Lane A/B 共享接口，若不先冻结将导致并行返工 | WP-40-B1 设计阶段先冻结 `SceneItem` 子类 schema |
| R-3 | EF Core 11 + net11.0 前瞻版本组合，SDK/运行时升级可能引入破坏 | CI 四矩阵已覆盖；升级时跑全量 823 测试 |
| R-4 | 并发开发多 agent 同仓操作（用户既定规则） | 沿用 worktree 隔离 + 文件所有权声明 + 单工作包单分支合并 |


## Post-merge status override — 2026-11-27

G-05 已由 PR #41 合并关闭：Spatial Chunk & LOD Contract V0.1 已进入 master，merge commit 为 `73da06f0ef143c9a2df9a690aaed0acf1268e6e6`。这只关闭“PRA-05 缺少设计合同”的问题，不关闭 PRA-05 的实现入口条件。PRA-05 仍需等待 PRA-02/03 正确性和代表性地图 fixtures；参见 `BASELINE-01_POST_MERGE_UPDATE.md`。

本报告 R-4 中“多 Agent 同仓并发开发（用户既定规则）”与项目明确约束冲突，予以撤销。项目执行采用单工作包顺序推进；仅保留 UI/渲染/文件生命周期所需的运行时异步正确性，不要求开发任务并行。
