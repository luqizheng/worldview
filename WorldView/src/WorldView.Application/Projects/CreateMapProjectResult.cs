namespace WorldView.Application.Projects;

/// <summary>
/// 创建地图项目用例的输出。失败时 <see cref="Project"/> 为空且 <see cref="Error"/> 给出原因。
/// </summary>
public sealed record CreateMapProjectResult(bool Success, MapProjectDto? Project, string? Error)
{
    /// <summary>构造成功结果。</summary>
    public static CreateMapProjectResult Ok(MapProjectDto project) => new(true, project, null);

    /// <summary>构造失败结果。</summary>
    public static CreateMapProjectResult Fail(string error) => new(false, null, error);
}
