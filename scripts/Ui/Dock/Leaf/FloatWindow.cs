using Godot;
using WorldView.Ui.Dock.Service;

namespace WorldView.Ui.Dock.Leaf;

/// <summary>
/// 浮动面板宿主窗口（spec §4.4 / tasks.md T1）。
///
/// <para><see cref="Window.Transient"/> = true（始终浮在父视口之上）、
/// <see cref="Window.Exclusive"/> = false（不抢输入焦点）、
/// <see cref="Window.Unresizable"/> = false（允许用户拉伸）。</para>
///
/// <para>Godot 默认开启 <c>display/window/subwindows/embed_subwindows</c>，
/// 因此浮动面板是**内嵌**在游戏窗口里的子窗口，不会被拖出主窗口（spec §9 N2）。</para>
/// </summary>
public partial class FloatWindow : Window
{
    /// <summary>本窗口承载的面板 ID（用于布局持久化）。</summary>
    public string PanelId { get; private set; } = string.Empty;

    public override void _Ready()
    {
        Transient = true;
        Exclusive = false;
        Unresizable = false;
        Visible = false;
        CloseRequested += OnCloseRequested;
    }

    /// <summary>把面板装进本窗口并显示。</summary>
    public void Present(DockPanel panel, Vector2 position, Vector2 size)
    {
        PanelId = panel.PanelId;
        Title = panel.PanelTitle;
        Size = new Vector2I(
            Mathf.Max(140, Mathf.RoundToInt(size.X)),
            Mathf.Max(72, Mathf.RoundToInt(size.Y)));
        Position = new Vector2I(Mathf.RoundToInt(position.X), Mathf.RoundToInt(position.Y));

        panel.Reparent(this);
        panel.Compact = false;
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 面板可能仍带着「隐藏」状态（Main.tscn 里两块面板默认 visible=false），
        // 不显式点亮会出现「窗口可见但里面空」。
        panel.Visible = true;
        Visible = true;
    }

    /// <summary>取回面板内容并隐藏窗口（供重新停靠时调用）。</summary>
    public void Detach()
    {
        Visible = false;
    }

    private void OnCloseRequested()
    {
        Visible = false;
        (DockLayout.Instance as IDockHost)?.OnPanelClosed(PanelId);
    }
}
