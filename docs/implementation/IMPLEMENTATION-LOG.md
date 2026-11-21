# World View — 实施与工程状态记录

- **更新**：2026-11-21
- **状态**：新架构工程骨架已提交；ME-01 / ME-02 设计包已建立；正式领域实现与若干关键技术契约仍待实施
- **当前 Map Editor 工作计划**：ME-01-work-plan-and-document-status-v0.9.md
- **权威工程目录**：`WorldView/`
- **权威项目文档目录**：仓库根 `docs/`

> 本文件记录当前仓库可核验的工程事实。它取代 2026-11-20 的“技术栈未定 / 实施暂停 / 仓库零代码”状态描述；旧记录仅作为历史背景保留在 Git 历史中。

## 1. 当前已提交工程基线

| 项目 | 当前状态 | 依据 / 边界 |
|---|---|---|
| 主语言与目标框架 | C# / .NET 11 / `net11.0` | `WorldView/docs/architecture/initial-tech-baseline.md` |
| 桌面 UI | Avalonia UI 12.1.6，Windows + macOS 目标 | 同上；不代表两个平台均已完成发布验证 |
| UI 组织 | MVVM；CommunityToolkit.Mvvm | 工程基线 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 工程基线 |
| 工程结构 | Desktop / Application / Domain / Infrastructure + 三个测试项目 | `WorldView/WorldView.sln` |
| 工程脚本 | Windows PowerShell 与 Unix shell 构建/运行/测试脚本 | `WorldView/scripts/` |
| 示例垂直切片 | MapProject 创建、内存 Repository、基础测试 | 仅用于验证工程分层，不是正式地图领域模型 |
| Core Domain 设计 | Command、Object、Geometry、Relationship、History、Persistence 与 Descriptor 设计包 | `docs/architecture/core-domain/` |
| Map Editor 设计 | ME-01 共享编辑器基线；ME-02-A 至 ME-02-H 自然地理完整设计包 | `docs/map-editor/` |

工程初始化提交及其验证记录见：
- `WorldView/docs/decisions/ADR-0001-project-bootstrap.md`
- `WorldView/docs/development/build-and-test.md`
- `WorldView/docs/development/dev-environment.md`

ADR-0001 记录初始化阶段的构建与测试结果。该历史记录不应被解释为当前 HEAD 已在所有目标操作系统上重新执行验证。

## 2. 已确定与尚未确定的技术边界

### 已确定
- C# / .NET 11、Avalonia UI、MVVM、DI 与分层 Solution。
- Windows 与 macOS 是桌面目标平台。
- Domain 不依赖 UI 或 Infrastructure；Application 依赖 Domain；Infrastructure 实现 Application 定义的接口。
- 新架构工程代码位于 `WorldView/`；仓库根 `docs/` 是项目设计文档权威位置。
- Map Editor 使用统一 Canvas-centered editor shell；Map Engine / Spatial Services 负责确定性空间计算，Core Domain 拥有正式地图事实与历史。

### 尚未确定 / 仍需专项评估
- 本地数据库与项目文件格式的最终物理方案及迁移实现。
- 地图 2D / 3D 渲染技术。
- Geometry / GIS 计算引擎与空间索引。
- 栅格/物理场 Asset 的实际编码、分块、缓存与读写方案。
- AI 模型接入方式、Agent Runtime。
- Task Planning & Execution Engine 的具体实现。
- Windows / macOS 的完整打包、安装、升级、备份恢复和性能验证。

这些事项应按 `WorldView/docs/roadmap/tech-assessment-backlog.md` 与对应设计文档推进，不得因工程骨架已存在而默认视为已选型。

## 3. 当前实现成熟度

| 层次 | 状态 | 说明 |
|---|---|---|
| 工程骨架 | 已提交 | Solution、项目分层、DI/MVVM 示例与测试项目 |
| 正式 Core Domain 实现 | 尚未在当前工程骨架中体现 | 领域设计文档不能替代实现 |
| 持久化实现 | 尚未在当前工程骨架中体现 | 当前示例 Repository 为内存实现 |
| 地图编辑器 UI | 尚未在当前工程骨架中体现 | Avalonia 主窗口为工程启动示例，不是 ME-01/ME-02 产品界面 |
| 地图渲染与空间计算 | 尚未在当前工程骨架中体现 | 技术评估与服务契约仍待完成 |
| AI Assistant / Generation | 尚未在当前工程骨架中体现 | 需遵循 Proposal / Candidate / Change Set 权限边界 |

