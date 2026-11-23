using System;
using System.IO;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Assets;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// G3 Building/Cave 原子发布：Definition 与完整 Geometry Asset link set 在同一真实
/// SQLite 事务写入；事务内重读生命周期 / CAS / Asset hash 与 integrity state；
/// 集合不一致或任一步失败时整体回滚，且单条 Link/Unlink 不能绕过该约束。
/// </summary>
public sealed class GeometryAtomicPublicationTests : IDisposable
{
    private const long Now = 1_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();
    private readonly SqlitePackageDatabase _database;
    private readonly SqliteCoreDomainDatabase _core;

    public GeometryAtomicPublicationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-g3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _core = new SqliteCoreDomainDatabase(_database);
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

    private SqliteConnectionAccess Access() => ((ISqliteConnectionProvider)_database).GetAccess();

    private long CountGeometryLinks(Guid projectId, Guid identityId) =>
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM core_object_definition_asset "
                + $"WHERE project_id = '{projectId:D}' AND object_identity_id = '{identityId:D}';";
            return (long)command.ExecuteScalar()!;
        });

    private ProjectRecord SeedProject()
    {
        var project = ProjectRules.CreateProject("G3", Now);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        return project;
    }

    private Guid CreateBuildingObject(Guid projectId, out string definitionHash)
    {
        var allocation = ObjectIdReservationService.ReserveByTypeKey(_core, projectId, "building", null, Now);
        var bound = ObjectIdReservationService.Bind(
            _core, projectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        ObjectCreationService.InsertObject(
            _core, projectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = projectId,
                ObjectIdentityId = bound.ObjectIdentityId!.Value,
                DefinitionSchemaVersion = 1,
                DefinitionPayloadJson = BuildingPayload("Empty Hall"),
                DefinitionHash = string.Empty,
                UpdatedAtUtcMs = Now,
            },
            null, null, Now);
        definitionHash = _core.ReadObjectDefinition(projectId, bound.ObjectIdentityId.Value)!.DefinitionHash;
        return bound.ObjectIdentityId.Value;
    }

    private AssetRecord CreateAsset(Guid projectId, byte[] bytes, bool verified = true)
    {
        var hash = AssetRules.Sha256Hex(bytes);
        var asset = AssetService.RegisterAsset(
            _core, projectId, Guid.NewGuid(), hash, bytes.LongLength, "application/json", "1", Now);

        if (verified)
        {
            _core.MarkAssetVerified(
                projectId,
                asset.AssetId,
                new AssetVerificationEvidence(asset.ContentHash, asset.ContentHash, asset.ByteLength, asset.ByteLength),
                Now + 1);
        }

        return asset;
    }

    private static string Component(Guid componentId, Guid assetId, string contentHash) =>
        "{\"geometry_component_id\":\"" + componentId.ToString("D") +
        "\",\"geometry_role\":\"footprint\",\"geometry_family\":\"polygon\",\"dimension\":2," +
        "\"coordinate_context_id\":\"local-context-1\",\"child_context\":{}," +
        "\"asset_id\":\"" + assetId.ToString("D") + "\",\"content_hash\":\"" + contentHash +
        "\",\"spatial_reference_id\":\"sr-local-1\",\"geometry_schema_version\":\"1\"," +
        "\"provenance\":{\"source_kind\":\"authored\"}," +
        "\"validity_metadata\":{\"status\":\"valid\",\"validation_profile_id\":\"v1\",\"issue_codes\":[]}}";

    private static string BuildingPayload(string name, params string[] components) =>
        """
        {"schema_version":1,"canonical_name":"NAME","building_class":"residential","description":"demo building","horizontal_frame":{"frame_id":"hframe-1","coordinate_kind":"local_cartesian","unit":"m","origin":{"x":0,"y":0},"orientation_degrees":0,"precision_profile_id":"hp-1"},"vertical_frame":{"vertical_frame_id":"vframe-1","unit":"m","positive_axis":"up","precision_profile_id":"vp-1"},"levels":[{"level_id":"level-ground","level_label":"Ground Floor","level_kind":"ground","display_order":0,"elevation_local":0,"status":"active"}],"spaces":[{"space_id":"space-hall","space_kind":"room","label":"Hall","status":"active"}],"plans":[],"boundaries":[],"access":{"entrances":[],"openings":[],"connections":[],"vertical_connectors":[],"exterior_anchors":[]},"geometry_components":[COMPONENTS]}
        """
        .Replace("NAME", name)
        .Replace("COMPONENTS", string.Join(",", components));

    [Fact]
    public void Publication_Success_WritesDefinitionAndCompleteLinkSetAtomically()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var asset = CreateAsset(project.ProjectId, "footprint-bytes"u8.ToArray());
        var componentId = Guid.NewGuid();
        var payload = BuildingPayload("Published Hall", Component(componentId, asset.AssetId, asset.ContentHash));

        ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            _core, project.ProjectId, identityId, currentHash, payload, 1, Now + 2);

        var definition = _core.ReadObjectDefinition(project.ProjectId, identityId)!;
        Assert.Contains("Published Hall", definition.DefinitionPayloadJson);
        Assert.NotEqual(currentHash, definition.DefinitionHash);
        Assert.NotNull(_core.FindDefinitionAssetLink(project.ProjectId, identityId, asset.AssetId, "geometry_component"));
    }

    [Fact]
    public void Publication_UnverifiedAsset_FailsWithoutPartialWrites()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var asset = CreateAsset(project.ProjectId, "unverified"u8.ToArray(), verified: false);
        var payload = BuildingPayload("Blocked Hall", Component(Guid.NewGuid(), asset.AssetId, asset.ContentHash));

        Assert.Throws<CoreDomainConflictException>(() => ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            _core, project.ProjectId, identityId, currentHash, payload, 1, Now + 2));

        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
        Assert.Equal(0, CountGeometryLinks(project.ProjectId, identityId));
    }

    [Fact]
    public void Publication_HashMismatch_FailsWithoutPartialWrites()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var asset = CreateAsset(project.ProjectId, "actual-hash"u8.ToArray());
        var mismatchedHash = new string('c', 64);
        var payload = BuildingPayload("Hash Hall", Component(Guid.NewGuid(), asset.AssetId, mismatchedHash));

        Assert.Throws<CoreDomainConflictException>(() => ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            _core, project.ProjectId, identityId, currentHash, payload, 1, Now + 2));

        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
        Assert.Equal(0, CountGeometryLinks(project.ProjectId, identityId));
    }

    [Fact]
    public void Publication_MissingAsset_FailsWithoutPartialWrites()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var payload = BuildingPayload(
            "Missing Hall",
            Component(Guid.NewGuid(), Guid.NewGuid(), new string('d', 64)));

        Assert.Throws<CoreDomainConflictException>(() => ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            _core, project.ProjectId, identityId, currentHash, payload, 1, Now + 2));

        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
        Assert.Equal(0, CountGeometryLinks(project.ProjectId, identityId));
    }

    [Fact]
    public void Publication_ExistingExtraLink_FailsExactSetConsistency()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var linked = CreateAsset(project.ProjectId, "linked"u8.ToArray());
        var extra = CreateAsset(project.ProjectId, "extra"u8.ToArray());

        // 直接注入一条 owner payload 未声明的 typed link，模拟历史不一致。
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO core_object_definition_asset "
                + "(object_definition_asset_id, project_id, object_identity_id, asset_id, role, created_at_utc_ms) "
                + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{identityId:D}', '{extra.AssetId:D}', 'geometry_component', {Now});";
            command.ExecuteNonQuery();
        });

        var payload = BuildingPayload("Exact Hall", Component(Guid.NewGuid(), linked.AssetId, linked.ContentHash));

        var ex = Assert.Throws<CoreDomainConflictException>(() =>
            ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
                _core, project.ProjectId, identityId, currentHash, payload, 1, Now + 2));
        Assert.Contains("ASSET_LINK_MISMATCH", ex.Message);
        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
    }

    [Fact]
    public void Publication_NonActiveDefinition_FailsInTransaction()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var asset = CreateAsset(project.ProjectId, "abandoned"u8.ToArray());

        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"UPDATE core_object SET lifecycle_status = 'abandoned' WHERE project_id = '{project.ProjectId:D}' AND object_identity_id = '{identityId:D}';";
            command.ExecuteNonQuery();
        });

        var payload = BuildingPayload("Abandoned Hall", Component(Guid.NewGuid(), asset.AssetId, asset.ContentHash));

        Assert.Throws<CoreDomainConflictException>(() =>
            _core.PublishObjectDefinitionWithGeometryAssets(new ObjectDefinitionPublicationPlan
            {
                ProjectId = project.ProjectId,
                ObjectIdentityId = identityId,
                ExpectedDefinitionHash = currentHash,
                DefinitionPayloadJson = payload,
                DefinitionSchemaVersion = 1,
                NowUtcMs = Now + 2,
            }));

        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
    }

    [Fact]
    public void Publication_ExceptionDuringLinkWrite_RollsBackWholeTransaction()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var asset = CreateAsset(project.ProjectId, "trigger"u8.ToArray());
        var payload = BuildingPayload("Trigger Hall", Component(Guid.NewGuid(), asset.AssetId, asset.ContentHash));

        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TRIGGER fail_geometry_link BEFORE INSERT ON core_object_definition_asset "
                + "BEGIN SELECT RAISE(ABORT, 'injected publication failure'); END;";
            command.ExecuteNonQuery();
        });

        try
        {
            Assert.ThrowsAny<Exception>(() => ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
                _core, project.ProjectId, identityId, currentHash, payload, 1, Now + 2));
        }
        finally
        {
            Access().Execute(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "DROP TRIGGER fail_geometry_link;";
                command.ExecuteNonQuery();
            });
        }

        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
        Assert.Equal(0, CountGeometryLinks(project.ProjectId, identityId));
    }

    [Fact]
    public void SingleLinkOrUnlink_CannotBypassAtomicPublication()
    {
        var project = SeedProject();
        var identityId = CreateBuildingObject(project.ProjectId, out var currentHash);
        var asset = CreateAsset(project.ProjectId, "bypass"u8.ToArray());

        Assert.Throws<CoreDomainConflictException>(() =>
            AssetService.LinkDefinitionAsset(_core, project.ProjectId, identityId, asset.AssetId, "geometry_component", Now + 2));
        Assert.Throws<CoreDomainConflictException>(() =>
            AssetService.UnlinkDefinitionAsset(_core, project.ProjectId, identityId, asset.AssetId, "geometry_component"));

        var changedPayload = BuildingPayload("Edit Hall", Component(Guid.NewGuid(), asset.AssetId, asset.ContentHash));
        Assert.Throws<CoreDomainConflictException>(() => ObjectDefinitionService.EditDefinition(
            _core, project.ProjectId, identityId, currentHash, changedPayload, 1, Now + 4));

        Assert.Equal(currentHash, _core.ReadObjectDefinition(project.ProjectId, identityId)!.DefinitionHash);
        Assert.Equal(0, CountGeometryLinks(project.ProjectId, identityId));
    }
}
