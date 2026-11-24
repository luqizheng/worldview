# 框架与包矩阵（WP-30 PRE-FLIGHT）

- **核验日期:** 2026-11-24
- **基准:** `master` @ `28897fc6c96a0e448a377950392f403956af0357`
- **方法:** `dotnet --list-sdks`、`WorldView/Directory.Build.props`、各 `.csproj`、`WorldView/src/WorldView.Desktop/obj/project.assets.json`（还原产物锁定）、源码 grep。
- **约束遵守:** 本核验未升级 .NET、未升级 Avalonia、未改任何 NuGet 版本。

## 1. .NET SDK 与 TargetFramework

| 项 | 值 | 证据 |
|---|---|---|
| 本机已装 SDK | `11.0.100`（唯一） | `dotnet --list-sdks` |
| TargetFramework | `net11.0`（8 个工程统一） | `WorldView/Directory.Build.props`（注释明确 ".NET 11 LTS（SDK 11.0.100+，支持至 2028-11）"） |
| SDK 版本锁定 | **无**（`global.json` 仅锁定 `"test": {"runner": "Microsoft.Testing.Platform"}`，无 `sdk.version`/`rollForward`） | `WorldView/global.json` |
| CI SDK | `11.0.x` | `.github/workflows/dotnet-ci.yml` `setup-dotnet` 输入 |
| 语言特性 | `ImplicitUsings=enable`、`Nullable=enable`、`Version=0.1.0`、`IsPackable=false` | 同 Directory.Build.props |

⚠️ **WP-30 需注意:** SDK 无 global.json 版本钉扎；PRA-01 交接 §3 要求"以仓库当前批准的目标框架为准，不得随任务升级"。若要钉扎，属独立技术决策（本任务不做）。

## 2. Avalonia 实际版本

| 包 | 版本 | 引用方式 | 证据 |
|---|---|---|---|
| Avalonia | **12.1.6** | 直接（仅 Desktop） | `WorldView/src/WorldView.Desktop/WorldView.Desktop.csproj` |
| Avalonia.Desktop | **12.1.6** | 直接 | 同上 |
| Avalonia.Themes.Fluent | **12.1.6** | 直接 | 同上 |
| Avalonia.Fonts.Inter | **12.1.6** | 直接 | 同上 |
| AvaloniaUI.DiagnosticsSupport | **2.2.3** | 直接（Debug-only：Release 配置 IncludeAssets=None/PrivateAssets=All） | 同上 |
| Avalonia.Skia | **12.1.6** | **传递**（经 Avalonia.Desktop） | `project.assets.json` |
| Avalonia.Win32 / Avalonia.Native / Avalonia.X11 / Avalonia.FreeDesktop(AtSpi) | 12.1.6 | 传递 | 同上 |
| Avalonia.HarfBuzz | 12.1.6 | 传递（文本整形已随框架在依赖树中） | 同上 |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | 传递（Win32 GL） | 同上 |
| Avalonia.BuildServices | 11.3.2 | 传递 | 同上 |
| Avalonia.Remote.Protocol | 12.1.6 | 传递 | 同上 |

其余直接包（非渲染）：CommunityToolkit.Mvvm 8.4.2、Microsoft.Extensions.DependencyInjection 11.0.0（Desktop）；Newtonsoft.Json 13.0.4、Corvus.Text.Json 5.8.11（Application）；MackySoft.Json.Canonicalization 0.1.0（Domain）；Microsoft.Data.Sqlite 11.0.0、EFCore.Sqlite/Design 11.0.0（Infrastructure）。

## 3. SkiaSharp 存在性

**存在，但为纯传递依赖，代码零消费：**

| 项 | 值 | 证据 |
|---|---|---|
| SkiaSharp | **3.119.4**（传递，经 Avalonia.Skia） | `project.assets.json` |
| SkiaSharp.NativeAssets.Win32 / macOS / Linux / WebAssembly | 3.119.4（原生资产随包就位） | 同上 |
| 直接 `PackageReference` SkiaSharp | **无**（任何 csproj） | 全部 8 个 csproj 核对 |
| C# 源码 `Skia` 标识符 | **零命中** | `grep -i "Skia" src tests`（排除 bin/obj） |

