using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;

namespace WorldView.Application.UseCases;

/// <summary>Create Project 结果类别（AF-02 §4.3）。</summary>
public enum CreateProjectOutcome
{
    /// <summary>包已成功创建并发布到最终路径。</summary>
    Succeeded,

    /// <summary>目标路径已存在（绝不覆盖既有包）。</summary>
    DestinationExists,

    /// <summary>输入不合法（名称/路径）。</summary>
    InvalidInput,

    /// <summary>
    /// 已到达 bootstrap 边界：Core Domain 项目创建服务未实现（AF-01 §3.4）。
    /// 未发布任何包；临时目录已清理。这是已登记的依赖门槛，不是程序错误。
    /// </summary>
    CoreDomainProjectCreationUnavailable,

    /// <summary>Core Domain 项目创建服务被调用但返回失败；临时目录已清理。</summary>
    CoreDomainProjectCreationFailed,

    /// <summary>IO/数据库失败；仅清理本次操作创建的临时目录。</summary>
    IoFailure,

    Cancelled,
}

/// <summary>Create Project 结果。</summary>
public sealed class CreateProjectResult
{
    private CreateProjectResult()
    {
    }

    public CreateProjectOutcome Outcome { get; private init; }

    /// <summary>发布后的包根路径（成功时）。</summary>
    public string? PackageRootPath { get; private init; }

    /// <summary>Core Domain 项目身份（成功且服务返回身份时）。</summary>
    public ProjectIdentity? Identity { get; private init; }

    /// <summary>面向用户的诊断说明。</summary>
    public string? Diagnostic { get; private init; }

    public static CreateProjectResult Succeeded(string packageRootPath, ProjectIdentity? identity) => new()
    {
        Outcome = CreateProjectOutcome.Succeeded,
        PackageRootPath = packageRootPath,
        Identity = identity,
    };

    public static CreateProjectResult Failed(CreateProjectOutcome outcome, string diagnostic) => new()
    {
        Outcome = outcome,
        Diagnostic = diagnostic,
    };
}

/// <summary>
/// Create Project 用例（AF-01 §3.4 顺序 / AF-02 §4.3）：
/// 校验目标 → 兄弟临时目录 → manifest + 数据库 bootstrap → Asset Store 初始化 →
/// Core Domain 项目创建（未实现则在此停止并报告门槛）→ 校验 → 同卷原子发布。
/// 失败时只清理本次创建的临时目录，绝不触碰既有用户包。
/// </summary>
public sealed class CreateProjectUseCase
{
    private readonly IFileSystem _fileSystem;
    private readonly IPackageDatabaseFactory _databaseFactory;
    private readonly ApplicationBootstrapper _bootstrapper;
    private readonly IProjectCreationService? _projectCreationService;
    private readonly TimeProvider _timeProvider;
    private readonly IAppLog? _log;

    public CreateProjectUseCase(
        IFileSystem fileSystem,
        IPackageDatabaseFactory databaseFactory,
        ApplicationBootstrapper bootstrapper,
        IProjectCreationService? projectCreationService,
        TimeProvider? timeProvider = null,
        IAppLog? log = null)
    {
        _fileSystem = fileSystem;
        _databaseFactory = databaseFactory;
        _bootstrapper = bootstrapper;
        _projectCreationService = projectCreationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _log = log;
    }

