using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldView.Domain;
using WorldView.Editing;

namespace WorldView.Editor;

/// <summary>
/// 编辑器总控（挂在 Main.tscn 的 Main 节点）。
///
/// 职责（tasks.md T9 / T17 / T20）：
/// <list type="bullet">
///   <item>启动时确保 <see cref="EditorContext"/> 单例挂载为子节点。</item>
///   <item>解析 Main.tscn 中的 UI 节点（按节点名约定）并连接到上下文信号。</item>
///   <item>响应 UI 按钮事件，调用 <see cref="EditorContext"/> 的状态变更方法。</item>
///   <item>实现 Ctrl+Z / Ctrl+Y / Ctrl+S / V/B/P 快捷键（与 <c>WorldCanvas</c> 互补）。</item>
///   <item>动态生成地形 / 建筑按钮（按 <see cref="EditorContext.Layers"/> + 枚举）。</item>
/// </list>
///
/// 节点名约定（与 Main.tscn 严格对齐，缺一漏一处会导致 UI 静默失联）：
/// <list type="bullet">
///   <item>OpenBtn / SaveBtn / UndoBtn / RedoBtn / BreadcrumbLabel</item>
///   <item>SelectToolBtn / BrushToolBtn / BuildingToolBtn + BrushRoundBtn / BrushSquareBtn / BrushRadiusSlider</item>
///   <item>TerrainBtnContainer / BuildingBtnContainer</item>
///   <item>InspectorBody（容器，动态填充地图信息）</item>
///   <item>LayerList（容器，动态填充图层切换 CheckBox）</item>
///   <item>StatusHintLabel / MouseCellLabel / SelectionLabel / ValidationLabel</item>
/// </list>
/// </summary>
public partial class EditorController : Control
{
    // ------------------------------------------------------------------ 节点引用
    private Button? _openBtn;
    private Button? _saveBtn;
    private Button? _undoBtn;
    private Button? _redoBtn;
    private Label? _breadcrumbLabel;

    private Button? _selectToolBtn;
    private Button? _brushToolBtn;
    private Button? _buildingToolBtn;
    private Button? _brushRoundBtn;
    private Button? _brushSquareBtn;
    private HSlider? _brushRadiusSlider;

    private VBoxContainer? _terrainBtnContainer;
    private VBoxContainer? _buildingBtnContainer;

    private VBoxContainer? _inspectorBody;
    private VBoxContainer? _layerList;

    private Label? _statusHintLabel;
    private Label? _mouseCellLabel;
    private Label? _selectionLabel;
    private Label? _validationLabel;

    // ------------------------------------------------------------------ 状态
    private EditorContext? _context;
    private FileDialog? _fileDialog;

    // ------------------------------------------------------------------ 生命周期

    public override void _Ready()
    {
        _context = EditorContext.Instance ?? EnsureContext();
        CacheNodes();
        CreateFileDialog();
        ConnectSignals();
        RebuildDynamicUi();
        UpdateUiFromContext();
        TryAutoloadSampleProject();
    }

    /// <summary>
    /// 启动时若 <c>res://examples/sample_world/project.yaml</c> 存在，自动加载；
    /// 失败不阻塞编辑器（用户可点 "打开工程" 重试）。
    /// 对应 D1 验收：F5 启动后立即看到示例世界。
    /// </summary>
    private void TryAutoloadSampleProject()
    {
        if (_context is null) return;
        var sampleRoot = EditorContext.ResolvePath("res://examples/sample_world");
        if (System.IO.Directory.Exists(sampleRoot))
        {
            _context.LoadProject(sampleRoot);
        }
        else
        {
            _context.NotifyHint($"未找到 {sampleRoot}，请点击「打开工程…」选择工程根目录（含 project.yaml）。");
        }
    }

    /// <summary>
    /// Main.tscn 已经把 <see cref="EditorContext"/> 挂在 <c>/root/Editor</c> 之前，理论上
    /// 应由 autoload 提供；为 MVP 简化场景，若不存在就在 Main 节点下挂一个实例。
    /// </summary>
    private EditorContext EnsureContext()
    {
        var ctx = new EditorContext { Name = "Editor" };
        AddChild(ctx);
        return ctx;
    }

