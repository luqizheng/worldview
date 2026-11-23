using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WorldView.Application.Bootstrap;
using WorldView.Application.UseCases;
using WorldView.Domain.Assets;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Assets;

/// <summary>
/// G2 真实字节验证：hash/length 来自实际文件字节；Verified 只能由验证用例写入，
/// 缺失/长度不符/hash 不符/读取失败分别产生明确结果；过期验证结果不能继续生效。
/// 全部使用真实临时 SQLite 与临时文件。
/// </summary>
public sealed class AssetIntegrityVerificationTests : IDisposable
{
    private const long Now = 1_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();
    private readonly SqlitePackageDatabase _database;
    private readonly FileAssetStore _store;
    private readonly SqliteCoreDomainDatabase _core;

    public AssetIntegrityVerificationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-g2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _core = new SqliteCoreDomainDatabase(_database);
        _store = new FileAssetStore(_root);
    }

    public void Dispose()
    {
        _store.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private VerifyAssetIntegrityUseCase Verifier() => new(_core, _store);

    private ProjectRecord SeedProject()
    {
        var project = ProjectRules.CreateProject("G2", Now);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        return project;
    }

    private AssetRecord RegisterWithRealBytes(Guid projectId, byte[] bytes)
    {
        var put = _store.PutAsync(new MemoryStream(bytes)).GetAwaiter().GetResult();
        Assert.True(put.Success, put.Diagnostic);
        return AssetService.RegisterAsset(
            _core, projectId, Guid.NewGuid(), put.ContentHash!, bytes.LongLength, "application/json", "1", Now);
    }

    private Guid CreateActiveRiverObject(Guid projectId)
    {
        var allocation = ObjectIdReservationService.ReserveByTypeKey(_core, projectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            _core, projectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        ObjectCreationService.InsertObject(
            _core, projectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = projectId,
                ObjectIdentityId = bound.ObjectIdentityId!.Value,
                DefinitionSchemaVersion = 2,
                DefinitionPayloadJson = """{"canonical_name":"Demo","river_class":"other"}""",
                DefinitionHash = string.Empty,
                UpdatedAtUtcMs = Now,
            },
            null, null, Now);
        return bound.ObjectIdentityId.Value;
    }

    [Fact]
    public async Task RealBytes_VerifiesAndMarksVerified()
    {
        var project = SeedProject();
        var bytes = "real geometry bytes"u8.ToArray();
        var asset = RegisterWithRealBytes(project.ProjectId, bytes);

        var result = await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Verified, result.Outcome);
        Assert.Equal(asset.ContentHash, result.ExpectedHash);
        Assert.Equal(asset.ContentHash, result.ActualHash);
        Assert.Equal(AssetIntegrityState.Verified, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task MissingFile_RecordsMissing()
    {
        var project = SeedProject();
        var asset = AssetService.RegisterAsset(
            _core, project.ProjectId, Guid.NewGuid(), new string('a', 64), 10, "application/json", "1", Now);

        var result = await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Missing, result.Outcome);
        Assert.Equal(AssetIntegrityState.Missing, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task LengthMismatch_RecordsCorrupt()
    {
        var project = SeedProject();
        var bytes = "length-sensitive"u8.ToArray();
        var put = _store.PutAsync(new MemoryStream(bytes)).GetAwaiter().GetResult();
        var asset = AssetService.RegisterAsset(
            _core, project.ProjectId, Guid.NewGuid(), put.ContentHash!, bytes.Length + 1, "application/json", "1", Now);

        var result = await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Corrupt, result.Outcome);
        Assert.Equal(AssetIntegrityState.Corrupt, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task HashMismatch_RecordsCorrupt()
    {
        var project = SeedProject();
        var expectedBytes = "expected-bytes"u8.ToArray();
        var otherBytes = "tampered-bytes"u8.ToArray();
        Assert.Equal(expectedBytes.Length, otherBytes.Length);

        var expectedHash = AssetRules.Sha256Hex(expectedBytes);
        var asset = AssetService.RegisterAsset(
            _core, project.ProjectId, Guid.NewGuid(), expectedHash, expectedBytes.Length, "application/json", "1", Now);
        await File.WriteAllBytesAsync(Path.Combine(_store.RootPath, expectedHash), otherBytes);

        var result = await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Corrupt, result.Outcome);
        Assert.NotEqual(expectedHash, result.ActualHash);
        Assert.Equal(AssetIntegrityState.Corrupt, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task BytesChangedAfterVerification_StaleEvidenceBecomesCorrupt()
    {
        var project = SeedProject();
        var good = "good-bytes"u8.ToArray();
        var asset = RegisterWithRealBytes(project.ProjectId, good);

        Assert.Equal(AssetIntegrityVerificationOutcome.Verified,
            (await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId)).Outcome);

        // 外部篡改同一内容寻址路径：再次验证必须使过期验证结果失效。
        await File.WriteAllBytesAsync(Path.Combine(_store.RootPath, asset.ContentHash), "evil-bytes"u8.ToArray());

        var stale = await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);
        Assert.Equal(AssetIntegrityVerificationOutcome.Corrupt, stale.Outcome);
        Assert.Equal(AssetIntegrityState.Corrupt, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task ReadFailure_RecordsCorruptWithDistinctOutcome()
    {
        var project = SeedProject();
        var asset = RegisterWithRealBytes(project.ProjectId, "read-failure"u8.ToArray());

        using (new FileStream(
            Path.Combine(_store.RootPath, asset.ContentHash),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            var result = await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);
            Assert.Equal(AssetIntegrityVerificationOutcome.ReadFailed, result.Outcome);
        }

        Assert.Equal(AssetIntegrityState.Corrupt, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public void UnverifiedAsset_CannotBeReferencedByFormalDefinition()
    {
        var project = SeedProject();
        var asset = RegisterWithRealBytes(project.ProjectId, "not-yet-verified"u8.ToArray());
        var identityId = CreateActiveRiverObject(project.ProjectId);

        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.LinkDefinitionAsset(_core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 1));
        Assert.Null(_core.FindDefinitionAssetLink(project.ProjectId, identityId, asset.AssetId, "centerline"));
    }

    [Fact]
    public async Task VerifiedAsset_CanBeReferencedByFormalDefinition()
    {
        var project = SeedProject();
        var asset = RegisterWithRealBytes(project.ProjectId, "verified-geometry"u8.ToArray());
        var identityId = CreateActiveRiverObject(project.ProjectId);

        await Verifier().ExecuteAsync(project.ProjectId, asset.AssetId);
        var link = AssetService.LinkDefinitionAsset(
            _core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 1);

        Assert.Equal("centerline", link.Role);
        Assert.NotNull(_core.FindDefinitionAssetLink(project.ProjectId, identityId, asset.AssetId, "centerline"));
    }

    [Fact]
    public async Task MarkVerified_WithDigestMeasuredFromOtherBytes_RejectedAndStateUnchanged()
    {
        var project = SeedProject();
        var asset = RegisterWithRealBytes(project.ProjectId, "metadata"u8.ToArray());

        // 摘要真实来自字节测量（不可伪造），但与当前 Asset 元数据不一致 →
        // 写事务内重读元数据拒绝写入，状态保持 Unverified。
        var foreignDigest = await AssetByteDigest.MeasureAsync(new MemoryStream("foreign-bytes"u8.ToArray()));
        Assert.NotEqual(foreignDigest.ContentHash, asset.ContentHash);

        Assert.Throws<CoreDomainConflictException>(() => _core.MarkAssetVerified(
            project.ProjectId,
            asset.AssetId,
            foreignDigest,
            Now + 1));
        Assert.Equal(AssetIntegrityState.Unverified, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public void VerifiedWrite_ApiSurfaceOnlyAcceptsMeasuredDigest()
    {
        // Task 002-A 防伪造边界：Verified 写入端口只接受实测摘要类型，
        // 该类型无公开构造器、唯一公开静态成员是 MeasureAsync——
        // 普通调用方不经真实字节读取就不存在摘要实例。
        var port = typeof(ICoreDomainDatabase).GetMethod(nameof(ICoreDomainDatabase.MarkAssetVerified))!;
        Assert.Equal(
            new[] { typeof(Guid), typeof(Guid), typeof(AssetByteDigest), typeof(long) },
            port.GetParameters().Select(p => p.ParameterType).ToArray());

        Assert.Empty(typeof(AssetByteDigest)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            new[] { nameof(AssetByteDigest.MeasureAsync) },
            typeof(AssetByteDigest)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .ToArray());
    }

    [Fact]
    public void IntegrityFailureApi_CannotWriteVerified()
    {
        var project = SeedProject();
        var asset = RegisterWithRealBytes(project.ProjectId, "api-guard"u8.ToArray());

        Assert.Throws<CoreDomainConflictException>(() => _core.RecordAssetIntegrityFailure(
            project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 1));
        Assert.Throws<CoreDomainConflictException>(() => _core.RecordAssetIntegrityFailure(
            project.ProjectId, asset.AssetId, AssetIntegrityState.Unverified, Now + 2));
        Assert.Equal(AssetIntegrityState.Unverified, _core.FindAsset(project.ProjectId, asset.AssetId)!.IntegrityState);
    }
}
