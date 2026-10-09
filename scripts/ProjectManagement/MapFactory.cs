using System;
using System.Collections.Generic;
using WorldView.Domain;

namespace WorldView.ProjectManagement;

/// <summary>
/// 新建空白地图文档工厂（规格 §6.2 数据契约默认值）。
/// </summary>
public static class MapFactory
{
    /// <summary>
    /// 生成一张空白 <see cref="MapDoc"/>。
    /// <para>
    /// 网格默认值：
    /// <list type="bullet">
    ///   <item>world：64×48、cell_distance=10、distance_unit=km</item>
    ///   <item>city：64×64、cell_distance=2、distance_unit=m</item>
    /// </list>
    /// 时间默认值：world = Month/Day、city = Hour/Hour。
    /// </para>
    /// </summary>
    public static MapDoc CreateBlankMap(
        MapType type,
        string id,
        string displayName,
        IReadOnlyList<LayerRef> defaultLayers)
    {
        var grid = type == MapType.World
            ? new GridSpec(Width: 64, Height: 48, CellDistance: 10, DistanceUnit: "km")
            : new GridSpec(Width: 64, Height: 64, CellDistance: 2, DistanceUnit: "m");

        var time = type == MapType.World
            ? new TimeSpec(DefaultGranularity: TimeGranularity.Month, MinGranularity: TimeGranularity.Day)
            : new TimeSpec(DefaultGranularity: TimeGranularity.Hour, MinGranularity: TimeGranularity.Hour);

        var layers = defaultLayers ?? Array.Empty<LayerRef>();

        return new MapDoc(
            Id: id,
            Name: displayName,
            Type: type,
            Grid: grid,
            Time: time,
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(
                Encoding: "sparse",
                Default: TerrainDefaults.Plain,
                CellsRef: null,
                Cells: Array.Empty<TerrainCellEntry>()),
            Layers: layers,
            CityRefs: Array.Empty<CityRef>(),
            Markers: Array.Empty<MarkerDoc>(),
            Routes: Array.Empty<RouteDoc>(),
            Buildings: Array.Empty<Building>(),
            Generation: null);
    }
}
