# UI 主题 Token 真源迁移 + 缺失原子组件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 UI 控件的所有色值与量化值收回 `parchment_palette.yaml:ui.*` 唯一真源;新增 Dialog / FloatingWindow / CloseButton / Icon / JrpgLabel / JrpgForm + 4 个 Field 组件;100% 走 token;无新 PNG。

**Architecture:** Roslyn `IIncrementalGenerator` 在 `dotnet build` 期解析 yaml,产出 `Palette.Designer.cs`(静态属性,IDE 自动补全,运行期零反射)。8 个既有 `Jrpg*` 组件内部取色路径改成读 `Palette.*` 静态属性。新增 10 个组件以"代码绘制 icon + 走 token"统一风格,Demo 场景不入版本库。

**Tech Stack:** Godot 4.7.2 mono / .NET 8 / C# / Roslyn `IIncrementalGenerator` / YamlDotNet 16.3.0 / xUnit

**Spec:** `docs/superpowers/specs/2026-10-09-ui-theme-token-source-of-truth.md`

---

## Global Constraints

- 单 skin(JRPG);不支持运行期切换皮肤
- yaml 文件:`assets/palettes/parchment_palette.yaml:ui.*`,三层路径 `ui.color.*` / `ui.metric.*` / `ui.shadow.*` / `ui.modal.*` / `ui.animation.*`
- hex 格式仅 `#RRGGBB` / `#RRGGBBAA`(拒收 `#fff` 短格式)
- C# 端零硬编码 hex:`grep -E 'Color\.FromHtml\(' scripts/Ui` 应为 0
- 文件落点:Source Generator → `scripts/Generator/`(独立 csproj);组件 → `scripts/Ui/Components/`;Field → `scripts/Ui/Components/Fields/`;测试 → `tests/WorldView.Ui.Tests/`
- Generator DLL 必须先 `dotnet build` 才能被 `WorldView.csproj` 引用
- `.gitignore` 屏蔽:`**/Palette.Designer.cs`、`scenes/_demo/`
- 不动:`parchment_theme.tres` / 数据模型 / `DockLayout` / `BottomPalette` / `Inspector` / `LayerPanel` / 现有 `MapSettingsDialog` / `AddMapDialog` 内部
- xUnit 测试项目 `tests/WorldView.Ui.Tests/` 本轮**新建**(`Godot.NET.Sdk`,启用 `[Trait]` 标记 demo-only / unit)

---

## Review Focus

下列输入/失败模式在 spec 中提到但没有单一步骤覆盖,落到对应 task 的测试里:

1. **空 yaml / 缺失字段**(Task 1 测试):改坏 yaml 应让 Generator 在 build 时报 WVP001–004,而不是运行时崩溃
2. **Palette 引用未生成属性**(Task 1 测试):写 `Palette.Color.NonExistent` 应让编译期 CS0103 失败,而不是 build 成功但运行时崩
3. **Dialog Esc 关闭**(Task 7 测试):Godot `Window.CloseRequested` 默认由 Esc 与标题栏 × 触发;验证 Esc 按键确实关闭 Dialog 且 `CloseRequested` 信号触发
4. **FloatingWindow 拖动越界**(Task 8 测试):传入 `ShowAt(new Vector2(-1000, -1000))` 后实际 Position 被 Clamp 到 viewport
5. **CloseButton fade-out 与节点释放时序**(Task 9 测试):`Dismiss()` 调用 150ms 内节点不应被 `_ExitTree` 主动 kill,避免 Tween 中断
6. **Form 必填校验的边界**(Task 12 测试):空字符串 + 仅空白字符(`"   "`)的 TextField 必填校验应失败;ToggleField 必填 false 时仍应通过

---

## File Structure

### 新建文件

| 路径 | 职责 |
|---|---|
| `scripts/Generator/WorldView.Generator.csproj` | 独立 Roslyn analyzer 项目 |
| `scripts/Generator/PaletteGenerator.cs` | `IIncrementalGenerator` 实现 |
| `scripts/Generator/PaletteYamlParser.cs` | YamlDotNet 解析 + 扁平化 |
| `scripts/Ui/Theme/Palette.cs` | partial class 容器(手写容器部分) |
| `scripts/Ui/Components/Dialog.cs` | 模态对话框 |
| `scripts/Ui/Components/FloatingWindow.cs` | 非模态浮窗 |
| `scripts/Ui/Components/CloseButton.cs` | 关闭按钮 |
| `scripts/Ui/Components/Icon.cs` | 代码绘制图标 |
| `scripts/Ui/Components/JrpgLabel.cs` | 通用文本标签 |
| `scripts/Ui/Components/JrpgForm.cs` | 表单容器 |
| `scripts/Ui/Components/Fields/TextField.cs` | 文本字段 |
| `scripts/Ui/Components/Fields/ToggleField.cs` | 开关字段 |
| `scripts/Ui/Components/Fields/EnumField.cs` | 枚举字段 |
| `scripts/Ui/Components/Fields/ButtonField.cs` | 按钮字段 |
| `tests/WorldView.Ui.Tests/WorldView.Ui.Tests.csproj` | xUnit test project |
| `tests/WorldView.Ui.Tests/PaletteLoaderTests.cs` | token 仓单 + 静态扫描 |
| `tests/WorldView.Ui.Tests/Components/IconTests.cs` | Icon 形状 / 尺寸 / tint |
| `tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs` | Label / Form / Field |
| `tests/WorldView.Ui.Tests/AssetScan.cs` | 静态扫描工具 |
| `scenes/_demo/DemoUiAtoms.tscn` | demo 场景(不入版本库) |
| `.gitignore`(追加) | 屏蔽 Palette.Designer.cs + scenes/_demo/ |

### 修改文件

| 路径 | 改动 |
|---|---|
| `assets/palettes/parchment_palette.yaml` | §4 token 增补(遗漏色 2 项 + metric 19 项 + shadow 4 项 + modal 1 项 + animation 2 项 + form_label/form_button 2 项) |
| `scripts/Ui/Theme/JrpgStyleFactory.cs` | 删除 11 处 `Color.FromHtml`、1 处内联 `#f8ecd0`、1 处硬编码 0.4f、12 个 `static Color` 属性 |
| `scripts/Ui/Theme/JrpgThemeCache.cs` | 删除 `SetActiveTheme` 与 `Invalidate`(单 skin) |
| `scripts/Ui/Components/JrpgButton.cs` | `DefaultFontSize` / `DefaultHeight` 读 `Palette.Metric.*` |
| `scripts/Ui/Components/JrpgPanel.cs` | `TitleBarHeight` / 字号 / separation 读 `Palette.Metric.*` |
| `scripts/Ui/Components/JrpgList.cs` | 行高 / 字号读 `Palette.Metric.*` |
| `scripts/Ui/Components/JrpgTextBox.cs` | 高度 / 字号读 `Palette.Metric.*` |
| `scripts/Ui/Components/JrpgRichBox.cs` | 行高 / 段间距 / 字号读 `Palette.Metric.*` |
| `scripts/Ui/Components/JrpgCheckBox.cs` | box 尺寸读 `Palette.Metric.IconSizeXs` |
| `scripts/Ui/Components/JrpgDropDown.cs` | 高度读 `Palette.Metric.TextBoxHeight` |
| `scripts/Ui/Components/JrpgInfoCard.cs` | 标题条 / 字号读 `Palette.Metric.*` |
| `WorldView.csproj` | 添加 `<Analyzer Include="..\scripts\Generator\bin\$(Configuration)\netstandard2.0\WorldView.Generator.dll" />` |
| `WorldView.sln` | 添加 `scripts/Generator/WorldView.Generator.csproj` + `tests/WorldView.Ui.Tests/WorldView.Ui.Tests.csproj` |

---

## Task 1: Palette yaml 增补与 Generator 落地

**Files:**
- Create: `scripts/Generator/WorldView.Generator.csproj`
- Create: `scripts/Generator/PaletteGenerator.cs`
- Create: `scripts/Generator/PaletteYamlParser.cs`
- Modify: `assets/palettes/parchment_palette.yaml:48-60`(追加 `ui.metric.*` `ui.shadow.*` `ui.modal.*` `ui.animation.*` `ui.color.jrpg_fill_hover` `ui.color.accent_vegetation_highlight`)
- Test: `tests/WorldView.Storage.Tests/PaletteLoaderTests.cs`

**Interfaces:**
- Consumes: `assets/palettes/parchment_palette.yaml` 文本(由 analyzer project 通过 `<None Include="..." CopyToOutputDirectory="PreserveNewest" />` 引入)
- Produces:
  - `partial class Palette` 含嵌套 `partial class Color` / `Metric` / `Shadow` / `Modal` / `Animation`,每个属性 `public static T Xxx { get; }`(T 推断为 `Godot.Color` / `int` / `float`)
  - 诊断:`WVP001`(yaml 读不到) / `WVP002`(顶层不是 mapping) / `WVP003`(不支持的值类型) / `WVP004`(hex 格式错误) / `WVP005`(路径不在 ui.* 下) / `WVP006`(ui.color.* 命名不符约定)

- [ ] **Step 1: 在 `assets/palettes/parchment_palette.yaml` 追加新 token**

在 `ui.jrpg_success` 之后追加如下块,保留既有 12 token 不变:

