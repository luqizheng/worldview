using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 建筑格（spec §4.2 / tasks.md T5）。
///
/// <para>拖出时返回 <see cref="BuildingDragData"/>（<c>: RefCounted</c>，否则
/// <c>Variant.From</c> 会失败或被静默置 null），并用 <c>SetDragPreview</c> 设置浮动预览。
/// 预览参数是 <c>Control</c> 而非 <c>Texture2D</c>。</para>
///
/// <para>落点由接收方 <c>WorldCanvas</c> 负责换算并走 <c>UndoStack</c>，
/// 本控件不写任何地图数据。</para>
/// </summary>
public partial class BuildingButton : Button
{
    private const string PreviewPath = "res://scenes/ui/preview/building_preview.png";

    /// <summary>该格对应的建筑类型。</summary>
    public BuildingType BuildingType { get; set; } = BuildingType.House;

    /// <inheritdoc/>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        SetDragPreview(BuildPreview());
        return Variant.From(new BuildingDragData
        {
            BuildingType = BuildingType,
            MapId = EditorContext.Instance?.ActiveMapId ?? string.Empty,
        });
    }

    private static Control BuildPreview()
    {
        var preview = new PanelContainer
        {
            Modulate = new Color(1f, 1f, 1f, 0.9f),
            CustomMinimumSize = new Vector2(52f, 52f),
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.45f, 0.55f, 0.85f, 0.28f),
            BorderColor = new Color(0.45f, 0.55f, 0.85f, 1f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        preview.AddThemeStyleboxOverride("panel", style);

        if (ResourceLoader.Exists(PreviewPath))
        {
            preview.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>(PreviewPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }

        return preview;
    }
}
