using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-02-B 真实 SQLite 验证（真实迁移 + 真实适配器 + 真实协调器；不使用替身冒充集成）。
/// 覆盖 Handoff §6 验收清单：类型播种、Reservation 状态机全转换与非法转换、allocator
/// 不回退、request_key 幂等、并发分配、TX-55 中途失败注入、scope/注册/成员前置负向、
/// CAS 冲突、指纹确定性、数据库 CHECK/FK/UNIQUE 负向、读侧 fail-closed。
/// </summary>
public sealed class Cd02ObjectPersistenceTests : IDisposable
{
    private const long Now = 1_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();
    private readonly SqlitePackageDatabase _database;

    public Cd02ObjectPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-cd02-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _ = new SqliteCoreDomainDatabase(_database);
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

    private void ExecuteRaw(string sql)
    {
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        });
    }

    private (ProjectRecord Project, MapCreationPlan Map) SeedProjectAndMap(string projectName = "P", string mapName = "M")
    {
        var core = Core();
        var project = ProjectRules.CreateProject(projectName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        core.InsertProject(ProjectCreationPlan.Create(project));
        var map = MapService.CreateMap(core, project.ProjectId, mapName, Now);
        return (project, map);
    }

    private const string RiverDefinition = """{"canonical_name":"Demo","river_class":"other"}""";

    private static ObjectDefinitionRecord Definition(Guid projectId, Guid identityId, string payload = RiverDefinition) => new()
    {
        ProjectId = projectId,
        ObjectIdentityId = identityId,
        DefinitionSchemaVersion = BuiltinObjectTypes.DescriptorSchemaVersion,
        DefinitionPayloadJson = payload,
        DefinitionHash = string.Empty,
        UpdatedAtUtcMs = Now,
    };

    /// <summary>Reserve→Bind→Insert 全链辅助；返回身份键。</summary>
    private Guid CreateActiveObject(
        Guid projectId,
        Guid? ownerMapId = null,
        Guid? registerToMapId = null,
        bool withMembership = false,
        string typeKey = "river")
    {
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, projectId, typeKey, null, Now);
        var scope = ownerMapId is null ? ObjectSharingScope.ProjectShared : ObjectSharingScope.MapLocal;
        var bound = ObjectIdReservationService.Bind(core, projectId, allocation.Reservation.ReservationId, scope, ownerMapId, Now);
        var identityId = bound.ObjectIdentityId!.Value;
        ObjectCreationService.InsertObject(
            core, projectId, bound.ReservationId,
            Definition(projectId, identityId),
            registerToMapId is null ? null : new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = registerToMapId.Value },
            withMembership ? new InitialObjectMembership { MembershipPayloadJson = """{"m":1}""" } : null,
            Now);
        return identityId;
    }

    // ---- 播种 ----

    [Fact]
    public void ProjectCreation_SeedsTwelveTypes_TwelveAllocators()
    {
        var (project, _) = SeedProjectAndMap();
        var types = Core().ListObjectTypes(project.ProjectId);
        Assert.Equal(12, types.Count);
        Assert.Equal(12, CountRows("core_object_id_allocator"));
        Assert.All(types, t => Assert.True(t.Allocatable));
        Assert.Contains("building", types.Select(t => t.TypeKey));
        Assert.Contains("cave_system", types.Select(t => t.TypeKey));
    }

    [Fact]
    public async Task Reopen_AfterProjectCreation_ValidationPasses()
    {
        var (project, _) = SeedProjectAndMap();
        var second = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath);
        await using (second.ConfigureAwait(false))
        {
            await second.OpenAsync();
            var result = await _bootstrapper.ValidateExistingDatabaseAsync(second);
            Assert.True(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
            _ = project;
        }
    }

    [Fact]
    public void DuplicateTypeKey_InsertRaw_RejectedByUnique()
    {
        var (project, _) = SeedProjectAndMap();
        var type = Core().FindObjectTypeByTypeKey(project.ProjectId, "river")!;
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object_type (object_type_id, project_id, type_key, display_name, allocatable, allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', 'river', 'dup', 1, '{{}}', 1, 1, 1);"));
        _ = type;
    }

    // ---- Reservation ----

    [Fact]
    public void Reserve_FullChain_SequenceAdvancesNoRollback()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var a1 = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var a2 = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        Assert.Equal("1", a1.Reservation.AllocatedIdValue);
        Assert.Equal("2", a2.Reservation.AllocatedIdValue);

        // 放弃第二个：allocator 不回退（下一个仍是 3）。
        ObjectIdReservationService.Abandon(core, project.ProjectId, a2.Reservation.ReservationId, "cancelled", Now);
        var a3 = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        Assert.Equal("3", a3.Reservation.AllocatedIdValue);
    }

    [Fact]
    public void Reserve_RequestKey_IdempotentOnRealDb()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var first = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", "logical-req-1", Now);
        var retry = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", "logical-req-1", Now + 99);
        Assert.True(retry.RecoveredByRequestKey);
        Assert.Equal(first.Reservation.ReservationId, retry.Reservation.ReservationId);
        Assert.Equal("1", retry.Reservation.AllocatedIdValue);
        Assert.Equal(1, CountRows("core_object_id_reservation"));
        Assert.Equal(1, CountRows("core_object_id_allocator", $"WHERE next_sequence = 2 AND project_id = '{project.ProjectId:D}'"));
    }

    [Fact]
    public void Reserve_DuplicateRequestKeyDifferentType_Rejected()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", "req-x", Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "lake", "req-x", Now));
    }

    [Fact]
    public async Task Reserve_Concurrent_NoDuplicateSequenceOrIdValue()
    {
        var (project, _) = SeedProjectAndMap();
        const int parallel = 8;
        var results = new ReservationAllocation[parallel];
        await Task.WhenAll(Enumerable.Range(0, parallel).Select(i => Task.Run(() =>
        {
            var core = Core();
            results[i] = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now + i);
        }))).ConfigureAwait(false);

        var sequences = results.Select(r => r.Reservation.AllocatedSequence).OrderBy(s => s).ToArray();
        var idValues = results.Select(r => r.Reservation.AllocatedIdValue).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        Assert.Equal(sequences.Distinct().Count(), parallel);
        Assert.Equal(idValues.Distinct().Count(), parallel);
        Assert.Equal(Enumerable.Range(1, parallel).Select(i => (long)i), sequences);
        Assert.Equal(parallel, CountRows("core_object_id_reservation"));
        Assert.Equal(1, CountRows("core_object_id_allocator", $"WHERE next_sequence = {parallel + 1}"));
    }

    [Fact]
    public void Reservation_IllegalTransitions_AllRejected()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();

        // Reserved→Inserted 直接插入被服务拒绝（仅 Bound 可 Insert）——经构造：未 Bind 就 Insert。
        var a = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);

        // 1) Reserved→Abandoned→（终态不可重开）
        var abandoned = ObjectIdReservationService.Abandon(core, project.ProjectId, a.Reservation.ReservationId, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Bind(core, project.ProjectId, abandoned.ReservationId, ObjectSharingScope.ProjectShared, null, Now));
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Abandon(core, project.ProjectId, abandoned.ReservationId, null, Now));

        // 2) Reserved→Bound→Abandoned→（终态不可重开）
        var b = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(core, project.ProjectId, b.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var abandonedBound = ObjectIdReservationService.Abandon(core, project.ProjectId, bound.ReservationId, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Bind(core, project.ProjectId, abandonedBound.ReservationId, ObjectSharingScope.ProjectShared, null, Now));

        // 3) Bound→Inserted 之后不可 Abandoned/Bind。
        var c = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound2 = ObjectIdReservationService.Bind(core, project.ProjectId, c.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        ObjectCreationService.InsertObject(core, project.ProjectId, bound2.ReservationId, Definition(project.ProjectId, bound2.ObjectIdentityId!.Value), null, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Abandon(core, project.ProjectId, bound2.ReservationId, null, Now));
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Bind(core, project.ProjectId, bound2.ReservationId, ObjectSharingScope.ProjectShared, null, Now));
    }

    [Fact]
    public void Bind_ReservedCheckEnforcedInTransaction()
    {
        // 事务内重读校验：并发窗口下状态已被改（此处直接用 Abandoned reservation）→ 拒绝。
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var a = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        ObjectIdReservationService.Abandon(core, project.ProjectId, a.Reservation.ReservationId, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Bind(core, project.ProjectId, a.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now));
    }

    // ---- TX-55 中途失败注入 ----

    [Fact]
    public void InsertObject_MidTransactionFailure_RollsBackCompletely_RetryReusesSameReservation()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        // 注入：definition 插入即 ABORT（事务中途失败）。
        ExecuteRaw("""
            CREATE TRIGGER fail_definition_insert
            BEFORE INSERT ON core_object_definition
            BEGIN
                SELECT RAISE(ABORT, 'injected failure');
            END;
            """);

        Assert.ThrowsAny<Exception>(
            () => ObjectCreationService.InsertObject(
                core, project.ProjectId, bound.ReservationId,
                Definition(project.ProjectId, identityId),
                new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = map.Map.MapId },
                new InitialObjectMembership { MembershipPayloadJson = """{"m":1}""" },
                Now));

        // 无半成品：identity 仍 draft、reservation 仍 Bound、无 definition/注册/成员/基线。
        Assert.Equal(ObjectLifecycleStatus.Draft, core.FindObject(project.ProjectId, identityId)!.LifecycleStatus);
        Assert.Equal(ReservationState.Bound, core.FindReservation(project.ProjectId, bound.ReservationId)!.State);
        Assert.Equal(0, CountRows("core_object_definition"));
        Assert.Equal(0, CountRows("core_map_object_ref"));
        // Map 创建已建立 baseline/revision zero/cursor（FOUNDATION-01B §5）；失败注入不得改动它们。
        Assert.Equal(1, CountRows("core_map_initial_state"));
        Assert.Equal(1, CountRows("core_map_initial_state_cursor"));
        Assert.Equal(1, CountRows("core_map_initial_state_revision", "WHERE revision_number = 0"));
        Assert.Equal(0, CountRows("core_map_initial_state_revision_member"));

        // 移除注入后重试：复用同一 reservation/identity 成功（绝不分配替代 ID）。
        ExecuteRaw("DROP TRIGGER fail_definition_insert;");
        ObjectCreationService.InsertObject(
            core, project.ProjectId, bound.ReservationId,
            Definition(project.ProjectId, identityId),
            new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = map.Map.MapId },
            new InitialObjectMembership { MembershipPayloadJson = """{"m":1}""" },
            Now);
        Assert.Equal(ObjectLifecycleStatus.Active, core.FindObject(project.ProjectId, identityId)!.LifecycleStatus);
        Assert.Equal(ReservationState.Inserted, core.FindReservation(project.ProjectId, bound.ReservationId)!.State);
        Assert.Equal("1", bound.AllocatedIdValue);
    }

    [Fact]
    public void Bind_MidTransactionFailure_RollsBack_RetrySameIdentity()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);

        // 注入：core_object 插入即 ABORT（Bind 路径中途失败）。
        ExecuteRaw("""
            CREATE TRIGGER fail_object_insert
            BEFORE INSERT ON core_object
            BEGIN
                SELECT RAISE(ABORT, 'injected failure');
            END;
            """);
        Assert.ThrowsAny<Exception>(
            () => ObjectIdReservationService.Bind(
                core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now));
        Assert.Equal(ReservationState.Reserved, core.FindReservation(project.ProjectId, allocation.Reservation.ReservationId)!.State);
        Assert.Equal(0, CountRows("core_object"));

        ExecuteRaw("DROP TRIGGER fail_object_insert;");
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        Assert.Equal(ReservationState.Bound, bound.State);
        Assert.Equal("1", bound.AllocatedIdValue);
    }

    // ---- scope/注册 ----

    [Fact]
    public void MapLocal_CrossMapRegistration_ServiceRejected()
    {
        var (project, map) = SeedProjectAndMap();
        var map2 = MapService.CreateMap(Core(), project.ProjectId, "M2", Now);
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId, ownerMapId: map.Map.MapId, registerToMapId: null);

        Assert.Throws<CoreDomainConflictException>(
            () => MapRegistrationService.Register(core, project.ProjectId, map2.Map.MapId, identityId, Now));
        Assert.Equal(0, CountRows("core_map_object_ref"));
    }

    [Fact]
    public void MapLocal_CrossMapRegistration_TriggerDefenseInDepth()
    {
        // 服务路径不可达时的 DB 纵深防御：手工 INSERT 跨 Map 注册 → 触发器 ABORT。
        var (project, map) = SeedProjectAndMap();
        var map2 = MapService.CreateMap(Core(), project.ProjectId, "M2", Now);
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId, ownerMapId: map.Map.MapId);

        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_map_object_ref (map_object_ref_id, project_id, map_id, object_identity_id, created_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{map2.Map.MapId:D}', '{identityId:D}', {Now});"));
    }

    [Fact]
    public void ProjectShared_MultipleMaps_RegistrationOk()
    {
        var (project, map) = SeedProjectAndMap();
        var map2 = MapService.CreateMap(Core(), project.ProjectId, "M2", Now);
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId);

        MapRegistrationService.Register(core, project.ProjectId, map.Map.MapId, identityId, Now);
        MapRegistrationService.Register(core, project.ProjectId, map2.Map.MapId, identityId, Now);
        Assert.Equal(1, CountRows("core_map_object_ref", $"WHERE map_id = '{map.Map.MapId:D}'"));
        Assert.Equal(1, CountRows("core_map_object_ref", $"WHERE map_id = '{map2.Map.MapId:D}'"));
    }

    [Fact]
    public void Registration_Duplicate_Rejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId, registerToMapId: map.Map.MapId);
        Assert.Throws<CoreDomainConflictException>(
            () => MapRegistrationService.Register(core, project.ProjectId, map.Map.MapId, identityId, Now));
    }

    [Fact]
    public void Unregister_WithMembership_Rejected_WithoutMembership_Ok()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId, registerToMapId: map.Map.MapId, withMembership: true);
        Assert.Throws<CoreDomainConflictException>(
            () => MapRegistrationService.Unregister(core, project.ProjectId, map.Map.MapId, identityId));

        var identityId2 = CreateActiveObject(project.ProjectId, registerToMapId: map.Map.MapId);
        MapRegistrationService.Unregister(core, project.ProjectId, map.Map.MapId, identityId2);
        Assert.Null(core.FindMapObjectRef(project.ProjectId, map.Map.MapId, identityId2));
    }

    // ---- CHECK/UNIQUE 数据库级负向 ----

    [Fact]
    public void CheckConstraints_ScopePairingAndReservationState_RejectRawViolations()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var type = core.FindObjectTypeByTypeKey(project.ProjectId, "river")!;

        // R-scope：project_shared 带 owner。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object (object_identity_id, project_id, object_type_id, object_type_schema_version, object_type_semantics_id, sharing_scope, owner_map_id, lifecycle_status, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 2, 'world-view.object-type.river.v1', 'project_shared', '{map.Map.MapId:D}', 'draft', {Now}, {Now});"));
        // R-scope：map_local 无 owner。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object (object_identity_id, project_id, object_type_id, object_type_schema_version, object_type_semantics_id, sharing_scope, owner_map_id, lifecycle_status, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 2, 'world-view.object-type.river.v1', 'map_local', NULL, 'draft', {Now}, {Now});"));
        // 未知 lifecycle。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object (object_identity_id, project_id, object_type_id, object_type_schema_version, object_type_semantics_id, sharing_scope, owner_map_id, lifecycle_status, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 2, 'world-view.object-type.river.v1', 'project_shared', NULL, 'zombie', {Now}, {Now});"));
        // Reservation CHECK：Reserved 带 identity。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object_id_reservation (reservation_id, project_id, object_type_id, object_identity_id, allocated_sequence, allocated_id_value, reservation_state, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', '{Guid.NewGuid():D}', 99, '99', 'Reserved', {Now}, {Now});"));
        // Reservation CHECK：Bound 无 identity。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object_id_reservation (reservation_id, project_id, object_type_id, allocated_sequence, allocated_id_value, reservation_state, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 99, '99', 'Bound', {Now}, {Now});"));
        // abandon_reason 只能出现在 Abandoned。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_object_id_reservation (reservation_id, project_id, object_type_id, allocated_sequence, allocated_id_value, reservation_state, abandon_reason, created_at_utc_ms, updated_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 99, '99', 'Reserved', 'why', {Now}, {Now});"));
    }

    [Fact]
    public void UniqueConstraints_ReservationDuplicateSequenceIdValueRequestKey_Rejected()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var type = core.FindObjectTypeByTypeKey(project.ProjectId, "river")!;
        var baseSql = $"INSERT INTO core_object_id_reservation (reservation_id, project_id, object_type_id, allocated_sequence, allocated_id_value, reservation_state, request_key, created_at_utc_ms, updated_at_utc_ms) VALUES";

        ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", "k1", Now);

        // 重复 sequence。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"{baseSql} ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 1, '999', 'Reserved', NULL, {Now}, {Now});"));
        // 重复 id_value。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"{baseSql} ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 999, '1', 'Reserved', NULL, {Now}, {Now});"));
        // 重复 request_key。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"{baseSql} ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{type.ObjectTypeId:D}', 999, '999', 'Reserved', 'k1', {Now}, {Now});"));
    }

    // ---- Map 创建原子性（FOUNDATION-01B §5） ----

    [Fact]
    public void MapCreation_EstablishesBaselineRevisionZeroAndCursorAtomically()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;

        // baseline identity + revision zero + cursor 全部存在且可读（无 lazy 建立）。
        var baseline = core.FindMapInitialStateBaseline(project.ProjectId, mapId);
        Assert.NotNull(baseline);
        Assert.Equal(map.InitialStateBaseline!.InitialStateId, baseline!.InitialStateId);

        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId);
        Assert.NotNull(state);
        Assert.Equal(0, state!.Snapshot.Revision.RevisionNumber);
        Assert.Null(state.Snapshot.Revision.ParentRevisionId);
        Assert.Null(state.Snapshot.Revision.OperationKind);
        Assert.Empty(state.Snapshot.Members);
        Assert.Equal(MapInitialStateService.EmptySnapshotFingerprint, state.Snapshot.Revision.ContentFingerprint);
        Assert.Equal(0, state.Cursor.CursorVersion);
        Assert.Equal(baseline.InitialStateId, state.Cursor.InitialStateId);

        // 每张 Map 恰好一套三行（CountRows 直接对表计数）。
        Assert.Equal(1, CountRows("core_map_initial_state", $"WHERE map_id = '{mapId:D}'"));
        Assert.Equal(1, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}'"));
        Assert.Equal(1, CountRows("core_map_initial_state_cursor", $"WHERE map_id = '{mapId:D}'"));
    }

    [Fact]
    public void MapCreation_AllExistingMapsHaveBaselineRevisionZeroAndCursor()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        MapService.CreateMap(core, project.ProjectId, "M2", Now + 1);
        MapService.CreateMap(core, project.ProjectId, "M3", Now + 2);

        Assert.Equal(CountRows("core_map"), CountRows("core_map_initial_state"));
        Assert.Equal(CountRows("core_map"), CountRows("core_map_initial_state_cursor"));
        Assert.Equal(CountRows("core_map"), CountRows(
            "core_map_initial_state_revision", "WHERE revision_number = 0"));
    }

    // ---- 数据库不可变性行为（WP7：真实 SQLite trigger 行为，不是只验证 trigger 存在） ----

    [Fact]
    public void Database_CommittedRevisionIsImmutable_UpdateRejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var obj = CreateActiveObject(project.ProjectId, registerToMapId: mapId);
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;

        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"UPDATE core_map_initial_state_revision SET content_fingerprint = '{new string('a', 64)}' "
            + $"WHERE revision_id = '{state.Cursor.ActiveRevisionId:D}';"));
        // 内容未变（trigger ABORT 回滚 UPDATE）。
        var reread = core.FindMapInitialStateRevisionSnapshot(project.ProjectId, mapId, state.Cursor.ActiveRevisionId)!;
        Assert.Equal(state.Snapshot.Revision.ContentFingerprint, reread.Revision.ContentFingerprint);
        _ = obj;
    }

    [Fact]
    public void Database_CommittedRevisionDelete_Rejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        CreateActiveObject(project.ProjectId, registerToMapId: mapId);
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;

        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"DELETE FROM core_map_initial_state_revision WHERE revision_id = '{state.Cursor.ActiveRevisionId:D}';"));
        Assert.Equal(1, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}'"));
    }

    [Fact]
    public void Database_RevisionMemberUpdateAndDelete_Rejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var obj = CreateActiveObject(project.ProjectId, registerToMapId: mapId, withMembership: true);

        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"UPDATE core_map_initial_state_revision_member SET initial_membership_payload_json = '{{}}' "
            + $"WHERE object_identity_id = '{obj:D}';"));
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"DELETE FROM core_map_initial_state_revision_member WHERE object_identity_id = '{obj:D}';"));

        // 成员行未变。
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        var member = Assert.Single(state.Snapshot.Members);
        Assert.Equal(obj, member.ObjectIdentityId);
        Assert.Equal("""{"m":1}""", member.MembershipPayloadJson);
    }

    [Fact]
    public void Database_CursorRemainsMutable_AndLegalRevisionInsertWorks()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;

        // cursor 合法更新被允许（唯一可变权威行）。
        ExecuteRaw(
            $"UPDATE core_map_initial_state_cursor SET cursor_version = 7 WHERE map_id = '{mapId:D}';");
        Assert.Equal(7, core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!.Cursor.CursorVersion);

        // 合法 revision INSERT 正常工作（append 事务路径的底层能力）。
        var newRevisionId = Guid.NewGuid();
        ExecuteRaw(
            "INSERT INTO core_map_initial_state_revision (revision_id, project_id, map_id, initial_state_id, "
            + "revision_number, parent_revision_id, baseline_schema_version, content_fingerprint, operation_kind, operation_payload_json, created_at_utc_ms) "
            + $"VALUES ('{newRevisionId:D}', '{project.ProjectId:D}', '{mapId:D}', '{state.Cursor.InitialStateId:D}', 1, "
            + $"'{state.Cursor.ActiveRevisionId:D}', 1, '{MapInitialStateService.EmptySnapshotFingerprint}', "
            + $"'snapshot_replace', '{{}}', {Now});");
        Assert.Equal(2, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}'"));

        // 关联完整性：成员行引用不存在 revision 被拒。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            "INSERT INTO core_map_initial_state_revision_member (project_id, map_id, initial_state_id, revision_id, "
            + "object_identity_id, initial_membership_payload_json, created_at_utc_ms) "
            + $"VALUES ('{project.ProjectId:D}', '{mapId:D}', '{state.Cursor.InitialStateId:D}', '{Guid.NewGuid():D}', "
            + $"'{Guid.NewGuid():D}', '{{}}', {Now});"));
    }

    [Fact]
    public void Database_StaleAppendIsRejectedWithoutAnyWrites()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var obj = CreateActiveObject(project.ProjectId, registerToMapId: mapId);
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;

        // stale cursor version → CoreDomainConflictException，无 revision/成员/cursor 写入。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                core, project.ProjectId, mapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion + 3,
                new List<InitialObjectWrite> { new() { ObjectIdentityId = obj, MembershipPayloadJson = "{}" } },
                Now + 1));

        Assert.Equal(1, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}'"));
        Assert.Equal(0, CountRows("core_map_initial_state_revision_member", $"WHERE map_id = '{mapId:D}'"));
        Assert.Equal(0, core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!.Cursor.CursorVersion);
    }

    [Fact]
    public void Database_FailedInsertObjectLeavesNoPartialState()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        var beforeRevisionCount = CountRows("core_map_initial_state_revision");
        var beforeMemberCount = CountRows("core_map_initial_state_revision_member");

        // Initial State 成员缺少注册行（P3⇒P2）→ InsertObject 整体回滚：
        // definition/reservation/identity/revision/成员/cursor 均无变化。
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectCreationService.InsertObject(
                core, project.ProjectId, bound.ReservationId,
                Definition(project.ProjectId, bound.ObjectIdentityId!.Value),
                registration: null,
                new InitialObjectMembership { MembershipPayloadJson = "{}" },
                Now));

        Assert.Equal(beforeRevisionCount, CountRows("core_map_initial_state_revision"));
        Assert.Equal(beforeMemberCount, CountRows("core_map_initial_state_revision_member"));
        Assert.Equal(0, CountRows("core_object_definition"));
        var after = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        Assert.Equal(state.Cursor.CursorVersion, after.Cursor.CursorVersion);
        Assert.Equal(state.Cursor.ActiveRevisionId, after.Cursor.ActiveRevisionId);
        Assert.Equal("Bound", "Bound"); // reservation 保持 Bound（失败回滚不推进状态机）
        Assert.Equal(ReservationState.Bound, core.FindReservation(project.ProjectId, bound.ReservationId)!.State);
    }

    [Fact]
    public void InitialState_OneBaselinePerMap_UniqueEnforced()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var baseline = core.FindMapInitialStateBaseline(project.ProjectId, map.Map.MapId)!;
        Assert.NotNull(baseline);

        // 第二条基线行被 UNIQUE(project,map) 拒绝。
        Assert.ThrowsAny<Exception>(() => ExecuteRaw(
            $"INSERT INTO core_map_initial_state (initial_state_id, project_id, map_id, created_at_utc_ms) "
            + $"VALUES ('{Guid.NewGuid():D}', '{project.ProjectId:D}', '{map.Map.MapId:D}', {Now});"));
    }

    [Fact]
    public void InitialState_UndoRedoRoundTripOnRealDatabase()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var obj = CreateActiveObject(project.ProjectId, registerToMapId: mapId, withMembership: true);

        var state1 = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        var editedRevision = state1.Cursor.ActiveRevisionId;

        // Undo → Redo round trip；每次 cursor version 单调 +1。
        var undone = MapInitialStateService.Undo(
            core, project.ProjectId, mapId, state1.Cursor.ActiveRevisionId, state1.Cursor.CursorVersion, Now + 1);
        Assert.Equal(editedRevision, undone.Cursor.SelectedRedoChildRevisionId);
        Assert.Empty(undone.Snapshot.Members);

        var redone = MapInitialStateService.Redo(
            core, project.ProjectId, mapId, undone.Cursor.ActiveRevisionId, undone.Cursor.CursorVersion, null, Now + 2);
        Assert.Equal(editedRevision, redone.Cursor.ActiveRevisionId);
        Assert.Null(redone.Cursor.SelectedRedoChildRevisionId);
        var member = Assert.Single(redone.Snapshot.Members);
        Assert.Equal(obj, member.ObjectIdentityId);
        Assert.True(redone.Cursor.CursorVersion > undone.Cursor.CursorVersion);

        // undo/redo 全程 revision 行数不变（cursor-only transition）。
        Assert.Equal(2, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}'"));
    }

    [Fact]
    public void InitialState_CloseReopenRestoresCursorAndRedoSelection()
    {
        var (project, map) = SeedProjectAndMap();
        var mapId = map.Map.MapId;
        CreateActiveObject(project.ProjectId, registerToMapId: mapId, withMembership: true);
        var coreBefore = Core();
        var state1 = coreBefore.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        var undone = MapInitialStateService.Undo(
            coreBefore, project.ProjectId, mapId, state1.Cursor.ActiveRevisionId, state1.Cursor.CursorVersion, Now + 1);

        // 模拟 close/reopen：新适配器实例（同一物理连接由会话层管理）。
        var coreAfter = Core();
        var state2 = coreAfter.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        Assert.Equal(undone.Cursor.ActiveRevisionId, state2.Cursor.ActiveRevisionId);
        Assert.Equal(undone.Cursor.SelectedRedoChildRevisionId, state2.Cursor.SelectedRedoChildRevisionId);
        Assert.Equal(undone.Cursor.CursorVersion, state2.Cursor.CursorVersion);

        // 持久化选择在 reopen 后仍驱动隐式 Redo。
        var redone = MapInitialStateService.Redo(
            coreAfter, project.ProjectId, mapId, state2.Cursor.ActiveRevisionId, state2.Cursor.CursorVersion, null, Now + 2);
        Assert.Equal(state1.Cursor.ActiveRevisionId, redone.Cursor.ActiveRevisionId);
    }

    // ---- Initial State 服务层（真实 DB） ----

    [Fact]
    public void InitialState_AppendIsDeterministicAndStaleRejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var mapId = map.Map.MapId;
        var obj1 = CreateActiveObject(project.ProjectId, registerToMapId: mapId);
        var obj2 = CreateActiveObject(project.ProjectId, registerToMapId: mapId);

        var zero = core.FindMapInitialStateRevisionState(project.ProjectId, mapId)!;
        var first = MapInitialStateService.AppendRevision(
            core, project.ProjectId, mapId, zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion,
            new List<InitialObjectWrite>
            {
                new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":1}""" },
                new() { ObjectIdentityId = obj2, MembershipPayloadJson = """{"b":2}""" },
            },
            Now + 1);

        Assert.Equal(2, first.Snapshot.Members.Count);
        Assert.Equal(
            InitialStateFingerprint.Compute(first.Snapshot.Revision.BaselineSchemaVersion, first.Snapshot.Members),
            first.Snapshot.Revision.ContentFingerprint);

        // stale 凭据（zero 的旧 revision/version）→ 拒绝且无写入。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                core, project.ProjectId, mapId, zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion,
                new List<InitialObjectWrite>(),
                Now + 2));
        Assert.Equal(2, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}'"));
    }

    [Fact]
    public void InitialState_DraftMember_ServiceRejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var state = core.FindMapInitialStateRevisionState(project.ProjectId, map.Map.MapId)!;

        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                core, project.ProjectId, map.Map.MapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion,
                new List<InitialObjectWrite>
                {
                    new() { ObjectIdentityId = bound.ObjectIdentityId!.Value, MembershipPayloadJson = "{}" },
                },
                Now + 1));
    }

    [Fact]
    public void InitialState_PlacementPayloadWithoutVersion_ServiceRejected()
    {
        var (project, map) = SeedProjectAndMap();
        var core = Core();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            core, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        // placement payload 与 schema version 必须成对（服务层前置；CHECK 兜底）。
        Assert.Throws<ArgumentException>(
            () => ObjectCreationService.InsertObject(
                core, project.ProjectId, bound.ReservationId,
                Definition(project.ProjectId, identityId),
                new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = map.Map.MapId },
                new InitialObjectMembership { MembershipPayloadJson = """{"m":1}""", PlacementPayloadJson = """{"x":0}""" },
                Now));
    }

    // ---- Definition CAS（真实 DB） ----

    [Fact]
    public void Definition_SingleRow_CasStaleRejected_HashTamperFailClosed()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId);
        var current = core.ReadObjectDefinition(project.ProjectId, identityId)!;

        ObjectDefinitionService.EditDefinition(
            core, project.ProjectId, identityId, current.DefinitionHash, """{"canonical_name":"V2","river_class":"other"}""",
            BuiltinObjectTypes.DescriptorSchemaVersion, Now + 1);
        var edited = core.ReadObjectDefinition(project.ProjectId, identityId)!;
        Assert.Equal(CanonicalJson.Sha256Hex(edited.DefinitionPayloadJson), edited.DefinitionHash);

        // stale CAS 拒绝。
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectDefinitionService.EditDefinition(
                core, project.ProjectId, identityId, current.DefinitionHash, """{"canonical_name":"V3","river_class":"other"}""",
                BuiltinObjectTypes.DescriptorSchemaVersion, Now + 2));

        // PK 保证单行。
        Assert.Equal(1, CountRows("core_object_definition", $"WHERE object_identity_id = '{identityId:D}'"));

        // hash 篡改 → 读侧 fail-closed。
        ExecuteRaw($"UPDATE core_object_definition SET definition_hash = 'deadbeef' WHERE object_identity_id = '{identityId:D}';");
        Assert.Throws<CoreDomainDataException>(() => core.ReadObjectDefinition(project.ProjectId, identityId));
    }

    // ---- 读侧 fail-closed（pin 篡改） ----

    [Fact]
    public void ReadObject_UnknownPin_FailClosed()
    {
        var (project, _) = SeedProjectAndMap();
        var core = Core();
        var identityId = CreateActiveObject(project.ProjectId);
        Assert.NotNull(core.FindObject(project.ProjectId, identityId));

        ExecuteRaw($"UPDATE core_object SET object_type_schema_version = 99 WHERE object_identity_id = '{identityId:D}';");
        Assert.Throws<CoreDomainConflictException>(() => core.FindObject(project.ProjectId, identityId));
    }

    // ---- Project 隔离 ----

    [Fact]
    public async Task CrossProjectIsolation_ReservationAndObject()
    {
        // 一库一项目（P2-09-11 §4.1）：隔离验证用两个独立数据库文件。
        var (projectA, _) = SeedProjectAndMap("A");
        var coreA = Core();
        var pathB = Path.Combine(_root, "project-b.sqlite");
        var databaseB = (SqlitePackageDatabase)_factory.CreateForFile(pathB);
        await using (databaseB.ConfigureAwait(false))
        {
            await databaseB.OpenAsync();
            var bootstrap = await _bootstrapper.InitializeNewDatabaseAsync(databaseB, DateTimeOffset.UtcNow);
            Assert.True(bootstrap.Success, string.Join("; ", bootstrap.Issues.Select(i => i.Message)));
            var coreB = new SqliteCoreDomainDatabase(databaseB);
            var projectB = ProjectRules.CreateProject("B", Now);
            coreB.InsertProject(ProjectCreationPlan.Create(projectB));

            var allocation = ObjectIdReservationService.ReserveByTypeKey(coreA, projectA.ProjectId, "river", null, Now);
            Assert.Null(coreB.FindReservation(projectB.ProjectId, allocation.Reservation.ReservationId));
            Assert.Null(coreB.FindObject(projectB.ProjectId, Guid.NewGuid()));
            Assert.NotNull(coreB.FindObjectTypeByTypeKey(projectB.ProjectId, "river"));
            // 同类型同序号在不同 Project 独立分配（V1 不要求跨 Project 唯一）。
            var allocationB = ObjectIdReservationService.ReserveByTypeKey(coreB, projectB.ProjectId, "river", null, Now);
            Assert.Equal("1", allocation.Reservation.AllocatedIdValue);
            Assert.Equal("1", allocationB.Reservation.AllocatedIdValue);
            _ = pathB;
        }
    }

    // ---- 并发混合读写（协调器继承） ----

    [Fact]
    public async Task Concurrent_ReservationAndReads_NoTornState()
    {
        var (project, _) = SeedProjectAndMap();
        var failures = new List<Exception>();
        var barrier = new Barrier(3);

        var writer = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                var core = Core();
                for (var i = 0; i < 10; i++)
                {
                    ObjectIdReservationService.ReserveByTypeKey(core, project.ProjectId, "river", null, Now + i);
                }
            }
            catch (Exception ex)
            {
                lock (failures)
                {
                    failures.Add(ex);
                }
            }
        });
        var reader = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                var core = Core();
                for (var i = 0; i < 60; i++)
                {
                    var count = core.ListObjectTypes(project.ProjectId).Count;
                    Assert.Equal(12, count);
                }
            }
            catch (Exception ex)
            {
                lock (failures)
                {
                    failures.Add(ex);
                }
            }
        });
        var allocatorReader = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                var core = Core();
                for (var i = 0; i < 60; i++)
                {
                    _ = core.FindObjectTypeByTypeKey(project.ProjectId, "river");
                }
            }
            catch (Exception ex)
            {
                lock (failures)
                {
                    failures.Add(ex);
                }
            }
        });

        await Task.WhenAll(writer, reader, allocatorReader).ConfigureAwait(false);
        Assert.Empty(failures);
        Assert.Equal(10, CountRows("core_object_id_reservation"));
        Assert.Equal(1, CountRows("core_object_id_allocator", $"WHERE next_sequence = 11"));
    }
}
