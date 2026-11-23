# P0 / P1 / P2 Gaps and Recommendations

优先级定义：**P0** = 阻止 M1；**P1** = M1 之后近期必须解决；**P2** = 专业地图制作阶段（M3/M4）；**Deferred** = 暂不应做。全部 Gap 均有审计证据（`WHOLE_PRODUCT_CAPABILITY_INVENTORY.md` 对应章节），机读版见 JSON `gaps` 数组。

---

## P0 — 阻止 M1（4 项）

### GAP-P0-1 Map Rendering 管线整体缺失
- **影响范围**：M1 验收 6（空白视口）、7（Pan/Zoom/resize 变换）；11（渲染不改数据）与 12（快照/变换/平台视口自动化测试）在渲染实现前不可验证；RND-01~19 全部 MISSING。
- **证据**：Desktop 3 个 AXAML 无 Canvas/Viewport/自定义控件（`ProjectWindow.axaml:15` 注释自证阶段决策）；全仓无 Skia 标识符、无 SkiaSharp 包；`WorkspaceViewState.CameraX/Y/Zoom` 零消费者（唯一消费是单测 `SessionUseCaseTests.cs:393,397`）；PRA-01 官方审计自判 NOT IMPLEMENTED（验收矩阵 9/10 Not implemented）。
- **依赖**：设计契约已冻结（PRA-01 v0.1 / PRODUCTION_RENDERER_ARCHITECTURE v0.2）；无数据侧阻塞。
- **推荐解决方向**：WP-30 最小切片——Presentation Context + Scene Snapshot 最小子集 + Avalonia 视口宿主 + Skia 绘制 + map↔screen 仿射变换 + Pan/Zoom + 空白帧中性背景（来自正式读投影，不伪造几何）。先落两项技术决策：自绘控件 vs `CustomDrawOperation`；PRA-01 最小子集裁剪清单。
- **阻塞 M1**：是。

### GAP-P0-2 Map 创建无用户入口
- **影响范围**：M1 验收 3；PMM-B6。
- **证据**：领域/持久化/测试三层齐备（`MapBranchServices.cs:12-28`、`MapCreation.cs:25-48`、`SqliteCoreDomainDatabase.cs:194-264`），但 Application/Desktop 对 `MapService` **零调用**（grep 复核）；ProjectWindow 仅三个命令（关闭项目/打开工作区/关闭工作区）。
- **依赖**：无硬阻塞——领域创建服务就绪，仅需接线（外加名称唯一性的最小 Domain 规则）。
- **推荐解决方向（WP-20a 架构要求，依赖方向固定）**：

  ```text
  ProjectWindow / ViewModel
          |
          v
  CreateMapUseCase
          |
          v
  IMapCreationService
          |
          v
  CoreDomainMapCreationService
          |
          v
  MapService
          |
          v
  ICoreDomainDatabase
          |
          v
  SqliteCoreDomainDatabase
          |
          v
  SQLite transaction
  ```

  - Application 层新增 `IMapCreationService` 端口；Infrastructure 层实现 `CoreDomainMapCreationService`（绑定当前 ProjectSession 的 `IPackageDatabase`，调用正式 Domain `MapService`，不复制 Domain 创建规则）。
  - `CreateMapUseCase` 只依赖 Application 抽象，不引用 Infrastructure 或 SQLite；UI 不直接操作 Domain 数据库或执行 SQL。
  - 复用 `CoreDomainProjectCreationService`、`CoreDomainMapQueryService` 的会话绑定适配器模式；经组合根注册，保证 ProjectSession 生命周期正确。
  - ProjectWindow"创建地图"入口 + 创建对话框（成功后刷新列表、不自动打开）；Map 名称唯一性产品规则（同 Project 内唯一、大小写不敏感）由 Domain/正式持久化写路径保障，不只在 ViewModel 检查。
  - **不重做现有 Map Domain 模型，不增加影子 Map 表。**
  - **建议作为第一个实施任务。**
- **阻塞 M1**：是。

### GAP-P0-3 Map 工作区无产品界面 / 上下文接线断裂
- **影响范围**：M1 验收 5/6；PMM-B8/B13/C15/C16/C17。
- **证据**：UI 打开工作区恒传 `branchId:null, historicalTimeInput:null`（`ProjectWorkspaceViewModel.cs:105-107`）；`MapWorkspaceSession.SwitchContextAsync`（`MapWorkspaceSession.cs:49-79`）零调用零测试死代码；`ValidatedWorkspaceContext` 自标"不透明占位"（`IMapQueryService.cs:104-115`）；ViewState 全占位。
- **依赖**：与 GAP-P0-1 同批实施（工作界面 = 文本列表 + 视口）。
- **推荐解决方向**：WP-30 同批——打开工作区时解析并**显示** Branch/时间上下文（WP-20 exit criteria 要求）；M1 阶段不实现 Branch 切换 UI（可用默认根 Branch），`SwitchContextAsync` 要么接线要么显式移除（避免死代码进发布）。
- **阻塞 M1**：是。

