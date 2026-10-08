# AGENTS.md — World View 项目导航（给 AI 编码代理）

> 目标：让 AI 在最短读取量内建立正确心智模型。**首次进入先读本文件，再按第 4 节顺序读文档。**
> 当前阶段：**设计文档阶段（v0.1）**。仓库尚无代码，只有文档。动手写代码前先读「3. 待决项」。

## 1. 项目是什么

- **World View（世界观构建工具）**：单机优先、目录化存储、YAML 驱动的世界构建工具。
- 面向：小说作者、TRPG 主持人、游戏策划、世界观创作者。
- 同时管理四个维度，核心目标是**让四者互相引用、互相驱动**：
  1. **地理空间**：世界地图 / 区域地图 / 城市地图 / 建筑室内图
  2. **时间维度**：时间轴、历史事件、状态变化
  3. **人物维度**：角色、性格、自定义属性、关系网
  4. **事件维度**：时间、地点、人物、结果
- 一句话定位：**以地理为底、时间轴为骨、人物与事件为魂的单机世界编辑器。**

## 2. 七条不可动摇的设计原则

改任何设计前必须先理解，违反任一条的方案不予采纳：

1. **单机可运行**——无需服务器，直接读写本地目录。
2. **目录即工程**——一个文件夹 = 一个完整世界工程，可拷贝、可压缩、可 Git。
3. **YAML 为主格式**——元数据、配置、矢量与所有对象一律 YAML，人类可读、可手动编辑、可 Git 版本管理。**唯一例外**：超大地图的网格数组（地形 / 高度）允许以**二进制分块**作为一等源格式（体积与加载性能所需），但必须遵循《二进制格式规格》，并在索引 YAML 中显式声明；**禁止对元数据使用二进制**。
4. **多地图层级**——世界地图 → 区域地图 → 城市地图 → 建筑/室内地图，逐层引用。
5. **图层化编辑**——地形/疆界/影响力/道路/事件点等分层，可独立开关、锁定、调透明度与顺序。
6. **非破坏编辑**——笔刷/选区可撤销、可重做、可保存快照。
7. **时间轴驱动**——拖动时间轴，地图图层、人物状态、势力范围随之变化。

> 原则 3（YAML 手改友好）意味着：**任何能落到 YAML 的字段都必须有稳定语义与校验**，不能依赖运行时才存在的隐式状态。这是本项目的核心工程约束。
> 二进制例外只适用于**大网格数组**：必须做到「**索引在 YAML、数据在二进制、声明可校验**」——二进制块承载不了语义配置，索引与元数据永远留在 YAML（详见 [二进制格式规格](docs/二进制格式规格_v0.1.md)）。

## 3. 待决项（写代码前必须收敛）

**P0 级已收敛（本轮定案，不再阻塞开工）**：

| 编号 | 决策点 | 结论 | 出处 |
|---|---|---|---|
| **P0-1** | 技术栈形态 | **2026-10-09 反转：Godot 4.7.2 mono + C#**（单机桌面壳）。Godot 整体接管客户端 UI + 渲染 + 编辑器；C# 同时承担 UI / 编辑器 / 数据契约 / 业务逻辑 / YAML 解析 / 空间索引 / 寻路。**Rust 降级为可选加速层**（`crates/` 保留空骨架，不参与 v0.1 主流程）。**Tauri + TypeScript 路线作废** | [总体设计 §3.1](docs/总体设计_v0.1.md) / [技术基调确认 §1 §10 §13](docs/技术基调确认_v0.1.md) |
| **P0-2** | 渲染方案 | **2026-10-09 反转：Godot 内置 2D**（`Node2D` / `CanvasItem` / `TileMap` / `TileSet` / `Polygon2D` / `Line2D`）；**PixiJS 全面下线**；Three.js 不引入 | [总体设计 §3.1](docs/总体设计_v0.1.md) / [技术基调确认 §3 §10](docs/技术基调确认_v0.1.md) |
| **P0-3** | 地图数据形态 | **定案：网格 + 矢量混合**；大网格用二进制分块，见 [二进制格式规格](docs/二进制格式规格_v0.1.md) | [数据模型规格 §3](docs/数据模型规格_v0.1.md) |

