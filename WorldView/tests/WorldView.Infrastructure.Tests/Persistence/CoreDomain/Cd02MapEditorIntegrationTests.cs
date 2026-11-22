using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Assets;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-02-F 端到端集成：ME-04 建筑/室内创作路径全部经 Core Domain Services
/// （Domain 服务 + 端口；无 UI 直写 SQLite）。这是 ME-03/04/05 所需领域能力的
/// 可调用性证明：包 bootstrap → 类型播种 → Reservation/Bind → typed aggregate
/// definition → Map 注册 → Initial State → 几何 Asset 发布/登记/typed link →
/// CAS 编辑 → Branch 创建（baseline 继承为解析时语义，不复制行）。
/// </summary>
public sealed class Cd02MapEditorIntegrationTests : IDisposable
{
    private const long Now = 1_000L;
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new(new[] { new CoreDomainMigrationSource() });
    private readonly SqlitePackageDatabase _database;

    public Cd02MapEditorIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-cd02me-{Guid.NewGuid():N}");
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

    /// <summary>P2-09-40 base fixture 风格的最小合法 Building 聚合（canonical JSON）。</summary>
    private static string BuildingAggregate(string canonicalName) => """
        {"schema_version":1,"canonical_name":"NAME","building_class":"residential","description":"demo building","horizontal_frame":{"frame_id":"hframe-1","coordinate_kind":"local_cartesian","unit":"m","origin":{"x":0,"y":0},"orientation_degrees":0,"precision_profile_id":"hp-1"},"vertical_frame":{"vertical_frame_id":"vframe-1","unit":"m","positive_axis":"up","precision_profile_id":"vp-1"},"levels":[{"level_id":"level-ground","level_label":"Ground Floor","level_kind":"ground","display_order":0,"elevation_local":0,"status":"active"}],"spaces":[{"space_id":"space-hall","space_kind":"room","label":"Hall","status":"active"}],"plans":[],"boundaries":[],"access":{"entrances":[],"openings":[],"connections":[],"vertical_connectors":[],"exterior_anchors":[]},"geometry_components":[]}
        """.Replace("NAME", canonicalName);

    private static string BuildingAggregateWithFootprint(string canonicalName, Guid componentId, Guid assetId, string contentHash) =>
        BuildingAggregate(canonicalName).Replace(
            "\"geometry_components\":[]",
            "\"geometry_components\":[{\"geometry_component_id\":\"" + componentId.ToString("D") +
            "\",\"geometry_role\":\"footprint\",\"geometry_family\":\"polygon\",\"dimension\":2," +
            "\"coordinate_context_id\":\"local-context-1\",\"child_context\":{}," +
            "\"asset_id\":\"" + assetId.ToString("D") + "\",\"content_hash\":\"" + contentHash +
            "\",\"spatial_reference_id\":\"sr-local-1\",\"geometry_schema_version\":\"1\"," +
            "\"provenance\":{\"source_kind\":\"authored\"}," +
            "\"validity_metadata\":{\"status\":\"valid\",\"validation_profile_id\":\"v1\",\"issue_codes\":[]}}]");

    [Fact]
    public void Me04_BuildingAuthoringPath_AllThroughDomainServices()
    {
        var core = Core();

        // 1) 包 bootstrap + Project 身份 + 12 类型播种（OD-4，创建事务内）。
        var project = ProjectRules.CreateProject("ME-04 Demo", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));

        // 2) Map 创建（CD-01 结构：root History + default Calendar + day-zero Configuration）。
        var map = MapService.CreateMap(core, project.ProjectId, "Demo Map", Now);
        var mapId = map.Map.MapId;

