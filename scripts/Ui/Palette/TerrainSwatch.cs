using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Editing;
using WorldView.Render;
using WorldView.Ui.Dock;

namespace WorldView.Ui.Palette;

/// <summary>
/// 地形色块（spec §3.1 row2 的 <c>Terrain</c> 控件 + §4.2 内容拖放 / tasks.md T5）：
/// 左键单击选为当前笔刷地形，按住拖出时携带 <see cref="TerrainDragData"/> 直接落到画布上。
///
/// <para>颜色来自 <see cref="TerrainPalette"/>（真源是 <c>Domain.TerrainDefaults.ColorHex</c>），
/// 因此小地图与工具条不会各维护一份配色。</para>
/// </summary>
public partial class TerrainSwatch : Button
{
    /// <summary>地形中文名（UI 层自备：Domain 只提供颜色，不提供显示名）。</summary>
    private static readonly System.Collections.Generic.Dictionary<TerrainType, string> Names = new()
    {
        [TerrainType.Plain] = "平原",
        [TerrainType.Mountain] = "山地",
        [TerrainType.Forest] = "森林",
        [TerrainType.Hill] = "丘陵",
        [TerrainType.Water] = "水域",
        [TerrainType.Desert] = "沙漠",
        [TerrainType.Swamp] = "沼泽",
        [TerrainType.Tundra] = "苔原",
        [TerrainType.Basin] = "盆地",
        [TerrainType.Coast] = "海岸",
        [TerrainType.River] = "河流",
    };

    private bool _selected;

    /// <summary>该色块对应的地形类型。</summary>
    public TerrainType TerrainType { get; set; } = TerrainType.Plain;

    /// <summary>地形显示名（工具条与小地图共用，避免各处再抄一份）。</summary>
    public static string NameOf(TerrainType type) =>
        Names.TryGetValue(type, out var name) ? name : type.ToString();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(14f, 14f);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        TooltipText = $"{NameOf(TerrainType)}（拖动到画布绘制）";
        ApplyStyles();
    }

    /// <summary>选中态（由容器统一维护，同一时刻只有一个色块被选中）。</summary>
    public void SetSelected(bool selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        ApplyStyles();
    }

    /// <inheritdoc/>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var context = EditorContext.Instance;
        SetDragPreview(BuildPreview());
        return Variant.From(new TerrainDragData
        {
            TerrainType = TerrainType,
            Brush = context?.Brush ?? BrushType.Round,
            Radius = context?.BrushRadius ?? 1,
        });
    }

    // ------------------------------------------------------------------ 外观

    private void ApplyStyles()
    {
        var fill = TerrainPalette.ColorOf(TerrainType);
        var border = _selected
            ? new Color(1f, 1f, 1f, 0.95f)
            : new Color(0.30f, 0.30f, 0.36f, 1f);
        var width = _selected ? 2 : 1;

        AddThemeStyleboxOverride("normal", MakeStyle(fill, width, border));
        AddThemeStyleboxOverride("hover", MakeStyle(fill.Lightened(0.15f), width, new Color(0.45f, 0.55f, 0.85f, 1f)));
        AddThemeStyleboxOverride("pressed", MakeStyle(fill.Darkened(0.2f), width, new Color(0.45f, 0.55f, 0.85f, 1f)));
        AddThemeStyleboxOverride("focus", MakeStyle(fill, width, new Color(0.45f, 0.55f, 0.85f, 1f)));
        AddThemeStyleboxOverride("disabled", MakeStyle(fill, width, border));
    }

    private static StyleBoxFlat MakeStyle(Color fill, int borderWidth, Color border)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = border };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(3);
        return style;
    }

    private Control BuildPreview()
    {
        var preview = new PanelContainer
        {
            Modulate = new Color(1f, 1f, 1f, 0.9f),
            CustomMinimumSize = new Vector2(40f, 40f),
        };

        var style = new StyleBoxFlat
        {
            BgColor = TerrainPalette.ColorOf(TerrainType),
            BorderColor = new Color(0.45f, 0.55f, 0.85f, 1f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        preview.AddThemeStyleboxOverride("panel", style);
        return preview;
    }
}