**仍未收敛（P1）**：

| 编号 | 决策点 | 现状 | 影响面 |
|---|---|---|---|
| **P1-1** | 疆界「自动贴合」算法范围 | 倾向先做高度阈值 + 山脊线 + 河流的简化规则 | 图层系统 §疆界 |
| **P1-2** | 时间轴分支（平行世界 / IF 线）是否进 v0.1 | 规划中标注「可选」 | 时间轴数据模型 |
| **P1-3** | **第三方**插件系统是否进 v0.1 | 规划中标注「可选」 | 架构分层 |

> 注意：P1-3 指**第三方扩展插件**；[地图类型规格](docs/地图类型规格_v0.1.md) 中的「类型化扩展点」（每种地图类型自带数据/渲染/工具画像）是**内建机制**，不是第三方插件，二者不要混淆。

**P2 — 延期处理（不影响 v0.1）**：

| 编号 | 决策点 | 现状 | 影响面 |
|---|---|---|---|
| **P2-1** | 旧 ChronoGeo Schema 稿吸收 | **延期**。原稿位于 `c:\Users\leo\AppData\Roaming\Trae CN\User\workspaceStorage\a0799e6de0be75a8cc512a1e3a595ca2\long-text\home\muxk14u6-waw1\地图数据 Schem....txt`，已被用户指认为本项目旧稿。含可借鉴字段（影响力 `clip_to_faction`、载具 `terrain_modifiers`、路线 `sub_map_refs`、事件 `time_precision`、快照 `influence_states`、导出可裁剪等），含潜在冲突（`project.yaml` 根入口、时间字符串格式、势力双源、建筑 `pixel_scale` 污染数据契约）。本轮**仅登记不处理**，留待 M1 之后批量吸收；吸收前须先升《数据模型规格》版本号并按事实来源优先级裁决 | 数据模型 / 图层 / 时间轴 / 存储 |

**纪律**：P0 已收敛（2026-10-09 反转 P0-1 / P0-2 后），实现层以 **Godot 4.7.2 mono + C# + Godot 内置 2D** 为主；**数据契约仍以中立字段为准**，Godot / C# / .NET 专有概念只允许出现在《总体设计》与各规格的「实现」小节，**不得污染《数据模型规格》的字段定义**。Rust 留作可选加速层，**v0.1 不参与主流程**。

## 4. 文档索引与阅读顺序

所有设计文档在 [`docs/`](docs/)，**中文文件名 + 版本号后缀**。按需停止：

| 顺序 | 文档 | 内容 | 何时读 |
|---|---|---|---|
| 0 | 本文件 | 导航、原则、待决项 | 永远先读 |
| 1 | [总体设计_v0.1.md](docs/总体设计_v0.1.md) | 项目定位、设计原则、技术架构与选型、统一地图内核、里程碑 M1–M8、风险与对策 | 建立全局观 |
| 2 | [数据模型规格_v0.1.md](docs/数据模型规格_v0.1.md) | **数据契约唯一真源**：工程/地图/地形/图层/人物/事件/时间轴/载具/路线/运输任务的字段与 YAML 示例 | 改任何数据结构前必读 |
| 3 | [存储与目录规范_v0.1.md](docs/存储与目录规范_v0.1.md) | 目录工程布局、YAML 文件划分、引用规则、混合存储（YAML+二进制）、备份与 Git | 涉及读写/导入导出 |
| 4 | [二进制格式规格_v0.1.md](docs/二进制格式规格_v0.1.md) | 大网格二进制分块格式契约：文件头、字节序、版本、chunk 索引、校验、与 YAML 的对应 | 涉及大网格读写/性能 |
| 5 | [地图类型规格_v0.1.md](docs/地图类型规格_v0.1.md) | 六类地图（世界/区域/城市/街区/建筑室内/地下）的技术画像：数据、渲染、编辑工具、默认图层、时间粒度、缩放层级、跨图引用 | 做某类地图前读 |
| 6 | [地图编辑器规格_v0.1.md](docs/地图编辑器规格_v0.1.md) | 世界/城市地图编辑、笔刷与工具、道路、建筑、居民生成、测量、多地图引用 | 做编辑器 |
| 7 | [图层系统规格_v0.1.md](docs/图层系统规格_v0.1.md) | 基础层、疆界自动贴合、影响力圆形裁剪、渲染叠加与视觉增强、RPG 可扩展图层清单 | 做图层 |
| 8 | [时间轴系统规格_v0.1.md](docs/时间轴系统规格_v0.1.md) | 时间刻度、事件挂载、回放、快照、分支 | 做时间 |
| 9 | [事件与人物登记规格_v0.1.md](docs/事件与人物登记规格_v0.1.md) | 事件/人物登记流程、属性模板、关系网、联动 | 做登记系统 |
| 10 | [界面与交互规格_v0.1.md](docs/界面与交互规格_v0.1.md) | 主界面布局、核心交互流 | 做 UI |

