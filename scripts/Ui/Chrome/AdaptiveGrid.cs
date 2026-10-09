using System;
using System.Collections.Generic;
using Godot;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 自适应网格（spec §3.5.1 / tasks.md T12）。
///
/// <para>列数 / 行数由可用宽高实时推导；**只在 cols/rows 真正变化时**重建（100ms 防抖），
/// 因此像素级的窗口 resize 不会造成反复重建。分页由本类统一管理，调用方只提供条目。</para>
///
/// <para>六条硬约束的实现位置：
/// ① 首次 Reflow 走 <c>CallDeferred</c>（<c>_Ready</c> 时 Size 为 0）；
/// ② 仅 cols/rows 变化才 <see cref="Rebuild"/> + 100ms 防抖；
/// ③ 先 <c>RemoveChild</c> 再 <c>QueueFree</c>；
/// ④ 每格 <c>CustomMinimumSize = (CellSize, CellSize)</c>；
/// ⑤ <c>PageCount &lt;= 1</c> 时由调用方隐藏分页器（本类暴露 <see cref="PageCount"/>）；
/// ⑥ 页码由调用方按分类保存并在切回时还原（本类只提供 <see cref="SetPage"/>）。</para>
/// </summary>
public partial class AdaptiveGrid : Control
{
    /// <summary>单格边长（spec §3.5：默认 56）。</summary>
    [Export]
    public int CellSize { get; set; } = 56;

    /// <summary>格间距。</summary>
    [Export]
    public int CellGap { get; set; } = 4;

    private readonly List<Control> _items = new();
    private GridContainer? _grid;
    private Godot.Timer? _debounce;
    private int _cols;
    private int _rows;
    private int _page;

    /// <summary>每页格数（cols × rows）。</summary>
    public int ItemsPerPage { get; private set; } = 1;

    /// <summary>总页数（至少 1）。</summary>
    public int PageCount => Mathf.Max(1, Mathf.CeilToInt(_items.Count / (float)Mathf.Max(1, ItemsPerPage)));

    /// <summary>当前页码（0 基）。</summary>
    public int Page => _page;

    /// <summary>当前列数（未完成首次 Reflow 时为 0）。</summary>
    public int Columns => _cols;

    /// <summary>当前行数。</summary>
    public int Rows => _rows;

    /// <summary>页码 / 页数变化。</summary>
    [Signal]
    public delegate void PageChangedEventHandler();

    /// <summary>列数或行数变化（调用方据此刷新分页器）。</summary>
    [Signal]
    public delegate void ReflowedEventHandler();

    public override void _Ready()
    {
        _grid = new GridContainer { Name = "Cells", Columns = 1 };
        _grid.AddThemeConstantOverride("h_separation", CellGap);
        _grid.AddThemeConstantOverride("v_separation", CellGap);
        AddChild(_grid);
        _grid.SetAnchorsPreset(LayoutPreset.TopLeft);

        Resized += OnResized;

        // 硬约束 1：_Ready 时 Size 为 0，必须延后一帧再算。
        Callable.From(ReflowNow).CallDeferred();
    }

    /// <summary>替换全部条目（会释放旧条目）。</summary>
    public void SetItems(IEnumerable<Control> items)
    {
        foreach (var item in _items)
        {
            if (item is null || !IsInstanceValid(item) || item.IsQueuedForDeletion())
            {
                continue;
            }

            // 只有「当前挂在本网格下」的条目才需要先摘除：非当前页的条目从未 AddChild 过，
            // 对非子节点调用 RemoveChild 会触发引擎报错
            // ERROR: Condition "p_child->data.parent != this" is true. (scene/main/node.cpp:1751)。
            if (_grid is not null && item.GetParent() == _grid)
            {
                _grid.RemoveChild(item);
            }

            item.QueueFree();
        }

        _items.Clear();
        _items.AddRange(items);
        _page = 0;

        // 必须 force：条目换了但行列可能没变（例如布局先跑过一次 Reflow 才 SetItems），
        // 若走 Reflow 的「行列未变即 return」短路，网格会一直空着。
        Reflow(Size, force: true);
    }

