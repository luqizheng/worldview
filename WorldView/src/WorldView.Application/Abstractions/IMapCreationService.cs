using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Core Domain 地图创建服务端口（WP-20a）。Map 创建的领域规则（Map+根 History+
/// default Calendar+day-zero Configuration 四件结构、Map 名称唯一性）由 Core Domain
/// 拥有；本端口只把"当前 Project 会话内的地图创建"暴露给 Application 用例层。
/// 实现绑定当前 ProjectSession 的包数据库（会话关闭后以失败结果拒绝）；
/// UI/Application 不得直接执行 SQL 或手工插入 Map 行。
/// </summary>
public interface IMapCreationService
{
    /// <summary>在指定项目内创建命名地图；失败返回失败结果，不留半创建地图。</summary>
    Task<MapCreationResult> CreateAsync(MapCreationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>地图创建请求。</summary>
public sealed record MapCreationRequest
{
    /// <summary>目标项目 ID（Core Domain 权威身份）。</summary>
    public required Guid ProjectId { get; init; }

    /// <summary>地图显示名（用户输入；唯一性与规范化以 Domain 规则为准）。</summary>
    public required string MapName { get; init; }
}

/// <summary>地图创建失败类别。</summary>
public enum MapCreationFailureKind
{
    /// <summary>非失败。</summary>
    None = 0,

    /// <summary>输入不合法（空白名称）。</summary>
    InvalidName,

    /// <summary>同名地图已存在（同 Project 内名称唯一，不区分大小写）。</summary>
    DuplicateName,

    /// <summary>持久化/领域服务失败（含会话已关闭、项目不存在、IO/SQLite 失败）。</summary>
    CreationFailed,
}

/// <summary>地图创建结果。</summary>
public sealed class MapCreationResult
{
    private MapCreationResult()
    {
    }

    public bool Success { get; private init; }

    /// <summary>失败类别（成功时为 None）。</summary>
    public MapCreationFailureKind FailureKind { get; private init; }

    /// <summary>面向用户的诊断说明（失败时）。</summary>
    public string? Diagnostic { get; private init; }

    /// <summary>创建出的地图摘要（成功时；Core Domain 读投影）。</summary>
    public MapSummary? Map { get; private init; }

    public static MapCreationResult Ok(MapSummary map) => new()
    {
        Success = true,
        Map = map,
    };

    public static MapCreationResult Failed(MapCreationFailureKind kind, string diagnostic) => new()
    {
        Success = false,
        FailureKind = kind,
        Diagnostic = diagnostic,
    };
}