**事实来源优先级**（文档冲突时以此裁决）：

1. **数据结构/字段/枚举** → 以《数据模型规格》为**唯一真源**。
2. 存储路径/文件划分 → 以《存储与目录规范》为准。
3. **二进制分块格式**（文件头/字节序/chunk 索引/校验）→ 以《二进制格式规格》为准。
4. **各地图类型的技术画像**（分辨率/渲染策略/默认图层/缩放层级）→ 以《地图类型规格》为准。
5. 算法规则（贴合、裁剪、影响衰减）→ 以《图层系统规格》为准。
6. 其余按章节归属文档；冲突时以版本号更高者为准，并同步修订低版本。

## 5. 文档与工程约定

- **语言**：文档与沟通用**中文**；代码、文件名、YAML 键名用**英文**（小写 + 下划线或驼峰，见数据模型文档）。
- **文档命名**：`docs/<主题>_v<版本>.md`，中文名 + 版本号；改设计先升版本号，不原地覆盖语义。同一软件版本内的**补充**（如本轮技术架构补入）保留既有文件名，在文末「变更记录」追加 `补充` 行说明。
- **版本口径**：本阶段为 `v0.1`，对应规划书「软件 0.1」。里程碑 M1–M8 见总体设计。
- **YAML 约定**：
  - 根键使用稳定英文标识符，不随显示名变化（显示名单独放 `name`/`display_name`）。
  - 所有跨文件引用使用**相对工程根的路径**或**稳定 ID**，二者不得混用（详见存储与目录规范）。
  - 手写 YAML 必须能被校验器捕获错误，错误信息须指明文件与行（设计目标，非现状）。
  - **大网格二进制例外**：地形/高度等大数组可落二进制块，但**索引与元数据必须留在 YAML**，且二进制块不得承载任何语义配置（格式见 [二进制格式规格](docs/二进制格式规格_v0.1.md)）。
- **数值纪律**：算法/规则中的阈值（贴合容差、影响力半径、衰减系数等）一律**可配置**，写进 YAML / 配置而非硬编码；具体数值以文档标注为准，未标定的一律标 `[待标定]`。
- **禁止过度构建**：v0.1 只做规划书范围。平行世界分支、插件系统、多人协作等未经明确要求不得提前实现。

## 6. 当前仓库状态

### 6.1 仓库布局决策（v0.1 起生效）

- **同仓布局**：文档与代码并置于 `world-view/` 单仓库。理由：当前仓库正在使用；文档改动经常伴随示例与代码改动，单仓便于同步迭代。
- **目录约定（2026-10-09 反转后；M1 落地前须按本约定清理旧栈骨架）**：

