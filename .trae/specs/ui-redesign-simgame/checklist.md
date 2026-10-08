
---

## `checklist.md`（修改版）

```markdown
# UI Redesign — SimCity/RTS 验收清单

> 对应 [spec.md](spec.md) §2 / §8 与 [tasks.md](tasks.md)。
> 每条用 `[ ]` / `[x]` 标记，附验收手段（截图 / 录屏 / 命令输出）。

---

## 编号对照表

| D 编号（Demo/回归） | G 编号（Goal） | 说明 |
|---|---|---|
| D1 | G1 | 启动 + 主界面显示 + 画布 ≥ 85% |
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

---

## 功能验收

- [ ] **G1 / D1**：1920×1080 默认打开，画布可视区 ≥ 85%（像素测量 + `.worldview/ui_redesign_demos/g1_canvas_area.png`）
- [ ] **G2 / D10**：所有面板可拖动（录屏 `.worldview/ui_redesign_demos/d10_panel_drag.mp4`）
- [ ] **G3 / D10**：拖到左 / 右 / 上 / 下边 → 停靠（录屏 + 截图各 4 张）
- [ ] **G4 / D11**：拖到中央 → 浮动 Window（录屏 `.worldview/ui_redesign_demos/d11_float_window.mp4`）
- [ ] **G5 / D9**：拖建筑图标到地图 → 放置（录屏 + 对比 `maps/<id>.yaml`）
- [ ] **G6 / D13**：布局持久化（手动调 → 退出 → 重启 → 比对布局）
- [ ] **G7 / D12**：F11 全屏切换（录屏 `.worldview/ui_redesign_demos/d12_fullscreen.mp4`）
- [ ] **G8**：底栏折叠 / 展开（截图前后对比）

## 回归验收（既有 D1–D8 demo）

- [ ] **D1**：启动 → 加载示例工程 → 主界面四区显示正确，画布 ≥ 85%
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
- [ ] `.worldview/layouts/` 不进版本库
- [ ] `docs/*.md` 字段定义未被修改
- [ ] 新增 `scripts/Ui/` 与 `scenes/ui/` 目录
- [ ] `PanelDragData` / `BuildingDragData` / `TerrainDragData` 均为 `public partial class ... : RefCounted`
- [ ] 既有信号 `MapChanged` / `LayersChanged` / `MouseMoved` 等**不**被改名；仅新增 `LayoutChanged`

## 视觉验收

- [ ] 暗色主题一致（采样 5 处 UI 元素取色比对 `scripts/Ui/Theme/Dark.tres`）
- [ ] 面板圆角 4px
- [ ] 选中 / hover 高亮可见
- [ ] 顶栏 / 底栏不遮挡地图内容