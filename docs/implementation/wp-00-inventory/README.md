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
| 前序盘点 | `docs/map-editor/implementation/wp-00-capability-inventory.{md,json}`（基线 `3fd1815`，未提交）。`3fd1815..30527eb` 仅含 2 个 docs 提交，**代码与当前 HEAD 完全一致**；本报告为全产品口径的完整替代版（渲染/制作两子系统分列、M1 专项分析、7 文件交付） |

---

## 总体结论

一句话：**这是一个拥有 fail-closed 数据内核与 Field Package 交换链、但没有一行渲染代码、没有一个编辑交互的系统。** 距离 M1（Basic Map Application）的主要差距不在数据层，而在「用户可达性」：Map 创建没有用例层与 UI 入口、地图视口完全不存在、Pan/Zoom 是零消费者占位属性。

四大区域的真实状态：

| 区域 | 状态 | 摘要 |
|---|---|---|
| Desktop Application | **基本可用**（最小双文本窗口） | 启动/组合根/Project 全生命周期/最近项目/文件选择器已实现且有测试；菜单仅 1 项、无工具栏；打包发布链完全缺失；进程级异常只写 stderr（WinExe 下不可观测） |
| Project / Map Management | **Project 侧成熟；Map 侧半通** | Project 创建/打开（五道闸）/关闭/会话/最近项目全部实现有测试；Map 列表读取与工作区打开/关闭走通生产链；**Map 创建停在领域层（无用例层、无 UI）**；重命名/复制/删除/Branch 列表/最近 Map 四层全无 |
| Core Infrastructure | **强**（700/700 测试） | 对象/定义/Initial State/Building-Cave 聚合/资产完整性/字段修订/项目包全链正式且测试密集；事务协调器单点无绕过；Command/Checkpoint/Relationship/World-State 重建为纯设计态；生产写路径几乎全部无用户可达入口 |
| Map Rendering | **零实现** | 21 项能力全部 MISSING/不适用；无视口控件、无 Skia 绘制代码、无 Scene Snapshot 管线、无坐标变换；PRA-01 官方审计文档自判 NOT IMPLEMENTED |
| Map Authoring | **零实现**（领域侧 CAS 与 Building/Cave 采纳链除外） | 19 项中 17 项 MISSING；编辑链路（选择/Inspector/工具/手势/暂存/预览/验证 UI/Candidate/Apply/Undo/操作适配器/结果对账）从零开始 |

---

## M1 完成度

**结论：M1 未达成，且当前代码上无法通过验收。** 12 项验收中：4 项完成、3 项部分、2 项缺失（空白视口、Pan/Zoom）、3 项验证门未过（Windows 全流程未演练、macOS 仅 CI build+test 无 GUI 证据）。

诚实的量化估计（以状态矩阵为准，见 `M1_BASIC_MAP_APPLICATION_GAP_ANALYSIS.md`）：**约 35%–45%**。数据侧（Project 生命周期、Map 持久化、重启恢复）已就绪；缺的是「渲染管线从 0 到 1」与「Map 创建的产品接线」两块。

M1 的关键路径（与 roadmap v1 的 WP 序列一致）：

1. **WP-10** Desktop 补强（异常可观测性、窗口关闭语义、打包）——非渲染阻塞项
2. **WP-20** Map 创建用例层 + UI 入口 + Branch/时间上下文接线
3. **WP-30** 最小渲染切片：Presentation Context + Scene Snapshot + Avalonia 视口 + Skia 绘制 + map↔screen 变换 + Pan/Zoom + 空白地图帧

---

## P0 阻塞问题（详见 `P0_P1_GAPS_AND_RECOMMENDATIONS.md`）

| ID | 缺口 | 阻塞 M1 |
|---|---|---|
| P0-1 | Map Rendering 管线整体缺失（视口/Skia/Scene Snapshot/坐标变换/Pan-Zoom 21 项全 MISSING） | 是（验收 6/7/11/12） |
| P0-2 | Map 创建无用户入口：领域服务齐备但 Application 用例层与 UI 均为零 | 是（验收 3） |
| P0-3 | Map 工作区无产品界面：文本窗口、`SwitchContextAsync` 零调用死代码、ViewState 全占位、UI 恒传 null Branch/时间 | 是（验收 5/6） |
| P0-4 | macOS GUI 运行证据为零 + 无打包/发布链（无 pubxml/PublishSingleFile/CI 发布 job/installer） | 是（验收 12；11 的证据强度也受限） |

---

## 建议下一步

1. **不要开始任何渲染实现**——先由产品负责人审阅本盘点，确认 M1 范围与 WP-10/20/30 切片顺序（roadmap v1 §5 已给出骨架）。
2. 第一个实施任务建议为 **WP-20 的 Map 创建切片**（`CreateMapUseCase` + ProjectWindow 入口 + Branch/时间显式选择），它是纯数据侧工作、领域零改动、可测试性最好，且直接解除 P0-2。
3. WP-30 渲染切片开工前，先落两份技术决策：Avalonia 自绘控件 vs `CustomDrawOperation` 的框架矩阵；Scene Snapshot 契约以 PRA-01 v0.1 为基线的最小子集。
4. 并行小项（不阻塞主线）：进程异常钩子接入 `FileAppLog`；`JsonRecentProjectsStore` 补测试；迁移防降级守卫补测试。

---

## 审计产物清单

| 文件 | 内容 |
|---|---|
| `README.md` | 本摘要 |
| `WHOLE_PRODUCT_CAPABILITY_INVENTORY.md` | 九章全能力盘点（Desktop / 项目地图管理 / Core 基础设施 / 渲染 / 制作 / 测试 CI / 平台证据 / 安全完整性 / 文档一致性） |
| `whole-product-capability-inventory.json` | 机读盘点（schema_version 1.0，66 capabilities + gaps + platform evidence + milestone assessment） |
| `M1_BASIC_MAP_APPLICATION_GAP_ANALYSIS.md` | M1 十二项验收逐条分析 |
| `IMPLEMENTATION_DEPENDENCY_GRAPH.md` | Mermaid 依赖图（M1 必需 vs 未来依赖） |
| `P0_P1_GAPS_AND_RECOMMENDATIONS.md` | P0/P1/P2/Deferred 缺口与建议 |
| `TEST_AND_PLATFORM_EVIDENCE.md` | 本机实测 + CI + 平台证据（未执行的项标 NOT_RUN） |
