using Godot;
using WorldView.Ui.Dock;

namespace WorldView.Ui.Palette;

/// <summary>
/// BottomPalette 的行内 section（spec §3.2 / §5.3 / tasks.md T11）。
///
/// <para>继承 <see cref="DockPanel"/> 以复用「16px drag handle + 拖放 + 关闭」逻辑，
/// 并补上 <c>size_mode</c> 三态与「跟随行高成方形」（minimap 的 <c>width: auto</c>）。</para>
/// </summary>
public partial class PaletteSection : DockPanel
{
    /// <summary>spec §5.3 的 <c>size_mode</c>。</summary>
    public enum SectionSizeMode
    {
        /// <summary>固定宽度（<see cref="FixedWidth"/>，或跟行高成方形）。</summary>
        Fixed,

        /// <summary>按 <see cref="FlexWeight"/> 瓜分剩余宽度。</summary>
        Flex,

        /// <summary>按内容宽度。</summary>
        Auto,
    }

    /// <summary>宽度算法（spec §5.3）。</summary>
    [Export]
    public SectionSizeMode Mode { get; set; } = SectionSizeMode.Flex;

    /// <summary>flex 权重。</summary>
    [Export]
    public int FlexWeight { get; set; } = 1;

    /// <summary>fixed 模式下的固定宽度（px）。</summary>
    [Export]
    public int FixedWidth { get; set; }

    /// <summary>宽度跟随行高成方形（minimap 的 <c>width: auto</c>）。</summary>
    [Export]
    public bool SquareAspect { get; set; }

    /// <summary>方形模式下扣掉的上下留白（spec §3.4：行高 152 − 48 = 104）。</summary>
    [Export]
    public int SquarePadding { get; set; } = 48;

    /// <summary>宽度死区：小于该差值不调整，避免重排震荡。</summary>
    private const float WidthTolerance = 1.5f;

    public override void _Ready()
    {
        base._Ready();
        Resized += OnSectionResized;
        ApplySizing();
    }

    /// <summary>应用 <see cref="Mode"/> 决定的大小标志与最小宽度。</summary>
    public void ApplySizing()
    {
        switch (Mode)
        {
            case SectionSizeMode.Flex:
                SizeFlagsHorizontal = SizeFlags.ExpandFill;
                SizeFlagsStretchRatio = Mathf.Max(1, FlexWeight);
                SetMinWidth(0f);
                break;

            case SectionSizeMode.Auto:
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                SetMinWidth(0f);
                break;

            default:
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                UpdateFixedWidth();
                break;
        }
    }

    private void OnSectionResized()
    {
        if (Mode == SectionSizeMode.Fixed)
        {
            UpdateFixedWidth();
        }
    }

    private void UpdateFixedWidth()
    {
        var target = SquareAspect
            ? Mathf.Max(48f, RowHeight() - SquarePadding)
            : Mathf.Max(0f, FixedWidth);
        SetMinWidth(target);
    }

    private void SetMinWidth(float width)
    {
        if (Mathf.Abs(CustomMinimumSize.X - width) < WidthTolerance)
        {
            return;
        }

        CustomMinimumSize = new Vector2(width, CustomMinimumSize.Y);
    }

    private float RowHeight()
    {
        if (Size.Y > 1f)
        {
            return Size.Y;
        }

        if (GetParent() is Control box && box.Size.Y > 1f)
        {
            return box.Size.Y;
        }

        return Mathf.Max(48f, FixedWidth + SquarePadding);
    }
}
