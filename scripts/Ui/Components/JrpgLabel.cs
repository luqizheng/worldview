using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用文本标签（§4.5.2 契约 Field 行内 label / 独立信息展示）
///
/// <para>独立通用 <see cref="Label"/>,默认:</para>
/// <list type="bullet">
///   <item>字号 <c>FontSizeBody</c>(14)</item>
///   <item>颜色 <c>JrpgText</c>(深棕)</item>
///   <item>行高 <c>LineHeight</c>(22)</item>
/// </list>
///
/// <para>调用方可链式 <c>SetText / SetColor / SetFontSize / SetAlignment</c> 覆盖默认值。
/// 不依赖 <c>RichTextLabel</c>,需要富文本用 <see cref="JrpgRichBox"/>。</para>
/// </summary>
public partial class JrpgLabel : Label
{
    /// <inheritdoc/>
    public JrpgLabel()
    {
        AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeBody);
        AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
    }

    /// <summary>语义化:设置文本。</summary>
    public JrpgLabel SetText(string text)
    {
        Text = text;
        return this;
    }

    /// <summary>语义化:设置颜色(覆盖默认 JrpgText)。</summary>
    public JrpgLabel SetColor(Color color)
    {
        AddThemeColorOverride("font_color", color);
        return this;
    }

    /// <summary>语义化:设置字号(覆盖默认 FontSizeBody)。</summary>
    public JrpgLabel SetFontSize(int size)
    {
        AddThemeFontSizeOverride("font_size", size);
        return this;
    }

    /// <summary>语义化:水平对齐方式。</summary>
    public JrpgLabel SetAlignment(HorizontalAlignment alignment)
    {
        HorizontalAlignment = alignment;
        return this;
    }
}
