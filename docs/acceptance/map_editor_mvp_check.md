# 地图编辑器 MVP 验收记录（Godot mono 版）

> **2026-10-09 反转公告**：本文件原 v0.1–v0.3（Tauri + TypeScript + PixiJS）时代的验收记录已**整体作废**——编译链、单测入口、冒烟脚本、桌面二进制产物路径、Demo 启动方式全部不适用于新栈（**Godot 4.7.2 mono + C# + Godot 内置 2D**）。
>
> 本文档按 P0-1 / P0-2 反转后的新栈**重新搭骨架**：保留与栈无关的测试用例语义（如 `switchMap` / `create_city_map` / `encoding: chunked` 命中 0 等纯数据契约校验），替换执行命令为 dotnet / Godot 命令。**M1 真实跑通后**,由用户在对应章节填实际命令输出。
>
> 历史验收数据（Tauri exe 路径 / WiX / NSIS / `tauri-plugin-dialog` 修复等）已**不具备回溯价值**,不保留占位字段,避免后续误读。

---

## 1. 自动验证（CI / 评审入口）

> 本节是 Godot mono 时代的验收入口；执行命令与产物路径待 M1 落地后填入。
> 历史版本（v0.1–v0.3 / Tauri）已作废,不复引用。

### 1.1 C# 领域层单测（dotNET）

```
dotnet test WorldView.Domain.sln
# 或：dotnet test --filter "Category!=Integration"
```

**结果**：__ / __ ⬜ 待填

- `WorldView.Domain`（领域层纯逻辑,不依赖 Godot）：覆盖 `model` / `character` / `terrain` / `validate` 四组,包含 spec §A2 sparse 坐标范围+唯一、§A3 cells_ref 不可达、§A4 楼宇内部 ref 不可达、§A5 城市 ref 不可达、§A7 版本与 ID 唯一、§A8 路径越界、§A10 YAML roundtrip 等。
- `WorldView.Storage`（存储层）：`load_and_save_roundtrip` / `create_city_map_writes_files_and_updates_index` / `build_default_city_map_has_required_defaults` / `serialize_terrain_sparse_only_writes_diffs` / `chunked_encoding_is_rejected` / `save_rejects_invalid_terrain_grid_size` / `load_fails_on_missing_cells_ref`。
- 编译耗时：⬜ 待填。

> **纪律**：领域层测试**不引用 `Godot.*` 命名空间**,确保可脱离 Godot 引擎运行；测试失败时定位到文件与行（与 [总体设计 §3](总体设计_v0.1.md) 分层约束对齐）。

### 1.2 C# / Godot 编译校验

```
dotnet build WorldView.sln -c Debug
# Godot 4.7.2 mono 工程本身用 project.godot 描述；.NET 解决方案由 WorldView.sln 驱动
```

**结果**：0 错 0 warning ⬜ 待填

覆盖：领域层 `WorldView.Domain`、存储层 `WorldView.Storage`、UI 控件 `WorldView.Ui`、编辑器 `WorldView.Editor`、Godot 场景 `WorldView.GodotNodes`。

### 1.3 Godot 编辑器导出资源

```
# Godot 4.x 无独立 "build" 步骤；.tscn / .tres 资源的有效性由编辑器打开时校验
# CI 中可用 headless 模式做语法/引用校验：
godot --headless --check-only --quit-after 1 project.godot
```

**结果**：⬜ 待填

涵盖：主界面 `scenes/main.tscn`、地图编辑器 `scenes/MapEditor.tscn`、时间轴 `scenes/Timeline.tscn`、属性面板 `scenes/PropertyPanel.tscn`、`TileSet` 资源（地形 / 建筑 / 植被）、`TileMap` 引用图集。

### 1.4 端到端冒烟脚本（C#）

```
dotnet run --project tests/WorldView.Smoke -- examples/sample_world/
# 或 Godot headless 跑：godot --headless --script tests/SmokeRunner.cs examples/sample_world/
```

**结果**：__ / __ ⬜ 待填

