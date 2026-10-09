using System.Collections.Generic;
using Godot;
using WorldView.Ui;
using WorldView.Ui.Components;
using WorldView.Ui.Dock;

namespace WorldView.Editor;

/// <summary>
/// 编辑器总控（挂在 <c>Main.tscn</c> 的 Main 节点）。
///
/// <para>UI 重设计后（spec §6）本类只保留<b>全局</b>职责，不再解析任何面板内部节点：</para>
/// <list type="bullet">
///   <item>启动时确保 <see cref="EditorContext"/> 实例存在并挂为子节点。</item>
///   <item>顶栏（<c>UiLayer/DockLayout/Root/TopStrip/TopBar</c>）：打开工程 / 保存 / 撤销 / 重做 / 全屏 / 面包屑。</item>
///   <item>把地图节点（留在默认画布层，继续受 Camera2D 变换）对齐到 UI 层的画布区矩形。</item>
///   <item>工程自动加载、文件对话框、全局快捷键（V / B / P / F11）。</item>
/// </list>
///
/// <para>工具按钮、笔刷、地形色块、坐标 / 状态 / 校验状态、检视器与图层面板分别由
/// <c>ToolStrip</c>、<c>BuildPaletteSection</c>、<c>Inspector</c>、<c>LayerPanel</c> 自行绑定，
/// 组件之间不通过本类中转。</para>
/// </summary>
public partial class EditorController : Control
{
	// ------------------------------------------------------------------ 节点引用
	private const string TopBarPath = "UiLayer/DockLayout/Root/TopStrip/TopBar";

	/// <summary>UI 层的画布区矩形（UI 在 <c>CanvasLayer(layer=1)</c> 内，不受相机变换）。</summary>
	private const string CanvasAreaPath = "UiLayer/DockLayout/Root/MidRow/CanvasArea";

	/// <summary>地图节点（唯一名；位于默认画布层，仍受 Camera2D 变换）。</summary>
	private const string WorldCanvasPath = "%WorldCanvas";

	private Button? _openBtn;
	private Button? _saveBtn;
	private Button? _undoBtn;
	private Button? _redoBtn;
	private Button? _fullscreenBtn;
	private JrpgBreadcrumb? _breadcrumb;
	private HBoxContainer? _topBar;
	private JrpgButton? _mapSettingsBtn;
	private JrpgScaleBar? _scaleBar;
	private JrpgMapTabBar? _mapTabBar;
	private JrpgReplayBar? _replayBar;

	// ------------------------------------------------------------------ 状态
	private EditorContext? _context;
	private FileDialog? _fileDialog;
	private MapSettingsDialog? _mapSettingsDialog;
	private Control? _canvasArea;
	private WorldView.Render.WorldCanvas? _worldCanvas;
	private bool _initialFitDone;
	private bool _lastCanUndo;
	private bool _lastCanRedo;

	// ------------------------------------------------------------------ 生命周期

	public override void _Ready()
	{
		_context = EditorContext.Instance ?? EnsureContext();
		CacheNodes();
		CreateFileDialog();
		CreateMapSettingsDialog();
		ConnectSignals();
		UpdateBreadcrumb();
		UpdateMapTabBar();
		UpdateUndoButtons(force: true);
		TryAutoloadSampleProject();
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		UpdateUndoButtons(force: false);
		SyncMapToCanvasArea();
	}