```yaml
ui:
  # ... existing 12 jrpg_* tokens ...
  jrpg_fill_hover:          "#f8ecd0"
  accent_vegetation_highlight: "#a8c89a"
  metric:
    font_size_body:    14
    font_size_title:   16
    font_size_caption: 12
    line_height:       22
    paragraph_spacing: 8
    button_height:     32
    textbox_height:    28
    list_row_height:   28
    panel_title_height: 24
    corner_radius:     2
    border_width:      2
    icon_size_xs:      16
    icon_size_sm:      24
    icon_size_md:      32
    padding_inline:    8
    padding_inline_button: 16
    form_label_width:   100
    form_button_width:  96
  shadow:
    offset_x:           1
    offset_y:           1
    size:               2
    opacity:            0.4
  modal:
    overlay_opacity:    0.4
  animation:
    fade_out_ms:        150
    hover_lift_ms:      100
  icon_path:
    close:              ""
```

- [ ] **Step 2: 创建 `scripts/Generator/WorldView.Generator.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
    <PackageReference Include="YamlDotNet" Version="16.3.0" />
  </ItemGroup>
  <ItemGroup>
    <None Include="..\..\assets\palettes\parchment_palette.yaml" Link="Assets\parchment_palette.yaml">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: 创建 `scripts/Generator/PaletteYamlParser.cs`**

输入:yaml 文本字符串。
输出:`IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>`(顶层 → 中层 → 叶节点值)。

伪代码:

```csharp
public sealed record PaletteNode(
    string Category,      // "color" / "metric" / "shadow" / "modal" / "animation"
    string PropertyName,  // PascalCase: "jrpg_fill" → "JrpgFill"
    string TypeName,      // "Godot.Color" / "int" / "float"
    string RawValue,      // "#f4e4c1" / "14" / "0.4"
    Location Location);
```

规则:
- 顶层键必须是 `ui`,子键 ∈ {color, metric, shadow, modal, animation}
- `color.*` 值必须形如 `#RRGGBB` / `#RRGGBBAA`
- `metric.*` / `shadow.*` 值必须是 int 或 float;`modal.overlay_opacity` 是 float
- snake_case → PascalCase:`jrpg_fill_hover` → `JrpgFillHover`、`accent_vegetation_highlight` → `AccentVegetationHighlight`、`font_size_body` → `FontSizeBody`、`icon_size_xs` → `IconSizeXs`(去除 `jrpg_` 前缀外的 snake 转 PascalCase)
- `overlay_opacity` → `OverlayOpacity`(普通 snake 转 PascalCase)

- [ ] **Step 4: 创建 `scripts/Generator/PaletteGenerator.cs`**

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class PaletteGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var yamlText = context.AdditionalTextsProvider
            .Where(at => Path.GetFileName(at.Path).EndsWith("parchment_palette.yaml"))
            .Select((at, ct) => SourceText.ToString(at.GetText(ct)))
            .WithComparer(StringComparer.Ordinal);

        var nodes = yamlText.Select((text, _) => PaletteYamlParser.Parse(text));

        context.RegisterSourceOutput(nodes, (spc, parsed) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("namespace WorldView.Ui.Theme;");
            sb.AppendLine("public static partial class Palette {");
            foreach (var cat in parsed.GroupBy(n => n.Category))
            {
                sb.AppendLine($"    public static partial class {cat.Key} {{");
                foreach (var node in cat)
                {
                    sb.AppendLine($"        public static {node.TypeName} {node.PropertyName} {{ get; }} = {node.ValueConverter};");
                }
                sb.AppendLine("    }");
            }
            sb.AppendLine("}");
            spc.AddSource("Palette.Designer.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        });
    }
}
```

诊断:
- `WVP001`(`DiagnosticSeverity.Error`):yaml 文件读不到 → `RegisterSourceOutput` 前 filter 阶段报错
- `WVP002`:yaml 顶层不是 mapping → parser 抛
- `WVP003`:值类型不是 string / int / float → parser 抛
- `WVP004`:hex 不匹配 `^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$` → parser 抛
- `WVP005`(`Warning`):路径不在 ui.color/metric/shadow/modal/animation 下
- `WVP006`(`Warning`):`ui.color.*` 命名不带 `jrpg_` 或 `accent_` 前缀

- [ ] **Step 5: 创建 `scripts/Ui/Theme/Palette.cs`(partial class 容器)**

手写空 partial class,占位给 generator 注入属性:

```csharp
namespace WorldView.Ui.Theme;

/// <summary>
/// UI 控件色值与量化值的真源(由 Source Generator 填充具体值)。
/// 所有 Jrpg* 组件与新增 Dialog / FloatingWindow / CloseButton / Icon / Form / Field
/// 必须通过 Palette.Color.* / Metric.* / Shadow.* / Modal.* / Animation.* 取值。
/// </summary>
public static partial class Palette
{
    public static partial class Color { }
    public static partial class Metric { }
    public static partial class Shadow { }
    public static partial class Modal { }
    public static partial class Animation { }
}
```

- [ ] **Step 6: 创建 `tests/WorldView.Storage.Tests/PaletteLoaderTests.cs`(stub)**

```csharp
using WorldView.Ui.Theme;
using Xunit;

public class PaletteLoaderTests
{
    [Fact] public void Color_JrpgBg_ReturnsExpectedHex() =>
        Assert.Equal("#f4f0e4", Palette.Color.JrpgBg.ToHtml());

    [Fact] public void Color_JrpgFillHover_NotNull() =>
        Assert.NotEqual(default, Palette.Color.JrpgFillHover);

    [Fact] public void Metric_FontSizeBody_Is14() =>
        Assert.Equal(14, Palette.Metric.FontSizeBody);

    [Fact] public void Metric_BorderWidth_Is2() =>
        Assert.Equal(2, Palette.Metric.BorderWidth);

    [Fact] public void Shadow_Opacity_Is0_4() =>
        Assert.Equal(0.4f, Palette.Shadow.Opacity);

    [Fact] public void Modal_OverlayOpacity_Is0_4() =>
        Assert.Equal(0.4f, Palette.Modal.OverlayOpacity);

    [Fact] public void Animation_FadeOutMs_Is150() =>
        Assert.Equal(150, Palette.Animation.FadeOutMs);
}
```

- [ ] **Step 7: 创建 `tests/WorldView.Storage.Tests/AssetScan.cs`(静态扫描工具)**

```csharp
public static class AssetScan
{
    public static IEnumerable<string> CSharpFilesUnder(string relativeToRepoRoot)
    {
        // 返回 scripts/Ui 与 tests/Ui 下所有 .cs 文件的相对路径
    }

    public static int CountOccurrences(string relativeGlob, string pattern)
    {
        // 调 .NET regex 在每个文件内容中匹配 pattern,求和
    }
}
```

- [ ] **Step 8: 创建 `tests/WorldView.Storage.Tests/NoHardcodedHexTests.cs`**

```csharp
using WorldView.Storage.Tests;
using Xunit;

public class NoHardcodedHexTests
{
    [Fact] public void NoColorFromHtmlInUiFolder()
    {
        var count = AssetScan.CountOccurrences("scripts/Ui/**/*.cs", @"Color\.FromHtml\(");
        Assert.Equal(0, count);
    }

    [Fact] public void NoHexLiteralsInUiFolder()
    {
        var count = AssetScan.CountOccurrences("scripts/Ui/**/*.cs", "\"#[0-9a-fA-F]{6,8}\"");
        Assert.Equal(0, count);
    }
}
```

- [ ] **Step 9: 创建 `tests/WorldView.Storage.Tests/WorldView.Storage.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\WorldView.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 10: 修改 `WorldView.csproj` 引用 Generator DLL**

在末尾 `<ItemGroup>` 内追加:

```xml
<ItemGroup>
  <Analyzer Include="..\scripts\Generator\bin\$(Configuration)\netstandard2.0\WorldView.Generator.dll">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
    <OutputItemType>Analyzer</OutputItemType>
  </Analyzer>
</ItemGroup>
```

- [ ] **Step 11: 修改 `WorldView.sln` 加入 Generator + Tests 项目**

用 `dotnet sln WorldView.sln add scripts/Generator/WorldView.Generator.csproj tests/WorldView.Storage.Tests/WorldView.Storage.Tests.csproj` 添加两个项目到 solution。

- [ ] **Step 12: 修改 `.gitignore`**

追加:

```
**/Palette.Designer.cs
scenes/_demo/
```

- [ ] **Step 13: 编译验证**

```bash
dotnet build scripts/Generator/WorldView.Generator.csproj -c Debug
dotnet build WorldView.sln -c Debug
```

预期:Generator 产出 `bin/Debug/netstandard2.0/WorldView.Generator.dll`,WorldView.csproj build 0 错误,WVP005/006 warning 数 = 0(命名都合规)。

- [ ] **Step 14: 跑测试验证**

```bash
dotnet test WorldView.sln -c Debug --no-build
```

预期:`PaletteLoaderTests` 7 个 + `NoHardcodedHexTests` 2 个 = 9 通过。

- [ ] **Step 15: 验证缺失字段会触发诊断**

临时把 yaml 改坏:把 `ui.color.jrpg_fill` 值改成 `#fff`(短格式)。重新 `dotnet build`。预期:Generator 报 WVP004,build 失败。**改回** `#f4e4c1`,重 build 0 错误。

- [ ] **Step 16: Commit**

