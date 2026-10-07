# 地图编辑器 MVP 验收记录

> 本文档汇总 World View MVP 各 demo 与端到端测试结果。
> 录屏/截图与详细日志在 `assets/` 子目录（保留供回溯，不入版本库大文件）。
> **附件约束**：所有录屏/截图不放在 `examples/sample_world/` 内（spec §B6），
> 以免被 `open_project` 误扫。

## 1. 自动验证（CI / 评审入口）

### 1.1 Rust 领域层单测
```
cd crates/world-view-core && cargo test
```
**结果**：17 / 17 ✅
- `validate` 5 条规则 + ID 跨类型唯一 + cells_ref 可达 + sparse 坐标范围/唯一
- `terrain` grid↔sparse 互转
- `character` YAML roundtrip + sample lin_yu 解析

```
cd crates/world-view-io && cargo test
```
**结果**：7 / 7 ✅
- load_project 加载示例工程（2 maps）+ save_project roundtrip
- cells_ref 不可达时报错
- create_city_map 生成文件 + 同步 map_index
- save 校验：city_ref.map_ref 不可达阻止写盘
- build_default_city_map 默认字段齐全
- chunked encoding 拒绝

### 1.2 端到端冒烟脚本（Node）
```
pnpm run smoke
# 或：node --experimental-strip-types examples/sample_world/_smoke/run.ts
```
**结果**：9 / 9 ✅
- G3 switchMap('maps/city/port_aurora.yaml') → currentMap.type === 'city'
- G4 create_city_map 默认字段齐全（grid 64×64 / cell_distance=2m / time granularity=hour / layers=[terrain, road]）
- K1 'encoding: chunked' 命中 0 条；*.bin 文件命中 0 条
- K1 terrain_data.encoding 全部 ∈ {grid, sparse}
- J2 1024×1024 + 128×128 视口 → getDrawnCellCount=4356 ≤ 4915 (64×64×1.2)

### 1.3 前端类型校验
```
pnpm typecheck
```
**结果**：0 错 ✅

## 2. 手动验收 demo（D1–D8，spec §7）

> **当前沙盒限制**：MSVC Build Tools 未安装，无法 `pnpm tauri dev`。
> 验收 demo 在用户本机（已安装 MSVC + Tauri CLI）执行；下方记录「手动验收脚本」。

| # | demo | 验收条件 | 用户本机状态 |
|---|---|---|---|
| D1 | 启动 + 打开示例工程 + 世界地图渲染 | 64×48 网格 + 山地 / 河流 / 森林色块 + 极光港标记 | ⬜ 待用户验收 |
| D2 | 滚轮缩放 / 拖拽 / F / G | 视口响应正确，坐标显示随指针更新 | ⬜ 待用户验收 |
| D3 | 圆形笔刷 + mountain + 拖拽 | 山地刷出色阶 | ⬜ 待用户验收 |
| D4 | Ctrl+Z / Ctrl+Y | 山地区域恢复 / 重画 | ⬜ 待用户验收 |
| D5 | 点击极光港 → 城市卡 → 进入编辑 | 切到城市地图，面包屑更新 | ⬜ 待用户验收 |
| D6 | 选建筑类型 + 拖拽 + 属性对话框 | building_refs 追加条目 | ⬜ 待用户验收 |
| D7 | 关闭 + 重开同一目录 | 修改保留（sparse 编码写出正确） | ⬜ 待用户验收 |
| D8 | 删 port_aurora.yaml → 保存 | 校验器报错，写盘取消，错误对话框列出 `map_ref 不可达` | ⬜ 待用户验收 |

## 3. 验收脚本（用户本机）

```powershell
# 1) 编译 + 单测
cd crates/world-view-core; cargo test; cd ../world-view-io; cargo test; cd ../..

# 2) 前端类型校验
pnpm typecheck

# 3) 冒烟脚本
pnpm run smoke

# 4) 启动 Tauri 应用
pnpm tauri dev
# 弹出窗口 → 顶栏「打开工程」→ 输入 examples/sample_world/ → 进入

# 5) 逐项验收 D1–D8
# （按本节表格顺序；每项通过后在本节勾选 ⬜ → ✅）
```

## 4. 已知挂起（待用户本机验收）

| 项 | 原因 | 状态 |
|---|---|---|
| `pnpm tauri dev` 启动 | 当前沙盒无 MSVC | 用户安装 MSVC 后即可 |
| D1–D8 手动验收 | 需要实际窗口交互 | 用户验收 |
| 视口缩放/拖拽帧率（J1 ≥ 30 fps） | 需要真实 WebView 跑分 | ⬜ |

## 5. 变更记录

| 版本 | 变更 |
|---|---|
| v0.1 | MVP 首版验收：领域层 24 单测全过 + 冒烟 9 项全过 + 前端 TS 0 错 |