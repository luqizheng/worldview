using System.Collections.Generic;
using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 面包屑（§4.9.7 契约 面包屑行）
///
/// <para>在顶栏 ProjectChrome 中显示「工程名 / 地图1 / 地图2」点击切换。
/// 文字 14px 衬线，分隔符 <c>/</c> 用 <c>--jrpg-text-placeholder</c>。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>容器：HBoxContainer，居中显示，水平 4px 间隔</item>
///   <item>段：14px 衬线，current 用 <c>--jrpg-text</c>，可点用 <c>--jrpg-link</c> + 1px 下划线</item>
///   <item>分隔符：14px 衬线，<c>--jrpg-text-placeholder</c> 灰褐，4px 左右 padding</item>
///   <item>高度 32px 与顶栏 TopStrip 一致</item>
/// </list>
///
/// <para>信号：</para>
/// <list type="bullet">
///   <item><see cref="MapJumpRequestedEventHandler"/>：点击某段时携带该段的 map id（0 段=工程无 id 跳；末段=当前地图不跳）</item>
/// </list>
/// </summary>
public partial class JrpgBreadcrumb : HBoxContainer
{
    /// <summary>JRPG 面包屑字号（与 §4.5 一致）。</summary>
    public const int DefaultFontSize = 14;

    /// <summary>面包屑条目。</summary>
    public sealed record Crumb(string Label, string? MapId);

    /// <summary>点击某段请求跳转到对应 map id（null 表示跳到「根/未加载」）。</summary>
    [Signal] public delegate void MapJumpRequestedEventHandler(string mapId);

    private readonly List<Crumb> _crumbs = new();

    /// <summary>JRPG 风格：自动加载 theme 并设置默认样式。</summary>
    public JrpgBreadcrumb()
    {
        ApplyJrpgTheme();
        BuildLayout();
    }

    /// <summary>语义化：设置面包屑条目（顺序：根 → 当前）。</summary>
    public JrpgBreadcrumb SetCrumbs(IEnumerable<Crumb> crumbs)
    {
        _crumbs.Clear();
        _crumbs.AddRange(crumbs);
        Rebuild();
        return this;
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(0, JrpgButton.DefaultHeight);
        Alignment = BoxContainer.AlignmentMode.Center;
        AddThemeConstantOverride("separation", 4);
    }

    private void Rebuild()
    {
        // 清掉旧子节点
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var lastIndex = _crumbs.Count - 1;

        for (var i = 0; i < _crumbs.Count; i++)
        {
            var crumb = _crumbs[i];
            var isLast = i == lastIndex;
            AddChild(BuildSegment(crumb, isLast));

            if (!isLast)
            {
                AddChild(BuildSeparator());
            }
        }
    }

    private Label BuildSegment(Crumb crumb, bool isLast)
    {
        var label = new Label
        {
            Text = crumb.Label,
            MouseFilter = isLast ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop,
        };
        label.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        label.VerticalAlignment = VerticalAlignment.Center;

        if (isLast)
        {
            label.AddThemeColorOverride("font_color", JrpgStyleFactory.JrpgText);
        }
        else
        {
            label.AddThemeColorOverride("font_color", JrpgStyleFactory.JrpgLink);
            // 1px 下划线提示可点
            label.AddThemeConstantOverride("underline", 1);
            label.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    if (!string.IsNullOrEmpty(crumb.MapId))
                    {
                        EmitSignal(SignalName.MapJumpRequested, crumb.MapId);
                    }
                    AcceptEvent();
                }
            };
        }

        return label;
    }

    private Label BuildSeparator()
    {
        var sep = new Label
        {
            Text = " / ",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        sep.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        sep.AddThemeColorOverride("font_color", JrpgStyleFactory.JrpgTextPlaceholder);
        sep.VerticalAlignment = VerticalAlignment.Center;
        return sep;
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
