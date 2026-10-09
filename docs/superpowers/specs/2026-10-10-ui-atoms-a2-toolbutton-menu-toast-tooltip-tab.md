# UI 原子组件 A2:ToolButton / Menu / Toast / Tooltip / Tab — Design

> A 轮后续集:在已落地的 10 个原子 (Icon / CloseButton / FloatingWindow / Dialog / JrpgLabel / 4-Field / Form) 之上,补全 5 个工作区常见原子。
> 路径: Architectural (写 spec → 用户评审 → writing-plans → 实现)。
> 范围: 仅这 5 个新组件。不动既有 Jrpg* 组件 / 数据模型 / 既有 Dialog (MapSettingsDialog / AddMapDialog)。
> 继承 [B 轮 + A 轮 spec](./2026-10-09-ui-theme-token-source-of-truth.md) 的全部真源纪律:走 `Palette.*`,不写 hex,代码绘制,无新 PNG。

## 1. 背景与现状

| 组件 | 现状 | 缺口 |
|---|---|---|
| ToolButton | 无 | 工作区顶部工具条 (ProjectChrome / MapTabBar 之外) 需要"图标 + 文字 + 可切换状态"的复合按钮,目前是 `Button` + 内嵌 `JrpgIcon`,无统一契约 |
| Menu | 无 | `OptionButton` 是单选下拉,无下拉菜单 (右键 / 顶栏触发) 支持;Inspector 行 / MapTabBar 标签右键菜单无法落地 |
| Toast | 无 | 操作反馈 ("已保存" / "导入失败") 只能用 `Print` 或 Dialog 替代,体验不连贯 |
| Tooltip | 无 | Godot 4 自带 `Control._MakeCustomTooltip`,但跨 token 主题接入需 token 化外壳 |
| Tab | 无 | `MapTabBar` 已有,但属于场景特定,无"通用可复用的横向标签栏"原子 (供 LayerPanel / Inspector 子面板用) |

## 2. 目标

1. 5 个新原子组件 100% 走 `Palette.*`,零硬编码 hex,无新 PNG (图标全部代码绘制,继承 [Icon 原子 §4.5.2](./2026-10-09-ui-theme-token-source-of-truth.md))。
2. 提供"工作区常见模式"的最小契约:Menu 项高亮 / Toast 自动消失 / Tooltip 延迟 / Tab 切换。
3. 全部接入 token 量化层 (高度 / 内边距 / 字号 / 阴影偏移走 `Palette.Metric.*` / `Palette.Shadow.*`)。
4. xUnit 测试覆盖:**语义层** (枚举 / token / 反射读 const) + **纯函数层** (不构造 Godot 节点,沿用本轮"3 参纯函数"经验)。

## 3. 非目标

- 不实现复杂交互 (例如:Menu 嵌套子菜单 / Toast 队列优先级 / Tooltip 富文本 / Tab 拖拽排序) — 这些留给后续轮 (A3+ 或 D 轮)。
- 不动 `MapTabBar` / `ProjectChrome` / `Inspector` 内部 — 既有调用点可选择"后续轮"迁到新原子,本轮不强制迁移。
- 不做 A2 之外的 UI (例如:CommandPalette / Snackbar 容器 / DropdownMenu) — 留后续轮。
- 不改数据模型 / 资源加载 / 持久化。

## 4. 5 个组件契约

### 4.1 ToolButton (顶部工具按钮)

**用途:** 图标 + 可选文字 + 可切换 active 状态的复合按钮 (例如:工具栏切换 / 视图模式切换)。

**继承:** `Button` (Godot)。

**API:**
```csharp
public partial class ToolButton : Button
{
    public JrpgIcon.Shape Icon { get; set; }   // 默认 Close
    public string Text { get; set; }            // 可选 (空则只显图标)
    public bool ToggleMode { get; set; }        // 默认 false
    public bool ToggledOn { get; set; }         // 当前 active 态

    [Signal] public delegate void ToggledEventHandler(bool on);
}
```

**token 引用:**
- 高度: `Palette.Metric.ButtonHeight`
- 字号: `Palette.Metric.FontSizeBody`
- 内边距: `Palette.Metric.PaddingInlineButton`
- active 底色: `Palette.Color.JrpgFillPressed`
- 文字色: `Palette.Color.JrpgText`

**验收:**
- F5 视觉:点击高亮 / 再点恢复 (active 切换)
- 高度严格 = 32,字号 = 14 (token 化)

### 4.2 Menu (下拉菜单)

**用途:** 触发后弹出纵向列表,点击项触发回调后自动关闭 (右键菜单 / 顶栏下拉)。

**继承:** `Popup` (Godot 自带 popup 容器)。

**API:**
```csharp
public partial class Menu : Popup
{
    public void AddItem(string label, Action onSelect, JrpgIcon.Shape? icon = null);
    public void AddSeparator();
    public int Count { get; }
    public int SelectedIndex { get; }

    [Signal] public delegate void ItemSelectedEventHandler(int index);
}
```

