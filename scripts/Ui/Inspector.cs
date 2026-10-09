using Godot;
using WorldView.Editor;
using WorldView.Ui.Dock;
using WorldView.Ui.Theme;

namespace WorldView.Ui;

/// <summary>
/// 检视器面板（spec §3.6 / tasks.md T3）：本轮默认**浮动**在右侧、默认不可见
/// （<c>kind: float</c>，<c>visible: false</c>），由 <see cref="DockLayout"/> 负责浮动与持久化。
///
/// <para>内容是被动只读的地图摘要；<see cref="EditorContext"/> 在 EditorController 的
/// <c>_Ready</c> 里才创建，而 Godot 的子节点 <c>_Ready</c> 早于父节点，因此这里用
/// 「<c>_Process</c> 轮询直到 Instance 非空再订阅信号」的延迟绑定范式。</para>
/// </summary>
public partial class Inspector : DockPanel
{
    private VBoxContainer? _body;
    private EditorContext? _context;
    private bool _bound;
    private bool _refreshQueued;

    public override void _Ready()
    {
        // 必须在 base._Ready() 之前定好 PanelId：base 里会立刻用它向 DockLayout 注册。
        if (string.IsNullOrEmpty(PanelId) || PanelId == "panel")
        {
            PanelId = "inspector";
        }

        if (string.IsNullOrEmpty(PanelTitle))
        {
            PanelTitle = "检视器";
        }

        base._Ready();

        _body = new VBoxContainer { Name = "Body" };
        _body.AddThemeConstantOverride("separation", 4);
        SetContent(_body);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_bound)
        {
            return;
        }

        if (EditorContext.Instance is not { } context)
        {
            return;
        }

        _bound = true;
        _context = context;
        context.MapChanged += QueueRefresh;
        context.LayersChanged += QueueRefresh;
        Refresh();
        SetProcess(false);
    }

    /// <summary>同一帧内的多次信号只触发一次重建（否则条目会成倍重复）。</summary>
    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Callable.From(() =>
        {
            _refreshQueued = false;
            Refresh();
        }).CallDeferred();
    }

    private void Refresh()
    {
        if (_body is null)
        {
            return;
        }

        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        var map = _context?.ActiveMap;
        if (map is null)
        {
            _body.AddChild(MakeRow("（未加载地图）", dim: true));
            return;
        }

        _body.AddChild(MakeRow($"地图：{map.Name}"));
        _body.AddChild(MakeRow($"ID：{map.Id}"));
        _body.AddChild(MakeRow($"类型：{map.Type}"));
        _body.AddChild(MakeRow($"网格：{map.Grid.Width} × {map.Grid.Height}"));
        _body.AddChild(MakeRow($"单格距离：{map.Grid.CellDistance} {map.Grid.DistanceUnit}"));
        _body.AddChild(MakeRow($"图层：{map.Layers.Count}"));
        _body.AddChild(MakeRow($"建筑：{map.Buildings.Count}"));
        _body.AddChild(MakeRow($"城市引用：{map.CityRefs.Count}"));
    }

    private static Label MakeRow(string text, bool dim = false)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", dim
            ? JrpgStyleFactory.JrpgTextPlaceholder
            : JrpgStyleFactory.JrpgText);
        return label;
    }
}
