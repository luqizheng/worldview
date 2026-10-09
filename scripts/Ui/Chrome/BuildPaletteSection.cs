using System.Collections.Generic;
using Godot;
using WorldView.Domain;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 建筑面板（spec §3.5 / tasks.md T5 + T12）：BottomPalette <c>build_row</c> 的 section 1。
///
/// <para>顶部分类 <see cref="TabBar"/> → 中部 <see cref="AdaptiveGrid"/> → 右下分页器。</para>
///
/// <para><b>分类 ID 与建筑类型的映射是硬约束</b>：spec §5.2 的布局 YAML 已经钉死
/// <c>active_category</c> 与 <c>pages_by_category</c> 的六个键（residences / castles / religion /
/// military / nature / facilities），所以这里的分类 ID 必须与之一致，否则 D13 / D15 的
/// 「切回分类还原页码」会失效。而 <see cref="BuildingType"/> 在 MVP 只有
/// House / Tower / WallGate 三个值，因此六分类中有三个暂时为空（显示占位提示），
/// **不新造 Domain 枚举值**。</para>
/// </summary>
public partial class BuildPaletteSection : PaletteContent
{
    private sealed record Category(string Id, string Label, BuildingType[] Buildings);

    private static readonly Category[] Categories =
    {
        new("residences", "民居", new[] { BuildingType.House }),
        new("castles", "城堡", new[] { BuildingType.WallGate }),
        new("religion", "宗教", System.Array.Empty<BuildingType>()),
        new("military", "军事", new[] { BuildingType.Tower }),
        new("nature", "自然", System.Array.Empty<BuildingType>()),
        new("facilities", "设施", System.Array.Empty<BuildingType>()),
    };

    private static readonly Dictionary<BuildingType, string> BuildingNames = new()
    {
        [BuildingType.House] = "农舍",
        [BuildingType.Tower] = "箭塔",
        [BuildingType.WallGate] = "城门",
    };

    private readonly Dictionary<string, int> _pagesByCategory = new();

    private TabBar? _tabs;
    private AdaptiveGrid? _grid;
    private HBoxContainer? _pager;
    private Button? _prevButton;
    private Button? _nextButton;
    private Label? _pageLabel;
    private Label? _emptyHint;
    private string _activeCategory = Categories[0].Id;
    private bool _syncing;

    public override void _Ready()
    {
        base._Ready();

        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(180f, 72f);
        BuildUi();
        SelectCategory(_activeCategory);
    }

    // ------------------------------------------------------------------ 构建

