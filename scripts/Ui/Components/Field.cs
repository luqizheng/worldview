using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// Field 抽象基类（§4.5.2 契约 Form 行公共接口）
///
/// <para>所有 4 个具体 Field(<see cref="TextField"/>, <see cref="ToggleField"/>,
/// <see cref="EnumField"/>, <see cref="ButtonField"/>)共享:</para>
/// <list type="bullet">
///   <item><c>Label</c>:行左侧标签文本</item>
///   <item><c>Value</c>:当前值(字符串化,Snapshot 时输出)</item>
///   <item><c>IsValid</c>:校验结果(<see cref="Form.Validate"/> 用)</item>
/// </list>
///
/// <para>Layout 行为(行 separation / Label 列宽)由各具体子类在构造里设。</para>
/// </summary>
public abstract partial class Field : HBoxContainer
{
    /// <summary>行标签文本。</summary>
    public abstract string Label { get; set; }

    /// <summary>当前值,字符串化(Snapshot 时用)。</summary>
    public abstract string Value { get; set; }

    /// <summary>校验结果。</summary>
    public abstract bool IsValid { get; }
}
