using System;
using System.Reflection;
using WorldView.Ui.Components;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class DateFieldTests
{
    [Fact]
    public void DateField_Is_Subclass_Of_Field()
    {
        Assert.True(typeof(Field).IsAssignableFrom(typeof(DateField)));
    }

    [Fact]
    public void DateField_DateOnly_Properties()
    {
        var t = typeof(DateField);
        var min = t.GetProperty("Min");
        var max = t.GetProperty("Max");
        var date = t.GetProperty("Date");
        Assert.NotNull(min);
        Assert.NotNull(max);
        Assert.NotNull(date);
        Assert.Equal(typeof(DateOnly), min!.PropertyType);
        Assert.Equal(typeof(DateOnly), max!.PropertyType);
        Assert.Equal(typeof(DateOnly), date!.PropertyType);
        Assert.True(min.CanRead && min.CanWrite);
        Assert.True(max.CanRead && max.CanWrite);
        Assert.True(date.CanRead && date.CanWrite);
    }

    [Fact]
    public void DateField_Format_ISO_8601_YYYY_MM_DD()
    {
        Assert.Equal("2026-10-10", DateField.ToIso(new DateOnly(2026, 10, 10)));
        Assert.Equal("2000-01-01", DateField.ToIso(new DateOnly(2000, 1, 1)));
        Assert.Equal("2099-12-31", DateField.ToIso(new DateOnly(2099, 12, 31)));
        Assert.Equal("0001-01-01", DateField.ToIso(new DateOnly(1, 1, 1)));
    }

    [Fact]
    public void DateField_DateChanged_Signal_Declared()
    {
        var ev = typeof(DateField).GetEvent("DateChanged");
        Assert.NotNull(ev);
        // Godot 不支持 DateOnly 作为 signal 参数,实际类型是 string
        // DateChangedEventHandler 是 Godot 4 [Signal] 生成的嵌套委托
        Assert.Equal("DateChangedEventHandler", ev!.EventHandlerType!.Name);
    }

    [Fact]
    public void DateField_Has_SetLabel_SetRange_SetValue()
    {
        var t = typeof(DateField);
        Assert.NotNull(t.GetMethod("SetLabel", new[] { typeof(string) }));
        Assert.NotNull(t.GetMethod("SetRange", new[] { typeof(DateOnly), typeof(DateOnly) }));
        Assert.NotNull(t.GetMethod("SetValue", new[] { typeof(DateOnly) }));
    }
}
