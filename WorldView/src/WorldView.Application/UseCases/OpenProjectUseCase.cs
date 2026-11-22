using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;

namespace WorldView.Application.UseCases;

/// <summary>Open Project 结果类别（AF-02 §4.4）。</summary>
public enum OpenProjectOutcome
{
    /// <summary>会话已建立并达到 Ready。</summary>
    Succeeded,

    /// <summary>包校验失败（InvalidPackage，见 <see cref="Report"/>）。</summary>
    InvalidPackage,

    /// <summary>该包已有会话（进程内禁止双开为两个可写会话）；见 <see cref="ExistingSession"/>。</summary>
    DuplicateOpen,

    /// <summary>
    /// 依赖门槛：Core Domain 项目身份读取服务未实现。包结构本身通过校验，
    /// 但无法按 AF-01 §3.3 第 5 步确认唯一逻辑项目身份；fail-closed，不建立会话。
    /// </summary>
    ProjectIdentityUnavailable,

    /// <summary>建立过程被取消；不产生 Ready 会话。</summary>
    Cancelled,

    /// <summary>建立过程失败（资源已逆序释放）。</summary>
    Failed,
}

/// <summary>Open Project 结果。</summary>
public sealed class OpenProjectResult
{
    private OpenProjectResult()
    {
    }

    public OpenProjectOutcome Outcome { get; private init; }

    /// <summary>就绪的会话（仅 Succeeded 时非 null）。</summary>
    public ProjectSession? Session { get; private init; }

    /// <summary>已存在的会话（DuplicateOpen 时非 null）。</summary>
    public ProjectSession? ExistingSession { get; private init; }

    /// <summary>包校验报告（InvalidPackage 时非 null）。</summary>
    public PackageValidationReport? Report { get; private init; }

    /// <summary>面向用户的诊断说明。</summary>
    public string? Diagnostic { get; private init; }

    public static OpenProjectResult Succeeded(ProjectSession session) => new()
    {
        Outcome = OpenProjectOutcome.Succeeded,
        Session = session,
    };

    public static OpenProjectResult InvalidPackage(PackageValidationReport report) => new()
    {
        Outcome = OpenProjectOutcome.InvalidPackage,
        Report = report,
        Diagnostic = report.ToSummary(),
    };

    public static OpenProjectResult Duplicate(ProjectSession existing) => new()
    {
        Outcome = OpenProjectOutcome.DuplicateOpen,
        ExistingSession = existing,
        Diagnostic = "该项目包已在本应用中打开。",
    };

    public static OpenProjectResult Gate(OpenProjectOutcome outcome, string diagnostic) => new()
    {
        Outcome = outcome,
        Diagnostic = diagnostic,
    };
}

/// <summary>
/// Open Project 用例（AF-01 §3.3/§5/§8.1）：分阶段校验 → 注册表占位（防双开）→
/// 建立会话（打开数据库 → bootstrap 校验/迁移 → 身份读取）→ Ready。
/// 任何失败按逆序释放已获取资源并从注册表移除占位；部分建立的会话绝不暴露为 Ready。
/// </summary>
public sealed class OpenProjectUseCase
{
    private readonly IFileSystem _fileSystem;
    private readonly IPackageDatabaseFactory _databaseFactory;
    private readonly ApplicationBootstrapper _bootstrapper;
    private readonly IProjectIdentityReader? _identityReader;
    private readonly IAssetStoreFactory _assetStoreFactory;
    private readonly ProjectSessionRegistry _registry;
    private readonly IAppLog? _log;
    private readonly IProjectFieldPayloadRecovery? _fieldPayloadRecovery;

    public OpenProjectUseCase(
        IFileSystem fileSystem,
        IPackageDatabaseFactory databaseFactory,
        ApplicationBootstrapper bootstrapper,
        IAssetStoreFactory assetStoreFactory,
        ProjectSessionRegistry registry,
        IProjectIdentityReader? identityReader,
        IAppLog? log = null,
        IProjectFieldPayloadRecovery? fieldPayloadRecovery = null)
    {
        _fileSystem = fileSystem;
        _databaseFactory = databaseFactory;
        _bootstrapper = bootstrapper;
        _assetStoreFactory = assetStoreFactory;
        _registry = registry;
        _identityReader = identityReader;
        _log = log;
        _fieldPayloadRecovery = fieldPayloadRecovery;
    }

    public async Task<OpenProjectResult> ExecuteAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        // ---- 0. 注册表占位（先于校验，防并发双开）----
        var canonical = ProjectPackageStructure.NormalizePackagePath(packagePath);
        var existing = _registry.FindByPackagePath(canonical);
        if (existing is not null)
        {
            return OpenProjectResult.Duplicate(existing);
        }