## 4. 当前设计包状态

### Core Domain Descriptor Closure
- P2-09-26 Object Type Registry V0.2：10 个 Object Type，推荐目录，非冻结 ADR。
- P2-09-27 Relationship Type Registry V0.2：5 个 Relationship Type，推荐目录，非冻结 ADR。
- P2-09-28 Validation Vectors V0.2：53 个设计测试向量，尚未对实现执行。
- P2-09-29 Cross-Registry Consistency Contract V0.1：Object / Relationship / Command / History scope 一致性工作契约。
- 这些目录和向量不是可直接运行的验证器；实际验证逻辑归 Core Domain。

### Map Editor
- ME-01 共享编辑器产品与架构工作基线已建立。
- ME-02-A 至 ME-02-H 已完成为产品、领域、UX 和跨架构设计工作基线。
- 当前 ME-02 总体基线为 V0.6，Map Editor 工作计划为 V0.9。
- ME-02-F V0.2 已区分 source Field Dataset、Derived Artifact、Core Asset 和 renderer cache。
- ME-02-G V0.2 已明确 Initial State、History-local、Project-shared 三种正式写入目标。
- ME-02-H 审查发现唯一 P0 跨架构实现门槛：Field Dataset 的正式权威与持久化生命周期尚未定义。

## 5. 关键未关闭门槛

| Gate | Owner | 阻塞范围 |
|---|---|---|
| H-G01 — Field Dataset authority、Initial State/History 语义与 typed Asset ownership | ME-05 + Core Domain | 正式 elevation / land-water source field 持久化与编辑 |
| H-G02 — Derived Artifact namespace/catalog 实现 | Map Engine | 派生栅格/分析输出生产与发布 |
| H-G03 — Calculation Definition / input fingerprint 精确序列化 | Map Engine + Core Domain | 可复现计算与缓存身份 |
| H-G04 — Hydrology/climate influence 与 coverage 声明 | ME-02 specialists + Map Engine | 生产级增量重算 |
| H-G05 — Formal Object/Geometry/Relationship descriptor enablement | Core Domain | 正式专业编辑操作开放 |
| H-G06 — Scene builder 与 active artifact generation 一致性 | Map Engine | 派生图层渲染集成 |
| H-G07 — 正常/异常/恢复场景 Figma 原型 | Product UX | UX 实现准备 |
| H-G08 — Windows/macOS 数值与存储故障注入验证 | Development team | 平台与生产就绪 |

其中 H-G01 是当前唯一 P0 跨架构门槛。ME-02 设计包完成不代表上述实现门槛关闭。

## 6. 后续实施入口

1. ME-05 与 Core Domain 共同定义 Field Dataset/source-field 权威、Map scope、不可变 payload、coordinate context、tiling 和编辑生命周期。
2. Map Engine 完成 Derived Artifact namespace/catalog、Calculation Definition fingerprint 与 SceneSnapshot 消费契约。
3. Core Domain 只启用 payload、验证规则和测试向量已齐备的 Object / Geometry / Relationship descriptors。
4. 建立正式 Core Domain 最小可验证垂直切片：Object identity / descriptor validation / Command append / deterministic replay。
5. 再扩展 Geometry、History reconstruction、Map Editor 和 AI Proposal / Change Set 应用链路。
6. 每个实现切片都应有自动化测试、平台验证记录和对应设计文档状态更新。

## 7. 记录规则

- 只记录已在仓库中核验的工程事实；计划、提案、设计规格与已实现能力必须明确区分。
- 测试只有在对应代码和环境中实际执行后才能标记通过。
- 每次实施状态变化后更新本文件及对应工作计划。
- 正式完成以文档、实现、测试证据和 GitHub 提交共同为准。
