using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用单行输入框（§4.5.2 契约 TextBox 行）
///
/// <para>内部封装 Godot <see cref="LineEdit"/>，自动应用 jrpg theme。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>单行，高 28px，圆角 2px，2px 焦褐描边（焦点态替换为金黄）</item>
///   <item>填色 #fff8e7，占位符 #a89878</item>
///   <item>字号 14px 衬线，左对齐 + 8px 内边距</item>
/// </list>
///
/// <para>3 状态：empty / typed / focus（由 LineEdit 内置驱动）</para>
/// </summary>
public partial class JrpgTextBox : LineEdit
{
    /// <summary>JRPG 输入框高度。</summary>
    public const int DefaultHeight = 28;

    /// <summary>JRPG 输入框字号。</summary>
    public const int DefaultFontSize = 14;

    /// <summary>文本变化事件。</summary>
    [Signal] public delegate void TextChangedEventHandler(string newText);

    /// <summary>JRPG 风格：自动加载 theme 并设置默认样式。</summary>
    public JrpgTextBox()
    {
        ApplyJrpgTheme();
    }

    /// <summary>语义化：设置占位符。</summary>
    public new JrpgTextBox SetPlaceholder(string placeholder)
    {
        PlaceholderText = placeholder;
        return this;
    }

    /// <summary>语义化：设置文本。</summary>
    public JrpgTextBox SetValue(string value)
    {
        Text = value;
        return this;
    }

    /// <summary>语义化：获取当前文本。</summary>
    public string Value => Text;

    /// <summary>语义化：清空。</summary>
    public new JrpgTextBox Clear()
    {
        Text = string.Empty;
        return this;
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
        CustomMinimumSize = new Vector2(0, DefaultHeight);
        AddThemeFontSizeOverride("font_size", DefaultFontSize);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
        TextChanged += newText => EmitSignal(SignalName.TextChanged, newText);
    }
}
