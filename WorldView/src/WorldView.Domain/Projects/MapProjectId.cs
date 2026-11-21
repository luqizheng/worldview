namespace WorldView.Domain.Projects;

/// <summary>
/// 地图项目标识。
/// </summary>
public readonly record struct MapProjectId(Guid Value)
{
    /// <summary>生成新的项目标识。</summary>
    public static MapProjectId New() => new(Guid.NewGuid());
}
