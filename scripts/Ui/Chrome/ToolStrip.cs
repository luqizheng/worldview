using System;
using System.Collections.Generic;
using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Editing;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 工具条（spec §3.1 row 2 / tasks.md T3 + T11 + T9）：BottomPalette 的 <c>row 2</c>，高 20px。
///
/// <para>承载原来散落在 TopStrip / 状态栏的信息：工具（V / B / P）、笔刷半径、地形、
/// 撤销 / 重做、鼠标格坐标、状态提示、校验结果。</para>
///
/// <para><b>只有 3 个工具按钮</b>：<see cref="ToolState"/> 只有 <c>Selected / Brush / Building</c>
/// 三个值，spec §3.1 图里的第 4 个 <c>E</c> 在 MVP 枚举中不存在，不得新造枚举值。</para>
/// </summary>
public partial class ToolStrip : PaletteContent
{
    private readonly Dictionary<ToolState, Button> _toolButtons = new();

    private readonly List<TerrainSwatch> _terrainSwatches = new();

    private HBoxContainer? _bar;
    private SpinBox? _radiusSpin;
    private Label? _radiusUnit;
    private Label? _radiusValue;
    private CheckButton? _refineCheck;
    private Button? _undoButton;
    private Button? _redoButton;
    private Label? _coordLabel;
    private Label? _statusLabel;
    private Label? _validationLabel;

    private EditorContext? _context;
    private bool _bound;
    private bool _syncing;
    private object? _lastValidation;
    private bool? _lastCanUndo;
    private bool? _lastCanRedo;
    private string? _lastActiveMapId;

    /// <summary>地形取值顺序（与 <see cref="TerrainSwatch"/> 的色块条一致）。</summary>
    private static readonly TerrainType[] TerrainOrder = (TerrainType[])Enum.GetValues(typeof(TerrainType));

    public override void _Ready()
    {
        base._Ready();

        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
        CustomMinimumSize = new Vector2(0f, 20f);

        BuildUi();
        SetProcess(true);
    }

    // ------------------------------------------------------------------ 构建

