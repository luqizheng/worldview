using Godot;
using WorldView.Editor;
using WorldView.Ui.Components;
using WorldView.Ui.Dock;
using WorldView.Ui.Theme;

namespace WorldView.Ui;

/// <summary>
/// 图层面板（spec §3.6 / tasks.md T3）：与 <see cref="Inspector"/> 一样，本轮默认**浮动**、
/// 默认不可见，由 <see cref="DockLayout"/> 负责浮动与持久化。
///
/// <para>图层可见性写在文档里（<c>LayerDoc.Visible</c>），走
/// <see cref="EditorContext.SetLayerVisible"/>，因此不受「UI 私有状态不落盘」约束。</para>
/// </summary>
public partial class LayerPanel : DockPanel
{
    private VBoxContainer? _body;
    private EditorContext? _context;
    private bool _bound;
    private bool _syncing;
    private bool _refreshQueued;

    public override void _Ready()
    {
        // 必须在 base._Ready() 之前定好 PanelId：base 里会立刻用它向 DockLayout 注册。
        if (string.IsNullOrEmpty(PanelId) || PanelId == "panel")
        {
            PanelId = "layer_panel";
        }

        if (string.IsNullOrEmpty(PanelTitle))
        {
            PanelTitle = "图层";
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
        context.LayersChanged += QueueRefresh;
        context.MapChanged += QueueRefresh;
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

        _syncing = true;
        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        if (_context is null || _context.Layers.Count == 0)
        {
            var empty = new Label
            {
                Text = "（无图层）",
                MouseFilter = MouseFilterEnum.Ignore,
            };
            empty.AddThemeFontSizeOverride("font_size", 11);
            empty.AddThemeColorOverride("font_color", JrpgStyleFactory.JrpgTextPlaceholder);
            _body.AddChild(empty);
            _syncing = false;
            return;
        }

        // ProjectLoader 会按「每张地图的 layers[] 引用」累积 LayerDoc，同一批图层文件被多张地图引用时
        // EditorContext.Layers 里会出现 Id 相同的重复项（示例世界 4 张地图 → 4 份重复）。
        // 这里按 Id 去重展示，与 EditorContext.FindLayer/SetLayerVisible 的「按 Id 命中第一项」语义一致。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layer in _context.Layers)
        {
            if (string.IsNullOrEmpty(layer.Id) || !seen.Add(layer.Id))
            {
                continue;
            }

            var check = new JrpgCheckBox
            {
                Text = $"{layer.Name} ({layer.Type})",
                ButtonPressed = layer.Visible,
            };
            check.AddThemeFontSizeOverride("font_size", 11);
            var id = layer.Id;
            check.Toggled += pressed =>
            {
                if (!_syncing)
                {
                    _context?.SetLayerVisible(id, pressed);
                }
            };
            _body.AddChild(check);
        }

        _syncing = false;
    }
}
