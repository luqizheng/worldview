# UI 原子组件 A2:ToolButton / Menu / Toast / Tooltip / Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-KILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 A 轮 10 个原子上叠加 5 个工作区常见原子 (ToolButton / Menu / Toast / Tooltip / Tab),100% 走 token,无新 PNG,xUnit 25 仓单全绿。

**Architecture:** 5 个新组件全部以 token 化 + 代码绘制 + 反射 / 纯函数测试,沿用 A 轮"3 参纯函数"经验,接入 `Palette.Color.*` / `Palette.Metric.*` / `Palette.Shadow.*` / `Palette.Animation.*`。Demo 场景加 5 行展示,本轮 0 新增 yaml token (B 轮已收齐)。

**Tech Stack:** Godot 4.7.2 mono / .NET 8 / C# / xUnit.v3 4.0.1 + MTP 2.4.0 / Roslyn IIncrementalGenerator (沿用)

**Spec:** `docs/superpowers/specs/2026-10-10-ui-atoms-a2-toolbutton-menu-toast-tooltip-tab.md`

---

## Global Constraints

- 命名:ToolButton / Menu / Toast / Tooltip / Tab 不带 `Jrpg` 前缀 (与 Field 同理)
- Icon 复用:全部用 `JrpgIcon.Shape` 枚举,0 新增 Icon 枚举
- token 接入:任何 hex / 量化值都走 `Palette.*`,零硬编码 (沿用 `grep -E 'Color\.FromHtml\(' scripts/Ui` 应为 0)
- 测试不构造 Godot 节点:走纯函数 + 反射 (沿用 Dialog 3 参纯函数经验)
- Demo 场景不入版本库:继续 `.gitignore` 屏蔽 `scenes/_demo/`
- 不动:parchment_palette.yaml (B 轮已收齐 19+14+4+1+2) / Palette.cs (5 partial 已收齐) / 11 既有 Jrpg* 组件

---

## Review Focus (spec 提到但没单步覆盖 → 测试里)

1. **Menu 选中触发 + 自动关闭** (Task 2 测试):`AddItem` 3 次,点击第 2 项 → ItemSelected(1) 信号触发 + 菜单 Hide
2. **Toast 自动消失 timer** (Task 3 测试):`ShowMessage` 后 `autoHideSec` 秒触发 Timeout 信号,淡出用 FadeOutMs
3. **Tooltip 延迟显示** (Task 4 测试):`ShowUnderCursor` 后 `DelaySec` 秒才 Visible=true,提前调 `Hide()` 则永远不显示
4. **Tab 切换 + 信号** (Task 5 测试):`ActiveIndex = 1` 触发 `TabChanged(1)`,同值不重复触发
5. **ToolButton Toggle 模式** (Task 1 测试):`ToggleMode=true` 时 ToggledOn 与 Toggled 信号严格对齐

---

## File Structure

### 新建
| 路径 | 职责 |
|---|---|
| `scripts/Ui/Components/ToolButton.cs` | 图标 + 文字 + 切换态 |
| `scripts/Ui/Components/Menu.cs` | 下拉菜单 (PanelContainer + 手动定位,不走 Popup OS 主题) |
| `scripts/Ui/Components/Toast.cs` | 短消息 (PanelContainer + fade + auto-hide) |
| `scripts/Ui/Components/Tooltip.cs` | 悬停提示 (PanelContainer + 延迟) |
| `scripts/Ui/Components/Tab.cs` | 横向标签栏 (HBoxContainer + 选中态) |
| `tests/WorldView.Tests/Ui/Components/ToolButtonTests.cs` | 5 仓单 |
| `tests/WorldView.Tests/Ui/Components/MenuTests.cs` | 5 仓单 |
| `tests/WorldView.Tests/Ui/Components/ToastTests.cs` | 5 仓单 |
| `tests/WorldView.Tests/Ui/Components/TooltipTests.cs` | 5 仓单 |
| `tests/WorldView.Tests/Ui/Components/TabTests.cs` | 5 仓单 |