    private void CacheNodes()
    {
        _openBtn = GetNodeOrNull<Button>("Root/TopBar/OpenBtn");
        _saveBtn = GetNodeOrNull<Button>("Root/TopBar/SaveBtn");
        _undoBtn = GetNodeOrNull<Button>("Root/TopBar/UndoBtn");
        _redoBtn = GetNodeOrNull<Button>("Root/TopBar/RedoBtn");
        _breadcrumbLabel = GetNodeOrNull<Label>("Root/TopBar/BreadcrumbLabel");

        _selectToolBtn = GetNodeOrNull<Button>("Root/MiddleRow/ToolPanel/SelectToolBtn");
        _brushToolBtn = GetNodeOrNull<Button>("Root/MiddleRow/ToolPanel/BrushToolBtn");
        _buildingToolBtn = GetNodeOrNull<Button>("Root/MiddleRow/ToolPanel/BuildingToolBtn");
        _brushRoundBtn = GetNodeOrNull<Button>("Root/MiddleRow/ToolPanel/BrushRoundBtn");
        _brushSquareBtn = GetNodeOrNull<Button>("Root/MiddleRow/ToolPanel/BrushSquareBtn");
        _brushRadiusSlider = GetNodeOrNull<HSlider>("Root/MiddleRow/ToolPanel/BrushRadiusSlider");

        _terrainBtnContainer = GetNodeOrNull<VBoxContainer>("Root/MiddleRow/ToolPanel/TerrainBtnContainer");
        _buildingBtnContainer = GetNodeOrNull<VBoxContainer>("Root/MiddleRow/ToolPanel/BuildingBtnContainer");

        _inspectorBody = GetNodeOrNull<VBoxContainer>("Root/MiddleRow/RightColumn/InspectorBody");
        _layerList = GetNodeOrNull<VBoxContainer>("Root/MiddleRow/RightColumn/LayerList");

        _statusHintLabel = GetNodeOrNull<Label>("Root/StatusBar/StatusHintLabel");
        _mouseCellLabel = GetNodeOrNull<Label>("Root/StatusBar/MouseCellLabel");
        _selectionLabel = GetNodeOrNull<Label>("Root/StatusBar/SelectionLabel");
        _validationLabel = GetNodeOrNull<Label>("Root/StatusBar/ValidationLabel");
    }

    private void ConnectSignals()
    {
        if (_openBtn is not null) _openBtn.Pressed += OnOpenPressed;
        if (_saveBtn is not null) _saveBtn.Pressed += OnSavePressed;
        if (_undoBtn is not null) _undoBtn.Pressed += OnUndoPressed;
        if (_redoBtn is not null) _redoBtn.Pressed += OnRedoPressed;

        if (_selectToolBtn is not null) _selectToolBtn.Pressed += () => _context?.SetTool(ToolState.Selected);
        if (_brushToolBtn is not null) _brushToolBtn.Pressed += () => _context?.SetTool(ToolState.Brush);
        if (_buildingToolBtn is not null) _buildingToolBtn.Pressed += () => _context?.SetTool(ToolState.Building);

        if (_brushRoundBtn is not null) _brushRoundBtn.Pressed += () => _context?.SetBrush(BrushType.Round);
        if (_brushSquareBtn is not null) _brushSquareBtn.Pressed += () => _context?.SetBrush(BrushType.Square);

        if (_brushRadiusSlider is not null)
        {
            _brushRadiusSlider.ValueChanged += v => { if (_context is not null) _context.BrushRadius = Mathf.RoundToInt(v); };
        }

        if (_context is not null)
        {
            _context.MapChanged += RebuildDynamicUi;
            _context.LayersChanged += RebuildDynamicUi;
            _context.StatusHint += OnStatusHint;
            _context.MouseMoved += OnMouseMoved;
            _context.SelectionChanged += OnSelectionChanged;
            _context.ToolChanged += _ => UpdateUiFromContext();
        }
    }

