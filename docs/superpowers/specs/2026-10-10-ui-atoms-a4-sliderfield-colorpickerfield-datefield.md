# A4 Spec: 数值型 Field 三件套 — SliderField / ColorPickerField / DateField

**目标**: 在已落地的 TextField / ToggleField / EnumField / ButtonField 之外,补 3 个数值/枚举型 Field,使 Form 覆盖完整设计需求(数字滑块、颜色、日期)。

**范围**: 3 Field 各 5 仓单 = **15 xUnit 新增**,UI 总目标 80 + 15 = 95/95 通过。

---

## 1. SliderField (int + 步长)

继承 `Field`(HBoxContainer + 左 JrpgLabel + 右控件)。
- 右控件:**HBoxContainer** 内嵌 `JrpgLabel value` + `HSlider` + `JrpgLabel min..max` 三件
  - HSlider.Value 走 Palette.Metric 行高,Value 显示 jrpg_text
  - Step 整数步长(0 不可,最小 1)
  - Min / Max 必填,Value 在范围内,ValueChanged 同步 value 标签
  - Min / Max 可设为负数
- **Token 接入**: `Palette.Metric.PaddingInlineButton` (分隔) / `Palette.Metric.FormLabelWidth` / `Palette.Color.JrpgText` / `Palette.Color.JrpgTextPlaceholder`(min..max 文字)
- **校验**: Value 必在 [Min, Max] 区间内

API:
```csharp
SliderField
    .SetLabel(string)
    .SetRange(int min, int max, int step = 1)
    .SetValue(int v)
    ValueChanged(int) // 拖动时实时
```

## 2. ColorPickerField (Godot 内建 ColorPicker + 按钮触发弹层)

继承 `Field`。
- 右控件:`HBoxContainer` 内嵌 `ColorRect swatch`(20×20) + `JrpgLabel hex`(显示 `#rrggbb`) + `JrpgButton "..."`(点击触发)
- 点击按钮后:
  - 弹一个**ColorPicker 浮窗**(PanelContainer + CloseButton + 内嵌 Godot `ColorPicker`)
  - ColorPicker.ColorChanged 信号同步 hex 标签 + swatch.Color + 本 Field.Value
  - 浮窗 Esc 关闭(复用 FloatingWindow.CloseRequested)
- **Token 接入**: `Palette.Metric.FormLabelWidth` / `Palette.Color.JrpgText` / `Palette.Color.JrpgOutline` (边框)
- **校验**: Alpha 必为 1.0(简化:仅 RGB,Color 不带 alpha)

API:
```csharp
ColorPickerField
    .SetLabel(string)
    .SetValue(Godot.Color)
    .OnChange(Action<Godot.Color>)
```

## 3. DateField (3 JrpgLabel 显示 + 按钮组 prev/next)

继承 `Field`。
- 右控件:`HBoxContainer` 内嵌 `JrpgLabel date`(显示 `yyyy-MM-dd`) + `JrpgButton "‹"` + `JrpgButton "›"`
- prev/next 按钮按 1 天步进
- 越界:超过 max date 不变(只更新 label,不增减);低于 min date 同样
- Min / Max 必填
- **Token 接入**: `Palette.Metric.FormLabelWidth` / `Palette.Color.JrpgText` / `Palette.Color.JrpgDanger`(越界)
- **校验**: 必在 [Min, Max] 区间内

API:
```csharp
DateField
    .SetLabel(string)
    .SetRange(DateOnly min, DateOnly max)
    .SetValue(DateOnly)
    DateChanged(DateOnly)
```

---

## 4. 15 xUnit 仓单分布

- **SliderFieldTests 5**:
  1. SliderField_Is_Subclass_Of_Field
  2. SliderField_Decimal_Properties (Min/Max/Step/Value 类型 + ReadWrite)
  3. SliderField_Default_Step_Is_1
  4. SliderField_ValueChanged_Signal_Declared (EventHandlerType)
  5. SliderField_IsValid_Requires_Range_Containment (IsValid in range)
- **ColorPickerFieldTests 5**:
  1. ColorPickerField_Is_Subclass_Of_Field
  2. ColorPickerField_Value_Property_Type_Color (ReadWrite)
  3. ColorPickerField_Hex_Format_RR_GG_BB (断言 hex 格式)
  4. ColorPickerField_ColorChanged_Signal_Declared
  5. ColorPickerField_IsValid_Always_True_For_Non_Null_Color
- **DateFieldTests 5**:
  1. DateField_Is_Subclass_Of_Field
  2. DateField_DateOnly_Properties (Min/Max/Value 类型)
  3. DateField_Format_ISO_8601_YYYY_MM_DD (断言格式)
  4. DateField_DateChanged_Signal_Declared
  5. DateField_IsValid_Requires_In_Range

---

## 5. 风险与决策记录

- **SliderField**: 跟现有 ToggleField(继承 Field + 内嵌 CheckBox + Label)模式一致,验证 Godot 4.7 HSlider.Value 是否 double 强转 int 截断 — 测步长 1 时表现一致
- **ColorPickerField**: Godot 内建 ColorPicker 是 Control,需要 AddChild 到浮窗,ColorChanged 信号实时推 → 决定走"点击按钮 → 弹 PanelContainer 浮窗 + ColorPicker + CloseButton"模式,而不是 Popup 弹 ColorPicker(可能太重)
- **DateField**: Godot 没内建 DatePicker,只能 prev/next 按钮;DateOnly 是 .NET 6+ 类型,Godot 4 + .NET 8 OK
- **零硬编码 hex**: 3 组件内不能用 `Color.FromHtml`,所有色走 `Palette.Color.*`,F4 (警告) 走 `Palette.Color.JrpgDanger`,focus 走 `Palette.Color.JrpgOutlineFocus`

---

## 6. Demo 接入

`scenes/_demo/DemoUiAtoms.tscn` 加 3 Field(每 Field 配 1 个标签 + 1 个 validate 按钮),配合 `Form.Validate()` 展示越界报错。