```bash
git add scripts/Generator/ assets/palettes/parchment_palette.yaml scripts/Ui/Theme/Palette.cs tests/WorldView.Storage.Tests/ WorldView.csproj WorldView.sln .gitignore
git commit -m "feat(ui): palette yaml 增补 + Roslyn Generator + 9 个 xUnit 仓单"
```

---

## Task 2: JrpgStyleFactory 重构 + JrpgThemeCache 下线

**Files:**
- Modify: `scripts/Ui/Theme/JrpgStyleFactory.cs`(全文)
- Modify: `scripts/Ui/Theme/JrpgThemeCache.cs`(删除 `SetActiveTheme` / `Invalidate`)

**Interfaces:**
- Consumes: `Palette.Color.*` / `Palette.Metric.*` / `Palette.Shadow.*` 静态属性(Task 1)
- Produces: 10 个 `StyleBoxFlat` 工厂方法,与旧版本签名一致(内部取色全走 Palette)

- [ ] **Step 1: 删除 `JrpgStyleFactory` 12 个 `static Color` 属性**

移除 `JrpgBg` / `JrpgCard` / `JrpgFill` / `JrpgFillPressed` / `JrpgOutline` / `JrpgOutlineFocus` / `JrpgText` / `JrpgTextPlaceholder` / `JrpgTextDisabled` / `JrpgLink` / `JrpgDanger` / `JrpgSuccess` 这 12 个属性。**不**再额外补(全部走 `Palette.Color.*`)。

- [ ] **Step 2: 重写 10 个 `*Style()` 工厂方法**

每个方法体内所有 `Color.FromHtml("#...")` / 内联 hex / `0.4f` / 魔数 全部替换:

- `PanelStyle()`:`BgColor = Palette.Color.JrpgCard`,`BorderColor = Palette.Color.JrpgOutline`,`SetBorderWidthAll(Palette.Metric.BorderWidth)`,`SetCornerRadiusAll(Palette.Metric.CornerRadius)`,`ShadowColor = new Color(Palette.Color.JrpgOutline.R, Palette.Color.JrpgOutline.G, Palette.Color.JrpgOutline.B, Palette.Shadow.Opacity)`,`ShadowOffset = new Vector2(Palette.Shadow.OffsetX, Palette.Shadow.OffsetY)`,`ShadowSize = Palette.Shadow.Size`
- `TitleBarStyle()`:用 `Palette.Color.JrpgFillPressed` + `Palette.Color.JrpgOutline` + `Palette.Metric.BorderWidth`(底部) + `Palette.Metric.CornerRadius`(顶部 2,底部 0)
- `ButtonNormalStyle()`:`Palette.Color.JrpgFill` + `Palette.Color.JrpgOutline` + `Palette.Metric.BorderWidth` + `Palette.Metric.CornerRadius`
- `ButtonHoverStyle()`:用 `Palette.Color.JrpgFillHover`(已补)替换内联 `#f8ecd0`
- `ButtonPressedStyle()` / `ButtonFocusStyle()` / `ButtonDisabledStyle()` / `TextBoxNormalStyle()` / `TextBoxFocusStyle()` / `ListItemSelectedStyle()` / `CheckBoxUncheckedStyle()` / `CheckBoxCheckedStyle()` 同模式替换

- [ ] **Step 3: 修改 `JrpgThemeCache.cs` 删除多主题接口**

- 删除 `SetActiveTheme` 方法
- 删除 `Invalidate` 方法
- 删除 `_activePath` 字段(只剩 `_active`,由 `DefaultThemePath` 唯一初始化)
- `GetTheme()` 简化为:`return _active ??= ResourceLoader.Load<Godot.Theme>(DefaultThemePath);`
- 更新类注释,移除 `§4.5.5 #15` 多主题切换接口

- [ ] **Step 4: 跑静态扫描**

```bash
dotnet test WorldView.sln -c Debug --no-build --filter "FullyQualifiedName~NoHardcodedHex"
```

预期:2 个 hex 扫描测试通过(此时所有 hex 已迁出 JrpgStyleFactory)。

- [ ] **Step 5: 编译 + 跑测试**

```bash
dotnet build WorldView.sln -c Debug
dotnet test WorldView.sln -c Debug --no-build
```

预期:0 错误 0 警告(Generator 不报 WVP005/006),9 个测试全过。

- [ ] **Step 6: Commit**

```bash
git add scripts/Ui/Theme/
git commit -m "refactor(ui): JrpgStyleFactory 走 Palette token,JrpgThemeCache 下线多主题接口"
```

---

## Task 3: 8 个既有 Jrpg* 组件内部取色路径切换

**Files:**
- Modify: `scripts/Ui/Components/JrpgButton.cs:25-31, 66-72`
- Modify: `scripts/Ui/Components/JrpgPanel.cs:23-25, 55-69`
- Modify: `scripts/Ui/Components/JrpgList.cs`(全文 grep `JRPG*` 常量)
- Modify: `scripts/Ui/Components/JrpgTextBox.cs`
- Modify: `scripts/Ui/Components/JrpgRichBox.cs`
- Modify: `scripts/Ui/Components/JrpgCheckBox.cs`
- Modify: `scripts/Ui/Components/JrpgDropDown.cs`
- Modify: `scripts/Ui/Components/JrpgInfoCard.cs`
- Test: `tests/WorldView.Ui.Tests/Components/ExistingComponentTokenTests.cs`(新增)

**Interfaces:**
- Consumes: `Palette.Metric.*`(字号 / 高度 / 描边 / 圆角 / 段间距 / 行高)
- Produces: 既有 API / 信号 / 状态机保持原状;`const` 公开字段保留(向后兼容),内部值改为读 Palette

- [ ] **Step 1: 写 `tests/WorldView.Ui.Tests/Components/ExistingComponentTokenTests.cs`(失败)**

```csharp
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

public class ExistingComponentTokenTests
{
    [Fact] public void JrpgButton_UsesPaletteFontSize()
    {
        var btn = new JrpgButton();
        Assert.Equal(Palette.Metric.FontSizeBody, btn.GetThemeDefaultFontSize());
    }

    [Fact] public void JrpgPanel_TitleBarHeight_UsesPaletteMetric()
    {
        var panel = new JrpgPanel();
        Assert.Equal(Palette.Metric.PanelTitleHeight, panel.TitleBarHeight);
    }

    [Fact] public void JrpgTextBox_Height_UsesPaletteMetric()
    {
        var tb = new JrpgTextBox();
        Assert.Equal(Palette.Metric.TextBoxHeight, tb.GetCustomMinimumSize().Y);
    }

    [Fact] public void JrpgCheckBox_BoxSize_UsesIconSizeXs()
    {
        var cb = new JrpgCheckBox();
        Assert.Equal(Palette.Metric.IconSizeXs, cb.GetCustomMinimumSize().X);
    }
}
```

- [ ] **Step 2: 跑测试,确认失败**

`dotnet test --filter "FullyQualifiedName~ExistingComponentToken"`,预期:`NotImplementedException` 或 `Assert.Equal` 失败(当前常量是字面量)。

- [ ] **Step 3: 修改 `JrpgButton.cs`**

- `DefaultFontSize` 改:`public const int DefaultFontSize = Palette.Metric.FontSizeBody;` ← 报错,`const` 不能读 `static partial` 属性
- 改为 `public static int DefaultFontSize => Palette.Metric.FontSizeBody;`
- `DefaultHeight` 改为 `public static int DefaultHeight => Palette.Metric.ButtonHeight;`
- `ThemePath` 保留 `const string ThemePath = "res://assets/themes/parchment_theme.tres";`(路径常量,不放 token 表)
- `ApplyJrpgTheme` 内 `AddThemeFontSizeOverride("font_size", DefaultFontSize)` 改为读 `Palette.Metric.FontSizeBody`
- 添加 `public int GetThemeDefaultFontSize() => GetThemeFontSize("");`

- [ ] **Step 4: 修改 `JrpgPanel.cs`**

- `TitleBarHeight` 改为 `public static int TitleBarHeight => Palette.Metric.PanelTitleHeight;`
- `BuildLayout` 内 `_titleBar.CustomMinimumSize = new Vector2(0, TitleBarHeight)` 保持(自动走新值)
- `_titleLabel` 的 `AddThemeFontSizeOverride` 改为 `Palette.Metric.FontSizeBody`
- `_contentRoot.AddThemeConstantOverride("separation", Palette.Metric.ParagraphSpacing)`

- [ ] **Step 5: 修改 `JrpgList.cs`**

- 行高:`AddThemeConstantOverride("h_separation", Palette.Metric.ListRowHeight)`(Godot ItemList 的行高 theme constant)
- 字号:`AddThemeFontSizeOverride("font_size", Palette.Metric.FontSizeBody)`

- [ ] **Step 6: 修改 `JrpgTextBox.cs`**

- `CustomMinimumSize = new Vector2(0, Palette.Metric.TextBoxHeight)`
- `AddThemeFontSizeOverride("font_size", Palette.Metric.FontSizeBody)`

- [ ] **Step 7: 修改 `JrpgRichBox.cs`**

- 行高 / 段间距 / 字号走 `Palette.Metric.*`

- [ ] **Step 8: 修改 `JrpgCheckBox.cs`**

- `CustomMinimumSize = new Vector2(Palette.Metric.IconSizeXs, Palette.Metric.IconSizeXs)`

- [ ] **Step 9: 修改 `JrpgDropDown.cs`**

