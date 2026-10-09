# World View

> 双层世界地图编辑器 — **Godot 4.7.2 (mono) + C#（.NET 8）** 桌面应用。

`World View` 是一个**单机、离线优先**的桌面地图编辑器：

- **世界层** — 大陆 / 区域级网格地图：地形、高度、道路、路线、建筑、疆界与影响力；
- **城市层** — 从世界地图下钻的城市网格，进一步进入建筑 / 室内编辑；
- **工程即目录** — 一个文件夹就是一个完整世界工程，YAML 为主数据格式，仅大网格走二进制分块。

详细规范见 [`docs/`](./docs/)（**字段定义以《数据模型规格》为唯一真源**）；任务与验收定义见 [`.trae/specs/`](./.trae/specs/)。

---

## 快速上手

### 0. 前置依赖

| 工具 | 版本 | 说明 |
|---|---|---|
| Godot | **4.7.2 stable mono** | 必须是 mono / .NET 版；路径见 [§ 本机环境](#本机环境) |
| .NET SDK | **8.0**（本机 8.0.425） | `dotnet --list-sdks` 校验 |
| Windows | 10 / 11 | 当前目标平台 |

NuGet 依赖由构建自动还原：`YamlDotNet 16.3.0`、`Godot.NET.Sdk 4.7.2`。

### 1. 构建（B1）

```powershell
git clone <repo>
cd world-view
dotnet build WorldView.sln -c Debug
```

预期：**0 个错误**（警告为已知噪音，主要是测试项目的 `xUnit1051` 提示）。

### 2. 跑单元测试（B2）

```powershell
& "tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe"
```

> 需先跑 B1 产出 exe。本项目测试已切到 **xunit.v3 + Microsoft.Testing.Platform**（`OutputType=Exe`，编译成可执行 runner），**不要用 `dotnet test`** —— vstest testhost 在本机会因沙箱进程限制拉起即崩（`Win32Exception 5`），且 MTP 项目由 VSTest 驱动时找不到测试 dll。

### 3. Godot 场景校验（B3）

```powershell
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" `
    --headless --path . --check-only --quit-after 120
```

预期：在超时内**自然退出**，且 stderr 无场景 / 脚本解析错误。

> `--quit-after` 不可省：`autoload/mcp_interaction_server.gd` 是一个常驻 TCP 服务器，单独使用 `--check-only` 时它会在校验完成后挂住主循环不退出。C# 运行时异常（不阻塞场景解析）不计入本项。

### 4. 领域冒烟（B4 之外的端到端检查）

```powershell
dotnet run --project tests\WorldView.Smoke
```

覆盖 `scripts/Domain/**` + `scripts/Storage/**` 的落盘 / 重载闭环。

### 5. 启动编辑器

用 Godot mono 打开本目录（含 `project.godot`），主场景为 `scenes/Main.tscn`，直接 **F5** 运行。

---

## 仓库布局

```
world-view/
├── project.godot              # Godot 工程入口（main_scene = scenes/Main.tscn）
├── WorldView.sln / .csproj    # C# 主项目（Godot.NET.Sdk 4.7.2，只编 scripts/）
├── scripts/                   # 全部 C# 源码
│   ├── Domain/                # 领域模型 + 校验（纯 C#，不依赖 Godot，可裸测）
│   ├── Storage/               # YAML 读写、工程加载 / 保存、校验器
│   ├── Editing/               # 命令模式撤销栈（含批处理）+ 笔刷形状 / 控制器
│   ├── Editor/                # 编辑器上下文、地图尺寸与单位换算
│   ├── Render/                # 渲染层（Terrain / Marker / Road / Grid 等）
│   ├── Ui/                    # 界面：Chrome / Palette / Components / Theme
│   ├── ProjectManagement/     # 工程管理：IFileSystem 抽象、最近项目、自动保存、备份
│   └── Generator/             # Roslyn 源生成器（编译期产出主题 token）
├── scenes/                    # Godot 场景（Main.tscn + ui/ + _demo/）
├── autoload/                  # Godot autoload（GDScript）
├── assets/                    # 主题 / 调色板 / 图标 / 纹理 / 图块集
├── tests/
│   ├── WorldView.Tests/       # 单元测试（xunit.v3 + MTP，独立 csproj）
│   └── WorldView.Smoke/       # 领域端到端冒烟运行器
├── examples/sample_world/     # 示例工程「艾瑟兰大陆」
├── crates/                    # Rust 骨架（v0.1 不启用）
├── docs/                      # 设计文档 v0.1 + 验收记录
└── .trae/specs/               # MVP / UI 任务定义（spec / checklist / tasks）
```

> 主项目 `WorldView.csproj` 设了 `EnableDefaultCompileItems=false`，只编译 `scripts/**`；`tests/` 由各自的 csproj 编译。不要改成默认收集，否则 `obj/`、`bin/` 会被卷进编译。

---

## 关键设计原则（v0.1 收敛，七条不可动摇）

1. **单机可运行** — 无需服务器，直接读写本地目录。
2. **目录即工程** — 一个文件夹 = 一个完整世界工程，可拷贝、可压缩、可 Git。
3. **YAML 为主格式** — 元数据 / 配置 / 矢量 / 所有对象一律 YAML，人类可读、可手改、可版本管理。**唯一例外**：超大地图的网格数组（地形 / 高度）允许以**二进制分块**作为一等源格式，但必须遵循《二进制格式规格》并在索引 YAML 中显式声明；**禁止对元数据使用二进制**。
4. **多地图层级** — 世界地图 → 区域地图 → 城市地图 → 建筑 / 室内地图，逐层引用。
5. **图层化编辑** — 地形 / 疆界 / 影响力 / 道路 / 事件点等分层，可独立开关、锁定、调透明度与顺序。
6. **非破坏编辑** — 笔刷 / 选区可撤销、可重做、可保存快照。
7. **时间轴驱动** — 拖动时间轴，地图图层、人物状态、势力范围随之变化。

技术栈（P0 收敛结论）：Godot 4.7.2 mono + C#（.NET 8）、Godot 内置 2D、网格 + 矢量 + 二进制分块三态编码。

---

## 已知约束与范围

- **时间轴分支**不在当前范围（待决项 D-2）。
- **第三方插件**不进（待决项 D-3）。
- **疆界贴合范围**待定（待决项 D-1）。
- **`crates/` 下的 Rust 骨架不启用**，v0.1 不构建、不测试。
- **历史遗留**：本仓曾用 Tauri + React + PixiJS 实现，已整体移除（旧栈清理见 `.trae/m1-cleanup-manifest.md`）。任何 `cargo` / `pnpm` 指令都已作废。

---

## 测试与验收

每次提交前应跑完整基线自检（B1–B4），命令即上面「快速上手」的第 1 / 2 / 3 步，外加：

```powershell
git status --short      # B4：工作树不该出现产物类噪音
```

> **当前存在已知基线失败**，清单、分类与判定口径统一记录在 [`docs/长期执行计划_v0.1.md`](./docs/长期执行计划_v0.1.md) §5 / §6 —— 校验基线时以该文档为准，不要在本文件里硬编码测试计数（计数会随任务推进变化）。

M1 验收对照表见 [`docs/acceptance/map_editor_mvp_check.md`](./docs/acceptance/map_editor_mvp_check.md)。

---

## 本机环境

| 项 | 路径 / 版本 |
|---|---|
| Godot | `C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe` |
| .NET SDK | `8.0.425`（另有 10.0.401，被 `global.json` 的 `rollForward: latestFeature` 挡在项目外） |
| 测试 runner | `tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe`（MTP 直跑） |

工程协作者（含 agent）请先读 [`AGENTS.md`](./AGENTS.md)：仓库约定、外部依赖硬编码路径、文档索引与事实来源优先级都在那里。

---

## 许可

仅限项目内部使用。