        // 3) Reservation → Bind（pin 来自 registry；Building 为采纳类型）。
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "building", "me04-building-1", Now + 1);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now + 2);
        var buildingId = bound.ObjectIdentityId!.Value;
        Assert.Equal("world-view.object-type.building.v1", bound.AllocatedIdValue is not null
            ? core.FindObject(project.ProjectId, buildingId)!.ObjectTypeSemanticsId
            : throw new InvalidOperationException("unreachable"));

        // 4) 正式创建：typed Building 聚合 definition + Map 注册 + time-zero 成员（TX-55 原子）。
        ObjectCreationService.InsertObject(
            core, project.ProjectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = project.ProjectId,
                ObjectIdentityId = buildingId,
                DefinitionSchemaVersion = 1,
                DefinitionPayloadJson = BuildingAggregate("Demo Hall"),
                DefinitionHash = string.Empty,
                UpdatedAtUtcMs = Now + 3,
            },
            new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = mapId },
            new InitialObjectMembership { MembershipPayloadJson = """{"placement":{"x":10,"y":20}}""" },
            Now + 3);

        // 5) 几何 Asset：字节发布 → 权威登记 → typed link（footprint role 经 registry 校验）。
        var envelope = """
            {"format_id":"worldview.geometry-json","format_version":1,"spatial_reference_id":"sr-local-1","axis_order":"x_y","unit":"m","coordinate_precision_profile_id":"p1","dimension":2,"geometry_family":"polygon","body":[[[0,0],[8,0],[8,6],[0,6],[0,0]]]}
            """;
        var bytes = Encoding.UTF8.GetBytes(envelope);
        var hash = AssetRules.Sha256Hex(bytes);
        var asset = AssetService.RegisterAsset(core, project.ProjectId, Guid.NewGuid(), hash, bytes.Length, "application/json", "1", Now + 4);
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 5);

        // Building/Cave 不允许 Definition 与 typed link 分两次提交。
        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.LinkDefinitionAsset(core, project.ProjectId, buildingId, asset.AssetId, "footprint", Now + 6));

        var definition = core.ReadObjectDefinition(project.ProjectId, buildingId)!;
        var componentId = Guid.NewGuid();

        // Asset hash 不匹配时原子发布失败，Definition 和 links 都保持原样。
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
                core, project.ProjectId, buildingId, definition.DefinitionHash,
                BuildingAggregateWithFootprint("Demo Hall", componentId, asset.AssetId, new string('0', 64)),
                1, Now + 7));
        Assert.Equal(definition.DefinitionHash, core.ReadObjectDefinition(project.ProjectId, buildingId)!.DefinitionHash);
        Assert.Empty(core.ListDefinitionAssetLinks(project.ProjectId, buildingId));

        // 正式发布：Definition manifest 与 distinct Asset typed links 同一事务提交。
        var published = ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            core, project.ProjectId, buildingId, definition.DefinitionHash,
            BuildingAggregateWithFootprint("Demo Hall", componentId, asset.AssetId, asset.ContentHash),
            1, Now + 8);
        var link = core.FindDefinitionAssetLink(project.ProjectId, buildingId, asset.AssetId, "geometry_component");
        Assert.NotNull(link);
        Assert.Equal("geometry_component", link.Role);

        // 不允许通过单独 unlink 或普通 Definition CAS 移除/改变正式 geometry 组件。
        Assert.Throws<CoreDomainConflictException>(
            () => AssetService.UnlinkDefinitionAsset(core, project.ProjectId, buildingId, asset.AssetId, "geometry_component"));
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectDefinitionService.EditDefinition(
                core, project.ProjectId, buildingId, published.DefinitionHash,
                BuildingAggregate("Demo Hall Renamed"), 1, Now + 9));

        // 非 geometry 字段仍可 CAS 编辑，组件身份与 Asset pin 保持不变。
        var updated = ObjectDefinitionService.EditDefinition(
            core, project.ProjectId, buildingId, published.DefinitionHash,
            BuildingAggregateWithFootprint("Demo Hall Renamed", componentId, asset.AssetId, asset.ContentHash), 1, Now + 10);
        Assert.Contains("Demo Hall Renamed", updated.DefinitionPayloadJson);

        // 7) Initial State CAS 编辑（baseline 建立时成员已由 Insert 建立 → expected=null 非法；用重读凭据）。
        var baseline = core.FindMapInitialState(project.ProjectId, mapId)!;
        var members = core.ListInitialObjects(project.ProjectId, mapId);
        Assert.Single(members);
        Assert.Equal(
            InitialStateFingerprint.Compute(baseline.BaselineSchemaVersion, members),
            baseline.ContentFingerprint);

        // 8) Branch 创建：baseline 继承是解析时语义——Branch 创建不触碰 Initial State/
        //    Definition/Reservation 任何行（CD-02-A §11 红线）。
        var rootHistory = core.FindRootHistoryByMap(project.ProjectId, mapId)!;
        var branch = BranchService.CreateBranch(core, project.ProjectId, rootHistory.HistoryId, "Renovation", WorldView.Domain.Time.HistoricalTime.Epoch, Now + 8);
        Assert.Equal(1, CountInitialStateRows());
        Assert.Equal(1, CountDefinitionRows());

        // 9) 孤儿对账：已 link 的 Asset 非孤儿。
        Assert.DoesNotContain(asset.AssetId, core.FindOrphanAssetIds(project.ProjectId));

        // 10) 全链可解析：building 对象 active、注册行存在、Baseline 成员存在。
        Assert.Equal(ObjectLifecycleStatus.Active, core.FindObject(project.ProjectId, buildingId)!.LifecycleStatus);
        Assert.NotNull(core.FindMapObjectRef(project.ProjectId, mapId, buildingId));
        _ = link;
    }

    [Fact]
    public void GeometryPublication_FailureAfterLinkInsertRollsBackDefinitionAndLinks()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("Atomic Geometry", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));

        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "building", null, Now + 1);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now + 2);
        var objectId = bound.ObjectIdentityId!.Value;
        ObjectCreationService.InsertObject(
            core, project.ProjectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = project.ProjectId,
                ObjectIdentityId = objectId,
                DefinitionSchemaVersion = 1,
                DefinitionPayloadJson = BuildingAggregate("Atomic Hall"),
                DefinitionHash = string.Empty,
                UpdatedAtUtcMs = Now + 3,
            },
            null, null, Now + 3);

        var bytes = Encoding.UTF8.GetBytes(
            """{"format_id":"worldview.geometry-json","format_version":1,"spatial_reference_id":"sr-local-1","axis_order":"x_y","unit":"m","coordinate_precision_profile_id":"p1","dimension":2,"geometry_family":"polygon","body":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}""");
        var asset = AssetService.RegisterAsset(
            core, project.ProjectId, Guid.NewGuid(), AssetRules.Sha256Hex(bytes), bytes.Length,
            "application/json", "1", Now + 4);
        core.UpdateAssetIntegrityState(project.ProjectId, asset.AssetId, AssetIntegrityState.Verified, Now + 5);

        var current = core.ReadObjectDefinition(project.ProjectId, objectId)!;
        var componentId = Guid.NewGuid();
        var proposed = BuildingAggregateWithFootprint("Atomic Hall", componentId, asset.AssetId, asset.ContentHash);
        var access = ((ISqliteConnectionProvider)_database).GetAccess();
        access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER fail_geometry_definition_update
                BEFORE UPDATE ON core_object_definition
                BEGIN
                    SELECT RAISE(ABORT, 'injected geometry publication failure');
                END;
                """;
            command.ExecuteNonQuery();
        });

        Assert.ThrowsAny<Exception>(() => ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            core, project.ProjectId, objectId, current.DefinitionHash, proposed, 1, Now + 6));

        Assert.Equal(current.DefinitionHash, core.ReadObjectDefinition(project.ProjectId, objectId)!.DefinitionHash);
        Assert.Empty(core.ListDefinitionAssetLinks(project.ProjectId, objectId));

        access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TRIGGER fail_geometry_definition_update;";
            command.ExecuteNonQuery();
        });

        var published = ObjectDefinitionService.PublishDefinitionWithGeometryAssets(
            core, project.ProjectId, objectId, current.DefinitionHash, proposed, 1, Now + 7);
        Assert.NotEqual(current.DefinitionHash, published.DefinitionHash);
        var links = core.ListDefinitionAssetLinks(project.ProjectId, objectId);
        Assert.Single(links);
        Assert.Equal(asset.AssetId, links[0].AssetId);
        Assert.Equal("geometry_component", links[0].Role);
    }

    [Fact]
    public void Me03_SettlementAuthoringPrimitives_ReachableThroughServices()
    {
        // ME-03 所需基础原语：settlement/road 对象 + 河流等地理对象可创建、可注册、
        // 可入 Initial State；Road 连接性（relationship）依赖 Relationship 阶段（缺口见报告）。
        var core = Core();
        var project = ProjectRules.CreateProject("ME-03 Demo", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));
        var map = MapService.CreateMap(core, project.ProjectId, "Town Map", Now);
        var mapId = map.Map.MapId;

        foreach (var typeKey in new[] { "settlement", "road", "river" })
        {
            var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, typeKey, $"me03-{typeKey}", Now + 1);
            var bound = ObjectIdReservationService.Bind(
                core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now + 2);
            ObjectCreationService.InsertObject(
                core, project.ProjectId, bound.ReservationId,
                new ObjectDefinitionRecord
                {
                    ProjectId = project.ProjectId,
                    ObjectIdentityId = bound.ObjectIdentityId!.Value,
                    DefinitionSchemaVersion = 2,
                    DefinitionPayloadJson = typeKey switch
                    {
                        "settlement" => """{"canonical_name":"First Town","settlement_class":"village"}""",
                        "road" => """{"road_class":"trail"}""",
                        _ => """{"canonical_name":"River","river_class":"perennial"}""",
                    },
                    DefinitionHash = string.Empty,
                    UpdatedAtUtcMs = Now + 3,
                },
                new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = mapId },
                new InitialObjectMembership { MembershipPayloadJson = """{"placed":true}""" },
                Now + 3);
        }

        // 三个对象全部注册 + 成员（P2⇒P3 链完整）。
        Assert.Equal(3, core.ListMapObjectRefs(project.ProjectId, mapId).Count);
        Assert.Equal(3, core.ListInitialObjects(project.ProjectId, mapId).Count);
    }

    private long CountInitialStateRows()
    {
        return ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM core_map_initial_object;";
            return (long)command.ExecuteScalar()!;
        });
    }

    private long CountDefinitionRows()
    {
        return ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM core_object_definition;";
            return (long)command.ExecuteScalar()!;
        });
    }
}
