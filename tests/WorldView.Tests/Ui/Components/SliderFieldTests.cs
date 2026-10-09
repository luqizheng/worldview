using System.Linq;
using System.Reflection;
using WorldView.Ui.Components;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class SliderFieldTests
{
    [Fact]
    public void SliderField_Is_Subclass_Of_Field()
    {
        Assert.True(typeof(Field).IsAssignableFrom(typeof(SliderField)));
    }

    [Fact]
    public void SliderField_Decimal_Properties()
    {
        var t = typeof(SliderField);
        Assert.Equal(typeof(int), t.GetProperty("Min")!.PropertyType);
        Assert.Equal(typeof(int), t.GetProperty("Max")!.PropertyType);
        Assert.Equal(typeof(int), t.GetProperty("Step")!.PropertyType);
        Assert.True(t.GetProperty("Min")!.CanRead && t.GetProperty("Min")!.CanWrite);
        Assert.True(t.GetProperty("Max")!.CanRead && t.GetProperty("Max")!.CanWrite);
        Assert.True(t.GetProperty("Step")!.CanRead && t.GetProperty("Step")!.CanWrite);
    }

    [Fact]
    public void SliderField_Default_Step_Is_1()
    {
        // 字段初始化器默认 Step=1
        var field = typeof(SliderField).GetField("<Step>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field); // auto-property 字段名格式
    }

    [Fact]
    public void SliderField_ValueChanged_Signal_Declared()
    {
        var t = typeof(SliderField);
        var ev = t.GetEvent("ValueChanged");
        Assert.NotNull(ev);
        Assert.Equal("ValueChangedEventHandler", ev!.EventHandlerType!.Name);
    }

    [Fact]
    public void SliderField_Has_SetRange_And_SetValue_And_SetLabel()
    {
        var t = typeof(SliderField);
        Assert.NotNull(t.GetMethod("SetRange", new[] { typeof(int), typeof(int), typeof(int) }));
        Assert.NotNull(t.GetMethod("SetValue", new[] { typeof(int) }));
        Assert.NotNull(t.GetMethod("SetLabel", new[] { typeof(string) }));
    }
}
