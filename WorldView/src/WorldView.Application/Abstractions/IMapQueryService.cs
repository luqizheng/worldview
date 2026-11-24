using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Domain.Time;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Core Domain 地图查询服务端口（AF-01 §7.1 读路径）。UI/Application 不得直接查询
/// SQLite 填充地图列表；地图查询语义由 Core Domain 读服务拥有并返回不可变投影。
///
/// AF-02 阶段真实实现不存在（正式 Core Domain 持久化尚未实现），运行时组合根不注册本接口；
/// 打开地图工作区流程在门槛处报告阻塞项。测试替身仅限单元测试。
/// </summary>
public interface IMapQueryService
{
    /// <summary>列出项目内全部地图摘要（不可变投影）。</summary>
    Task<IReadOnlyList<MapSummary>> ListMapsAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>读取单个地图摘要；地图不存在时返回 null。</summary>
    Task<MapSummary?> GetMapAsync(Guid projectId, Guid mapId, CancellationToken cancellationToken = default);
}

/// <summary>地图摘要（Core Domain 不可变读投影）。</summary>
public sealed record MapSummary
{
    public required Guid MapId { get; init; }

    public required string DisplayName { get; init; }
}

/// <summary>
/// Core Domain Branch / 正式历史时间解析端口（AF-01 §6 规则 2：Branch 与 Historical Time
/// 必须经 Core Domain 服务解析/校验）。
///
/// 参数与结果当前为不透明占位（正式 Historical Time 语义随 Core Domain 实现落地）；
/// 本端口不发明任何新的领域语义。
/// </summary>
public interface IWorkspaceContextResolver
{
    /// <summary>
    /// 校验地图存在性并解析 Branch / 规范历史时间。
    /// </summary>
    /// <param name="projectId">项目 ID。</param>
    /// <param name="mapId">目标地图 ID。</param>
    /// <param name="branchId">目标 Branch；null 表示该地图的默认/主历史（语义由 Core Domain 拥有）。</param>
    /// <param name="historicalTimeInput">不透明历史时间输入；null 表示默认时间点。</param>
    Task<WorkspaceContextResolution> ResolveAsync(
        Guid projectId,
        Guid mapId,
        Guid? branchId,
        string? historicalTimeInput,
        CancellationToken cancellationToken = default);
}

/// <summary>工作区上下文解析结果。</summary>
public sealed class WorkspaceContextResolution
{
    private WorkspaceContextResolution()
    {
    }

    public bool Success { get; private init; }

    /// <summary>诊断说明（失败时）。</summary>
    public string? Diagnostic { get; private init; }

    /// <summary>校验后的工作区上下文（成功时）。</summary>
    public ValidatedWorkspaceContext? Context { get; private init; }

    public static WorkspaceContextResolution Ok(ValidatedWorkspaceContext context) => new()
    {
        Success = true,
        Context = context,
    };

    public static WorkspaceContextResolution Failed(string diagnostic) => new()
    {
        Success = false,
        Diagnostic = diagnostic,
    };
}

/// <summary>
/// 经 Core Domain 校验的工作区上下文。WP-30A（PRA-01 M1 Profile §4.1）起，Application
/// 边界完整保留权威解析结果：MapId、BranchId、RootHistoryId 与 typed 规范历史时间
/// （不再降级为不透明字符串）。身份/时间语义规则仍由 Core Domain 独占；
/// 本类型只承载已校验的不可变值，不引入第二事实来源。
/// </summary>
public sealed record ValidatedWorkspaceContext
{
    public required Guid MapId { get; init; }

    /// <summary>解析后的 Branch ID；null 表示默认/主历史（根历史）。</summary>
    public Guid? BranchId { get; init; }

    /// <summary>Map 根历史 ID（Core Domain 权威解析；缺失即数据损坏，不会出现在成功结果中）。</summary>
    public required Guid RootHistoryId { get; init; }

    /// <summary>
    /// typed 规范历史时间（Core Domain <see cref="HistoricalTime"/>；权威解析时
    /// null 输入已解析为纪元，永不为 null）。保留 day/offset 全精度。
    /// </summary>
    public required HistoricalTime HistoricalTime { get; init; }

    /// <summary>规范历史时间的规范文本（由 typed 值计算，无第二事实来源）。</summary>
    public string CanonicalHistoricalTime => HistoricalTime.Canonical();
}
