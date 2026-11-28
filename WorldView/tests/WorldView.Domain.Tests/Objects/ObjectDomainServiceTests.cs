using System;
using System.Collections.Generic;
using System.Linq;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Tests.TestDoubles;
using Xunit;

namespace WorldView.Domain.Tests.Objects;

/// <summary>
/// CD-02-B Domain 服务流测试（FakeCoreDomainDatabase；真实 SQLite 证据见
/// Infrastructure.Tests）。覆盖：播种、Reservation 状态机全链、TX-55 原子创建、
/// scope/注册/成员前置负向、Definition CAS、Initial State CAS 与指纹。
/// </summary>
public sealed class ObjectDomainServiceTests
{
    private const long Now = 1_000L;

    /// <summary>合法 river definition payload（registry typed 校验通过）。</summary>
    private const string RiverDefinition = """{"canonical_name":"Demo","river_class":"other"}""";

    private static (FakeCoreDomainDatabase Db, ProjectRecord Project, MapCreationPlan Map) Seed()
    {
        var db = new FakeCoreDomainDatabase();
        var project = ProjectRules.CreateProject("P", Now);
        db.InsertProject(ProjectCreationPlan.Create(project));
        var map = MapService.CreateMap(db, project.ProjectId, "M", Now);
        return (db, project, map);
    }

    // ---- 播种 ----

    [Fact]
    public void ProjectCreation_SeedsTenTypesAndAllocators()
    {
        var (db, project, _) = Seed();
        var types = db.ListObjectTypes(project.ProjectId).ToList();
        Assert.Equal(12, types.Count);
        Assert.All(types, t => Assert.True(t.Allocatable));
        Assert.All(types, t => Assert.Equal(ObjectAllocationFormat.VersionMinimalDecimal, t.AllocationFormatVersion));
        Assert.Equal(12, db.Allocators.Count(a => a.ProjectId == project.ProjectId && a.NextSequence == 1));
        Assert.Equal(12, types.Select(t => t.TypeKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("building", types.Select(t => t.TypeKey));
        Assert.Contains("cave_system", types.Select(t => t.TypeKey));
    }

    [Fact]
    public void ProjectCreation_SecondInsert_Fails()
    {
        var (db, project, _) = Seed();
        Assert.Throws<CoreDomainDataException>(
            () => db.InsertProject(ProjectCreationPlan.Create(
                ProjectRules.CreateProject("second", Now))));
        Assert.Single(db.Projects);
        _ = project;
    }

    // ---- Reservation ----

    [Fact]
    public void ReserveByTypeKey_AllocatesSequentialIds()
    {
        var (db, project, _) = Seed();
        var r1 = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var r2 = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        Assert.Equal(ReservationState.Reserved, r1.Reservation.State);
        Assert.Null(r1.Reservation.ObjectIdentityId);
        Assert.Equal("1", r1.Reservation.AllocatedIdValue);
        Assert.Equal("2", r2.Reservation.AllocatedIdValue);
        Assert.NotEqual(r1.Reservation.ReservationId, r2.Reservation.ReservationId);
    }

    [Fact]
    public void Reserve_RequestKey_Idempotent()
    {
        var (db, project, _) = Seed();
        var first = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", "req-1", Now);
        var retry = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", "req-1", Now + 5);
        Assert.False(first.RecoveredByRequestKey);
        Assert.True(retry.RecoveredByRequestKey);
        Assert.Equal(first.Reservation.ReservationId, retry.Reservation.ReservationId);
        Assert.Equal(first.Reservation.AllocatedIdValue, retry.Reservation.AllocatedIdValue);
        Assert.Equal(1, db.Reservations.Count); // 仅一条 reservation（重试不新分配）
    }

    [Fact]
    public void Reserve_UnknownTypeKey_FailClosed()
    {
        var (db, project, _) = Seed();
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "interior_space", null, Now));
    }

