# 初始技术基线（2026-11-20 工程初始化）

本文档记录工程初始化时**已确定**与**尚未确定**的技术项。尚未确定项不得提前实现或引入依赖；
选型变更须通过 ADR 记录（见 `../decisions/`）。

## 1. 已确定

| 项 | 选择 | 版本 | 说明 |
| --- | --- | --- | --- |
| 编程语言 | C# | .NET 11 LTS | SDK 11.0.100；LTS 支持至 2028-11 |
| 目标框架 | net11.0 | — | 由 `Directory.Build.props` 统一指定 |
| UI 框架 | Avalonia UI | 12.1.6（稳定版） | 官方模板 `Avalonia.Templates@12.1.6`；Win + macOS 桌面 |
| UI 组织模式 | MVVM | CommunityToolkit.Mvvm 8.4.2 | 源生成器（ObservableProperty / RelayCommand） |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 11.0.0 | 组合根位于 `WorldView.Desktop` Program.cs |
| 单元测试 | xUnit.v3 | 4.0.3 + Test.Sdk 18.12.0 | 2026-11-21 升级：.NET 11 SDK 下 `dotnet test` 需经 `global.json` 启用 Microsoft.Testing.Platform；coverlet.collector 10.1.1 |
| 解决方案格式 | 经典 `.sln` | — | 兼容性优于 `.slnx`（.NET 11 SDK 默认生成 slnx，已改用 sln） |
| Nullable | 全局开启 | — | `Directory.Build.props` |
| 编译绑定 | 开启 | — | Desktop 项目 `AvaloniaUseCompiledBindingsByDefault=true` |

### 分层与依赖方向

```
Desktop ──► Application ──► Domain
   │              ▲
   └─► Infrastructure
```

- Desktop → Infrastructure 仅供组合根（Program.cs）注册实现使用。
- Domain 零依赖；Application 仅依赖 Domain；Infrastructure 仅依赖 Application（接口）。

## 2. 尚未确定（不得提前锁定）

以下选型在相应评估完成前，**不得**因工程初始化方便而提前引入依赖：

1. 地图 2D 渲染技术（Skia / 自绘 / 其他）。
2. 地图 3D 渲染技术（Vulkan / Metal / OpenGL 封装等）。
3. 几何计算引擎。
4. 地理空间数据处理引擎。
5. 空间索引。
6. 本地数据库。
7. 地图项目文件格式。
8. AI 模型接入方式。
9. AI Agent 运行时。
10. Task Planning & Execution Engine 的最终实现细节。

评估任务清单见 `../roadmap/tech-assessment-backlog.md`。

## 3. 初始化时的临时实现（示例代码）

- `Domain.Projects.MapProject` / `Application.Projects.MapProjectService` /
  `Infrastructure.Projects.InMemoryMapProjectRepository` 仅用于建立分层、DI、MVVM 与测试基线。
- 正式领域模型以 Phase 2 历史状态引擎设计（仓库根 `docs/P2-01-D01/`）为准，
  实现阶段可整体替换或删除示例代码。
- 持久化当前为内存实现；本地存储与项目文件格式选型完成前不引入真实存储。
