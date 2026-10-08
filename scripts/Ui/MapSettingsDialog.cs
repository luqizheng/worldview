using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldView.Domain;
using WorldView.Domain.Generation;
using WorldView.Editing;
using WorldView.Editor;
using WorldView.Render;
using WorldView.Ui.Palette;

namespace WorldView.Ui;

/// <summary>
/// 地图设置对话框（plan L402-429 / spec 1 §3–§5）。两态：
/// <list type="bullet">
///   <item><b>新建世界</b>：物理尺寸 + 分辨率 + 生成参数 + 缩略图预览 + 细化统计，<c>[创建]</c> 走
///     <see cref="WorldGenerator.Generate"/> → <see cref="EditorContext.AddMap"/> → <see cref="EditorContext.DrillTo"/>。</item>
///   <item><b>已存在地图</b>：只可改尺寸 / 分辨率；生成参数只读、掷骰禁用；<c>[应用]</c> 前用
///     <see cref="ResizeMapCommand.DescribeLoss"/> 弹确认，确认后推 <see cref="ResizeMapCommand"/>（因此可撤销）。</item>
/// </list>
///
/// <para>刻意不做的事（spec 1 §4.4 / §8 范围外）：不新增 <see cref="MapType"/> 取值、不做「新地图向导」、
/// 不自动生成 <c>scale_label</c>、不新增 <c>scripts/Render/**</c> 文件、**逐格 <c>SetTerrainCells</c> 一律禁用**
/// （越界格会被静默跳过，是数据丢失源）。</para>
/// </summary>
public partial class MapSettingsDialog : AcceptDialog
{
    // ──── 分辨率预设档（spec 1 §3.4） ────────────────────────────────────────
    // MVP 只有 World / City 两个 MapType 取值可选；其余档位（region km 1、street m 0.5/1、
    // building m 0.5/1、underground m 1/5）只作为预设**数据**存在，待 MapType 扩充后再暴露。
    private static readonly (MapType Type, string Unit, double[] Values)[] ResolutionPresets =
    {
        (MapType.World, "km", new[] { 10d, 25d, 50d }),
        (MapType.City, "m", new[] { 1d, 2d, 5d, 10d }),
    };

    /// <summary>预设档末项固定为「自定义…」；只有选中它才允许手输任意小数（spec 1 §3.4）。</summary>
    private const string CustomOptionLabel = "自定义…";

    /// <summary>预览用的格数上限：超过则按比例降采样生成（并在统计行注明），避免一次预览跑满 512×512 的约 0.5s。</summary>
    private const int PreviewCellCap = 192;

    private EditorContext? _context;
    private MapDoc? _existing;

    private VBoxContainer? _genBox;
    private SpinBox? _lenSpin;
    private SpinBox? _widSpin;
    private Label? _sizeUnitLabel;
    private OptionButton? _resPreset;
    private SpinBox? _resCustom;
    private OptionButton? _tileStyle;
    private Label? _coverageLabel;
    private SpinBox? _landSpin;
    private SpinBox? _ruggedSpin;
    private SpinBox? _seedSpin;
    private Button? _rerollBtn;
    private TextureRect? _preview;
    private Label? _statsLabel;
    private Label? _warnLabel;
    private Label? _titleLabel;
    private Button? _newWorldBtn;

    private Texture2D? _previewTexture;
    private GenerationSpec? _lastSpec;
    private RefineStats? _lastStats;
    private bool _previewDownscaled;
    private bool _wired;

    // ──── 对外入口 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 打开对话框。<paramref name="existing"/> 为 null 即「新建世界」态，否则为「改已存在地图」态。
    /// 同一实例可重复 Setup（每次重新读默认值）。
    /// </summary>
    public void Setup(EditorContext context, MapDoc? existing)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _existing = existing;

        if (!_wired)
        {
            BuildUi();
            Confirmed += OnConfirmed;
            CloseRequested += OnCanceled;
            Canceled += OnCanceled;
            _wired = true;
        }

