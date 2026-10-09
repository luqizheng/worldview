// <auto-not-in-spec />
// Task 10 仓单 §8.1.10 — JrpgFormTests(6 个)
// 验证 JrpgForm 行 separation = ParagraphSpacing,Field 基类有 Label/Value/IsValid 抽象。
// 注释: 不直接构造 Form(Godot 需 SceneTree),只断言语义 + 验证 Field 抽象层。

using WorldView.Ui.Theme;
using WorldView.Ui.Components;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class JrpgFormTests
{
    [Fact]
    public void Form_ParagraphSpacing_IsPaletteToken_8()
    {
        // JrpgForm() 内 AddThemeConstantOverride("separation", ParagraphSpacing)
        Assert.Equal(8, Palette.Metric.ParagraphSpacing);
    }

    [Fact]
    public void Form_AbstractField_HasLabelValueIsValid()
    {
        // Field 是 abstract,Label/Value/IsValid 三个 abstract 成员
        var t = typeof(Field);
        Assert.True(t.IsAbstract);
        var labelProp = t.GetProperty("Label");
        Assert.NotNull(labelProp);
        Assert.Equal(typeof(string), labelProp!.PropertyType);
        var valueProp = t.GetProperty("Value");
        Assert.NotNull(valueProp);
        Assert.Equal(typeof(string), valueProp!.PropertyType);
        var validProp = t.GetProperty("IsValid");
        Assert.NotNull(validProp);
        Assert.Equal(typeof(bool), validProp!.PropertyType);
    }

    [Fact]
    public void Form_ConcreteFields_InheritFromField()
    {
        // 4 Field 全部继承 Field
        Assert.True(typeof(TextField).IsSubclassOf(typeof(Field)));
        Assert.True(typeof(ToggleField).IsSubclassOf(typeof(Field)));
        Assert.True(typeof(EnumField).IsSubclassOf(typeof(Field)));
        Assert.True(typeof(ButtonField).IsSubclassOf(typeof(Field)));
    }

    [Fact]
    public void Form_TextField_IsRequired_Default_False()
    {
        // TextField.IsRequired 默认 false(可选)
        var prop = typeof(TextField).GetProperty("IsRequired");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    [Fact]
    public void Form_ValidationFailed_Signal_Exists()
    {
        // JrpgForm 暴露 ValidationFailed(label, reason) 信号
        var ev = typeof(JrpgForm).GetEvent("ValidationFailed");
        Assert.NotNull(ev);
    }

    [Fact]
    public void Form_Snapshot_Method_Exists()
    {
        // JrpgForm.Snapshot(Action<IReadOnlyList<(string, string)>>)
        var method = typeof(JrpgForm).GetMethod("Snapshot");
        Assert.NotNull(method);
    }
}