	/// <summary>
	/// 地图节点留在默认画布层（继续受 Camera2D 的平移 / 缩放），UI 在 <c>CanvasLayer(layer=1)</c> 内。
	/// 地图已不再是 <c>CanvasArea</c> 的子节点，因此每帧把它的位置对齐到画布区矩形左上角——
	/// 等价于改造前「WorldCanvas 作为 CanvasArea 子节点继承 (0,32) 偏移」的效果。
	/// </summary>
	private void SyncMapToCanvasArea()
	{
		if (_canvasArea is null || _worldCanvas is null ||
			!IsInstanceValid(_canvasArea) || !IsInstanceValid(_worldCanvas))
		{
			return;
		}

		var target = _canvasArea.GlobalPosition;
		if (!_worldCanvas.Position.IsEqualApprox(target))
		{
			_worldCanvas.Position = target;
		}

		// 相机拟合目标 = 画布区矩形（不是整个窗口）：地图留在默认画布层仍受 Camera2D 缩放，
		// 而 UI 在 CanvasLayer 内不受缩放，若按窗口拟合，地图会比画布区大一截、下半被底栏遮住。
		var size = _canvasArea.Size;
		if (size.X <= 1f || size.Y <= 1f)
		{
			return;
		}

		_worldCanvas.FitRect = new Rect2(_canvasArea.GlobalPosition, size);

		// 初始拟合只能等布局跑完（_Ready 时 CanvasArea 尺寸还是 0，此时会退回旧行为按窗口拟合）。
		if (!_initialFitDone)
		{
			_initialFitDone = true;
			_worldCanvas.RefitCamera();
		}
	}

	/// <summary>
	/// Main.tscn 没有把 <see cref="EditorContext"/> 作为 autoload，这里兜底挂一个实例
	/// （<c>EditorContext</c> 自身在 <c>_EnterTree</c> 里维护 <c>Instance</c>）。
	/// </summary>
	private EditorContext EnsureContext()
	{
		var ctx = new EditorContext { Name = "Editor" };
		AddChild(ctx);
		return ctx;
	}

	private void CacheNodes()
	{
		_openBtn = GetNodeOrNull<Button>($"{TopBarPath}/OpenBtn");
		_saveBtn = GetNodeOrNull<Button>($"{TopBarPath}/SaveBtn");
		_undoBtn = GetNodeOrNull<Button>($"{TopBarPath}/UndoBtn");
		_redoBtn = GetNodeOrNull<Button>($"{TopBarPath}/RedoBtn");
		_fullscreenBtn = GetNodeOrNull<Button>($"{TopBarPath}/FullscreenBtn");
		_breadcrumb = GetNodeOrNull<JrpgBreadcrumb>($"{TopBarPath}/Breadcrumb");
		_scaleBar = GetNodeOrNull<JrpgScaleBar>($"{CanvasAreaPath}/CanvasOverlay/ScaleBar");
		_mapTabBar = GetNodeOrNull<JrpgMapTabBar>("UiLayer/DockLayout/Root/MapTabBar");
		_replayBar = GetNodeOrNull<JrpgReplayBar>("UiLayer/DockLayout/Root/ReplayBar");
		_topBar = GetNodeOrNull<HBoxContainer>(TopBarPath);
		_canvasArea = GetNodeOrNull<Control>(CanvasAreaPath);
		_worldCanvas = GetNodeOrNull<WorldView.Render.WorldCanvas>(WorldCanvasPath);
	}

