using System.Collections.Generic;
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 多地图标签页（§4.9.7 契约 多地图标签页行）
///
/// <para>ProjectChrome 下方 24px，显示当前工程所有地图的 tab。
/// 选中 tab #fff8e7 + 2px 金黄下划线；未选 #f4f0e4 + #a89878 文字。
/// 标签高 22px（外加 2px 间距 = 24px 容器），文字 13px。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>容器：HBoxContainer，2px 间隔，固定高 24px</item>
///   <item>选中：bg #fff8e7，text #3a2810，底部 2px 金黄下划线 #e8c34a</item>
///   <item>未选：bg #f4f0e4，text #a89878，无下划线</item>
///   <item>字号：13px 衬线</item>
/// </list>
///
/// <para>API：</para>
/// <list type="bullet">
///   <item><see cref="SetTabs"/>：传所有 (id, label) + 当前 activeId</item>
///   <item><see cref="MapTabSelectedEventHandler"/>：点击 tab 时携带 mapId</item>
/// </list>
/// </summary>
public partial class JrpgMapTabBar : PanelContainer
{
    /// <summary>JRPG 标签页字号（与 §4.5 UI 区分：13px 用于辅助 UI）。</summary>
    public const int DefaultFontSize = 13;

    /// <summary>标签页容器固定高度（§4.9.7 契约）。</summary>
    public const int ContainerHeight = 24;

    /// <summary>标签页标签高（22px + 2px 间距 = 24px）。</summary>
    public const int TabHeight = 22;

    /// <summary>选中态下划线高度。</summary>
    public const int UnderlineHeight = 2;

    /// <summary>标签页条目。</summary>
    public sealed record Tab(string MapId, string Label);

    /// <summary>点击 tab 请求切换地图。</summary>
    [Signal] public delegate void MapTabSelectedEventHandler(string mapId);

    private readonly HBoxContainer _tabRow = new();
    private readonly List<Tab> _tabs = new();
    private string _activeId = string.Empty;

    /// <summary>JRPG 风格：自动加载 theme 并构建标签页行。</summary>
    public JrpgMapTabBar()
    {
        ApplyJrpgTheme();
        BuildLayout();
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(0, ContainerHeight);

        _tabRow.Alignment = BoxContainer.AlignmentMode.Begin;
        _tabRow.AddThemeConstantOverride("separation", 2);
        _tabRow.MouseFilter = MouseFilterEnum.Pass;
        AddChild(_tabRow);
    }

    /// <summary>语义化：设置所有 tab + 当前 active id。</summary>
    public JrpgMapTabBar SetTabs(IEnumerable<Tab> tabs, string activeId)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
        _activeId = activeId;
        Rebuild();
        return this;
    }

    private void Rebuild()
    {
        foreach (var child in _tabRow.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var tab in _tabs)
        {
            var isActive = tab.MapId == _activeId;
            _tabRow.AddChild(BuildTab(tab, isActive));
        }
    }

    private Button BuildTab(Tab tab, bool isActive)
    {
        var btn = new Button
        {
            Text = tab.Label,
            CustomMinimumSize = new Vector2(0, TabHeight),
            MouseFilter = MouseFilterEnum.Stop,
        };
        btn.AddThemeFontSizeOverride("font_size", DefaultFontSize);

        // 选中/未选样式
        var styleNormal = new StyleBoxFlat
        {
            BgColor = isActive ? JrpgStyleFactory.JrpgCard : JrpgStyleFactory.JrpgBg,
            BorderWidthBottom = isActive ? UnderlineHeight : 0,
            BorderColor = isActive ? JrpgStyleFactory.JrpgOutlineFocus : JrpgStyleFactory.JrpgOutline,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };
        btn.AddThemeStyleboxOverride("normal", styleNormal);
        btn.AddThemeStyleboxOverride("hover", styleNormal);
        btn.AddThemeStyleboxOverride("pressed", styleNormal);
        btn.AddThemeStyleboxOverride("focus", styleNormal);

        btn.AddThemeColorOverride("font_color", isActive ? JrpgStyleFactory.JrpgText : JrpgStyleFactory.JrpgTextPlaceholder);
        btn.AddThemeColorOverride("font_hover_color", isActive ? JrpgStyleFactory.JrpgText : JrpgStyleFactory.JrpgTextPlaceholder);
        btn.AddThemeColorOverride("font_pressed_color", isActive ? JrpgStyleFactory.JrpgText : JrpgStyleFactory.JrpgTextPlaceholder);

        btn.Flat = true;
        btn.Pressed += () => EmitSignal(SignalName.MapTabSelected, tab.MapId);
        return btn;
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
