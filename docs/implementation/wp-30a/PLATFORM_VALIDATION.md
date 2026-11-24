# WP-30A 平台验证记录

## 1. 本机验证（Windows）

- 环境：Windows 10.0.26300 x64，.NET SDK 11.0.100（global.json 钉扎 MTP runner）。
- 日期：2026-11-24。

### 1.1 Debug

| 步骤 | 命令 | 结果 |
|---|---|---|
| Restore | `dotnet restore WorldView.sln` | 通过（10 项目） |
| Build | `dotnet build WorldView.sln --configuration Debug` | 0 警告 0 错误 |
| Test | 五个测试 DLL 直调（Domain/Application/Infrastructure/Desktop/Presentation） | **836/836 通过**（266/137/318/11/104） |

### 1.2 Release

| 步骤 | 命令 | 结果 |
|---|---|---|
| Build | `dotnet build WorldView.sln --configuration Release` | 0 错误（`WorldView.Presentation.csproj` 0 警告） |
| Test | 五个测试 DLL 直调（Release 输出） | **836/836 通过** |

### 1.3 本机 `dotnet test` 说明

本机环境 `dotnet test WorldView.sln` 的 MTP 桥接存在既知 NamedPipe
`UnauthorizedAccessException` 问题（与 master 现状一致，非本阶段引入；见项目记忆
"MTP 过滤用直调 DLL"惯例）。CI 环境不存在该问题——CI 使用 sln 级
`dotnet test`（见 §2）。因此本机以直调测试 DLL 的等效方式完成验证。

## 2. CI 验证（GitHub Actions 四平台矩阵）

- Workflow：`.github/workflows/dotnet-ci.yml`（windows-latest / macos-latest × Debug / Release，
  sln 级 build + `dotnet test`，自动发现新工程与新测试项目）。
- 触发：本分支 push 不触发（CI 仅对 master push / PR 目标 master 等触发）；以 **PR 目标 master**
  触发完整四 job 矩阵。
- 结果：**CI run 记录见 PR 页面 Checks**；四 job（Windows×2、macOS×2）全部绿为验收条件，
  见 IMPLEMENTATION_SUMMARY.md §1 中记录的 run 链接。

## 3. CI 与真实 GUI 验证的边界（不得混淆）

- **CI 证明的是**：Windows/macOS 双平台的 headless 构建 + 单元/集成测试通过
  （含 Presentation 全部 104 项逻辑/并发/生命周期测试）。
- **CI 不能证明**：任何真实 GUI 行为——本阶段没有 Avalonia 视口、没有 Skia 绘制、
  没有 `ICustomDrawOperation`（均为 WP-30B/C 范围）。macOS 上的窗口呈现/字体/渲染
  零证据。
- **本机为 Windows**：macOS 仅由 CI 覆盖，不存在任何本机 macOS 运行验证；
  本文档明确声明不做"macOS 已验证"的表述。
- 真实 GUI 执行证据（roadmap M1 验收 1/2/10）按 Profile §11 属后续阶段
  （WP-30B blank viewport、WP-30C Pan/Zoom/Resize/DPI 平台验证）。

## 4. 无关工作区内容保护

- `docs/map-editor/implementation/`（用户未跟踪目录）：**未读取、未修改、未纳入提交**，
  保持原样（Profile §9 DoD 第 12 项）。
- 其余未触碰文件：见 IMPLEMENTATION_SUMMARY.md §3 修改清单对照。
