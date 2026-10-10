using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Dock.Leaf;
using WorldView.Ui.Dock.Service;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 底部面板容器（spec §3.2 / §3.5 / tasks.md T8 + T11 + T12）。
///
/// <para>三层结构：<c>BottomPalette → PaletteRow（纵向） → PaletteSection（横向）</c>。
/// <c>timeline_row</c> 必须是本容器的 row 0，<b>不能</b>做成独立顶层节点，
/// 否则折叠时播放条会与容器错位（tasks.md T11）。</para>
///
/// <para>展开 200px ↔ 折叠 40px（spec §2：折叠后画布占比 93.3%）。
/// 折叠态（spec §4 C3 = K1）不是整体收起，而是只收起 <c>timeline_row</c> / <c>build_row</c>，
/// 由常驻的 <c>tool_row</c> 显示 <see cref="ToolStrip"/> 紧凑变体（工具切换图标 + 坐标）。</para>
/// </summary>
public partial class BottomPalette : Control, IPaletteHost
{
    /// <summary>展开态高度（spec §2）。</summary>
    public const int ExpandedHeight = 200;

    /// <summary>折叠态高度（只露一行图标）。</summary>
    public const int CollapsedHeight = 40;

    /// <summary>三个默认行的 ID（与布局 YAML 一一对应）。</summary>
    private const string TimelineRowId = "timeline_row";
    private const string BuildRowId = "build_row";
    private const string ToolRowId = "tool_row";

    /// <summary>工具方块区 section 的 ID（spec §4 row1：小地图 + 工具方块区）。</summary>
    public const string ToolsSectionId = "tools";

    /// <summary>工具行 section 的 ID（折叠态由它常驻）。</summary>
    private const string ToolStripSectionId = "tool_strip";

    private Panel? _background;
    private VBoxContainer? _rows;
    private JrpgButton? _collapseButton;
    private bool _defaultsBuilt;
    private bool _collapsed;
    private int _expandedHeight = ExpandedHeight;

    /// <summary>是否处于折叠态（40px）。</summary>
    public bool Collapsed => _collapsed;

    /// <summary>行容器（默认三行的父节点）。</summary>
    public VBoxContainer? Rows => _rows;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        CustomMinimumSize = new Vector2(0f, ExpandedHeight);

