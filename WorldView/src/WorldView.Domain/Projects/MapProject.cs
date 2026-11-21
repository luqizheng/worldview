namespace WorldView.Domain.Projects;

/// <summary>
/// 地图项目聚合根（架构示例）。
/// 当前仅承载最小不变量，用于建立分层、DI 与测试基线；
/// 正式领域模型以后续历史状态引擎设计（docs/P2-01-D01、docs/adr）为准。
/// </summary>
public sealed class MapProject
{
    private MapProject(MapProjectId id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>项目标识。</summary>
    public MapProjectId Id { get; }

    /// <summary>项目名称（非空白，去除首尾空白后保存）。</summary>
    public string Name { get; private set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 创建地图项目；名称为空白时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static MapProject Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new MapProject(MapProjectId.New(), name.Trim(), DateTimeOffset.UtcNow);
    }
}
