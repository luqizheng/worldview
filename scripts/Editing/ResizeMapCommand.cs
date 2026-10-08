using System.Collections.Generic;
using System.Globalization;
using WorldView.Domain;
using WorldView.Editor;

namespace WorldView.Editing;

/// <summary>
/// 调整地图尺寸的可撤销命令（plan L344-365 Task 10）。
///
/// 五个纯静态方法 <see cref="CropCells"/> / <see cref="CropBuildings"/> / <see cref="Resize"/> /
/// <see cref="DescribeLoss"/> / <see cref="Describe"/> 不依赖 <see cref="EditorContext"/>，便于裸测；实例路径
/// <see cref="Apply"/> / <see cref="Revert"/> 走 <see cref="EditorContext.ReplaceMap"/> 写回通道。
///
/// <para>
/// 警告：<see cref="Resize"/> 不可逆——越界格与建筑在内存中丢失。
/// <see cref="Apply"/> 首次调用必须把整张 <see cref="MapDoc"/> 存为 <c>_before</c>，
/// <see cref="Revert"/> 才能完整恢复（含原始 <see cref="MapDoc.Grid"/>）。
/// </para>
/// </summary>
public sealed class ResizeMapCommand : IEditorCommand
{
    private readonly EditorContext _context;
    private readonly string _mapId;
    private readonly GridSpec _newGrid;

    private MapDoc? _before;
    private string? _description;

    public ResizeMapCommand(EditorContext context, string mapId, GridSpec newGrid)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mapId);
        ArgumentNullException.ThrowIfNull(newGrid);

        _context = context;
        _mapId = mapId;
        _newGrid = newGrid;
    }

    /// <summary>
    /// 懒求值：首次读取时才 <c>FindMap</c>（ctor 不碰 <c>EditorContext</c>，纯 .NET 测试可构造）。
    /// map 缺失时回退占位文案；格式化委托给纯静态 <see cref="Describe"/>。
    /// </summary>
    public string Description
    {
        get
        {
            if (_description is null)
            {
                var current = _context.FindMap(_mapId);
                _description = current is null
                    ? $"调整地图尺寸：→ {_newGrid.Width}×{_newGrid.Height} 格"
                    : Describe(current.Grid, _newGrid);
            }
            return _description;
        }
    }

    /// <summary>纯：把「旧尺寸 → 新尺寸」写成中文摘要（plan L359）。</summary>
    public static string Describe(GridSpec oldGrid, GridSpec newGrid)
    {
        ArgumentNullException.ThrowIfNull(oldGrid);
        ArgumentNullException.ThrowIfNull(newGrid);
        return $"调整地图尺寸：{oldGrid.Width}×{oldGrid.Height} → {newGrid.Width}×{newGrid.Height} 格";
    }

    /// <summary>
    /// 纯：换 <see cref="MapDoc.Grid"/> + 裁剪越界地形格与建筑。其余字段原样保留：
    /// <c>Encoding</c> / <c>Default</c> / <c>CellsRef</c> / <c>Generation</c> 都不动。
    /// </summary>
    public static MapDoc Resize(MapDoc map, GridSpec newGrid)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(newGrid);

        var newW = newGrid.Width;
        var newH = newGrid.Height;

        var croppedBuildings = CropBuildings(map.Buildings, newW, newH);

        if (map.TerrainData is null)
        {
            return map with
            {
                Grid = newGrid,
                Buildings = croppedBuildings,
            };
        }

        var croppedCells = CropCells(map.TerrainData.Cells, newW, newH);

        // 整个 TerrainData with 一次（保留 Encoding / Default / CellsRef / Generation），
        // 仅替换 Cells。
        var newTerrain = map.TerrainData with { Cells = croppedCells };

        return map with
        {
            Grid = newGrid,
            TerrainData = newTerrain,
            Buildings = croppedBuildings,
        };
    }

    /// <summary>
    /// 纯：裁掉 <c>x &gt;= width</c> 或 <c>y &gt;= height</c> 的格；其余保持原顺序
    /// （约定 <c>(y 升序, x 升序)</c>）。
    /// </summary>
    public static IReadOnlyList<TerrainCellEntry> CropCells(
        IReadOnlyList<TerrainCellEntry> cells, int width, int height)
    {
        if (cells is null || cells.Count == 0) return cells ?? System.Array.Empty<TerrainCellEntry>();

        var kept = new List<TerrainCellEntry>(cells.Count);
        foreach (var entry in cells)
        {
            if (entry.At.X < width && entry.At.Y < height)
            {
                kept.Add(entry);
            }
        }
        return kept;
    }

    /// <summary>
    /// 纯：裁掉 <c>x &gt;= width</c> 或 <c>y &gt;= height</c> 的建筑；其余保持原顺序。
    /// </summary>
    public static IReadOnlyList<Building> CropBuildings(
        IReadOnlyList<Building> buildings, int width, int height)
    {
        if (buildings is null || buildings.Count == 0) return buildings ?? System.Array.Empty<Building>();

        var kept = new List<Building>(buildings.Count);
        foreach (var b in buildings)
        {
            if (b.At.X < width && b.At.Y < height)
            {
                kept.Add(b);
            }
        }
        return kept;
    }

    /// <summary>
    /// 纯：把缩小 / 放大造成的丢格 / 丢建筑数写成中文摘要（zh-CN N0 数字）。
    /// <list type="bullet">
    ///   <item>丢格 / 丢建筑数均为 0 → <c>"扩大到 W × H 格不会丢弃任何数据。"</c></item>
    ///   <item>否则 → <c>"缩小到 W × H 格将丢弃 N1 个地形格与 N2 个建筑。"</c></item>
    /// </list>
    /// </summary>
    public static string DescribeLoss(MapDoc map, GridSpec newGrid)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(newGrid);

        var newW = newGrid.Width;
        var newH = newGrid.Height;
        var zh = CultureInfo.GetCultureInfo("zh-CN");

        var cellsBefore = map.TerrainData?.Cells?.Count ?? 0;
        var cellsAfter = map.TerrainData is null
            ? 0
            : CropCells(map.TerrainData.Cells, newW, newH).Count;
        var cellsDropped = cellsBefore - cellsAfter;

        var buildingsBefore = map.Buildings?.Count ?? 0;
        var buildingsAfter = CropBuildings(map.Buildings, newW, newH).Count;
        var buildingsDropped = buildingsBefore - buildingsAfter;

        if (cellsDropped == 0 && buildingsDropped == 0)
        {
            return $"扩大到 {newW} × {newH} 格不会丢弃任何数据。";
        }

        var cellsStr = cellsDropped.ToString("N0", zh);
        var buildingsStr = buildingsDropped.ToString("N0", zh);
        return $"缩小到 {newW} × {newH} 格将丢弃 {cellsStr} 个地形格与 {buildingsStr} 个建筑。";
    }

    public void Apply()
    {
        var map = _context.FindMap(_mapId)
            ?? throw new InvalidOperationException($"Map not found: {_mapId}");

        _before ??= map;

        var resized = Resize(map, _newGrid);
        _context.ReplaceMap(resized);
    }

    public void Revert()
    {
        if (_before is null)
        {
            throw new InvalidOperationException("Revert before Apply is not allowed.");
        }
        _context.ReplaceMap(_before);
    }
}