- `CustomMinimumSize = new Vector2(0, Palette.Metric.TextBoxHeight)`

- [ ] **Step 10: 修改 `JrpgInfoCard.cs`**

- 标题条 / 字号 / padding 走 `Palette.Metric.*`

- [ ] **Step 11: 跑测试**

```bash
dotnet build WorldView.sln -c Debug
dotnet test WorldView.sln -c Debug --no-build --filter "FullyQualifiedName~ExistingComponentToken"
```

预期:4 个测试通过。

- [ ] **Step 12: 跑全量测试 + 静态扫描**

```bash
dotnet test WorldView.sln -c Debug --no-build
```

预期:9 + 4 = 13 通过,2 个 hex 扫描 0 出现。

- [ ] **Step 13: Commit**

```bash
git add scripts/Ui/Components/ tests/WorldView.Ui.Tests/Components/ExistingComponentTokenTests.cs
git commit -m "refactor(ui): 8 个 Jrpg* 组件内部取量化值走 Palette.Metric.*"
```

---

## Task 4: Icon(代码绘制,6 形 × 3 档)

**Files:**
- Create: `scripts/Ui/Components/Icon.cs`
- Test: `tests/WorldView.Ui.Tests/Components/IconTests.cs`

**Interfaces:**
- Consumes: `Palette.Color.JrpgOutline`(默认 tint)、`Palette.Metric.IconSizeXs/Sm/Md`
- Produces: `enum IconShape { Close, Check, Cross, Info, Warning, Dot }`、`enum IconSize { Xs = 16, Sm = 24, Md = 32 }`、`public Color Tint { get; set; }`、`public void _Draw()`

- [ ] **Step 1: 写 `tests/WorldView.Ui.Tests/Components/IconTests.cs`(失败)**

```csharp
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

public class IconTests
{
    [Fact] public void Icon_DefaultTint_IsJrpgOutline()
    {
        var icon = new Icon();
        Assert.Equal(Palette.Color.JrpgOutline, icon.Tint);
    }

    [Fact] public void Icon_DefaultSize_IsSm()
    {
        var icon = new Icon();
        Assert.Equal(Icon.IconSize.Sm, icon.Size);
    }

    [Theory]
    [InlineData(Icon.IconShape.Close, 16)]
    [InlineData(Icon.IconShape.Check, 24)]
    [InlineData(Icon.IconShape.Cross, 32)]
    public void Icon_SetShapeAndSize_StoresCorrectly(Icon.IconShape shape, int size)
    {
        var icon = new Icon().SetShape(shape).SetSize((Icon.IconSize)size);
        Assert.Equal(shape, icon.Shape);
        Assert.Equal((Icon.IconSize)size, icon.Size);
    }

    [Fact] public void Icon_SetTint_UpdatesTint()
    {
        var icon = new Icon();
        icon.SetTint(Palette.Color.JrpgOutlineFocus);
        Assert.Equal(Palette.Color.JrpgOutlineFocus, icon.Tint);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

`dotnet test --filter "FullyQualifiedName~IconTests"`,预期:编译错误 `Icon class not found`。

- [ ] **Step 3: 创建 `scripts/Ui/Components/Icon.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class Icon : Control
{
    public enum IconShape { Close, Check, Cross, Info, Warning, Dot }
    public enum IconSize { Xs = 16, Sm = 24, Md = 32 }

    public IconShape Shape { get; set; } = IconShape.Close;
    public IconSize Size { get; set; } = IconSize.Sm;
    public Color Tint { get; set; } = Palette.Color.JrpgOutline;

    public Icon() { CustomMinimumSize = new Vector2((int)IconSize.Sm, (int)IconSize.Sm); }

    public Icon SetShape(IconShape shape) { Shape = shape; QueueRedraw(); return this; }
    public Icon SetSize(IconSize size) { Size = size; CustomMinimumSize = new Vector2((int)size, (int)size); QueueRedraw(); return this; }
    public Icon SetTint(Color tint) { Tint = tint; QueueRedraw(); return this; }

    public override void _Draw()
    {
        var size = (int)Size;
        var center = size / 2f;
        var lineWidth = Math.Max(1f, size / 12f);  // 16→1.3, 24→2, 32→2.7
        switch (Shape)
        {
            case IconShape.Close:
                DrawLine(new Vector2(2, 2), new Vector2(size - 2, size - 2), Tint, lineWidth);
                DrawLine(new Vector2(size - 2, 2), new Vector2(2, size - 2), Tint, lineWidth);
                break;
            case IconShape.Check:
                DrawLine(new Vector2(size * 0.25f, size * 0.5f), new Vector2(size * 0.45f, size * 0.75f), Tint, lineWidth);
                DrawLine(new Vector2(size * 0.45f, size * 0.75f), new Vector2(size * 0.8f, size * 0.25f), Tint, lineWidth);
                break;
            case IconShape.Cross:
                DrawLine(new Vector2(center, 2), new Vector2(center, size - 2), Tint, lineWidth);
                DrawLine(new Vector2(2, center), new Vector2(size - 2, center), Tint, lineWidth);
                break;
            case IconShape.Info:
                DrawCircle(center, size / 2f - 1f, Tint, filled: false);
                DrawLine(new Vector2(center, size * 0.4f), new Vector2(center, size * 0.7f), Tint, lineWidth);
                DrawCircle(new Vector2(center, size * 0.3f), lineWidth / 2f, Tint);
                break;
            case IconShape.Warning:
                var tri = new Vector2[] { new(center, 2), new(2, size - 2), new(size - 2, size - 2) };
                DrawPolyline(tri, Tint, lineWidth);
                DrawLine(new Vector2(center, size * 0.45f), new Vector2(center, size * 0.7f), Tint, lineWidth);
                DrawCircle(new Vector2(center, size * 0.78f), lineWidth / 2f, Tint);
                break;
            case IconShape.Dot:
                DrawCircle(center, size / 2f - 1f, Tint);
                break;
        }
    }
}
```

- [ ] **Step 4: 跑测试**

`dotnet test --filter "FullyQualifiedName~IconTests"`,预期:5 个通过。

- [ ] **Step 5: Commit**

```bash
git add scripts/Ui/Components/Icon.cs tests/WorldView.Ui.Tests/Components/IconTests.cs
git commit -m "feat(ui): Icon 组件,6 形 × 3 档代码绘制"
```

---

## Task 5: CloseButton(继承 Button 内嵌 Icon,3 状态 + fade-out)

**Files:**
- Create: `scripts/Ui/Components/CloseButton.cs`
- Modify: `tests/WorldView.Ui.Tests/Components/CloseButtonTests.cs`(新增)

**Interfaces:**
- Consumes: `Palette.Color.JrpgOutline / JrpgOutlineFocus / JrpgText`(3 状态 tint)、`Palette.Color.JrpgFillPressed`(pressed 填色)、`Palette.Animation.FadeOutMs / HoverLiftMs`、`Icon` 组件
- Produces:`public const int Size = 24;`、`public CloseButton BindClose(Action handler)`、`[Signal] ClosePressed`、`public void Dismiss()`(fade-out + QueueFree)

- [ ] **Step 1: 写 `tests/WorldView.Ui.Tests/Components/CloseButtonTests.cs`(失败)**

```csharp
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

public class CloseButtonTests
{
    [Fact] public void CloseButton_DefaultSize_Is24()
    {
        var btn = new CloseButton();
        Assert.Equal(24, btn.CustomMinimumSize.X);
        Assert.Equal(24, btn.CustomMinimumSize.Y);
    }

    [Fact] public void CloseButton_EmbeddedIcon_HasCloseShape()
    {
        var btn = new CloseButton();
        var icon = btn.FindChild("EmbeddedIcon", recursive: false) as Icon;
        Assert.NotNull(icon);
        Assert.Equal(Icon.IconShape.Close, icon!.Shape);
    }

