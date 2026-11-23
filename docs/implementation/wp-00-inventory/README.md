# WP-00 — Whole-product Implementation Inventory（审计摘要）

| 项 | 值 |
|---|---|
| 审计日期 | 2026-11-23 |
| 仓库 | `github.com/luqizheng/worldview`（本地 `C:\projects\world-view`） |
| 分支 | `master` |
| HEAD SHA | `30527eb31d73452703b1bd53c22cea5cce112aa9` |
| 工作区状态 | 除本审计新增目录 `docs/implementation/wp-00-inventory/` 与上一轮盘点的未跟踪文件 `docs/map-editor/implementation/`（用户文件，未触碰）外干净 |
| 审计方式 | 只读：5 路并行源码取证（Desktop / Project-Map 管理 / Core 基础设施补充 / Map Rendering / Map Authoring+Tests-CI）+ 本机实测 build/test + GitHub Actions 核验 |
| 业务代码改动 | **无**。未修改任何 src/tests/Schema/Migration/CI/正式设计文档，未新增 Migration，未提交，未建 PR |
| 机读文件 | `whole-product-capability-inventory.json`（同目录，经标准 JSON Parser 校验） |
| 修订 | **2026-11-24**：WP-20a Map 创建切片完成（PR #26，`feat/wp20a-map-creation` @ `50e3d3e`）后修订相关结论——GAP-P0-2 已解决（移入 `resolved_gaps`）、M1 验收 3 转 `IMPLEMENTED_VERIFIED`、m1_blockers 移除 P0-2、测试基线 700→728（本机 Debug 实测 + CI run 45211773969 四 job 全绿）。M1 总判定仍 NOT_MET；其余结论仍以 2026-11-23 审计为准 |
| 前序盘点 | `docs/map-editor/implementation/wp-00-capability-inventory.{md,json}`（基线 `3fd1815`，未提交）。`3fd1815..30527eb` 仅含 2 个 docs 提交，**代码与当前 HEAD 完全一致**；本报告为全产品口径的完整替代版（渲染/制作两子系统分列、M1 专项分析、7 文件交付） |

---

## 总体结论

一句话：**这是一个拥有 fail-closed 数据内核与 Field Package 交换链、但没有一行渲染代码、几乎没有编辑交互的系统。** 距离 M1（Basic Map Application）的主要差距不在数据层，而在「用户可达性」与「渲染」：Map 创建已由 WP-20a 接通（PR #26），地图视口完全不存在、Pan/Zoom 是零消费者占位属性。

四大区域的真实状态：

| 区域 | 状态 | 摘要 |
|---|---|---|
| Desktop Application | **基本可用**（最小双文本窗口） | 启动/组合根/Project 全生命周期/最近项目/文件选择器/Map 创建对话框已实现且有测试；菜单仅 1 项、无工具栏；打包发布链完全缺失；进程级异常只写 stderr（WinExe 下不可观测） |
| Project / Map Management | **Project 侧成熟；Map 侧主链已通** | Project 创建/打开（五道闸）/关闭/会话/最近项目全部实现有测试；**Map 创建全链已落地（WP-20a，PR #26：UI 对话框+用例+端口+会话绑定适配器→正式 MapService，名称唯一在正式写路径保障）**；Map 列表读取与工作区打开/关闭走通生产链；重命名/复制/删除/Branch 列表/最近 Map 四层全无 |
| Core Infrastructure | **强**（728/728 测试） | 对象/定义/Initial State/Building-Cave 聚合/资产完整性/字段修订/项目包全链正式且测试密集；事务协调器单点无绕过；Command/Checkpoint/Relationship/World-State 重建为纯设计态；除 Map 创建与项目创建播种外，正式写路径几乎全部无用户可达入口 |
| Map Rendering | **零实现** | 21 项能力全部 MISSING/不适用；无视口控件、无 Skia 绘制代码、无 Scene Snapshot 管线、无坐标变换；PRA-01 官方审计文档自判 NOT IMPLEMENTED |
| Map Authoring | **零实现**（领域侧 CAS 与 Building/Cave 采纳链除外） | 19 项中 17 项 MISSING；编辑链路（选择/Inspector/工具/手势/暂存/预览/验证 UI/Candidate/Apply/Undo/操作适配器/结果对账）从零开始 |

---

## M1 完成度

**结论：M1 未达成，且当前代码上无法通过验收。** 正式验收编号以 `docs/map-editor/implementation-roadmap-v1.md` WP-30 的 12 项 M1 acceptance 为唯一依据。12 项中：4 项完成（含 WP-20a 打通的"创建 Map"，验收 3）、3 项部分、5 项缺失或未验证（空白视口、Pan/Zoom、macOS 全流程、渲染不改数据证明、渲染类自动化测试）。

