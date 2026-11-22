using System;
using System.IO;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using Xunit;

namespace WorldView.Application.Tests.Packaging;

/// <summary>包校验器测试（内存文件系统 + 假数据库）。</summary>
public class ProjectPackageValidatorTests : IDisposable
{
    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FakePackageDatabaseFactory _databaseFactory = new();
    private readonly ApplicationBootstrapper _bootstrapper;
    private readonly string _packageRoot;

    public ProjectPackageValidatorTests()
    {
        _bootstrapper = new ApplicationBootstrapper(Array.Empty<IMigrationSource>());
        _packageRoot = Path.Combine(Path.GetTempPath(), $"wv-validator-{Guid.NewGuid():N}.wvproject");
    }

    public void Dispose()
    {
        _fileSystem.DeleteDirectory(_packageRoot);
    }

    private ProjectPackageValidator CreateValidator(IProjectIdentityReader? identityReader = null)
        => new(_fileSystem, _databaseFactory, _bootstrapper, identityReader);

    /// <summary>在内存文件系统内构造一个通过 bootstrap 初始化的包（不含身份服务）。</summary>
    private async Task BuildValidPackageAsync()
    {
        _fileSystem.CreateDirectory(_packageRoot);
        var manifest = PackageManifest.CreateV1(DateTimeOffset.UtcNow);
        _fileSystem.WriteAllText(
            ProjectPackageStructure.ManifestPath(_packageRoot), manifest.ToJson());
        var dbPath = ProjectPackageStructure.DatabasePath(_packageRoot, manifest);
        // 假数据库在文件系统层以占位文件表示（真实文件行为在 Infrastructure.Tests 覆盖）。
        _fileSystem.WriteAllText(dbPath, string.Empty);
        _fileSystem.CreateDirectory(ProjectPackageStructure.AssetHashRoot(_packageRoot));
        await using var database = _databaseFactory.CreateForFile(dbPath);
        await database.OpenAsync();
        var result = await _bootstrapper.InitializeNewDatabaseAsync(database, DateTimeOffset.UtcNow);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidPackage_Accepted_WithIdentityGate()
    {
        await BuildValidPackageAsync();

        var outcome = await CreateValidator(identityReader: null).ValidateAsync(_packageRoot);

        Assert.False(outcome.Report.HasFailures);
        Assert.True(outcome.Report.HasDependencyGates);
        Assert.Equal(PackageIssueCode.ProjectIdentityUnavailable, outcome.Report.Issues[0].Code);
        Assert.NotNull(outcome.Manifest);
    }

    [Fact]
    public async Task ValidPackage_Accepted_WhenIdentityReaderSucceeds()
    {
        await BuildValidPackageAsync();
        var reader = new FakeProjectIdentityReader();

        var outcome = await CreateValidator(reader).ValidateAsync(_packageRoot);

        Assert.False(outcome.Report.HasFailures);
        Assert.False(outcome.Report.HasDependencyGates);
    }

    [Fact]
    public async Task MissingSuffix_Rejected()
    {
        var noSuffix = Path.Combine(Path.GetTempPath(), $"wv-nosuffix-{Guid.NewGuid():N}");
        _fileSystem.CreateDirectory(noSuffix);

        var outcome = await CreateValidator().ValidateAsync(noSuffix);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.PackageSuffixMissing, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task MissingDirectory_Rejected()
    {
        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.PackageDirectoryMissing, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task MissingManifest_Rejected()
    {
        _fileSystem.CreateDirectory(_packageRoot);

        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.ManifestMissing, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task MalformedManifest_Rejected()
    {
        _fileSystem.CreateDirectory(_packageRoot);
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(_packageRoot), "{ broken");

        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.ManifestMalformed, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task ManifestPathTraversal_Rejected()
    {
        _fileSystem.CreateDirectory(_packageRoot);
        var json = "{\"packageFormatVersion\":1,\"database\":{\"relativePath\":\"..\\\\evil.sqlite\"},\"createdAtUtc\":\"2026-11-21T00:00:00Z\"}";
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(_packageRoot), json);

        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.ManifestPathTraversal, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task MissingDatabase_Rejected()
    {
        _fileSystem.CreateDirectory(_packageRoot);
        var manifest = PackageManifest.CreateV1(DateTimeOffset.UtcNow);
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(_packageRoot), manifest.ToJson());
        // 不创建数据库文件。

        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.DatabaseMissing, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task MissingBootstrapMetadata_FailsClosed()
    {
        _fileSystem.CreateDirectory(_packageRoot);
        var manifest = PackageManifest.CreateV1(DateTimeOffset.UtcNow);
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(_packageRoot), manifest.ToJson());
        // 数据库文件存在但从未 bootstrap（内存替身无元数据）。
        _fileSystem.WriteAllText(
            ProjectPackageStructure.DatabasePath(_packageRoot, manifest), string.Empty);
        _databaseFactory.CreateForFile(ProjectPackageStructure.DatabasePath(_packageRoot, manifest));

        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.BootstrapMetadataMissing, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task MissingAssetStoreRoot_Rejected()
    {
        // 构造含数据库文件与 bootstrap 的包，但不创建 Asset 目录。
        _fileSystem.CreateDirectory(_packageRoot);
        var manifest = PackageManifest.CreateV1(DateTimeOffset.UtcNow);
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(_packageRoot), manifest.ToJson());
        var dbPath = ProjectPackageStructure.DatabasePath(_packageRoot, manifest);
        _fileSystem.WriteAllText(dbPath, string.Empty);
        await using (var database = _databaseFactory.CreateForFile(dbPath))
        {
            await database.OpenAsync();
            await _bootstrapper.InitializeNewDatabaseAsync(database, DateTimeOffset.UtcNow);
        }

        var outcome = await CreateValidator(new FakeProjectIdentityReader()).ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.AssetStoreRootMissing, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task NewerDatabaseFormat_FailsClosed()
    {
        _fileSystem.CreateDirectory(_packageRoot);
        var manifest = PackageManifest.CreateV1(DateTimeOffset.UtcNow);
        _fileSystem.WriteAllText(ProjectPackageStructure.ManifestPath(_packageRoot), manifest.ToJson());
        _fileSystem.CreateDirectory(ProjectPackageStructure.AssetHashRoot(_packageRoot));
        var dbPath = ProjectPackageStructure.DatabasePath(_packageRoot, manifest);
        _fileSystem.WriteAllText(dbPath, string.Empty);
        await using (var database = (FakePackageDatabase)_databaseFactory.CreateForFile(dbPath))
        {
            await database.OpenAsync();
            await _bootstrapper.InitializeNewDatabaseAsync(database, DateTimeOffset.UtcNow);
            database.Metadata = new BootstrapMetadata { DatabaseFormatVersion = 2, CreatedAtUtc = DateTimeOffset.UtcNow };
        }

        var outcome = await CreateValidator().ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.DatabaseFormatUnsupported, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task IdentityReaderNotEstablished_FailsClosed()
    {
        await BuildValidPackageAsync();
        _fileSystem.CreateDirectory(ProjectPackageStructure.AssetHashRoot(_packageRoot));
        var reader = new FakeProjectIdentityReader
        {
            Result = ProjectIdentityReadResult.Failed(ProjectIdentityFailure.NotEstablished, "无项目身份。"),
        };

        var outcome = await CreateValidator(reader).ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.ProjectIdentityNotEstablished, outcome.Report.FirstFailureCode);
    }

    [Fact]
    public async Task IdentityReaderMultipleProjects_FailsClosed()
    {
        await BuildValidPackageAsync();
        _fileSystem.CreateDirectory(ProjectPackageStructure.AssetHashRoot(_packageRoot));
        var reader = new FakeProjectIdentityReader
        {
            Result = ProjectIdentityReadResult.Failed(ProjectIdentityFailure.MultipleProjects, "多于一个项目。"),
        };

        var outcome = await CreateValidator(reader).ValidateAsync(_packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.ProjectIdentityMultiple, outcome.Report.FirstFailureCode);
    }
}