### GAP-P0-4 macOS GUI 实际运行证据缺失
- **影响范围**：M1 验收 10（macOS 全流程）；PLT-02。
- **证据**：macOS 仅 CI build+test 绿（run 45183823988 macos-latest Debug/Release）；无任何 macOS GUI 冒烟、无 headless Avalonia UI 测试；roadmap 明文"support is established by evidence on both platforms, not inferred from shared source code"。
- **依赖**：依赖前三个 Gap 有可演示的流程。
- **推荐解决方向**：在 macOS 实机启动桌面应用并完成正式 M1 用户流程（roadmap WP-30 十二项逐项执行），记录实际设备、系统版本、应用版本/commit、执行结果与未通过项。**CI build/test 不能替代 GUI 运行证据。**
- **阻塞 M1**：是。

---

## P1 — M1 之后近期必须解决（15 项）

| ID | 缺口 | 影响范围 | 证据 | 依赖 | 推荐方向 | 阻塞 M1 |
|---|---|---|---|---|---|---|
| GAP-P1-01 | 进程级异常只写 stderr（WinExe 不可观测）；`ApplicationBootstrapper(log: null)`；窗口 async void 无保护 | 可诊断性/交付质量 | `Program.cs:29,34,77`；`StartWindow.axaml.cs:42,57` | 无 | 钩子接 `FileAppLog`；补 UI 线程 handler | 否（建议随 WP-10） |
| GAP-P1-02 | ProjectWindow 点 X 不关会话（句柄占用至停机；重开同包 DuplicateOpen 二开窗口） | 数据一致性 UX | `ProjectWindow.axaml.cs` 无窗口→VM 链路 | 无 | Closing 事件→`CloseAsync` 或明确提示 | 否 |
| GAP-P1-03 | Settings 服务已实现但生产未接线；无设置 UI | 应用偏好（最近 Map 等落点） | `SettingsDocumentService` 未注册 DI | WP-20b 偏好项 | 接线+最小设置面 | 否 |
| GAP-P1-04 | Desktop/UI 测试为零；应用启停无测试 | 回归安全网 | 测试工程不引用 Desktop | WP-30 视口测试共址 | 新建 Desktop 冒烟工程（Avalonia headless） | 否 |
| GAP-P1-05 | `JsonRecentProjectsStore` 零测试 | M1 验收 10 的证据强度 | tests 全域 grep 零命中 | 无 | 补存储单测（读写/截断/降级） | 否（建议同 WP-20a） |
| GAP-P1-06 | EF 迁移防降级守卫（未知迁移版本拒绝打开）无专门测试 | 长期演进安全 | `CoreDomainEfSession.ApplyMigrations` fail-closed 无测试名命中 | 无 | 补负向测试 | 否 |
| GAP-P1-07 | 几何 envelope 结构校验器建成但未接入生产发布链 | 数据完整性一致性 | `GeometryAssetEnvelope.Validate` 生产零调用（src 仅常量引用）；生产走 registry schema+`ValidateGeometryPublicationContext`（不校验 envelope JSON 内容） | 无 | 接入发布事务或在 CD-03 明确降级理由 | 否 |
| GAP-P1-08 | 资产验证用例/孤儿对账生产零调用；Asset 孤儿只读上报无回收 | 资产一致性闭环 | `VerifyAssetIntegrityUseCase`/`FindOrphanAssetIds` 仅测试可达 | 无 | WP-20/30 期间接入启动对账或独立维护路径 | 否 |
| GAP-P1-09 | 应用层读模型缺口：对象/几何/资产/Field 无 Application 投影（M2 渲染真实内容的权威输入缺失） | M2（可读地图查看器） | grep `ReadModel|Projection` 零命中；PRA01-AUD-02 | WP-40 | PRA-03 投影适配器切片 | 否 |
| GAP-P1-10 | Branch 列表端口缺失；最近 Map 恢复缺失 | WP-20 完整 exit criteria | `ICoreDomainDatabase` 无 ListBranches；grep LastMap 零命中 | 无 | `ListBranches` 端口+适配器；last-map 偏好 | 否 |
| GAP-P1-11 | 文档漂移与死代码：ME-01 文件名 v3.19 vs 内容 v3.23；`ICoreDomainDatabase.cs:30`"十内置" vs 12 类型；`StartViewModel` 两个不可达死分支（引用过时 AF-02 文案）；`SwitchContextAsync` 死代码 | 文档可信度/代码卫生 | 见盘点 §9/1.13 | 无 | 随 WP-10/WP-20 清理 | 否 |
| GAP-P1-12 | WAL 未启用 + Backups 目录无写入者（两个已声明 open gate） | 数据安全（单机 V1 可暂缓） | `SqlitePackageDatabase.cs:32-33` 注释 | 备份/恢复证明先行 | 按原 gate 计划执行，勿跳过证明 | 否 |
| GAP-P1-13 | MTP 与 VSTest 双通道冗余配置 | 测试栈演进 | csproj 保留 `Microsoft.NET.Test.Sdk`+`xunit.runner.visualstudio`，global.json 切 MTP | 无 | 择一去冗余（升级窗口处理） | 否 |
| GAP-P1-14 | M2 渲染完整性（Layer/Label/Symbol/LOD/Raster/Cache）全缺 | M2（可读地图查看器） | RND-11,14-18 | GAP-P1-09 + WP-40 序列 | 按 roadmap WP-40 顺序 | 否 |
| GAP-P1-15 | Windows/macOS 打包发布链缺失（无 pubxml/PublishSingleFile/SelfContained/CI 发布 job/installer） | 桌面交付与发布准备（WP-10 Desktop application foundation） | DSK-11 | WP-10 | `dotnet publish` 自包含产物（win-x64 + osx-x64/arm64）+ CI 可选发布 job + 发布产物验证流程 | 否（属 WP-10 交付准备，**不构成 M1 独立阻塞项**；M1 平台门是 GUI 流程证据，见 GAP-P0-4） |

