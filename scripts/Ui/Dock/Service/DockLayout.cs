using System;
using System.Collections.Generic;
using Godot;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Dock.Service;

/// <summary>
/// 停靠布局根（spec §4.1 面板拖放 / §5 布局持久化 / tasks.md T1 + T4 + T6 + T7）。
///
/// <para>持有主场景里已存在的容器（<c>Root/TopStrip</c>、<c>Root/BottomPalette</c>、
/// 左右 / 上 dock），并由代码创建 5 个 <see cref="DockZone"/>（正常隐藏，
/// 只在面板拖放进行中亮起，否则会吞掉画布的鼠标事件）。</para>
///
/// <para><b>不依赖 <c>WorldView.Ui.Palette</c></b>：底部容器通过
/// <see cref="IPaletteHost"/> 接口交互，避免反向命名空间依赖。</para>
/// </summary>
public partial class DockLayout : Control, IDockHost, IPanelRegistry
{
    /// <summary>全局单例。必须在 <c>_EnterTree</c> 赋值（父先于子），
    /// 否则子面板在 <c>_Ready</c> 里注册时拿不到实例。</summary>
    public static DockLayout? Instance { get; private set; }

    /// <summary>布局落盘防抖时长（spec §5.2 写时机①）。</summary>
    private const double SaveDebounceSeconds = 0.5;

    private readonly Dictionary<string, DockPanel> _panels = new();
    private readonly Dictionary<string, FloatWindow> _floats = new();
    private readonly List<DockZone> _zones = new();
    private readonly Dictionary<string, bool> _fullscreenSnapshot = new();

    private Control? _topStrip;
    private Control? _bottomPalette;
    private VBoxContainer? _topDock;
    private VBoxContainer? _leftDock;
    private VBoxContainer? _rightDock;
    private Control? _zonesLayer;
    private Godot.Timer? _saveTimer;

    private string _projectRoot = string.Empty;
    private bool _layoutLoaded;
    private bool _layoutDirty;
    private bool _applying;
    private bool _fullscreen;

    /// <summary>当前是否处于全屏地图模式。</summary>
    public bool Fullscreen => _fullscreen;

    /// <summary>当前布局文件所属工程根目录（未加载时为 <c>string.Empty</c>）。</summary>
    public string ProjectRoot => _projectRoot;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        // spec §5.2 写时机②：退出时**同步**立即刷盘（禁止 await）。
        FlushLayoutNow();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Ready()
    {
        // 本节点自身不接收鼠标，避免盖住画布；子容器各自决定。
        MouseFilter = MouseFilterEnum.Ignore;

        _topStrip = GetNodeOrNull<Control>("Root/TopStrip");
        _bottomPalette = GetNodeOrNull<Control>("Root/BottomPalette");
        _topDock = GetNodeOrNull<VBoxContainer>("Root/TopDock");
        _leftDock = GetNodeOrNull<VBoxContainer>("Root/MidRow/LeftDock");
        _rightDock = GetNodeOrNull<VBoxContainer>("Root/MidRow/RightDock");

        BuildZones();

        _saveTimer = new Godot.Timer
        {
            Name = "LayoutSaveTimer",
            OneShot = true,
            WaitTime = SaveDebounceSeconds,
            Autostart = false,
        };
        AddChild(_saveTimer);
        _saveTimer.Timeout += FlushLayoutNow;

        Resized += LayoutZones;
        LayoutZones();

        // 布局还原必须延后：示例工程在 EditorController._Ready 里加载，
        // 而 EditorController 是场景根 → 它的 _Ready 晚于本节点的 _Ready。
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_layoutLoaded)
        {
            SetProcess(false);
            return;
        }

        if (Editor.EditorContext.Instance is not { } context || string.IsNullOrEmpty(context.ProjectRoot))
        {
            return;
        }

