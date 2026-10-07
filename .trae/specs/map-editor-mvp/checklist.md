# Map Editor MVP — 验收清单(Checklist)

> 栈:Godot 4.7.2(mono)+ C# (.NET 8)+ YamlDotNet 16.3.0。
> 验收方式:逐项勾选 ✅;每项都可以用一个具体命令 / 操作 / 断言来验证。
> 最终合格标准:**A–K 全部 ✅ 且无遗留 ⏸️**。

---

## A. 工程骨架与领域层

- [ ] **A1 Workspace 结构正确**:`tree -L 2` 输出含 `project.godot` `WorldView.sln` `WorldView.csproj` `scenes/` `scripts/` `examples/` `docs/` `crates/` `AGENTS.md` `README.md` `.gitignore`,**不**含 `src/` `src-tauri/` `Cargo.toml` `package.json` `pnpm-lock.yaml` `vite.config.ts`。
- [ ] **A2 Godot 编辑器可打开工程**:Godot 4.7.2 → "Import" → 选 `project.godot` → 无「Project version mismatch」警告。
- [ ] **A3 dotnet build 成功**:`dotnet build WorldView.sln` 退出码 0,无 warning(允许 IDE0005 文件级抑制)。
- [ ] **A4 dotnet test 跑过**:`dotnet test WorldView.sln` 退出码 0(领域层 + 坐标单元测试)。
- [ ] **A5 Domain 层无 Godot 依赖**:`grep -rn "using Godot" scripts/Domain/ scripts/Storage/ scripts/Editing/` 无输出(`scripts/Editing/BrushController.cs` 因需 `Time.GetTicksMsec()` 是唯一例外)。
  - *说明*:旧版本包含 `scripts/Editing/Editing/*.cs` 与 `scripts/Domain/Editing/*.cs` 两个**不存在的路径**(spec.md §6.1 仓库布局未列),已删除避免假阳性通过。
- [ ] **A6 YamlDotNet 16.3.0 引用正确**:`WorldView.csproj` 中 `PackageReference YamlDotNet` 版本 = `16.3.0`,`dotnet restore` 成功。

---

## B. 示例工程

- [ ] **B1 project.yaml 存在**:文件 `examples/sample_world/project.yaml`,YAML 解析通过(无 YamlDotNet 异常)。
- [ ] **B2 工程可加载**:顶栏「文件 → 打开示例工程」点击后,**≤ 5 秒** 进入主场景(D1 验收)。
- [ ] **B3 工程字段合法**:`view_mode="2d"`,`default_unit="m"`,`maps:` 至少 1 项。
- [ ] **B4 至少 1 张世界地图 + 1 张城市地图**:`maps/world/world_kael.yaml`(type="world",grid 64×48)+ `maps/city/city_kael.yaml`(type="city",grid 64×64)。
- [ ] **B5 4 类图层齐全**:`layers/terrain.yaml` / `road.yaml` / `building.yaml` / `marker.yaml`,每张 `type` 字段对应,`visible=true`。
- [ ] **B6 城市标记可下钻**:`maps/world/world_kael.yaml` 中 `city_refs:` 列表 ≥ 1,`map_ref` 指向 `maps/city/city_kael.yaml`。
- [ ] **B7 至少 2 个建筑占位**:`maps/city/city_kael.yaml` 中 `buildings:` 列表 ≥ 2,`type ∈ {house, tower, wall_gate}`,`at` 在 `0 ≤ x,y < 64` 内,`map_id="map_city_kael"`。
- [ ] **B8 老文件名清理**:`maps/world/main.yaml` / `main.terrain.yaml` / `maps/city/port_aurora.yaml` / `port_aurora.terrain.yaml` 4 个文件已废弃为 stub(M1 验收通过后由用户批准 m1-cleanup-manifest.md 实际删除)。

---

## C. 主界面与视图

