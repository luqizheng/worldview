using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用多行富文本（§4.5.2 契约 RichBox 行）
///
/// <para>内部封装 Godot <see cref="RichTextLabel"/>，自动应用 jrpg theme。
/// 支持 BBCode：bold / italic / underline / link。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>多行，行高 22px，段间距 8px</item>
///   <item>圆角 2px，2px 焦褐描边</item>
///   <item>填色 #fff8e7，字号 14px 衬线</item>
///   <item>link 颜色 #3a5a8a + 1px 虚线下划线</item>
/// </list>
///
/// <para>2 状态：normal / link-hover（由 RichTextLabel meta-hover 内置驱动）</para>
/// </summary>
public partial class JrpgRichBox : RichTextLabel
{
    /// <summary>JRPG 富文本字号。</summary>
    public const int DefaultFontSize = WorldView.Ui.Theme.Palette.Metric.FontSizeBody;

    /// <summary>JRPG 富文本行高。</summary>
    public const int DefaultLineHeight = WorldView.Ui.Theme.Palette.Metric.LineHeight;

    /// <summary>JRPG 富文本段间距。</summary>
    public const int DefaultParagraphSpacing = WorldView.Ui.Theme.Palette.Metric.ParagraphSpacing;

    /// <summary>链接点击事件（meta 为用户在 [url] 标签里设的 id）。</summary>
    [Signal] public delegate void LinkClickedEventHandler(string meta);

    /// <summary>JRPG 风格：自动加载 theme 并开启 BBCode。</summary>
    public JrpgRichBox()
    {
        ApplyJrpgTheme();
    }

    /// <summary>语义化：设置 BBCode 文本（自动开启 bbcode_enabled）。</summary>
    public JrpgRichBox SetBbcode(string bbcode)
    {
        BbcodeEnabled = true;
        Text = bbcode;
        return this;
    }

    /// <summary>语义化：清空。</summary>
    public new JrpgRichBox Clear()
    {
        Text = string.Empty;
        return this;
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
        BbcodeEnabled = true;
        FitContent = true;
        ScrollActive = true;
        AddThemeFontSizeOverride("normal_font_size", DefaultFontSize);
        AddThemeFontSizeOverride("bold_font_size", DefaultFontSize);
        AddThemeFontSizeOverride("italics_font_size", DefaultFontSize);
        AddThemeFontSizeOverride("bold_italics_font_size", DefaultFontSize);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
        MetaClicked += meta => EmitSignal(SignalName.LinkClicked, meta);
    }
}
