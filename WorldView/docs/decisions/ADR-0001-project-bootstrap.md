# ADR-0001：新架构工程初始化基线

- 状态：Accepted（工程初始化任务授权范围内；后续变更须新 ADR）
- 日期：2026-11-20
- 决策人：用户（任务书授权）；agent 执行

## 背景

World View 新架构技术方向已由用户确定为 C# + Avalonia（Windows + macOS 桌面单机应用）。
本决策记录工程初始化（bootstrap）阶段作出的全部工程级选择。

与仓库根已冻结决策的关系检查：ADR-001（分叉历史地图模型）、ADR-002（统一 History 实体）、
ADR-003（命令驱动播放）、ADR-004（批量修复 Undo/Redo）、ADR-005（Branch 分叉规则）均为
**领域模型层**决策，与本决策的工程结构选择无冲突；这些决策的落地属于后续历史状态引擎实现阶段。

## 决策

1. **目录方案**：在仓库根新建自包含目录 `WorldView/` 作为新架构工程根；仓库根现有 `docs/`
   与 `server/` 保持原样（只读引用），不迁移、不修改、不删除。理由：仓库根 `docs/` 已有稳定
   的正式文档结构与冻结 ADR，新工程文档需要独立演进空间，最小侵入式隔离新旧内容。
2. **解决方案**：经典 `WorldView.sln`（不使用 .NET 11 SDK 默认生成的 `.slnx`），兼容现有
   IDE / CI / agent 工具链。
3. **目标框架**：net11.0（.NET 11 LTS，SDK 11.0.100，支持至 2028-11），由
   `Directory.Build.props` 统一指定，各 csproj 不重复声明。
4. **UI 框架版本**：Avalonia 12.1.6（NuGet 最新稳定版）；MVVM 使用 CommunityToolkit.Mvvm
   8.4.2；DI 使用 Microsoft.Extensions.DependencyInjection 11.0.0。均为稳定版本，无预览依赖。
5. **初始项目集**：Desktop / Application / Domain / Infrastructure 四个主项目 +
   三个测试项目（xUnit 2.9.3），依赖方向为
   Desktop→Application→Domain、Infrastructure→Application、Desktop→Infrastructure（仅组合根）。
6. **示例垂直切片**：Domain/Application/Infrastructure/ViewModel 中的 `MapProject` 相关代码
   为架构示例（含测试），用于验证分层、DI、MVVM 全链路与测试基线；正式领域模型由 Phase 2
   历史状态引擎设计确定后替换。
7. **构建脚本**：`scripts/` 提供构建/测试/运行的双平台脚本对（`*.ps1` / `*.sh`），内部仅转发
   dotnet CLI，不引入额外构建系统。
8. **编译绑定**：Desktop 项目开启 `AvaloniaUseCompiledBindingsByDefault`，XAML 绑定错误在
   编译期暴露。

## 理由

- .NET 11 是本机唯一已安装 SDK（11.0.100），且为 2026-11 刚发布的 LTS，避免安装额外 SDK。
- Avalonia 12.1.6 为查询 NuGet 确认的最新稳定版；Avalonia 官方模板 12.1.6 已安装验证可用。
- 经典 .sln 与独立目录方案把对既有仓库的侵入降到最低，同时满足"新旧工程关系清晰"的任务要求。

## 影响

- 后续所有新架构代码必须在 `WorldView/` 内；跨目录引用需新 ADR。
- 示例代码删除/替换不视为架构变更，无需新 ADR。
- 平台验证状态与遗留验证项见 `../development/dev-environment.md` 与根 README。

## 验证

- `dotnet build WorldView.sln` 全部 7 项目构建通过（初始化当日因环境问题受限于 Smart App
  Control，验证清单见 `../development/dev-environment.md`）。
- `dotnet test WorldView.sln` 全部测试通过。
- `git status` 仅显示新增 `WorldView/` 目录，旧工程内容零改动。