含义：PRA-02/WP-30 引入 Skia 绘制时，**不需要新增 NuGet 包**即可 `using SkiaSharp`（可传递解析）；但按交接 §7，`ICustomDrawOperation` 与 Skia lease 管理属 PRA-02 范围。是否把 SkiaSharp 从传递提升为显式引用是 WP-30 实施时的工程决策（显式引用更符合"用到即声明"，且不改变版本）。

## 4. 平台目标与构建配置

| 项 | Windows | macOS | 证据 |
|---|---|---|---|
| TargetFramework | net11.0 | net11.0（同一 TFM，无平台条件编译） | Directory.Build.props；csproj 无 `Condition` 平台分支 |
| Desktop 输出类型 | WinExe（跨平台 exe，非 win-only 标记） | 同 | `WorldView.Desktop.csproj` |
| 平台宿主包 | Avalonia.Win32（传递） | Avalonia.Native + Avalonia.X11（传递） | project.assets.json |
| app.manifest | 有（Windows 10 supportedOS；Avalonia 模板标准注释） | 不适用 | `WorldView/src/WorldView.Desktop/app.manifest` |
| RID / self-contained | **均未配置**（无 RuntimeIdentifiers、无 publish profile） | 同 | csproj 核对 |
| CI 矩阵 | windows-latest × Debug/Release | macos-latest × Debug/Release | `.github/workflows/dotnet-ci.yml`（4 job，`working-directory: WorldView`） |

无 AnyCPU/x64 平台特殊化；测试在本机以 `net11.0|x64` 运行。

## 5. 当前 Avalonia 渲染后端配置

**零自定义配置——全部默认：**

```csharp
// WorldView/src/WorldView.Desktop/Program.cs — BuildAvaloniaApp()（全文）
AppBuilder.Configure<App>()
    .UsePlatformDetect()
#if DEBUG
    .WithDeveloperTools()
#endif
    .WithInterFont()
    .LogToTrace();
```

- 无 `UseWin32/UseX11/UseAvaloniaNative` 强制选择；无 `Win32PlatformOptions`/`X11PlatformOptions`/`AvaloniaNativePlatformOptions`（grep 零命中）；
- Avalonia 12 桌面默认渲染后端即为 **Skia**（经传递的 Avalonia.Skia）——即"当前后端"= Skia 默认路径，但应用代码尚未绘制任何自定义内容；
- 无 GPU/软件渲染开关、无 `RenderingMode` 设置。

## 6. 自定义绘制控件 / ICustomDrawOperation

**不存在：**

- `ICustomDrawOperation`/`FuncCustomDrawOperation`/`CustomDrawOperation`：src+tests 零命中；
- `RenderTargetBitmap`/`WriteableBitmap`/离屏位图：零命中；
- AXAML 控件树（StartWindow/ProjectWindow/CreateMapDialog 三窗口）：无 Canvas、无 Viewport、无自定义 Control 子类；
- 唯一自定义绘制相关事实：`ProjectWindow.axaml:15` 注释自证"本窗口为文本基线：地图渲染属 PRA-01/PRA-02 之后的阶段"（显式阶段决策，非遗漏）。

## 7. WP-30 视角小结

| WP-30 需要 | 现状 | 结论 |
|---|---|---|
| Avalonia 12 视口宿主 | 框架在位（12.1.6 全家桶+Skia 传递依赖+原生资产） | 就绪，无需动包 |
| Skia 绘制入口 | 无任何绘制代码 | 全新实现（PRA-02 切片） |
| 渲染后端 | 默认 Skia 已激活 | 无需配置变更（显式化后端选择可留待需要时） |
| TFM/SDK | net11.0 + SDK 11.0.100 + CI 11.0.x | 冻结不动（交接 §3） |
| 无头 UI 测试基建 | 无（GAP-P1-04 仍未关闭） | WP-30 视口测试需新建（headless 或渲染中立） |