        ApplyMode();
    }

    // ──── 界面构建 ───────────────────────────────────────────────────────────

    private void BuildUi()
    {
        Size = new Vector2I(560, 460);
        Unresizable = false;

        var root = new VBoxContainer { Name = "Body" };
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        _titleLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_titleLabel);

        // 编辑态下也能切到「新建世界」——否则工程一加载就有活动地图，创建态永远进不去。
        _newWorldBtn = new Button { Text = "＋ 新建世界地图…" };
        _newWorldBtn.Pressed += () =>
        {
            if (_context is not null)
            {
                Setup(_context, null);
            }
        };
        root.AddChild(_newWorldBtn);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 4);
        root.AddChild(grid);

        // 尺寸：物理长 × 宽 + 单位
        grid.AddChild(new Label { Text = "尺寸" });
        var sizeRow = new HBoxContainer();
        _lenSpin = new SpinBox
        {
            MinValue = 1, MaxValue = 1_000_000, Step = 1, Value = 12800,
            CustomMinimumSize = new Vector2(96f, 0f),
            TooltipText = "地图的物理长度（与右侧单位一致）",
        };
        sizeRow.AddChild(_lenSpin);
        sizeRow.AddChild(new Label { Text = "×" });
        _widSpin = new SpinBox
        {
            MinValue = 1, MaxValue = 1_000_000, Step = 1, Value = 12800,
            CustomMinimumSize = new Vector2(96f, 0f),
            TooltipText = "地图的物理宽度（与右侧单位一致）",
        };
        sizeRow.AddChild(_widSpin);
        _sizeUnitLabel = new Label { Text = "km" };
        sizeRow.AddChild(_sizeUnitLabel);
        grid.AddChild(sizeRow);

        // 分辨率：预设档 + 自定义
        grid.AddChild(new Label { Text = "分辨率" });
        var resRow = new HBoxContainer();
        _resPreset = new OptionButton();
        foreach (var preset in ResolutionPresets)
        {
            for (var i = 0; i < preset.Values.Length; i++)
            {
                _resPreset.AddItem($"{preset.Values[i]} {preset.Unit}/格");
            }
        }

        _resPreset.AddItem(CustomOptionLabel);
        _resPreset.ItemSelected += OnResolutionPresetSelected;
        resRow.AddChild(_resPreset);
        _resCustom = new SpinBox
        {
            MinValue = 0.01, MaxValue = 10000, Step = 0.01, Value = 25,
            CustomMinimumSize = new Vector2(84f, 0f),
            Suffix = "/格",
            TooltipText = "自定义单格距离；只有在上一个下拉里选「自定义…」时才生效",
        };
        _resCustom.ValueChanged += _ => RefreshCoverage();
        resRow.AddChild(_resCustom);
        grid.AddChild(resRow);

        // 实际覆盖（spec 1 §3.5：必须写出来，可能略大于输入）
        grid.AddChild(new Label { Text = "实际覆盖" });
        _coverageLabel = new Label { Text = "—" };
        grid.AddChild(_coverageLabel);

        // 瓦片风格（T14）：条目来自 assets/textures/tiles/styles.yaml 的注册器。
        // 目前选择只写进 EditorContext.CurrentTilesetId（进程内），渲染层尚未消费，故不会改变画面。
        grid.AddChild(new Label { Text = "瓦片风格" });
        _tileStyle = new OptionButton
        {
            TooltipText = "瓦片风格来自 assets/textures/tiles/styles.yaml；本轮仅进程内生效，尚未写入地图文件",
        };
        foreach (var styleId in TileSetLoader.AvailableIds)
        {
            _tileStyle.AddItem(styleId);
        }

        _tileStyle.ItemSelected += OnTileStyleSelected;
        grid.AddChild(_tileStyle);

        root.AddChild(new HSeparator());

        // 生成参数（仅「新建世界」态可编辑）
        _genBox = new VBoxContainer { Name = "GenBox" };
        _genBox.AddThemeConstantOverride("separation", 4);
        root.AddChild(_genBox);

        var landRow = new HBoxContainer();
        landRow.AddChild(new Label { Text = "陆地比例" });
        _landSpin = new SpinBox
        {
            MinValue = 0.05, MaxValue = 0.80, Step = 0.01, Value = 0.35,
            CustomMinimumSize = new Vector2(96f, 0f),
            TooltipText = "生成时陆地占比（spec 2 §6.3：0.05–0.80，默认 0.35）",
        };
        _landSpin.ValueChanged += _ => RefreshCoverage();
        landRow.AddChild(_landSpin);
        landRow.AddChild(new Label { Text = "（0.05 – 0.80）" });
        _genBox.AddChild(landRow);

        var ruggedRow = new HBoxContainer();
        ruggedRow.AddChild(new Label { Text = "曲折度" });
        _ruggedSpin = new SpinBox
        {
            MinValue = 0, MaxValue = 1, Step = 0.05, Value = 0.5,
            CustomMinimumSize = new Vector2(96f, 0f),
            TooltipText = "海岸线曲折程度（spec 2 §6.3：0–1，默认 0.5）",
        };
        _ruggedSpin.ValueChanged += _ => RefreshCoverage();
        ruggedRow.AddChild(_ruggedSpin);
        ruggedRow.AddChild(new Label { Text = "（0 – 1）" });
        _genBox.AddChild(ruggedRow);

        var seedRow = new HBoxContainer();
        seedRow.AddChild(new Label { Text = "种子" });
        _seedSpin = new SpinBox
        {
            MinValue = 0, MaxValue = 2_147_483_647, Step = 1, Value = 0,
            CustomMinimumSize = new Vector2(120f, 0f),
            TooltipText = "同一种子 + 同一参数必定生成同一世界（确定性）",
        };
        seedRow.AddChild(_seedSpin);
        _rerollBtn = new Button { Text = "🎲 重新掷骰", TooltipText = "换一个随机种子并刷新预览" };
        _rerollBtn.Pressed += OnRerollSeed;
        seedRow.AddChild(_rerollBtn);
        _genBox.AddChild(seedRow);

        root.AddChild(new HSeparator());

        // 预览
        var previewRow = new HBoxContainer();
        previewRow.AddChild(new Label { Text = "预览" });
        var refreshBtn = new Button { Text = "刷新预览", TooltipText = "按当前参数重新生成一次预览" };
        refreshBtn.Pressed += RefreshPreview;
        previewRow.AddChild(refreshBtn);
        root.AddChild(previewRow);

        _preview = new TextureRect
        {
            Name = "Preview",
            CustomMinimumSize = new Vector2(0f, 180f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        root.AddChild(_preview);

        _statsLabel = new Label { Text = "细化：—" };
        _statsLabel.AddThemeFontSizeOverride("font_size", 11);
        root.AddChild(_statsLabel);

        _warnLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _warnLabel.AddThemeFontSizeOverride("font_size", 11);
        _warnLabel.AddThemeColorOverride("font_color", new Color(0.93f, 0.75f, 0.31f));
        root.AddChild(_warnLabel);
    }

    // ──── 两态切换 ───────────────────────────────────────────────────────────

    private void ApplyMode()
    {
        var creating = _existing is null;

        if (_titleLabel is not null)
        {
            _titleLabel.Text = creating
                ? "新建世界地图：填物理尺寸与分辨率，生成参数可调。"
                : $"调整地图尺寸：{_existing!.Name}（生成参数只读）。";
        }

        if (_newWorldBtn is not null)
        {
            _newWorldBtn.Visible = !creating;
            _newWorldBtn.Disabled = creating;
        }

        if (_genBox is not null)
        {
            // 已存在地图不提供「重新生成」：参数只读、掷骰禁用。
            foreach (var child in _genBox.GetChildren())
            {
                if (child is Control c)
                {
                    c.Modulate = creating ? Colors.White : new Color(1f, 1f, 1f, 0.55f);
                }
            }
        }

        SetGenEditable(creating);

        if (_rerollBtn is not null)
        {
            _rerollBtn.Disabled = !creating;
        }

        OkButtonText = creating ? "创建" : "应用";

        // 瓦片风格：两种态都显示当前值（默认来自 styles.yaml 的 default:）
        SelectTileStyle(_context?.CurrentTilesetId ?? TileSetLoader.DefaultId);

        // 初值：新建 → 默认 12800 km 见方 @ 25 km/格（= 512 格，spec 2 §4.1 的世界默认）；
        // 已存在 → 反推该图当前的物理边长与分辨率。
        if (creating)
        {
            var unit = UnitFor(MapType.World);
            if (_sizeUnitLabel is not null) _sizeUnitLabel.Text = unit;
            if (_lenSpin is not null) _lenSpin.Value = 12800;
            if (_widSpin is not null) _widSpin.Value = 12800;
            SelectPresetIndex(MapType.World, 25d, fallback: 1);
            if (_landSpin is not null) _landSpin.Value = 0.35;
            if (_ruggedSpin is not null) _ruggedSpin.Value = 0.5;
            if (_seedSpin is not null) _seedSpin.Value = 0;
        }
        else
        {
            var grid = _existing!.Grid;
            var unit = string.IsNullOrEmpty(grid.DistanceUnit) ? UnitFor(_existing.Type) : grid.DistanceUnit;
            if (_sizeUnitLabel is not null) _sizeUnitLabel.Text = unit;
            if (_lenSpin is not null) _lenSpin.Value = grid.Width * grid.CellDistance;
            if (_widSpin is not null) _widSpin.Value = grid.Height * grid.CellDistance;
            if (_resCustom is not null) _resCustom.Value = grid.CellDistance;
            SelectPresetIndex(_existing.Type, grid.CellDistance, fallback: -1);
            // 生成参数只读地显示该图已保存的值（无 generation 块则用默认值）。
            var spec = _existing.Generation;
            if (_landSpin is not null) _landSpin.Value = spec?.LandRatio ?? 0.35d;
            if (_ruggedSpin is not null) _ruggedSpin.Value = spec?.Ruggedness ?? 0.5d;
            if (_seedSpin is not null) _seedSpin.Value = spec?.Seed ?? 0;
        }

        RefreshCoverage();

        // 已存在地图不需要预览（不让用户以为可以「重新生成」）；新建态才跑一次预览。
        if (creating)
        {
            if (_preview is not null) _preview.Visible = true;
            RefreshPreview();
        }
        else
        {
            ClearPreview();
            if (_statsLabel is not null) _statsLabel.Text = "细化：—（该图已生成的统计见地图文件）";
        }
    }

    private void SetGenEditable(bool editable)
    {
        foreach (var spin in new[] { _landSpin, _ruggedSpin, _seedSpin })
        {
            if (spin is not null)
            {
                spin.Editable = editable;
            }
        }
    }

    // ──── 参数读取 ───────────────────────────────────────────────────────────

    private string CurrentUnit => _sizeUnitLabel?.Text ?? "km";

    /// <summary>当前生效的单格距离：预设档优先，只有选中「自定义…」才读手输框。</summary>
    private double CurrentCellDistance()
    {
        var selected = _resPreset?.Selected ?? -1;
        var flat = FlattenPresets();
        if (selected >= 0 && selected < flat.Count)
        {
            return flat[selected].Value;
        }

        return _resCustom?.Value ?? 25d;
    }

    private List<(MapType Type, string Unit, double Value)> FlattenPresets()
    {
        var list = new List<(MapType, string, double)>();
        foreach (var preset in ResolutionPresets)
        {
            foreach (var v in preset.Values)
            {
                list.Add((preset.Type, preset.Unit, v));
            }
        }

        return list;
    }

    private GenerationSpec BuildSpec()
    {
        return new GenerationSpec(
            Algorithm: WorldGenerator.Algorithm,
            Seed: (long)(_seedSpin?.Value ?? 0d),
            LandRatio: _landSpin?.Value ?? 0.35d,
            Ruggedness: _ruggedSpin?.Value ?? 0.5d,
            MinIslandCells: 8,
            MinLakeCells: 6,
            Erosion: 1);
    }

    /// <summary>按当前输入推导目标网格。<c>template</c> 只提供格距离与单位，宽高由物理范围算出。</summary>
    private GridSpec BuildGrid()
    {
        var cellDistance = Math.Max(0.01d, CurrentCellDistance());
        var length = Math.Max(1d, _lenSpin?.Value ?? 12800d);
        var width = Math.Max(1d, _widSpin?.Value ?? 12800d);
        var template = new GridSpec(1, 1, cellDistance, CurrentUnit, CurrentUnit, CurrentUnit);
        return MapSizing.GridFromExtent(length, width, template);
    }

    // ──── 覆盖率 / 警告 / 预览 ───────────────────────────────────────────────

    private void OnResolutionPresetSelected(long index)
    {
        var flat = FlattenPresets();
        if (index >= 0 && index < flat.Count && _resCustom is not null)
        {
            _resCustom.Editable = false;
            _resCustom.Value = flat[(int)index].Value;
        }
        else if (_resCustom is not null)
        {
            // 「自定义…」：只有选它才允许手输任意小数。
            _resCustom.Editable = true;
        }

        RefreshCoverage();
        RefreshPreview();
    }

    /// <summary>T14：下拉只是把选择写进 <see cref="EditorContext.CurrentTilesetId"/>（进程内，不落盘）。</summary>
    private void OnTileStyleSelected(long index)
    {
        var ids = TileSetLoader.AvailableIds;
        if (_context is not null && index >= 0 && index < ids.Count)
        {
            _context.CurrentTilesetId = ids[(int)index];
        }
    }

    private void SelectTileStyle(string id)
    {
        if (_tileStyle is null)
        {
            return;
        }

        var ids = TileSetLoader.AvailableIds;
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                _tileStyle.Selected = i;
                return;
            }
        }
    }

    private void SelectPresetIndex(MapType type, double cellDistance, int fallback)
    {
        if (_resPreset is null)
        {
            return;
        }

        var flat = FlattenPresets();
        for (var i = 0; i < flat.Count; i++)
        {
            if (flat[i].Type == type && Math.Abs(flat[i].Value - cellDistance) < 1e-9)
            {
                _resPreset.Selected = i;
                if (_resCustom is not null)
                {
                    _resCustom.Editable = false;
                    _resCustom.Value = cellDistance;
                }

                return;
            }
        }

        // 没命中任何预设：落在「自定义…」上（新建态用 fallback 指定一个合理档）。
        if (fallback >= 0)
        {
            _resPreset.Selected = fallback;
            if (_resCustom is not null)
            {
                _resCustom.Editable = false;
                _resCustom.Value = flat[fallback].Value;
            }

            return;
        }

        _resPreset.Selected = flat.Count; // 末项 = 自定义…
        if (_resCustom is not null)
        {
            _resCustom.Editable = true;
            _resCustom.Value = cellDistance;
        }
    }

    /// <summary>刷新「实际覆盖」行与超大网格警告（spec 1 §3.5 + plan L420）。</summary>
    private void RefreshCoverage()
    {
        var grid = BuildGrid();
        if (_coverageLabel is not null)
        {
            _coverageLabel.Text =
                $"{grid.Width * grid.CellDistance:0.##} × {grid.Height * grid.CellDistance:0.##} {CurrentUnit} 见方" +
                $"（{grid.Width} 格 × {grid.Height} 格 @ {grid.CellDistance:0.##} {CurrentUnit}/格）";
        }

        if (_warnLabel is null)
        {
            return;
        }

        var cells = (long)grid.Width * grid.Height;
        if (cells > 1024L * 1024L)
        {
            var records = cells / 1000 * 1000;
            var mb = cells * 24d / 1024d / 1024d; // 每格稀疏记录粗估 24 字节
            _warnLabel.Text =
                $"⚠ 预计写盘约 {records} 条记录（约 {mb:0.#} MB）… 建议把分辨率调粗到 25 km/格或更大。";
        }
        else
        {
            _warnLabel.Text = string.Empty;
        }
    }

    private void OnRerollSeed()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        if (_seedSpin is not null)
        {
            _seedSpin.Value = rng.RandiRange(0, 2_147_483_647);
        }

        RefreshPreview();
    }

    /// <summary>按当前参数跑一次生成并画缩略图。超过 <see cref="PreviewCellCap"/> 时按比例降采样（并在统计行注明）。</summary>
    private void RefreshPreview()
    {
        if (_context is null || _existing is not null || _preview is null || _statsLabel is null)
        {
            return;
        }

        var grid = BuildGrid();
        _previewDownscaled = Math.Max(grid.Width, grid.Height) > PreviewCellCap;
        var genW = grid.Width;
        var genH = grid.Height;
        if (_previewDownscaled)
        {
            var scale = (double)PreviewCellCap / Math.Max(grid.Width, grid.Height);
            genW = Math.Max(8, (int)Math.Round(grid.Width * scale));
            genH = Math.Max(8, (int)Math.Round(grid.Height * scale));
        }

        var spec = BuildSpec();
        GeneratedWorld world;
        try
        {
            world = WorldGenerator.Generate(new GenerationParams(spec, genW, genH, grid.CellDistance));
        }
        catch (Exception ex)
        {
            _statsLabel.Text = $"预览生成失败：{ex.Message}";
            ClearPreview();
            return;
        }

        _lastSpec = spec;
        _lastStats = world.Stats;
        DrawPreview(world, genW, genH, world.DefaultType);

        _statsLabel.Text =
            $"细化（去碎岛 {world.Stats.RemovedCells} · 填碎海 {world.Stats.FilledCells} · 重标海岸 {world.Stats.RelabeledCells}）" +
            (_previewDownscaled ? $"　预览已降采样至 {genW}×{genH}，创建时按 {grid.Width}×{grid.Height} 全量生成。" : string.Empty);
    }

    private void DrawPreview(GeneratedWorld world, int width, int height, TerrainType defaultType)
    {
        if (_preview is null)
        {
            return;
        }

        // 手法与 MinimapSection.RebuildTexture() 一致（plan L421）：Image.CreateEmpty + Fill + SetPixel + CreateFromImage，
        // 不新增 scripts/Render/** 文件。
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(TerrainPalette.ColorOf(defaultType));
        foreach (var cell in world.Cells)
        {
            if (cell.At.X < 0 || cell.At.Y < 0 || cell.At.X >= width || cell.At.Y >= height)
            {
                continue;
            }

            image.SetPixel(cell.At.X, cell.At.Y, TerrainPalette.ColorOf(cell.Type));
        }

        var texture = ImageTexture.CreateFromImage(image);
        _preview.Texture = texture;
        _previewTexture?.Dispose();
        _previewTexture = texture;
    }

    private void ClearPreview()
    {
        if (_preview is not null)
        {
            _preview.Texture = null;
        }

        _previewTexture?.Dispose();
        _previewTexture = null;
    }

    // ──── 确认 / 取消 ────────────────────────────────────────────────────────

    private void OnCanceled()
    {
        ClearPreview();
    }

    private void OnConfirmed()
    {
        if (_context is null)
        {
            return;
        }

        var grid = BuildGrid();
        if (_existing is not null)
        {
            ApplyResize(grid);
            return;
        }

        CreateWorld(grid);
    }

    /// <summary>已存在地图：先算出会丢多少，有损失就弹二次确认，确认后才推命令（可撤销）。</summary>
    private void ApplyResize(GridSpec grid)
    {
        var map = _existing!;
        var loss = ResizeMapCommand.DescribeLoss(map, grid);

        // DescribeLoss 在「不丢数据」时返回「扩大到 … 不会丢弃任何数据。」，此时无需二次确认。
        if (!loss.Contains("不会丢弃", StringComparison.Ordinal))
        {
            var confirm = new ConfirmationDialog
            {
                Title = "确认调整尺寸",
                DialogText = loss,
            };
            confirm.Confirmed += () =>
            {
                _context!.UndoStack.Push(new ResizeMapCommand(_context, map.Id, grid));
                confirm.QueueFree();
            };
            confirm.Canceled += confirm.QueueFree;
            AddChild(confirm);
            confirm.PopupCentered();
            return;
        }

        _context!.UndoStack.Push(new ResizeMapCommand(_context, map.Id, grid));
    }

    /// <summary>
    /// 新建世界：生成 → <see cref="EditorContext.AddMap"/> → <see cref="EditorContext.DrillTo"/>。
    /// 生成发生在**地图存在之前**，故不进撤销栈（plan L422）。
    /// </summary>
    private void CreateWorld(GridSpec grid)
    {
        var spec = BuildSpec();
        var world = WorldGenerator.Generate(new GenerationParams(spec, grid.Width, grid.Height, grid.CellDistance));

        // sparse 列表按 (Y, X) 升序 —— 项目约定。
        var cells = world.Cells
            .OrderBy(e => e.At.Y)
            .ThenBy(e => e.At.X)
            .ToList();

        var id = NextWorldMapId();
        var map = new MapDoc(
            Id: id,
            Name: $"新世界 {id}",
            Type: MapType.World,
            Grid: grid,
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(
                Encoding: "sparse",
                Default: TerrainDefaults.Water,
                CellsRef: null,
                Cells: cells),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>(),
            Generation: spec);

        // 顺序是 load-bearing 的（见 EditorContext.AddMap 的 XML doc）：先入 _maps，再更新 World.MapIndex，
        // 否则新图会进 _maps 但落盘时 ProjectSaver 找不到对应的 map_index 项。
        _context!.AddMap(map, $"maps/world/{id}/main.yaml");
        _context.DrillTo(id);
        ClearPreview();
    }

    /// <summary>挑一个工程内未被占用的世界图 id。</summary>
    private string NextWorldMapId()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        if (_context is not null)
        {
            foreach (var m in _context.Maps)
            {
                taken.Add(m.Id);
            }
        }

        if (!taken.Contains("map_world_new"))
        {
            return "map_world_new";
        }

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"map_world_new_{i}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"map_world_new_{DateTime.UtcNow.Ticks}";
    }

    // ──── 小工具 ─────────────────────────────────────────────────────────────

    /// <summary>单位口径：World → km、City → m；其余（未来扩充）暂按 m。</summary>
    private static string UnitFor(MapType type) => type switch
    {
        MapType.World => "km",
        _ => "m",
    };
}