```
world-view/
├── AGENTS.md                 # 本文件（AI 导航）
├── docs/                     # 设计规格（v0.1 × 10 + 技术基调确认 + MVP 评估纪要）
├── .trae/specs/              # MVP / Task / Checklist 等任务定义（按需新增）
├── project.godot             # Godot 4.7.2 mono 工程根
├── WorldView.sln / *.csproj  # .NET 解决方案 + Godot SDK C# 项目
├── scripts/                  # C# 业务与 UI 代码（领域层、存储层、UI 控件）
├── scenes/                   # Godot 场景文件（*.tscn）—主界面、地图编辑器、时间轴等
├── crates/                   # 【可选】Rust crates 骨架（world-view-core 等；v0.1 不参与主流程，M2+ 实测瓶颈再启用）
├── examples/                 # 示例工程（含 sample_world）
├── .worldview/               # 应用运行时私有目录（autosave / backup / cache / debug 探针与截图），不进版本库
├── *.csproj.user / obj/ / bin/  # .NET 构建产物
└── .gitignore                # 忽略 .worldview/、autosave/、build artifacts、.godot/、*.csproj.user
```

**已撤销的旧栈骨架**（M1 开工前清理）：`src/`（TS + Vite）、`src-tauri/`（Tauri 主进程）、`Cargo.toml`、`Cargo.lock`、`build.rs`、`tauri.conf.json`、`tsconfig.json`、`vite.config.ts`、`index.html`、`package.json`、`pnpm-lock.yaml`、`pnpm-workspace.yaml`、`node_modules/`、`dist/`。清理动作**不进 v0.1 文档范围**,在仓库根清单更新任务中处理。清理清单与执行步骤见 [.trae/m1-cleanup-manifest.md](.trae/m1-cleanup-manifest.md)（待用户批准 A / B / C 选项后执行）。

- **纪律**：
  - 文档与代码**同仓不同目录**；文档改动进 `docs/`，代码改动进 `scripts/` / `scenes/` / `crates/`，不要互窜。
  - C# 命名空间根建议 `WorldView.{Domain, Storage, Render, Ui, Editor}`（与 [总体设计 §3](docs/总体设计_v0.1.md) 分层对齐）；领域层纯逻辑**不引用** `Godot.*` 命名空间,便于脱离引擎单测。
  - `examples/sample_world/` 是**示例工程**（一份独立世界工程），不是应用的一部分。它应能被 `open_project` 加载；不要把它嵌进应用二进制。
  - `.worldview/` 是应用运行时产生的私有目录（autosave / backup / cache），**不进版本库**。
  - `.trae/specs/` 存放任务定义（spec / checklist / tasks），与 `docs/` 设计规格分离；前者是任务清单，后者是设计文档。
  - Rust crates **v0.1 不参与主流程**;启用前须先在 [总体设计 §3.1](docs/总体设计_v0.1.md) 增补接入路径并经评审。

### 6.2 当前进度

