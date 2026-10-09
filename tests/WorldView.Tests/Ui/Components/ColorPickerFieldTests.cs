using System.Linq;
using System.Reflection;
using Godot;
using WorldView.Ui.Components;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class ColorPickerFieldTests
{
    [Fact]
    public void ColorPickerField_Is_Subclass_Of_Field()
    {
        Assert.True(typeof(Field).IsAssignableFrom(typeof(ColorPickerField)));
    }

    [Fact]
    public void ColorPickerField_Color_Property_Type_Color_ReadWrite()
    {
        var prop = typeof(ColorPickerField).GetProperty("Color");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Color), prop!.PropertyType);
        Assert.True(prop.CanRead && prop.CanWrite);
    }

    [Fact]
    public void ColorPickerField_ToHex_Format_RR_GG_BB()
    {
        // 纯函数: R=0x12 G=0x34 B=0x56 → "#123456"
        var hex = ColorPickerField.ToHex(new Color(0x12 / 255f, 0x34 / 255f, 0x56 / 255f, 1));
        Assert.Equal("#123456", hex);

        // 白色
        Assert.Equal("#FFFFFF", ColorPickerField.ToHex(new Color(1, 1, 1, 1)));

        // 黑色
        Assert.Equal("#000000", ColorPickerField.ToHex(new Color(0, 0, 0, 1)));
    }

    [Fact]
    public void ColorPickerField_ColorChanged_Signal_Declared()
    {
        var ev = typeof(ColorPickerField).GetEvent("ColorChanged");
        Assert.NotNull(ev);
        Assert.Equal("ColorChangedEventHandler", ev!.EventHandlerType!.Name);
    }

    [Fact]
    public void ColorPickerField_Has_SetLabel_SetValue_OnChange_OpenPicker()
    {
        var t = typeof(ColorPickerField);
        Assert.NotNull(t.GetMethod("SetLabel", new[] { typeof(string) }));
        Assert.NotNull(t.GetMethod("SetValue", new[] { typeof(Color) }));
        Assert.NotNull(t.GetMethod("OnChange", new[] { typeof(System.Action<Color>) }));
        Assert.NotNull(t.GetMethod("OpenPicker", System.Type.EmptyTypes));
    }
}
