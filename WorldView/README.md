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

## 当前状态（2026-11-21 AF-02 应用基础实施后）

- **应用基础（AF-01/AF-02）已实现**：`.wvproject` 目录式 Project Package（manifest v1 +
  SQLite 数据库 + 内容寻址 Asset Store + Staging/Backups）、分阶段包校验（fail-closed）、
  Project Session / Map Workspace Session 生命周期与进程内隔离、启动面
  （创建/打开/最近项目）。详见 [ADR-0002](docs/decisions/ADR-0002-application-foundation-package-bootstrap.md)
  与 `docs/application/AF-02_IMPLEMENTATION_REPORT_v0.1.md`。
- **Core Domain 集成为显式门槛**：`IProjectIdentityReader` / `IMapQueryService` /
  `IWorkspaceContextResolver` / `IProjectCreationService` 四个边界端口尚无真实实现，
  创建/打开在 bootstrap 边界如实报告阻塞项；正式 Core Domain 持久化落地后注册真实实现
  即可点亮真实读路径。真实 Map 读路径**尚未打通**。
- Domain/Application/Infrastructure 中的 `MapProject` 相关类型是**架构示例代码**，仅用于建立
  分层与测试基线；正式领域模型以 Phase 2 历史状态引擎设计为准，实现阶段可替换或删除。
- 平台验证状态：
  - **Windows**：7/7 项目 Debug/Release 构建通过；70/70 测试通过（SDK 11.0.100）；
    Debug/Release 桌面应用启动、启动面渲染、WM_CLOSE 干净退出（exit 0）均已在
    Windows 实机验证（证据见 AF-02 实施报告）。
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
- [docs/architecture/initial-tech-baseline.md](docs/architecture/initial-tech-baseline.md) — 技术基线（已定/未定）
- [docs/development/dev-environment.md](docs/development/dev-environment.md) — 开发环境（含 Smart App Control 事件记录）
- [docs/development/build-and-test.md](docs/development/build-and-test.md) — 构建与测试
- [docs/roadmap/tech-assessment-backlog.md](docs/roadmap/tech-assessment-backlog.md) — 后续技术选型清单
