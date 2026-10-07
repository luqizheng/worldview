# World View

> 双层世界地图编辑器 — **Tauri (Rust) + React + PixiJS v8** 桌面应用。

`World View` 是一个桌面端地图编辑器：

- **世界层** — 64×48 网格，艾瑟兰大陆风格的色块地形 + 城市标记；
- **城市层** — 64×64 网格，进入建筑 / 单元级编辑。

MVP 阶段聚焦核心数据契约层（YAML）+ PixiJS v8 渲染管线 + Tauri 桌面壳；人物 / 时间轴等模块按 spec 推到 M5 之后。

详细规范见 [`docs/`](./docs/)（设计文档 v0.1、数据模型规格、验收记录等）；任务定义见 [`.trae/specs/map-editor-mvp/`](./.trae/specs/map-editor-mvp/)。

---

## 快速上手

### 0. 前置依赖

| 工具 | 版本 | 说明 |
|---|---|---|
| Rust | ≥ 1.77（推荐 1.97） | `rustup default stable` |
| Node | ≥ 20（推荐 24.x） | 用 [nvm-windows](https://github.com/coreybutler/nvm-windows) 切换 |
| pnpm | ≥ 11 | `npm i -g pnpm` |
| Windows | MSVC Build Tools + Edge WebView2 | macOS / Linux 也可，命令一致 |

> 注：`cargo` workspace 根只包含两个领域 crate；`src-tauri` 独立成 crate 通过 path dep 引用，避免 Tauri 工具链 resolver 冲突（spec §T1.2）。

### 1. 拉取与安装

```bash
git clone <repo>
cd world-view
pnpm install
```

### 2. 验证领域层（30 秒）

```bash
cargo test --workspace
```

预期：24 / 24 ✅（17 core + 7 io）。

### 3. 验证前端类型（10 秒）

```bash
pnpm typecheck
```

预期：零错误。

### 4. 跑端到端冒烟脚本（10 秒）

```bash
pnpm run smoke
```

预期：9 / 9 ✅（覆盖 G3 / G4 / K1 / J2 等 spec §7 demo 点）。

### 5. 启动 demo 桌面应用

**方式 A — 直接跑已编译二进制**（最快）

```bash
# 在已编译过 `pnpm tauri build --debug` 的机器上：
src-tauri/target/debug/world-view.exe          # Windows
# 或对应的 macOS / Linux 二进制
```

**方式 B — 启 dev server（带 dev-tools）**

```bash
pnpm tauri dev
```

应用窗口弹出后（默认 1280×800）：

1. 顶栏 → **「打开工程」** → 弹系统原生文件夹选择器 → 选 `examples/sample_world/`
   （Tauri 桌面端走 `pick_directory` 命令；纯浏览器预览则 `window.prompt` 让你手敲路径）
2. 中央渲染 64×48 世界地图，山地 / 河流 / 森林色块 + 「极光港」标记
3. 点击「极光港」 → 城市卡 → **进入编辑**
4. 顶栏 **ToolPanel** 切笔刷 → 在网格拖动 → 改地形
5. `Ctrl+Z` / `Ctrl+Y` 撤销 / 重做
6. **Save** → 写回 `examples/sample_world/maps/aurora_harbor/terrain.yaml`
7. **New City Map** → 选建筑模板 → 写入 `characters/`

详细的 D1–D8 验收对照表见 [`docs/acceptance/map_editor_mvp_check.md`](./docs/acceptance/map_editor_mvp_check.md)。

### 6. 重新编译桌面二进制

```bash
pnpm tauri build --debug       # 15 MB 左右，~30s（增量）
# 或
pnpm tauri build               # release，最小且最优
```

---

## 仓库布局

```
world-view/
├── crates/                # Rust workspace
│   ├── world-view-core/   # 领域模型（Project / Map / Layer / Terrain / Character / 校验）
│   └── world-view-io/     # YAML 读写 + 路径校验 + 自带 tempfile 测试夹具
├── src-tauri/             # Tauri 主进程壳（独立 crate，避免工具链冲突）
│   ├── src/lib.rs         # #[tauri::command] open_project / save_project / create_city_map / pick_directory
│   ├── capabilities/      # 默认权限（仅 core:*）
│   └── tauri.conf.json    # 1280×800、CSP、beforeDevCommand 等
├── src/                   # React 前端
│   ├── app/App.tsx        # 应用根
│   ├── render/            # 渲染层：Grid / Terrain / Marker / Building Layer（PixiJS v8）
│   ├── editing/           # 笔刷 / Commands / UndoStack
│   ├── ui/                # TopBar / ToolPanel / LayerPanel / Inspector / StatusBar / Dialogs
│   ├── state/             # zustand stores
│   └── bridge/tauri.ts    # hasTauri() 探测 + mock fallback（无 Tauri 时也能 demo）
├── examples/sample_world/ # 示例工程「艾瑟兰大陆」
├── docs/                  # 设计文档 + 数据模型 + 验收记录
├── .trae/specs/map-editor-mvp/  # MVP 任务定义（spec / checklist / tasks）
└── package.json           # pnpm workspace
```

---

## 关键设计原则（v0.1 收敛）

1. **Tauri + Rust + TS 前后端分工**：Rust 持有领域模型与 IO，TS 只做渲染 / UI / 编辑，不持有真源。
2. **PixiJS v8 渲染**：WebGL2 优先、WebGPU 兜底、Canvas 兜兜底；网格 + 矢量 + 二进制分块三态（`encoding: grid | sparse`，chunked 暂留）。
3. **数据契约**：`project.yaml` + `maps/<kind>/<name>.yaml` + `characters/<name>.yaml` 三层；MVP 不引数据库。
4. **校验前置**：保存前 `world-view-core::validate` 跑全套规则（ID 唯一、cells_ref 可达、坐标范围、路径越界）。
5. **mock 兜底**：`src/bridge/tauri.ts` 在浏览器无 Tauri 时走 mock，让任何机器都能跑前端 demo。
6. **示例工程是契约样例**：`examples/sample_world/characters/` 不进 UI，是 M5 的契约参考。
7. **不引外网依赖**：所有数据 / 资源本地化。

---

## 已知约束与排期

- **MVP 不支持 chunked 二进制分块**：`encoding: chunked` 在 `world-view-io::save_project` 显式拒绝；待 M3+ 引入。
- **人物编辑面板 / 时间轴联动 / 关系网**：M5+ 才上 UI；当前数据契约已就绪。
- **样例工程是中文 / 东方奇幻风格**：`examples/sample_world/` 的人物与地名属于"虚构数据"演示。

---

## 测试与验收

```bash
cargo test --workspace        # Rust 领域层单测
pnpm typecheck                # 前端 TS 类型
pnpm run smoke                # Node 冒烟脚本（领域端到端）
pnpm tauri build --debug      # 桌面二进制
```

每次 PR 之前应保证：领域层 24/24、typecheck 0 错、smoke 9/9。完整验收记录见 [`docs/acceptance/map_editor_mvp_check.md`](./docs/acceptance/map_editor_mvp_check.md)。

---

## 许可

仅限项目内部使用。