测试用例（沿用 v0.1 数据契约语义,与栈无关）：

- G3 `switchMap('maps/city/port_aurora.yaml')` → `currentMap.type === 'city'`
- G4 `create_city_map` 默认字段齐全（`grid 64×64` / `cell_distance=2m` / `time granularity=hour` / `layers=[terrain, road]`）
- K1 `encoding: chunked` 命中 0 条；`*.bin` 文件命中 0 条
- K1 `terrain_data.encoding` 全部 ∈ {`grid`, `sparse`}
- J2 `1024×1024` + `128×128` 视口 → `getDrawnCellCount=4356 ≤ 4915 (64×64×1.2)`

### 1.5 桌面端可执行（Godot mono `WorldView.exe`）

```
# Godot 桌面导出（编辑器菜单 Project → Export → 选 Windows Desktop）
# 或命令行：
godot --headless --export-release "Windows Desktop" build/WorldView.exe
```

**结果**：⬜ 待填

产物清单（M1 跑通后填实际路径与大小）：

| 产物 | 路径 | 大小 | 写入时间 |
|---|---|---|---|
| 主可执行 | ⬜ | ⬜ | ⬜ |
| .NET 程序集 | ⬜ | ⬜ | ⬜ |
| TileSet / 资源包 | ⬜ | ⬜ | ⬜ |

二进制形态：Godot 4.7.2 mono 桌面导出产物，依赖同目录 `.NET` 运行时（自包含或系统已装均可），用户双击 `WorldView.exe` 直接运行。

---

## 2. 手动验收 demo（D1–D8，spec §7）

> **运行环境**：本节描述用户本机在 M1 跑通后的验收脚本与预期现象；启动方式以 Godot mono 工程为准。
>
> **两种启动方式**：
> - 方式 A（直接跑 demo 二进制）：双击 `build/WorldView.exe`（M1 落地后填实际路径）。
> - 方式 B（带 dev-tools）：`godot --path . scenes/main.tscn`，编辑器 inspector / remote tree 可开。

| # | demo | 验收条件 | 自动化 / 用户状态 |
|---|---|---|---|
| D1 | 启动 + 打开示例工程 + 世界地图渲染 | 64×48 网格 + 山地 / 河流 / 森林色块 + 极光港标记（`Node2D` 树 + `TileMap` 渲染） | ⬜ 待用户验收 |
| D2 | 滚轮缩放 / 拖拽 / F / G | 视口响应正确，坐标显示随指针更新 | ⬜ 待用户验收 |
| D3 | 圆形笔刷 + mountain + 拖拽 | 山地刷出色阶（`CanvasItem` 脏区重绘） | ⬜ 待用户验收 |
| D4 | Ctrl+Z / Ctrl+Y | 山地区域恢复 / 重画（命令栈 C# 实现） | ⬜ 待用户验收 |
| D5 | 点击极光港 → 城市卡 → 进入编辑 | 切到城市地图，面包屑更新（`anchor` 跨图跳转） | ⬜ 待用户验收 |
| D6 | 选建筑类型 + 拖拽 + 属性对话框 | `building_refs` 追加条目（`Sprite2D` + 吸附） | ⬜ 待用户验收 |
| D7 | 关闭 + 重开同一目录 | 修改保留（sparse 编码写出正确） | ⬜ 待用户验收 |
| D8 | 删 `port_aurora.yaml` → 保存 | 校验器报错，写盘取消，错误对话框列出 `map_ref 不可达` | ⬜ 待用户验收 |

---

## 3. 验收脚本（用户本机）

```powershell
# 1) .NET 编译 + 单测
cd D:\kudi-project\world-view
dotnet test WorldView.Domain.sln

# 2) Godot 工程语法校验（headless）
godot --headless --check-only --quit-after 1 project.godot

# 3) 冒烟脚本
dotnet run --project tests/WorldView.Smoke -- examples/sample_world/

# 4) 启动 Godot mono 应用（二选一）
#    方式 A：跑 M1 编译好的 demo 二进制
".\build\WorldView.exe"

#    方式 B：自己起 dev（带 inspector / remote tree）
godot --path . scenes/main.tscn
# 弹出窗口 → 顶栏「打开工程」→ 输入 examples/sample_world/ → 进入

# 5) 逐项验收 D1–D8
# （按本节表格顺序；每项通过后在本节勾选 ⬜ → ✅）
```