**token 引用:**
- 行高: `Palette.Metric.ListRowHeight` (28)
- 字号: `Palette.Metric.FontSizeBody`
- 边框: `Palette.Color.JrpgOutline`
- 背景: `Palette.Color.JrpgBg`
- 悬停高亮: `Palette.Color.JrpgFillHover`
- 文本色: `Palette.Color.JrpgText`
- 分隔条色: `Palette.Color.JrpgOutline`

**验收:**
- `AddItem` 3 项 + `AddSeparator` + 1 项,弹窗高 = (3+1)*28
- 点击第 2 项触发 ItemSelected(1) + 自动 Hide
- 全部 token 化

### 4.3 Toast (操作反馈短消息)

**用途:** 屏幕右上 / 底部弹出短消息,2.5s 自动消失 (任务完成 / 错误提示 / 状态变更)。

**继承:** `PanelContainer` (走 token 不走 OS Window 主题)。

**API:**
```csharp
public partial class Toast : PanelContainer
{
    public enum Severity { Info, Success, Warning, Error }
    public enum Position  { TopRight, BottomRight, TopCenter, BottomCenter }

    public void ShowMessage(string text, Severity sev = Severity.Info, Position pos = Position.BottomRight, float autoHideSec = 2.5f);
    public void ShowMessage(Control host, string text, Severity sev = Severity.Info, float autoHideSec = 2.5f);

    [Signal] public delegate void TimeoutEventHandler();
}
```

**token 引用:**
- 字号: `Palette.Metric.FontSizeBody`
- 字号 (小) / 严重: `Palette.Metric.FontSizeCaption`
- 内边距: `Palette.Metric.PaddingInline` (8)
- 字号 / 字号 cap: 走 token
- 字号 (严重标题): `Palette.Metric.FontSizeTitle` (16)
- 文本色: `Palette.Color.JrpgText`
- 边框: `Palette.Color.JrpgOutline`
- 严重色: `Palette.Color.JrpgDanger` (Error) / `Palette.Color.JrpgSuccess` (Success) / `Palette.Color.JrpgOutlineFocus` (Warning) / `Palette.Color.JrpgTextPlaceholder` (Info)
- 背景: `Palette.Color.JrpgBg`
- 阴影偏移: `Palette.Shadow.OffsetX` / `OffsetY` / `Size`
- 淡入/淡出: `Palette.Animation.FadeOutMs`
- 圆角: `Palette.Metric.CornerRadius` (2)

**验收:**
- `ShowMessage("已保存")` 弹出 2.5s 自动消失
- Severity=Error 用 `JrpgDanger` 色
- 全部走 token

### 4.4 Tooltip (悬停提示)

**用途:** 鼠标悬停延迟显示的小卡片,内含文字 (可选 JrpgIcon 头标)。

**继承:** `PanelContainer` (走 token 不走 OS 主题)。

**API:**
```csharp
public partial class Tooltip : PanelContainer
{
    public string Text { get; set; }
    public JrpgIcon.Shape? Icon { get; set; }   // 可选头标
    public float DelaySec { get; set; } = 0.4f; // 悬停多久显示

    public void ShowUnderCursor(Control target);
    public void Hide();
}
```

**token 引用:**
- 字号: `Palette.Metric.FontSizeCaption` (12)
- 内边距: `Palette.Metric.PaddingInline` (8)
- 文本色: `Palette.Color.JrpgText`
- 边框: `Palette.Color.JrpgOutline`
- 背景: `Palette.Color.JrpgBg`
- 阴影: `Palette.Shadow.*`
- 淡入: `Palette.Animation.HoverLiftMs` (100) / FadeOutMs (150)

**验收:**
- 调用 `ShowUnderCursor(button)` 后 0.4s 显示
- `Hide()` 立即消失
- 走 token

### 4.5 Tab (横向标签栏)

**用途:** 通用可复用横向标签栏,Item 可点击切换 active。供 LayerPanel 子面板 / Inspector 子面板 / 设置面板切换用。

**继承:** `HBoxContainer` (Godot 横向容器)。

**API:**
```csharp
public partial class Tab : HBoxContainer
{
    public int Count { get; }
    public int ActiveIndex { get; set; }  // 0-based
    public void AddTab(string label, JrpgIcon.Shape? icon = null);
    public void RemoveTab(int index);

    [Signal] public delegate void TabChangedEventHandler(int index);
}
```

**token 引用:**
- 高度: `Palette.Metric.PanelTitleHeight` (24) 或 `ButtonHeight` (32) — 选 24 (横向标签)
- 字号: `Palette.Metric.FontSizeBody` (14)
- 内边距: `Palette.Metric.PaddingInline` (8)
- 文本色 (active): `Palette.Color.JrpgLink`
- 文本色 (inactive): `Palette.Color.JrpgTextPlaceholder`
- 边框 (active 底): `Palette.Color.JrpgLink`
- 边框 (inactive 底): `Palette.Color.JrpgOutline`
- 背景: `Palette.Color.JrpgBg`