### 修改
| 路径 | 改动 |
|---|---|
| `scenes/_demo/DemoUiAtoms.tscn` | 加 5 行 (ToolButton 1 / Menu 1 / Toast 1 / Tooltip 1 / Tab 1) |
| `scripts/_demo/DemoUiAtoms.cs` | 实例化 5 组件 + 信号挂接 (open menu / show toast / etc.) |

### 不动
- `parchment_palette.yaml` / `Palette.cs` / 11 既有 Jrpg* 组件

---

## Tasks (5 串行,每个独立可验)

### Task 1: ToolButton (图标 + 文字 + Toggle)
- [ ] 写 `scripts/Ui/Components/ToolButton.cs`:Button 子类,内嵌 JrpgIcon + Label,Icon 属性映射到 JrpgIcon.CurrentShape,Text 走 Palette.Metric.FontSizeBody,ToggleMode 反射设,active 态用 `Modulate = Palette.Color.JrpgFillPressed` 覆盖
- [ ] 写 `tests/WorldView.Tests/Ui/Components/ToolButtonTests.cs`:5 仓单 — 高度走 Palette.Metric.ButtonHeight=32 / 字号走 FontSizeBody=14 / 内边距走 PaddingInlineButton=16 / Toggled 信号存在 (EventHandlerType.Name) / ToggleMode 反射读写
- [ ] build 0 错,跑 `dotnet run --project tests/WorldView.Tests --filter "WorldView.Tests.Ui.Components.ToolButtonTests"` 5/5 通过

### Task 2: Menu (下拉菜单)
- [ ] 写 `scripts/Ui/Components/Menu.cs`:PanelContainer 子类 (不用 Popup 避免 OS 主题),内含 VBoxContainer + 每个 Item 一个 MenuItem 内部类 (Button 子类),AddItem 内部 new MenuItem + AddChild,AddSeparator 内部 new HSeparator + 走 Palette.Color.JrpgBorderSubtle,点击 Item 触发 ItemSelected(index) + Hide
- [ ] 写 `tests/WorldView.Tests/Ui/Components/MenuTests.cs`:5 仓单 — 行高走 ListRowHeight=28 / 字号走 FontSizeBody=14 / 边框色走 Palette 反射 / 背景色走 Palette 反射 / ItemSelected 信号存在
- [ ] build 0 错,跑测试 5/5 通过

### Task 3: Toast (短消息)
- [ ] 写 `scripts/Ui/Components/Toast.cs`:PanelContainer 子类,内含 HBoxContainer + 左边 Severity 颜色条 (Panel 8px 宽) + 右边 JrpgLabel,ShowMessage 内部:加到 host / GetTree().Root,Position 按 Severity/Position 枚举算 (TopRight = viewport 右上角偏移 16,16),CreateTween().TweenInterval(autoHideSec).TweenProperty(modulate:a, 0, FadeOutMs/1000).TweenCallback(QueueFree)
- [ ] 写 `tests/WorldView.Tests/Ui/Components/ToastTests.cs`:5 仓单 — 字号走 FontSizeBody=14 / 严重色映射 (Error→JrpgDanger / Success→JrpgAccent / Warning→JrpgWarning / Info→JrpgTextPlaceholder) 走反射读 const / Position 枚举 4 个值 / 淡出走 FadeOutMs=150 / Timeout 信号存在
- [ ] build 0 错,跑测试 5/5 通过

### Task 4: Tooltip (悬停提示)
- [ ] 写 `scripts/Ui/Components/Tooltip.cs`:PanelContainer 子类,内含 HBoxContainer + 左边 JrpgIcon(可选) + 右边 JrpgLabel,ShowUnderCursor 内部:加到 GetTree().Root,延迟用 SceneTreeTimer (CreateTimer(DelaySec)) + Timeout 回调里 Visible=true,Hide 立即 Visible=false + QueueFree
- [ ] 写 `tests/WorldView.Tests/Ui/Components/TooltipTests.cs`:5 仓单 — 字号走 FontSizeCaption=12 / DelaySec 默认 0.4 走反射 / 字号 cap / 阴影走 Palette.Shadow.OffsetX=1 / 文本色走 Palette 反射
- [ ] build 0 错,跑测试 5/5 通过

