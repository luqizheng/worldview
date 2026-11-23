using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Workspace;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// AF-02 端口 IProjectCreationService 的正式 Core Domain 适配（CD-01 §5.1）：
/// 路径圈禁 → 自开包数据库 → ProjectRules 创建正式身份 → 单事务插入 → 释放句柄。
/// Application 不直接 SQL Insert；失败返回失败结果（数据库不留半成品——插入自身事务回滚）。
/// </summary>
public sealed class CoreDomainProjectCreationService : IProjectCreationService
{
    private readonly IPackageDatabaseFactory _databaseFactory;
    private readonly TimeProvider _timeProvider;

    public CoreDomainProjectCreationService(
        IPackageDatabaseFactory databaseFactory,
        TimeProvider? timeProvider = null)
    {
        _databaseFactory = databaseFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProjectCreationResult> CreateAsync(ProjectCreationRequest request, CancellationToken cancellationToken = default)
    {
        if (!ProjectPackageStructure.IsRelativePathConfinedToPackage(request.PackageRootPath, request.DatabaseRelativePath))
        {
            return ProjectCreationResult.Failed("包内数据库相对路径非法（越出包根或含遍历段），拒绝创建。");
        }

        var databasePath = Path.GetFullPath(Path.Combine(request.PackageRootPath, request.DatabaseRelativePath));
        try
        {
            await using var database = _databaseFactory.CreateForFile(databasePath);
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            var core = new SqliteCoreDomainDatabase(database);
            var record = ProjectRules.CreateProject(
                request.DisplayName,
                _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            // OD-4 建议案：Project 身份 + 十内置 Object Type 种子行同一事务写入。
            core.InsertProject(WorldView.Domain.Objects.ProjectCreationPlan.Create(record));

            return ProjectCreationResult.Ok(new ProjectIdentity
            {
                ProjectId = record.ProjectId,
                DisplayName = record.ProjectName,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return ProjectCreationResult.Failed("项目名不能为空白。");
        }
        catch (CoreDomainDataException ex)
        {
            return ProjectCreationResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            return ProjectCreationResult.Failed($"项目身份持久化失败：{ex.Message}");
        }
    }
}

/// <summary>
/// AF-02 端口 IProjectIdentityReader 的正式 Core Domain 适配（CD-01 §5.2）：
/// 从真实包数据库读取 core_project 原始行，身份校验规则（单行/UUID）由 Domain 拥有；
/// 损坏或不匹配的数据库按类别 fail-closed，不以 manifest 字段冒充正式身份。
/// </summary>
public sealed class CoreDomainProjectIdentityReader : IProjectIdentityReader
{
    public Task<ProjectIdentityReadResult> ReadAsync(IPackageDatabase database, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var core = new SqliteCoreDomainDatabase(database);
        IReadOnlyList<RawProjectIdentityRow> rows;
        try
        {
            rows = core.ReadProjectIdentityRows();
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return Task.FromResult(ProjectIdentityReadResult.Failed(
                ProjectIdentityFailure.Invalid,
                "项目身份 UUID 文本不可解析。"));
        }

        var (outcome, record) = ProjectRules.ValidateIdentityRows(rows);
        return Task.FromResult(outcome switch
        {
            ProjectIdentityOutcome.Ok when record is not null => ProjectIdentityReadResult.Ok(new ProjectIdentity
            {
                ProjectId = record.ProjectId,
                DisplayName = record.ProjectName,
            }),
            ProjectIdentityOutcome.NotEstablished => ProjectIdentityReadResult.Failed(
                ProjectIdentityFailure.NotEstablished,
                "数据库中不存在项目身份（尚未经 Core Domain 创建流程建立）。"),
            ProjectIdentityOutcome.MultipleProjects => ProjectIdentityReadResult.Failed(
                ProjectIdentityFailure.MultipleProjects,
                "数据库中存在多个项目身份，违反一库一项目规则。"),
            _ => ProjectIdentityReadResult.Failed(
                ProjectIdentityFailure.Invalid,
                "项目身份格式不可读（UUID 解析或字段校验失败）。"),
        });
    }
}

/// <summary>
/// AF-02 端口 IMapQueryService 的正式 Core Domain 适配（CD-01 §5.3）。
/// 会话绑定：构造时持有本会话的包数据库，查询始终按 Project 范围隔离；
/// 跨 Project 或不存在的 Map 一律返回 null，不区分原因（不泄露其他 Project 的存在性）。
/// </summary>
public sealed class CoreDomainMapQueryService : IMapQueryService
{
    private readonly SqliteCoreDomainDatabase _core;

    public CoreDomainMapQueryService(IPackageDatabase sessionDatabase)
    {
        _core = new SqliteCoreDomainDatabase(sessionDatabase);
    }

    public Task<IReadOnlyList<MapSummary>> ListMapsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summaries = _core.ListMaps(projectId)
            .Select(m => new MapSummary { MapId = m.MapId, DisplayName = m.MapName })
            .ToArray();
        return Task.FromResult<IReadOnlyList<MapSummary>>(summaries);
    }

    public Task<MapSummary?> GetMapAsync(Guid projectId, Guid mapId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = _core.FindMap(projectId, mapId);
        return Task.FromResult(map is null
            ? null
            : new MapSummary { MapId = map.MapId, DisplayName = map.MapName });
    }
}

/// <summary>
/// WP-20a 端口 IMapCreationService 的正式 Core Domain 适配：会话绑定（构造时持有
/// 本会话的包数据库）→ 调用正式 Domain <see cref="MapService"/>（创建规则与名称
/// 唯一性不复制）→ 单事务持久化（失败整体回滚，不留半创建 Map）。
/// 领域冲突（同名地图）映射为 DuplicateName 失败结果；输入空白映射为 InvalidName。
/// </summary>
public sealed class CoreDomainMapCreationService : IMapCreationService
{
    private readonly SqliteCoreDomainDatabase _core;
    private readonly TimeProvider _timeProvider;

    public CoreDomainMapCreationService(IPackageDatabase sessionDatabase, TimeProvider? timeProvider = null)
    {
        _core = new SqliteCoreDomainDatabase(sessionDatabase);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<MapCreationResult> CreateAsync(MapCreationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var plan = MapService.CreateMap(
                _core,
                request.ProjectId,
                request.MapName,
                _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());

            return Task.FromResult(MapCreationResult.Ok(new MapSummary
            {
                MapId = plan.Map.MapId,
                DisplayName = plan.Map.MapName,
            }));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return Task.FromResult(MapCreationResult.Failed(
                MapCreationFailureKind.InvalidName,
                "地图名不能为空白。"));
        }
        catch (CoreDomainConflictException ex)
        {
            return Task.FromResult(MapCreationResult.Failed(
                MapCreationFailureKind.DuplicateName,
                ex.Message));
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or CoreDomainDataException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            // 含会话关闭后的 ObjectDisposedException（InvalidOperationException 派生）：
            // 会话已释放时同样以失败结果拒绝，不留局部状态。
            return Task.FromResult(MapCreationResult.Failed(
                MapCreationFailureKind.CreationFailed,
                $"地图创建失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// AF-02 端口 IWorkspaceContextResolver 的正式 Core Domain 适配（CD-01 §5.4）：
/// 验证 Map → 解析 Branch 世系（环/跨 Map/悬空拒绝）→ 解析规范历史时间，
/// 规则全部由 Domain（WorkspaceContextRules）拥有；适配器只做结果映射。
/// 会话绑定：构造时持有本会话的包数据库。
/// </summary>
public sealed class CoreDomainWorkspaceContextResolver : IWorkspaceContextResolver
{
    private readonly SqliteCoreDomainDatabase _core;

    public CoreDomainWorkspaceContextResolver(IPackageDatabase sessionDatabase)
    {
        _core = new SqliteCoreDomainDatabase(sessionDatabase);
    }

    public Task<WorkspaceContextResolution> ResolveAsync(
        Guid projectId,
        Guid mapId,
        Guid? branchId,
        string? historicalTimeInput,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // CD-01-F1：解析的多次读取（Map → 根 History → Branch 世系）置于统一临界区
        // （InSnapshotScope，Monitor 同线程可重入），对外为单一原子快照。
        string? error = null;
        var context = _core.InSnapshotScope(core =>
        {
            var ok = WorkspaceContextRules.TryResolve(
                core, projectId, mapId, branchId, historicalTimeInput, out var resolved, out var failure);
            error = failure;
            return ok ? resolved : null;
        });

        if (context is null)
        {
            return Task.FromResult(WorkspaceContextResolution.Failed(error ?? "Workspace 上下文解析失败。"));
        }

        return Task.FromResult(WorkspaceContextResolution.Ok(new ValidatedWorkspaceContext
        {
            MapId = context.MapId,
            BranchId = context.BranchId,
            CanonicalHistoricalTime = context.Time.Canonical(),
        }));
    }
}
