using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;

namespace WorldView.Application.UseCases;

/// <summary>
/// Create Map 用例（WP-20a）：会话边界检查 → 输入边界检查（去首尾空白、非空）→
/// 经 <see cref="IMapCreationService"/> 端口调用正式 Core Domain 创建服务。
/// 领域规则（名称唯一性、根 History/Calendar/day-zero 结构）由 Domain 拥有，
/// 本用例不复制规则、不引用 Infrastructure/SQLite。
/// 实例与会话绑定（组合根按 ProjectSession 构造），会话关闭后拒绝新工作。
/// </summary>
public sealed class CreateMapUseCase
{
    private readonly ProjectSession _session;
    private readonly IMapCreationService _mapCreation;
    private readonly IAppLog? _log;

    public CreateMapUseCase(ProjectSession session, IMapCreationService mapCreation, IAppLog? log = null)
    {
        _session = session;
        _mapCreation = mapCreation;
        _log = log;
    }

    /// <summary>所属项目会话（实例与会话同生命周期）。</summary>
    public ProjectSession Session => _session;

    /// <summary>
    /// 执行创建。<paramref name="mapName"/> 为用户输入（可含首尾空白，此处规范化）；
    /// 一切失败均以结果返回（不抛异常，取消除外），不产生任何局部状态。
    /// </summary>
    public async Task<MapCreationResult> ExecuteAsync(string? mapName, CancellationToken cancellationToken = default)
    {
        // ---- 1. 会话边界 ----
        if (!_session.IsReady)
        {
            return MapCreationResult.Failed(
                MapCreationFailureKind.CreationFailed,
                $"项目会话未就绪（当前 {_session.State}），无法创建地图。");
        }

        if (_session.ProjectId is not { } projectId)
        {
            return MapCreationResult.Failed(
                MapCreationFailureKind.CreationFailed,
                "项目身份缺失：无法经正式 Core Domain 边界创建地图。");
        }

        // ---- 2. 输入边界（用户体验；权威规则由 Domain/持久化写路径保障） ----
        var trimmed = mapName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return MapCreationResult.Failed(MapCreationFailureKind.InvalidName, "地图名不能为空白。");
        }

        // ---- 3. 端口调用（正式领域创建） ----
        try
        {
            return await _mapCreation.CreateAsync(
                new MapCreationRequest { ProjectId = projectId, MapName = trimmed },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Warn("CreateMap", "地图创建失败。", ex);
            return MapCreationResult.Failed(
                MapCreationFailureKind.CreationFailed,
                $"创建地图失败：{ex.Message}");
        }
    }
}
