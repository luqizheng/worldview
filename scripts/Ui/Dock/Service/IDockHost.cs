using Godot;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Dock.Service;

/// <summary>
/// Leaf 节点 (DockPanel / DockZone / FloatWindow) 拿这个接口就能跟布局根对话,
/// 不需要知道 <see cref="DockLayout"/> 的具体类型, 也不需要直接 using 整个 Service
/// namespace。替代 <c>DockLayout.Instance?.X(...)</c> 的反环访问 (C 轮 Task 2)。
/// </summary>
public interface IDockHost
{
    /// <summary>把 <paramref name="panelId"/> 落到 <paramref name="atPosition"/> 所在 dock。</summary>
    void HandlePanelDrop(string edge, string panelId, Vector2 atPosition);

    /// <summary>面板拖放开始：亮起全部停靠区（Leaf 触发）。</summary>
    void NotifyPanelDragBegin(DockPanel panel);

    /// <summary>面板拖放结束（含取消）。</summary>
    void NotifyPanelDragEnd();

    /// <summary>面板关闭时通知布局根回收（FloatWindow 调用）。</summary>
    void OnPanelClosed(string panelId);

    /// <summary>布局落盘防抖触发（任何 Leaf 节点改布局后调用）。</summary>
    void MarkLayoutChanged();

    /// <summary>切换全屏地图模式（Editor 顶层按 F11 触发）。</summary>
    void ToggleFullscreen();

    /// <summary>面板当前所在的停靠区：<c>left</c>/<c>right</c>/<c>top</c>/<c>bottom</c>/<c>center</c>。</summary>
    string ZoneOf(Node? panel);
}
