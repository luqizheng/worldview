using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;

namespace WorldView.Application.Packaging;

/// <summary>包校验结果：报告 + （有效时）解析出的 manifest。</summary>
public sealed class PackageValidationOutcome
{
    public required PackageValidationReport Report { get; init; }

    public PackageManifest? Manifest { get; init; }

    public bool IsValid => !Report.HasFailures;
}

/// <summary>
/// Project Package 分阶段校验器（AF-01 §3.3）：
/// 包形状 → manifest → 数据库 → bootstrap/ledger → Asset 根 → 项目身份。
/// 只读检查，不修复、不重写包；不支持的更高版本拒绝对其写入。
/// 项目身份读取依赖 Core Domain 读取服务：未注册时登记为依赖门槛（DependencyGate），
/// 不伪造“包有效”的结论。
/// </summary>
public sealed class ProjectPackageValidator
{
    private readonly IFileSystem _fileSystem;
    private readonly IPackageDatabaseFactory _databaseFactory;
    private readonly ApplicationBootstrapper _bootstrapper;
    private readonly IProjectIdentityReader? _identityReader;
    private readonly IAppLog? _log;

    public ProjectPackageValidator(
        IFileSystem fileSystem,
        IPackageDatabaseFactory databaseFactory,
        ApplicationBootstrapper bootstrapper,
        IProjectIdentityReader? identityReader,
        IAppLog? log = null)
    {
        _fileSystem = fileSystem;
        _databaseFactory = databaseFactory;
        _bootstrapper = bootstrapper;
        _identityReader = identityReader;
        _log = log;
    }

