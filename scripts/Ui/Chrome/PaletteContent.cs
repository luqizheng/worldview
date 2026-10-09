using Godot;
using WorldView.Ui.Dock.Leaf;
using WorldView.Ui.Dock.Service;

namespace WorldView.Ui.Chrome;

/// <summary>
/// BottomPalette 内某个 section 的**内容**基类（spec §5.2 / tasks.md T11）。
///
/// <para>内容负责自己的私有状态在 YAML 里的读写（<c>cell_size</c> / <c>cell_gap</c> /
/// <c>active_category</c> / <c>pages_by_category</c> / <c>expanded</c> 等），
/// 而 section 的几何（<c>size_mode</c> / <c>flex</c> / <c>width</c> / <c>visible</c>）
/// 由 <see cref="PaletteSection"/> 负责。</para>
/// </summary>
public abstract partial class PaletteContent : Control
{
    /// <summary>承载本内容的 section。</summary>
    public PaletteSection? Section => GetParent() as PaletteSection;

    /// <summary>承载本内容的行（section → Box → row）。</summary>
    public PaletteRow? Row => Section?.GetParent()?.GetParent() as PaletteRow;

    protected static Editor.EditorContext? Context => Editor.EditorContext.Instance;

    /// <summary>从布局文档还原私有状态（启动时由 BottomPalette 调用）。</summary>
    public virtual void ApplyState(PanelState? state)
    {
    }

    /// <summary>把私有状态写回布局文档（落盘前由 BottomPalette 调用）。</summary>
    public virtual void CaptureState(PanelState state)
    {
    }

    protected void MarkLayoutChanged() => (DockLayout.Instance as IDockHost)?.MarkLayoutChanged();
}
