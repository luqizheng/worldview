# 构建与测试

## 常用命令

所有命令在 `WorldView/` 目录下执行。

```bash
# 还原 + 构建
dotnet build WorldView.sln

# 测试（全部 3 个测试项目）
dotnet test WorldView.sln

# 运行桌面应用
dotnet run --project src/WorldView.Desktop

# 仅构建某项目（含其依赖）
dotnet build src/WorldView.Desktop
```

脚本对（内容仅转发上述命令，便于跨平台与 agent 调用）：

| 动作 | Windows | macOS/Linux |
| --- | --- | --- |
| 构建 | `scripts/build.ps1` | `scripts/build.sh` |
| 测试 | `scripts/test.ps1` | `scripts/test.sh` |
| 运行 | `scripts/run.ps1` | `scripts/run.sh` |
| 环境自检 | `scripts/verify-environment.ps1` | — |

## 构建配置

- 统一属性在 `Directory.Build.props`（net11.0、Nullable、ImplicitUsings、版本 0.1.0、Product）。
  各 csproj 只保留差异项；新增公共属性优先放这里，不要散落到各项目。
- 依赖包版本集中在各 csproj 的 `PackageReference`；升级版本时全文检索同名包保持一致。

## 项目结构约定

- 新增领域类型放 `src/WorldView.Domain/<子域>/`；用例与 DTO 放
  `src/WorldView.Application/`（接口在 `Abstractions/`）；外部实现放
  `src/WorldView.Infrastructure/`。
- 测试目录结构与被测项目对应（`tests/WorldView.Domain.Tests/Projects/…` 对应
  `src/WorldView.Domain/Projects/…`）。
- ViewModel 放 `src/WorldView.Desktop/ViewModels/`，Window/UserControl 放 `Views/`；
  ViewModel 只依赖 Application 层接口。

## 已知问题与排查

1. **Smart App Control 未关闭时**（见 `dev-environment.md`）：Desktop 项目构建在
   GenerateAvaloniaResources 处报 MSB4216/MSB4061、`dotnet test` 报 0x800711C7、git 报
   "error launching git"。处理：确认 `VerifiedAndReputableDesktopState=0` 并重启。
2. **Avalonia 构建警告**：`自定义任务 Avalonia.Build.Tasks 需要回退到进程外执行…` —
   MSBuild 18 对未声明 Runtime/Architecture 的 UsingTask 的性能提示，非错误，可忽略；
   待 Avalonia 上游在其 targets 中补属性后消失。
3. **新建 Avalonia 项目**：模板 `dotnet new avalonia.app -n <Name> -o src/<Name>`，随后
   （a）删除 csproj 中的 `TargetFramework`/`Nullable`/`ImplicitUsings`（由
   Directory.Build.props 提供）、（b）TargetFramework 若为 net10.0 需删除改由统一配置、
   （c）`dotnet sln WorldView.sln add <csproj>` 加入解决方案。
4. **测试命名**：测试类与被测类对应（`MapProjectTests` ↔ `MapProject`）；断言用 xUnit
   `Assert.*`，不引入额外断言库。

## 排查工具

```bash
# MSBuild 二进制日志（用 MSBuild Structured Log Viewer 或 dotnet-msbuildlog 查看）
dotnet build WorldView.sln -bl

# 构建服务器异常时强制全新
dotnet build-server shutdown
dotnet build WorldView.sln -nodeReuse:false
```
