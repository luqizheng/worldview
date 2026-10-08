using Godot;
using WorldView.Domain;
using WorldView.Editing;
using WorldView.Ui;
using WorldView.Ui.Dock;
using WorldView.Ui.Inspector;
using WorldView.Ui.LayerPanelUi;
using WorldView.Ui.Palette;

namespace WorldView.Editor;

/// <summary>
/// 编辑器总控（挂在 Main.tscn 的 Main 节点）。
/// <para>
/// M2 职责：
/// <list type="bullet">
///   <item>启动时确保 <see cref="EditorContext"/> 单例挂载为子节点。</item>
///   <item>解析 Main.tscn 中的 UI 节点（按节点名约定）并连接到上下文信号。</item>
///   <item>把 <see cref="ToolStrip"/> / <see cref="Inspector"/> / <see cref="LayerPanel"/> 注册到 <see cref="DockLayout"/>。</item>
///   <item>提供 F11 全屏、Ctrl+B 折叠、Ctrl+I 检视器、Ctrl+L 图层面板 快捷键。</item>
/// </list>
/// </para>
/// </summary>
public partial class EditorController : Control
{
	// ------------------------------------------------------------------ 节点引用
	private Button? _openBtn;
	private Button? _saveBtn;
	private Button? _undoBtn;
	private Button? _redoBtn;
	private Label? _breadcrumbLabel;
	private Button? _fullscreenBtn;

	private DockLayout? _dockLayout;
	private BottomPalette? _bottomPalette;
	private ToolStrip? _toolStrip;
	private FloatPanelHost? _inspectorHost;
	private FloatPanelHost? _layerPanelHost;

	// ------------------------------------------------------------------ 状态
	private EditorContext? _context;
	private FileDialog? _fileDialog;
	private bool _fullscreen;

	// ------------------------------------------------------------------ 生命周期

	public override void _Ready()
	{
		_context = EditorContext.Instance ?? EnsureContext();
		CacheNodes();
		CreateFileDialog();
		WireDock();
		ConnectSignals();
		TryAutoloadSampleProject();
	}

	private void CacheNodes()
	{
		_openBtn = GetNodeOrNull<Button>("DockLayout/CenterZone/MainLayout/TopStrip/OpenBtn");
		_saveBtn = GetNodeOrNull<Button>("DockLayout/CenterZone/MainLayout/TopStrip/SaveBtn");
		_undoBtn = GetNodeOrNull<Button>("DockLayout/CenterZone/MainLayout/TopStrip/UndoBtn");
		_redoBtn = GetNodeOrNull<Button>("DockLayout/CenterZone/MainLayout/TopStrip/RedoBtn");
		_breadcrumbLabel = GetNodeOrNull<Label>("DockLayout/CenterZone/MainLayout/TopStrip/BreadcrumbLabel");
		_fullscreenBtn = GetNodeOrNull<Button>("DockLayout/CenterZone/MainLayout/TopStrip/FullscreenBtn");

		_dockLayout = GetNodeOrNull<DockLayout>("DockLayout");
		_bottomPalette = GetNodeOrNull<BottomPalette>("DockLayout/CenterZone/MainLayout/BottomPalette");
	}

	/// <summary>把所有 DockPanel 子树接入 DockLayout 注册表。</summary>
	private void WireDock()
	{
		if (_dockLayout is null) return;

		// ToolStrip — 由 W2 直接生成在 .tscn 中,本步只负责注册
		_toolStrip = FindToolStrip();
		if (_toolStrip is not null)
		{
			_dockLayout.RegisterPanel(_toolStrip);
			_toolStrip.BindContextSignals();
		}

		// Inspector / LayerPanel — 默认浮动、隐藏
		_inspectorHost = new FloatPanelHost
		{
			Name = "InspectorHost",
			PanelId = "inspector",
			Title = "Inspector",
			InitialPosition = new Vector2I(120, 80),
		};
		var inspector = new Inspector();
		_inspectorHost.AdoptPanel(inspector);
		_dockLayout.RegisterPanel(inspector);
		AddChild(_inspectorHost);
		inspector.BindContextSignals();

		_layerPanelHost = new FloatPanelHost
		{
			Name = "LayerPanelHost",
			PanelId = "layer_panel",
			Title = "Layers",
			InitialPosition = new Vector2I(420, 80),
		};
		var layerPanel = new LayerPanel();
		_layerPanelHost.AdoptPanel(layerPanel);
		_dockLayout.RegisterPanel(layerPanel);
		AddChild(_layerPanelHost);
		layerPanel.BindContextSignals();

		// TopStrip 上的 Inspector / Layers 按钮：先取 F10 / F9 快捷键代替,TopStrip 不加按钮。
	}

