# Checklist — 地图编辑器 MVP

> 验收检查清单；每条对应 spec.md §1.1 的一条 IN MVP 能力，或 §7 的一条 demo。
> 由实现完成后逐条核验；通过的项在 `- [ ]` 上打 `[x]` 并附验证记录（命令 / 截图路径 / 时间戳）。

---

## A. 工程骨架与领域层

- [ ] A1. **Workspace 结构（src-tauri 独立）**：
  - 仓库根 `Cargo.toml` 为 workspace 根，`members = ["crates/world-view-core", "crates/world-view-io"]`（**不含** `src-tauri`，避免与 Tauri 工具链冲突）
  - `src-tauri/Cargo.toml` **保持独立 crate**，不写 `[workspace]` 段
  - `src-tauri/Cargo.toml` 通过 `[dependencies] world-view-core = { path = "../crates/world-view-core" }` 与 `world-view-io = { path = "../crates/world-view-io" }` 引入两个领域 crate
  - 两个领域 crate **不依赖** `tauri`（仅 `serde_yaml` / `serde` / `thiserror`），保证纯逻辑可单测
- [ ] A2. `pnpm tauri dev` 启动空白 Tauri 窗口成功
- [ ] A3. `world-view-core` 含 `Project` / `Map` / `Layer` / `TerrainCell` / `CityRef` / `BuildingRef` 类型，对应数据模型规格 MVP 子集
- [ ] A4. `terrain_data` 支持 `grid` ↔ `sparse` 序列化往返，且单测覆盖以下三种场景：
  - 全默认值的 grid → sparse 应只剩默认值声明
  - 含 50 个非默认单元的 grid → sparse → 反向解回 grid 数值一致
  - sparse（无 `default`）→ grid 自动用 `Default::default()` 填充
- [ ] A5. `validate(&Project)` 覆盖 spec §3 Requirement: 校验器 五条规则且单测通过
- [ ] A6. **Commands 注册**：
  - `src-tauri/src/lib.rs` 中 `tauri::generate_handler![open_project, save_project, create_city_map]`
  - `src-tauri/capabilities/default.json` 中：
    - `permissions` 数组含 `core:default` + 自定义 `world-view:allow-open-project` / `world-view:allow-save-project` / `world-view:allow-create-city-map`
    - `fs.scope` 允许对所选工程目录（含 `examples/sample_world/` 及未来用户目录）下任意子路径的 `readFile` / `writeFile` / `readDir` / `mkdir`
  - `src-tauri/tauri.conf.json` 的 `app.security.csp` 允许 `connect-src` 含 `ipc://localhost` 与 `http://ipc.localhost`（Tauri v2 IPC 默认）

## B. 示例工程

- [ ] B1. `examples/sample_world/project.yaml` 存在且 `project_version = "0.1"`
- [ ] B2. `examples/sample_world/maps/world/main.yaml` 含 `极光港` 城市条目
- [ ] B3. `examples/sample_world/maps/world/main.terrain.yaml` 含明确地形分布：**至少 1 片 `mountain`（≥ 20 格连片）、1 条 `river`（≥ 8 格折线或直线）、其余 `plain`**；`move_cost` / `buildable` 字段按数据模型规格 §3.2 默认值填
- [ ] B4. `examples/sample_world/maps/city/port_aurora.yaml` 存在，`building_refs` 为空
- [ ] B5. `examples/sample_world/characters/lin_yu.yaml` 存在，`residence.map_ref` 指向城市地图；**MVP 不加载到 UI store**，仅 `world-view-core` 单测 `character_yaml_roundtrip` 通过
- [ ] B6. 加载示例工程的录屏/截图保留为 **`docs/acceptance/map_editor_mvp_check.md`** 附件（**不放**在 `examples/sample_world/` 内，避免被 `open_project` 误扫）
- [ ] B7. `examples/sample_world/README.md` 存在，首段含「人物 / 事件为数据契约样例，MVP 不进 UI」声明

## C. 主界面与视图

- [ ] C1. 应用启动后用户能看到顶栏 / 左工具栏 / 中央画布 / 右面板 / 状态栏五段布局（按界面规格 §1）
- [ ] C2. 状态栏显示面包屑、当前地图名、比例尺、当前鼠标所在网格坐标
- [ ] C3. 滚轮缩放围绕指针焦点，缩放比例 clamp 到 [0.25, 8.0]
- [ ] C4. 鼠标交互与快捷键：
  - 左键拖拽 → 平移（中键 / 右键亦等效）
  - 滚轮 → 围绕指针焦点缩放
  - `F` → 全图适配
  - `G` → 切换网格显示
  - `C` → 切换坐标显示
  - `Ctrl+S` → 触发保存（无论焦点位置）
  - `Ctrl+Z` / `Ctrl+Y` → 撤销 / 重做
  - `Esc` → 取消当前工具，回到 pan
  - 快捷键在画布聚焦时生效；非画布聚焦时不响应（不抢占浏览器默认行为）

## D. 图层面板

- [ ] D1. 右图层面板列出当前地图所有 `layers[]`，每行含可见性 / 锁定 / 透明度 / z_order 控件
- [ ] D2. 取消勾选可见性 → 地形立即消失；恢复勾选 → 立即重现
- [ ] D3. 修改透明度 → 渲染层实时响应

## E. 笔刷与撤销