    private void BuildUi()
    {
        _bar = new HBoxContainer { Name = "Bar" };
        _bar.AddThemeConstantOverride("separation", 6);
        AddChild(_bar);
        _bar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _bar.OffsetLeft = 4f;
        _bar.OffsetTop = 1f;
        _bar.OffsetRight = -6f;
        _bar.OffsetBottom = -1f;

        var group = new ButtonGroup();
        AddToolButton(group, "V", ToolState.Selected, "选择 / 检视 (V)");
        AddToolButton(group, "B", ToolState.Brush, "地形笔刷 (B)");
        AddToolButton(group, "P", ToolState.Building, "放置建筑 (P)");

        AddSeparator();

        _bar.AddChild(MakeCaption("笔刷"));
        _radiusSpin = new SpinBox
        {
            MinValue = 0.1,
            MaxValue = 5000,
            Step = 0.1,
            Value = 1,
            CustomMinimumSize = new Vector2(72f, 16f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        // SpinBox 继承 Range（非 Control），没有 TooltipText —— 提示挂到它内部的 LineEdit 上。
        _radiusSpin.Ready += OnRadiusSpinReady;
        _radiusSpin.ValueChanged += OnRadiusChanged;
        _bar.AddChild(_radiusSpin);

        _radiusUnit = MakeCaption("格");
        _bar.AddChild(_radiusUnit);

        _radiusValue = MakeCaption("= 1 格");
        _radiusValue.CustomMinimumSize = new Vector2(52f, 0f);
        _radiusValue.TooltipText = "按当前地图的单格距离换算后实际生效的格半径（上限 64 格）";
        _bar.AddChild(_radiusValue);

        _refineCheck = new CheckButton
        {
            Text = "细化",
            ButtonPressed = _context?.RefineOnStroke ?? true,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "落笔结束时沿海岸线做一次细化（一次拖动仍只记 1 步撤销）",
        };
        _refineCheck.AddThemeFontSizeOverride("font_size", 10);
        ApplyCompactStyle(_refineCheck, flat: true);
        _refineCheck.Toggled += OnRefineToggled;
        _bar.AddChild(_refineCheck);

        AddSeparator();

        _bar.AddChild(MakeCaption("地形"));
        var terrainStrip = new HBoxContainer
        {
            Name = "TerrainStrip",
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        terrainStrip.AddThemeConstantOverride("separation", 2);
        _bar.AddChild(terrainStrip);

        foreach (var terrain in TerrainOrder)
        {
            var swatch = new TerrainSwatch { Name = "Swatch" + terrain, TerrainType = terrain };
            var captured = terrain;
            // 色块既能拖到画布（§4.2），也能单击选为当前笔刷地形。
            swatch.Pressed += () => OnSwatchPressed(captured);
            terrainStrip.AddChild(swatch);
            _terrainSwatches.Add(swatch);
        }

        AddSeparator();

        _undoButton = MakeIconButton("⟲", "撤销 (Ctrl+Z)", () => _context?.UndoStack.Undo());
        _bar.AddChild(_undoButton);

        _redoButton = MakeIconButton("⟳", "重做 (Ctrl+Shift+Z)", () => _context?.UndoStack.Redo());
        _bar.AddChild(_redoButton);

        AddSeparator();

        _coordLabel = MakeCaption("坐标(-,-)");
        _coordLabel.CustomMinimumSize = new Vector2(96f, 0f);
        _bar.AddChild(_coordLabel);

        AddSeparator();

        _statusLabel = MakeCaption("就绪");
        _statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _bar.AddChild(_statusLabel);

        _validationLabel = MakeCaption("校验 —");
        _validationLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _validationLabel.CustomMinimumSize = new Vector2(110f, 0f);
        _bar.AddChild(_validationLabel);
    }

    private void AddToolButton(ButtonGroup group, string text, ToolState tool, string tooltip)
    {
        var button = new Button
        {
            Name = "Tool" + tool,
            Text = text,
            ToggleMode = true,
            ButtonGroup = group,
            Flat = false,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(22f, 16f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontSizeOverride("font_size", 10);
        ApplyCompactStyle(button, flat: false);
        button.Toggled += pressed =>
        {
            if (pressed && !_syncing)
            {
                _context?.SetTool(tool);
            }
        };

        _toolButtons[tool] = button;
        _bar!.AddChild(button);
    }

    private void AddSeparator()
    {
        var separator = MakeCaption("│");
        separator.AddThemeColorOverride("font_color", new Color(0.30f, 0.30f, 0.36f));
        _bar!.AddChild(separator);
    }

    private static Label MakeCaption(string text)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
        return label;
    }

    private static Button MakeIconButton(string text, string tooltip, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(22f, 16f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        ApplyCompactStyle(button, flat: true);
        button.Pressed += onPressed;
        return button;
    }

    /// <summary>
    /// <see cref="SpinBox"/> 继承 <c>Range</c> 而不是 <c>Control</c>，没有 <c>TooltipText</c>，
    /// 因此提示要挂到它内部那个 <see cref="LineEdit"/> 上。同时把输入框压扁，
    /// 否则主题默认内容边距会把控件撑过 <c>tool_row</c> 的 20px。
    /// </summary>
    private void OnRadiusSpinReady()
    {
        if (_radiusSpin?.GetLineEdit() is not { } edit)
        {
            return;
        }

        edit.TooltipText = "笔刷半径（物理单位）：世界图按 km、城市图按 m 输入，自动换算为格数";
        edit.AddThemeFontSizeOverride("font_size", 10);
        edit.CustomMinimumSize = new Vector2(0f, 16f);
        edit.AddThemeStyleboxOverride("normal", CompactStyle(new Color(0.14f, 0.14f, 0.18f)));
        edit.AddThemeStyleboxOverride("focus", CompactStyle(new Color(0.20f, 0.20f, 0.26f)));
    }

    /// <summary>
    /// 主题里的 Button 样式盒自带较大的内容边距（实测把按钮撑到 22px 高），
    /// 而 <c>tool_row</c> 只有 20px（spec §3.1：28 + 152 + 20 = 200），
    /// 会被 <c>ClipContents</c> 裁掉底部。这里换成紧凑样式盒（上下各 1px）。
    /// </summary>
    private static void ApplyCompactStyle(Button button, bool flat)
    {
        button.AddThemeStyleboxOverride("normal", CompactStyle(flat ? Colors.Transparent : new Color(0.18f, 0.18f, 0.22f)));
        button.AddThemeStyleboxOverride("hover", CompactStyle(new Color(0.24f, 0.24f, 0.30f)));
        button.AddThemeStyleboxOverride("pressed", CompactStyle(new Color(0.45f, 0.55f, 0.85f, 0.85f)));
        button.AddThemeStyleboxOverride("disabled", CompactStyle(new Color(0.14f, 0.14f, 0.17f, 0.55f)));
        button.AddThemeStyleboxOverride("focus", CompactStyle(new Color(0.45f, 0.55f, 0.85f, 0.30f)));
    }

    private static StyleBoxFlat CompactStyle(Color background)
    {
        var style = new StyleBoxFlat { BgColor = background };
        style.SetCornerRadiusAll(3);
        style.ContentMarginTop = 1f;
        style.ContentMarginBottom = 1f;
        style.ContentMarginLeft = 4f;
        style.ContentMarginRight = 4f;
        return style;
    }

    // ------------------------------------------------------------------ 延迟绑定

    public override void _Process(double delta)
    {
        if (!_bound)
        {
            if (EditorContext.Instance is not { } context)
            {
                return;
            }

            _bound = true;
            Bind(context);
            return;
        }

        // UndoStack / Validation 都不带变更信号，这里做廉价脏检查。
        if (_context is null)
        {
            return;
        }

        var canUndo = _context.UndoStack.CanUndo;
        if (_lastCanUndo != canUndo)
        {
            _lastCanUndo = canUndo;
            if (_undoButton is not null)
            {
                _undoButton.Disabled = !canUndo;
            }
        }

        var canRedo = _context.UndoStack.CanRedo;
        if (_lastCanRedo != canRedo)
        {
            _lastCanRedo = canRedo;
            if (_redoButton is not null)
            {
                _redoButton.Disabled = !canRedo;
            }
        }
    }

    private void Bind(EditorContext context)
    {
        _context = context;
        context.ToolChanged += OnToolChanged;
        context.MouseMoved += OnMouseMoved;
        context.StatusHint += OnStatusHint;
        context.SelectionChanged += OnSelectionChanged;
        context.MapChanged += OnMapChanged;
        context.LayersChanged += OnMapChanged;

        SyncTool(context.Tool);
        _lastActiveMapId = context.ActiveMapId;
        SyncRadius(context.BrushRadius);
        if (_refineCheck is not null)
        {
            _syncing = true;
            _refineCheck.ButtonPressed = context.RefineOnStroke;
            _syncing = false;
        }

        SyncTerrain(context.PaintTerrain);

        RefreshValidation();
    }

    // ------------------------------------------------------------------ 事件

    private void OnToolChanged(int tool)
    {
        SyncTool((ToolState)tool);
    }

    /// <summary>
    /// 选中信息（旧 StatusBar 的 SelectionLabel 位置，spec §3.3 未单列此标签）。
    /// 与 StatusHint 共用同一个状态标签：最后写入者生效。
    /// </summary>
    private void OnSelectionChanged(string description)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = string.IsNullOrEmpty(description) ? "就绪" : description;
    }

    private void SyncTool(ToolState tool)
    {
        if (!_toolButtons.TryGetValue(tool, out var button))
        {
            return;
        }

        _syncing = true;
        if (!button.ButtonPressed)
        {
            button.ButtonPressed = true;
        }

        _syncing = false;
    }

    private void OnMouseMoved(Vector2I cell)
    {
        if (_coordLabel is not null)
        {
            _coordLabel.Text = $"坐标({cell.X},{cell.Y})";
        }
    }

    private void OnStatusHint(string text)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.IsNullOrEmpty(text) ? "就绪" : text;
        }
    }

    private void OnMapChanged()
    {
        // 换图（或图层变化）后单格物理距离与单位都可能变：把当前格半径按新图重新显示出来。
        if (_context is not null && _context.ActiveMapId != _lastActiveMapId)
        {
            _lastActiveMapId = _context.ActiveMapId;
            SyncRadius(_context.BrushRadius);
        }

        RefreshValidation();
    }

    private void RefreshValidation()
    {
        if (_context is null || _validationLabel is null)
        {
            return;
        }

        var validation = _context.Validation;
        if (ReferenceEquals(validation, _lastValidation))
        {
            return;
        }

        _lastValidation = validation;

        if (validation is null)
        {
            _validationLabel.Text = "校验 —";
            return;
        }

        if (validation.IsValid)
        {
            _validationLabel.Text = validation.Warnings.Count > 0
                ? $"校验 OK · {validation.Warnings.Count} 警告"
                : "校验 OK";
            _validationLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.78f, 0.55f));
            return;
        }

        _validationLabel.Text = $"校验 {validation.Errors.Count} 错误";
        _validationLabel.AddThemeColorOverride("font_color", new Color(0.902f, 0.42f, 0.29f));
    }