- 文档阶段产物：`AGENTS.md` + `docs/` 下 **10** 份规格文档 + [技术基调确认_v0.1.md](docs/技术基调确认_v0.1.md)（P0 收敛官方记录,2026-10-09 反转一次）。
- 已收敛：**P0-1 / P0-2 / P0-3**（技术栈 **Godot 4.7.2 mono + C#**，渲染 **Godot 内置 2D**，数据形态网格+矢量+二进制分块）；Tauri + TS + PixiJS 路线已作废。
- MVP 任务定义：`.trae/specs/map-editor-mvp/`（spec + checklist + tasks；含 MVP / Task / Checklist,任务清单须按新栈重写 C# / Godot 节点对应项）。
- 旧栈骨架待清理：`src/` `src-tauri/` `Cargo.toml` `package.json` `tsconfig.json` `tauri.conf.json` `vite.config.ts` 等（见 §6.1，详情见 [.trae/m1-cleanup-manifest.md](.trae/m1-cleanup-manifest.md)）。
- UI 重设计规格（M2）：`.trae/specs/ui-redesign-simgame/`（spec v0.2 + tasks v0.3 + checklist v0.3）。**已定案**：UI 收敛为 **map-first + 单条 `BottomPalette`**（内部 rows → sections：事件播放条 / 小地图 + 自适应网格建筑面板 / 工具行）；建筑网格列数按可用宽高运行时推导（`items_per_page` 不落盘）；小地图进 M2；`Render/` 开唯一只读例外 `TerrainPalette.cs`。落地节奏见 spec §13（W1–W4）。设计层同步：`docs/界面与交互规格_v0.1.md` §1 加修订标记、两处文末「变更记录」追加一行。
- **UI 重设计已落成（W1–W4）**：`scenes/Main.tscn` 改为三层结构——`BgLayer`（`CanvasLayer`，`layer = -1`，画布底色）/ `WorldCanvas`（留在**默认画布层**，继续受 `Camera2D` 平移缩放）/ `UiLayer`（`CanvasLayer`，`layer = 1`，`DockLayout` 及其下全部面板）。**动机**：`Camera2D` 的 canvas 变换作用于默认画布层的所有 `CanvasItem`（含 `Control`），此前 UI 被整体缩到约 38% 并错位——这正是 spec §1.2 里「左侧按钮被切掉一半」的真因；修复只换 UI 分层，`scripts/Render/**` 一行未改（spec §6 限制）。新增脚本在 `scripts/Ui/{Dock,Palette}/`、`scripts/Ui/{Inspector,LayerPanel,WorldDropSurface}.cs`，主题 `scripts/Ui/Theme/Dark.tres`，场景 `scenes/ui/*.tscn`。**已知遗留**：地图**初始拟合**仍以窗口为目标（`scripts/Render/WorldCanvas.cs:478-484` 用 `GetViewportRect()`），故地图略大于画布区、下半部被底栏遮挡；要改成以画布区为拟合目标需改 `Render/` 或改走 `SubViewport`，待批准修订 spec。
- 当前阶段：**进入 MVP 实现（M1 基线 + M2 部分 + M4 部分子集）**；其余 M3–M8 仍待办。**M1 开工前置动作**：① 用 Godot 工程模板初始化仓库根（`project.godot` + `WorldView.sln`，已完成）；② 清理 §6.1 列出的旧栈骨架（待用户批准清理清单）；③ 重写 `.trae/specs/map-editor-mvp/` 任务清单至 C# / Godot 节点粒度（已完成 v0.2，待 §6.3 工程模板 ready 后方可启动实现）。

### 6.3 本机外部依赖（agent 调用清单）

> 本节记录本机已安装的外部依赖与 CLI 调用入口，供 AI agent 直接复用，无需每次探测。

#### 6.3.1 Godot 4.7.2 mono

