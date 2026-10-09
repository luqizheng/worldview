using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用面板（§4.5.2 契约 Panel 行）
///
/// <para>内部封装 Godot <see cref="PanelContainer"/>，自动应用 jrpg theme。
/// 顶部 24px 标题条 + 内容区两段式布局。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>容器填色 #fff8e7，2px 焦褐描边，2px 圆角</item>
///   <item>顶部 24px 标题条填色 #e8c89a</item>
///   <item>标题 14px 粗体衬线居中</item>
///   <item>阴影：1px 1px 焦褐 40% 透明偏移（≤ 2px blur 满足拒绝清单 #2）</item>
/// </list>
///
/// <para>无状态（容器组件，状态由内部 child 决定）</para>
/// </summary>
public partial class JrpgPanel : PanelContainer
{
    /// <summary>JRPG 面板标题条高度。</summary>
    public const int TitleBarHeight = 24;

    private readonly Label _titleLabel = new();
    private readonly VBoxContainer _contentRoot = new();
    private readonly PanelContainer _titleBar = new();
    private bool _titleBarVisible;

    /// <summary>JRPG 风格：自动加载 theme 并构建标题条 + 内容区。</summary>
    public JrpgPanel()
    {
        ApplyJrpgTheme();
        BuildLayout();
    }

    /// <summary>语义化：设置标题（空字符串隐藏标题条）。</summary>
    public JrpgPanel SetTitle(string title)
    {
        _titleLabel.Text = title;
        _titleBar.Visible = !string.IsNullOrEmpty(title);
        _titleBarVisible = _titleBar.Visible;
        return this;
    }

    /// <summary>语义化：添加子控件到内容区。</summary>
    public JrpgPanel AddContent(Control child)
    {
        _contentRoot.AddChild(child);
        return this;
    }

    private void BuildLayout()
    {
        _titleBar.CustomMinimumSize = new Vector2(0, TitleBarHeight);
        _titleBar.AddThemeStyleboxOverride("panel", JrpgStyleFactory.TitleBarStyle());
        _titleBar.Visible = false;

        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", JrpgButton.DefaultFontSize);
        _titleBar.AddChild(_titleLabel);

        _contentRoot.AddThemeConstantOverride("separation", JrpgRichBox.DefaultParagraphSpacing);
        _contentRoot.AddChild(_titleBar);

        AddChild(_contentRoot);
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
    }
}
