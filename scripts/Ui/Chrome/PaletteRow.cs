using System.Collections.Generic;
using Godot;

namespace WorldView.Ui.Chrome;

/// <summary>
/// BottomPalette 的一行（spec §3.2 / tasks.md T11）。
///
/// <para><c>full_width</c> 行：固定高度（<c>timeline_row</c> 28 / <c>tool_row</c> 20），
/// 只放一个铺满整行的 section。</para>
/// <para><c>horizontal</c> 行：吃掉剩余高度（<c>build_row</c>，<c>flex</c> 权重），
/// 下面用 <see cref="HBoxContainer"/> 横向排若干 section。</para>
/// </summary>
public partial class PaletteRow : Control
{
    /// <summary>展开态变化（28 ↔ 56），供行内 section 内容同步（TimelineStrip 的展开附加区）。</summary>
    [Signal]
    public delegate void ExpandedChangedEventHandler(bool expanded);

    /// <summary>行 ID（写进布局 YAML 的 <c>id</c>）。</summary>
    [Export]
    public string RowId { get; set; } = "row";

    /// <summary>true = <c>full_width</c>，false = <c>horizontal</c>。</summary>
    [Export]
    public bool FullWidth { get; set; }

    /// <summary>full_width 行的固定高度。</summary>
    [Export]
    public int FixedHeight { get; set; } = 28;

    /// <summary>时间段展开后的高度（仅 <c>timeline_row</c>：56）。</summary>
    [Export]
    public int ExpandedHeight { get; set; } = 56;

    /// <summary>horizontal 行的 flex 权重。</summary>
    [Export]
    public int FlexWeight { get; set; } = 1;

    /// <summary>该行是否只接受 <c>timeline</c>（spec §4.1：timeline_row 只接受 TimelineStrip）。</summary>
    [Export]
    public bool TimelineOnly { get; set; }

    private HBoxContainer? _box;
    private int _baseHeight;
    private bool _expanded;

    /// <summary>行内 section 的容器（懒建，保证在 <c>_Ready</c> 之前也能塞 section）。</summary>
    public HBoxContainer Box
    {
        get
        {
            EnsureBox();
            return _box!;
        }
    }

    /// <summary>行内 section（按 UI 顺序）。</summary>
    public IEnumerable<PaletteSection> Sections
    {
        get
        {
            EnsureBox();
            foreach (var child in _box!.GetChildren())
            {
                if (child is PaletteSection section)
                {
                    yield return section;
                }
            }
        }
    }

    /// <summary>时间段行当前是否展开（28 ↔ 56）。</summary>
    public bool Expanded => _expanded;

    /// <summary>未展开时的高度（写回 YAML 的 <c>height</c>）。</summary>
    public int BaseHeight => _baseHeight > 0 ? _baseHeight : FixedHeight;

    public override void _Ready()
    {
        EnsureBox();
        _baseHeight = FixedHeight;
        ApplyLayout();
    }

    private void EnsureBox()
    {
        if (_box is not null && IsInstanceValid(_box))
        {
            return;
        }

        _box = new HBoxContainer { Name = "Box" };
        _box.AddThemeConstantOverride("separation", 6);
        AddChild(_box);
        _box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>按 <see cref="FullWidth"/> 应用尺寸标志。</summary>
    public void ApplyLayout()
    {
        if (FullWidth)
        {
            CustomMinimumSize = new Vector2(0f, Mathf.Max(16, FixedHeight));
            SizeFlagsVertical = SizeFlags.ShrinkBegin;
            SizeFlagsStretchRatio = 0f;
        }
        else
        {
            CustomMinimumSize = new Vector2(0f, 0f);
            SizeFlagsVertical = SizeFlags.ExpandFill;
            SizeFlagsStretchRatio = Mathf.Max(1, FlexWeight);
        }
    }

    /// <summary>切换时间段展开（仅对 <see cref="FullWidth"/> 行有意义）。</summary>
    public void SetExpanded(bool expanded)
    {
        if (_baseHeight <= 0)
        {
            _baseHeight = FixedHeight;
        }

        _expanded = expanded;
        FixedHeight = expanded ? Mathf.Max(_baseHeight, ExpandedHeight) : _baseHeight;
        ApplyLayout();
        EmitSignal(SignalName.ExpandedChanged, expanded);
    }

    /// <summary>追加 section（可指定插入位置）。</summary>
    public void AddSection(PaletteSection section, int index = -1)
    {
        var box = Box;
        if (section.GetParent() is { } oldParent && oldParent != box)
        {
            oldParent.RemoveChild(section);
        }

        if (section.GetParent() is null)
        {
            box.AddChild(section);
        }

        if (index >= 0 && index < box.GetChildCount() - 1)
        {
            box.MoveChild(section, index);
        }

        section.Compact = true;
        section.ApplySizing();
    }

    /// <summary>按落点 X（全局坐标）把 section 插到相应位置（spec §4.1「按落点 X 排序插入」）。</summary>
    public void InsertSectionByPosition(PaletteSection section, float globalX)
    {
        var index = Box.GetChildCount();
        for (var i = 0; i < Box.GetChildCount(); i++)
        {
            if (Box.GetChild(i) is not Control existing)
            {
                continue;
            }

            var centerX = existing.GetGlobalRect().Position.X + existing.GetGlobalRect().Size.X * 0.5f;
            if (globalX < centerX)
            {
                index = i;
                break;
            }
        }

        AddSection(section, index);
    }

    /// <summary>该行是否接受该面板（<c>timeline_row</c> 只接受 <c>timeline</c>）。</summary>
    public bool Accepts(string panelId) => !TimelineOnly || panelId == "timeline";

    /// <summary>该行是否为空（没有 section）。</summary>
    public bool IsEmpty => Box.GetChildCount() == 0;
}