- **安装路径**：`C:\App\Godot_v4.7.2-stable_mono_win64\`
- **可执行**：`C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe`（Godot 4 起官方 Windows mono 包的可执行名带完整版本字符串；目录内的 `GodotSharp/` 是 .NET 绑定 + GodotSharpEditor.dll，运行时由 exe 自动加载）
- **版本**：4.7.2-stable.mono（mono 构建，支持 C# / .NET 8）
- **用途**：`project.godot` 的 F5 运行 / 资源导入 / 导出 / `--headless --check-only` 工程完整性校验 / `--export-release` 打包
- **CLI 调用模板**（PowerShell，`D:\kudi-project\world-view` 为工程根）：

  ```powershell
  $GODOT = "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
  $ROOT  = "D:\kudi-project\world-view"

  # 1. 打开工程（GUI）
  & $GODOT --path $ROOT

  # 2. 无头校验（CI / agent 自检,验证场景节点与脚本引用正确,不需要显示窗口）
  & $GODOT --headless --path $ROOT --check-only

  # 3. 直接运行主场景（等价 F5,但需要先编译 C#）
  & $GODOT --path $ROOT  # 主场景由 project.godot 的 run/main_scene 指定

  # 4. 导出（ExportDebug / ExportRelease 需先用编辑器导出一份 export_presets.cfg）
  & $GODOT --headless --path $ROOT --export-release "Windows Desktop" "build/WorldView.exe"

  # 5. 版本自检
  & $GODOT --version   # 应输出 4.7.2-stable.mono
  ```

- **路径探测规则**：若上列硬编码路径不可用（机器更换 / Godot 升级），优先在 `C:\App\`、`C:\Program Files\Godot\`、`D:\Godot\`、`%LOCALAPPDATA%` 下扫描 `Godot*.exe`，挑版本号 ≥ `4.7.2` 且文件名带 `mono` 的构建。
- **版本约束**：必须 ≥ 4.7.2 且 mono flavor；**不要**用 `Godot_v4.7.2-stable_win64`（无 C# 支持，运行时所有 C# 脚本会失败）。

#### 6.3.2 .NET SDK 8.0+

- **安装路径**：`C:\Program Files\dotnet\`（默认；如使用 `dotnet --list-sdks` 返回空，需先安装）
- **用途**：`dotnet build WorldView.sln` / `dotnet test` / `dotnet restore` / `dotnet run --project tests/WorldView.Smoke`
- **CLI 调用模板**：

  ```powershell
  $ROOT = "D:\kudi-project\world-view"

  # 1. 构建解决方案（含 C# → .NET 8 → Godot 程序集）
  dotnet build $ROOT/WorldView.sln -c Debug

  # 2. 跑 xUnit 套件（领域层 + 坐标转换）
  dotnet test  $ROOT/WorldView.sln -c Debug --no-build

  # 3. 拉取 NuGet 依赖（YamlDotNet 16.3.0 等）
  dotnet restore $ROOT/WorldView.sln
  ```

- **版本探测**：`dotnet --list-sdks` 应至少包含 `8.0.x`。若仅有 `8.0.0` 之前的预览版，需升级到 8.0.100+ GA。

#### 6.3.3 NuGet 包依赖

- **YamlDotNet 16.3.0**：由 `WorldView.csproj` 的 `<PackageReference>` 声明，`dotnet restore` 自动拉取，无需手动安装；离线环境下需预先 `dotnet restore` 缓存。
- **Godot.NET.Sdk 4.7.0**：同上。
- **不要**在外部安装 yaml / godot 类的系统级工具；所有依赖均通过 NuGet。

#### 6.3.4 Rust 工具链（v0.1 不启用）

- `crates/` 留作可选加速层（M2+ 实测瓶颈再启用）；v0.1 不需要 Rust 工具链可正常工作。
- 若后续启用，需安装 `rustup` + `cargo`，并把 `src-tauri/` 风格的入口重新接入（详见 [.trae/m1-cleanup-manifest.md §2](.trae/m1-cleanup-manifest.md) 关于 `crates/` 保留的说明）。

> **依赖变更纪律**：任何外部依赖的新增 / 移除 / 版本切换，需在本节更新路径与版本号，并同步 [技术基调确认_v0.1.md §10 §13](docs/技术基调确认_v0.1.md) 变更记录。Godot 主版本升级需经总体设计评审。

## 7. 改动检查清单

- [ ] 是否违反了第 2 节的七条设计原则？（尤其「YAML 为主格式，二进制仅限大网格」与「单机无服务器」）
- [ ] 框架专有概念是否只出现在「实现」小节，未污染《数据模型规格》的字段定义？
- [ ] 大网格是否同时提供 YAML 声明与《二进制格式规格》约定的二进制块？是否误把元数据写进了二进制？
- [ ] 新增字段是否已写入《数据模型规格》并带 YAML 示例？跨文件引用是否遵守存储规范的引用约定？
- [ ] 新增地图类型是否登记进《地图类型规格》？新增图层是否登记进《图层系统规格》的图层清单，并声明时间有效性与是否参与贴合/裁剪？
- [ ] 新增算法阈值是否可配置、是否标注了 `[待标定]`？
- [ ] 是否在 v0.1 范围外？（分支时间线 / 第三方插件 / 协作）

## 8. 沟通与回复纪律

- **语言**：始终用中文思考、用中文回复。
- **内容**：只给必要内容，简洁直接。
- **禁用**：寒暄、客套、敬语、道歉、感谢、祝福、铺垫、免责声明、重复问题、无关解释。
- **禁止**：不主动总结、不主动扩展、不问无关问题。
- **句式**：能一句说清就一句；需列点用最短条目。
- **过程**：除非用户明确要求，否则不解释过程。
