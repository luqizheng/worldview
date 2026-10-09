# UI 主题 Token 真源迁移（B 轮 + A 部分）— Design

> 单 skin(JRPG)前提下，把 UI 控件的所有色值与量化值从 C# 代码 / `parchment_theme.tres` 收回 **YAML 唯一真源**(B);
> 并交付 4 个缺失原子组件(Dialog / FloatingWindow / CloseButton / Icon)(A)。
> 路径：**Architectural**(写 spec → 用户评审 → writing-plans → 实现)。
> 范围：**B 全部 + A 中 4 个用户点名的组件**。其它缺失原子(`ToolButton` / `Menu` / `Slider` / `ColorPicker` / `Toast` / `Tooltip` / `Tab`)另起 spec。
> 不动：数据模型 / `parchment_theme.tres` / `DockLayout` / `BottomPalette` / `Inspector` / `LayerPanel` / 现有 `MapSettingsDialog` / `AddMapDialog` 内部实现(新组件以新类交付)。

## 1. 背景与现状

| 项 | 现状 | 问题 |
|---|---|---|
| `assets/palettes/parchment_palette.yaml:ui.*` | 已写入 12 个 `jrpg_*` 色 token(§4.5.1) | 文档记录滞后(规范文本仍标 `[待写入]`);缺量化 token;缺遗漏色(`#f8ecd0` hover / `#a8c89a` vegetation) |
| `scripts/Ui/Theme/JrpgStyleFactory.cs` | 273 行,12 个 `static Color` 属性 + 11 个 `Color.FromHtml("#...")` + 1 个内联 `#f8ecd0` + 1 处硬编码透明度 `0.4f` | 违反 [美术资源规范 §4.5.6 单一真源纪律](美术资源规范_v0.1.md);改色必须改 C#;hex 与 yaml 漂移风险 |
| `scripts/Ui/Theme/JrpgThemeCache.cs` | 留 `SetActiveTheme(path)` 多主题切换接口(§4.5.5 #15) | 项目已收敛为单 skin(JRPG),多主题接口随之下线 |
| `assets/themes/parchment_theme.tres` | 已重写 24 个 StyleBoxFlat | 本轮不动 |
| 8 个 Jrpg* 组件 | `Button` / `List` / `TextBox` / `RichBox` / `Panel` / `CheckBox` / `DropDown` / `InfoCard` | 仅改内部取色路径;API 一行不动 |

## 2. 目标

1. **`parchment_palette.yaml:ui.*` 成为 UI 控件色值与量化值的唯一真源**。
2. C# 取色 / 取量化值走 **Source Generator 编译期生成**的静态属性,IDE 自动补全;运行期零反射、零分配。
3. 现有 8 个 `Jrpg*` 组件内部取色路径切换为新属性,API、信号、状态机保持原状。
4. 多主题切换接口下线(单 skin 已确认);`JrpgThemeCache.SetActiveTheme` 标记 `[Obsolete]` 并在本轮内删除。

## 3. 非目标

- 不新增其它缺失原子(`ToolButton` / `Menu` / `Slider` / `ColorPicker` / `Toast` / `Tooltip` / `Tab`)→ A2 轮
- 不改 `DockLayout` / `BottomPalette` / `Inspector` / `LayerPanel` → C 轮
- 不重构现有 `MapSettingsDialog` / `AddMapDialog` 内部实现;新组件以新类交付,旧调用点本轮**不**迁移(留出 C 轮由调用方决定是否迁)
- 不改 `parchment_theme.tres`(静态 StyleBoxFlat 由 Godot Theme 子资源覆盖;运行期 `AddThemeStyleboxOverride` 走 token 表)
- 不改数据模型规范
- 不支持运行期切换皮肤;改 yaml 后需要重新 `dotnet build`
- FloatingWindow 位置 / 大小不落盘(对齐 [界面与交互规格 §6](界面与交互规格_v0.1.md) UI 状态仅内存纪律)

## 4. 命名与结构

**三层蛇形**,与 §4.5.1 既有 `jrpg_*` 前缀不冲突:

```
ui:
  color:
    jrpg_bg:                  "#f4f0e4"
    jrpg_card:                "#fff8e7"
    jrpg_fill:                "#f4e4c1"
    jrpg_fill_hover:          "#f8ecd0"   # NEW — §4.5.2 Button hover,旧 JrpgStyleFactory 内联
    jrpg_fill_pressed:        "#e8c89a"
    jrpg_outline:             "#5a3a1a"
    jrpg_outline_focus:       "#e8c34a"
    jrpg_text:                "#3a2810"
    jrpg_text_placeholder:    "#a89878"
    jrpg_text_disabled:       "#8a7a5a"
    jrpg_link:                "#3a5a8a"
    jrpg_danger:              "#c8341a"
    jrpg_success:             "#5a8a3a"
    accent_vegetation_highlight: "#a8c89a"  # NEW — §4.9.1 森林点缀
  metric:
    font_size_body:    14     # §4.5.3 正文(原 JrpgButton.DefaultFontSize)
    font_size_title:   16     # §4.5.3 标题 14-18px,取中位
    font_size_caption: 12     # §4.5.3 占位符缩号(原默认值未现式定,现现定)
    line_height:       22     # §4.5.3 行高(14px × 1.57)
    paragraph_spacing: 8      # §4.5.3 段间距
    button_height:     32     # §4.5.2 Button 高(原 JrpgButton.DefaultHeight)
    textbox_height:    28     # §4.5.2 TextBox 高
    list_row_height:   28     # §4.5.2 List 行高
    panel_title_height: 24    # §4.5.2 Panel 标题条高
    corner_radius:     2      # §4.5.4(拒绝清单 #11)
    border_width:      2      # §4.5.4 统一描边
    icon_size_xs:      16     # §4.5.2 Icon 三档
    icon_size_sm:      24
    icon_size_md:      32
    padding_inline:    8      # §4.5.2 TextBox/List 左对齐 + 8px
    padding_inline_button: 16 # §4.5.2 Button 宽=内容 + 16px
    form_label_width:   100    # Form 字段行 Label 固定宽(本轮 §14.4)
    form_button_width:   96     # ButtonField 内 Button 固定宽(本轮 §14.4.4)
  state:                                # 状态表占位名(本轮仅描述,不动逻辑)
    normal:             "normal"
    hover:              "hover"
    pressed:            "pressed"
    disabled:           "disabled"
    focus:              "focus"
  shadow:
    offset_x:           1      # §4.5.2 Panel 阴影 1px 1px
    offset_y:           1
    size:               2      # §4.5.4 ≤ 2px blur
    opacity:            0.4    # §4.5.2 Panel 阴影 40% 透明(原 C# 硬编码 0.4f)
  modal:
    overlay_opacity:    0.4    # Dialog 背后 overlay 透明度
  animation:
    fade_out_ms:        150    # CloseButton 离场 fade 时长
    hover_lift_ms:      100    # CloseButton hover 描边切换时长
  icon_path:                          # NEW — icon 资源路径占位,本轮不用 PNG,只画形状
    close:              ""              # 空字符串 = 代码绘制
```

**C# 端映射**(生成器产出,人读友好 + IDE 健全部走):

```csharp
namespace WorldView.Ui.Theme;

public static partial class Palette
{
    public static partial class Color
    {
        public static Godot.Color JrpgBg            { get; }   // #f4f0e4
        public static Godot.Color JrpgCard          { get; }   // #fff8e7
        public static Godot.Color JrpgFill          { get; }   // #f4e4c1
        public static Godot.Color JrpgFillHover     { get; }   // #f8ecd0
        public static Godot.Color JrpgFillPressed   { get; }   // #e8c89a
        public static Godot.Color JrpgOutline       { get; }   // #5a3a1a
        public static Godot.Color JrpgOutlineFocus  { get; }   // #e8c34a
        public static Godot.Color JrpgText          { get; }   // #3a2810
        public static Godot.Color JrpgTextPlaceholder { get; } // #a89878
        public static Godot.Color JrpgTextDisabled  { get; }   // #8a7a5a
        public static Godot.Color JrpgLink          { get; }   // #3a5a8a
        public static Godot.Color JrpgDanger        { get; }   // #c8341a
        public static Godot.Color JrpgSuccess       { get; }   // #5a8a3a
        public static Godot.Color AccentVegetationHighlight { get; } // #a8c89a
    }
    public static partial class Metric
    {
        public static int FontSizeBody { get; }       // 14
        public static int FontSizeTitle { get; }      // 16
        public static int FontSizeCaption { get; }    // 12
        public static int LineHeight { get; }         // 22
        public static int ParagraphSpacing { get; }   // 8
        public static int ButtonHeight { get; }       // 32
        public static int TextBoxHeight { get; }      // 28
        public static int ListRowHeight { get; }      // 28
        public static int PanelTitleHeight { get; }   // 24
        public static int CornerRadius { get; }       // 2
        public static int BorderWidth { get; }        // 2
        public static int IconSizeXs { get; }         // 16
        public static int IconSizeSm { get; }         // 24
        public static int IconSizeMd { get; }         // 32
        public static int PaddingInline { get; }      // 8
        public static int PaddingInlineButton { get; } // 16
            public static int FormLabelWidth { get; }      // 100
            public static int FormButtonWidth { get; }     // 96
        }
    public static partial class Shadow
    {
        public static int OffsetX { get; }            // 1
        public static int OffsetY { get; }            // 1
        public static int Size { get; }               // 2
        public static float Opacity { get; }          // 0.4
    }
    public static partial class Modal
    {
        public static float OverlayOpacity { get; }   // 0.4
    }
    public static partial class Animation
    {
        public static int FadeOutMs { get; }          // 150
        public static int HoverLiftMs { get; }        // 100
    }
}
```

**使用**: `var fill = Palette.Color.JrpgFill;` / `var h = Palette.Metric.ButtonHeight;`

## 5. 加载机制：Roslyn IIncrementalGenerator

### 5.1 组件布局

```
scripts/
├── Generator/                                # NEW 独立 analyzer 项目
│   ├── WorldView.Generator.csproj
│   ├── PaletteGenerator.cs                   # IIncrementalGenerator 实现
│   └── PaletteYamlParser.cs                   # YamlDotNet 解析 + 扁平化
├── Ui/
│   ├── Theme/
│   │   ├── Palette.cs                        # NEW:partial class 容器
│   │   └── JrpgStyleFactory.cs               # CHG:取色走 Palette.Color.*,取值走 Palette.Metric.*
│   └── Components/                           # 8 个 Jrpg* 组件
│       └── JrpgButton.cs                     # CHG:DefaultFontSize / DefaultHeight / ThemePath 全部走 Palette
└── ...
tests/
└── WorldView.Storage.Tests/
    └── PaletteLoaderTests.cs                 # NEW:xUnit 仓单
```

### 5.2 WorldView.csproj 改动

最小改动:挂 analyzer project 作为 reference(不是 project-to-project 引用,避免污染 `WorldView` 自身依赖)。

```xml
<ItemGroup>
  <Analyzer Include="..\scripts\Generator\bin\$(Configuration)\netstandard2.0\WorldView.Generator.dll" />
</ItemGroup>
```

**前置约束**: `WorldView.Generator.csproj` 必须先 `dotnet build` 生成 DLL,然后 `WorldView.csproj` 的 build 才能引用。**Build 顺序通过解决方案的 `<ProjectReference>` + `dotnet build WorldView.sln` 保证**(`WorldView.sln` 把两个项目都列出,SLN 拓扑解析)。

> 不使用 `<Exec Command>` / `<BeforeBuild>` 调用 Python 脚本。Source Generator 是 .NET 8 内置机制,无需额外 toolchain。

### 5.3 Generator 行为

- 输入: `assets/palettes/parchment_palette.yaml`(`AdditionalTexts` 由 analyzer 项目自身标记)
- 解析: `YamlDotNet.Serialization` 反出 `Dictionary<string,object>`(嵌套映射)
- 扁平化: 三层路径转 PascalCase 属性名
  - `ui.color.jrpg_fill` → `Color.JrpgFill`(去除 `jrpg_` 前缀,缩短调用)
  - `ui.color.accent_vegetation_highlight` → `Color.AccentVegetationHighlight`
  - `ui.metric.font_size_body` → `Metric.FontSizeBody`
  - `ui.shadow.opacity` → `Shadow.Opacity`
- 类型推断:
  - `string` 且形如 `#RRGGBB` / `#RRGGBBAA` → `Godot.Color`(调 `Color.FromHtml`)
  - `int` / `float` → 原样
  - 其它 → 生成器报错(诊断 P001:不支持的 token 类型)
- 缺失检测: 代码引用 `Palette.Color.<X>` / `Palette.Metric.<X>` / `Palette.Shadow.<X>` 但 generator 未产出对应属性 → 编译器原生 CS0103(`名称 "X" 不存在`),build 失败
- 增量: `IIncrementalGeneratorProvider`,只有 yaml 内容变化才重生成
- 落地产物: `Palette.Designer.cs`(注入到 `WorldView` 主程序集,**不**落盘到仓库;`.gitignore` 加 `**/Palette.Designer.cs`)

### 5.4 失败诊断

| 编号 | 等级 | 触发 | 信息 |
|---|---|---|---|
| **WVP001** | Error | yaml 文件读不到 | `palette yaml 在 {path} 读取失败: {ex.Message}` |
| **WVP002** | Error | yaml 顶层不是 mapping | `palette yaml 顶层必须是 mapping,实际类型: {actualType}` |
| **WVP003** | Error | token 值类型不支持(如 bool / array) | `ui.{path} 值类型 {actualType} 不支持,仅 string / int / float` |
| **WVP004** | Error | hex 格式错误 | `ui.color.{name} 值 "{value}" 不是合法 #RRGGBB / #RRGGBBAA` |
| **WVP005** | Warning | 路径不在已知三层下 | `ui.{path} 路径未在 ui.color / ui.metric / ui.shadow 下,会被忽略` |
| **WVP006** | Warning | `ui.color.*` 命名不带 `jrpg_` / `accent_` 前缀 | `ui.color.{name} 不符合 jrpg_*/accent_* 命名约定` |

### 5.5 不支持的功能

- 运行期切换皮肤:本轮明确不做;改 yaml 须重新 `dotnet build`
- 多 yaml 合并:本轮**仅一个** `parchment_palette.yaml`(单 skin)
- Godot Theme.tres 中的 StyleBoxFlat 色值:仍由 `parchment_theme.tres` 独立持有;运行期 `AddThemeStyleboxOverride` 走 Palette

## 6. 现有 8 个 Jrpg* 组件改动

**仅改内部取色 / 取量化值路径**;API、信号、状态机一行不动。

| 文件 | 改动 |
|---|---|
| `JrpgButton.cs` | `DefaultFontSize` / `DefaultHeight` 常量保留为公开签名(`Backwards Compat`),内部值改为读 `Palette.Metric.FontSizeBody` / `Palette.Metric.ButtonHeight`。`ThemePath` 保持 `const string`(路径常量,不放 token 表;若要可配置,另起 `ui.theme.path` 节点)。`ApplyJrpgTheme` 内 `AddThemeFontSizeOverride` 改读 `Palette.Metric.FontSizeBody` |
| `JrpgPanel.cs` | `TitleBarHeight` 常量改读 `Palette.Metric.PanelTitleHeight`;`_titleLabel` 字号改读 `Palette.Metric.FontSizeBody`;`_contentRoot` separation 改读 `Palette.Metric.ParagraphSpacing` |
| `JrpgList.cs` | 行高改读 `Palette.Metric.ListRowHeight`;字号 `Palette.Metric.FontSizeBody` |
| `JrpgTextBox.cs` | 高度 `Palette.Metric.TextBoxHeight`;字号 `Palette.Metric.FontSizeBody` |
| `JrpgRichBox.cs` | 行高 / 段间距 / 字号同上 |
| `JrpgCheckBox.cs` | box 尺寸 `Palette.Metric.IconSizeXs`(16×16) |
| `JrpgDropDown.cs` | 高度 `Palette.Metric.TextBoxHeight` |
| `JrpgInfoCard.cs` | 标题条 / 字号同上 |

**所有 11 处 `Color.FromHtml("#...")`** + **1 处内联 `#f8ecd0`** + **1 处硬编码透明度 0.4** 全部移除,改读 `Palette.Color.*` / `Palette.Shadow.*`。

## 7. `JrpgStyleFactory` 重构

**重构后形态**: 仅保留 `*Style()` 工厂方法(返回 `StyleBoxFlat`),色值/量化值 100% 来自 `Palette.Color.*` / `Palette.Metric.*` / `Palette.Shadow.*`。**不再持有任何 hex / magic number**。

```csharp
public static StyleBoxFlat PanelStyle()
{
    var s = new StyleBoxFlat
    {
        BgColor = Palette.Color.JrpgCard,
        BorderColor = Palette.Color.JrpgOutline,
    };
    s.SetBorderWidthAll(Palette.Metric.BorderWidth);
    s.SetCornerRadiusAll(Palette.Metric.CornerRadius);
    s.ShadowColor = new Color(
        Palette.Color.JrpgOutline.R,
        Palette.Color.JrpgOutline.G,
        Palette.Color.JrpgOutline.B,
        Palette.Shadow.Opacity);
    s.ShadowOffset = new Vector2(Palette.Shadow.OffsetX, Palette.Shadow.OffsetY);
    s.ShadowSize = Palette.Shadow.Size;
    return s;
}
```

`JrpgStyleFactory` 不再持有 12 个 `static Color` 属性(交给 `Palette.Color`)。`JrpgThemeCache.SetActiveTheme` 标记 `[Obsolete]` 并在本轮删除(单 skin 已确认;§4.5.5 #15 接口下线)。

## 8. 测试

### 8.1 `tests/WorldView.Storage.Tests/PaletteLoaderTests.cs`

xUnit 测试项目;仓单:

| 测试 | 断言 |
|---|---|
| `Color_JrpgBg_ReturnsExpectedHex` | `Palette.Color.JrpgBg` == `#f4f0e4` |
| `Color_JrpgFillHover_NotNull` | 验证遗漏色 `#f8ecd0` 已补(旧版内联) |
| `Color_AllExpectedKeysPresent` | 反射枚举 `Palette.Color.*` 属性集合 == yaml 中 `ui.color.*` 字段集合(防 yaml 漏改 C# 漏改) |
| `Metric_FontSizeBody_Is14` | 字号 14 |
| `Metric_BorderWidth_Is2` | 描边 2 |
| `Shadow_Opacity_Is0_4` | 阴影透明度 0.4 |
| `Palette_HasNoColorFromHtmlCalls` | 受测样本 = `AssetScan.CSharpFilesUnder("scripts/Ui")`(测试工具类,Roslyn 解析 + 词法扫描 `Color\.FromHtml\(...\)` 调用,数量应为 0,防 C# 重新引入硬编码 hex) |
| `Palette_HasNoHexLiteralsInUiFolder` | 受测样本同上,词法扫描 `#" + 6–8 hex` 字符串字面量,数量应为 0(防 C# 内联 hex) |

### 8.2 隔纸对比

把 `Palette.Color.JrpgFill` 改为 `#ff00ff`(临时测试),跑 `godot_run_project` 看 `JrpgButton` normal 态填色是否变粉;改回 `#f4e4c1` 看是否恢复。**不写自动化视觉测试**(超出 B 轮范围)。

### 8.3 验收

- `dotnet test WorldView.sln` 全部通过
- `dotnet build WorldView.csproj` 0 错误 0 警告
- `godot_run_project` 启动场景 `Main.tscn`,8 个 `Jrpg*` 组件(在 `BottomPalette` / `ProjectChrome` / `Inspector` / `LayerPanel` 中)视觉与重构前一致
- 改 `parchment_palette.yaml:ui.color.jrpg_fill` 为 `#aabbcc`,重 build,按钮 visible 态变为 `#aabbcc`(验证真源迁移成功)
- **A 部分新增验收**: 一个 demo 场景 `DemoUiAtoms.tscn`(临时,不入版本库)挂 1 个 Dialog + 1 个 FloatingWindow + 1 个 CloseButton + 4 个 Icon(close / check / cross / info),F5 打开后可见并响应 hover/press/close

## 9. 风险与对策

| 风险 | 对策 |
|---|---|
| Source Generator 引入新依赖(`Microsoft.CodeAnalysis.CSharp.Workspaces` 等) | 在 `WorldView.Generator.csproj` 内 `<PackageReference>` 标注;主项目 `WorldView.csproj` 不引入 analyzer 包,只挂 `<Analyzer Include="..."/>` DLL |
| `Palette.Designer.cs` 被误提交到版本库 | `.gitignore` 加 `**/Palette.Designer.cs`;CI 加 PR check |
| 改 yaml 后忘记重 build | 文档化: §10 沟通纪律 「改 palette 必须 build」;PR check 监测 designer.cs 是否被 git diff(若 diff 出现改动但 designer.cs 未变则报警) |
| 现有 8 个 Jrpg* 组件改动同步生效失败 | §8 仓单 + F5 运行对比;本轮**只**改内部取色路径,不改 API,失败必现于视觉 |
| Color.FromHtml 接受 `#fff` 短格式 | generator **拒收**,要求 `#RRGGBB` / `#RRGGBBAA`;诊断 WVP004 |
| Dialog overlay 透明度不当导致世界画布黑边 | overlay 是 `Control` 节点,使用 `Color(0,0,0,OverlayOpacity)`,位置 = `WorldCanvas` 上方;LayeredContainer 不影响 `Camera2D` 变换 |
| FloatingWindow 拖动脱离 WorldCanvas | 限制拖动范围 `ClampRect` = viewport 矩形,不允许拖出可视区 |
| CloseButton fade-out 与 Tween kill 时机冲突 | `TreeExited` 触发时 `_Tween` 已自动 kill,无需手写 cleanup |
| Modal Dialog 焦点逃出 | Godot `Popup` 类已禁用底层 Control 输入,本组件继承 `Popup` 或 `Window` 自带 |
| `tests/WorldView.Ui.Tests` 项目不存在 | 本 spec 假定该 test project 已 `Godot.NET.Sdk` 化(可跑 Godot 节点);若不存在,**A 部分测试降级为 demo 场景手动验证**(`scenes/_demo/DemoUiAtoms.tscn` 视觉确认) |

## 10. 文档同步

- [美术资源规范_v0.1.md §4.5.1](美术资源规范_v0.1.md): 移除 `[待写入]` 标记,改为「已写入(本轮 B 增补量化层 + 增补遗漏色)」
- [美术资源规范_v0.1.md §4.6](美术资源规范_v0.1.md): 把「C# 端 12 个 `static Color` 属性」改为「C# 端走 Source Generator 生成的 `Palette.Color.*` / `Palette.Metric.*` / `Palette.Shadow.*`」
- [美术资源规范_v0.1.md §4.7 变更记录](美术资源规范_v0.1.md): 追加一行 `v0.1(B 轮:UI token 真源迁移)` 记录
- [界面与交互规格_v0.1.md §1.7 ProjectChrome](界面与交互规格_v0.1.md): 不动
- [AGENTS.md §6.3.3](agent_commands): 增加 `Generator` 项目说明(与 §6.3.1/§6.3.2 同节并列)

## 11. 变更记录

| 版本 | 变更 |
|---|---|
| v0.1 | 首版:B 轮 UI token 真源迁移 spec |
| v0.1(追加 A 部分) | 用户追加需求:Dialog / FloatingWindow / CloseButton / Icon。范围扩为 B + A(4 件),其它缺失原子组件另起 A2 轮 |
| v0.1(追加 Form + Label) | 用户第二轮追加需求:JrpgLabel + JrpgForm + TextField/ToggleField/EnumField/ButtonField。范围扩为 B + A + Form + Label(6 件);SliderField/ColorPickerField/DateField 走 A4 轮 |

---

## 13. A 部分 · Dialog / FloatingWindow / CloseButton / Icon

### 13.1 4 个组件总览

| 类 | 继承 | 落点 | 状态 |
|---|---|---|---|
| `Dialog` | `Window` (Godot 4.7) | `scripts/Ui/Components/Dialog.cs` | NEW — 模态对话框 |
| `FloatingWindow` | `PanelContainer` + 拖动处理 | `scripts/Ui/Components/FloatingWindow.cs` | NEW — 非模态浮窗 |
| `CloseButton` | `Button` (内嵌 `_draw`) | `scripts/Ui/Components/CloseButton.cs` | NEW — 关闭按钮 + 配套 × icon |
| `Icon` | `Control` (自定义 `_draw`) | `scripts/Ui/Components/Icon.cs` | NEW — 16/24/32 三档代码绘制图标 |

**纪律**:
- 4 个组件**100% 走 token**;任何 hex / 量化值都来自 `Palette.Color.*` / `Palette.Metric.*` / `Palette.Modal.*` / `Palette.Animation.*`
- 不引入 PNG icon 资源;所有形状 `_draw()` 程序化绘制
- 不入现有 `MapSettingsDialog` / `AddMapDialog`(它们是项目级对话;新 `Dialog` 是组件级,二者正交)

### 13.2 Dialog(模态)

**职责**: 弹出模态对话框,背后有半透明 overlay 阻挡其他 Control 接收输入;点击 overlay 触发 `CloseRequested`,由调用方决定关闭还是警告。

**继承**: `Window`(Godot 4.7 内置模态窗口类,自带 focus trap + Esc 关闭 + 标题栏)

**视觉契约**(§4.5.2 + §1.2):
- 容器 24px 标题条 + 16px 内部 padding + 内容区 + 32px 按钮条(底部右对齐)
- overlay 填 `Color(0,0,0, Palette.Modal.OverlayOpacity)`
- 描边 2px `Palette.Color.JrpgOutline`,圆角 2px
- 阴影 offset 1,1,size 2,opacity 0.4(走 `Palette.Shadow.*`)
- 字号 14 / 行高 22 / 段间距 8(走 `Palette.Metric.*`)

**公共 API**:
```csharp
public partial class Dialog : Window
{
    /// <summary>设置标题(显示在标题条;空字符串 = 无标题条)。</summary>
    public Dialog SetTitle(string title);

    /// <summary>添加内容到内容区。</summary>
    public Dialog AddContent(Control content);

    /// <summary>添加 OK 按钮(默认右侧);返回按钮引用供调用方 `BindClick`。</summary>
    public JrpgButton AddOkButton(string label = "OK");

    /// <summary>添加 Cancel 按钮。</summary>
    public JrpgButton AddCancelButton(string label = "取消");

    /// <summary>模态弹出(阻塞父节点输入)。</summary>
    public void PopupCentered(Vector2? sizeOverride = null);

    /// <summary>关闭(带 fade-out 150ms)。</summary>
    public void Dismiss();

    [Signal] public delegate void OkPressedEventHandler();
    [Signal] public delegate void CancelPressedEventHandler();
    [Signal] public delegate void CloseRequestedEventHandler();  // 点 overlay / Esc / 标题栏 ×
}
```

**overlay 实现**: 自身作为 `Window` 弹出时,Godot 4.7 `Window` 自带 modal 行为(背景窗 dim + 禁用输入);**不**需要手写 overlay。**本组件不另画 overlay**——若调用方需要"点 overlay 关闭"行为,通过 `CloseRequested` 信号已包含 `CloseRequested`;Godot `Window` 默认 Esc / 标题栏 × 触发 `CloseRequested`。

### 13.3 FloatingWindow(非模态)

**职责**: 非模态浮窗,可在 WorldCanvas 上方叠加;可拖动、可调整大小(可选,本轮**不**实现 resize);多个实例并存。

**继承**: `PanelContainer`(自定义拖动 `_GuiInput`)

**视觉契约**:
- 容器 + 24px 标题条(可隐藏) + 关闭按钮(右上角)
- 描边 2px `Palette.Color.JrpgOutline`,圆角 2px,阴影同 Dialog
- 标题条填 `Palette.Color.JrpgFillPressed`

**公共 API**:
```csharp
public partial class FloatingWindow : PanelContainer
{
    /// <summary>设置标题。</summary>
    public FloatingWindow SetTitle(string title);

    /// <summary>添加内容到内容区。</summary>
    public FloatingWindow AddContent(Control content);

    /// <summary>设置关闭按钮可见(默认 true);false = 不画 ×。</summary>
    public FloatingWindow SetCloseable(bool closeable);

    /// <summary>显示到指定位置。</summary>
    public void ShowAt(Vector2 position);

    /// <summary>关闭。</summary>
    public void Dismiss();

    [Signal] public delegate void CloseRequestedEventHandler();
}
```

**拖动实现**: `_GuiInput` 拦截 `InputEventMouseButton`(左键按下 → 进入拖动)+ `InputEventMouseMotion`(拖动中 → 移动 Position);`ClampRect` = `GetViewportRect()`,不允许超出可视区。

**持久化**: **不落盘**(对齐 [界面与交互规格 §6](界面与交互规格_v0.1.md) UI 状态仅内存纪律)。位置仅在内存持有,关闭工程时释放。

### 13.4 CloseButton

**职责**: 通用关闭按钮,内嵌 `×` icon,3 状态机(normal / hover / pressed),fade-out 离场。

**继承**: `Button`(覆盖 `_Draw`)

**视觉契约**:
- 24×24px 矩形,描边 1px(§4.5.4 简化版,标题栏专用;非通用 2px)
- 内部 `×` icon: 2px 焦褐线段(`Palette.Color.JrpgOutline`),hover 转 `Palette.Color.JrpgOutlineFocus`,pressed 转 `Palette.Color.JrpgText`
- normal 无填色;pressed 淡入 `Palette.Color.JrpgFillPressed` 50%
- 离场 fade-out:`Modulate.A` 由 1 → 0,`Tween` 150ms(走 `Palette.Animation.FadeOutMs`)

**公共 API**:
```csharp
public partial class CloseButton : Button
{
    public const int Size = 24;

    public CloseButton();

    /// <summary>绑定关闭回调。</summary>
    public CloseButton BindClose(System.Action handler);

    [Signal] public delegate void ClosePressedEventHandler();
}
```

### 13.5 Icon(代码绘制)

**职责**: 通用图标,色值从 `Palette.Color.*` 取,**不**落 PNG,所有形状 `_draw()` 程序化绘制。

**继承**: `Control`(覆盖 `_Draw`)

**支持的形状字典**(本轮):

| 形状名 | 绘制 | 用途 |
|---|---|---|
| `close` | 2 条对角线(`×`) | CloseButton / Dialog 标题栏 × |
| `check` | 1 条短 + 1 条长对角线(`✓`) | 复选框 / 成功提示 |
| `cross` | 1 横 + 1 竖(`+`,非对角) | 新增按钮 / 危险取消 |
| `info` | 圆 + 内部 `i` 字(`i` 用 1 像素 + 1 像素点) | 信息卡标题 |
| `warning` | 三角形 + `!` | 警告 |
| `dot` | 圆点(无描边) | marker / 通用圆点 |

**三档尺寸**: 16(`Palette.Metric.IconSizeXs`) / 24(`Palette.Metric.IconSizeSm`) / 32(`Palette.Metric.IconSizeMd`)。**画布上 ≤ 32px**(拒绝清单 #5)。

**公共 API**:
```csharp
public partial class Icon : Control
{
    public enum IconShape { Close, Check, Cross, Info, Warning, Dot }
    public enum IconSize  { Xs = 16, Sm = 24, Md = 32 }

    public IconShape Shape { get; set; } = IconShape.Close;
    public IconSize  Size  { get; set; } = IconSize.Sm;
    public Color     Tint  { get; set; } = Palette.Color.JrpgOutline;

    public Icon SetShape(IconShape shape);
    public Icon SetSize(IconSize size);
    public Icon SetTint(Color tint);   // tint 默认 JrpgOutline;hover 态由调用方手动 SetTint(JrpgOutlineFocus)
}
```

**使用场景**:
- CloseButton 内部嵌入 1 个 `Icon(shape: Close, size: Sm, tint: JrpgOutline)`
- Dialog 标题栏 × / FloatingWindow 标题栏 × 同上
- 工具栏按钮前缀 icon(`button.SetPrefixIcon(new Icon { Shape = IconShape.Info, Size = Xs })`)→ 本轮**不**实现 SetPrefixIcon,仅交付 Icon 本体

### 13.6 动效 token(`Palette.Animation`)

| Token | 值 | 用途 |
|---|---|---|
| `FadeOutMs` | 150 | CloseButton / Dialog / FloatingWindow 离场 fade-out |
| `HoverLiftMs` | 100 | CloseButton hover 描边切换 |

两者由 Source Generator 同 `Palette.Metric.*` 一起生成。

### 13.7 测试(A 部分)

xUnit 测试在 `tests/WorldView.Ui.Tests/Components/`(新建子目录):

| 测试 | 断言 |
|---|---|
| `Icon_CloseShape_Renders2pxLines` | 调 `_Draw()` 后 CanvasItem `GetItemAt` 在中点 4 个角各有像素落点 |
| `Icon_CheckShape_RendersCheckMark` | 检查 `Shape = Check` 时 `_Draw` 走 `DrawLine(短)` + `DrawLine(长)` |
| `Dialog_PopupCentered_HasOverlayOpacity` | overlay 透明度 == `Palette.Modal.OverlayOpacity` |
| `Dialog_Dismiss_FiresCloseRequested` | `Dismiss()` → `Dialog.CloseRequested` 信号触发 |
| `FloatingWindow_ShowAt_ClampsToViewport` | 传入位置 x = -100 → 实际位置 Clamp 到 0 |
| `CloseButton_BindClose_FiresCallback` | click → 回调 + `ClosePressed` 信号触发 |
| `CloseButton_Hover_ChangesTintToFocus` | hover 后 Icon.Tint == `Palette.Color.JrpgOutlineFocus` |
| `Palette_HasNoColorFromHtmlCalls_InAllUi` | 受测样本扩大为 `scripts/Ui/**` + `tests/Ui/**`,数量 0 |

### 13.8 演示场景

**不入版本库**;只用于本轮验收:

`scenes/_demo/DemoUiAtoms.tscn`(目录前缀 `_` 表示私有):
- 1 个 `Dialog`(标题 "Demo",内容 "Hello World",1 OK 按钮)
- 2 个 `FloatingWindow`(位置 (60,80) 与 (300,200),标题 "Inspector" 与 "Layer")
- 1 个 `CloseButton`(独立,演示 hover)
- 4 个 `Icon`(close / check / cross / info,各 24px)

`scenes/_demo/.gitignore`(demo 不入版本库):
```
scenes/_demo/
```

**验收流**: F5 → demo 场景 → 看到 dialog + 2 floating window + close button + 4 icon → 点 dialog OK = 关闭 → 点 floating window × = 关闭 + fade-out 150ms → 改 `Palette.Color.JrpgOutline` → 重 build → 重启 demo,所有 × icon 描边色变。

---

## 14. A 部分 · Label 与 Form(用户第二轮追加)

### 14.1 范围

| 类 | 继承 | 落点 | 状态 |
|---|---|---|---|
| `Label` | `Label` (Godot 4.7) | `scripts/Ui/Components/JrpgLabel.cs` | NEW — 通用文本标签 |
| `Form` | `VBoxContainer` + 字段聚合 | `scripts/Ui/Components/JrpgForm.cs` | NEW — 表单容器 |
| `TextField` | `HBoxContainer` (Label + JrpgTextBox) | `scripts/Ui/Components/Fields/TextField.cs` | NEW — 文本字段 |
| `ToggleField` | `HBoxContainer` (Label + JrpgCheckBox) | `scripts/Ui/Components/Fields/ToggleField.cs` | NEW — 开关字段 |
| `EnumField` | `HBoxContainer` (Label + JrpgDropDown) | `scripts/Ui/Components/Fields/EnumField.cs` | NEW — 枚举字段 |
| `ButtonField` | `HBoxContainer` (Label + JrpgButton) | `scripts/Ui/Components/Fields/ButtonField.cs` | NEW — 按钮字段(常用于"保存""重置" 行) |

**未做(下轮 A4)**:
- `SliderField`(Slider + Label)
- `ColorPickerField`(ColorPicker + Label)
- `DateField`(日期选择 + Label)

### 14.2 Label(独立组件)

**职责**: 通用文本标签。Dialog 内提示、富文本标题、FormField 旁的标签皆走同一组件。

**继承**: `Label`(覆盖默认样式)

**视觉契约**:
- 字号 `Palette.Metric.FontSizeBody`(14px),衬线,左对齐
- 字色 `Palette.Color.JrpgText`(深褐);disabled 态 `Palette.Color.JrpgTextDisabled`
- 单行;多行用 `AutowrapMode = Word` + `LineHeight = Palette.Metric.LineHeight`
- 不画描边、不画背景(纯文本容器)

**公共 API**:
```csharp
public partial class JrpgLabel : Label
{
    public JrpgLabel();
    public JrpgLabel(string text);

    /// <summary>设置文本。</summary>
    public JrpgLabel SetText(string text);

    /// <summary>设置字号(走 Palette.Metric.FontSize*);默认 Body。</summary>
    public JrpgLabel SetFontSize(LabelSize size);  // Body / Title(16) / Caption(12)

    /// <summary>启用 / 禁用(切换字色 token)。</summary>
    public JrpgLabel SetEnabled(bool enabled);

    /// <summary>设置为必填标记(在文本末尾追加红色 `*`)。</summary>
    public JrpgLabel SetRequired(bool required);

    public enum LabelSize { Body, Title, Caption }
}
```

**约束**:
- **不**做链接样式(link 走 `JrpgRichBox`)
- **不**做图标前缀(若需 icon 前缀,调用方手动 `Icon` + `HBox` 拼接)

### 14.3 Form(独立组件)

**职责**: 表单聚合容器,内部以 VBoxContainer 排列字段行;支持行间分隔、必填校验、保存回调。

**继承**: `PanelContainer`(内部 `VBoxContainer _rows`)

**视觉契约**:
- 容器描边 2px `Palette.Color.JrpgOutline`,圆角 2px,内部 padding 12px
- 行间分隔:1px `Palette.Color.JrpgOutline` 30% 透明,默认开(`SetSeparatorsVisible(true)`)
- 标题可选(`SetTitle("新建项目")` → 顶部 24px 标题条,复用 `JrpgPanel` 模式)

**公共 API**:
```csharp
public partial class JrpgForm : PanelContainer
{
    public JrpgForm();
    public JrpgForm(string title);

    /// <summary>设置表单标题(顶部 24px 条);空字符串 = 无。</summary>
    public JrpgForm SetTitle(string title);

    /// <summary>添加文本字段(行 = Label + TextBox)。</summary>
    public TextField AddTextField(string name, string label, string placeholder = "");

    /// <summary>添加开关字段(行 = Label + CheckBox)。</summary>
    public ToggleField AddToggleField(string name, string label, bool defaultValue = false);

    /// <summary>添加枚举字段(行 = Label + DropDown)。</summary>
    public EnumField AddEnumField(string name, string label, IEnumerable<string> options, int defaultIndex = 0);

    /// <summary>添加按钮字段(行 = Label + Button,常用于 "保存" 行)。</summary>
    public ButtonField AddButtonField(string name, string label, string buttonText = "...");

    /// <summary>添加自定义行(任意 Control,跨过字段抽象)。</summary>
    public JrpgForm AddCustomRow(Control row);

    /// <summary>设置行间分隔可见性(默认 true)。</summary>
    public JrpgForm SetSeparatorsVisible(bool visible);

    /// <summary>读取字段当前值(key = 调用 Add*Field 时的 name)。</summary>
    public object? GetFieldValue(string name);

    /// <summary>设置字段值。</summary>
    public JrpgForm SetFieldValue(string name, object value);

    /// <summary>校验所有必填字段;返回未填字段名列表(空 = 通过)。</summary>
    public IReadOnlyList<string> Validate();

    /// <summary>绑定保存回调(校验通过后触发)。</summary>
    public JrpgForm BindSubmit(System.Action<IReadOnlyDictionary<string, object>> onSubmit);

    /// <summary>绑定重置回调(把所有字段值回到 defaultValue)。</summary>
    public JrpgForm BindReset(System.Action onReset);

    [Signal] public delegate void SubmittedEventHandler();
    [Signal] public delegate void ResetEventHandler();
    [Signal] public delegate void ValidationFailedEventHandler(string[] missingFields);
}
```

### 14.4 Field 实现(本轮 4 种)

#### 14.4.1 TextField

**继承**: `HBoxContainer`(Label + JrpgTextBox)

```csharp
public partial class TextField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgTextBox Input { get; }

    public TextField(string name, string label, string placeholder);

    public string Value { get; set; }   // 同步到 Input.Text
    public bool IsRequired { get; set; }  // 同步到 Label.SetRequired + Validate 时检查
}
```

行布局: Label 固定宽 `Palette.Metric.FormLabelWidth`(100px), Input 占满剩余。

#### 14.4.2 ToggleField

```csharp
public partial class ToggleField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgCheckBox Toggle { get; }

    public ToggleField(string name, string label, bool defaultValue);

    public bool Value { get; set; }
}
```

行布局: Label `FormLabelWidth`(100px) + CheckBox 自适应宽。

#### 14.4.3 EnumField

```csharp
public partial class EnumField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgDropDown DropDown { get; }

    public EnumField(string name, string label, IEnumerable<string> options, int defaultIndex);

    public int SelectedIndex { get; set; }
    public string SelectedText { get; }
}
```

行布局: Label `FormLabelWidth`(100px) + DropDown 占满剩余。

#### 14.4.4 ButtonField

```csharp
public partial class ButtonField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgButton Button { get; }

    public ButtonField(string name, string label, string buttonText);

    public JrpgButton BindClick(System.Action handler);
}
```

行布局: Label `FormLabelWidth`(100px) + Button `FormButtonWidth`(96px)。

### 14.5 必填校验

`Form.Validate()` 流程:
1. 遍历所有 `Add*Field` 注册的字段(`Dictionary<string, FieldInfo>` 内部持有)
2. 任意 `IsRequired == true` 字段:
   - TextField:`Value.Trim() != ""`
   - ToggleField:`Value == true`? 否(默认 false 不算必填)
   - EnumField:始终通过(下拉默认有选项)
3. 失败字段名收集到 `IReadOnlyList<string>` 返回
4. 失败信号 `ValidationFailed(missingFields)` 同时触发

### 14.6 字段值读取与回填

`Form` 内部持有 `Dictionary<string, object> _values`, `BindSubmit` 回调签名:
```csharp
Action<IReadOnlyDictionary<string, object>> submitHandler;
```
回调收到不可变快照;调用方在回调里读 `dict["project_name"]` 等。**不**做自动 YAML 序列化(那是领域层职责)。

### 14.7 Form 内嵌提交 / 重置行

**可选 API**(本轮**不**做,留 A4):
```csharp
form.SetFooterButtons(submitText: "保存", resetText: "重置");
```
本轮调用方自行 `form.AddButtonField("submit", "", "保存").BindClick(...)` 实现。

### 14.8 测试(A 部分第二轮)

xUnit 测试在 `tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs`(若 project 不存在走 demo 手动验证):

| 测试 | 断言 |
|---|---|
| `Label_SetRequired_AppendsAsterisk` | `Label.Text` 以 `*` 结尾 |
| `Label_SetFontSize_Title_UsesPaletteTitle` | 字号 == `Palette.Metric.FontSizeTitle`(16) |
| `Form_AddTextField_AppearsInRows` | `form.GetFieldValue("name")` 返回 TextField 实例 |
| `Form_SetFieldValue_UpdatesField` | `form.SetFieldValue("x", "hello")` 后 `TextField.Value == "hello"` |
| `Form_Validate_EmptyRequiredField_Fails` | 必填字段空 → `Validate()` 返回含字段名 |
| `Form_BindSubmit_FiresCallbackWithSnapshot` | submit 后回调收到 `dict.Count == 字段数` |
| `Form_ValidationFailed_FiresSignal` | 缺必填字段时 `ValidationFailed` 信号触发 |

### 14.9 demo 扩展

`scenes/_demo/DemoUiAtoms.tscn` 新增:
- 1 个 `Form`(标题 "新建项目")
  - 1 个 TextField("name", "名称", "必填")
  - 1 个 ToggleField("auto_save", "自动保存")
  - 1 个 EnumField("theme", "主题", ["parchment", "minimal"])
  - 1 个 ButtonField("submit", "", "保存")
- 5 个独立 `JrpgLabel`(各字号 + 各状态演示:Body / Title / Caption / disabled / required)

**验收流追加**: F5 → demo → Form 内填名称 + 选主题 + 点 Toggle → 点 ButtonField "保存" → 校验通过 → 回调 log 输出。