- [ ] E1. 圆形笔刷可调 size（1–16）/ 地形类型 / strength，落点吸附网格
- [ ] E2. 拖拽期间按 **100ms 节流**（`requestAnimationFrame` + 时间戳门控；按住移动每秒约落 10 个 stroke 中心，落点之间不补插值）落点；松开即提交一条完整 `ApplyBrushCommand`
- [ ] E3. `Ctrl+Z` 至少可回退 50 步；`Ctrl+Y` 重做；新操作清空 redo 栈
- [ ] E4. 笔刷命令的 `revert()` 把受影响单元精确恢复成落点前的值（手改地形后撤销可见差异）

## G. 城市下钻与导航

- [ ] G1. 世界地图上能看到「极光港」圆形标记 + 文字
- [ ] G2. 点击标记弹出基础信息卡（名称 / 人口 / 势力 / 备注）
- [ ] G3. 点击「进入编辑」→ 切到城市地图；面包屑更新；画布重绘
- [ ] G4. 点击「创建城市地图」（`map_ref` 为空场景）→ 自动生成 `maps/city/<id>.yaml` 后下钻
- [ ] G5. 面包屑点击任一段可跳回该层级（至少两级：世界 / 城市）
- [ ] G6. **冒烟测试**：在 `examples/sample_world/_smoke/` 提供端到端冒烟脚本
  - G3 路径：通过 IPC 直接调 `invoke('open_project')` 加载示例工程 → 直接调领域层 `switchMap('maps/city/port_aurora.yaml')` → 断言 `breadcrumb.length == 3` 且当前地图 `type == "city"`
  - G4 路径：准备一份「`city_refs[0].map_ref` 为空」的临时工程 → 调 `invoke('create_city_map')` → 断言生成的 YAML 文件存在且 `project.map_index` 已同步
  - MVP **不强制要求** Playwright；冒烟脚本以 Node 直跑（无浏览器），覆盖核心状态机即可；GUI 点击路径在 D1–D8 录屏中手动验收

## H. 建筑放置

- [ ] H1. 工具栏切到「放置建筑」+ 选类型 → 进入占地拖拽状态
- [ ] H2. 拖拽松开弹出属性对话框（`name` 必填 / `interior_ref` 可选）
- [ ] H3. 确认后 `building_refs` 追加一条；建筑以矩形 + 文字渲染；当建筑占地跨越 1024 格上限时**提示**「MVP 未启用 `chunked`，建议把城市地图缩到 1024×1024 以内或使用 sparse 编码」，**不阻塞**保存
- [ ] H4. 取消属性对话框不入撤销栈

## I. 保存与校验

- [ ] I1. **保存语义区分**：
  - 「保存」点击 → 若校验全过：写回所有 dirty 地图 + 清空 `dirtyMapIds` + 显示「已保存」
  - 若校验失败：写盘动作**取消**；`dirtyMapIds` **保留**；弹错误对话框（`ErrorDialog`，逐条展示 `ValidationError`，可点复制纯文本）
  - 「保存」按钮在 `dirtyMapIds` 为空时置灰（无变更不写盘，避免误覆盖）
- [ ] I2. 删除 `port_aurora.yaml` 后点保存 → 校验器报错，写盘取消；`city_refs[i].map_ref` 在错误信息中显式指明
- [ ] I3. ID 重名时校验报错并阻止保存
- [ ] I4. `terrain_data.cells` 行/列与 `grid.width/height` 不一致时校验报错

## J. 性能与体验

- [ ] J1. 笔刷拖拽期间帧率 ≥ 30 fps（开发者工具实测）
- [ ] J2. **视口裁剪量化**：
  - 测试地形：**1024×1024 全平原地图**（人工生成的边界用例，与示例工程分开存放）
  - 测试视口：128×128 像素 + `scale=2.0`（即覆盖 64×64 网格）
  - 断言：`TerrainLayer` 内 `Graphics` 对象**只绘制**视口覆盖的 64×64 单元；`getSpritesCount()` ≤ `64 × 64 × 1.2 = 4915`（1.2 为边带冗余）
  - 验证手段：在 `TerrainLayer` 暴露 `getDrawnCellCount(): number`，由冒烟脚本读取并断言
  - 若未实现 cull，断言会失败 → 强制落实视口裁剪
- [ ] J3. 应用启动到示例工程首屏 ≤ 2 秒（Release 构建）

## K. 范围纪律（再次确认 MVP 边界）

- [ ] K1. **`chunked` 严格排除**：
  - `examples/sample_world/` 及其所有子目录**不存在**任何 `*.bin` 文件
  - 所有 `terrain_data.encoding` 与 `height_data.encoding`（如有）**均为** `grid` 或 `sparse`
  - 冒烟脚本扫一遍工程目录，断言 `grep -r 'encoding: chunked'` 命中 0 条
  - 任何 `chunked` 字段出现 = 拒收（违反 MVP 边界）
- [ ] K2. **未实现** `building` 类型地图的下钻（点击带 `interior_ref` 的建筑只展示属性，不切到室内图）
- [ ] K3. **未实现** 时间轴联动 / 回放 / 快照 UI
- [ ] K4. **未实现** 运输 / 路线 / 载具面板
- [ ] K5. **未实现** 疆界自动贴合 / 影响力裁剪 / 居民生成
- [ ] K6. **未引入** 第三方插件 / 分支时间线 / 协作