---

## 4. 已知挂起（待用户本机验收）

| 项 | 原因 | 状态 |
|---|---|---|
| D1–D8 手动验收 | 需要实际窗口交互（人类操作） | 用户验收 |
| Godot 视口缩放/拖拽帧率（J1 ≥ 30 fps） | 需要真实 GPU 跑分 | ⬜ |
| Godot 桌面导出模板（`Windows Desktop` preset） | M1 须先在编辑器里配置一次 `export_presets.cfg` | 不阻塞 demo |

---

## 5. 变更记录

| 版本 | 变更 |
|---|---|
| v0.1 | MVP 首版验收：领域层 24 单测全过 + 冒烟 9 项全过 + 前端 TS 0 错（Tauri 栈） |
| v0.2 (2026-05-29) | 重新执行（Tauri 栈）：① `cargo test --workspace` 24/24 ✅；② `pnpm typecheck` 0 错；③ `pnpm build` 11 chunks 已生成（455 KB / gzip 144 KB）；④ `pnpm tauri build --debug` 生成 `world-view.exe`（15.25 MB）；⑤ 修订 capabilities（移除不存在字符串，验证 Tauri 2 仅识别 `core:*`）；⑥ 修复 sandbox 拦截 `target/.cargo-build-lock` 与 `%TEMP%\esbuild-*` 的访问。 |
| v0.3 (2026-10-07) | **D1「打开工程」按钮无反应修复（路线 2）**：① 失败尝试：引入 `tauri-plugin-dialog` + `@tauri-apps/plugin-dialog`，但 sandbox 网络层拦截 crates.io / tuna / ustc / rsproxy 全部 443 出站，无法拉新依赖；② 改用路线 2：自实现 Rust 端 `#[tauri::command] pick_directory()`（`std::process::Command` 调 PowerShell `FolderBrowserDialog`），零新 crate 依赖；③ 前端 `src/bridge/tauri.ts` 新增 `pickDirectory(current)`；④ `src/ui/TopBar.tsx` 改用 `bridge.pickDirectory()`，保留文本框+回车提交作为降级路径；⑤ 还原 Cargo.toml（删 `tauri-plugin-dialog`）、capabilities/default.json（删 `dialog:default`）、package.json（删 `@tauri-apps/plugin-dialog`）、`lib.rs:run()`（删 `.plugin(tauri_plugin_dialog::init())`）；⑥ 重新跑通：`cargo test --workspace --offline` 24/24 ✅、`pnpm typecheck` 0 错 ✅、`pnpm tauri build --debug` 成功生成主二进制（15.02 MB）+ MSI（4.91 MiB）+ NSIS（2.96 MiB）。 |
| **v0.4 (2026-10-09) — 整体重写（栈反转）** | 用户决议放弃 Tauri + TypeScript + PixiJS,改 Godot 4.7.2 mono + C# + Godot 内置 2D。本文件按新栈**重新搭骨架**：① §1 自动验证章节命令替换为 `dotnet test` / `godot --headless --check-only` / `dotnet run --project tests/WorldView.Smoke` / `godot --export-release`；② §2 D1–D8 描述改为 Godot 节点 / `CanvasItem` / 命令栈 / `Sprite2D` 措辞；③ §3 用户本机验收脚本替换为 `dotnet` + `godot` 命令；④ §6、§7（Tauri sandbox 修复与 WiX / NSIS 下载）整段删除（与新栈无关）。**v0.1–v0.3 全部历史记录仅作为「栈反转前的存档」保留**,不作为新栈验收依据。M1 真实跑通后,由用户在对应章节填实际命令输出与产物大小。 |
