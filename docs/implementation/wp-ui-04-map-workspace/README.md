# WP-UI-04：Map Workspace 独立地图工作区

## 1. 项目目标

为 world-view 桌面应用建立**独立的地图工作区窗口**架构：每张地图可在独立窗口中打开为
一个 Map Workspace，作为未来 Map Editor 的稳定宿主。本阶段（WP-UI-04A）只做代码审查、
架构设计与文档交付，不实现业务代码。

- 仓库：<https://github.com/luqizheng/worldview>
- 基线：master `8bdbad1906f7b0c5f3d573b65fc1076867452943`（merge PR #34）
- 工作分支：`feat/wp-ui-04-map-workspace`
- 代码位置：`WorldView/src/`（Application / Desktop / Domain / Infrastructure / Presentation）

## 2. 当前状态

| 项 | 状态 |
| --- | --- |
| PR #34 合并确认 | ✅ `8bdbad1` 已在 master 历史中（本阶段基线即最新 master） |
| 代码审查 | ✅ 完成，见 `architecture.md` §2 与 §10 |
| 目标架构 / 窗口管理 / 生命周期状态机 / UI / 实施计划 / 验收 / 开放问题 | ✅ 本目录八份文档 |
| 业务代码实现（04A） | ⛔ 本阶段禁止（已遵守） |
| **WP-UI-04B Session 层实现** | ✅ 完成（PR #36：`feat/wp-ui-04b-map-session-lifecycle`；B1-B5 全部落地，895/895；R1 最终修正 P2-01/P2-02 已落地，全量 900/900） |
| WP-UI-04C 窗口层实现 | ⛔ 未开始 |

## 3. 核心决策（已确认，本阶段不再重议）

1. **一个地图一个工作区窗口**：同一地图重复打开 → 激活已有窗口，不重复建 Session。
2. **不同地图可同时打开**；**不同项目的地图也可同时打开**。
   工作区唯一身份 = `(ProjectId, MapId)`，不允许用显示名或 GUID 局部值判断。
3. **关闭地图工作区不关闭项目**；Session 释放失败时窗口与 Session 保留、可重试；
   重复关闭幂等；已关闭窗口不得再被激活。
4. **关闭项目必须关闭全部地图工作区**：所有子工作区成功释放后项目才进入 `Closed`；
   失败时项目不得假装关闭成功；不得遗留活动 Session、窗口注册或 UI/Session 分叉。
5. **Map Workspace ≠ Map Editor**：Workspace 是窗口与地图级会话；Editor 是未来承载在
   Workspace 中的编辑能力。本阶段不开发地图绘制。
6. **先释放成功，后解除注册**：任何情况下不允许"先从注册表/列表移除，再尝试释放资源"。

## 4. 文档索引

| 文档 | 内容 |
| --- | --- |
| [architecture.md](architecture.md) | 组件架构、职责边界、十项必答问题逐项回答 |
| [lifecycle-state-machine.md](lifecycle-state-machine.md) | MapWorkspaceSession 生命周期状态机（Mermaid） |
| [window-management.md](window-management.md) | 窗口创建、激活、去重、关闭与异常处理 |
| [ui-structure.md](ui-structure.md) | 第一版 MapWorkspaceWindow UI 结构与交互 |
| [implementation-plan.md](implementation-plan.md) | WP-UI-04B / 04C / 04D 分阶段实施计划 |
| [acceptance-criteria.md](acceptance-criteria.md) | 自动化测试设计 + Windows/macOS 人工验收 |
| [open-questions.md](open-questions.md) | 未解决问题、技术风险与待决策事项 |

相关既有文档：

- `WorldView/docs/architecture/project-session-close-lifecycle.md`（WP-UI-02，PR #34 的
  Project Session 关闭状态机——本阶段直接复用其语义）
- `docs/implementation/wp-ui-03-project-workspace-audit/`（PR #34 合并前的项目工作区审计；
  其 P1 发现"地图工作区没有 UI 终点"正是 WP-UI-04 的直接动因）

## 5. 后续阶段

- **WP-UI-04B**：✅ 已完成——Session 层增强（生命周期状态机、地图显示名、同图去重、关闭原子化）
  + 单元测试；实现细节与设计差异见 `lifecycle-state-machine.md` §7 与 `implementation-plan.md`。
  R1 最终修正（关闭取消语义、释放接缝一致性）已落地，最终全量测试 900/900（04B 基线 895 + R1 新增 5）。
- **WP-UI-04C**：窗口层（MapWorkspaceViewModel / MapWorkspaceWindow / 窗口注册表 / 打开入口改造）。
- **WP-UI-04D**：项目关闭与窗口联动、诊断打磨、验收执行。
- 更远：Map Editor 宿主接入（见 `architecture.md` §10-Q10），地图绘制能力另立工作包。
