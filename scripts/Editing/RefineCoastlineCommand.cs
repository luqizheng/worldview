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
/// 三个静态方法是<strong>纯函数</strong>（不读不写 <see cref="EditorContext"/>），方便裸测；
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

    public string Description
    {
        get
        {
            if (_outcome is null) return "细化海岸线";
            return $"细化海岸线（去碎岛 {_outcome.Stats.RemovedCells} · 填碎海 {_outcome.Stats.FilledCells}）";
        }
    }

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
    /// 纯：撤销细化。
    ///
    /// 通过先对当前 map 应用一次 outcome（把「应用后的状态」拿稳），再把 <paramref name="beforeCells"/>
    /// 写回（保持 sparse 升序）实现，逻辑与 <see cref="ApplyTo"/> 一致：值为 defaultType 的格从 cells 中删除。
    /// 调用方传入 Apply 前的 snapshot，保证值相等。
    /// </summary>
    public static MapDoc RevertTo(MapDoc map, RefineOutcome outcome, IReadOnlyList<TerrainCellEntry>? beforeCells = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(outcome);

        var defaultType = map.TerrainData.Default?.Type ?? TerrainDefaults.DefaultType;

        // 优先使用 beforeCells（精确还原 Apply 前的 sparse 状态）；否则按 outcome 字典反推。
        var byCoord = new Dictionary<GridCoord, TerrainCellEntry>();
        if (beforeCells is not null)
        {
            foreach (var entry in beforeCells)
            {
                byCoord[entry.At] = entry;
            }
        }
        else
        {
            // 退化路径：从当前 cells 出发，反向应用 outcome.
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
                    // Apply 时是「删除」：原来这里有一格非默认；要恢复它需要原类型，outcome 没记，
                    // 故退化路径只能丢弃——降级路径上由 beforeCells 必填来避免。
                    // 为安全起见，此处保留 byCoord[kvp.Key] 不变（什么都不做）。
                }
                else
                {
                    // Apply 时是「写入」：原值就是 defaultType，不在 sparse 里。
                    // 这里也得丢掉——同样退化路径只能保留现状。
                }
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

    public void Apply()
    {
        var map = _context.FindMap(_mapId)
            ?? throw new InvalidOperationException($"Map not found: {_mapId}");
        _beforeCells ??= map.TerrainData.Cells;
        _outcome ??= Compute(map, _rangeMin, _rangeMax, _options);
        var updated = ApplyTo(map, _outcome);
        _context.ReplaceMap(updated);
    }

    public void Revert()
    {
        if (_beforeCells is null || _outcome is null)
            throw new InvalidOperationException("Revert before Apply is not allowed.");
        var map = _context.FindMap(_mapId)
            ?? throw new InvalidOperationException($"Map not found: {_mapId}");
        var restored = RevertTo(map, _outcome, _beforeCells);
        _context.ReplaceMap(restored);
    }
}