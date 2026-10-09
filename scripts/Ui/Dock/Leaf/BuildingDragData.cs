using Godot;
using WorldView.Domain;

namespace WorldView.Ui.Dock.Leaf;

/// <summary>
/// 建筑图标拖放 payload（spec §4.2 / tasks.md T5）。
/// 必须是 <c>public partial class ... : RefCounted</c>，理由见 <see cref="PanelDragData"/>。
/// </summary>
public partial class BuildingDragData : RefCounted
{
    /// <summary>要放置的建筑类型。</summary>
    public BuildingType BuildingType { get; set; } = BuildingType.House;

    /// <summary>发起拖放时所在的地图 id；与落点当前地图不一致时拒绝放置。</summary>
    public string MapId { get; set; } = string.Empty;
}
