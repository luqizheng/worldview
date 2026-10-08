# UI Redesign — SimCity/RTS 验收清单（v0.3）

> 对应 [spec.md](spec.md) §2 / §8 与 [tasks.md](tasks.md)。
> 每条用 `[ ]` / `[x]` 标记，附验收手段（截图 / 录屏 / 命令输出）。

---

## 编号对照表

| D 编号（Demo/回归） | G 编号（Goal） | 说明 |
|---|---|---|
| D1 | G1 | 启动 + 主界面显示 + 画布 ≥ 75%（展开态） |
| D2 | G9 | 切地形 + 笔刷绘制（功能不变） |
| D3 | G9 | Ctrl+Z 撤销（功能不变） |
| D4 | G9 | Ctrl+Shift+Z 重做（功能不变） |
| D5 | G9 | 双击城市下钻 + Esc 返回（功能不变） |
| D6 | G5 / G9 | 放建筑（按钮或拖放均可）；越界弹提示 |
| D7 | G9 | 保存 + 重新打开一致（功能不变） |
| D8 | G9 | 校验失败弹错（功能不变） |
| D9 | G5 | 拖建筑到地图 → 放置 |
| D10 | G3 | 拖面板到四边 → 停靠 |
| D11 | G4 | 拖到中央 → 浮动 |
| D12 | G7 | F11 全屏切换 |
| D13 | G6 | 布局持久化 |
| D14 | — | 分页器按需出现 / 隐藏 |
| D15 | — | 切换分类刷新网格 + 页码还原 |
| D16 | — | `cell_size` 改动 → 立即重排 |
| D17 | G10 | 小地图显示全图 |
| D18 | G10 | 点击小地图 → 主视图跳转 |
| D19 | — | `M` 键显示 / 隐藏小地图 |
| D20 | G11 | 编辑地形 → 小地图 ≤ 200ms 刷新 |
| D21 | G12 | 窗口拉宽 / 拉窄 → 建筑网格实时重排 |
| D22 | — | 建筑数超出可视格数 → 分页器出现 |
| D23 | G8 | `BottomPalette` 折叠 / 展开 |
| D24 | G13 | 播放条随 `BottomPalette` 一起折叠 / 展开 |

---

## 功能验收

- [ ] **G1 / D1**：1920×1080 默认打开，**展开态**画布可视 ≥ 75%（像素测量 + `.worldview/ui_redesign_demos/g1_canvas_expanded.png`）
- [ ] **G1 / D23**：**折叠态**（`BottomPalette` 40px）画布可视 ≥ 90%（`.worldview/ui_redesign_demos/g1_canvas_collapsed.png`）
- [ ] **G2 / D10**：所有面板与 section 可拖动（录屏 `.worldview/ui_redesign_demos/d10_panel_drag.mp4`）
- [ ] **G3 / D10**：拖到左 / 右 / 上 / 下边 → 停靠（录屏 + 截图各 4 张）
- [ ] **G4 / D11**：拖到中央 → 浮动 Window（录屏 `.worldview/ui_redesign_demos/d11_float_window.mp4`）
- [ ] **G5 / D9**：拖建筑图标到地图 → 放置（录屏 + 对比 `maps/<id>.yaml`）
- [ ] **G6 / D13**：布局持久化（手动调 → 退出 → 重启 → 比对布局）
- [ ] **G7 / D12**：F11 全屏切换（录屏 `.worldview/ui_redesign_demos/d12_fullscreen.mp4`）
- [ ] **G8 / D23**：`BottomPalette` 折叠 / 展开（截图前后对比）
- [ ] **G10 / D17**：小地图显示全图，范围与主地图一致
- [ ] **G10 / D18**：点击小地图任意位置 → 主视图以该点为圆心居中
- [ ] **G11 / D20**：编辑地形后小地图 ≤ 200ms 刷新（秒表 + 录屏）
- [ ] **G12 / D21**：1920 → 1280 → 2560 拉窗口，建筑网格行列实时重排，无需重启
- [ ] **G13 / D24**：`BottomPalette` 折叠 / 展开时播放条同步收起，无「palette 收了但播放条还在」的错位
- [ ] **D14**：`PageCount <= 1` 时分页器整体隐藏（不显示 "1/1"）；`> 1` 时出现且可翻页
- [ ] **D15**：切换分类 → 网格内容刷新；切回原分类 → 页码还原
- [ ] **D16**：`cell_size` 56 → 96 → 40，网格立即重排
- [ ] **D19**：`M` 键显示 / 隐藏小地图
- [ ] **D22**：构造 > 一屏的建筑数 → 分页器出现且翻页正确

## 回归验收（既有 D1–D8 demo）

- [ ] **D1**：启动 → 加载示例工程 → 主界面显示正确，画布 ≥ 75%（展开态）
- [ ] **D2**：切地形 + 笔刷绘制 20 格
- [ ] **D3**：Ctrl+Z 撤销
- [ ] **D4**：Ctrl+Shift+Z 重做
- [ ] **D5**：双击城市下钻 + Esc 返回
- [ ] **D6**：放建筑 + 越界弹提示
- [ ] **D7**：保存 + 重新打开一致
- [ ] **D8**：校验失败弹错

## 工程验收

- [ ] `dotnet build WorldView.sln` 成功（无 warning）
- [ ] Godot `--headless --check-only` 通过
- [ ] `scripts/Domain/` 无 `using Godot;`
- [ ] `scripts/Storage/` 无 `using Godot;`（除必要的 IO 入口）
- [ ] `scripts/Render/` 改动**仅限** `WorldCanvas.cs`（drag handler）与新增 `TerrainPalette.cs`
- [ ] `TerrainPalette.cs` 不含渲染逻辑、不持有节点、不写状态
- [ ] `.worldview/layouts/` 不进版本库
- [ ] `docs/*.md` 的**字段定义**未被修改
- [ ] 新增 `scripts/Ui/Dock/`、`scripts/Ui/Palette/`、`scenes/ui/` 目录
- [ ] `PanelDragData` / `BuildingDragData` / `TerrainDragData` 均为 `public partial class ... : RefCounted`
- [ ] `items_per_page` / `cols` / `rows` **未**写入 `.worldview/layouts/*.yaml`（推导值不落盘）
- [ ] 既有信号 `MapChanged` / `LayersChanged` / `MouseMoved` 等**不**被改名；仅新增 `LayoutChanged`

## 视觉验收

- [ ] 暗色主题一致（采样 5 处 UI 元素取色比对 `scripts/Ui/Theme/Dark.tres`）
- [ ] 面板圆角 4px
- [ ] 选中 / hover 高亮可见
- [ ] `TopStrip` / `BottomPalette` 不遮挡地图内容
- [ ] 建筑格保持正方形（`CustomMinimumSize` 生效，未被 `GridContainer` 拉伸）
- [ ] 小地图缩放后无彩色噪点（`TextureFilter = Nearest` 生效）
- [ ] `TopStrip` 上**不再**出现时间文本（时间唯一在 `timeline_row`）