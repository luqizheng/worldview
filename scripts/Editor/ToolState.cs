namespace WorldView.Editor;

/// <summary>
/// 当前激活的编辑工具（见 tasks.md T7 ToolPanel / spec.md §3.2）。
/// MVP 仅 3 个工具，不做工具插件化（N13 范围外）。
/// </summary>
public enum ToolState
{
    /// <summary>选择工具：点选单元格 / 对象，仅更新 StatusBar 与 InspectorPanel。</summary>
    Selected,

    /// <summary>地形笔刷：圆形 / 方形，中心格 + 半径。</summary>
    Brush,

    /// <summary>建筑放置：把 <see cref="EditorContext.PlaceBuildingType"/> 放到已选单元格。</summary>
    Building,
}