诚实的量化估计（以状态矩阵为准，见 `M1_BASIC_MAP_APPLICATION_GAP_ANALYSIS.md`）：**约 40%–50%——这是区间估计，不是精确完成度指标**；验收判定以逐项状态矩阵为准。数据侧（Project 生命周期、Map 创建与持久化、重启恢复）已就绪；缺的是「渲染管线从 0 到 1」与「工作区产品面/Branch-时间显式显示」两块。

M1 的关键路径（与 roadmap v1 的 WP 序列一致）：

1. ~~WP-10~~ Desktop 补强（异常可观测性、窗口关闭语义、打包发布链）——打包归此 WP，非渲染阻塞项（可与渲染并行）
2. ~~WP-20a~~ **Map 创建切片：已完成**（PR #26，`50e3d3e`，GAP-P0-2 已解决，验收 3 达成）；**WP-20b** Branch/时间上下文显式显示仍待做
3. **WP-30** 最小渲染切片：Presentation Context + Scene Snapshot + Avalonia 视口 + Skia 绘制 + map↔screen 变换 + Pan/Zoom + 空白地图帧——**当前 M1 关键路径首位**

---

## P0 阻塞问题（详见 `P0_P1_GAPS_AND_RECOMMENDATIONS.md`）

| ID | 缺口 | 阻塞 M1 |
|---|---|---|
| P0-1 | Map Rendering 管线整体缺失（视口/Skia/Scene Snapshot/坐标变换/Pan-Zoom 21 项全 MISSING） | 是（验收 6/7；11 与 12 的渲染部分随其存在才可验证） |
| P0-3 | Map 工作区无产品界面：文本窗口、`SwitchContextAsync` 零调用死代码、ViewState 全占位、UI 恒传 null Branch/时间 | 是（验收 5/6） |
| P0-4 | macOS GUI 实际运行证据缺失（仅 CI build+test，无 GUI 冒烟/headless 测试；CI 不能替代 GUI 运行证据） | 是（验收 10；12 的 macOS 侧平台证据） |

~~P0-2（Map 创建无用户入口）~~：**已解决**——WP-20a 完成（PR #26，`50e3d3e`），验收 3 转为 IMPLEMENTED_VERIFIED。

注：打包/发布链缺失（无 pubxml/PublishSingleFile/CI 发布 job/installer）是**性质不同的另一问题**，归 **GAP-P1-15 / WP-10 Desktop application foundation**（桌面交付与发布准备），不构成 M1 的独立阻塞项——M1 平台门是 GUI 流程证据，不是 installer。

---

## 建议下一步

1. **不要开始任何渲染实现前先做技术决策**——产品负责人审阅本盘点确认 M1 范围后，第一个实施任务为 **WP-30 渲染切片**（原建议的 WP-20a Map 创建切片已完成：PR #26，`50e3d3e`，架构完全按本文件原第 2 条的依赖方向落地并已验证）。开工前先落两份技术决策：Avalonia 自绘控件 vs `CustomDrawOperation` 的框架矩阵；Scene Snapshot 契约以 PRA-01 v0.1 为基线的最小子集。
2. WP-20 收尾：**WP-20b** Branch/时间上下文显式显示（roadmap WP-20 exit criteria）。
3. 并行小项（不阻塞主线，归 WP-10）：进程异常钩子接入 `FileAppLog`；`JsonRecentProjectsStore` 补测试；迁移防降级守卫补测试；打包发布链（GAP-P1-15）。

---

## 审计产物清单

| 文件 | 内容 |
|---|---|
| `README.md` | 本摘要 |
| `WHOLE_PRODUCT_CAPABILITY_INVENTORY.md` | 九章全能力盘点（Desktop / 项目地图管理 / Core 基础设施 / 渲染 / 制作 / 测试 CI / 平台证据 / 安全完整性 / 文档一致性） |
| `whole-product-capability-inventory.json` | 机读盘点（schema_version 1.0，118 capabilities + gaps/resolved_gaps + platform evidence + milestone assessment，经标准 JSON Parser 校验） |
| `M1_BASIC_MAP_APPLICATION_GAP_ANALYSIS.md` | M1 十二项验收逐条分析 |
| `IMPLEMENTATION_DEPENDENCY_GRAPH.md` | Mermaid 依赖图（M1 必需 vs 未来依赖） |
| `P0_P1_GAPS_AND_RECOMMENDATIONS.md` | P0/P1/P2/Deferred 缺口与建议 |
| `TEST_AND_PLATFORM_EVIDENCE.md` | 本机实测 + CI + 平台证据（未执行的项标 NOT_RUN） |
