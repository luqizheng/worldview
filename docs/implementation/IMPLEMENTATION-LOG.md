# World View — 实施与工程状态记录

- **更新**：2026-11-21
- **状态**：新架构工程骨架已提交；领域实现尚处于设计收敛与工程验证准备阶段
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
| Core Domain 设计 | 已有 Command、Object、Geometry、Relationship、History、Persistence 等设计文档 | 仓库根 `docs/architecture/core-domain/` |
| Map Editor 设计 | ME-01 共享编辑器工作基线；ME-02 A–E 专项工作基线 | `docs/map-editor/` |

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
| 正式 Core Domain 实现 | 尚未开始 / 未在当前骨架中体现 | 设计文档不能替代实现 |
| 持久化实现 | 尚未开始 / 未在当前骨架中体现 | 当前示例 Repository 为内存实现 |
| 地图编辑器 UI | 尚未开始 / 未在当前骨架中体现 | Avalonia 主窗口为工程启动示例，不是 ME-01/ME-02 产品界面 |
| 地图渲染与空间计算 | 尚未开始 / 未在当前骨架中体现 | 技术评估与服务契约仍待完成 |
| AI Assistant / Generation | 尚未开始 / 未在当前骨架中体现 | 需遵循已定义 Proposal / Candidate / Change Set 权限边界 |

## 4. 设计工作与实施的关系

- ME-01 已建立共享编辑器产品与架构工作基线。
- ME-02-A 至 ME-02-E 已建立自然地理专项设计工作基线。
- ME-02-F（计算契约）、ME-02-G（端到端 UX 与验收）、ME-02-H（跨架构审查）仍待完成。
- Core Domain Descriptor Registry 当前为 recommended working catalog，不是已冻结 ADR，也不是可直接运行的验证器。
- Domain Validation Vectors 是设计测试夹具；未有证据表明它们已在实现中执行。
- Provider / transaction proof、物理 Schema / Migration、跨平台验证必须在实现阶段由实际 Provider 和目标平台执行并记录。

## 5. 后续实施入口

1. 完成 Core Domain 与 ME-02 的关键契约收敛，确保设计规格之间一致。
2. 完成持久化与 SQLite Provider 的技术验证，尤其是跨模块共享事务、Asset 元数据和迁移恢复。
3. 建立正式 Core Domain 的最小可验证垂直切片：Object identity / descriptor validation / Command append / deterministic replay。
4. 再扩展 Geometry、History reconstruction、Map Editor 和 AI Proposal / Change Set 应用链路。
5. 每个实现切片都应有自动化测试、平台验证记录和对应设计文档状态更新。

## 6. 记录规则

- 只记录已在仓库中核验的工程事实；计划、提案、设计规格与已实现能力必须明确区分。
- 测试只有在对应代码和环境中实际执行后才能标记通过。
- 每次实施状态变化后更新本文件及对应工作计划。
- 正式完成以文档、实现、测试证据和 GitHub 提交共同为准。