    /// <summary>
    /// 半径控件的回调：把用户输入的物理长度换算成格数。
    ///
    /// <para>口径（plan L394-395 + spec 1 §5.2）：<c>cells = Clamp(CellsForLength(physical, CellDistance), 1, 64)</c>。
    /// 在 <c>_syncing</c> 为真时只刷新显示、不回写 <c>EditorContext.BrushRadius</c>（避免回环）。</para>
    ///
    /// <para>三种退化情形都退回「纯格数直选」（1–64，步长 1）：
    /// ① 无活动地图；② 单格距离不是正数（<see cref="MapSizing.CellsForLength"/> 会抛
    /// <see cref="ArgumentOutOfRangeException"/>）；③ 控件尚未构建。</para>
    /// </summary>
    private void OnRadiusChanged(double value)
    {
        var map = _context?.ActiveMap;
        var cellDistance = map?.Grid.CellDistance ?? 0d;
        var physical = cellDistance > 0 && map is not null;

        int cells;
        if (physical)
        {
            cells = Mathf.Clamp(MapSizing.CellsForLength(value, cellDistance), 1, MapSizing.MaxRadiusCells);
        }
        else
        {
            cells = Mathf.Clamp((int)Math.Round(value), 1, MapSizing.MaxRadiusCells);
        }

        if (!_syncing && _context is not null)
        {
            _context.BrushRadius = cells;
        }

        UpdateRadiusDisplay(cells, physical);
    }