	private void ConnectSignals()
	{
		if (_openBtn is not null) _openBtn.Pressed += OnOpenPressed;
		if (_saveBtn is not null) _saveBtn.Pressed += OnSavePressed;
		if (_undoBtn is not null) _undoBtn.Pressed += OnUndoPressed;
		if (_redoBtn is not null) _redoBtn.Pressed += OnRedoPressed;
		if (_fullscreenBtn is not null) _fullscreenBtn.Pressed += OnFullscreenPressed;
		if (_breadcrumb is not null) _breadcrumb.MapJumpRequested += OnBreadcrumbJump;
		if (_mapTabBar is not null) _mapTabBar.MapTabSelected += OnMapTabSelected;
		if (_replayBar is not null)
		{
			_replayBar.Visible = _context?.Mode == "replay";
		}

		if (_context is not null)
		{
			_context.MapChanged += UpdateBreadcrumb;
			_context.LayersChanged += UpdateBreadcrumb;
			_context.MapChanged += UpdateMapTabBar;
			_context.MouseMoved += OnMouseMoved;
			_context.ModeChanged += OnModeChanged;
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

	/// <summary>
	/// 地图设置对话框（Task 13）。沿用 <see cref="CreateFileDialog"/> 的「动态创建 + AddChild」手法；
	/// 顶栏按钮也在代码里建，避免为了一个入口去改 <c>Main.tscn</c>（本任务文件范围只有 2 个）。
	/// </summary>
	private void CreateMapSettingsDialog()
	{
		_mapSettingsDialog = new MapSettingsDialog { Name = "MapSettingsDialog" };
		AddChild(_mapSettingsDialog);

		if (_topBar is null)
		{
			return;
		}

		_mapSettingsBtn = new JrpgButton
		{
			Name = "MapSettingsBtn",
			Text = "地图…",
			TooltipText = "新建世界地图 / 调整当前地图尺寸（Ctrl+M）",
			CustomMinimumSize = new Vector2(64f, 0f),
		};
		_mapSettingsBtn.Pressed += OnMapSettingsPressed;
		_topBar.AddChild(_mapSettingsBtn);

		// 插到「打开工程… / 保存」之后、面包屑之前，保持顶栏原有语义分组。
		var index = _saveBtn is not null ? _saveBtn.GetIndex() + 1 : 1;
		_topBar.MoveChild(_mapSettingsBtn, Mathf.Min(index, _topBar.GetChildCount() - 1));
	}

	private void OnMapSettingsPressed()
	{
		if (_mapSettingsDialog is null || _context is null)
		{
			return;
		}

		// 无活动地图 → 新建世界态；有活动地图 → 改该图尺寸（生成参数只读）。
		_mapSettingsDialog.Setup(_context, _context.ActiveMap);
		_mapSettingsDialog.PopupCentered(new Vector2I(560, 460));
	}

	/// <summary>
	/// 启动时若 <c>res://examples/sample_world/project.yaml</c> 存在，自动加载；
	/// 失败不阻塞编辑器（用户可点「打开工程…」重试）。对应 D1 验收：F5 启动后立即看到示例世界。
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

	// ------------------------------------------------------------------ UI 同步

	private void UpdateBreadcrumb()
	{
		if (_breadcrumb is null)
		{
			return;
		}

		// §4.9.7 面包屑：工程名 / 地图1 / 地图2，每段可点击；末段=当前地图用 JrpgText 不可点
		if (_context is { IsLoaded: true } ctx)
		{
			var crumbs = new List<JrpgBreadcrumb.Crumb>
			{
				new(ctx.World.Name, null),
			};
			foreach (var id in ctx.Breadcrumbs) crumbs.Add(new(ctx.DisplayNameOf(id), id));
			crumbs.Add(new(ctx.DisplayNameOf(ctx.ActiveMapId), null));
			_breadcrumb.SetCrumbs(crumbs);
		}
		else
		{
			_breadcrumb.SetCrumbs(new[] { new JrpgBreadcrumb.Crumb("未加载工程", null) });
		}
	}

	private void OnBreadcrumbJump(string mapId)
	{
		// §4.9.7 面包屑：跳转到非当前地图；DrillBack 一路退到目标
		var ctx = _context;
		if (ctx is null) return;
		while (ctx.ActiveMapId != mapId && ctx.CanDrillBack)
		{
			ctx.DrillBack();
		}
	}

	private void OnMapTabSelected(string mapId)
	{
		// §4.9.7 多地图标签页：直接切活动地图
		_context?.SetActiveMap(mapId);
	}

	private void OnModeChanged(string mode)
	{
		// §4.9.5 replay 顶条：仅 replay 模式显示
		if (_replayBar is not null)
		{
			_replayBar.Visible = mode == "replay";
			if (mode == "replay" && _context?.ActiveMap is { } map)
			{
				_replayBar.SetTitle(map.Name);
				_replayBar.SetProgress(SimulateReplayProgress());
			}
		}
	}

	// v0.1 简化：用 Time.GetTicksMsec / 60000 取模当作 % 进度
	// M3+ 时间轴实装后改用真实 TimeBar 位置
	private float SimulateReplayProgress()
	{
		return (Time.GetTicksMsec() % 60_000) / 60_000f;
	}

	private void UpdateMapTabBar()
	{
		if (_mapTabBar is null || _context is null) return;
		var tabs = new List<JrpgMapTabBar.Tab>();
		foreach (var map in _context.Maps)
		{
			tabs.Add(new JrpgMapTabBar.Tab(map.Id, map.Name));
		}
		_mapTabBar.SetTabs(tabs, _context.ActiveMapId);

		// §4.9.7 比例尺：按 GridSpec.CellDistance 换算"每格多少米"再格式化为距离文字
		var activeMap = _context.ActiveMap;
		if (activeMap is not null && _scaleBar is not null)
		{
			var cd = activeMap.Grid.CellDistance;
			// 横线 60px = cd×2 格（按 v0.1 简化：固定 2 格），文字取整为 1 位小数
			var text = FormatDistanceText(cd * 2, activeMap.Grid.DistanceUnit);
			_scaleBar.SetDistance(text);
		}
	}

	private static string FormatDistanceText(double distance, string unit) =>
		distance >= 1000.0
			? $"{distance / 1000.0:0.#} km"
			: $"{distance:0.#} {unit}";

	private void OnMouseMoved(Vector2I cell)
	{
		// §4.9.7 坐标显示：跟随鼠标更新
		_scaleBar?.SetCell(cell.X, cell.Y);
	}

	private void UpdateUndoButtons(bool force)
	{
		var stack = _context?.UndoStack;
		var canUndo = stack?.CanUndo ?? false;
		var canRedo = stack?.CanRedo ?? false;
		if (!force && canUndo == _lastCanUndo && canRedo == _lastCanRedo)
		{
			return;
		}

		_lastCanUndo = canUndo;
		_lastCanRedo = canRedo;
		if (_undoBtn is not null) _undoBtn.Disabled = !canUndo;
		if (_redoBtn is not null) _redoBtn.Disabled = !canRedo;
	}

	private static string FormatBreadcrumbs(EditorContext ctx)
	{
		// 2026-10-09 收敛：面包屑改用 JrpgBreadcrumb 组件，文本拼接走 SetCrumbs；本方法保留签名以防外部调用，未在调用方使用。
		var parts = new List<string>();
		foreach (var id in ctx.Breadcrumbs) parts.Add(ctx.DisplayNameOf(id));
		parts.Add(ctx.DisplayNameOf(ctx.ActiveMapId));
		return string.Join(" / ", parts);
	}

	// ------------------------------------------------------------------ 槽

	private void OnOpenPressed() => _fileDialog?.Popup(new Rect2I(8, 8, 720, 480));

	private void OnSavePressed() => _context?.SaveProject();

	private void OnUndoPressed() => _context?.UndoStack?.Undo();

	private void OnRedoPressed() => _context?.UndoStack?.Redo();

	private void OnFullscreenPressed() => DockLayout.Instance?.ToggleFullscreen();

	// ------------------------------------------------------------------ 快捷键

	public override void _Input(InputEvent @event)
	{
		// D4：spec 要求 Ctrl+Shift+Z 重做。既有的 WorldCanvas.HandleKey 只实现
		// Ctrl+Z（撤销）与 Ctrl+Y（重做），而 Ctrl+Shift+Z 会落进它的 Ctrl+Z 分支被当成撤销；
		// WorldCanvas._UnhandledInput 又早于本节点的 _UnhandledInput 执行，无法在那之后纠正。
		// 因此必须在这里（_Input 早于 _UnhandledInput）截获并标记已处理。
		// 不修改 scripts/Render/** —— spec §6 限制。
		if (@event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}

		if (!key.CtrlPressed || !key.ShiftPressed || key.Keycode != Key.Z)
		{
			return;
		}

		var context = _context ?? EditorContext.Instance;

		if (context is null)
		{
			return;
		}

		context.NotifyHint(context.UndoStack.Redo() ? "已重做" : "没有可重做的操作");
		GetViewport().SetInputAsHandled();
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
			case Key.M when key.CtrlPressed:
				OnMapSettingsPressed();
				GetViewport().SetInputAsHandled();
				break;
			case Key.F11:
				DockLayout.Instance?.ToggleFullscreen();
				GetViewport().SetInputAsHandled();
				break;
		}
	}
}