---

## P2 — 专业地图制作阶段（M3/M4）

| ID | 缺口 | 证据 | 依赖 | 推荐方向 |
|---|---|---|---|---|
| GAP-P2-01 | Relationship 层缺失（无实体/表/写路径；descriptor 声明 `core.relationship.establish/end` enabled vs 执行器缺位） | `CoreDomainInitialSql.cs:300` 唯一命中；p2-09-15 v0.5:92-93 | 需先出关系模型 ADR/设计冻结 | M4 连接语义前实施；Road/River 连接按 ME-03 门禁封存 |
| GAP-P2-02 | Command/Event 层缺席（无表/总线/执行器；`CommandCapabilities` 已解析但零消费） | `CoreDomainInitialSql.cs:374`；`ObjectTypeRegistry.cs:107,474-515` | CD-03 规划 | History-local 命令写入按 CD 系列计划 |
| GAP-P2-03 | Checkpoint 零实现 | src+tests 零命中；p2-09-11 v0.10:290-294 表规格 | 依赖 Command 层 | 按 P2-01-D01 第 18 章设计实施 |
| GAP-P2-04 | History 状态重建缺席（只有世系解析） | `WorkspaceContextResolution.cs:34` 显式免责 | 依赖 Command 层 | 与 Checkpoint 同批（重建输入指纹） |
| GAP-P2-05 | 编辑器交互框架全套（Selection/Inspector/Tools/Gesture/TempGeometry/Staging/Preview/Validation UI/Candidate/Apply/Undo/Freshness 闸/操作适配器/结果对账） | AUT-01~15 穷尽 grep；ME-01-06 设计基线存在 | WP-30 视口 + Map Engine 最小切片（命中/变换） | roadmap WP-50；先做"可用性状态 + fail-closed 工具门" |
| GAP-P2-06 | Spatial Site/Group 与 Templates/Modules 零概念 | AUT-17,19 | 决策类 WP（WP-07/WP-09） | 维持决策前不持久化 |
| GAP-P2-07 | Calendar 语义占位 + Initial State typed schema（OD-1）开放 | `CalendarRecord.CalendarType=null` | Registry V2 激活门 | 按 open-gate 计划 |

---

## Deferred — 暂不应该做

| 项 | 理由 |
|---|---|
| 3D 渲染 | roadmap 明确 V1 不做 |
| 通用 GIS 功能（投影库/瓦片服务/要素服务） | 非通用 GIS 定位；投影参数已按 canonical JSON 存储即可 |
| 云同步/多人协作/分布式锁/CRDT | 单机产品约束；现有 CAS+协调器已足够 |
| 几何 split/merge、对象 retire、runtime attribute change | V1 设计内显式拒绝（`RejectSplitMerge` 恒抛）；待正式设计变更 |
| AI 自动制作地图 | M4 之后；AI 直写 Core Domain 被边界表禁止 |
| Timeline 动画渲染（PRA-06） | 等权威 Historical Time/Unit movement 输入存在 |
| Export/layout | 等同一场景语义可稳定渲染到非交互目标 |
| 大规模性能工程（LOD 网格/空间索引/批处理） | M1/M2 数据量不需要；过早引入违反"保持比例"原则 |

---

## 建议的第一批实施任务（供产品负责人裁决）

1. **WP-20a Map 创建切片**（GAP-P0-2）：最小、零阻塞、可测试性最好，直接打通 M1 验收 3（架构方向见 GAP-P0-2 依赖图：`IMapCreationService` 端口 → `CoreDomainMapCreationService` → 正式 `MapService`）。
2. **WP-10 小项包**（GAP-P1-01/02/05/11 + GAP-P1-15 打包发布链）：全部小改动，随批清理死代码；打包/发布产物归 WP-10。
3. **WP-30 技术决策 + 渲染切片**（GAP-P0-1/P0-3）：先两页决策文档，再实施。
4. **平台门清单**（GAP-P0-4 macOS GUI 冒烟）：双平台冒烟清单模板 + macOS 实机执行留证，可与 1-3 并行准备。