    private void CreateFileDialog()
    {
        _fileDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "选择工程根目录（含 project.yaml）",
            Size = new Vector2I(720, 480),
            CurrentDir = EditorContext.ResolvePath("res://examples"),
        };
        _fileDialog.DirSelected += path => _context?.LoadProject(path);
        AddChild(_fileDialog);
    }

    // ------------------------------------------------------------------ 动态 UI

    private void RebuildDynamicUi()
    {
        RebuildTerrainButtons();
        RebuildBuildingButtons();
        RebuildLayerList();
        RebuildInspector();
        UpdateUiFromContext();
    }

    private void RebuildTerrainButtons()
    {
        if (_terrainBtnContainer is null) return;
        foreach (var child in _terrainBtnContainer.GetChildren())
        {
            child.QueueFree();
        }

        var bg = new ButtonGroup();
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var btn = new Button
            {
                Text = terrain.ToString(),
                ToggleMode = true,
                ButtonGroup = bg,
            };
            if (_context?.PaintTerrain == terrain) btn.ButtonPressed = true;
            var captured = terrain;
            btn.Pressed += () => _context?.SetPaintTerrain(captured);
            _terrainBtnContainer.AddChild(btn);
        }
    }

    private void RebuildBuildingButtons()
    {
        if (_buildingBtnContainer is null) return;
        foreach (var child in _buildingBtnContainer.GetChildren())
        {
            child.QueueFree();
        }

        var bg = new ButtonGroup();
        foreach (var btype in Enum.GetValues<BuildingType>())
        {
            var btn = new Button
            {
                Text = btype.ToString(),
                ToggleMode = true,
                ButtonGroup = bg,
            };
            if (_context?.PlaceBuildingType == btype) btn.ButtonPressed = true;
            var captured = btype;
            btn.Pressed += () => _context?.SetPlaceBuildingType(captured);
            _buildingBtnContainer.AddChild(btn);
        }
    }

    private void RebuildLayerList()
    {
        if (_layerList is null || _context is null) return;
        foreach (var child in _layerList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var layer in _context.Layers)
        {
            var cb = new CheckBox
            {
                Text = $"{layer.Name} ({layer.Type})",
                ButtonPressed = layer.Visible,
            };
            var capturedId = layer.Id;
            var capturedVisible = layer.Visible;
            cb.Toggled += pressed => _context?.SetLayerVisible(capturedId, pressed);
            _layerList.AddChild(cb);
        }
    }

    private void RebuildInspector()
    {
        if (_inspectorBody is null || _context is null) return;
        foreach (var child in _inspectorBody.GetChildren())
        {
            child.QueueFree();
        }

        var map = _context.ActiveMap;
        if (map is null)
        {
            _inspectorBody.AddChild(new Label { Text = "（未加载地图）" });
            return;
        }

        _inspectorBody.AddChild(new Label { Text = $"地图：{map.Name}" });
        _inspectorBody.AddChild(new Label { Text = $"ID：{map.Id}" });
        _inspectorBody.AddChild(new Label { Text = $"类型：{map.Type}" });
        _inspectorBody.AddChild(new Label { Text = $"网格：{map.Grid.Width} × {map.Grid.Height}" });
        _inspectorBody.AddChild(new Label { Text = $"单格距离：{map.Grid.CellDistance} {map.Grid.DistanceUnit}" });
        _inspectorBody.AddChild(new Label { Text = $"图层：{map.Layers.Count}" });
        _inspectorBody.AddChild(new Label { Text = $"建筑：{map.Buildings.Count}" });
        _inspectorBody.AddChild(new Label { Text = $"城市引用：{map.CityRefs.Count}" });
    }

    // ------------------------------------------------------------------ UI 同步

    private void UpdateUiFromContext()
    {
        if (_context is null) return;

        if (_selectToolBtn is not null) _selectToolBtn.ButtonPressed = _context.Tool == ToolState.Selected;
        if (_brushToolBtn is not null) _brushToolBtn.ButtonPressed = _context.Tool == ToolState.Brush;
        if (_buildingToolBtn is not null) _buildingToolBtn.ButtonPressed = _context.Tool == ToolState.Building;

        if (_brushRoundBtn is not null) _brushRoundBtn.ButtonPressed = _context.Brush == BrushType.Round;
        if (_brushSquareBtn is not null) _brushSquareBtn.ButtonPressed = _context.Brush == BrushType.Square;
        if (_brushRadiusSlider is not null && !Equals(_brushRadiusSlider.Value, _context.BrushRadius))
        {
            _brushRadiusSlider.Value = _context.BrushRadius;
        }

        if (_breadcrumbLabel is not null)
        {
            _breadcrumbLabel.Text = _context.IsLoaded
                ? FormatBreadcrumbs(_context)
                : "未加载工程";
        }

        if (_validationLabel is not null)
        {
            var v = _context.Validation;
            _validationLabel.Text = v.Errors.Count == 0
                ? $"校验通过 ({v.Warnings.Count} 警告)"
                : $"校验错误 {v.Errors.Count}";
        }
    }

    private static string FormatBreadcrumbs(EditorContext ctx)
    {
        var parts = new List<string>();
        foreach (var id in ctx.Breadcrumbs) parts.Add(ctx.DisplayNameOf(id));
        parts.Add(ctx.DisplayNameOf(ctx.ActiveMapId));
        return string.Join(" / ", parts);
    }

    // ------------------------------------------------------------------ 槽

    private void OnOpenPressed() => _fileDialog?.PopupCenteredRatio(0.5f);
    private void OnSavePressed() => _context?.SaveProject();
    private void OnUndoPressed() => _context?.UndoStack?.Undo();
    private void OnRedoPressed() => _context?.UndoStack?.Redo();

    private void OnStatusHint(string text)
    {
        if (_statusHintLabel is not null) _statusHintLabel.Text = text;
    }

    private void OnMouseMoved(Vector2I cell)
    {
        if (_mouseCellLabel is not null) _mouseCellLabel.Text = $"({cell.X}, {cell.Y})";
    }

    private void OnSelectionChanged(string description)
    {
        if (_selectionLabel is not null) _selectionLabel.Text = description;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.V when !key.CtrlPressed:
                _context?.SetTool(ToolState.Selected);
                break;
            case Key.B when !key.CtrlPressed:
                _context?.SetTool(ToolState.Brush);
                break;
            case Key.P when !key.CtrlPressed:
                _context?.SetTool(ToolState.Building);
                break;
        }
    }
}