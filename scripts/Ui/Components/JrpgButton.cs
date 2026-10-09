using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用按钮（§4.5.2 契约 Button 行）
///
/// <para>内部封装 Godot <see cref="Button"/>，自动应用 <c>parchment_theme.tres</c>，
/// 暴露语义化 API（SetText / SetEnabled / OnClick）替代直接调用 Godot API。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>矩形，宽 = 内容 + 16px，高 32px，圆角 2px，2px 焦褐描边（焦点态替换为金黄）</item>
///   <item>填色：normal #f4e4c1 / hover #f8ecd0 / pressed #e8c89a / disabled 50% 透明</item>
///   <item>字号 14-16px 衬线居中</item>
/// </list>
///
/// <para>5 状态机：normal / hover / pressed / disabled / focus（由 Godot Button 内置 + theme styles 自动驱动）</para>
/// </summary>
/// <remarks>
/// 拒绝清单（§4.5.5）：#11 圆角>4px、#12 焦点态叠加外发光。
/// </remarks>
public partial class JrpgButton : Button
{
    /// <summary>JRPG 按钮推荐高度（与 §4.5.2 契约一致）。</summary>
    public const int DefaultHeight = 32;

    /// <summary>JRPG 按钮推荐字号。</summary>
    public const int DefaultFontSize = 14;

    /// <summary>JRPG 按钮主题路径。</summary>
    public const string ThemePath = "res://assets/themes/parchment_theme.tres";

    /// <summary>点击事件（C# 友好包装，避免使用者直接连 Godot 信号）。</summary>
    [Signal] public delegate void ClickedEventHandler();

    /// <summary>JRPG 风格：自动加载 theme 并设置默认样式。</summary>
    public JrpgButton()
    {
        ApplyJrpgTheme();
    }

    /// <summary>语义化：设置按钮文本（自动加 16px 水平内边距留给描边）。</summary>
    public JrpgButton SetLabel(string text)
    {
        Text = text;
        return this;
    }

    /// <summary>语义化：启用 / 禁用。</summary>
    public JrpgButton SetEnabled(bool enabled)
    {
        Disabled = !enabled;
        return this;
    }

    /// <summary>语义化：绑定点击回调。</summary>
    public JrpgButton BindClick(System.Action handler)
    {
        Pressed += () => handler();
        EmitSignal(SignalName.Clicked);
        return this;
    }

    private void ApplyJrpgTheme()
    {
        // 优先用 Theme 单例，避免每按钮都加载
        Theme = JrpgThemeCache.GetTheme();
        CustomMinimumSize = new Vector2(0, DefaultHeight);
        AddThemeFontSizeOverride("font_size", DefaultFontSize);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // 主题已在构造时设置；_Ready 内只补一遍避免热重载
        if (Theme == null) ApplyJrpgTheme();
    }
}
