# 开发环境说明

更新：2026-11-20（工程初始化当日）。

## 基础环境

| 项 | 值 | 备注 |
| --- | --- | --- |
| OS（当前开发机） | Windows 11（build 26300，x64） | 目标平台另含 macOS（未验证） |
| .NET SDK | 11.0.100（唯一安装） | `dotnet --list-sdks` 确认 |
| Avalonia 模板 | Avalonia.Templates@12.1.6 | `dotnet new list` 可见 `avalonia.app` 等 |
| NuGet | 默认源 nuget.org | 连通性已验证 |
| Git | Git for Windows（UCRT64 布局，2026-10-26 版） | 见下文 Smart App Control 事件 |
| 远程仓库 | `git@github.com:luqizheng/worldview.git` | 本机直连 HTTPS GitHub 被阻断，SSH 经 ssh.github.com:443 路由 |

IDE：任意支持 .NET 11 + Avalonia 的环境（VS 2022 / Rider / VS Code + Avalonia 扩展）。
Avalonia XAML 预览器依赖项目模板默认配置，当前可直接使用。

## Windows Smart App Control 事件记录（重要）

2026-11-20 工程初始化时发现本机 **Smart App Control（SAC，内部策略
VerifiedAndReputableDesktop）处于 On 并强制执行**，导致以下现象全部同源：

| 现象 | 表面错误 | 实际原因 |
| --- | --- | --- |
| `git` 任何命令立即失败（exit 0xC0F10002），ZCode Bash 工具无法启动 | "error launching git" | Code Integrity 拦截 git.exe 加载 libpcre2-8-0.dll 等自家 DLL（事件 3077） |
| Avalonia Desktop 项目构建失败 | MSB4216 / MSB4061（任务宿主 / ctor 缺失） | MSBuild 任务宿主进程加载 `Avalonia.Build.Tasks.dll` 被拦截（0x800711C7，事件 3077） |
| `dotnet test` 失败 | "没有可用测试" + FileLoadException | xUnit 加载新编译的测试 DLL 被拦截 |
| 任何新编译的 .exe 无法运行 | "已被 Device Guard 阻止"（exit 4551） | 实测：最小控制台程序也被拦截 |

诊断方法（供以后复用）：

```powershell
(Get-MpComputerStatus).SmartAppControlState          # On / Evaluation / Off
citool -lp                                          # 查看活动 Code Integrity 策略
Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-CodeIntegrity/Operational'; Id=3077} -MaxEvents 20
```

**处理**（2026-11-20 已执行，由 agent 在初始化任务授权下实施并经用户知悉）：

```powershell
Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' `
  -Name 'VerifiedAndReputableDesktopState' -Value 0 -Type DWord   # 0 = Off
```

- **重启后生效**（Code Integrity 策略在引导期加载，运行期写入不即时生效）。
- SAC 关闭后**无法重新开启**（除非重置 Windows）；标准 Defender 杀毒防护不受影响。
- SAC 不支持白名单/例外，开发机（需要运行本地编译产物与第三方工具链）与其不可调和，
  关闭是唯一可行路径。

### 重启后验证清单

1. `powershell -Command "(Get-MpComputerStatus).SmartAppControlState"` → 应为 `Off`。
2. `git status`（在仓库根）→ 正常输出；应仅显示新增 `WorldView/` 目录为未跟踪。
3. `dotnet build WorldView.sln` → 7/7 项目构建通过。
4. `dotnet test WorldView.sln` → 全部测试通过（Domain 3 + Application 3 + Infrastructure 3 = 9 个）。
5. `scripts/run.ps1`（或 `dotnet run --project src/WorldView.Desktop`）→ 主窗口显示 "World View"。
6. 任一验证失败时按 `build-and-test.md` 排查，并对照本文档事件记录判断是否残留 SAC 影响。

## macOS 环境注意（首次在 macOS 构建时确认）

- 安装 .NET 11 SDK（x64 / arm64 均可）；`scripts/build.sh`、`scripts/test.sh`、`scripts/run.sh`
  可直接使用。
- 工程无 Windows 专有代码；`app.manifest` 仅作用于 Windows，macOS 忽略。
- Inter 字体包（Avalonia.Fonts.Inter）保证两平台默认字体一致。
- 未验证项：构建、测试、运行、打包（.app 打包方案属后续技术评估，见 roadmap）。