    /// <summary>反向同步：由 <c>context.BrushRadius</c> 反推控件该显示的物理值/格数。</summary>
    private void SyncRadius(int brushRadius)
    {
        if (_radiusSpin is null)
        {
            return;
        }

        var map = _context?.ActiveMap;
        var cellDistance = map?.Grid.CellDistance ?? 0d;
        var physical = cellDistance > 0 && map is not null;
        var cells = Mathf.Clamp(brushRadius, 1, MapSizing.MaxRadiusCells);

        _syncing = true;
        SetRadiusMode(physical);
        _radiusSpin.Value = physical ? MapSizing.LengthForCells(cells, cellDistance) : cells;
        _syncing = false;

        UpdateRadiusDisplay(cells, physical);
    }

    private void OnRefineToggled(bool pressed)
    {
        if (!_syncing && _context is not null)
        {
            _context.RefineOnStroke = pressed;
        }
    }

    /// <summary>切换半径控件的工作模式：物理单位（世界图 km / 城市图 m）或纯格数直选。</summary>
    private void SetRadiusMode(bool physical)
    {
        if (_radiusSpin is null)
        {
            return;
        }

        if (physical)
        {
            if (!Mathf.IsEqualApprox((float)_radiusSpin.MinValue, 0.1f))
            {
                _radiusSpin.MinValue = 0.1;
            }

            _radiusSpin.MaxValue = 5000;
            _radiusSpin.Step = 0.1;
        }
        else
        {
            _radiusSpin.MinValue = 1;
            _radiusSpin.MaxValue = MapSizing.MaxRadiusCells;
            _radiusSpin.Step = 1;
        }

        if (_radiusUnit is not null)
        {
            _radiusUnit.Text = physical ? UnitOf(_context?.ActiveMap) : "格";
            _radiusUnit.Visible = physical;
        }
    }

    /// <summary>派生标签显示「实际生效」的格数（不是用户输入的物理值）。</summary>
    private void UpdateRadiusDisplay(int cells, bool physical)
    {
        if (_radiusValue is null)
        {
            return;
        }

        _radiusValue.Text = cells > 32 ? $"= {cells} 格（较大，落笔较慢）" : $"= {cells} 格";
        _radiusValue.TooltipText = physical
            ? $"按当前地图的单格距离换算：1 格 = {_context?.ActiveMap?.Grid.CellDistance} {UnitOf(_context?.ActiveMap)}；上限 64 格"
            : $"当前没有可用的单格距离，按纯格数输入；上限 {MapSizing.MaxRadiusCells} 格";
    }

    /// <summary>
    /// 单位口径：优先用 <see cref="GridSpec.DistanceUnit"/>（数据模型的事实来源），
    /// 为空/空白时按 <see cref="MapType"/> 回退（World → km，City → m）。
    /// </summary>
    private static string UnitOf(MapDoc? map)
    {
        if (map is null)
        {
            return "格";
        }

        var unit = map.Grid.DistanceUnit;
        if (!string.IsNullOrWhiteSpace(unit))
        {
            return unit.Trim();
        }

        return map.Type switch
        {
            MapType.World => "km",
            MapType.City => "m",
            _ => "m",
        };
    }

    private void OnSwatchPressed(TerrainType terrain)
    {
        if (_context is null)
        {
            return;
        }

        _context.SetPaintTerrain(terrain);
        _context.SetTool(ToolState.Brush);
        SyncTerrain(terrain);
    }

    private void SyncTerrain(TerrainType terrain)
    {
        foreach (var swatch in _terrainSwatches)
        {
            swatch.SetSelected(swatch.TerrainType == terrain);
        }
    }
}
