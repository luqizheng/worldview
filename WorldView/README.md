# World View — 新架构工程（C# + Avalonia）

World View 是 Windows 和 macOS 双平台的专业地图编辑与 AI 辅助制图桌面应用。
本目录是 2026-11-20 工程初始化建立的全新 C# + Avalonia 工程根。

## 目录结构

```
WorldView/
├── WorldView.sln              # 解决方案（7 个项目）
├── AGENTS.md                  # Coding Agent 共同工程规范
├── README.md                  # 本文件
├── Directory.Build.props      # 统一构建配置（net11.0 / Nullable 等）
├── docs/
│   ├── architecture/          # 技术方案
│   ├── decisions/             # 架构决策记录（ADR）+ 模板
│   ├── development/           # 开发环境与构建说明
│   └── roadmap/               # 后续技术选型任务清单
├── scripts/                   # 构建/测试/运行脚本（Windows: *.ps1，macOS/Linux: *.sh）
├── src/
│   ├── WorldView.Desktop/     # Avalonia UI、入口、Views/ViewModels（WinExe）
│   ├── WorldView.Application/ # 用例服务、接口、DTO
│   ├── WorldView.Domain/      # 领域模型（零依赖）
│   └── WorldView.Infrastructure/ # 仓储等基础设施实现
└── tests/
    ├── WorldView.Domain.Tests/
    ├── WorldView.Application.Tests/
    └── WorldView.Infrastructure.Tests/
```

## 快速开始

前置：.NET SDK 11（本机 11.0.100）。环境注意事项见 [docs/development/dev-environment.md](docs/development/dev-environment.md)。

```bash
# 构建
scripts/build.ps1          # Windows
scripts/build.sh           # macOS/Linux

# 测试
scripts/test.ps1
scripts/test.sh

# 运行桌面应用
scripts/run.ps1
scripts/run.sh
```

或直接使用 dotnet CLI：`dotnet build WorldView.sln` / `dotnet test WorldView.sln` /
`dotnet run --project src/WorldView.Desktop`。

## 当前状态（2026-11-21 CD-01 Core Domain 持久化实施后）

- **应用基础（AF-01/AF-02）已实现**：`.wvproject` 目录式 Project Package（manifest v1 +
  SQLite 数据库 + 内容寻址 Asset Store + Staging/Backups）、分阶段包校验（fail-closed）、
  Project Session / Map Workspace Session 生命周期与进程内隔离、启动面
  （创建/打开/最近项目）。详见 [ADR-0002](docs/decisions/ADR-0002-application-foundation-package-bootstrap.md)
  与仓库根 `docs/application/AF-02_IMPLEMENTATION_REPORT_v0.1.md`。
- **Core Domain 最小持久化（CD-01）已实现**：6 张领域表经 4 条 core-domain ledger 迁移落地
  （P2-09-11 V0.11 最小子集）；Project Identity 真实创建/读取；项目范围 Map 查询；
  Branch 世系 + 规范历史时间（`"day:offset"`）Workspace 上下文解析；AF-02 四个边界端口
  全部接入真实实现并在组合根注册（Map Query / Context Resolver 会话绑定）。
  真实链路 **Create → Open → Identity → Maps → Workspace 已打通**。
  详见 [ADR-0003](docs/decisions/ADR-0003-core-domain-persistence-cd01.md) 与仓库根
  `docs/core-domain/CD-01_IMPLEMENTATION_REPORT_v0.1.md`。
- 早期 `MapProject` 架构示例代码已删除（CD-01 影子模型收敛）；领域模型以
  `WorldView.Domain` 为准。
- 平台验证状态：
  - **Windows**：7/7 项目 Debug/Release 构建通过（0 错误）；139/139 测试通过
    （SDK 11.0.100；Domain 58 / Application 44 / Infrastructure 37）；Release 桌面应用
    启动冒烟正常。
  - **macOS**：**未验证**（当前无 macOS 环境）。工程配置按双平台要求编写（无平台专有代码、
    使用 Avalonia 跨平台模板基线），首次在 macOS 上构建时需按开发文档确认。

## 与旧工程的关系

- 本仓库根 `docs/`：正式历史文档（ADR-001~005 已冻结、Rebuild 架构基线 V1.0、域模型讨论记录）。
  旧系统（Rust + Bevy 工程）按基线记录为 Reference Only，不在本仓库内。
- `server/`（旧技术栈时期的 Python 领域草稿）：已于 2026-11-20 经用户决定整体删除；
  其设计结论以仓库根 `docs/implementation/IMPLEMENTATION-LOG.md` 与 `docs/P2-01-D01/` 为准。
- 新架构对仓库根 `docs/` 只读引用；两套目录互不侵入。

## 文档索引

- [docs/decisions/ADR-0001-project-bootstrap.md](docs/decisions/ADR-0001-project-bootstrap.md) — 工程初始化决策
- [docs/decisions/ADR-0002-application-foundation-package-bootstrap.md](docs/decisions/ADR-0002-application-foundation-package-bootstrap.md) — AF-02 应用基础决策
- [docs/decisions/ADR-0003-core-domain-persistence-cd01.md](docs/decisions/ADR-0003-core-domain-persistence-cd01.md) — CD-01 Core Domain 持久化决策
- [docs/architecture/initial-tech-baseline.md](docs/architecture/initial-tech-baseline.md) — 技术基线（已定/未定）
- [docs/development/dev-environment.md](docs/development/dev-environment.md) — 开发环境（含 Smart App Control 事件记录）
- [docs/development/build-and-test.md](docs/development/build-and-test.md) — 构建与测试
- [docs/roadmap/tech-assessment-backlog.md](docs/roadmap/tech-assessment-backlog.md) — 后续技术选型清单
