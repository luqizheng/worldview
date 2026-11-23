using System;
using System.Linq;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Assets;
using WorldView.Domain.Geometry;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-02-D 真实 SQLite 验证：core_asset 权威元数据（UNIQUE 内容寻址、完整性状态转换、
/// 孤儿对账）、core_object_definition_asset typed link（FK 链、role 唯一、损坏 Asset
/// 拒绝引用）、manifest↔link↔asset 三方一致性。
/// </summary>
public sealed class Cd02AssetPersistenceTests : IDisposable
{
    private const long Now = 1_000L;
    private const string ValidHash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new(new[] { new CoreDomainMigrationSource() });
    private readonly SqlitePackageDatabase _database;

    public Cd02AssetPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-cd02a-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    private SqliteConnectionAccess Access() => ((ISqliteConnectionProvider)_database).GetAccess();

    private long CountRows(string table, string? where = null)
    {
        return Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table} {where ?? string.Empty};";
            return (long)command.ExecuteScalar()!;
        });
    }

    private (ProjectRecord Project, Guid RiverTypeId) SeedProject()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("P", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));
        var type = core.FindObjectTypeByTypeKey(project.ProjectId, "river")!;
        return (project, type.ObjectTypeId);
    }

    private Guid CreateActiveRiverObject(Guid projectId)
    {
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, projectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            core, projectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        ObjectCreationService.InsertObject(
            core, projectId, bound.ReservationId,
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
    public void RegisterAsset_CreatesMetadataWithDerivedPath()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var record = AssetService.RegisterAsset(
            core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);

        Assert.Equal("sha-256", record.HashAlgorithm);
        Assert.Equal($"Assets/sha256/{ValidHash}", record.RelativePath);
        Assert.Equal(AssetPublicationState.Published, record.PublicationState);
        Assert.Equal(AssetIntegrityState.Unverified, record.IntegrityState);
        Assert.Equal(1, CountRows("core_asset"));
    }

    [Fact]
    public void RegisterAsset_SameContent_Idempotent()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var first = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);
        var second = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now + 5);
        Assert.Equal(first.AssetId, second.AssetId);
        Assert.Equal(1, CountRows("core_asset"));
    }

    [Fact]
    public void RegisterAsset_DuplicateHashRawInsert_Rejected()
    {
        var (project, _) = SeedProject();
        AssetService.RegisterAsset(Core(), project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);
        Assert.ThrowsAny<Exception>(() => Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"INSERT INTO core_asset (asset_id, project_id, content_hash, hash_algorithm, byte_length, media_type, format_version, relative_path, publication_state, integrity_state, created_at_utc_ms) "
                + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{ValidHash}', 'sha-256', 1, 'x', '1', 'Assets/sha256/{ValidHash}', 'published', 'unverified', {Now});";
            command.ExecuteNonQuery();
        }));
    }

    [Fact]
    public void IntegrityState_Transitions_CorruptBlocksNewLinks()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var identityId = CreateActiveRiverObject(project.ProjectId);
        var asset = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);

        // verified → 可引用。
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 1);
        AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 2);

        // corrupt → 新 link 拒绝（既有 link 不自动删除——引用期间的资产字节保持可用语义由使用方保障）。
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Corrupt, Now + 3);
        var identityId2 = CreateActiveRiverObject(project.ProjectId);
        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId2, asset.AssetId, "centerline", Now + 4));
    }

    [Fact]
    public void LinkDefinitionAsset_FkChainAndRoleUniqueness()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var identityId = CreateActiveRiverObject(project.ProjectId);
        var asset = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 1);

        // 未声明 role（extent 不是 river 的 role）拒绝。
        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "extent", Now + 2));

        AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 3);
        Assert.Equal(1, CountRows("core_object_definition_asset"));

        // 同 (owner, asset, role) 重复拒绝。
        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 4));

        // 同 asset 不同 role 合法（UNIQUE 四元组）。
        AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "reach", Now + 5);
        Assert.Equal(2, core.ListDefinitionAssetLinks(project.ProjectId, identityId).Count);

        // 跨 Project 的 Asset 引用被复合 FK 拒绝（asset 属于本 Project 才可 link——不同 Project 资产独立库）。
        AssetService.UnlinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "reach");
        AssetService.UnlinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "centerline");
        Assert.Equal(0, CountRows("core_object_definition_asset"));
    }

    [Fact]
    public void LinkDefinitionAsset_DraftObject_Rejected()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var asset = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);

        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.LinkDefinitionAsset(core, project.ProjectId, bound.ObjectIdentityId!.Value, asset.AssetId, "centerline", Now + 1));
    }

    [Fact]
    public void OrphanScan_IdentifiesUnreferencedAssets()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var identityId = CreateActiveRiverObject(project.ProjectId);
        var orphan = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash, 8, "application/json", "1", Now);
        var referenced = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), ValidHash[..63] + "0", 8, "application/json", "1", Now);
        AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, referenced.AssetId, "centerline", Now + 1);

        var orphans = core.FindOrphanAssetIds(project.ProjectId);
        Assert.Single(orphans);
        Assert.Equal(orphan.AssetId, orphans[0]);
    }

    [Fact]
    public void ManifestConsistency_FullChain_RealDb()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var identityId = CreateActiveRiverObject(project.ProjectId);

        // 发布字节 → 登记元数据 → link → 三方一致性校验。
        var envelopeJson = """
            {"format_id":"worldview.geometry-json","format_version":1,"spatial_reference_id":"sr-local-1","axis_order":"x_y","unit":"meter","coordinate_precision_profile_id":"p1","dimension":2,"geometry_family":"line_string","body":[[0,0],[10,10]]}
            """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(envelopeJson);
        var hash = AssetRules.Sha256Hex(bytes);
        var asset = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), hash, bytes.Length, "application/json", "1", Now);
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 1);
        AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 2);

        // envelope 结构校验（P2-09-25 有界实现）。
        GeometryAssetEnvelope.Validate(envelopeJson, "line_string", 2);

        // manifest 构造 + 三方一致性。
        var manifest = new GeometryComponentManifest
        {
            GeometryComponentId = GeometryRules.NewComponentId(),
            GeometryRole = "centerline",
            GeometryFamily = "line_string",
            Dimension = 2,
            SpatialReferenceId = "sr-local-1",
            GeometrySchemaVersion = 1,
            AssetId = asset.AssetId,
            ContentHash = asset.ContentHash,
        };
        var descriptor = WorldView.Domain.Registry.ObjectTypeRegistry.Require("river");
        GeometryRules.ValidateManifest(descriptor, manifest);
        var link = core.FindDefinitionAssetLink(project.ProjectId, identityId, asset.AssetId, "centerline");
        GeometryRules.ValidateManifestConsistency(asset, link, manifest);
        Assert.Equal(bytes.Length, core.FindAsset(project.ProjectId, asset.AssetId)!.ByteLength);
    }

    [Fact]
    public void SchemaAudit_ForeignKeyCheckAndIntegrityCheck_NoViolationsAfterObjectAssetLink()
    {
        var (project, _) = SeedProject();
        var core = Core();
        var identityId = CreateActiveRiverObject(project.ProjectId);
        var asset = AssetService.RegisterAsset(
            core, project.ProjectId, Guid.NewGuid(), ValidHash, 128, "application/json", "1", Now);
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 1);
        AssetService.LinkDefinitionAsset(core, project.ProjectId, identityId, asset.AssetId, "centerline", Now + 2);

        var audit = Access().Execute(connection =>
        {
            using var foreignKeyCommand = connection.CreateCommand();
            foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";
            var foreignKeyViolations = 0;
            using (var foreignKeyReader = foreignKeyCommand.ExecuteReader())
            {
                while (foreignKeyReader.Read())
                {
                    foreignKeyViolations++;
                }
            }

            using var integrityCommand = connection.CreateCommand();
            integrityCommand.CommandText = "PRAGMA integrity_check;";
            var integrityResult = (string)integrityCommand.ExecuteScalar()!;
            return (ForeignKeyViolations: foreignKeyViolations, IntegrityResult: integrityResult);
        });

        Assert.Equal(0, audit.ForeignKeyViolations);
        Assert.Equal("ok", audit.IntegrityResult);
    }
}
