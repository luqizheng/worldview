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

        // 注册行 + 成员行 + 基线。
        Assert.NotNull(db.FindMapObjectRef(project.ProjectId, map.Map.MapId, identityId));
        var members = db.ListInitialObjects(project.ProjectId, map.Map.MapId);
        Assert.Single(members);
        Assert.Equal("""{"side":"north"}""", members[0].MembershipPayloadJson);
        var baseline = db.FindMapInitialState(project.ProjectId, map.Map.MapId)!;
        Assert.Equal(
            InitialStateFingerprint.Compute(baseline.BaselineSchemaVersion, members),
            baseline.ContentFingerprint);
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

    // ---- Initial State revisions / cursor ----

    [Fact]
    public void InitialState_AppendsImmutableSnapshotsAndSupportsUndoRedoBranching()
    {
        var (db, project, map) = Seed();
        var mapId = map.Map.MapId;
        var obj1 = CreateActiveObject(db, project.ProjectId, mapId);
        var obj2 = CreateActiveObject(db, project.ProjectId, mapId);

        var revisionZero = db.FindMapInitialState(project.ProjectId, mapId)!;
        Assert.Equal(0, revisionZero.ActiveRevisionNumber);
        Assert.Empty(db.ListInitialObjects(project.ProjectId, mapId));

        var revisionOne = MapInitialStateService.EditInitialState(
            db, project.ProjectId, mapId, revisionZero.ActiveRevisionId, revisionZero.CursorVersion,
            MapInitialStateService.StructuralBaselineSchemaVersion,
            new List<InitialObjectWrite>
            {
                new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":1}""" },
            },
            Now + 1);
        Assert.Equal(1, revisionOne.ActiveRevisionNumber);
        Assert.Single(db.ListInitialObjects(project.ProjectId, mapId));
        Assert.Empty(db.ListInitialStateRevisionObjects(project.ProjectId, mapId, revisionZero.ActiveRevisionId));

        var revisionTwo = MapInitialStateService.EditInitialState(
            db, project.ProjectId, mapId, revisionOne.ActiveRevisionId, revisionOne.CursorVersion,
            MapInitialStateService.StructuralBaselineSchemaVersion,
            new List<InitialObjectWrite>
            {
                new() { ObjectIdentityId = obj2, MembershipPayloadJson = """{"b":2}""" },
            },
            Now + 2);
        Assert.Equal(2, revisionTwo.ActiveRevisionNumber);
        Assert.Equal(obj2, db.ListInitialObjects(project.ProjectId, mapId).Single().ObjectIdentityId);
        Assert.Equal(obj1, db.ListInitialStateRevisionObjects(project.ProjectId, mapId, revisionOne.ActiveRevisionId).Single().ObjectIdentityId);

        // A stale active revision/cursor pair cannot append.
        Assert.Throws<CoreDomainConflictException>(() =>
            MapInitialStateService.EditInitialState(
                db, project.ProjectId, mapId, revisionOne.ActiveRevisionId, revisionOne.CursorVersion,
                MapInitialStateService.StructuralBaselineSchemaVersion,
                new List<InitialObjectWrite> { new() { ObjectIdentityId = obj1, MembershipPayloadJson = "{}" } },
                Now + 3));

        var undone = MapInitialStateService.Undo(
            db, project.ProjectId, mapId, revisionTwo.ActiveRevisionId, revisionTwo.CursorVersion, Now + 4);
        Assert.Equal(revisionOne.ActiveRevisionId, undone.ActiveRevisionId);
        Assert.Equal(revisionTwo.ActiveRevisionId, undone.SelectedRedoChildRevisionId);
        Assert.Equal(obj1, db.ListInitialObjects(project.ProjectId, mapId).Single().ObjectIdentityId);

        var redone = MapInitialStateService.Redo(
            db, project.ProjectId, mapId, undone.ActiveRevisionId, undone.CursorVersion,
            revisionTwo.ActiveRevisionId, Now + 5);
        Assert.Equal(revisionTwo.ActiveRevisionId, redone.ActiveRevisionId);
        Assert.Equal(obj2, db.ListInitialObjects(project.ProjectId, mapId).Single().ObjectIdentityId);

        // Undo and edit again: create a sibling revision, preserving the abandoned descendant.
        var backAtOne = MapInitialStateService.Undo(
            db, project.ProjectId, mapId, redone.ActiveRevisionId, redone.CursorVersion, Now + 6);
        var revisionThree = MapInitialStateService.EditInitialState(
            db, project.ProjectId, mapId, backAtOne.ActiveRevisionId, backAtOne.CursorVersion,
            MapInitialStateService.StructuralBaselineSchemaVersion,
            new List<InitialObjectWrite>
            {
                new() { ObjectIdentityId = obj1, MembershipPayloadJson = """{"a":3}""" },
            },
            Now + 7);
        var revisions = db.ListMapInitialStateRevisions(project.ProjectId, mapId);
        Assert.Equal(new long[] { 0, 1, 2, 3 }, revisions.Select(x => x.RevisionNumber));
        Assert.Equal(revisionOne.ActiveRevisionId, revisions.Single(x => x.RevisionId == revisionTwo.ActiveRevisionId).ParentRevisionId);
        Assert.Equal(revisionOne.ActiveRevisionId, revisions.Single(x => x.RevisionId == revisionThree.ActiveRevisionId).ParentRevisionId);

        // The abandoned branch remains redoable after reopening the logical cursor.
        var undoneBranch = MapInitialStateService.Undo(
            db, project.ProjectId, mapId, revisionThree.ActiveRevisionId, revisionThree.CursorVersion, Now + 8);
        var selectedOldBranch = MapInitialStateService.Redo(
            db, project.ProjectId, mapId, undoneBranch.ActiveRevisionId, undoneBranch.CursorVersion,
            revisionTwo.ActiveRevisionId, Now + 9);
        Assert.Equal(revisionTwo.ActiveRevisionId, selectedOldBranch.ActiveRevisionId);
        Assert.Equal(obj2, db.ListInitialObjects(project.ProjectId, mapId).Single().ObjectIdentityId);
    }

    [Fact]
    public void InitialState_MemberWithoutRegistration_Rejected()
    {
        var (db, project, map) = Seed();
        var orphan = CreateActiveObject(db, project.ProjectId); // 未注册
        var baseline = db.FindMapInitialState(project.ProjectId, map.Map.MapId)!;
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.EditInitialState(
                db, project.ProjectId, map.Map.MapId, baseline.ActiveRevisionId, baseline.CursorVersion,
                MapInitialStateService.StructuralBaselineSchemaVersion,
                new List<InitialObjectWrite>
                {
                    new() { ObjectIdentityId = orphan, MembershipPayloadJson = "{}" },
                },
                Now));
    }

    [Fact]
    public void InitialState_DuplicateMembers_Rejected()
    {
        var (db, project, map) = Seed();
        var obj = CreateActiveObject(db, project.ProjectId, map.Map.MapId);
        var baseline = db.FindMapInitialState(project.ProjectId, map.Map.MapId)!;
        Assert.Throws<CoreDomainConflictException>(
            () => MapInitialStateService.EditInitialState(
                db, project.ProjectId, map.Map.MapId, baseline.ActiveRevisionId, baseline.CursorVersion,
                MapInitialStateService.StructuralBaselineSchemaVersion,
                new List<InitialObjectWrite>
                {
                    new() { ObjectIdentityId = obj, MembershipPayloadJson = "{}" },
                    new() { ObjectIdentityId = obj, MembershipPayloadJson = "{}" },
                },
                Now));
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