    public async Task<PackageValidationOutcome> ValidateAsync(
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        var report = new PackageValidationReport();

        // ---- 阶段 1：目录形状 ----
        var root = ProjectPackageStructure.NormalizePackagePath(packageRoot);
        const string shapeStage = "package-shape";
        if (!root.EndsWith(ProjectPackageFormat.PackageSuffix, StringComparison.OrdinalIgnoreCase))
        {
            report.Add(PackageIssue.Failure(
                PackageIssueCode.PackageSuffixMissing, shapeStage,
                $"路径不是 {ProjectPackageFormat.PackageSuffix} 包目录。"));
            return new PackageValidationOutcome { Report = report };
        }

        if (!_fileSystem.DirectoryExists(root))
        {
            report.Add(PackageIssue.Failure(
                PackageIssueCode.PackageDirectoryMissing, shapeStage, "包目录不存在。"));
            return new PackageValidationOutcome { Report = report };
        }

        // ---- 阶段 2：manifest ----
        const string manifestStage = "manifest";
        var manifestPath = ProjectPackageStructure.ManifestPath(root);
        if (!_fileSystem.FileExists(manifestPath))
        {
            report.Add(PackageIssue.Failure(PackageIssueCode.ManifestMissing, manifestStage, "manifest.json 缺失。"));
            return new PackageValidationOutcome { Report = report };
        }

        string manifestJson;
        try
        {
            manifestJson = _fileSystem.ReadAllText(manifestPath);
        }
        catch (Exception ex)
        {
            _log?.Warn("PackageValidator", $"读取 manifest 失败：{manifestPath}", ex);
            report.Add(PackageIssue.Failure(PackageIssueCode.ManifestMalformed, manifestStage, "manifest.json 无法读取。"));
            return new PackageValidationOutcome { Report = report };
        }

        var parse = PackageManifest.TryParse(manifestJson);
        if (!parse.Success || parse.Manifest is null)
        {
            report.Add(PackageIssue.Failure(
                parse.FailureCode == PackageIssueCode.ManifestUnsupportedVersion
                    ? PackageIssueCode.ManifestUnsupportedVersion
                    : PackageIssueCode.ManifestMalformed,
                manifestStage,
                parse.Diagnostic ?? "manifest.json 解析失败。"));
            return new PackageValidationOutcome { Report = report };
        }

        var manifest = parse.Manifest;

        // ---- 阶段 2b：manifest 声明的数据库路径必须 confined ----
        var dbRelative = manifest.DatabaseRelativePath;
        if (System.IO.Path.IsPathRooted(dbRelative))
        {
            report.Add(PackageIssue.Failure(PackageIssueCode.ManifestPathAbsolute, manifestStage, "manifest 数据库路径为绝对路径，拒绝。"));
            return new PackageValidationOutcome { Report = report };
        }

        if (dbRelative.Contains("..", StringComparison.Ordinal))
        {
            report.Add(PackageIssue.Failure(PackageIssueCode.ManifestPathTraversal, manifestStage, "manifest 数据库路径包含路径遍历片段，拒绝。"));
            return new PackageValidationOutcome { Report = report };
        }

        if (!ProjectPackageStructure.IsRelativePathConfinedToPackage(root, dbRelative))
        {
            report.Add(PackageIssue.Failure(PackageIssueCode.ManifestPathOutsidePackage, manifestStage, "manifest 数据库路径逃逸包根，拒绝。"));
            return new PackageValidationOutcome { Report = report };
        }

        // ---- 阶段 3：数据库文件 ----
        const string databaseStage = "database";
        var dbPath = ProjectPackageStructure.DatabasePath(root, manifest);
        if (!_fileSystem.FileExists(dbPath))
        {
            report.Add(PackageIssue.Failure(PackageIssueCode.DatabaseMissing, databaseStage, "数据库文件缺失。"));
            return new PackageValidationOutcome { Report = report };
        }

        await using var database = _databaseFactory.CreateForFile(dbPath);
        try
        {
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await database.IntegrityCheckAsync(cancellationToken).ConfigureAwait(false))
            {
                report.Add(PackageIssue.Failure(PackageIssueCode.DatabaseCorrupt, databaseStage, "SQLite 完整性检查未通过。"));
                return new PackageValidationOutcome { Report = report };
            }

            // ---- 阶段 4：bootstrap 元数据与迁移 ledger（fail-closed） ----
            var bootstrap = await _bootstrapper.ValidateExistingDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
            foreach (var issue in bootstrap.Issues)
            {
                report.Add(issue);
            }

            if (bootstrap.Issues.Count > 0)
            {
                return new PackageValidationOutcome { Report = report };
            }

            // ---- 阶段 5：Asset Store 根（既有包必须存在；内容完整性按惰性策略另行检查） ----
            if (!_fileSystem.DirectoryExists(ProjectPackageStructure.AssetHashRoot(root)))
            {
                report.Add(PackageIssue.Failure(PackageIssueCode.AssetStoreRootMissing, "assets", "Asset Store 根目录缺失。"));
            }

            // ---- 阶段 6：项目身份（Core Domain 边界） ----
            const string identityStage = "project-identity";
            if (_identityReader is null)
            {
                report.Add(PackageIssue.DependencyGate(
                    PackageIssueCode.ProjectIdentityUnavailable,
                    identityStage,
                    "Core Domain 项目身份读取服务尚未实现，无法确认数据库内的唯一项目身份。"));
            }
            else
            {
                var identity = await _identityReader.ReadAsync(database, cancellationToken).ConfigureAwait(false);
                if (!identity.Success)
                {
                    report.Add(identity.Failure switch
                    {
                        ProjectIdentityFailure.NotEstablished => PackageIssue.Failure(
                            PackageIssueCode.ProjectIdentityNotEstablished, identityStage,
                            identity.Diagnostic ?? "数据库内不存在已建立的项目身份。"),
                        ProjectIdentityFailure.MultipleProjects => PackageIssue.Failure(
                            PackageIssueCode.ProjectIdentityMultiple, identityStage,
                            identity.Diagnostic ?? "数据库内存在多个逻辑项目，违反一库一项目规则。"),
                        _ => PackageIssue.Failure(
                            PackageIssueCode.ProjectIdentityInvalid, identityStage,
                            identity.Diagnostic ?? "项目身份不可读或未通过校验。"),
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Error("PackageValidator", $"数据库校验异常：{dbPath}", ex);
            report.Add(PackageIssue.Failure(PackageIssueCode.DatabaseCorrupt, databaseStage, "数据库打开或校验失败。"));
        }

        return new PackageValidationOutcome { Report = report, Manifest = manifest };
    }
}