**验收:**
- `AddTab` 3 次,`ActiveIndex=1` 时第 2 项高亮
- 点击第 3 项触发 `TabChanged(2)`
- 全部 token 化

## 5. 文件结构

### 新建
| 路径 | 职责 |
|---|---|
| `scripts/Ui/Components/ToolButton.cs` | 图标 + 文字 + 切换 |
| `scripts/Ui/Components/Menu.cs` | 下拉菜单 |
| `scripts/Ui/Components/Toast.cs` | 短消息 |
| `scripts/Ui/Components/Tooltip.cs` | 悬停提示 |
| `scripts/Ui/Components/Tab.cs` | 横向标签栏 |
| `tests/WorldView.Tests/Ui/Components/ToolButtonTests.cs` | 语义 + 反射 |
| `tests/WorldView.Tests/Ui/Components/MenuTests.cs` | 语义 + 纯函数 |
| `tests/WorldView.Tests/Ui/Components/ToastTests.cs` | 语义 + Severity/Position 枚举 |
| `tests/WorldView.Tests/Ui/Components/TooltipTests.cs` | 语义 + Delay 纯函数 |
| `tests/WorldView.Tests/Ui/Components/TabTests.cs` | 语义 + 反射 |

### 修改
| 路径 | 改动 |
|---|---|
| `scenes/_demo/DemoUiAtoms.tscn` | 增 5 个新组件的展示行 |
| `scripts/_demo/DemoUiAtoms.cs` | 增 5 个新组件的实例化 + 信号挂接 |

### 不动
- `parchment_palette.yaml` (B 轮已收齐 19+14+4+1+2, 本轮 0 新增)
- `Palette.cs` (B 轮已收齐 5 个 partial, 本轮 0 新增)
- 11 既有 Jrpg* 组件 (零修改)

## 6. 测试覆盖目标

5 组件 × 5 仓单 = **25 xUnit 仓单**:
- ToolButtonTests 5: token 高度 / 字号 / 内边距 / Toggled 信号 / ToggleMode 反射
- MenuTests 5: 行高 / 字号 / AddItem 计数 / AddSeparator 计数 / SelectedIndex 反射
- ToastTests 5: 字号 / 严重色映射 / Position 枚举 / 淡入淡出 token / Timeout 信号
- TooltipTests 5: 字号 / Delay 纯函数 / 字号 cap / 阴影 token / 文本色
- TabTests 5: 高度 / 字号 / ActiveIndex setter / TabChanged 信号 / AddTab 计数

**验收口径:**
- `dotnet build WorldView.sln` — 0 错
- `dotnet run --project tests/WorldView.Tests --filter "WorldView.Tests.Ui"` — 80/80 (现 55 + 25) 通过
- F5 demo 视觉:5 个新组件全部可见、交互正确 (切 ToolButton active / 弹 Menu / Toast 自动消失 / Tooltip 延迟 / Tab 切换)

## 7. 不变量 (跨轮纪律)

- 命名: 既有 11 组件保留 `Jrpg*` 前缀,ToolButton/Menu/Toast/Tooltip/Tab 不带 `Jrpg` 前缀 (与 [Plan 1](#) "Field 不带 Jrpg 前缀" 同理)
- Icon 复用: 全部用 `JrpgIcon.Shape` 枚举 (Close / Check / Cross / Info / Warning / Dot),不新增 Icon 枚举
- token 接入: 任何 hex / 量化值都走 `Palette.*`,零硬编码 (沿用本轮 `grep -E 'Color\.FromHtml\(' scripts/Ui` 应为 0)
- 测试不构造 Godot 节点: 走纯函数 + 反射 (沿用本轮 Dialog 3 参纯函数经验)
- Demo 场景不入版本库: `scenes/_demo/` 继续被 `.gitignore` 屏蔽

## 8. 风险与权衡

| 风险 | 应对 |
|---|---|
| Menu 用 `Popup` 可能走 OS 主题 | 改用 `PanelContainer` + 手动定位,跟 Dialog (改用 PanelContainer) 同一招 |
| Toast 自动消失 timer 与 Godot Tween 重叠 | 沿用本轮 Dialog 经验:`CreateTween().TweenInterval(sec).TweenProperty(modulate:a, 0, FadeOutMs/1000).TweenCallback(QueueFree)` |
| Tooltip 跨 Control 跟随鼠标 | 用 `GetGlobalMousePosition()` + 偏移,跟 MapTabBar 现有 tooltip 同思路 |
| Tab 高度选择 24 vs 32 | 选 24 (与 PanelTitleHeight 对齐,横向标签视觉密度合适) |
| 25 仓单是否够 | 5 组件 × 5 仓单 = 25 是本轮 plan 的下限,验收口径严格,沿用"语义 + 纯函数"双层 |