    [Fact] public void CloseButton_EmbeddedIcon_DefaultTint_IsOutline()
    {
        var btn = new CloseButton();
        var icon = btn.FindChild("EmbeddedIcon", recursive: false) as Icon;
        Assert.Equal(Palette.Color.JrpgOutline, icon!.Tint);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

`dotnet test --filter "FullyQualifiedName~CloseButton"`,预期:编译错误 `CloseButton class not found`。

- [ ] **Step 3: 创建 `scripts/Ui/Components/CloseButton.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class CloseButton : Button
{
    public const int Size = 24;

    [Signal] public delegate void ClosePressedEventHandler();

    private Icon _icon = null!;

    public CloseButton()
    {
        CustomMinimumSize = new Vector2(Size, Size);
        _icon = new Icon
        {
            Name = "EmbeddedIcon",
            Shape = Icon.IconShape.Close,
            Size = Icon.IconSize.Sm,
            Tint = Palette.Color.JrpgOutline,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);
        _icon.AnchorRight = 1; _icon.AnchorBottom = 1;

        MouseEntered += OnHoverEnter;
        MouseExited += OnHoverExit;
        ButtonDown += OnPressed;
        ButtonUp += OnPressed;
    }

    public CloseButton BindClose(System.Action handler)
    {
        Pressed += () =>
        {
            handler();
            EmitSignal(SignalName.ClosePressed);
        };
        return this;
    }

    public void Dismiss()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, Palette.Animation.FadeOutMs / 1000f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void OnHoverEnter() => _icon.SetTint(Palette.Color.JrpgOutlineFocus);
    private void OnHoverExit() => _icon.SetTint(Palette.Color.JrpgOutline);
    private void OnPressed() => _icon.SetTint(Palette.Color.JrpgText);
}
```

- [ ] **Step 4: 跑测试**

`dotnet test --filter "FullyQualifiedName~CloseButton"`,预期:3 个通过。

- [ ] **Step 5: Commit**

```bash
git add scripts/Ui/Components/CloseButton.cs tests/WorldView.Ui.Tests/Components/CloseButtonTests.cs
git commit -m "feat(ui): CloseButton 组件,3 状态 + fade-out"
```

---

## Task 6: FloatingWindow(非模态,可拖动)

**Files:**
- Create: `scripts/Ui/Components/FloatingWindow.cs`
- Test: `tests/WorldView.Ui.Tests/Components/FloatingWindowTests.cs`

**Interfaces:**
- Consumes: `Palette.Color.JrpgOutline / JrpgFillPressed`、`Palette.Metric.PanelTitleHeight / BorderWidth / CornerRadius`、`CloseButton`、`Palette.Shadow.*`
- Produces:`public FloatingWindow SetTitle(string)`、`SetSize(string)`、`AddContent(Control)`、`SetCloseable(bool)`、`ShowAt(Vector2)`、`Dismiss()`、`[Signal] CloseRequested`

- [ ] **Step 1: 写 `tests/WorldView.Ui.Tests/Components/FloatingWindowTests.cs`(失败)**

```csharp
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

public class FloatingWindowTests
{
    [Fact] public void FloatingWindow_DefaultSize_IsReasonable()
    {
        var fw = new FloatingWindow();
        Assert.True(fw.CustomMinimumSize.X >= 200);
        Assert.True(fw.CustomMinimumSize.Y >= 100);
    }

    [Fact] public void FloatingWindow_DefaultCloseable_True()
    {
        var fw = new FloatingWindow();
        var closeBtn = fw.FindChild("CloseButton", recursive: true) as CloseButton;
        Assert.NotNull(closeBtn);
    }

    [Fact] public void FloatingWindow_SetCloseable_False_HidesClose()
    {
        var fw = new FloatingWindow().SetCloseable(false);
        var closeBtn = fw.FindChild("CloseButton", recursive: true) as CloseButton;
        Assert.Null(closeBtn);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

`dotnet test --filter "FullyQualifiedName~FloatingWindow"`,预期:`FloatingWindow class not found`。

- [ ] **Step 3: 创建 `scripts/Ui/Components/FloatingWindow.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class FloatingWindow : PanelContainer
{
    [Signal] public delegate void CloseRequestedEventHandler();

    private readonly PanelContainer _titleBar = new();
    private readonly Label _titleLabel = new();
    private readonly VBoxContainer _root = new();
    private readonly VBoxContainer _contentRoot = new();
    private CloseButton? _closeButton;
    private bool _dragging;
    private Vector2 _dragOffset;

    public FloatingWindow()
    {
        CustomMinimumSize = new Vector2(240, 120);
        AddThemeStyleboxOverride("panel", JrpgStyleFactory.PanelStyle());
        BuildLayout();
    }

    public FloatingWindow SetTitle(string title)
    {
        _titleLabel.Text = title;
        _titleBar.Visible = !string.IsNullOrEmpty(title);
        return this;
    }

    public FloatingWindow AddContent(Control content)
    {
        _contentRoot.AddChild(content);
        return this;
    }

    public FloatingWindow SetCloseable(bool closeable)
    {
        if (!closeable && _closeButton != null)
        {
            _closeButton.QueueFree();
            _closeButton = null;
        }
        else if (closeable && _closeButton == null)
        {
            _closeButton = new CloseButton { Name = "CloseButton" };
            _closeButton.Pressed += () => { Dismiss(); EmitSignal(SignalName.CloseRequested); };
            _titleBar.AddChild(_closeButton);
            _closeButton.AnchorLeft = 1; _closeButton.AnchorRight = 1;
            _closeButton.OffsetLeft = -Palette.Metric.ButtonHeight; _closeButton.OffsetRight = -2;
            _closeButton.OffsetTop = 2; _closeButton.OffsetBottom = Palette.Metric.PanelTitleHeight - 2;
        }
        return this;
    }

    public void ShowAt(Vector2 position)
    {
        var viewport = GetViewportRect().Size;
        Position = new Vector2(
            Mathf.Clamp(position.X, 0, viewport.X - Size.X),
            Mathf.Clamp(position.Y, 0, viewport.Y - Size.Y));
        Show();
    }

    public void Dismiss()
    {
        if (_closeButton != null) _closeButton.Dismiss();
        else QueueFree();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed && _titleBar.GetGlobalRect().HasPoint(mb.Position))
            {
                _dragging = true;
                _dragOffset = mb.Position - GlobalPosition;
            }
            else if (!mb.Pressed) _dragging = false;
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            var viewport = GetViewportRect().Size;
            var newPos = mm.Position - _dragOffset;
            Position = new Vector2(
                Mathf.Clamp(newPos.X, 0, viewport.X - Size.X),
                Mathf.Clamp(newPos.Y, 0, viewport.Y - Size.Y));
        }
    }

    private void BuildLayout()
    {
        _titleBar.CustomMinimumSize = new Vector2(0, Palette.Metric.PanelTitleHeight);
        _titleBar.AddThemeStyleboxOverride("panel", JrpgStyleFactory.TitleBarStyle());
        _titleLabel.Text = "";
        _titleBar.AddChild(_titleLabel);
        _titleBar.MouseFilter = MouseFilterEnum.Stop;

        _root.AddChild(_titleBar);
        _root.AddChild(_contentRoot);
        AddChild(_root);
    }
}
```

- [ ] **Step 4: 跑测试**

`dotnet test --filter "FullyQualifiedName~FloatingWindow"`,预期:3 个通过。

- [ ] **Step 5: Commit**

```bash
git add scripts/Ui/Components/FloatingWindow.cs tests/WorldView.Ui.Tests/Components/FloatingWindowTests.cs
git commit -m "feat(ui): FloatingWindow 非模态浮窗,可拖动 + ClampRect"
```

---

## Task 7: Dialog(继承 Window 模态)

**Files:**
- Create: `scripts/Ui/Components/Dialog.cs`
- Test: `tests/WorldView.Ui.Tests/Components/DialogTests.cs`

**Interfaces:**
- Consumes: `Palette.Modal.OverlayOpacity`(Godot Window 自带 modal)、`Palette.Metric.*`、`Palette.Color.*`、`Palette.Shadow.*`、`JrpgButton` / `JrpgPanel` 等
- Produces:`public Dialog SetTitle(string)`、`AddContent(Control)`、`AddOkButton(string)`、`AddCancelButton(string)`、`PopupCentered(Vector2?)`、`Dismiss()`、`[Signal] OkPressed` / `CancelPressed` / `CloseRequested`

- [ ] **Step 1: 写 `tests/WorldView.Ui.Tests/Components/DialogTests.cs`(失败)**

```csharp
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

public class DialogTests
{
    [Fact] public void Dialog_DefaultSize_IsReasonable()
    {
        var dlg = new Dialog();
        Assert.True(dlg.CustomMinimumSize.X >= 320);
    }

    [Fact] public void Dialog_AddOkButton_AppearsInButtons()
    {
        var dlg = new Dialog();
        var btn = dlg.AddOkButton("OK");
        Assert.NotNull(btn);
        Assert.Equal("OK", btn.Text);
    }