    /// <summary>当前页可见的条目（已挂进网格）。</summary>
    public IEnumerable<Control> VisibleItems
    {
        get
        {
            if (_grid is null)
            {
                yield break;
            }

            foreach (var child in _grid.GetChildren())
            {
                if (child is Control control)
                {
                    yield return control;
                }
            }
        }
    }

    /// <summary>按可用尺寸重算行列（spec §3.5.1 算法本体）。</summary>
    /// <param name="avail">可用尺寸（通常是本控件的 <c>Size</c>）。</param>
    /// <param name="force">行列未变也强制重建（条目集合变化时用）。</param>
    public void Reflow(Vector2 avail, bool force = false)
    {
        var cell = Mathf.Max(1, CellSize);
        var gap = Mathf.Max(0, CellGap);

        var cols = Mathf.Max(1, Mathf.FloorToInt((avail.X + gap) / (float)(cell + gap)));
        var rows = Mathf.Max(1, Mathf.FloorToInt((avail.Y + gap) / (float)(cell + gap)));

        // 只在真正变化时重建，避免像素级 resize 抖动（硬约束 2）。
        if (!force && cols == _cols && rows == _rows)
        {
            return;
        }

        _cols = cols;
        _rows = rows;
        ItemsPerPage = cols * rows;

        if (_grid is not null)
        {
            _grid.Columns = cols;
        }

        _page = Mathf.Clamp(_page, 0, Mathf.Max(0, PageCount - 1));
        Rebuild();
        EmitSignal(SignalName.Reflowed);
        EmitSignal(SignalName.PageChanged);
    }

    /// <summary>切到指定页（越界自动 clamp）。</summary>
    public void SetPage(int page)
    {
        var clamped = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount - 1));
        if (clamped == _page)
        {
            return;
        }

        _page = clamped;
        Rebuild();
        EmitSignal(SignalName.PageChanged);
    }

    /// <summary>下一页。</summary>
    public void NextPage() => SetPage(_page + 1);

    /// <summary>上一页。</summary>
    public void PrevPage() => SetPage(_page - 1);

    // ------------------------------------------------------------------ 内部

    private void OnResized()
    {
        if (_cols == 0)
        {
            Reflow(Size);
            return;
        }

        // 硬约束 2：100ms 防抖，只有 cols/rows 变化时才会真正重建。
        _debounce ??= CreateDebounce();
        _debounce.Start();
    }

    private Godot.Timer CreateDebounce()
    {
        var timer = new Godot.Timer
        {
            Name = "ReflowDebounce",
            OneShot = true,
            WaitTime = 0.1,
            Autostart = false,
        };
        AddChild(timer);
        timer.Timeout += ReflowNow;
        return timer;
    }

    private void ReflowNow()
    {
        Reflow(Size);
    }

    private void Rebuild()
    {
        if (_grid is null)
        {
            return;
        }

        var start = _page * ItemsPerPage;
        var end = Mathf.Min(_items.Count, start + ItemsPerPage);

        // 本页要展示的条目：先算出来，避免下面边摘边挂时漏项。
        var page = new List<Control>();
        for (var i = start; i < end; i++)
        {
            var item = _items[i];
            if (item is not null && IsInstanceValid(item) && !item.IsQueuedForDeletion())
            {
                page.Add(item);
            }
        }

        // 硬约束 3：先 RemoveChild 再 QueueFree，避免一帧残影。
        // 关键：**只有本页不再展示的旧条目**才能 QueueFree —— 仍在 page 里的条目只是被摘下，
        // 随后立刻重新挂回。若对它们也 QueueFree，它们会在帧末被真正释放，
        // 于是 ItemsPerPage/列数变化后的每一次 Rebuild 都无条目可挂，网格会永久空白。
        var keep = new HashSet<Control>(page);
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);

            if (child is Control control && keep.Contains(control))
            {
                continue;
            }

            child.QueueFree();
        }

        foreach (var item in page)
        {
            // 硬约束 4：GridContainer 会拉伸子节点，必须钉住格子尺寸。
            item.CustomMinimumSize = new Vector2(CellSize, CellSize);
            _grid.AddChild(item);
        }
    }
}
