# A4 Plan: SliderField / ColorPickerField / DateField

**目标**: 在 11 + 5 组件之外,补 3 数值型 Field,各 5 仓单 = 15 新增,UI 总目标 95/95 通过。

---

## Task 1: SliderField + 5 仓单

- `scripts/Ui/Components/SliderField.cs`:
  - partial class SliderField : Field
  - 行布局:左 JrpgLabel(FormLabelWidth=100) + 右 HBoxContainer( JrpgLabel value + HSlider + JrpgLabel range)
  - public int Min { get; set; }
  - public int Max { get; set; } = 100
  - public int Step { get; set; } = 1
  - public int Value { get; set; } (setter 同步 HSlider.Value + value 标签 + 触发 ValueChanged)
  - [Signal] ValueChangedEventHandler(int v)
  - SliderField SetLabel(string) / SetRange(int min, int max, int step) / SetValue(int)
  - override bool IsValid => Value >= Min && Value <= Max
- `tests/WorldView.Tests/Ui/Components/SliderFieldTests.cs` 5 仓单

## Task 2: ColorPickerField + 5 仓单

- `scripts/Ui/Components/ColorPickerField.cs`:
  - partial class ColorPickerField : Field
  - 行布局:左 JrpgLabel(FormLabelWidth=100) + 右 HBoxContainer(ColorRect swatch 20×20 + JrpgLabel hex + JrpgButton "...")
  - public Color Value { get; set; } = new Color(1,1,1,1) (setter 同步 swatch + hex + ColorChanged)
  - 内部 _pickerWindow: PanelContainer(CloseButton + Godot 内建 ColorPicker)
  - 点击 "..." 按钮 → _pickerWindow = new ...; AddChild to root; SetPosition
  - [Signal] ColorChangedEventHandler(Color c)
  - 关闭按钮触发 _pickerWindow.QueueFree
  - ColorPickerField SetLabel(string) / SetValue(Color) / OnChange(Action<Color>)
  - override bool IsValid => true (Color 永远 valid,简化)
  - private static string ToHex(Color c) => $"#{(int)(c.R*255):X2}{(int)(c.G*255):X2}{(int)(c.B*255):X2}"
- `tests/WorldView.Tests/Ui/Components/ColorPickerFieldTests.cs` 5 仓单

## Task 3: DateField + 5 仓单

- `scripts/Ui/Components/DateField.cs`:
  - partial class DateField : Field
  - 行布局:左 JrpgLabel(FormLabelWidth=100) + 右 HBoxContainer(JrpgLabel date + JrpgButton "‹" + JrpgButton "›")
  - public DateOnly Min { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-10))
  - public DateOnly Max { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(10))
  - public DateOnly Value { get; set; } (setter 同步 date 标签 + 触发 DateChanged)
  - [Signal] DateChangedEventHandler(DateOnly d)
  - 内部 prev/next 按钮:prev = Value.AddDays(-1),next = Value.AddDays(1);越界不更新
  - DateField SetLabel(string) / SetRange(DateOnly min, DateOnly max) / SetValue(DateOnly)
  - override bool IsValid => Value >= Min && Value <= Max
  - private static string ToIso(DateOnly d) => d.ToString("yyyy-MM-dd")
- `tests/WorldView.Tests/Ui/Components/DateFieldTests.cs` 5 仓单

## Task 4: Demo 场景扩展

- `scenes/_demo/DemoUiAtoms.tscn` + `scripts/_demo/DemoUiAtoms.cs`:
  - 加 3 Field(每 Field 配 1 个 JrpgLabel 头部 + 1 个 validate 按钮),显示越界
  - "操作" 行加 1 个 validate form 按钮(JrpgForm 用,可选,纯演示)

## Task 5: build 0 错 + UI 95/95 + 验收

- `dotnet build WorldView.sln` → 0 错
- `dotnet run --project tests/WorldView.Tests --filter "WorldView.Tests.Ui"` → 95/95
- `grep -E "Color\.FromHtml\(" scripts/Ui` → 0
- git commit "feat(ui): A4 数值三件套 — SliderField / ColorPickerField / DateField"
- F5 重测 (用户视觉确认)

---

## 依赖关系

- Task 1 / 2 / 3 独立,可并行写
- Task 4 依赖 Task 1/2/3
- Task 5 依赖 Task 1/2/3/4

## 风险

- ColorPicker 内嵌 PanelContainer 浮窗:Position 走 SetPosition(Vector2),RelativeTo parent
- DateField 按钮:Godot 4 BaseButton.Text 必填(Text="‹"/"›"),Icon 不必
- SliderField HSlider 步长:HSlider.Step 是 double,int 截断用 Math.Round(value/step)*step