	private ToolStrip? FindToolStrip()
	{
		// BottomPalette/ToolRow 下找 ToolStrip 节点
		var toolRow = GetNodeOrNull("DockLayout/CenterZone/MainLayout/BottomPalette/ToolRow");
		if (toolRow is null) return null;
		foreach (var child in toolRow.GetChildren())
		{
			if (child is ToolStrip ts) return ts;
			foreach (var inner in child.GetChildren())
			{
				if (inner is ToolStrip ts2) return ts2;
			}
		}
		return null;
	}

	private void ConnectSignals()
	{
		if (_openBtn is not null) _openBtn.Pressed += OnOpenPressed;
		if (_saveBtn is not null) _saveBtn.Pressed += OnSavePressed;
		if (_undoBtn is not null) _undoBtn.Pressed += OnUndoPressed;
		if (_redoBtn is not null) _redoBtn.Pressed += OnRedoPressed;
		if (_fullscreenBtn is not null) _fullscreenBtn.Pressed += () => ToggleFullscreen();

		if (_context is not null)
		{
			_context.StatusHint += OnStatusHint;
			_context.ToolChanged += OnToolChanged;
			_context.MouseMoved += OnMouseMoved;
			_context.SelectionChanged += OnSelectionChanged;
			_context.BreadcrumbChanged += UpdateBreadcrumb;
		}
	}

	private void OnToolChanged(int tool)
	{
		// 工具切换时刷新 Title 显示,无 DOM 副作用。
		_context?.NotifyHint($"工具已切换（{(ToolState)tool}）");
	}

	private void UpdateBreadcrumb()
	{
		if (_breadcrumbLabel is null || _context is null) return;
		if (!_context.IsLoaded)
		{
			_breadcrumbLabel.Text = "未加载工程";
			return;
		}
		var parts = new System.Collections.Generic.List<string>();
		foreach (var id in _context.Breadcrumbs) parts.Add(_context.DisplayNameOf(id));
		parts.Add(_context.DisplayNameOf(_context.ActiveMapId));
		_breadcrumbLabel.Text = string.Join(" / ", parts);
	}

	private EditorContext EnsureContext()
	{
		var ctx = new EditorContext { Name = "Editor" };
		AddChild(ctx);
		return ctx;
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
			_context.NotifyHint($"未找到 {sampleRoot},请点击 \"打开\" 选择工程目录。");
		}
	}

	// ------------------------------------------------------------------ 槽

	private void OnOpenPressed()
	{
		_fileDialog?.Popup(new Rect2I(8, 8, 720, 480));
	}

	private void OnSavePressed() => _context?.SaveProject();

	private void OnUndoPressed() => _context?.UndoStack?.Undo();

	private void OnRedoPressed() => _context?.UndoStack?.Redo();

	private void OnStatusHint(string text)
	{
		// 状态文本由 ToolStrip.StatusLabel 承载；本处留空以备扩展。
	}

	private void OnMouseMoved(Vector2I cell) { }
	private void OnSelectionChanged(string description) { }

	// ------------------------------------------------------------------ 全屏 / 折叠 / 浮动

	private void ToggleFullscreen()
	{
		_fullscreen = !_fullscreen;
		if (_dockLayout is not null) _dockLayout.SetAllPanelsVisible(!_fullscreen);
		_context?.NotifyHint(_fullscreen ? "进入全屏地图模式（F11 退出）" : "退出全屏");
	}

	private void TogglePalette()
	{
		if (_bottomPalette is null) return;
		_bottomPalette.ToggleCollapsed();
		_context?.NotifyHint(_bottomPalette.Collapsed ? "BottomPalette 已折叠（Ctrl+B 展开）" : "BottomPalette 已展开");
	}

	private void ToggleInspector()
	{
		if (_inspectorHost is null) return;
		if (_inspectorHost.Visible) _inspectorHost.HidePanel();
		else _inspectorHost.ShowPanel();
	}

	private void ToggleLayerPanel()
	{
		if (_layerPanelHost is null) return;
		if (_layerPanelHost.Visible) _layerPanelHost.HidePanel();
		else _layerPanelHost.ShowPanel();
	}

	// ------------------------------------------------------------------ 键盘

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
			case Key.F11:
				ToggleFullscreen();
				break;
			case Key.B when key.CtrlPressed:
				TogglePalette();
				break;
			case Key.I when key.CtrlPressed:
				ToggleInspector();
				break;
			case Key.L when key.CtrlPressed:
				ToggleLayerPanel();
				break;
		}
	}
}