    [Fact]
    public void Reserve_NonAllocatable_Rejected()
    {
        var (db, project, _) = Seed();
        var type = db.FindObjectTypeByTypeKey(project.ProjectId, "river")!;
        db.ObjectTypes.RemoveAll(t => t.ObjectTypeId == type.ObjectTypeId);
        db.ObjectTypes.Add(type with { Allocatable = false });
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now));
    }

    // ---- Bind / Abandon ----

    [Fact]
    public void Bind_ReservedToBound_CreatesDraftIdentity()
    {
        var (db, project, _) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "settlement", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);

        Assert.Equal(ReservationState.Bound, bound.State);
        Assert.NotNull(bound.ObjectIdentityId);
        var identity = db.FindObject(project.ProjectId, bound.ObjectIdentityId!.Value)!;
        Assert.Equal(ObjectLifecycleStatus.Draft, identity.LifecycleStatus);
        Assert.Equal(BuiltinObjectTypes.DescriptorSchemaVersion, identity.ObjectTypeSchemaVersion);
        Assert.Equal("world-view.object-type.settlement.v1", identity.ObjectTypeSemanticsId);
        Assert.Equal(ObjectSharingScope.ProjectShared, identity.SharingScope);
        Assert.Null(identity.OwnerMapId);
    }

    [Fact]
    public void Bind_MapLocal_RequiresOwnerMap()
    {
        var (db, project, map) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "road", null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Bind(
                db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.MapLocal, null, Now));

        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.MapLocal, map.Map.MapId, Now);
        Assert.Equal(map.Map.MapId, db.FindObject(project.ProjectId, bound.ObjectIdentityId!.Value)!.OwnerMapId);
    }

    [Fact]
    public void Bind_Twice_SecondRejected()
    {
        var (db, project, _) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "lake", null, Now);
        ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Bind(
                db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now));
    }

    [Fact]
    public void Abandon_FromReserved_NoIdentityRow()
    {
        var (db, project, _) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var abandoned = ObjectIdReservationService.Abandon(db, project.ProjectId, allocation.Reservation.ReservationId, "user cancelled", Now);
        Assert.Equal(ReservationState.Abandoned, abandoned.State);
        Assert.Equal("user cancelled", abandoned.AbandonReason);
        Assert.Null(abandoned.ObjectIdentityId);
        Assert.Empty(db.Objects);
    }

    [Fact]
    public void Abandon_FromBound_LeavesTombstone()
    {
        var (db, project, _) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var abandoned = ObjectIdReservationService.Abandon(db, project.ProjectId, bound.ReservationId, null, Now);
        Assert.Equal(ReservationState.Abandoned, abandoned.State);
        var tombstone = db.FindObject(project.ProjectId, bound.ObjectIdentityId!.Value)!;
        Assert.Equal(ObjectLifecycleStatus.Abandoned, tombstone.LifecycleStatus);
    }

    [Fact]
    public void Abandon_InsertedOrAbandoned_Rejected()
    {
        var (db, project, map) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        ObjectCreationService.InsertObject(
            db, project.ProjectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = project.ProjectId,
                ObjectIdentityId = bound.ObjectIdentityId!.Value,
                DefinitionSchemaVersion = BuiltinObjectTypes.DescriptorSchemaVersion,
                DefinitionPayloadJson = RiverDefinition,
                DefinitionHash = "",
                UpdatedAtUtcMs = Now,
            },
            null, null, Now);

        // Inserted 为终态：不可放弃。
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectIdReservationService.Abandon(db, project.ProjectId, bound.ReservationId, null, Now));
    }

    // ---- 正式创建（TX-55 原子） ----

    [Fact]
    public void InsertObject_ProjectSharedWithRegistrationAndMembership_Atomic()
    {
        var (db, project, map) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        ObjectCreationService.InsertObject(
            db, project.ProjectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = project.ProjectId,
                ObjectIdentityId = identityId,
                DefinitionSchemaVersion = BuiltinObjectTypes.DescriptorSchemaVersion,
                DefinitionPayloadJson = """{"canonical_name":"Alpha","river_class":"other"}""",
                DefinitionHash = "",
                UpdatedAtUtcMs = Now,
            },
            new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = map.Map.MapId },
            new InitialObjectMembership { MembershipPayloadJson = """{"side":"north"}""" },
            Now);

        var identity = db.FindObject(project.ProjectId, identityId)!;
        Assert.Equal(ObjectLifecycleStatus.Active, identity.LifecycleStatus);
        Assert.Equal(ReservationState.Inserted, db.FindReservation(project.ProjectId, bound.ReservationId)!.State);

        // definition 首行已 canonical 化并带 hash。
        var definition = db.ReadObjectDefinition(project.ProjectId, identityId)!;
        Assert.Equal("""{"canonical_name":"Alpha","river_class":"other"}""", definition.DefinitionPayloadJson);
        Assert.Equal(CanonicalJson.Sha256Hex(definition.DefinitionPayloadJson), definition.DefinitionHash);

        // 注册行 + revision one 成员行（Insert 路径同事务追加 child revision）。
        Assert.NotNull(db.FindMapObjectRef(project.ProjectId, map.Map.MapId, identityId));
        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, map.Map.MapId);
        Assert.Equal(1, state.Snapshot.Revision.RevisionNumber);
        var members = state.Snapshot.Members;
        Assert.Single(members);
        Assert.Equal("""{"side":"north"}""", members[0].MembershipPayloadJson);
        Assert.Equal(identityId, members[0].ObjectIdentityId);
        // revision zero 不变且保留可读。
        var revisionZeroId = MapInitialStateService.ListRevisions(db, project.ProjectId, map.Map.MapId)
            .Single(r => r.RevisionNumber == 0).RevisionId;
        var revisionZero = MapInitialStateService.ReadRevisionSnapshot(
            db, project.ProjectId, map.Map.MapId, revisionZeroId);
        Assert.Equal(0, revisionZero.Revision.RevisionNumber);
        Assert.Empty(revisionZero.Members);
        Assert.Equal(MapInitialStateService.EmptySnapshotFingerprint, revisionZero.Revision.ContentFingerprint);
        Assert.Equal(
            InitialStateFingerprint.Compute(state.Snapshot.Revision.BaselineSchemaVersion, members),
            state.Snapshot.Revision.ContentFingerprint);
    }

    [Fact]
    public void InsertObject_DraftCannotRegisterOrJoinInitialState()
    {
        var (db, project, map) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        // draft 身份直接注册被拒（R-成员前置）。
        Assert.Throws<CoreDomainConflictException>(
            () => MapRegistrationService.Register(db, project.ProjectId, map.Map.MapId, identityId, Now));
    }

    [Fact]
    public void InsertObject_MembershipWithoutRegistration_Rejected()
    {
        var (db, project, map) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        Assert.Throws<CoreDomainConflictException>(
            () => ObjectCreationService.InsertObject(
                db, project.ProjectId, bound.ReservationId,
                new ObjectDefinitionRecord
                {
                    ProjectId = project.ProjectId,
                    ObjectIdentityId = identityId,
                    DefinitionSchemaVersion = BuiltinObjectTypes.DescriptorSchemaVersion,
                    DefinitionPayloadJson = RiverDefinition,
                    DefinitionHash = "",
                    UpdatedAtUtcMs = Now,
                },
                registration: null,
                new InitialObjectMembership { MembershipPayloadJson = "{}" },
                Now));
        _ = map;
    }

    [Fact]
    public void InsertObject_MapLocalToForeignMap_Rejected()
    {
        var (db, project, map) = Seed();
        var otherMap = MapService.CreateMap(db, project.ProjectId, "M2", Now);
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "road", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.MapLocal, map.Map.MapId, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        Assert.Throws<CoreDomainConflictException>(
            () => ObjectCreationService.InsertObject(
                db, project.ProjectId, bound.ReservationId,
                new ObjectDefinitionRecord
                {
                    ProjectId = project.ProjectId,
                    ObjectIdentityId = identityId,
                    DefinitionSchemaVersion = BuiltinObjectTypes.DescriptorSchemaVersion,
                    DefinitionPayloadJson = """{"road_class":"local"}""",
                    DefinitionHash = "",
                    UpdatedAtUtcMs = Now,
                },
                new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = otherMap.Map.MapId },
                null,
                Now));
    }

    [Fact]
    public void InsertObject_DefinitionSchemaVersionMismatch_Rejected()
    {
        var (db, project, _) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        Assert.Throws<CoreDomainConflictException>(
            () => ObjectCreationService.InsertObject(
                db, project.ProjectId, bound.ReservationId,
                new ObjectDefinitionRecord
                {
                    ProjectId = project.ProjectId,
                    ObjectIdentityId = identityId,
                    DefinitionSchemaVersion = 3,
                    DefinitionPayloadJson = RiverDefinition,
                    DefinitionHash = "",
                    UpdatedAtUtcMs = Now,
                },
                null, null, Now));
    }

    // ---- Definition CAS ----

    [Fact]
    public void EditDefinition_CasFlow()
    {
        var (db, project, _) = Seed();
        var identityId = CreateActiveObject(db, project.ProjectId);
        var current = db.ReadObjectDefinition(project.ProjectId, identityId)!;

        var updated = ObjectDefinitionService.EditDefinition(
            db, project.ProjectId, identityId, current.DefinitionHash, """{"canonical_name":"Beta","river_class":"other"}""",
            BuiltinObjectTypes.DescriptorSchemaVersion, Now + 10);
        Assert.Equal("""{"canonical_name":"Beta","river_class":"other"}""", updated.DefinitionPayloadJson);

        // stale 凭据再编辑 → CAS 冲突。
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectDefinitionService.EditDefinition(
                db, project.ProjectId, identityId, current.DefinitionHash, """{"canonical_name":"Gamma","river_class":"other"}""",
                BuiltinObjectTypes.DescriptorSchemaVersion, Now + 20));
    }

    [Fact]
    public void EditDefinition_DraftOrMissing_Rejected()
    {
        var (db, project, _) = Seed();
        var allocation = ObjectIdReservationService.ReserveByTypeKey(db, project.ProjectId, "river", null, Now);
        var bound = ObjectIdReservationService.Bind(
            db, project.ProjectId, allocation.Reservation.ReservationId, ObjectSharingScope.ProjectShared, null, Now);
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectDefinitionService.EditDefinition(
                db, project.ProjectId, bound.ObjectIdentityId!.Value, "any", "{}",
                BuiltinObjectTypes.DescriptorSchemaVersion, Now));
    }

    // ---- 注册/取消注册 ----

    [Fact]
    public void Registration_SharedObjectMultipleMaps_Ok()
    {
        var (db, project, map) = Seed();
        var map2 = MapService.CreateMap(db, project.ProjectId, "M2", Now);
        var identityId = CreateActiveObject(db, project.ProjectId);

        MapRegistrationService.Register(db, project.ProjectId, map.Map.MapId, identityId, Now);
        MapRegistrationService.Register(db, project.ProjectId, map2.Map.MapId, identityId, Now);
        Assert.Equal(2, db.ListMapObjectRefs(project.ProjectId, map.Map.MapId).Count
            + db.ListMapObjectRefs(project.ProjectId, map2.Map.MapId).Count);

        Assert.Throws<CoreDomainConflictException>(
            () => MapRegistrationService.Register(db, project.ProjectId, map.Map.MapId, identityId, Now));
    }

    [Fact]
    public void Unregister_WithMembership_Rejected()
    {
        var (db, project, map) = Seed();
        var identityId = CreateActiveObject(db, project.ProjectId, map.Map.MapId, withMembership: true);
        Assert.Throws<CoreDomainConflictException>(
            () => MapRegistrationService.Unregister(db, project.ProjectId, map.Map.MapId, identityId));
    }

    [Fact]
    public void Unregister_WithoutMembership_Ok()
    {
        var (db, project, map) = Seed();
        var identityId = CreateActiveObject(db, project.ProjectId);
        MapRegistrationService.Register(db, project.ProjectId, map.Map.MapId, identityId, Now);
        MapRegistrationService.Unregister(db, project.ProjectId, map.Map.MapId, identityId);
        Assert.Null(db.FindMapObjectRef(project.ProjectId, map.Map.MapId, identityId));
    }

    // ---- Initial State 修订（FOUNDATION-01B §6/§7） ----

    [Fact]
    public void InitialState_MapCreationEstablishesRevisionZeroAndCursor()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;

        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);
        Assert.Equal(0, state.Snapshot.Revision.RevisionNumber);
        Assert.Null(state.Snapshot.Revision.ParentRevisionId);
        Assert.Null(state.Snapshot.Revision.OperationKind);
        Assert.Empty(state.Snapshot.Members);
        Assert.Equal(MapInitialStateService.EmptySnapshotFingerprint, state.Snapshot.Revision.ContentFingerprint);
        Assert.Equal(0, state.Cursor.CursorVersion);
        Assert.Null(state.Cursor.SelectedRedoChildRevisionId);
        Assert.Empty(state.RedoChildren);
        Assert.NotNull(db.FindMapInitialStateBaseline(project.ProjectId, mapId));
    }

    [Fact]
    public void InitialState_AppendCreatesChildRevisionWithDeterministicFingerprintAndOperation()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;
        var obj1 = CreateActiveObject(db, project.ProjectId, mapId);

        var zero = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);
        var afterFirst = MapInitialStateService.AppendRevision(
            db, project.ProjectId, mapId,
            zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion,
            new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":1}""" } },
            Now + 1);

        Assert.Equal(1, afterFirst.Snapshot.Revision.RevisionNumber);
        Assert.Equal(zero.Cursor.ActiveRevisionId, afterFirst.Snapshot.Revision.ParentRevisionId);
        Assert.Equal(InitialStateRevisionRules.SnapshotReplaceOperationKind, afterFirst.Snapshot.Revision.OperationKind);
        Assert.Single(afterFirst.Snapshot.Members);
        Assert.Equal(zero.Cursor.CursorVersion + 1, afterFirst.Cursor.CursorVersion);
        Assert.Empty(afterFirst.RedoChildren);
        Assert.Equal(
            InitialStateFingerprint.Compute(
                afterFirst.Snapshot.Revision.BaselineSchemaVersion, afterFirst.Snapshot.Members),
            afterFirst.Snapshot.Revision.ContentFingerprint);

        // 操作描述记录了完整 diff（added/removed/updated 互斥且排序）。
        var payload = System.Text.Json.JsonDocument.Parse(afterFirst.Snapshot.Revision.OperationPayloadJson!).RootElement;
        Assert.Equal(new[] { obj1.ToString("D") }, payload.GetProperty("added").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Empty(payload.GetProperty("removed").EnumerateArray());
        Assert.Empty(payload.GetProperty("updated").EnumerateArray());

        // revision zero 原样保留（不可变、永久可寻址）。
        var rereadZero = MapInitialStateService.ReadRevisionSnapshot(
            db, project.ProjectId, mapId, afterFirst.Snapshot.Revision.ParentRevisionId!.Value);
        Assert.Equal(MapInitialStateService.EmptySnapshotFingerprint, rereadZero.Revision.ContentFingerprint);
        Assert.Empty(rereadZero.Members);
    }

    [Fact]
    public void InitialState_StaleExpectedRevisionOrCursorVersion_Rejected()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;
        var obj1 = CreateActiveObject(db, project.ProjectId, mapId);
        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);

        // 错误 cursor version。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                db, project.ProjectId, mapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion + 5,
                new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = "{}" } },
                Now + 1));

        // 错误 active revision。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                db, project.ProjectId, mapId, Guid.NewGuid(), state.Cursor.CursorVersion,
                new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = "{}" } },
                Now + 1));

        // 失败不产生任何 revision 或 cursor mutation。
        var after = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);
        Assert.Equal(state.Cursor.CursorVersion, after.Cursor.CursorVersion);
        Assert.Single(MapInitialStateService.ListRevisions(db, project.ProjectId, mapId));
    }

    [Fact]
    public void InitialState_UndoRedoAndStaleRejections()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;
        // 不带 membership 创建（不追加 revision），保持 cursor 在 revision zero。
        var obj1 = CreateActiveObject(db, project.ProjectId, mapId);

        var zero = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);

        // root 不可 Undo。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.Undo(
                db, project.ProjectId, mapId, zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion, Now + 1));

        // 首次编辑 → revision one。
        var first = MapInitialStateService.AppendRevision(
            db, project.ProjectId, mapId, zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion,
            new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":1}""" } },
            Now + 2);
        var leftRevision = first.Cursor.ActiveRevisionId;

        // Undo：cursor 回 parent，selected redo child 指向刚离开的 revision。
        var undone = MapInitialStateService.Undo(
            db, project.ProjectId, mapId, first.Cursor.ActiveRevisionId, first.Cursor.CursorVersion, Now + 3);
        Assert.Equal(0, undone.Snapshot.Revision.RevisionNumber);
        Assert.Empty(undone.Snapshot.Members);
        Assert.Equal(leftRevision, undone.Cursor.SelectedRedoChildRevisionId);
        Assert.Single(undone.RedoChildren);

        // stale Undo 凭据拒绝。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.Undo(
                db, project.ProjectId, mapId, first.Cursor.ActiveRevisionId, first.Cursor.CursorVersion, Now + 4));

        // Redo：回到刚离开的 revision；redo 选择被清除。
        var redone = MapInitialStateService.Redo(
            db, project.ProjectId, mapId, undone.Cursor.ActiveRevisionId, undone.Cursor.CursorVersion, null, Now + 5);
        Assert.Equal(leftRevision, redone.Cursor.ActiveRevisionId);
        Assert.Null(redone.Cursor.SelectedRedoChildRevisionId);
        Assert.Single(redone.Snapshot.Members);
        Assert.Equal(obj1, redone.Snapshot.Members[0].ObjectIdentityId);

        // Redo 后无 redo child → 明确领域错误。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.Redo(
                db, project.ProjectId, mapId, redone.Cursor.ActiveRevisionId, redone.Cursor.CursorVersion, null, Now + 6));
    }

    [Fact]
    public void InitialState_EditAfterUndoCreatesBranchAndPreservesDescendants()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;
        var obj1 = CreateActiveObject(db, project.ProjectId, mapId);
        var obj2 = CreateActiveObject(db, project.ProjectId, mapId);

        var zero = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);
        var first = MapInitialStateService.AppendRevision(
            db, project.ProjectId, mapId, zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion,
            new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":1}""" } },
            Now + 1);
        var undone = MapInitialStateService.Undo(
            db, project.ProjectId, mapId,
            first.Cursor.ActiveRevisionId, first.Cursor.CursorVersion, Now + 2);

        // Undo 后新编辑 → 新分支（同 parent 的第二个 child）；旧后代保留。
        var branched = MapInitialStateService.AppendRevision(
            db, project.ProjectId, mapId, undone.Cursor.ActiveRevisionId, undone.Cursor.CursorVersion,
            new List<InitialObjectWrite> { new() { ObjectIdentityId = obj2, MembershipPayloadJson = """{"b":2}""" } },
            Now + 3);

        Assert.Equal(zero.Cursor.ActiveRevisionId, branched.Snapshot.Revision.ParentRevisionId);
        Assert.Equal(2, branched.Snapshot.Revision.RevisionNumber);
        Assert.Single(branched.Snapshot.Members);
        Assert.Equal(obj2, branched.Snapshot.Members[0].ObjectIdentityId);

        // 活动 revision 有两个直接子（旧分支 + 新分支）。
        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);
        Assert.Equal(branched.Cursor.ActiveRevisionId, state.Cursor.ActiveRevisionId);
        Assert.Equal(zero.Cursor.ActiveRevisionId, state.Snapshot.Revision.ParentRevisionId);
        Assert.Empty(state.RedoChildren); // cursor 已在新分支上，直接子集按 active revision 计算。

        var zeroChildren = MapInitialStateService.ListRevisions(db, project.ProjectId, mapId)
            .Where(r => r.ParentRevisionId == zero.Cursor.ActiveRevisionId)
            .Select(r => r.RevisionId.ToString("D"))
            .ToHashSet(System.StringComparer.Ordinal);
        Assert.Equal(2, zeroChildren.Count);
        Assert.Contains(first.Cursor.ActiveRevisionId.ToString("D"), zeroChildren);
        Assert.Contains(branched.Cursor.ActiveRevisionId.ToString("D"), zeroChildren);

        // 多子且无选择 → Redo 必须显式选择。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.Redo(
                db, project.ProjectId, mapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion, null, Now + 4));
    }

    [Fact]
    public void InitialState_RedoChildSelectionPersistsAndConstrainsRedo()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;
        var obj1 = CreateActiveObject(db, project.ProjectId, mapId);
        var obj2 = CreateActiveObject(db, project.ProjectId, mapId);

        var zero = MapInitialStateService.ReadActiveState(db, project.ProjectId, mapId);
        var first = MapInitialStateService.AppendRevision(
            db, project.ProjectId, mapId, zero.Cursor.ActiveRevisionId, zero.Cursor.CursorVersion,
            new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":1}""" } },
            Now + 1);
        var undone1 = MapInitialStateService.Undo(
            db, project.ProjectId, mapId, first.Cursor.ActiveRevisionId, first.Cursor.CursorVersion, Now + 2);
        var branch = MapInitialStateService.AppendRevision(
            db, project.ProjectId, mapId, undone1.Cursor.ActiveRevisionId, undone1.Cursor.CursorVersion,
            new List<InitialObjectWrite> { new() { ObjectIdentityId = obj2, MembershipPayloadJson = """{"b":2}""" } },
            Now + 3);

        // 再 Undo 回到 revision zero：此时 zero 有两个直接子（旧分支 + 新分支），
        // selected redo child 指向刚离开的新分支。
        var undone2 = MapInitialStateService.Undo(
            db, project.ProjectId, mapId, branch.Cursor.ActiveRevisionId, branch.Cursor.CursorVersion, Now + 4);
        Assert.Equal(0, undone2.Snapshot.Revision.RevisionNumber);
        Assert.Equal(branch.Cursor.ActiveRevisionId, undone2.Cursor.SelectedRedoChildRevisionId);
        Assert.Equal(2, undone2.RedoChildren.Count);

        // 非直接子（revision zero 自己）不可选择。
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.SelectRedoChild(
                db, project.ProjectId, mapId, undone2.Cursor.ActiveRevisionId, undone2.Cursor.CursorVersion,
                zero.Cursor.ActiveRevisionId, Now + 5));

        // 显式选择旧分支（first）；选择持久化在 cursor 上（close/reopen 语义）。
        var selected = MapInitialStateService.SelectRedoChild(
            db, project.ProjectId, mapId, undone2.Cursor.ActiveRevisionId, undone2.Cursor.CursorVersion,
            first.Cursor.ActiveRevisionId, Now + 6);
        Assert.Equal(first.Cursor.ActiveRevisionId, selected.Cursor.SelectedRedoChildRevisionId);
        Assert.Equal(undone2.Cursor.ActiveRevisionId, selected.Cursor.ActiveRevisionId);

        // 持久化选择决定隐式 Redo 目标；Redo 后选择清除。
        var redone = MapInitialStateService.Redo(
            db, project.ProjectId, mapId, selected.Cursor.ActiveRevisionId, selected.Cursor.CursorVersion, null, Now + 7);
        Assert.Equal(first.Cursor.ActiveRevisionId, redone.Cursor.ActiveRevisionId);
        Assert.Null(redone.Cursor.SelectedRedoChildRevisionId);
        Assert.Single(redone.Snapshot.Members);
        Assert.Equal(obj1, redone.Snapshot.Members[0].ObjectIdentityId);
    }

    [Fact]
    public void InitialState_MemberWithoutRegistration_Rejected()
    {
        var (db, project, map) = Seed();
        var orphan = CreateActiveObject(db, project.ProjectId); // 未注册
        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, map.Map.MapId);
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                db, project.ProjectId, map.Map.MapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion,
                new List<InitialObjectWrite>
                {
                    new() { ObjectIdentityId = orphan, MembershipPayloadJson = "{}" },
                },
                Now + 1));
    }

    [Fact]
    public void InitialState_DuplicateMembers_Rejected()
    {
        var (db, project, map) = Seed();
        var obj = CreateActiveObject(db, project.ProjectId, map.Map.MapId);
        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, map.Map.MapId);
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.AppendRevision(
                db, project.ProjectId, map.Map.MapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion,
                new List<InitialObjectWrite>
                {
                    new() { ObjectIdentityId = obj, MembershipPayloadJson = "{}" },
                    new() { ObjectIdentityId = obj, MembershipPayloadJson = "{}" },
                },
                Now + 1));
    }

    [Fact]
    public void InitialState_PlacementPayloadWithoutVersion_Rejected()
    {
        var (db, project, map) = Seed();
        var obj = CreateActiveObject(db, project.ProjectId, map.Map.MapId);
        var state = MapInitialStateService.ReadActiveState(db, project.ProjectId, map.Map.MapId);
        Assert.Throws<ArgumentException>(
            () => MapInitialStateService.AppendRevision(
                db, project.ProjectId, map.Map.MapId, state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion,
                new List<InitialObjectWrite>
                {
                    new() { ObjectIdentityId = obj, MembershipPayloadJson = "{}", PlacementPayloadJson = """{"x":0}""" },
                },
                Now + 1));
    }

    // ---- 辅助 ----

    private static Guid CreateActiveObject(
        FakeCoreDomainDatabase db,
        Guid projectId,
        Guid? ownerMapId = null,
        bool withMembership = false)
    {
        var scope = ownerMapId is null ? ObjectSharingScope.ProjectShared : ObjectSharingScope.MapLocal;
        var allocation = ObjectIdReservationService.Reserve(db, projectId, db.FindObjectTypeByTypeKey(projectId, "river")!.ObjectTypeId, null, Now);
        var bound = ObjectIdReservationService.Bind(db, projectId, allocation.Reservation.ReservationId, scope, ownerMapId, Now);
        var identityId = bound.ObjectIdentityId!.Value;

        var registration = ownerMapId is null
            ? null
            : new MapObjectRegistration { MapObjectRefId = Guid.NewGuid(), MapId = ownerMapId.Value };
        var membership = withMembership
            ? new InitialObjectMembership { MembershipPayloadJson = """{"m":true}""" }
            : null;

        ObjectCreationService.InsertObject(
            db, projectId, bound.ReservationId,
            new ObjectDefinitionRecord
            {
                ProjectId = projectId,
                ObjectIdentityId = identityId,
                DefinitionSchemaVersion = BuiltinObjectTypes.DescriptorSchemaVersion,
                DefinitionPayloadJson = RiverDefinition,
                DefinitionHash = "",
                UpdatedAtUtcMs = Now,
            },
            registration,
            membership,
            Now);
        return identityId;
    }
}
