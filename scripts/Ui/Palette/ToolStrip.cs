using System;
using System.Collections.Generic;
using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Editing;

namespace WorldView.Ui.Palette;

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
    private HSlider? _radiusSlider;
    private Label? _radiusValue;
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
        _radiusSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 8,
            Step = 1,
            Value = 1,
            CustomMinimumSize = new Vector2(72f, 16f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "笔刷半径 (1–8)",
        };
        _radiusSlider.ValueChanged += OnRadiusChanged;
        _bar.AddChild(_radiusSlider);

        _radiusValue = MakeCaption("1");
        _radiusValue.CustomMinimumSize = new Vector2(12f, 0f);
        _bar.AddChild(_radiusValue);

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
        if (_radiusSlider is not null)
        {
            _radiusSlider.Value = Mathf.Clamp(context.BrushRadius, 1, 8);
            if (_radiusValue is not null)
            {
                _radiusValue.Text = ((int)_radiusSlider.Value).ToString();
            }
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

    private void OnRadiusChanged(double value)
    {
        var radius = Mathf.Clamp((int)value, 1, 8);
        if (_radiusValue is not null)
        {
            _radiusValue.Text = radius.ToString();
        }

        if (!_syncing && _context is not null)
        {
            _context.BrushRadius = radius;
        }
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
