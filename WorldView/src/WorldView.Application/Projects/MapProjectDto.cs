namespace WorldView.Application.Projects;

/// <summary>地图项目的应用层视图（DTO）。</summary>
public sealed record MapProjectDto(Guid Id, string Name, DateTimeOffset CreatedAt);