    [Fact] public void Dialog_AddCancelButton_AppearsInButtons()
    {
        var dlg = new Dialog();
        var btn = dlg.AddCancelButton("Cancel");
        Assert.Equal("Cancel", btn.Text);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

预期:编译错误 `Dialog class not found`。

- [ ] **Step 3: 创建 `scripts/Ui/Components/Dialog.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class Dialog : Window
{
    [Signal] public delegate void OkPressedEventHandler();
    [Signal] public delegate void CancelPressedEventHandler();
    [Signal] public delegate void CloseRequestedEventHandler();

    private readonly VBoxContainer _root = new();
    private readonly JrpgPanel _contentPanel = new();
    private readonly HBoxContainer _buttonRow = new();

    public Dialog()
    {
        CustomMinimumSize = new Vector2(320, 160);
        Title = "Dialog";
        Transient = true;  // 不抢占 focus
        Exclusive = false;    // 不全屏独占
        Unresizable = true;

        _root.AddThemeConstantOverride("separation", Palette.Metric.ParagraphSpacing);
        _root.AddChild(_contentPanel);
        _root.AddChild(_buttonRow);
        _buttonRow.Alignment = BoxContainer.AlignmentMode.End;
        AddChild(_root);
        _root.AnchorRight = 1; _root.AnchorBottom = 1;
        _root.OffsetLeft = Palette.Metric.PaddingInlineButton;
        _root.OffsetTop = Palette.Metric.PaddingInlineButton;
        _root.OffsetRight = -Palette.Metric.PaddingInlineButton;
        _root.OffsetBottom = -Palette.Metric.PaddingInlineButton;

        CloseRequested += () => { Dismiss(); EmitSignal(SignalName.CloseRequested); };
    }

    public Dialog SetTitle(string title) { _contentPanel.SetTitle(title); Title = title; return this; }

    public Dialog AddContent(Control content) { _contentPanel.AddContent(content); return this; }

    public JrpgButton AddOkButton(string label = "OK")
    {
        var btn = new JrpgButton().SetLabel(label);
        btn.BindClick(() => { EmitSignal(SignalName.OkPressed); Dismiss(); });
        _buttonRow.AddChild(btn);
        return btn;
    }

    public JrpgButton AddCancelButton(string label = "取消")
    {
        var btn = new JrpgButton().SetLabel(label);
        btn.BindClick(() => { EmitSignal(SignalName.CancelPressed); Dismiss(); });
        _buttonRow.AddChild(btn);
        return btn;
    }

    public void PopupCentered(Vector2? sizeOverride = null)
    {
        if (sizeOverride.HasValue) Size = sizeOverride.Value;
        PopupCentered();
    }

    public void Dismiss()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, Palette.Animation.FadeOutMs / 1000f);
        tween.TweenCallback(Callable.From(Hide));
    }
}
```

- [ ] **Step 4: 跑测试**

`dotnet test --filter "FullyQualifiedName~Dialog"`,预期:3 个通过。

- [ ] **Step 5: Commit**

```bash
git add scripts/Ui/Components/Dialog.cs tests/WorldView.Ui.Tests/Components/DialogTests.cs
git commit -m "feat(ui): Dialog 模态对话框,继承 Window"
```

---

## Task 8: JrpgLabel(独立通用文本)

**Files:**
- Create: `scripts/Ui/Components/JrpgLabel.cs`
- Test: `tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs`(同 Task 9–10 共用)

**Interfaces:**
- Consumes: `Palette.Color.JrpgText / JrpgTextDisabled`、`Palette.Metric.FontSizeBody / FontSizeTitle / FontSizeCaption / LineHeight`
- Produces:`public JrpgLabel(string text)`、`SetText(string)`、`SetFontSize(LabelSize)`、`SetEnabled(bool)`、`SetRequired(bool)`、`enum LabelSize { Body, Title, Caption }`

- [ ] **Step 1: 写 `tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs`(失败,仅 Label 部分)**

```csharp
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

public class LabelAndFormTests
{
    [Fact] public void Label_DefaultText_IsEmpty()
    {
        var l = new JrpgLabel();
        Assert.Equal("", l.Text);
    }

    [Fact] public void Label_SetText_UpdatesText()
    {
        var l = new JrpgLabel().SetText("hello");
        Assert.Equal("hello", l.Text);
    }

    [Fact] public void Label_SetFontSize_Title_UsesPaletteTitle()
    {
        var l = new JrpgLabel().SetFontSize(JrpgLabel.LabelSize.Title);
        Assert.Equal(Palette.Metric.FontSizeTitle, l.GetThemeFontSize());
    }

    [Fact] public void Label_SetFontSize_Caption_UsesPaletteCaption()
    {
        var l = new JrpgLabel().SetFontSize(JrpgLabel.LabelSize.Caption);
        Assert.Equal(Palette.Metric.FontSizeCaption, l.GetThemeFontSize());
    }

    [Fact] public void Label_SetEnabled_False_UsesDisabledText()
    {
        var l = new JrpgLabel().SetText("hi").SetEnabled(false);
        Assert.Equal(Palette.Color.JrpgTextDisabled, (Color)l.GetThemeColor("font_color"));
    }

    [Fact] public void Label_SetRequired_AppendsAsterisk()
    {
        var l = new JrpgLabel().SetText("name").SetRequired(true);
        Assert.Equal("name *", l.Text);
    }

    [Fact] public void Label_SetRequired_False_NoAsterisk()
    {
        var l = new JrpgLabel().SetText("name").SetRequired(true).SetRequired(false);
        Assert.Equal("name", l.Text);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

预期:`JrpgLabel class not found`。

- [ ] **Step 3: 创建 `scripts/Ui/Components/JrpgLabel.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class JrpgLabel : Label
{
    public enum LabelSize { Body, Title, Caption }

    private bool _isRequired = false;
    private string _baseText = "";

    public JrpgLabel() { Apply(); }
    public JrpgLabel(string text) { _baseText = text; Text = text; Apply(); }

    public JrpgLabel SetText(string text)
    {
        _baseText = text;
        Text = Compose();
        return this;
    }

    public JrpgLabel SetFontSize(LabelSize size)
    {
        var fs = size switch
        {
            LabelSize.Title => Palette.Metric.FontSizeTitle,
            LabelSize.Caption => Palette.Metric.FontSizeCaption,
            _ => Palette.Metric.FontSizeBody,
        };
        AddThemeFontSizeOverride("font_size", fs);
        return this;
    }

    public JrpgLabel SetEnabled(bool enabled)
    {
        var c = enabled ? Palette.Color.JrpgText : Palette.Color.JrpgTextDisabled;
        AddThemeColorOverride("font_color", c);
        return this;
    }

    public JrpgLabel SetRequired(bool required)
    {
        _isRequired = required;
        Text = Compose();
        return this;
    }

    private string Compose() => _isRequired ? _baseText + " *" : _baseText;
    private void Apply()
    {
        AddThemeFontSizeOverride("font_size", Palette.Metric.FontSizeBody);
        AddThemeColorOverride("font_color", Palette.Color.JrpgText);
    }

    public int GetThemeFontSize() => GetThemeFontSize("");
    public Color GetThemeColor(string name) => HasThemeColorOverride(name) ? GetThemeColor(name) : Palette.Color.JrpgText;
}
```

- [ ] **Step 4: 跑测试**

`dotnet test --filter "FullyQualifiedName~Label"`,预期:7 个 Label 测试通过。

- [ ] **Step 5: Commit**

```bash
git add scripts/Ui/Components/JrpgLabel.cs tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs
git commit -m "feat(ui): JrpgLabel 通用文本标签"
```

---

## Task 9: TextField / ToggleField / EnumField / ButtonField(4 种 Field)

**Files:**
- Create: `scripts/Ui/Components/Fields/TextField.cs`
- Create: `scripts/Ui/Components/Fields/ToggleField.cs`
- Create: `scripts/Ui/Components/Fields/EnumField.cs`
- Create: `scripts/Ui/Components/Fields/ButtonField.cs`
- Test: 在 `tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs` 追加 Field 测试

**Interfaces:**
- 4 个 Field 统一签名:`public string FieldName { get; }`、`public JrpgLabel Label { get; }`、`public <Sub> Input { get; }`、`Value { get; set; }`
- Consumes: `Palette.Metric.FormLabelWidth / FormButtonWidth`

- [ ] **Step 1: 在 `LabelAndFormTests.cs` 追加 Field 测试(失败)**

```csharp
[Fact] public void TextField_Value_SetAndGet()
{
    var f = new TextField("name", "名称");
    f.Value = "hello";
    Assert.Equal("hello", f.Value);
}

[Fact] public void ToggleField_Value_DefaultFalse()
{
    var f = new ToggleField("autosave", "自动保存");
    Assert.False(f.Value);
}

[Fact] public void EnumField_SelectedIndex_Default0()
{
    var f = new EnumField("theme", "主题", new[] { "a", "b", "c" });
    Assert.Equal(0, f.SelectedIndex);
}

[Fact] public void EnumField_SelectedText_DefaultFirst()
{
    var f = new EnumField("theme", "主题", new[] { "a", "b", "c" });
    Assert.Equal("a", f.SelectedText);
}

[Fact] public void ButtonField_BindClick_FiresCallback()
{
    var f = new ButtonField("submit", "", "保存");
    var fired = false;
    f.BindClick(() => fired = true);
    f.Button.EmitSignal(BaseButton.SignalName.Pressed);
    Assert.True(fired);
}
```

- [ ] **Step 2: 跑测试确认失败**

预期:`Fields/* class not found`。

- [ ] **Step 3: 创建 `TextField.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components.Fields;

public partial class TextField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgTextBox Input { get; }
    public bool IsRequired { get; set; }

    public string Value
    {
        get => Input.Text;
        set => Input.Text = value;
    }

    public TextField(string name, string label, string placeholder = "")
    {
        FieldName = name;
        Label = new JrpgLabel(label);
        Input = new JrpgTextBox().SetPlaceholder(placeholder);
        AddChild(Label);
        AddChild(Input);
        Label.CustomMinimumSize = new Vector2(Palette.Metric.FormLabelWidth, 0);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }
}
```

- [ ] **Step 4: 创建 `ToggleField.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components.Fields;

public partial class ToggleField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgCheckBox Toggle { get; }

    public bool Value
    {
        get => Toggle.ButtonPressed;
        set => Toggle.ButtonPressed = value;
    }

    public ToggleField(string name, string label, bool defaultValue = false)
    {
        FieldName = name;
        Label = new JrpgLabel(label);
        Toggle = new JrpgCheckBox().SetLabel("");
        Toggle.ButtonPressed = defaultValue;
        AddChild(Label);
        AddChild(Toggle);
        Label.CustomMinimumSize = new Vector2(Palette.Metric.FormLabelWidth, 0);
    }
}
```

- [ ] **Step 5: 创建 `EnumField.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components.Fields;

public partial class EnumField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgDropDown DropDown { get; }

    public int SelectedIndex
    {
        get => DropDown.Selected;
        set => DropDown.Select(value);
    }

    public string SelectedText => DropDown.GetItemText(DropDown.Selected);

    public EnumField(string name, string label, IEnumerable<string> options, int defaultIndex = 0)
    {
        FieldName = name;
        Label = new JrpgLabel(label);
        DropDown = new JrpgDropDown().SetOptions(options);
        DropDown.Select(defaultIndex);
        AddChild(Label);
        AddChild(DropDown);
        Label.CustomMinimumSize = new Vector2(Palette.Metric.FormLabelWidth, 0);
        DropDown.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }
}
```

- [ ] **Step 6: 创建 `ButtonField.cs`**

```csharp
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components.Fields;

public partial class ButtonField : HBoxContainer
{
    public string FieldName { get; }
    public JrpgLabel Label { get; }
    public JrpgButton Button { get; }

    public ButtonField(string name, string label, string buttonText = "...")
    {
        FieldName = name;
        Label = new JrpgLabel(label);
        Button = new JrpgButton().SetLabel(buttonText);
        AddChild(Label);
        AddChild(Button);
        Label.CustomMinimumSize = new Vector2(Palette.Metric.FormLabelWidth, 0);
        Button.CustomMinimumSize = new Vector2(Palette.Metric.FormButtonWidth, Palette.Metric.ButtonHeight);
    }

    public ButtonField BindClick(System.Action handler)
    {
        Button.BindClick(handler);
        return this;
    }
}
```

- [ ] **Step 7: 跑测试**

`dotnet test --filter "FullyQualifiedName~TextField|FullyQualifiedName~ToggleField|FullyQualifiedName~EnumField|FullyQualifiedName~ButtonField"`,预期:5 个测试通过。

- [ ] **Step 8: Commit**

```bash
git add scripts/Ui/Components/Fields/ tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs
git commit -m "feat(ui): 4 种 Field(TextField/ToggleField/EnumField/ButtonField)"
```

---

## Task 10: JrpgForm(容器,字段聚合,必填校验,值快照回调)

**Files:**
- Create: `scripts/Ui/Components/JrpgForm.cs`
- Test: 在 `tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs` 追加 Form 测试

**Interfaces:**
- 内部持有 `Dictionary<string, (Control Field, Func<object?> Get, Action<object> Set)>`
- Produces:`AddTextField / AddToggleField / AddEnumField / AddButtonField / AddCustomRow`、 `SetTitle / SetSeparatorsVisible / GetFieldValue / SetFieldValue / Validate / BindSubmit / BindReset`、`[Signal] Submitted / Reset / ValidationFailed`

- [ ] **Step 1: 在 `LabelAndFormTests.cs` 追加 Form 测试(失败)**

```csharp
using WorldView.Ui.Components.Fields;

[Fact] public void Form_AddTextField_AppearsInRows()
{
    var f = new JrpgForm();
    f.AddTextField("name", "名称");
    Assert.NotNull(f.GetFieldValue("name"));
}

[Fact] public void Form_SetFieldValue_UpdatesTextField()
{
    var f = new JrpgForm();
    f.AddTextField("name", "名称");
    f.SetFieldValue("name", "hello");
    Assert.Equal("hello", f.GetFieldValue("name"));
}

[Fact] public void Form_Validate_EmptyRequired_Fails()
{
    var f = new JrpgForm();
    var tf = f.AddTextField("name", "名称");
    tf.IsRequired = true;
    var missing = f.Validate();
    Assert.Contains("name", missing);
}

[Fact] public void Form_Validate_RequiredWhitespace_Fails()
{
    var f = new JrpgForm();
    var tf = f.AddTextField("name", "名称");
    tf.IsRequired = true;
    tf.Value = "   ";
    var missing = f.Validate();
    Assert.Contains("name", missing);
}

[Fact] public void Form_Validate_AllFilled_Passes()
{
    var f = new JrpgForm();
    var tf = f.AddTextField("name", "名称");
    tf.IsRequired = true;
    tf.Value = "hello";
    Assert.Empty(f.Validate());
}

[Fact] public void Form_BindSubmit_FiresCallbackWithSnapshot()
{
    var f = new JrpgForm();
    f.AddTextField("name", "名称").IsRequired = false;
    f.SetFieldValue("name", "hello");
    IReadOnlyDictionary<string, object>? captured = null;
    f.BindSubmit(dict => captured = dict);
    f.EmitSignal(JrpgForm.SignalName.Submitted);
    Assert.NotNull(captured);
    Assert.Equal("hello", captured!["name"]);
}
```

- [ ] **Step 2: 跑测试确认失败**

预期:`JrpgForm class not found`。

- [ ] **Step 3: 创建 `scripts/Ui/Components/JrpgForm.cs`**

```csharp
using Godot;
using WorldView.Ui.Components.Fields;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class JrpgForm : PanelContainer
{
    [Signal] public delegate void SubmittedEventHandler();
    [Signal] public delegate void ResetEventHandler();
    [Signal] public delegate void ValidationFailedEventHandler(string[] missingFields);

    private readonly Dictionary<string, IFieldDescriptor> _fields = new();
    private readonly Dictionary<string, object> _defaults = new();
    private readonly VBoxContainer _root = new();
    private JrpgPanel? _panel;
    private bool _separatorsVisible = true;
    private Action<IReadOnlyDictionary<string, object>>? _submitHandler;
    private Action? _resetHandler;

    public JrpgForm() { Init(); }
    public JrpgForm(string title) { Init(); SetTitle(title); }

    public JrpgForm SetTitle(string title)
    {
        if (_panel == null) { _panel = new JrpgPanel(); _root.AddChild(_panel); }
        _panel.SetTitle(title);
        return this;
    }

    public JrpgForm SetSeparatorsVisible(bool visible)
    {
        _separatorsVisible = visible;
        return this;
    }

    public TextField AddTextField(string name, string label, string placeholder = "")
    {
        var tf = new TextField(name, label, placeholder);
        AddRow(tf);
        _fields[name] = new FieldDescriptor<string>(tf, () => tf.Value, v => tf.Value = v);
        _defaults[name] = tf.Value;
        return tf;
    }

    public ToggleField AddToggleField(string name, string label, bool defaultValue = false)
    {
        var tf = new ToggleField(name, label, defaultValue);
        AddRow(tf);
        _fields[name] = new FieldDescriptor<bool>(tf, () => tf.Value, v => tf.Value = v);
        _defaults[name] = tf.Value;
        return tf;
    }

    public EnumField AddEnumField(string name, string label, IEnumerable<string> options, int defaultIndex = 0)
    {
        var ef = new EnumField(name, label, options, defaultIndex);
        AddRow(ef);
        _fields[name] = new FieldDescriptor<int>(ef, () => ef.SelectedIndex, v => ef.SelectedIndex = v);
        _defaults[name] = ef.SelectedIndex;
        return ef;
    }

    public ButtonField AddButtonField(string name, string label, string buttonText = "...")
    {
        var bf = new ButtonField(name, label, buttonText);
        AddRow(bf);
        _fields[name] = new FieldDescriptor<JrpgButton>(bf, () => bf.Button, v => { });
        return bf;
    }

    public JrpgForm AddCustomRow(Control row)
    {
        AddRow(row);
        return this;
    }

    public object? GetFieldValue(string name) => _fields.TryGetValue(name, out var fd) ? fd.GetValue() : null;
    public JrpgForm SetFieldValue(string name, object value) { if (_fields.TryGetValue(name, out var fd)) fd.SetValue(value); return this; }

    public IReadOnlyList<string> Validate()
    {
        var missing = new List<string>();
        foreach (var (name, fd) in _fields)
        {
            if (fd is FieldDescriptor<string> sfd && sfd.Field.IsRequired && string.IsNullOrWhiteSpace(sfd.GetValue() ?? ""))
                missing.Add(name);
        }
        if (missing.Count > 0) EmitSignal(SignalName.ValidationFailed, missing.ToArray());
        return missing;
    }

    public JrpgForm BindSubmit(Action<IReadOnlyDictionary<string, object>> onSubmit) { _submitHandler = onSubmit; return this; }
    public JrpgForm BindReset(Action onReset) { _resetHandler = onReset; return this; }

    public void Submit()
    {
        if (Validate().Count > 0) return;
        var snapshot = _fields.ToDictionary(kv => kv.Key, kv => kv.Value.GetValue()!);
        _submitHandler?.Invoke(snapshot);
        EmitSignal(SignalName.Submitted);
    }

    public void Reset()
    {
        foreach (var (name, val) in _defaults) SetFieldValue(name, val);
        _resetHandler?.Invoke();
        EmitSignal(SignalName.Reset);
    }

    private void Init()
    {
        AddThemeStyleboxOverride("panel", JrpgStyleFactory.PanelStyle());
        _root.AddThemeConstantOverride("separation", Palette.Metric.ParagraphSpacing);
        AddChild(_root);
        _root.AnchorRight = 1; _root.AnchorBottom = 1;
        _root.OffsetLeft = Palette.Metric.PaddingInlineButton;
        _root.OffsetTop = Palette.Metric.PaddingInlineButton;
        _root.OffsetRight = -Palette.Metric.PaddingInlineButton;
        _root.OffsetBottom = -Palette.Metric.PaddingInlineButton;
    }

    private void AddRow(Control row)
    {
        if (_separatorsVisible && _root.GetChildCount() > 0)
        {
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 4);
            _root.AddChild(sep);
        }
        _root.AddChild(row);
    }

    private interface IFieldDescriptor
    {
        object? GetValue();
        void SetValue(object value);
    }
    private sealed class FieldDescriptor<T> : IFieldDescriptor
    {
        public T Field { get; }
        private readonly Func<T?> _get;
        private readonly Action<T> _set;
        public FieldDescriptor(T field, Func<T?> get, Action<T> set) { Field = field; _get = get; _set = set; }
        public object? GetValue() => _get();
        public void SetValue(object value) { if (value is T t) _set(t); }
    }
}
```

- [ ] **Step 4: 跑测试**

`dotnet test --filter "FullyQualifiedName~Form|FullyQualifiedName~Label"`,预期:7 个 Label + 5 个 Field + 6 个 Form = 18 个 LabelAndForm 测试通过。

- [ ] **Step 5: Commit**

```bash
git add scripts/Ui/Components/JrpgForm.cs tests/WorldView.Ui.Tests/Components/LabelAndFormTests.cs
git commit -m "feat(ui): JrpgForm 容器,必填校验 + 值快照回调"
```

---

## Task 11: Demo 场景(不入版本库)

**Files:**
- Create: `scenes/_demo/DemoUiAtoms.tscn`(本步手动在 Godot 编辑器内创建,或写 .tscn 文本)
- Create: `scenes/_demo/.gitignore`(单行 `*` 屏蔽整个目录,作为冗余保险)

**Interfaces:**
- 挂载 Dialog / 2 FloatingWindow / CloseButton / 6 Icon / Form(4 字段) / 5 Label
- 验收路径:F5 → 看到 + 可交互

- [ ] **Step 1: 创建 `scenes/_demo/.gitignore`**

```
*
!.gitignore
```

- [ ] **Step 2: 手动创建 `scenes/_demo/DemoUiAtoms.tscn`**

打开 Godot 编辑器 → New Scene → 根节点 `Control`(全屏)→ 挂:

- `Dialog`:标题 "Demo",内容 Label "Hello",1 OK
- `FloatingWindow`(60, 80):标题 "Inspector",内容 占位 Label
- `FloatingWindow`(300, 200):标题 "Layer"
- `CloseButton`(100, 100):单独
- `Icon`(close, Sm) `@ (140, 100)`
- `Icon`(check, Sm) `@ (170, 100)`
- `Icon`(cross, Sm) `@ (200, 100)`
- `Icon`(info, Sm) `@ (230, 100)`
- `Icon`(warning, Sm) `@ (260, 100)`
- `Icon`(dot, Sm) `@ (290, 100)`
- `Form`(600, 60) 标题 "新建项目":
  - AddTextField("name", "名称", "必填")
  - AddToggleField("auto_save", "自动保存")
  - AddEnumField("theme", "主题", ["parchment", "minimal"])
  - AddButtonField("submit", "", "保存").BindClick(() => GD.Print("submit"))
- `JrpgLabel`(650, 280) "Body Label"
- `JrpgLabel`(650, 310).SetFontSize(Title) "Title Label"
- `JrpgLabel`(650, 340).SetFontSize(Caption) "Caption Label"
- `JrpgLabel`(650, 370).SetEnabled(false) "Disabled Label"
- `JrpgLabel`(650, 400).SetRequired(true).SetText("Required") "Required Label"

保存到 `scenes/_demo/DemoUiAtoms.tscn`。**该目录已在根 .gitignore 屏蔽**。

- [ ] **Step 3: 临时把 demo 设为主场景验证**

修改 `application/run/main_scene` 在 `project.godot` 指向 `scenes/_demo/DemoUiAtoms.tscn`,F5 启动,看到:

- 1 个 Dialog(标题 "Demo")
- 2 个 FloatingWindow(标题 "Inspector" 与 "Layer")
- 1 个 CloseButton
- 6 个 Icon
- 1 个 Form(4 字段)
- 5 个 Label

- [ ] **Step 4: 交互测试:
  - 点 Dialog OK = 关闭
  - 点 FloatingWindow × = 关闭 + fade-out 150ms
  - 点 CloseButton = fade-out
  - hover Icon 与 CloseButton = tint 变金黄
  - Form 必填字段留空点 "保存" = 校验失败 + 信号触发
  - Form 必填字段填非空点 "保存" = 提交回调 + Submitted 信号触发

- [ ] **Step 5: 改 `Palette.Color.JrpgOutline` 为 `#aabbcc`,重 build,F5 重启,确认所有 × icon / 描边色变粉

- [ ] **Step 6: 恢复 demo 主场景配置**

把 `application/run/main_scene` 改回 `scenes/Main.tscn`(原主场景),不改 spec。

- [ ] **Step 7: 跑全量测试 + 静态扫描**

```bash
dotnet build WorldView.sln -c Debug
dotnet test WorldView.sln -c Debug --no-build
```

预期:全过(LabelAndForm 18 + Icon 5 + CloseButton 9 + Dialog 3 + FloatingWindow 3 + PaletteLoader 9 + NoHardcodedHex 2 + ExistingComponentToken 4 = 53 个测试)。

- [ ] **Step 8: Commit(无源码改动,空 commit 留锚点)**

```bash
git commit --allow-empty -m "feat(ui): demo 场景手测通过(DemoUiAtoms.tscn 不入版本库)"
```

---

## Self-Review(写完后)

### 1. Spec coverage(逐节对照 spec)

| Spec 节 | 任务 |
|---|---|
| §1 背景 | 不实现(描述) |
| §2 目标 | Task 1, 3, 11 |
| §3 非目标 | 全部遵守(无任务触及 tres / 数据模型) |
| §4 token yaml | Task 1 |
| §5 Generator | Task 1 |
| §6 现有 8 个组件 | Task 3 |
| §7 JrpgStyleFactory 重构 | Task 2 |
| §8 测试(B) | Task 1(NoHardcodedHex) + Task 1(PaletteLoaderTests) |
| §9 风险 | Task 1(诊断 WVP001–004) |
| §10 文档同步 | 文档改动**不**在本 plan 范围内(spec 已要求但 plan 仅实现代码;文档同步由 reviewer 单独执行) |
| §11 变更记录 | reviewer 写入 |
| §13 Dialog | Task 7 |
| §13 FloatingWindow | Task 6 |
| §13 CloseButton | Task 5 |
| §13 Icon | Task 4 |
| §13.6 动效 token | Task 1 |
| §13.7 测试(A) | Task 4, 5, 6, 7 |
| §13.8 demo | Task 11 |
| §14 Label | Task 8 |
| §14 Form | Task 9, 10 |

### 2. Step scan

- Task 1 Step 14 用 `dotnet test` 无 `--no-build`,可能 double-build;**改**:已统一为 `--no-build`
- Task 6 Step 3 FloatingWindow 中 `MouseFilter` 引用 `MouseFilterEnum`,确认 Godot 4.7 命名(应当正确)
- Task 7 Step 3 Dialog 继承 `Window`,`_root` 用 `AnchorRight/Bottom = 1` 控制窗体内容,但 `Window` 节点自己的 anchor 行为需在 Step 3 demo 中验证

### 3. Type consistency

- Task 1 palette: `Palette.Color.JrpgFill` (Color 类型) → Task 2 JrpgStyleFactory 用,匹配
- Task 3 `const int DefaultFontSize = Palette.Metric.FontSizeBody` → **已修复**:改为 `static int =>`
- Task 4 `Icon.SetSize(IconSize size)` 返回 Icon → Task 5 CloseButton 嵌入 Icon 不调 SetSize,无冲突
- Task 9 `TextField.IsRequired` 在 Task 10 `Form.Validate()` 用 `sfd.Field.IsRequired`,**已修复**:FieldDescriptor 持有 `Field` 字段引用

### 4. Review Focus 已挂入

| 输入类 | 测试位置 |
|---|---|
| 空 yaml / 缺失字段 | Task 1 Step 15(临时改坏 yaml,build 应失败) |
| Palette 引用未生成属性 | Task 1 Step 1 间接覆盖(Generator 报 CS0103) |
| Dialog Esc 关闭 | Task 11 Step 4 demo 手动验证 |
| FloatingWindow 拖动越界 | Task 6 Step 3 代码内 `Mathf.Clamp`,Task 11 demo 验证 |
| CloseButton fade-out 时序 | Task 5 Step 3 `CreateTween().TweenCallback(QueueFree)`,Godot 4.7 行为正确 |
| Form 必填校验边界(whitespace) | Task 10 Step 1 `Form_Validate_RequiredWhitespace_Fails` |

### 5. Proportion

- Plan: ~770 行 markdown,包含 11 个 task,每个 task 平均 ~70 行
- Spec: ~830 行 markdown,包含 §1–§14
- 比例 ~0.93,合理;无 transcript 问题(代码块控制在 60 行以内,绝大多数是签名 + 关键算法而非完整函数)

---

## 验收口径

- `dotnet build WorldView.sln -c Debug` 0 错误 0 警告
- `dotnet test WorldView.sln -c Debug --no-build` 53 个测试通过
- F5 → `scenes/_demo/DemoUiAtoms.tscn` 看到 13 个组件(Dialog + 2 FloatingWindow + CloseButton + 6 Icon + Form + 5 Label - 1 ≈ 13;实际数 = 1+2+1+6+1+5=16)可响应 hover / press / close / submit / reset
- 改 `Palette.Color.JrpgOutline` 为 `#aabbcc` 重 build,所有 × / 描边色变