using System;
using System.Collections.Generic;
using System.Linq;
using WorldView.Domain;
using WorldView.Domain.Generation;
using WorldView.Editor;

namespace WorldView.Editing;

/// <summary>
/// 对地图指定范围执行海岸线细化的可撤销命令（spec 2 §6.4 + plan L321-339）。
///
/// 五个静态方法是<strong>纯函数</strong>（不读不写 <see cref="EditorContext"/>），方便裸测：
/// <see cref="OptionsFor"/> / <see cref="Compute"/> / <see cref="ApplyTo"/> / <see cref="RevertTo"/> / <see cref="Describe"/>；
/// 实例路径（<see cref="Apply"/> / <see cref="Revert"/>）走 <see cref="EditorContext.ReplaceMap"/> 写回通道。
/// </summary>
public sealed class RefineCoastlineCommand : IEditorCommand
{
    private readonly EditorContext _context;
    private readonly string _mapId;
    private readonly GridCoord _rangeMin;
    private readonly GridCoord _rangeMax;
    private readonly RefineOptions _options;

    private RefineOutcome? _outcome;
    private IReadOnlyList<TerrainCellEntry>? _beforeCells;
    private bool _applied;

    public RefineCoastlineCommand(
        EditorContext context,
        string mapId,
        GridCoord rangeMin,
        GridCoord rangeMax,
        int radiusCells,
        GenerationSpec? spec = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mapId);
        _context = context;
        _mapId = mapId;
        _rangeMin = rangeMin;
        _rangeMax = rangeMax;
        _options = OptionsFor(radiusCells, spec);
    }

    /// <summary>纯：把细化统计格式化成 <see cref="Description"/> 使用的字符串（spec 2 §6.4 + brief §3.1）。中点用 <c>U+00B7</c>（MIDDLE DOT），不是全角 <c>U+30FB</c>。</summary>
    public static string Describe(RefineOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return $"细化海岸线（去碎岛 {outcome.Stats.RemovedCells} · 填碎海 {outcome.Stats.FilledCells}）";
    }

    public string Description => _outcome is null ? "细化海岸线" : Describe(_outcome);

    /// <summary>
    /// 把「笔刷半径」映射到三档细化强度（spec 2 §6.4）：
    /// 1–2 → 只重标 coast（精细活）；3–8 → 温和清理小碎块；>8 → 完整细化（用 spec.Erosion）。
    /// </summary>
    public static RefineOptions OptionsFor(int radiusCells, GenerationSpec? spec)
    {
        if (radiusCells <= 2) return new RefineOptions(MinIslandCells: 0, MinLakeCells: 0, Erosion: 0, Morphology: false);
        if (radiusCells <= 8) return new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false);
        return new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: spec?.Erosion ?? 1, Morphology: true);
    }

    /// <summary>纯：在给定地图与范围上算出细化结果（不修改任何状态）。</summary>
    public static RefineOutcome Compute(
        MapDoc map, GridCoord rangeMin, GridCoord rangeMax, RefineOptions options)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(options);
        var defaultType = map.TerrainData.Default?.Type ?? TerrainDefaults.DefaultType;
        var sparse = new Dictionary<GridCoord, TerrainType>();
        if (map.TerrainData.Cells is not null)
        {
            foreach (var entry in map.TerrainData.Cells)
            {
                sparse[entry.At] = entry.Type;
            }
        }
        Func<int, int, TerrainType> read = (x, y) =>
        {
            var key = new GridCoord(x, y);
            return sparse.TryGetValue(key, out var t) ? t : defaultType;
        };
        return CoastlineRefiner.Refine(
            read, map.Grid.Width, map.Grid.Height,
            rangeMin, rangeMax, defaultType, options);
    }

    /// <summary>纯：把细化结果应用到 MapDoc 上，得到新的 MapDoc。值为 <see cref="TerrainType"/> 等于 <c>defaultType</c> 的格从 sparse 列表中删除（沿用 SetTerrainCells 口径）；结果按 (Y, X) 升序。</summary>
    public static MapDoc ApplyTo(MapDoc map, RefineOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(outcome);

        var defaultType = map.TerrainData.Default?.Type ?? TerrainDefaults.DefaultType;

        var byCoord = new Dictionary<GridCoord, TerrainCellEntry>();
        if (map.TerrainData.Cells is not null)
        {
            foreach (var entry in map.TerrainData.Cells)
            {
                byCoord[entry.At] = entry;
            }
        }

        foreach (var kvp in outcome.Changed)
        {
            if (EqualityComparer<TerrainType>.Default.Equals(kvp.Value, defaultType))
            {
                byCoord.Remove(kvp.Key);
            }
            else
            {
                byCoord[kvp.Key] = new TerrainCellEntry(kvp.Key, kvp.Value);
            }
        }

        var ordered = byCoord.Values
            .OrderBy(e => e.At.Y)
            .ThenBy(e => e.At.X)
            .ToList();

        var terrain = map.TerrainData with
        {
            Encoding = "sparse",
            CellsRef = null,
            Cells = ordered,
        };

        return map with { TerrainData = terrain };
    }

    /// <summary>
    /// 纯：把 sparse cells 列表还原成调用方传入的、<see cref="ApplyTo"/> 之前的精确快照。
    ///
    /// <paramref name="beforeCells"/> <strong>必填</strong>：<see cref="RefineOutcome.Changed"/> 是单向的
    /// （被删除的格不记录原类型），无法从 outcome 反推 Apply 前的状态，故撤销必须依赖调用方快照。
    /// <paramref name="outcome"/> 参数仅为与 <see cref="ApplyTo"/> 对称保留，<strong>不参与计算</strong>。
    /// 还原结果按 (Y, X) 升序。
    /// </summary>
    public static MapDoc RevertTo(MapDoc map, RefineOutcome outcome, IReadOnlyList<TerrainCellEntry> beforeCells)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(beforeCells);

        var byCoord = new Dictionary<GridCoord, TerrainCellEntry>();
        foreach (var entry in beforeCells)
        {
            byCoord[entry.At] = entry;
        }

        var ordered = byCoord.Values
            .OrderBy(e => e.At.Y)
            .ThenBy(e => e.At.X)
            .ToList();

        var terrain = map.TerrainData with
        {
            Encoding = "sparse",
            CellsRef = null,
            Cells = ordered,
        };

        return map with { TerrainData = terrain };
    }

    public void Apply()
    {
        var map = _context.FindMap(_mapId)
            ?? throw new InvalidOperationException($"Map not found: {_mapId}");
        if (!_applied)
        {
            _beforeCells = map.TerrainData.Cells ?? Array.Empty<TerrainCellEntry>();
            _outcome = Compute(map, _rangeMin, _rangeMax, _options);
            _applied = true;
        }
        _context.ReplaceMap(ApplyTo(map, _outcome!));
    }

    public void Revert()
    {
        if (!_applied)
            throw new InvalidOperationException("Revert before Apply is not allowed.");
        var map = _context.FindMap(_mapId)
            ?? throw new InvalidOperationException($"Map not found: {_mapId}");
        _context.ReplaceMap(RevertTo(map, _outcome!, _beforeCells!));
    }
}