### Task 5: Tab (横向标签栏)
- [ ] 写 `scripts/Ui/Components/Tab.cs`:HBoxContainer 子类,内含每项一个 TabItem 内部类 (PanelContainer + 下边线),AddTab 内部 new TabItem + AddChild,ActiveIndex setter 内部遍历更新每项的 Modulate (active=Palette.Color.JrpgAccent / inactive=Palette.Color.JrpgTextPlaceholder) + 触发 TabChanged
- [ ] 写 `tests/WorldView.Tests/Ui/Components/TabTests.cs`:5 仓单 — 高度走 PanelTitleHeight=24 / 字号走 FontSizeBody=14 / ActiveIndex setter 反射读写 / TabChanged 信号存在 (EventHandlerType.Name) / AddTab 计数走 Count 反射
- [ ] build 0 错,跑测试 5/5 通过

### Task 6: Demo 场景扩展
- [ ] 改 `scenes/_demo/DemoUiAtoms.tscn`:加 5 行节点
  - ToolButton (含 JrpgIcon=Close + Text="Bold")
  - "Open Menu" Button + Menu 实例 (3 项)
  - "Show Toast" Button + Toast 实例
  - 任意 Control + Tooltip 实例 ("This is a tooltip")
  - Tab 实例 (3 tab: "Layer" / "Inspector" / "Settings")
- [ ] 改 `scripts/_demo/DemoUiAtoms.cs`:在 _Ready 末尾追加
  - menuBtn.Pressed → menu.PopupCentered(new Vector2(160, 120))
  - toastBtn.Pressed → toast.ShowMessage(this, "已保存", Toast.Severity.Success, 2.5f)
  - tab.ActiveIndex = 1
- [ ] build 0 错,git add scenes/_demo/ + scripts/_demo/ (虽 .gitignore 屏蔽,但本地手测要存在)

### Task 7: 全量验证 + commit
- [ ] `dotnet build WorldView.sln` — 0 错
- [ ] `dotnet run --project tests/WorldView.Tests --filter "WorldView.Tests.Ui"` — 80/80 (55 + 25)
- [ ] `grep -E 'Color\.FromHtml\(' scripts/Ui` — 仍为 0
- [ ] commit 5fbdbc0 → 2b61e61 → 81c603d → 3fd525e → 18be952 → b15f8c2 → **new** "feat(ui): A2 5 原子 — ToolButton / Menu / Toast / Tooltip / Tab"
- [ ] F5 demo 视觉验证 (5 组件全部可见 + 交互正确)

---

## 验收口径

- `dotnet build WorldView.sln` — 0 错 (本轮新错都为 0)
- `dotnet run --project tests/WorldView.Tests --filter "WorldView.Tests.Ui"` — 80/80 (现 55 + 本轮 25)
- `grep -E 'Color\.FromHtml\(' scripts/Ui` — 0 (纪律不破)
- F5 demo — 5 新组件可见 + 交互正确

## 风险与教训 (从 A 轮带过来)

| 教训 | 应用 |
|---|---|
| 任何 `BaseButton` 子类方法覆盖要小心 name collision (本轮:Button.Icon 跟 Icon 撞名) | ToolButton 内部用 `JrpgIcon _icon` 字段,不覆盖 `Icon` 属性 |
| `Popup` / `Window` 走 OS 主题 → 改用 `PanelContainer` | Menu 改用 PanelContainer (本轮 spec 已定) |
| 测试不构造 Godot 节点:走纯函数 + 反射 | 5 组件测试 0 构造,纯走 `typeof(X).GetProperty/GetEvent/反射读 const` |
| xunit.v3 filter 不支持 pipe → 用全限定 namespace | 验收口径写 `--filter "WorldView.Tests.Ui"` |
| Godot 4 `[Signal]` delegate 在反射看是 `*EventHandler` 嵌套类型 | 测试断言 `EventHandlerType.Name` (e.g. "ToggledEventHandler") |
| 任何 `P/Invoke` 进 Godot native 的 .NET 类型 (InputEventKey / NodePath) 在 xunit 无 SceneTree 时必崩 | 5 组件测试不构造这些类型,只反射 |