    private void BuildUi()
    {
        var root = new VBoxContainer { Name = "Root" };
        root.AddThemeConstantOverride("separation", 2);
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 2f;
        root.OffsetTop = 1f;
        root.OffsetRight = -2f;
        root.OffsetBottom = -1f;

        _tabs = new TabBar
        {
            Name = "Tabs",
            ClipTabs = true,
            CustomMinimumSize = new Vector2(0f, 22f),
            TabAlignment = TabBar.AlignmentMode.Left,
        };
        _tabs.AddThemeFontSizeOverride("font_size", 11);
        foreach (var category in Categories)
        {
            _tabs.AddTab(category.Label);
        }

        _tabs.TabChanged += OnTabChanged;
        root.AddChild(_tabs);

        _grid = new AdaptiveGrid
        {
            Name = "Grid",
            CellSize = 56,
            CellGap = 4,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _grid.PageChanged += OnPageChanged;
        _grid.Reflowed += OnReflowed;
        root.AddChild(_grid);

        _emptyHint = new Label
        {
            Name = "EmptyHint",
            Text = "该分类暂无条目（建筑类型待扩充）",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _emptyHint.AddThemeFontSizeOverride("font_size", 11);
        _emptyHint.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
        root.AddChild(_emptyHint);

        _pager = new HBoxContainer
        {
            Name = "Pager",
            Alignment = BoxContainer.AlignmentMode.End,
            CustomMinimumSize = new Vector2(0f, 16f),
            Visible = false,
        };
        _pager.AddThemeConstantOverride("separation", 4);
        root.AddChild(_pager);

        _prevButton = MakePagerButton("◀", "上一页");
        _prevButton.Pressed += () => _grid?.PrevPage();
        _pager.AddChild(_prevButton);

        _pageLabel = new Label
        {
            Name = "PageLabel",
            Text = "1/1",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _pageLabel.AddThemeFontSizeOverride("font_size", 10);
        _pageLabel.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
        _pager.AddChild(_pageLabel);

        _nextButton = MakePagerButton("▶", "下一页");
        _nextButton.Pressed += () => _grid?.NextPage();
        _pager.AddChild(_nextButton);
    }

    private static Button MakePagerButton(string text, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(16f, 14f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontSizeOverride("font_size", 9);
        return button;
    }

    // ------------------------------------------------------------------ 分类 / 分页

    private void OnTabChanged(long tab)
    {
        if (_syncing || tab < 0 || tab >= Categories.Length)
        {
            return;
        }

        // 硬约束 6：切走前记下当前页码。
        _pagesByCategory[_activeCategory] = _grid?.Page ?? 0;
        SelectCategory(Categories[(int)tab].Id);
        MarkLayoutChanged();
    }

    private void SelectCategory(string categoryId)
    {
        var category = FindCategory(categoryId) ?? Categories[0];
        _activeCategory = category.Id;

        var index = System.Array.IndexOf(Categories, category);
        _syncing = true;
        if (_tabs is not null && index >= 0 && _tabs.CurrentTab != index)
        {
            _tabs.CurrentTab = index;
        }

        _syncing = false;

        var hasItems = category.Buildings.Length > 0;
        if (_grid is not null)
        {
            _grid.Visible = hasItems;
            if (hasItems)
            {
                _grid.SetItems(BuildItems(category));
                _grid.SetPage(_pagesByCategory.TryGetValue(category.Id, out var page) ? page : 0);
            }
        }

        if (_emptyHint is not null)
        {
            _emptyHint.Visible = !hasItems;
        }

        if (_pager is not null)
        {
            // 硬约束 5：PageCount <= 1 时整体隐藏分页器（不显示 "1/1"）。
            _pager.Visible = hasItems && (_grid?.PageCount ?? 1) > 1;
        }

        UpdatePageLabel();
    }

    private static Category? FindCategory(string id)
    {
        foreach (var category in Categories)
        {
            if (category.Id == id)
            {
                return category;
            }
        }

        return null;
    }

    private List<Control> BuildItems(Category category)
    {
        var items = new List<Control>(category.Buildings.Length);
        foreach (var building in category.Buildings)
        {
            var button = new BuildingButton
            {
                Name = "Building_" + building,
                Text = BuildingNames.TryGetValue(building, out var name) ? name : building.ToString(),
                BuildingType = building,
                TooltipText = $"拖动到画布放置（{(BuildingNames.TryGetValue(building, out var hint) ? hint : building.ToString())}）",
                ClipText = true,
                Alignment = HorizontalAlignment.Center,
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            items.Add(button);
        }

        return items;
    }

    private void OnPageChanged()
    {
        _pagesByCategory[_activeCategory] = _grid?.Page ?? 0;
        if (_pager is not null)
        {
            _pager.Visible = (_grid?.Visible ?? false) && (_grid?.PageCount ?? 1) > 1;
        }

        UpdatePageLabel();
        MarkLayoutChanged();
    }

    private void OnReflowed()
    {
        if (_pager is not null)
        {
            _pager.Visible = (_grid?.Visible ?? false) && (_grid?.PageCount ?? 1) > 1;
        }

        UpdatePageLabel();
    }

    private void UpdatePageLabel()
    {
        if (_pageLabel is null || _grid is null)
        {
            return;
        }

        _pageLabel.Text = $"{_grid.Page + 1}/{_grid.PageCount}";
    }

    // ------------------------------------------------------------------ 布局状态

    public override void ApplyState(PanelState? state)
    {
        if (state is null || _grid is null)
        {
            return;
        }

        if (state.CellSize is { } cellSize and >= 16)
        {
            _grid.CellSize = cellSize;
        }

        if (state.CellGap is { } cellGap and >= 0)
        {
            _grid.CellGap = cellGap;
        }

        _pagesByCategory.Clear();
        if (state.PagesByCategory is { } pages)
        {
            foreach (var pair in pages)
            {
                _pagesByCategory[pair.Key] = pair.Value;
            }
        }

        SelectCategory(state.ActiveCategory ?? Categories[0].Id);
    }

    public override void CaptureState(PanelState state)
    {
        if (_grid is null)
        {
            return;
        }

        state.CellSize = _grid.CellSize;
        state.CellGap = _grid.CellGap;
        state.ActiveCategory = _activeCategory;
        state.PagesByCategory = new Dictionary<string, int>(_pagesByCategory);
    }
}