- [ ] **C1 主场景四区布局**:`scenes/Main.tscn` 实例化后,TopBar / ToolPanel / InspectorPanel / StatusBar / LayerPanel 5 个区域**位置 + 大小** 与 `spec.md §6.1` 一致(TopBar 顶 32 px / StatusBar 底 24 px / ToolPanel 左 64 px / InspectorPanel 右 240 px / 中间余下为画布)。
- [ ] **C2 画布节点树正确**:`WorldCanvas` 子节点含 `Camera2D` + 4 个 `TileMapLayer`(terrain / road / building / marker)+ `GridOverlay`,**节点顺序** 与画布 z 序一致(terrain 在底,marker 在顶)。
- [ ] **C3 键盘平移**:WASD 或方向键按下时,`Camera2D` 位置平移(单位 = `cell_distance × 1`,可通过调试打印验证)。
- [ ] **C4 鼠标交互**:鼠标中键拖拽平移 + 鼠标滚轮缩放(范围 0.25× ~ 4.0×,步长 1.2×)。
- [ ] **C5 状态栏实时刷新**:鼠标移动时,状态栏中间段 `Label.Text` 显示当前坐标(格式 `x: <int>, y: <int>`)。

---

## D. 图层面板

- [ ] **D1 图层 CheckBox 渲染**:`LayerPanel` 启动后,CheckBox 数 = `project.maps[0].layers` 列表长度,每项 label = `layer.name`。
- [ ] **D2 显隐切换即时**:点击某 CheckBox 后,对应 `TileMapLayer.Visible` 在**一个帧内**(≤ 16 ms)翻转。
- [ ] **D3 顺序与 project.yaml 一致**:`LayerPanel` 中 CheckBox 自顶向下顺序 = `project.maps[0].layers` 列表顺序。

---

## E. 笔刷与撤销

- [ ] **E1 工具切换**:工具栏切到「笔刷」后,鼠标在画布上的光标变为「笔刷」状态(自定义 `Cursor` 资源或 `default_cursor = Shape.CURSOR_DRAG`)。
- [ ] **E2 笔刷覆盖范围**:在 (10, 10) 落点 + 半径 1 时,`ApplyBrushCommand` 影响格集合 = 欧几里得圆盘 `{c : sqrt(dx²+dy²) ≤ 1}`,即 5 格:`(10,10) / (9,10) / (11,10) / (10,9) / (10,11)`。
- [ ] **E3 笔刷节流**:1 秒内连续点击 100 次鼠标,实际下笔次数 ≤ 10(100 ms 节流)。
- [ ] **E4 重绘帧率**:笔刷落点后,`dirty_rect` 包围盒内的格子在**一帧内** 重绘完毕(可通过 `Performance.GetMonitor(Performance.Monitor.TimeFps)` 验证无显著掉帧)。
- [ ] **E5 撤销**:按 `Ctrl+Z` 后,刚刷的格子全部还原为 `default` 地形(`plain`)。
- [ ] **E6 重做**:按 `Ctrl+Shift+Z` 后,撤销前的格子恢复为选定地形。
- [ ] **E7 UndoStack 上限**:连续 push 250 个 `ApplyBrushCommand`,UndoStack 容量 = 200(最旧的 50 个被 FIFO 丢弃)。
- [ ] **E8 撤销 + 重做循环无残留**:xUnit 测试 `for _ in range(100): push + undo;` 后,MapDoc 与初始 state 逐字段相等。

---

## F. 渲染层正确性

- [ ] **F1 11 色地形调色板**:`Render/TerrainPalette.cs` 中 `GetColor(TerrainType type)` 返回的 `Color` 与 `spec.md §2.4` 表中 11 个 hex 完全一致。
- [ ] **F2 TileSet 单一来源**:`Render/terrain.tres` 存在,被 4 个 TileMapLayer 共用,切换地形不重建 TileSet。
- [ ] **F3 坐标转换互逆**:xUnit 测试 `CoordTransform.WorldToCell(CellToWorld(c)) == c` 全部通过(GridOverlay 与 WorldCanvas 共用)。
- [ ] **F4 视口裁剪**:xUnit 测试 1024×1024 网格 + 128×128 视口下,`WorldCanvas.GetDrawnCellCount() ≤ 4915`(J2 量化断言)。

---

## G. 城市下钻与导航

- [ ] **G1 双击进入**:在城市地图标记上双击,场景切到对应城市地图。
- [ ] **G2 面包屑可见**:顶栏面包屑显示「世界 / Kael」(2 段),自顶向下顺序 = 路径顺序。
- [ ] **G3 点击面包屑回退**:点面包屑「世界」段或按 `Esc`,场景回到上一级地图。
- [ ] **G4 面包屑不直接跳根**:点面包屑中间段(如「Kael」)只回退到 Kael,不会直接跳到根。
- [ ] **G5 城市地图四区一致**:切到城市地图后,ToolPanel / InspectorPanel / LayerPanel 切换为城市地图的内容。
- [ ] **G6 双击城市地图标记**:在 `city_kael.yaml` 中的 marker 上双击,提示「已在最深层」或更早一步(实现允许处理)。

