using System;
using System.Collections.Generic;
using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 工具方块区（spec §4 row1 的 <c>tools</c> section / §9 增量 1、5）。
///
/// <para>行内与「小地图」并排：小地图（fixed / width=auto 方形，默认 104×104）+ 本区（flex）。
/// 内部分为「地形 / 建筑」两个分组（K5：单一归属地 + 内部分组，同一时刻只显示一个分组），
/// 分组按钮在区头，内容区复用既有 <see cref="AdaptiveGrid"/> 与「分类 + 分页」范式。</para>
///
/// <para>地形分组由 <see cref="TerrainType"/> 的 11 个值生成 11 个 <see cref="TerrainSwatch"/> 方块
/// （原先在 <c>ToolStrip</c> 的 <c>TerrainStrip</c>，本轮迁到这里）；建筑分组内嵌
/// <c>scenes/ui/BuildPaletteSection.tscn</c>，其私有布局状态（分类 / 分页）原样写回本 section 的
/// <see cref="PanelState"/>——spec §8 要求 schema 不变、字段语义不挪用，所以只转发不新增字段。</para>
/// </summary>
public partial class ToolPaletteSection : PaletteContent
{
    /// <summary>地形分组 ID。</summary>
    public const string TerrainGroupId = "terrain";

    /// <summary>建筑分组 ID。</summary>
    public const string BuildingGroupId = "building";

    private const string BuildingScenePath = "res://scenes/ui/BuildPaletteSection.tscn";

    private readonly List<TerrainSwatch> _swatches = new();
    private readonly Dictionary<string, Button> _groupButtons = new();
    private HBoxContainer? _groupBar;
    private Control? _groupHost;
    private AdaptiveGrid? _terrainGrid;
    private BuildPaletteSection? _buildPalette;
    private string _activeGroup = TerrainGroupId;
    private bool _syncing;
    private bool _bound;
    private TerrainType _lastTerrain = TerrainType.Plain;

    /// <summary>当前分组 ID。</summary>
    public string ActiveGroup => _activeGroup;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
        CustomMinimumSize = new Vector2(180f, 72f);

        var root = new VBoxContainer { Name = "Root" };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 2f;
        root.OffsetTop = 1f;
        root.OffsetRight = -2f;
        root.OffsetBottom = -1f;
        root.AddThemeConstantOverride("separation", 2);
        AddChild(root);

        _groupBar = new HBoxContainer { Name = "GroupBar", CustomMinimumSize = new Vector2(0f, 20f) };
        _groupBar.AddThemeConstantOverride("separation", 4);
        root.AddChild(_groupBar);

        var group = new ButtonGroup();
        AddGroupButton(group, TerrainGroupId, "地形");
        AddGroupButton(group, BuildingGroupId, "建筑");

        _groupHost = new Control
        {
            Name = "Groups",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_groupHost);

        _terrainGrid = new AdaptiveGrid
        {
            Name = "TerrainGrid",
            CellSize = 44,
            CellGap = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _terrainGrid.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _groupHost.AddChild(_terrainGrid);
        _terrainGrid.SetItems(BuildTerrainItems());

        if (ResourceLoader.Exists(BuildingScenePath))
        {
            _buildPalette = GD.Load<PackedScene>(BuildingScenePath)?.InstantiateOrNull<BuildPaletteSection>();
        }

        if (_buildPalette is not null)
        {
            _buildPalette.Name = "BuildPalette";
            _buildPalette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _groupHost.AddChild(_buildPalette);
        }
        else
        {
            GD.PushWarning($"缺少 UI 场景，工具方块区的建筑分组为空：{BuildingScenePath}");
        }

        SelectGroup(_activeGroup);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        var context = Context;
        if (context is null)
        {
            return;
        }

        if (!_bound)
        {
            _bound = true;
            _lastTerrain = context.PaintTerrain;
            SyncTerrain(_lastTerrain);
            return;
        }

        if (context.PaintTerrain != _lastTerrain)
        {
            _lastTerrain = context.PaintTerrain;
            SyncTerrain(_lastTerrain);
        }
    }

    /// <summary>切换内部分组（同一时刻只显示一个分组）。</summary>
    public void SelectGroup(string groupId)
    {
        _activeGroup = groupId == BuildingGroupId ? BuildingGroupId : TerrainGroupId;

        var isTerrain = _activeGroup == TerrainGroupId;
        if (_terrainGrid is not null)
        {
            _terrainGrid.Visible = isTerrain;
        }

        if (_buildPalette is not null)
        {
            _buildPalette.Visible = !isTerrain;
        }

        _syncing = true;
        foreach (var pair in _groupButtons)
        {
            pair.Value.ButtonPressed = pair.Key == _activeGroup;
        }

        _syncing = false;
    }

    public override void ApplyState(PanelState? state)
    {
        _buildPalette?.ApplyState(state);
    }

    public override void CaptureState(PanelState state)
    {
        _buildPalette?.CaptureState(state);
    }

    private void AddGroupButton(ButtonGroup group, string groupId, string title)
    {
        var captured = groupId;
        var button = new Button
        {
            Name = "Group_" + groupId,
            Text = title,
            ToggleMode = true,
            ButtonGroup = group,
            CustomMinimumSize = new Vector2(48f, 20f),
            TooltipText = $"{title}工具",
        };
        button.AddThemeFontSizeOverride("font_size", 10);
        button.Toggled += pressed =>
        {
            if (_syncing || !pressed)
            {
                return;
            }

            SelectGroup(captured);
        };

        _groupButtons[groupId] = button;
        _groupBar?.AddChild(button);
    }

    private List<Control> BuildTerrainItems()
    {
        var items = new List<Control>();
        foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
        {
            var captured = terrain;
            var swatch = new TerrainSwatch { Name = "Swatch" + captured, TerrainType = captured };
            swatch.Pressed += () => OnSwatchPressed(captured);
            _swatches.Add(swatch);
            items.Add(swatch);
        }

        return items;
    }

    private void OnSwatchPressed(TerrainType terrain)
    {
        var context = Context;
        if (context is null)
        {
            return;
        }

        context.SetPaintTerrain(terrain);
        context.SetTool(ToolState.Brush);
        SyncTerrain(terrain);
    }

    private void SyncTerrain(TerrainType active)
    {
        foreach (var swatch in _swatches)
        {
            swatch.SetSelected(swatch.TerrainType == active);
        }
    }
}
