using Godot;
using WorldView.Domain;
using WorldView.Editing;

namespace WorldView.Ui.Dock;

/// <summary>
/// 地形色块拖放 payload（spec §4.2 / tasks.md T5）。
/// 必须是 <c>public partial class ... : RefCounted</c>，理由见 <see cref="PanelDragData"/>。
/// </summary>
public partial class TerrainDragData : RefCounted
{
    /// <summary>要涂刷的地形类型。</summary>
    public TerrainType TerrainType { get; set; } = TerrainType.Plain;

    /// <summary>笔刷形状。</summary>
    public BrushType Brush { get; set; } = BrushType.Round;

    /// <summary>笔刷半径（格）。</summary>
    public int Radius { get; set; } = 1;
}
