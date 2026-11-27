# AGENTS.md — World View 新架构工程规范

本文件是所有 Coding Agent 在本工程中工作的共同规范。修改本文件必须经用户明确批准。

## A. 项目定位

World View 是 Windows 和 macOS 双平台的**专业地图编辑与 AI 辅助制图桌面应用**。

- 技术栈：C# + Avalonia UI，.NET 11 LTS（net11.0）。
- 应用形态：单机桌面应用，模块化单体架构。
- 仓库布局：`WorldView/` 是新架构工程的根。仓库根 `docs/` 是正式历史文档（含已冻结的
  ADR-001~005、架构基线、域模型讨论记录），不在本工程范围内、未经用户批准不得修改。
  （历史注：根 `server/` Python 草稿已于 2026-11-20 经用户决定删除，其设计结论以
  仓库根 `docs/implementation/` 与 `docs/P2-01-D01/` 为准。）

## B. 技术原则

- C# + Avalonia；单机优先。
- 模块化单体架构；简单、清晰、可维护。
- 不为未来可能的分布式场景提前设计。
- 不引入没有实际必要的基础设施依赖（地图渲染、几何计算、空间数据、本地数据库、AI 运行时等
  领域的选型均未冻结，见 `docs/roadmap/tech-assessment-backlog.md`，不得提前引入）。

## C. 架构边界

| 项目 | 职责 |
| --- | --- |
| `WorldView.Desktop` | Avalonia UI、应用入口、Window/View、ViewModel、桌面平台适配 |
| `WorldView.Application` | 应用服务、用例、流程协调、接口定义、DTO |
| `WorldView.Domain` | 核心领域模型、领域规则、领域服务 |
| `WorldView.Infrastructure` | 文件系统访问、数据存储适配、外部组件适配 |

- 依赖方向必须保持：`Desktop → Application → Domain`；`Infrastructure → Application`；
  `Desktop → Infrastructure`（仅限 Program.cs 组合根用于依赖注册）。
- Domain 不得依赖 UI 或桌面平台 API（当前 Domain 无任何包/项目引用，保持为零依赖）。
- Application 不得直接绑定 Avalonia（不得引用 Avalonia 包）。
- UI / ViewModel 只能通过 Application 层接口调用用例，不得绕过应用服务直接操作领域模型或仓储。
- AI Agent 不得直接绕过 Domain Services 修改正式地图数据。
- 正式地图修改必须遵循后续确定的 Validation 和 Change Set 机制（机制未定前不实现正式修改链路）。

## D. 跨平台要求

- 所有正式功能必须考虑 Windows 和 macOS 双平台。
- 不得将 Windows 专有 API 写入通用业务层。
- 文件路径必须使用跨平台 API（`System.IO` / `Path` 抽象），不得手写分隔符拼接。
- 不得假定两个平台的文件系统行为完全相同（大小写、保留字符、换行、权限语义）。
- 平台特定代码必须隔离（条件编译或独立的平台适配类型），并注释说明适用平台。

## E. 代码规范

- Nullable 引用类型已由 `Directory.Build.props` 全局开启，不得关闭。
- 保持异步操作正确性（异步链路不阻塞等待、不 async void）。
- 避免无意义的静态全局状态（组合根例外：`App.Services` 仅限启动期使用）。
- 合理使用 DI：类型通过构造函数接收接口，依赖在组合根注册。
- 避免过度设计；不为单一实现提前抽象。
- 优先保证代码可测试性；用例测试不依赖真实文件系统与 UI。
- 不允许通过吞掉异常掩盖错误；领域拒绝输入属业务分支时才允许转结果对象返回。

## F. 测试规范

- Domain 核心逻辑必须有单元测试。
- Application 用例必须有相应测试（仓储用测试替身）。
- 关键基础设施必须有测试。
- 重要功能需要考虑 Windows 和 macOS 差异。
- 不得删除失败测试来使构建通过。
- 运行方式：`scripts/test.ps1`（Windows）或 `scripts/test.sh`（macOS/Linux）。

## G. Agent 工作规范

- 修改前先阅读相关架构文档（阅读顺序）：
  1. 仓库根 `docs/adr/`（ADR-001~005，均已冻结，禁 Merge、Branch 语义等裁决不得违反）；
  2. 仓库根 `docs/architecture-baseline-v1.0.md`；
  3. 仓库根 `docs/P2-01-D01/`（域模型讨论）；
  4. `WorldView/docs/`（本工程文档）。
- 不得擅自改变已经确认的架构决策；与冻结决策冲突时必须提出新 ADR 由用户裁决。
- 不确定的重大技术选型必须先记录问题；不得把未确认的方案写成正式架构。
- 完成任务必须执行构建和测试：`scripts/build.ps1|sh` + `scripts/test.ps1|sh`。
- 交付必须列出修改文件、测试结果和遗留问题。
- 不得擅自提交或推送 Git；不得执行破坏性操作。
- 多会话并发规则（2026-11-25 用户追加）：若已有另一个 session（Agent 会话）正在本仓库
  执行任务，当前 session 不得直接在原工作目录操作，必须先创建独立的 git worktree
  （`git worktree add ../worldview-wt-<任务名> <branch>`）在隔离目录内工作，
  避免并发修改冲突与构建产物互相污染；合并回主工作目录前须经用户裁决。
- 环境注意：本机 Windows Smart App Control 已于 2026-11-20 关闭（注册表写入，重启后生效）。
  在重启完成前，git、`dotnet test`、Avalonia XAML 构建与桌面应用运行均不可用；
  详见 `docs/development/dev-environment.md`。

## H. 文档规范

- 重要架构决定写入 `WorldView/docs/decisions/`（ADR 格式，模板见同目录）。
- 技术方案写入 `WorldView/docs/architecture/`。
- 开发和构建说明写入 `WorldView/docs/development/`。
- 重大变更同步更新文档。
- 仓库根 `docs/` 为正式历史文档，未经用户批准不得修改。