        BuildChrome();
        EnsureDefaultRows();
    }

    // ------------------------------------------------------------------ chrome

    private void BuildChrome()
    {
        if (_background is not null)
        {
            return;
        }

        _background = new Panel { Name = "Background", MouseFilter = MouseFilterEnum.Stop };
        AddChild(_background);
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 行高严格按 spec §3.2：28 + 152 + 20 = 200，所以纵向不留 padding、行间 separation = 0；
        // 右侧留 28px 给折叠按钮（否则会压住 timeline_row 右端的时间刻度占位）。
        _rows = new VBoxContainer { Name = "Rows" };
        _rows.AddThemeConstantOverride("separation", 0);
        AddChild(_rows);
        _rows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rows.OffsetLeft = 0f;
        _rows.OffsetTop = 0f;
        _rows.OffsetRight = -28f;
        _rows.OffsetBottom = 0f;

        _collapseButton = new JrpgButton
        {
            Name = "Collapse",
            Text = "▾",
            Flat = true,
            TooltipText = "折叠面板 (Ctrl+B)",
        };
        AddChild(_collapseButton);
        _collapseButton.SetAnchorsPreset(LayoutPreset.TopRight);
        _collapseButton.OffsetLeft = -26f;
        _collapseButton.OffsetRight = -4f;
        _collapseButton.OffsetTop = 2f;
        _collapseButton.OffsetBottom = 22f;
        _collapseButton.Pressed += () => SetCollapsed(!_collapsed);
    }

    // ------------------------------------------------------------------ 默认三行

    private void EnsureDefaultRows()
    {
        if (_defaultsBuilt)
        {
            return;
        }

        _defaultsBuilt = true;
        if (_rows is null)
        {
            BuildChrome();
        }

        if (_rows!.GetChildCount() > 0)
        {
            return;
        }

        var timelineRow = CreateRow(TimelineRowId, fullWidth: true, height: 28, flex: 1, order: 0, timelineOnly: true);
        var buildRow = CreateRow(BuildRowId, fullWidth: false, height: 0, flex: 1, order: 1);
        var toolRow = CreateRow(ToolRowId, fullWidth: true, height: 20, flex: 1, order: 2);

        timelineRow.AddSection(CreateSection("timeline", "时间轴", PaletteSection.SectionSizeMode.Flex, 1, false, "TimelineStrip"));
        buildRow.AddSection(CreateSection("minimap", "小地图", PaletteSection.SectionSizeMode.Fixed, 1, true, "MinimapSection"));
        buildRow.AddSection(CreateSection(ToolsSectionId, "工具", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolPaletteSection"));
        toolRow.AddSection(CreateSection(ToolStripSectionId, "工具栏", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolStrip"));
    }

    private PaletteRow CreateRow(string rowId, bool fullWidth, int height, int flex, int order, bool timelineOnly = false)
    {
        var row = new PaletteRow
        {
            Name = "Row_" + rowId,
            RowId = rowId,
            FullWidth = fullWidth,
            FixedHeight = height,
            FlexWeight = flex,
            TimelineOnly = timelineOnly,
        };

        _rows!.AddChild(row);
        _rows.MoveChild(row, Mathf.Clamp(order, 0, _rows.GetChildCount() - 1));
        return row;
    }

    private PaletteSection CreateSection(
        string sectionId,
        string title,
        PaletteSection.SectionSizeMode mode,
        int flex,
        bool square,
        string contentScene)
    {
        var section = new PaletteSection
        {
            Name = "Section_" + sectionId,
            PanelId = sectionId,
            PanelTitle = title,
            Mode = mode,
            FlexWeight = flex,
            SquareAspect = square,
            Compact = true,
            Closable = true,
        };

        if (LoadContent(contentScene) is { } content)
        {
            section.SetContent(content);
        }

        return section;
    }

    /// <summary>按名字加载 <c>res://scenes/ui/&lt;name&gt;.tscn</c>（缺失只警告，不崩）。</summary>
    private static Control? LoadContent(string sceneName)
    {
        var path = $"res://scenes/ui/{sceneName}.tscn";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"缺少 UI 场景，section 内容为空：{path}");
            return null;
        }

        return GD.Load<PackedScene>(path)?.InstantiateOrNull<Control>();
    }

    // ------------------------------------------------------------------ 查询

    /// <summary>按行 ID 取行。</summary>
    public PaletteRow? RowOf(string rowId)
        => _rows?.GetChildren().OfType<PaletteRow>().FirstOrDefault(row => row.RowId == rowId);

    /// <summary>按 section ID 取 section。</summary>
    public PaletteSection? SectionOf(string sectionId)
    {
        if (_rows is null)
        {
            return null;
        }

        foreach (var child in _rows.GetChildren())
        {
            if (child is not PaletteRow row)
            {
                continue;
            }

            foreach (var section in row.Sections)
            {
                if (section.PanelId == sectionId)
                {
                    return section;
                }
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ IPaletteHost

    /// <summary>
    /// 面板落到 palette：落在某行的 section 空缺位 → 成为该行新 section（按落点 X 排序）；
    /// 落在行间隙 → 成为新行插在该行之前/之后（spec §4.1）。
    /// </summary>
    public void AcceptPanel(DockPanel panel, Vector2 atPosition)
    {
        if (panel is null || !IsInstanceValid(panel) || _rows is null)
        {
            return;
        }

        var local = _rows.GetGlobalTransformWithCanvas().AffineInverse() * atPosition;
        var targetRow = RowAt(local);
        if (targetRow is not null && !targetRow.Accepts(panel.PanelId))
        {
            targetRow = null;
        }

        var section = AsSection(panel);
        if (targetRow is not null)
        {
            targetRow.InsertSectionByPosition(section, atPosition.X);
        }
        else
        {
            InsertNewRow(section, local);
        }

        MarkLayoutChanged();
    }

    /// <summary>
    /// 切换折叠态（展开 200px ↔ 折叠 40px）。
    ///
    /// <para>K1（spec §4）：折叠态 = <see cref="ToolStrip"/> 紧凑常驻变体。
    /// 折叠时<b>不</b>整体收起 <c>Rows</c>，而是只收起 <c>timeline_row</c> 与 <c>build_row</c>，
    /// 保留 <c>tool_row</c> 常驻并让 <see cref="ToolStrip"/> 切到紧凑变体（工具切换图标 + 坐标）；
    /// 这样折叠后仍有工具入口，避免「属性面板默认隐藏 + 底部折叠 = 零入口」。</para>
    /// </summary>
    public void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;

        if (RowOf(TimelineRowId) is { } timelineRow)
        {
            timelineRow.Visible = !collapsed;
        }

        if (RowOf(BuildRowId) is { } buildRow)
        {
            buildRow.Visible = !collapsed;
        }

        if (RowOf(ToolRowId) is { } toolRow)
        {
            toolRow.Visible = true;
        }

        if (SectionOf(ToolStripSectionId)?.Content is ToolStrip toolStrip)
        {
            toolStrip.SetCollapsedMode(collapsed);
        }

        if (_rows is not null)
        {
            // 折叠态 Rows 高 40px 而 tool_row 只占 20px：上下各留 10px 让它纵向居中。
            _rows.OffsetTop = collapsed ? 10f : 0f;
            _rows.OffsetBottom = collapsed ? -10f : 0f;
        }

        if (_collapseButton is not null)
        {
            _collapseButton.Text = collapsed ? "▴" : "▾";
            _collapseButton.TooltipText = collapsed ? "展开面板 (Ctrl+B)" : "折叠面板 (Ctrl+B)";
        }

        CustomMinimumSize = new Vector2(CustomMinimumSize.X, collapsed ? CollapsedHeight : _expandedHeight);
        MarkLayoutChanged();
    }

    /// <summary>按布局文档还原行 / 节。</summary>
    public void ApplyLayout(PanelState? state)
    {
        if (state is null)
        {
            return;
        }

        EnsureDefaultRows();

        if (state.Height is { } height and > 0)
        {
            _expandedHeight = height;
        }

        if (state.Rows is { Count: > 0 } rows)
        {
            ApplyRowStates(rows);
        }

        SetCollapsed(state.Collapsed ?? false);
    }

    /// <summary>把当前行 / 节状态写回布局文档。</summary>
    public void CaptureLayout(PanelState state)
    {
        state.Kind = "dock_container";
        state.Edge = "bottom";
        state.Visible = Visible;
        state.Collapsed = _collapsed;
        state.CollapsedHeight = CollapsedHeight;
        state.Height = _collapsed ? _expandedHeight : Mathf.Max(CollapsedHeight, Mathf.RoundToInt(Size.Y));

        if (_rows is null)
        {
            return;
        }

        var existing = state.Rows ?? new List<PanelState>();
        var ordered = new List<PanelState>();
        var rowIndex = 0;

        foreach (var child in _rows.GetChildren())
        {
            if (child is not PaletteRow row)
            {
                continue;
            }

            var rowState = FindState(existing, row.RowId) ?? new PanelState { Id = row.RowId };
            rowState.Id = row.RowId;
            rowState.Order = rowIndex++;
            rowState.Layout = row.FullWidth ? "full_width" : "horizontal";
            rowState.Flex = row.FullWidth ? null : row.FlexWeight;
            // 折叠态下 timeline_row / build_row 被 SetCollapsed 临时隐藏，这不是用户意图，
            // 绝不能落盘（同 2026-10-10 F11 全屏事故）。折叠时一律记 true。
            rowState.Visible = _collapsed ? true : row.Visible;
            if (row.FullWidth)
            {
                rowState.Height = row.BaseHeight;
                rowState.ExpandedHeight = row.ExpandedHeight;
                rowState.Expanded = row.Expanded;
            }
            else
            {
                rowState.Height = null;
                rowState.ExpandedHeight = null;
                rowState.Expanded = null;
            }

            if (row.RowId == TimelineRowId)
            {
                rowState.ModeHeight = rowState.ModeHeight ?? 120;
            }

            var sections = new List<PanelState>();
            var sectionIndex = 0;
            foreach (var section in row.Sections)
            {
                var sectionState = FindStateDeep(existing, section.PanelId) ?? new PanelState { Id = section.PanelId };
                sectionState.Id = section.PanelId;
                sectionState.Order = sectionIndex++;
                sectionState.SizeMode = section.Mode switch
                {
                    PaletteSection.SectionSizeMode.Fixed => "fixed",
                    PaletteSection.SectionSizeMode.Auto => "auto",
                    _ => "flex",
                };
                sectionState.Flex = section.Mode == PaletteSection.SectionSizeMode.Flex ? section.FlexWeight : null;
                sectionState.Visible = section.Visible;
                sectionState.Width = section.Mode == PaletteSection.SectionSizeMode.Fixed
                    ? section.SquareAspect ? "auto" : section.FixedWidth
                    : null;
                sectionState.Rows = null;

                if (section.Content is PaletteContent content)
                {
                    content.CaptureState(sectionState);
                }

                sections.Add(sectionState);
            }

            rowState.Sections = sections;
            rowState.Rows = null;
            ordered.Add(rowState);
        }

        state.Rows = ordered;
    }

    // ------------------------------------------------------------------ 内部

    /// <summary>本容器不是 <see cref="PaletteContent"/>，转发到 DockLayout 标脏。</summary>
    private static void MarkLayoutChanged()
    {
        (DockLayout.Instance as IDockHost)?.MarkLayoutChanged();
    }

    private void ApplyRowStates(List<PanelState> states)
    {
        var ordered = new List<PanelState>(states);
        ordered.Sort((a, b) => (a.Order ?? 0).CompareTo(b.Order ?? 0));

        var index = 0;
        foreach (var rowState in ordered)
        {
            var row = RowOf(rowState.Id);
            if (row is null || _rows is null)
            {
                continue;
            }

            if (rowState.Layout == "full_width" && rowState.Height is { } height)
            {
                row.FixedHeight = height;
                row.ApplyLayout();
            }
            else if (rowState.Flex is { } flex)
            {
                row.FlexWeight = flex;
                row.ApplyLayout();
            }

            if (rowState.ExpandedHeight is { } expandedHeight)
            {
                row.ExpandedHeight = expandedHeight;
            }

            if (rowState.Expanded == true)
            {
                row.SetExpanded(true);
            }

            row.Visible = rowState.Visible ?? true;
            _rows.MoveChild(row, Mathf.Clamp(index, 0, _rows.GetChildCount() - 1));
            index++;

            if (rowState.Sections is { Count: > 0 } sections)
            {
                ApplySectionStates(row, sections);
            }
        }
    }

    private void ApplySectionStates(PaletteRow row, List<PanelState> states)
    {
        var ordered = new List<PanelState>(states);
        ordered.Sort((a, b) => (a.Order ?? 0).CompareTo(b.Order ?? 0));

        var index = 0;
        foreach (var sectionState in ordered)
        {
            var section = SectionOf(sectionState.Id);
            if (section is null)
            {
                continue;
            }

            section.Mode = sectionState.SizeMode switch
            {
                "fixed" => PaletteSection.SectionSizeMode.Fixed,
                "auto" => PaletteSection.SectionSizeMode.Auto,
                _ => PaletteSection.SectionSizeMode.Flex,
            };
            if (sectionState.Flex is { } flex)
            {
                section.FlexWeight = flex;
            }

            if (sectionState.WidthPx is { } width)
            {
                section.FixedWidth = width;
            }

            if (sectionState.AutoWidth)
            {
                section.SquareAspect = true;
            }

            section.Visible = sectionState.Visible ?? true;
            section.ApplySizing();
            row.AddSection(section, index++);

            if (section.Content is PaletteContent content)
            {
                content.ApplyState(sectionState);
            }
        }
    }

    private PaletteRow? RowAt(Vector2 local)
    {
        if (_rows is null)
        {
            return null;
        }

        foreach (var child in _rows.GetChildren())
        {
            if (child is not PaletteRow row)
            {
                continue;
            }

            if (new Rect2(row.Position, row.Size).HasPoint(local))
            {
                return row;
            }
        }

        return null;
    }

    private PaletteSection AsSection(DockPanel panel)
    {
        if (panel is PaletteSection existing)
        {
            return existing;
        }

        var section = new PaletteSection
        {
            Name = panel.Name,
            PanelId = panel.PanelId,
            PanelTitle = string.IsNullOrEmpty(panel.PanelTitle) ? panel.PanelId : panel.PanelTitle,
            Compact = true,
            Closable = true,
        };

        // 用新 section 承载原面板的内容，避免嵌套两层 handle。
        if (panel.TakeContent() is { } content)
        {
            section.SetContent(content);
        }

        panel.GetParent()?.RemoveChild(panel);
        panel.QueueFree();
        return section;
    }

    private void InsertNewRow(PaletteSection section, Vector2 local)
    {
        if (_rows is null)
        {
            return;
        }

        var row = new PaletteRow
        {
            Name = "Row_" + section.PanelId,
            RowId = section.PanelId + "_row",
            FullWidth = false,
            FlexWeight = 1,
        };

        var index = _rows.GetChildCount();
        for (var i = 0; i < _rows.GetChildCount(); i++)
        {
            if (_rows.GetChild(i) is not PaletteRow existing)
            {
                continue;
            }

            if (local.Y < existing.Position.Y + existing.Size.Y * 0.5f)
            {
                index = i;
                break;
            }
        }

        _rows.AddChild(row);
        _rows.MoveChild(row, Mathf.Clamp(index, 0, _rows.GetChildCount() - 1));
        row.AddSection(section);
    }

    private static PanelState? FindState(List<PanelState> list, string id)
    {
        foreach (var state in list)
        {
            if (state.Id == id)
            {
                return state;
            }
        }

        return null;
    }

    private static PanelState? FindStateDeep(List<PanelState> list, string id)
    {
        foreach (var state in list)
        {
            if (state.Id == id)
            {
                return state;
            }

            if (state.Rows is { } rows && FindStateDeep(rows, id) is { } inRows)
            {
                return inRows;
            }

            if (state.Sections is { } sections && FindStateDeep(sections, id) is { } inSections)
            {
                return inSections;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ 快捷键（spec T8 / §3.4）

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.B && key.CtrlPressed)
        {
            SetCollapsed(!_collapsed);
            GetViewport().SetInputAsHandled();
            return;
        }

        // M = 显示 / 隐藏小地图（放在容器上：section 隐藏后收不到按键）。
        if (key.Keycode == Key.M && !key.CtrlPressed && !key.ShiftPressed && !key.AltPressed)
        {
            if (SectionOf("minimap") is { } minimap)
            {
                minimap.Visible = !minimap.Visible;
                MarkLayoutChanged();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