---

## H. 建筑放置

- [ ] **H1 工具切换**:工具栏切到「建筑」后,光标变为「可放置」状态。
- [ ] **H2 非城市地图禁用**:在世界地图(`type="world"`)下,「建筑」工具为 `Disabled` 状态。
- [ ] **H3 落点约束**:在 `terrain.buildable=false` 的格子上点击时,状态栏文字显示「此处不可放置建筑」(无弹窗)。
- [ ] **H4 落点成功**:在 `terrain.buildable=true` 的格子上点击时,弹「建筑占位」对话框,确认后建筑出现在 `at` 处。
- [ ] **H5 建筑登记**:`MapDoc.Buildings` 列表新增 1 项,`id` 8 字符 UUID,`type` 取自工具栏,`map_id` 等于当前城市地图 id。

---

## I. 保存与校验

- [ ] **I1 保存回写**:点击「保存」按钮,所有被修改文件按原路径落盘(可 `git status` 验证)。
- [ ] **I2 重新打开字段逐一对齐**:保存 → 关闭 → 重新打开 → `MapDoc` 与保存前逐字段相等(键序可不同)。
- [ ] **I3 「另存为」可用**:顶栏「文件 → 另存为」可写入任意目录,D7 demo 验证。
- [ ] **I4 启动校验失败弹错误对话框**:手动把 `maps/city_kael.yaml` 的 `grid.cols` 改成 `-1` → 重启 → 弹错误对话框,显示 `maps/city_kael.yaml / map.grid.cols / enum_out_of_range`,渲染被阻止。
- [ ] **I5 错误对话框可关闭**:点「关闭」后,主场景回到空状态,**不** 渲染任何地图。
- [ ] **I6 校验覆盖 5 种错误码**:xUnit 测试覆盖 `RequiredMissing` / `EnumOutOfRange` / `RefNotFound` / `IdDuplicate` / `TypeMismatch`。

---

## J. 性能与体验

- [ ] **J1 100 ms 节流验证**:E3 测试已覆盖。
- [ ] **J2 视口裁剪量化断言**(F4 已覆盖,这里再独立验证一次):`tests/Render.Tests/ViewportCullingTests.cs` 通过。
- [ ] **J3 启动时间**:从点 F5 到 D1 主场景就位,**≤ 5 秒**(可在 `Main.cs._Ready` 末尾打 `GD.Print($"Ready in {(Time.GetTicksMsec() - startTicks) / 1000.0:F2}s")` 验证)。

---

## K. 范围纪律(反向断言)

- [ ] **K1 无 chunked 编码**:`grep -rn "encoding.*chunked\|chunked" docs/ examples/ scripts/ scenes/` 仅在 spec.md / 总体设计 / 存储规范 中出现,**examples/sample_world/** 与 **scripts/Domain/** 无 `chunked` 字样。
- [ ] **K2 无 `using Godot;` in Domain**:`grep -rn "using Godot" scripts/Domain/` 无输出(BrushController 例外除外)。
- [ ] **K3 `ref` 不含 `..` 或绝对路径**:`grep -rnE 'ref:\s*"(\.\./|/)' examples/sample_world/` 无输出。
- [ ] **K4 `id` 与 `ref` 不混用**:`grep -rnE 'ref:\s*"[^"]+"' examples/sample_world/` 中,引用 4 类图层与跨文件引用使用 `ref`,地图与建筑与图层自身标识使用 `id`,无混淆。
- [ ] **K5 所有修改走命令模式**:`scripts/Render/` 与 `scripts/Ui/` 中**禁止**调用 `MapDoc.X = ...` 这类直接写入;必须通过 `UndoStack.Push(new ApplyBrushCommand(...))` 等方式。
- [ ] **K6 性能纪律**:不引入 `chunked` / 室内下钻 / 时间轴 / 运输 / 贴合 / 视觉增强 / 插件。

---

## 验收签字

- 全部 A–K 节 ✅ 后,`docs/acceptance/map_editor_mvp_check.md` 末尾签字栏填 `验收日期 / 验收人 / commit SHA`。
- `git tag mvp-v0.1` 标注本 MVP 节点。