        _projectRoot = context.ProjectRoot;
        _layoutLoaded = true;
        ApplyLayout(LayoutState.Load(_projectRoot));
        SetProcess(false);
    }

    // ------------------------------------------------------------------ 面板登记

    /// <summary>面板在 <c>_Ready</c> 里登记自己（稳定 ID 跨会话复用，保留旧 API）。</summary>
    public void RegisterPanel(DockPanel panel)
    {
        if (panel is null || !IsInstanceValid(panel) || string.IsNullOrEmpty(panel.PanelId))
        {
            return;
        }

        _panels[panel.PanelId] = panel;
    }

    /// <summary>按 ID 取面板（不存在返回 null）。</summary>
    public DockPanel? Panel(string panelId)
        => _panels.TryGetValue(panelId, out var panel) && IsInstanceValid(panel) ? panel : null;

    // ---- IPanelRegistry seam (C 轮 Task 2) -------------------------------
    // 替代 Leaf 用 DockLayout.Instance?.RegisterPanel(this) 的反环访问。
    // DockPanel 子类（Inspector / LayerPanel / WorldDropSurface）只 using IPanelRegistry
    // 接口，不直接 using DockLayout 类。

    void IPanelRegistry.Register(DockPanel panel) => RegisterPanel(panel);

    void IPanelRegistry.Unregister(DockPanel panel)
    {
        // 当前 DockLayout 没有 unregister 动作；保留为显式 seam hook，后续按需扩展。
        _ = panel;
    }

    DockPanel? IPanelRegistry.Find(string panelId) => Panel(panelId);

    /// <summary>面板当前所在的停靠区：<c>left</c>/<c>right</c>/<c>top</c>/<c>bottom</c>/<c>center</c>。</summary>
    public string ZoneOf(Node? panel)
    {
        for (var node = panel?.GetParent(); node is not null; node = node.GetParent())
        {
            if (node is FloatWindow)
            {
                return "center";
            }

            if (node == _leftDock)
            {
                return "left";
            }

            if (node == _rightDock)
            {
                return "right";
            }

            if (node == _topDock)
            {
                return "top";
            }

            if (node == _bottomPalette)
            {
                return "bottom";
            }

            if (ReferenceEquals(node, this))
            {
                return "center";
            }
        }

        return string.Empty;
    }

    // ------------------------------------------------------------------ 停靠区

    private void BuildZones()
    {
        _zonesLayer = new Control
        {
            Name = "Zones",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_zonesLayer);
        _zonesLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        AddZone("left");
        AddZone("right");
        AddZone("top");
        AddZone("bottom");
        AddZone("center");
    }

    private void AddZone(string zoneId)
    {
        var zone = new DockZone
        {
            Name = "Zone_" + zoneId,
            ZoneId = zoneId,
            Edge = zoneId,
        };
        _zonesLayer!.AddChild(zone);
        _zones.Add(zone);
    }

    /// <summary>5 个互不重叠的停靠矩形（按当前 TopStrip / BottomPalette 实际高度算）。</summary>
    private void LayoutZones()
    {
        var total = Size;
        if (total.X < 1f || total.Y < 1f)
        {
            return;
        }

        var topHeight = _topStrip is { Visible: true } ? Mathf.Max(24f, _topStrip.Size.Y) : 0f;
        var bottomHeight = _bottomPalette is { Visible: true } ? Mathf.Max(24f, _bottomPalette.Size.Y) : 0f;
        var band = 64f;
        var sideWidth = Mathf.Clamp(total.X * 0.10f, 88f, 180f);
        var midTop = topHeight + band;
        var midBottom = total.Y - bottomHeight - band;
        var midHeight = Mathf.Max(64f, midBottom - midTop);

        foreach (var zone in _zones)
        {
            var rect = zone.ZoneId switch
            {
                "top" => new Rect2(0f, 0f, total.X, topHeight + band),
                "bottom" => new Rect2(0f, total.Y - bottomHeight - band, total.X, bottomHeight + band),
                "left" => new Rect2(0f, midTop, sideWidth, midHeight),
                "right" => new Rect2(total.X - sideWidth, midTop, sideWidth, midHeight),
                _ => new Rect2(sideWidth, midTop, Mathf.Max(64f, total.X - sideWidth * 2f), midHeight),
            };

            zone.Position = rect.Position;
            zone.Size = rect.Size;
        }
    }

    /// <summary>面板拖放开始：亮起全部停靠区。</summary>
    public void NotifyPanelDragBegin(DockPanel panel)
    {
        LayoutZones();
        var sourceZone = ZoneOf(panel);
        foreach (var zone in _zones)
        {
            zone.SetActive(true, highlighted: zone.ZoneId == sourceZone);
        }
    }

    /// <summary>面板拖放结束（含取消）。</summary>
    public void NotifyPanelDragEnd()
    {
        foreach (var zone in _zones)
        {
            zone.SetActive(false);
        }
    }

    // ------------------------------------------------------------------ 落点处理

    /// <summary>停靠区收到面板：<paramref name="edge"/> 决定去向（spec §4.1）。</summary>
    public void HandlePanelDrop(string edge, string panelId, Vector2 atPosition)
    {
        if (!_panels.TryGetValue(panelId, out var panel) || !IsInstanceValid(panel))
        {
            return;
        }

        switch (edge)
        {
            case "left":
                DockInto(_leftDock, panel, new Vector2(280f, 0f));
                break;
            case "right":
                DockInto(_rightDock, panel, new Vector2(280f, 0f));
                break;
            case "top":
                DockInto(_topDock, panel, new Vector2(0f, 168f));
                break;
            case "bottom":
                if (_bottomPalette is IPaletteHost host)
                {
                    host.AcceptPanel(panel, atPosition);
                }

                break;
            default:
                // center = 浮动
                FloatPanel(panel, atPosition);
                break;
        }

        MarkLayoutChanged();
    }

    private void DockInto(BoxContainer? dock, DockPanel panel, Vector2 minimumSize)
    {
        if (dock is null || !IsInstanceValid(panel))
        {
            return;
        }

        MoveOutOfFloatWindow(panel);

        var parent = panel.GetParent();
        if (parent is null)
        {
            dock.AddChild(panel);
        }
        else if (parent != dock)
        {
            panel.Reparent(dock, keepGlobalTransform: false);
        }

        panel.Compact = false;
        panel.Visible = true;
        panel.RestoreAfterDrag();

        // 停靠进 BoxContainer 时必须显式撑满：容器只把富余空间分给带 Expand 的子节点，
        // 否则面板会按 CustomMinimumSize(0,0) 被压成 0 高（实测 inspector 停靠左侧后 size=(280,0)）。
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        dock.Visible = true;
        dock.CustomMinimumSize = new Vector2(
            Mathf.Max(dock.CustomMinimumSize.X, minimumSize.X),
            Mathf.Max(dock.CustomMinimumSize.Y, minimumSize.Y));
    }

    /// <summary>没有可见面板的停靠容器应收起，否则会留下一条 280px 空槽。</summary>
    private void RefreshEmptyDocks()
    {
        RefreshDock(_leftDock);
        RefreshDock(_rightDock);
        RefreshDock(_topDock);
    }

    private static void RefreshDock(BoxContainer? dock)
    {
        if (dock is null || !IsInstanceValid(dock))
        {
            return;
        }

        var any = false;
        foreach (var child in dock.GetChildren())
        {
            if (child is Control control && control.Visible)
            {
                any = true;
                break;
            }
        }

        dock.Visible = any;
    }

    /// <summary>把面板装进（必要时新建）浮动窗口。</summary>
    public void FloatPanel(DockPanel panel, Vector2 dropPosition, Vector2? size = null, Vector2? position = null)
    {
        if (panel is null || !IsInstanceValid(panel))
        {
            return;
        }

        // 面板尚未被摆放时是 DockLayout 的 full-rect 子节点，Size 等于整个视口；
        // 直接拿它当浮窗尺寸会得到一个占满屏幕的"浮窗"。只在尺寸像一块面板时才沿用。
        var plausible = panel.Size.X is >= 160f and <= 720f && panel.Size.Y is >= 120f and <= 720f;
        var targetSize = size ?? (plausible ? panel.Size : new Vector2(280f, 340f));
        var targetPosition = position ?? (dropPosition - new Vector2(12f, 8f));

        if (!_floats.TryGetValue(panel.PanelId, out var window) || !IsInstanceValid(window))
        {
            window = new FloatWindow { Name = "Float_" + panel.PanelId };
            AddChild(window);
            _floats[panel.PanelId] = window;
        }

        window.Present(panel, targetPosition, targetSize);

        // 面板从停靠槽挪到浮窗后，空掉的槽要收起（否则会留下 280px 空条）。
        RefreshEmptyDocks();
        LayoutZones();
        MarkLayoutChanged();
    }

    private void MoveOutOfFloatWindow(DockPanel panel)
    {
        if (panel.GetParent() is FloatWindow window)
        {
            window.Detach();
        }
    }

    /// <summary>面板被关闭：隐藏面板与其浮窗（spec §3.7 的 <c>x</c> 按钮）。</summary>
    public void OnPanelClosed(string panelId)
    {
        if (_panels.TryGetValue(panelId, out var panel) && IsInstanceValid(panel))
        {
            panel.Visible = false;
        }

        if (_floats.TryGetValue(panelId, out var window) && IsInstanceValid(window))
        {
            window.Visible = false;
        }

        RefreshEmptyDocks();
        LayoutZones();
        MarkLayoutChanged();
    }

    /// <summary>显示 / 隐藏（必要时浮动）某个面板。</summary>
    public void ShowFloat(string panelId, bool visible, Vector2? position = null, Vector2? size = null)
    {
        if (!_panels.TryGetValue(panelId, out var panel) || !IsInstanceValid(panel))
        {
            return;
        }

        if (visible)
        {
            if (!_floats.TryGetValue(panelId, out var existing) || !IsInstanceValid(existing))
            {
                FloatPanel(panel, position ?? new Vector2(1620f, 80f), size, position ?? new Vector2(1620f, 80f));
            }

            panel.Visible = true;
            if (_floats.TryGetValue(panelId, out var window) && IsInstanceValid(window))
            {
                window.Visible = true;
            }
        }
        else
        {
            panel.Visible = false;
            if (_floats.TryGetValue(panelId, out var window) && IsInstanceValid(window))
            {
                window.Visible = false;
            }
        }

        MarkLayoutChanged();
    }

    /// <summary>面板是否可见。</summary>
    public bool IsPanelVisible(string panelId)
        => _panels.TryGetValue(panelId, out var panel) && IsInstanceValid(panel) && panel.Visible;

    /// <summary>切换浮动面板显隐。</summary>
    public void ToggleFloat(string panelId) => ShowFloat(panelId, !IsPanelVisible(panelId));

    // ------------------------------------------------------------------ 全屏（spec T7）

    /// <summary>显示 / 隐藏全部已登记面板（含浮窗）。</summary>
    public void SetAllPanelsVisible(bool visible)
    {
        foreach (var (id, panel) in _panels)
        {
            if (!IsInstanceValid(panel))
            {
                continue;
            }

            panel.Visible = visible;
            if (_floats.TryGetValue(id, out var window) && IsInstanceValid(window))
            {
                window.Visible = visible;
            }
        }

        MarkLayoutChanged();
    }

    /// <summary>F11：100% 视口（隐藏 TopStrip / BottomPalette / 所有面板），退出时还原可见性快照。</summary>
    public void ToggleFullscreen() => SetFullscreen(!_fullscreen);

    public void SetFullscreen(bool on)
    {
        if (_fullscreen == on)
        {
            return;
        }

        _fullscreen = on;
        if (on)
        {
            // 进 F11 前把真实可见状态立即落盘（此时 chrome/panel 还是用户看到的样子）。
            // 否则进入全屏后才触发的防抖 flush 会把 `visible: false` 写进布局文件，
            // 下次启动还原时 TopStrip / BottomPalette 全部消失。
            FlushLayoutNow();

            _fullscreenSnapshot.Clear();
            foreach (var path in new[]
                     {
                         "Root/TopStrip", "Root/BottomPalette", "Root/TopDock",
                         "Root/MidRow/LeftDock", "Root/MidRow/RightDock",
                     })
            {
                if (GetNodeOrNull<Control>(path) is { } chrome)
                {
                    _fullscreenSnapshot[path] = chrome.Visible;
                    chrome.Visible = false;
                }
            }

            foreach (var (id, panel) in _panels)
            {
                if (!IsInstanceValid(panel))
                {
                    continue;
                }

                _fullscreenSnapshot["panel:" + id] = panel.Visible;
                panel.Visible = false;
                if (_floats.TryGetValue(id, out var window) && IsInstanceValid(window))
                {
                    window.Visible = false;
                }
            }
        }
        else
        {
            foreach (var (key, value) in _fullscreenSnapshot)
            {
                if (key.StartsWith("panel:", StringComparison.Ordinal))
                {
                    if (_panels.TryGetValue(key[6..], out var panel) && IsInstanceValid(panel))
                    {
                        panel.Visible = value;
                    }
                }
                else if (GetNodeOrNull<Control>(key) is { } chrome)
                {
                    chrome.Visible = value;
                }
            }

            _fullscreenSnapshot.Clear();

            foreach (var (id, window) in _floats)
            {
                if (_panels.TryGetValue(id, out var panel) && IsInstanceValid(panel) && panel.Visible && IsInstanceValid(window))
                {
                    window.Visible = true;
                }
            }
        }

        var host = GetWindow();
        if (host is not null && DisplayServer.GetName() != "headless")
        {
            host.Mode = on ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Windowed;
        }

        // 刻意【不】调 MarkLayoutChanged()：F11 的可见性隐藏是会话级状态，
        // 由 _fullscreenSnapshot 还原，不写入磁盘布局文件。若标脏，防抖 flush 会把
        // `visible: false` 落盘——下次启动布局还原时 BottomPalette / TopStrip 会消失。
        // 真正的持久布局变化（拖动面板、折叠、改高度）另有各自的 MarkLayoutChanged 调用。
    }

    // ------------------------------------------------------------------ 持久化（spec §5）

    /// <summary>布局变化：发信号 + 防抖落盘。</summary>
    public void MarkLayoutChanged()
    {
        if (!_applying)
        {
            Editor.EditorContext.Instance?.NotifyLayoutChanged();
        }

        if (_applying || !_layoutLoaded || string.IsNullOrEmpty(_projectRoot))
        {
            return;
        }

        _layoutDirty = true;
        _saveTimer?.Start();
    }

    /// <summary>按布局文档还原（TopStrip 高度 / BottomPalette 行节 / 浮动面板位置）。</summary>
    public void ApplyLayout(LayoutState? document)
    {
        if (document is null)
        {
            return;
        }

        _applying = true;
        try
        {
            if (document.Find("top_strip") is { } topStrip)
            {
                if (_topStrip is not null && IsInstanceValid(_topStrip))
                {
                    _topStrip.Visible = topStrip.Visible ?? true;
                    if (topStrip.Height is { } height and > 0)
                    {
                        _topStrip.CustomMinimumSize = new Vector2(_topStrip.CustomMinimumSize.X, height);
                    }
                }
            }

            if (document.Find("bottom_palette") is { } palette)
            {
                if (_bottomPalette is not null && IsInstanceValid(_bottomPalette))
                {
                    _bottomPalette.Visible = palette.Visible ?? true;
                }

                if (_bottomPalette is IPaletteHost host)
                {
                    host.ApplyLayout(palette);
                    host.SetCollapsed(palette.Collapsed ?? false);
                }
            }

            ApplyFloatState(document.Find("inspector"), "inspector");
            ApplyFloatState(document.Find("layer_panel"), "layer_panel");
            RefreshEmptyDocks();
            LayoutZones();
        }
        finally
        {
            _applying = false;
        }

        Editor.EditorContext.Instance?.NotifyLayoutChanged();
    }

    private void ApplyFloatState(PanelState? state, string panelId)
    {
        if (state is null)
        {
            return;
        }

        var position = new Vector2(state.X ?? 1620f, state.Y ?? 80f);
        var size = new Vector2(state.WidthPx ?? 280, state.Height ?? 320);
        ShowFloat(panelId, state.Visible ?? false, position, size);
    }

    /// <summary>从当前界面捕获一份完整布局文档（结构对齐 spec §5.2）。</summary>
    public LayoutState CaptureLayout()
    {
        var document = LayoutState.Default(_projectRoot);

        if (document.Find("top_strip") is { } topStrip && _topStrip is not null && IsInstanceValid(_topStrip))
        {
            topStrip.Visible = _topStrip.Visible;
            topStrip.Height = Mathf.RoundToInt(_topStrip.Size.Y);
        }

        if (document.Find("bottom_palette") is { } palette)
        {
            if (_bottomPalette is not null && IsInstanceValid(_bottomPalette))
            {
                palette.Visible = _bottomPalette.Visible;
            }

            if (_bottomPalette is IPaletteHost host)
            {
                palette.Collapsed = host.Collapsed;
                host.CaptureLayout(palette);
            }
        }

        CaptureFloatState(document.Find("inspector"), "inspector");
        CaptureFloatState(document.Find("layer_panel"), "layer_panel");
        return document;
    }

    private void CaptureFloatState(PanelState? state, string panelId)
    {
        if (state is null || !_panels.TryGetValue(panelId, out var panel) || !IsInstanceValid(panel))
        {
            return;
        }

        state.Visible = panel.Visible;
        if (_floats.TryGetValue(panelId, out var window) && IsInstanceValid(window))
        {
            state.X = Mathf.RoundToInt(window.Position.X);
            state.Y = Mathf.RoundToInt(window.Position.Y);
            state.Width = Mathf.RoundToInt(window.Size.X);
            state.Height = Mathf.RoundToInt(window.Size.Y);
        }
    }

    /// <summary>同步刷盘（只写脏数据）。</summary>
    public void FlushLayoutNow()
    {
        if (!_layoutDirty || !_layoutLoaded || string.IsNullOrEmpty(_projectRoot))
        {
            return;
        }

        _layoutDirty = false;
        CaptureLayout().Save(_projectRoot);
    }
}