        // ---- 1. 分阶段包校验（只读，fail-closed）----
        var validator = new ProjectPackageValidator(
            _fileSystem, _databaseFactory, _bootstrapper, _identityReader, _log);
        PackageValidationOutcome validation;
        try
        {
            validation = await validator.ValidateAsync(canonical, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return OpenProjectResult.Gate(OpenProjectOutcome.Cancelled, "打开已取消。");
        }

        if (!validation.IsValid || validation.Manifest is null)
        {
            return OpenProjectResult.InvalidPackage(validation.Report);
        }

        if (validation.Report.HasDependencyGates)
        {
            // 门槛如实报告：包结构通过校验，但身份确认依赖尚未实现的 Core Domain 服务。
            var issue = validation.Report.Issues[0];
            var outcome = issue.Code == PackageIssueCode.ProjectIdentityUnavailable
                ? OpenProjectOutcome.ProjectIdentityUnavailable
                : OpenProjectOutcome.Failed;
            return OpenProjectResult.Gate(outcome, issue.Message);
        }

        // ---- 2. 占位 + 建立会话 ----
        var manifest = validation.Manifest;
        var dbPath = ProjectPackageStructure.DatabasePath(canonical, manifest);
        var database = _databaseFactory.CreateForFile(dbPath);
        var assetStore = _assetStoreFactory.CreateForPackage(canonical);
        var session = new ProjectSession(
            Guid.NewGuid(), canonical, manifest, database, assetStore, projectId: null);

        if (!_registry.TryRegister(session))
        {
            // 并发双开：另一个 Open 已先占位。清理本方刚创建的资源后返回重复结果。
            await database.DisposeAsync().ConfigureAwait(false);
            await assetStore.DisposeAsync().ConfigureAwait(false);
            return _registry.FindByPackagePath(canonical) is { } winner
                ? OpenProjectResult.Duplicate(winner)
                : OpenProjectResult.Gate(OpenProjectOutcome.Failed, "无法登记会话（并发冲突）。");
        }

        try
        {
            session.TransitionTo(ProjectSessionState.Validating);
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await database.IntegrityCheckAsync(cancellationToken).ConfigureAwait(false))
            {
                await FailAndReleaseAsync(session, ProjectSessionState.Failed, "数据库完整性检查未通过。");
                return OpenProjectResult.Gate(OpenProjectOutcome.Failed, "数据库完整性检查未通过。");
            }

            session.TransitionTo(ProjectSessionState.Migrating);
            var bootstrap = await _bootstrapper.ValidateExistingDatabaseAsync(database, cancellationToken)
                .ConfigureAwait(false);
            if (!bootstrap.Success)
            {
                var message = bootstrap.Issues.Count > 0 ? bootstrap.Issues[0].Message : "bootstrap 校验失败。";
                await FailAndReleaseAsync(session, ProjectSessionState.Failed, message);
                return OpenProjectResult.Gate(OpenProjectOutcome.Failed, message);
            }

            // ---- 3. 项目身份（Core Domain 边界；运行时未注册时 fail-closed）----
            if (_identityReader is null)
            {
                await FailAndReleaseAsync(session, ProjectSessionState.Failed,
                    "Core Domain 项目身份读取服务尚未实现，无法确认唯一项目身份。");
                return OpenProjectResult.Gate(
                    OpenProjectOutcome.ProjectIdentityUnavailable,
                    "Core Domain 项目身份读取服务尚未实现：包结构与数据库均通过校验，"
                    + "但项目身份确认依赖尚未实现的 Core Domain 读取服务，打开已 fail-closed 停止。");
            }

            var identity = await _identityReader.ReadAsync(database, cancellationToken).ConfigureAwait(false);
            if (!identity.Success || identity.Identity is null)
            {
                var message = identity.Diagnostic ?? "项目身份读取失败。";
                await FailAndReleaseAsync(session, ProjectSessionState.Failed, message);
                return OpenProjectResult.Gate(OpenProjectOutcome.Failed, message);
            }

            session.AttachProjectIdentity(identity.Identity);

            // ---- 4. Field Payload startup reconciliation ----
            if (_fieldPayloadRecovery is not null)
            {
                var payloadReport = await _fieldPayloadRecovery.ReconcileAsync(
                    database, canonical, identity.Identity.ProjectId, cancellationToken).ConfigureAwait(false);
                if (payloadReport.MissingPayloadIds.Count > 0
                    || payloadReport.CorruptPayloadIds.Count > 0
                    || payloadReport.OrphanContentHashes.Count > 0)
                {
                    _log?.Warn(
                        "OpenProject",
                        $"Field Payload reconciliation: missing={payloadReport.MissingPayloadIds.Count}, "
                        + $"corrupt={payloadReport.CorruptPayloadIds.Count}, "
                        + $"orphan-files={payloadReport.OrphanContentHashes.Count}.");
                }
            }

            // ---- 5. Ready（晚完成保护：被并发取消/关闭时不得暴露 Ready）----
            if (!session.TryCompleteOpen())
            {
                await session.DisposeAsync().ConfigureAwait(false);
                _registry.Remove(session);
                return OpenProjectResult.Gate(OpenProjectOutcome.Cancelled, "会话在建立完成前已被关闭或取消。");
            }

            return OpenProjectResult.Succeeded(session);
        }
        catch (OperationCanceledException)
        {
            await FailAndReleaseAsync(session, ProjectSessionState.Cancelled, "打开已取消。");
            return OpenProjectResult.Gate(OpenProjectOutcome.Cancelled, "打开已取消。");
        }
        catch (Exception ex)
        {
            _log?.Error("OpenProject", $"建立会话失败：{canonical}", ex);
            await FailAndReleaseAsync(session, ProjectSessionState.Failed, "建立会话失败。");
            return OpenProjectResult.Gate(OpenProjectOutcome.Failed, $"建立会话失败：{ex.Message}");
        }
    }

    private async Task FailAndReleaseAsync(ProjectSession session, ProjectSessionState state, string diagnostic)
    {
        session.MarkAborted(state);
        await session.DisposeAsync().ConfigureAwait(false);
        _registry.Remove(session);
        _log?.Warn("OpenProject", diagnostic);
    }
}