    /// <summary>执行创建。<paramref name="parentDirectory"/> 为目标父目录，<paramref name="packageName"/> 可含或不含 .wvproject 后缀。</summary>
    public async Task<CreateProjectResult> ExecuteAsync(
        string parentDirectory,
        string packageName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        // ---- 1. 输入与目标校验 ----
        if (string.IsNullOrWhiteSpace(parentDirectory) || string.IsNullOrWhiteSpace(packageName))
        {
            return CreateProjectResult.Failed(CreateProjectOutcome.InvalidInput, "父目录或包名缺失。");
        }

        if (packageName.Contains(Path.DirectorySeparatorChar) || packageName.Contains(Path.AltDirectorySeparatorChar))
        {
            return CreateProjectResult.Failed(CreateProjectOutcome.InvalidInput, "包名不能包含路径分隔符。");
        }

        displayName = string.IsNullOrWhiteSpace(displayName) ? packageName : displayName.Trim();

        string finalPath;
        try
        {
            var parent = Path.GetFullPath(parentDirectory.Trim());
            var name = ProjectPackageStructure.WithPackageSuffix(packageName.Trim());
            finalPath = ProjectPackageStructure.NormalizePackagePath(Path.Combine(parent, name));
        }
        catch (Exception ex)
        {
            _log?.Warn("CreateProject", "路径规范化失败。", ex);
            return CreateProjectResult.Failed(CreateProjectOutcome.InvalidInput, "目标路径不合法。");
        }

        if (!finalPath.EndsWith(ProjectPackageFormat.PackageSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return CreateProjectResult.Failed(CreateProjectOutcome.InvalidInput, "包名必须以 .wvproject 结尾。");
        }

        if (!_fileSystem.DirectoryExists(Path.GetDirectoryName(finalPath)!))
        {
            return CreateProjectResult.Failed(CreateProjectOutcome.InvalidInput, "目标父目录不存在。");
        }

        if (_fileSystem.DirectoryExists(finalPath) || _fileSystem.FileExists(finalPath))
        {
            return CreateProjectResult.Failed(CreateProjectOutcome.DestinationExists, "目标路径已存在，创建已取消（不覆盖既有内容）。");
        }

        // ---- 2. 兄弟临时目录 ----
        var now = _timeProvider.GetUtcNow();
        var tempPath = ProjectPackageStructure.NormalizePackagePath(
            Path.Combine(Path.GetDirectoryName(finalPath)!, $"{Path.GetFileName(finalPath)}.tmp-{Guid.NewGuid():N}"));
        _fileSystem.CreateDirectory(tempPath);

        try
        {
            var result = await BuildPackageAsync(tempPath, finalPath, now, displayName, cancellationToken).ConfigureAwait(false);

            // 任何未发布路径（未到达 MoveDirectory）都必须只清理本次创建的临时目录。
            if (result.Outcome != CreateProjectOutcome.Succeeded)
            {
                SafeCleanupTemp(tempPath);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            SafeCleanupTemp(tempPath);
            return CreateProjectResult.Failed(CreateProjectOutcome.Cancelled, "创建已取消。");
        }
        catch (Exception ex)
        {
            _log?.Error("CreateProject", $"创建项目包失败：{finalPath}", ex);
            SafeCleanupTemp(tempPath);
            return CreateProjectResult.Failed(CreateProjectOutcome.IoFailure, $"创建失败：{ex.Message}");
        }
    }

    /// <summary>在临时目录内构建包内容；发布成功前不产生任何最终路径副作用。</summary>
    private async Task<CreateProjectResult> BuildPackageAsync(
        string tempPath,
        string finalPath,
        DateTimeOffset now,
        string displayName,
        CancellationToken cancellationToken)
    {
        // ---- 3. manifest ----
        var manifest = PackageManifest.CreateV1(now);
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(tempPath), manifest.ToJson());

        // ---- 4. 数据库 bootstrap ----
        var dbPath = ProjectPackageStructure.DatabasePath(tempPath, manifest);
        await using (var database = _databaseFactory.CreateForFile(dbPath))
        {
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            var bootstrap = await _bootstrapper.InitializeNewDatabaseAsync(database, now, cancellationToken)
                .ConfigureAwait(false);
            if (!bootstrap.Success)
            {
                return CreateProjectResult.Failed(
                    CreateProjectOutcome.IoFailure,
                    bootstrap.Issues.Count > 0 ? bootstrap.Issues[0].Message : "数据库初始化失败。");
            }
        }
        // 数据库句柄已释放，满足后续同卷目录重命名的 Windows 句柄要求。

        // ---- 5. Asset Store 目录结构 ----
        _fileSystem.CreateDirectory(ProjectPackageStructure.AssetHashRoot(tempPath));
        _fileSystem.CreateDirectory(ProjectPackageStructure.StagingRoot(tempPath));
        _fileSystem.CreateDirectory(ProjectPackageStructure.BackupsRoot(tempPath));

        // ---- 6. Core Domain 项目创建（门槛） ----
        if (_projectCreationService is null)
        {
            // AF-01 §3.4：核心创建服务未实现时停在 bootstrap 边界并报告阻塞项；
            // 不手工插入 Project 行、不造替代 schema。发布环节不执行，临时目录由调用方清理。
            return CreateProjectResult.Failed(
                CreateProjectOutcome.CoreDomainProjectCreationUnavailable,
                "Core Domain 项目创建服务尚未实现：包结构已就绪但项目身份无法经正式领域边界建立，"
                + "本操作已在 bootstrap 边界停止，未生成任何包文件。");
        }

        var creation = await _projectCreationService.CreateAsync(
            new ProjectCreationRequest
            {
                PackageRootPath = tempPath,
                DatabaseRelativePath = manifest.DatabaseRelativePath,
                DisplayName = displayName,
            },
            cancellationToken).ConfigureAwait(false);
        if (!creation.Success)
        {
            return CreateProjectResult.Failed(
                CreateProjectOutcome.CoreDomainProjectCreationFailed,
                creation.Diagnostic ?? "Core Domain 项目创建失败。");
        }

        // ---- 7. 发布（同卷原子重命名；目标竞态时清理临时目录） ----
        if (_fileSystem.DirectoryExists(finalPath) || _fileSystem.FileExists(finalPath))
        {
            return CreateProjectResult.Failed(CreateProjectOutcome.DestinationExists, "目标路径已存在（并发创建），创建已取消。");
        }

        _fileSystem.MoveDirectory(tempPath, finalPath);
        return CreateProjectResult.Succeeded(finalPath, creation.Identity);
    }

    /// <summary>只清理本次操作创建的临时目录；绝不触碰任何既有路径。</summary>
    private void SafeCleanupTemp(string tempPath)
    {
        try
        {
            // 仅当路径仍是我们创建的临时形态（含 .tmp- 前缀标记）才删除。
            if (tempPath.Contains(".tmp-", StringComparison.Ordinal))
            {
                _fileSystem.DeleteDirectory(tempPath);
            }
        }
        catch (Exception ex)
        {
            _log?.Warn("CreateProject", $"清理临时目录失败（保留现场）：{tempPath}", ex);
        }
    }
}
