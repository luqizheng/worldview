using System;
using System.Collections.Generic;
using System.Linq;
using WorldView.Domain.Assets;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;

namespace WorldView.Domain.Tests.TestDoubles;

/// <summary>
/// ICoreDomainDatabase 的内存测试替身（仅 Domain 单元测试使用；真实持久化证据
/// 由 Infrastructure.Tests 的真实 SQLite 测试提供，不以本类冒充集成）。
/// 模拟端口的事务语义：状态机转换前置重读校验、CAS 比对、UNIQUE/FK 语义，
/// 但不模拟并发撕裂/连接协调（那属真实 SQLite 测试域）。
/// </summary>
public sealed class FakeCoreDomainDatabase : ICoreDomainDatabase
{
    public List<ProjectRecord> Projects { get; } = new();

    public List<MapRecord> Maps { get; } = new();

    public List<SpatialReferenceDefinitionRecord> SpatialReferenceDefinitions { get; } = new();

    public List<HistoryRecord> Histories { get; } = new();

    public List<BranchRecord> Branches { get; } = new();

    public List<CalendarRecord> Calendars { get; } = new();

    public List<CalendarConfigurationRecord> CalendarConfigurations { get; } = new();

    public List<ObjectTypeRecord> ObjectTypes { get; } = new();

    public List<ObjectRecord> Objects { get; } = new();

    public List<ObjectDefinitionRecord> Definitions { get; } = new();

    public List<(Guid ProjectId, Guid ObjectTypeId, long NextSequence)> Allocators { get; } = new();

    public List<ReservationRecord> Reservations { get; } = new();

    public List<MapObjectRefRecord> MapObjectRefs { get; } = new();

    public List<AssetRecord> Assets { get; } = new();

    public List<ObjectDefinitionAssetLinkRecord> DefinitionAssetLinks { get; } = new();

    public List<FieldDatasetRecord> FieldDatasets { get; } = new();

    public List<FieldRevisionRecord> FieldRevisions { get; } = new();

    public List<MapInitialStateRecord> InitialStates { get; } = new();

    public List<InitialObjectRecord> InitialObjects { get; } = new();

    public ProjectRecord? FindProject(Guid projectId) => Projects.FirstOrDefault(p => p.ProjectId == projectId);

    public IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows() => Projects
        .Select(p => new RawProjectIdentityRow
        {
            ProjectIdText = p.ProjectId.ToString("D"),
            ProjectName = p.ProjectName,
            ProjectFormatVersion = p.ProjectFormatVersion,
            CreatedAtUtcMs = p.CreatedAtUtcMs,
            UpdatedAtUtcMs = p.UpdatedAtUtcMs,
        })
        .ToArray();

    public void InsertProject(ProjectCreationPlan plan)
    {
        if (Projects.Count > 0)
        {
            throw new CoreDomainDataException("已存在项目身份（一库一项目）。");
        }

        var typeKeys = plan.BuiltinObjectTypes.Select(t => t.TypeKey).ToHashSet(StringComparer.Ordinal);
        if (typeKeys.Count != plan.BuiltinObjectTypes.Count)
        {
            throw new CoreDomainDataException("内置类型种子行含重复 type_key。");
        }

        Projects.Add(plan.Project);
        ObjectTypes.AddRange(plan.BuiltinObjectTypes);
        foreach (var type in plan.BuiltinObjectTypes.Where(t => t.Allocatable))
        {
            Allocators.Add((type.ProjectId, type.ObjectTypeId, 1));
        }
    }

    public MapRecord? FindMap(Guid projectId, Guid mapId) =>
        Maps.FirstOrDefault(m => m.ProjectId == projectId && m.MapId == mapId);

    public SpatialReferenceDefinitionRecord? FindSpatialReferenceDefinition(Guid projectId, string spatialReferenceId) =>
        SpatialReferenceDefinitions.FirstOrDefault(x => x.ProjectId == projectId && x.SpatialReferenceId == spatialReferenceId);

    public void InsertSpatialReferenceDefinition(SpatialReferenceDefinitionRecord definition)
    {
        if (FindProject(definition.ProjectId) is null)
            throw new CoreDomainConflictException("Spatial Reference Project 不存在。");
        if (FindSpatialReferenceDefinition(definition.ProjectId, definition.SpatialReferenceId) is not null)
            throw new CoreDomainConflictException("Spatial Reference identity 已存在且不可覆盖。");
        SpatialReferenceDefinitions.Add(SpatialReferenceRules.Validate(definition));
    }

    public MapRecord BindMapSpatialReference(Guid projectId, Guid mapId, string spatialReferenceId, long nowUtcMs)
    {
        var map = FindMap(projectId, mapId) ?? throw new CoreDomainConflictException("Map 不存在或不属于当前 Project。");
        if (FindSpatialReferenceDefinition(projectId, spatialReferenceId) is null)
            throw new CoreDomainConflictException("Spatial Reference 不存在或不属于当前 Project。");
        if (map.SpatialReferenceId is not null)
        {
            if (map.SpatialReferenceId == spatialReferenceId) return map;
            throw new CoreDomainConflictException("Map 的 Spatial Reference 已绑定；更换必须经过坐标转换流程。");
        }
        var bound = map with { SpatialReferenceId = spatialReferenceId, UpdatedAtUtcMs = nowUtcMs };
        Maps.Remove(map);
        Maps.Add(bound);
        return bound;
    }

    public IReadOnlyList<MapRecord> ListMaps(Guid projectId) =>
        Maps.Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.CreatedAtUtcMs)
            .ThenBy(m => m.MapId)
            .ToArray();

    public void CreateMapStructure(MapCreationPlan plan)
    {
        Maps.Add(plan.Map);
        Histories.Add(plan.RootHistory);
        Calendars.Add(plan.DefaultCalendar);
        CalendarConfigurations.Add(plan.DayZeroConfiguration);
    }

    public HistoryRecord? FindHistory(Guid projectId, Guid historyId) =>
        Histories.FirstOrDefault(h => h.ProjectId == projectId && h.HistoryId == historyId);

    public HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId) =>
        Histories.FirstOrDefault(h =>
            h.ProjectId == projectId && h.Kind == HistoryKind.MapRoot && h.OwnerMapId == mapId);

    public BranchRecord? FindBranch(Guid projectId, Guid branchId) =>
        Branches.FirstOrDefault(b => b.ProjectId == projectId && b.BranchId == branchId);

    public BranchRecord? FindBranchByOwnHistory(Guid projectId, Guid ownHistoryId) =>
        Branches.FirstOrDefault(b => b.ProjectId == projectId && b.OwnHistoryId == ownHistoryId);

    public void CreateBranchStructure(BranchCreationPlan plan)
    {
        Histories.Add(plan.OwnHistory);
        Branches.Add(plan.Branch);
    }

    // ---- Object Type ----

    public IReadOnlyList<ObjectTypeRecord> ListObjectTypes(Guid projectId) =>
        ObjectTypes.Where(t => t.ProjectId == projectId).OrderBy(t => t.TypeKey, StringComparer.Ordinal).ToArray();

    public ObjectTypeRecord? FindObjectType(Guid projectId, Guid objectTypeId) =>
        ObjectTypes.FirstOrDefault(t => t.ProjectId == projectId && t.ObjectTypeId == objectTypeId);

    public ObjectTypeRecord? FindObjectTypeByTypeKey(Guid projectId, string typeKey) =>
        ObjectTypes.FirstOrDefault(t => t.ProjectId == projectId && t.TypeKey == typeKey);

    // ---- Reservation ----

    public ReservationRecord? FindReservation(Guid projectId, Guid reservationId) =>
        Reservations.FirstOrDefault(r => r.ProjectId == projectId && r.ReservationId == reservationId);

    public ReservationAllocation ReserveObjectId(ObjectIdReservationRequest request)
    {
        if (request.RequestKey is not null)
        {
            var recovered = Reservations.FirstOrDefault(r =>
                r.ProjectId == request.ProjectId && r.RequestKey == request.RequestKey);
            if (recovered is not null)
            {
                if (recovered.ObjectTypeId != request.ObjectTypeId)
                {
                    throw new CoreDomainConflictException("request_key 已属于不同 Object Type 的分配，拒绝幂等复用。");
                }

                return new ReservationAllocation { Reservation = recovered, RecoveredByRequestKey = true };
            }
        }

        var type = FindObjectType(request.ProjectId, request.ObjectTypeId)
            ?? throw new CoreDomainConflictException("Object Type 不存在，拒绝分配。");
        if (!type.Allocatable)
        {
            throw new CoreDomainConflictException("该 Object Type 不可分配。");
        }

        (Guid ProjectId, Guid ObjectTypeId, long NextSequence)? allocatorLookup = Allocators
            .Where(a => a.ProjectId == request.ProjectId && a.ObjectTypeId == request.ObjectTypeId)
            .Cast<(Guid ProjectId, Guid ObjectTypeId, long NextSequence)?>()
            .FirstOrDefault();
        if (allocatorLookup is null)
        {
            throw new CoreDomainDataException("缺少 allocator 行。");
        }

        var allocator = allocatorLookup.Value;
        var sequence = allocator.NextSequence;
        Allocators.Remove(allocator);
        Allocators.Add((allocator.ProjectId, allocator.ObjectTypeId, sequence + 1));

        var reservation = new ReservationRecord
        {
            ReservationId = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            ObjectTypeId = request.ObjectTypeId,
            ObjectIdentityId = null,
            AllocatedSequence = sequence,
            AllocatedIdValue = ObjectAllocationFormat.DeriveAllocatedIdValue(sequence),
            State = ReservationState.Reserved,
            RequestKey = request.RequestKey,
            AbandonReason = null,
            CreatedAtUtcMs = request.NowUtcMs,
            UpdatedAtUtcMs = request.NowUtcMs,
        };

        if (Reservations.Any(r => r.ProjectId == request.ProjectId
                && r.ObjectTypeId == request.ObjectTypeId
                && r.AllocatedSequence == sequence))
        {
            throw new CoreDomainConflictException("分配序号冲突（UNIQUE 语义）。");
        }

        Reservations.Add(reservation);
        return new ReservationAllocation { Reservation = reservation, RecoveredByRequestKey = false };
    }

    public ReservationRecord BindReservation(ObjectBindRequest request)
    {
        var reservation = Reservations.FirstOrDefault(r =>
                r.ProjectId == request.ProjectId && r.ReservationId == request.ReservationId)
            ?? throw new CoreDomainConflictException("Reservation 不存在。");
        ReservationRules.ValidateTransition(reservation.State, ReservationState.Bound);

        var type = FindObjectType(request.ProjectId, reservation.ObjectTypeId)
            ?? throw new CoreDomainDataException("Reservation 引用的 Object Type 不存在。");
        var descriptor = WorldView.Domain.Registry.ObjectTypeRegistry.Require(type.TypeKey);
        ObjectRules.ValidatePin(type.TypeKey, descriptor.SchemaVersion, descriptor.SemanticsId);
        ObjectRules.ValidateScopePairing(request.SharingScope, request.OwnerMapId);

        var identity = new ObjectRecord
        {
            ObjectIdentityId = request.ObjectIdentityId,
            ProjectId = request.ProjectId,
            ObjectTypeId = reservation.ObjectTypeId,
            ObjectTypeSchemaVersion = descriptor.SchemaVersion,
            ObjectTypeSemanticsId = descriptor.SemanticsId,
            SharingScope = request.SharingScope,
            OwnerMapId = request.OwnerMapId,
            LifecycleStatus = ObjectLifecycleStatus.Draft,
            CreatedAtUtcMs = request.NowUtcMs,
            UpdatedAtUtcMs = request.NowUtcMs,
        };
        Objects.Add(identity);

        var bound = reservation with
        {
            ObjectIdentityId = request.ObjectIdentityId,
            State = ReservationState.Bound,
            UpdatedAtUtcMs = request.NowUtcMs,
        };
        Reservations.Remove(reservation);
        Reservations.Add(bound);
        return bound;
    }

    public ReservationRecord AbandonReservation(Guid projectId, Guid reservationId, string? abandonReason, long nowUtcMs)
    {
        var reservation = Reservations.FirstOrDefault(r =>
                r.ProjectId == projectId && r.ReservationId == reservationId)
            ?? throw new CoreDomainConflictException("Reservation 不存在。");
        ReservationRules.ValidateTransition(reservation.State, ReservationState.Abandoned);

        if (reservation.ObjectIdentityId is not null)
        {
            var identity = Objects.First(o =>
                o.ProjectId == projectId && o.ObjectIdentityId == reservation.ObjectIdentityId.Value);
            if (identity.LifecycleStatus != ObjectLifecycleStatus.Draft)
            {
                throw new CoreDomainConflictException("绑定身份不是 draft 状态，无法放弃。");
            }

            Objects.Remove(identity);
            Objects.Add(identity with { LifecycleStatus = ObjectLifecycleStatus.Abandoned, UpdatedAtUtcMs = nowUtcMs });
        }

        var abandoned = reservation with
        {
            State = ReservationState.Abandoned,
            AbandonReason = abandonReason,
            UpdatedAtUtcMs = nowUtcMs,
        };
        Reservations.Remove(reservation);
        Reservations.Add(abandoned);
        return abandoned;
    }

    // ---- Object / Definition ----

    public ObjectRecord? FindObject(Guid projectId, Guid objectIdentityId) =>
        Objects.FirstOrDefault(o => o.ProjectId == projectId && o.ObjectIdentityId == objectIdentityId);

    public ObjectDefinitionRecord? ReadObjectDefinition(Guid projectId, Guid objectIdentityId) =>
        Definitions.FirstOrDefault(d => d.ProjectId == projectId && d.ObjectIdentityId == objectIdentityId);

    public void InsertObject(ObjectInsertPlan plan)
    {
        var reservation = Reservations.FirstOrDefault(r =>
                r.ProjectId == plan.ProjectId && r.ReservationId == plan.ReservationId)
            ?? throw new CoreDomainConflictException("Reservation 不存在。");
        if (reservation.State != ReservationState.Bound || reservation.ObjectIdentityId is null)
        {
            throw new CoreDomainConflictException("仅 Bound 状态可正式创建。");
        }

        if (reservation.ObjectIdentityId.Value != plan.ObjectIdentityId)
        {
            throw new CoreDomainDataException("Insert 计划身份与 reservation 溯源不一致。");
        }

        var identity = Objects.FirstOrDefault(o =>
                o.ProjectId == plan.ProjectId && o.ObjectIdentityId == plan.ObjectIdentityId)
            ?? throw new CoreDomainDataException("绑定身份不存在。");
        if (identity.LifecycleStatus != ObjectLifecycleStatus.Draft)
        {
            throw new CoreDomainConflictException("仅 draft 身份可正式创建。");
        }

        ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.Definition.DefinitionSchemaVersion);

        if (plan.Registration is not null)
        {
            RegisterInMemory(plan.ProjectId, plan.Registration.MapId, plan.ObjectIdentityId, plan.NowUtcMs, refId: plan.Registration.MapObjectRefId);
        }

        var (canonical, hash) = CanonicalJson.CanonicalizeAndHash(plan.Definition.DefinitionPayloadJson);
        Definitions.Add(new ObjectDefinitionRecord
        {
            ProjectId = plan.ProjectId,
            ObjectIdentityId = plan.ObjectIdentityId,
            DefinitionSchemaVersion = plan.Definition.DefinitionSchemaVersion,
            DefinitionPayloadJson = canonical,
            DefinitionHash = hash,
            UpdatedAtUtcMs = plan.NowUtcMs,
        });

        if (plan.InitialMembership is not null)
        {
            if (plan.Registration is null
                || !MapObjectRefs.Any(r =>
                    r.ProjectId == plan.ProjectId && r.MapId == plan.Registration.MapId
                    && r.ObjectIdentityId == plan.ObjectIdentityId))
            {
                throw new CoreDomainConflictException("Initial State 成员缺少同 Map 注册行（P3⇒P2）。");
            }

            AppendInitialMember(plan.ProjectId, plan.Registration.MapId, plan.ObjectIdentityId, new InitialObjectWrite
            {
                ObjectIdentityId = plan.ObjectIdentityId,
                MembershipPayloadJson = plan.InitialMembership.MembershipPayloadJson,
                PlacementPayloadJson = plan.InitialMembership.PlacementPayloadJson,
                PlacementSchemaVersion = plan.InitialMembership.PlacementSchemaVersion,
            }, plan.NowUtcMs);
        }

        Objects.Remove(identity);
        Objects.Add(identity with { LifecycleStatus = ObjectLifecycleStatus.Active, UpdatedAtUtcMs = plan.NowUtcMs });
        Reservations.Remove(reservation);
        Reservations.Add(reservation with { State = ReservationState.Inserted, UpdatedAtUtcMs = plan.NowUtcMs });
    }

    public void EditObjectDefinition(ObjectDefinitionEditPlan plan)
    {
        var identity = Objects.FirstOrDefault(o =>
                o.ProjectId == plan.ProjectId && o.ObjectIdentityId == plan.ObjectIdentityId)
            ?? throw new CoreDomainConflictException("Object 不存在。");
        if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
        {
            throw new CoreDomainConflictException("仅 active Object 可编辑定义。");
        }

        ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.DefinitionSchemaVersion);

        var current = Definitions.FirstOrDefault(d =>
                d.ProjectId == plan.ProjectId && d.ObjectIdentityId == plan.ObjectIdentityId)
            ?? throw new CoreDomainDataException("active Object 缺少定义行。");
        if (!string.Equals(current.DefinitionHash, plan.ExpectedDefinitionHash, StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException("定义 CAS 冲突。");
        }

        var (canonical, hash) = CanonicalJson.CanonicalizeAndHash(plan.DefinitionPayloadJson);
        Definitions.Remove(current);
        Definitions.Add(current with
        {
            DefinitionSchemaVersion = plan.DefinitionSchemaVersion,
            DefinitionPayloadJson = canonical,
            DefinitionHash = hash,
            UpdatedAtUtcMs = plan.NowUtcMs,
        });
    }

    // ---- Map 注册 ----

    public MapObjectRefRecord? FindMapObjectRef(Guid projectId, Guid mapId, Guid objectIdentityId) =>
        MapObjectRefs.FirstOrDefault(r =>
            r.ProjectId == projectId && r.MapId == mapId && r.ObjectIdentityId == objectIdentityId);

    public IReadOnlyList<MapObjectRefRecord> ListMapObjectRefs(Guid projectId, Guid mapId) =>
        MapObjectRefs.Where(r => r.ProjectId == projectId && r.MapId == mapId)
            .OrderBy(r => r.ObjectIdentityId)
            .ToArray();

    public void RegisterMapObject(MapObjectRegistrationPlan plan)
    {
        var identity = Objects.FirstOrDefault(o =>
                o.ProjectId == plan.ProjectId && o.ObjectIdentityId == plan.ObjectIdentityId)
            ?? throw new CoreDomainConflictException("Object 不存在。");
        RegisterInMemory(plan.ProjectId, plan.MapId, plan.ObjectIdentityId, plan.NowUtcMs, refId: plan.MapObjectRefId);
    }

    public void UnregisterMapObject(Guid projectId, Guid mapId, Guid objectIdentityId)
    {
        var reference = FindMapObjectRef(projectId, mapId, objectIdentityId)
            ?? throw new CoreDomainConflictException("注册行不存在。");
        if (InitialObjects.Any(m =>
                m.ProjectId == projectId && m.MapId == mapId && m.ObjectIdentityId == objectIdentityId))
        {
            throw new CoreDomainConflictException("仍有 Initial State 成员行，须先移除成员（P3⇒P2）。");
        }

        MapObjectRefs.Remove(reference);
    }

    // ---- Initial State ----

    public MapInitialStateRecord? FindMapInitialState(Guid projectId, Guid mapId) =>
        InitialStates.FirstOrDefault(s => s.ProjectId == projectId && s.MapId == mapId);

    public IReadOnlyList<InitialObjectRecord> ListInitialObjects(Guid projectId, Guid mapId) =>
        InitialObjects.Where(m => m.ProjectId == projectId && m.MapId == mapId)
            .OrderBy(m => m.ObjectIdentityId)
            .ToArray();

    public void EditMapInitialState(EditInitialStatePlan plan)
    {
        var baseline = FindMapInitialState(plan.ProjectId, plan.MapId);
        if (baseline is not null)
        {
            if (plan.ExpectedFingerprint is null
                || !string.Equals(baseline.ContentFingerprint, plan.ExpectedFingerprint, StringComparison.Ordinal))
            {
                throw new CoreDomainConflictException("Initial State CAS 冲突。");
            }

            if (baseline.InitialStateId != plan.InitialStateId)
            {
                throw new CoreDomainDataException("编辑计划基线与既有基线不一致。");
            }
        }
        else if (plan.ExpectedFingerprint is not null)
        {
            throw new CoreDomainConflictException("尚无基线；expected fingerprint 必须为 null。");
        }
        else
        {
            InitialStates.Add(new MapInitialStateRecord
            {
                InitialStateId = plan.InitialStateId,
                ProjectId = plan.ProjectId,
                MapId = plan.MapId,
                BaselineSchemaVersion = plan.BaselineSchemaVersion,
                ContentFingerprint = "-",
                CreatedAtUtcMs = plan.NowUtcMs,
                UpdatedAtUtcMs = plan.NowUtcMs,
            });
        }

        var seen = new HashSet<Guid>();
        foreach (var member in plan.Members)
        {
            if (!seen.Add(member.ObjectIdentityId))
            {
                throw new CoreDomainConflictException("成员集合含重复身份。");
            }

            var identity = Objects.FirstOrDefault(o =>
                    o.ProjectId == plan.ProjectId && o.ObjectIdentityId == member.ObjectIdentityId)
                ?? throw new CoreDomainConflictException("成员身份不存在。");
            if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
            {
                throw new CoreDomainConflictException("draft/abandoned 身份不得进入 Initial State。");
            }

            if (FindMapObjectRef(plan.ProjectId, plan.MapId, member.ObjectIdentityId) is null)
            {
                throw new CoreDomainConflictException("成员缺少注册行（P3⇒P2）。");
            }
        }

        InitialObjects.RemoveAll(m => m.ProjectId == plan.ProjectId && m.MapId == plan.MapId);
        foreach (var member in plan.Members)
        {
            InitialObjects.Add(new InitialObjectRecord
            {
                ProjectId = plan.ProjectId,
                MapId = plan.MapId,
                InitialStateId = plan.InitialStateId,
                ObjectIdentityId = member.ObjectIdentityId,
                MembershipPayloadJson = CanonicalJson.Canonicalize(member.MembershipPayloadJson),
                PlacementPayloadJson = member.PlacementPayloadJson is null
                    ? null
                    : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
                PlacementSchemaVersion = member.PlacementSchemaVersion,
                CreatedAtUtcMs = plan.NowUtcMs,
            });
        }

        var members = ListInitialObjects(plan.ProjectId, plan.MapId);
        var fingerprint = InitialStateFingerprint.Compute(plan.BaselineSchemaVersion, members);
        var updated = InitialStates.First(s => s.ProjectId == plan.ProjectId && s.MapId == plan.MapId);
        InitialStates.Remove(updated);
        InitialStates.Add(updated with
        {
            BaselineSchemaVersion = plan.BaselineSchemaVersion,
            ContentFingerprint = fingerprint,
            UpdatedAtUtcMs = plan.NowUtcMs,
        });
    }

    // ---- Asset / definition link ----

    public AssetRecord? FindAsset(Guid projectId, Guid assetId) =>
        Assets.FirstOrDefault(a => a.ProjectId == projectId && a.AssetId == assetId);

    public AssetRecord? FindAssetByContentHash(Guid projectId, string hashAlgorithm, string contentHash) =>
        Assets.FirstOrDefault(a =>
            a.ProjectId == projectId && a.HashAlgorithm == hashAlgorithm && a.ContentHash == contentHash);

    public IReadOnlyList<AssetRecord> ListAssets(Guid projectId) =>
        Assets.Where(a => a.ProjectId == projectId).OrderBy(a => a.AssetId).ToArray();

    public AssetRecord RegisterAsset(RegisterAssetPlan plan)
    {
        var hash = AssetRules.NormalizeContentHash(plan.ContentHash);
        var record = new AssetRecord
        {
            AssetId = plan.AssetId,
            ProjectId = plan.ProjectId,
            ContentHash = hash,
            HashAlgorithm = "sha-256",
            ByteLength = plan.ByteLength,
            MediaType = plan.MediaType,
            FormatVersion = plan.FormatVersion,
            RelativePath = AssetRules.ContentAddressedRelativePath(hash),
            PublicationState = AssetPublicationState.Published,
            IntegrityState = AssetIntegrityState.Unverified,
            CreatedAtUtcMs = plan.CreatedAtUtcMs,
        };
        Assets.Add(record);
        return record;
    }

    public void UpdateAssetIntegrityState(Guid projectId, Guid assetId, AssetIntegrityState integrityState, long nowUtcMs)
    {
        var asset = FindAsset(projectId, assetId)
            ?? throw new CoreDomainConflictException("Asset 不存在。");
        Assets.Remove(asset);
        Assets.Add(asset with { IntegrityState = integrityState });
    }

    public IReadOnlyList<Guid> FindOrphanAssetIds(Guid projectId)
    {
        var linked = DefinitionAssetLinks.Where(l => l.ProjectId == projectId).Select(l => l.AssetId)
            .Concat(FieldRevisions.Where(r => r.ProjectId == projectId).Select(r => r.FieldRevisionAssetId))
            .ToHashSet();
        return Assets.Where(a => a.ProjectId == projectId && !linked.Contains(a.AssetId))
            .Select(a => a.AssetId).OrderBy(g => g).ToArray();
    }

    public FieldDatasetRecord? FindFieldDataset(Guid projectId, Guid mapId, string fieldKey) =>
        FieldDatasets.FirstOrDefault(x => x.ProjectId == projectId && x.MapId == mapId && x.FieldKey == fieldKey);

    public FieldRevisionRecord? FindFieldRevision(Guid projectId, Guid fieldDatasetId, Guid fieldRevisionId) =>
        FieldRevisions.FirstOrDefault(x => x.ProjectId == projectId
            && x.FieldDatasetId == fieldDatasetId && x.FieldRevisionId == fieldRevisionId);

    public IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId) =>
        FieldRevisions.Where(x => x.ProjectId == projectId && x.FieldDatasetId == fieldDatasetId)
            .OrderBy(x => x.RevisionNumber).ToArray();

    public FieldRevisionRecord PublishFieldRevision(PublishFieldRevisionPlan plan)
    {
        var dataset = FindFieldDataset(plan.ProjectId, plan.MapId, plan.FieldKey);
        if (dataset is null)
        {
            if (plan.ExpectedCurrentFieldRevisionId is not null)
                throw new CoreDomainConflictException("首次 Field Dataset publication 的 expected current revision 必须为 null。");
            dataset = new FieldDatasetRecord
            {
                FieldDatasetId = plan.FieldDatasetId,
                ProjectId = plan.ProjectId,
                MapId = plan.MapId,
                FieldKey = plan.FieldKey,
                DescriptorSchemaVersion = plan.DescriptorSchemaVersion,
                SemanticsId = plan.SemanticsId,
                CurrentFieldRevisionId = null,
                CreatedAtUtcMs = plan.CreatedAtUtcMs,
                UpdatedAtUtcMs = plan.CreatedAtUtcMs,
            };
            FieldDatasets.Add(dataset);
        }
        else
        {
            if (dataset.FieldDatasetId != plan.FieldDatasetId
                || dataset.CurrentFieldRevisionId != plan.ExpectedCurrentFieldRevisionId)
                throw new CoreDomainConflictException("Field Dataset identity 或 current revision CAS 冲突。");
        }

        var revision = new FieldRevisionRecord
        {
            FieldRevisionId = plan.FieldRevisionId,
            ProjectId = plan.ProjectId,
            FieldDatasetId = plan.FieldDatasetId,
            RevisionNumber = FieldRevisions.Where(x => x.ProjectId == plan.ProjectId && x.FieldDatasetId == plan.FieldDatasetId)
                .Select(x => x.RevisionNumber).DefaultIfEmpty(0).Max() + 1,
            ParentRevisionId = dataset.CurrentFieldRevisionId,
            FieldRevisionAssetId = plan.FieldRevisionAssetId,
            ContentFingerprint = plan.ContentFingerprint,
            ManifestJson = plan.ManifestJson,
            Metadata = plan.Metadata,
            ProvenanceJson = plan.ProvenanceJson,
            CreatedAtUtcMs = plan.CreatedAtUtcMs,
        };
        FieldRevisions.Add(revision);
        FieldDatasets.Remove(dataset);
        FieldDatasets.Add(dataset with
        {
            CurrentFieldRevisionId = revision.FieldRevisionId,
            UpdatedAtUtcMs = plan.CreatedAtUtcMs,
        });
        return revision;
    }

    public ObjectDefinitionAssetLinkRecord? FindDefinitionAssetLink(Guid projectId, Guid objectIdentityId, Guid assetId, string role) =>
        DefinitionAssetLinks.FirstOrDefault(l =>
            l.ProjectId == projectId && l.ObjectIdentityId == objectIdentityId
            && l.AssetId == assetId && l.Role == role);

    public IReadOnlyList<ObjectDefinitionAssetLinkRecord> ListDefinitionAssetLinks(Guid projectId, Guid objectIdentityId) =>
        DefinitionAssetLinks.Where(l => l.ProjectId == projectId && l.ObjectIdentityId == objectIdentityId)
            .OrderBy(l => l.AssetId).ThenBy(l => l.Role, StringComparer.Ordinal).ToArray();

    public void LinkDefinitionAsset(LinkDefinitionAssetPlan plan)
    {
        DefinitionAssetLinks.Add(new ObjectDefinitionAssetLinkRecord
        {
            ObjectDefinitionAssetId = plan.ObjectDefinitionAssetId,
            ProjectId = plan.ProjectId,
            ObjectIdentityId = plan.ObjectIdentityId,
            AssetId = plan.AssetId,
            Role = plan.Role,
            CreatedAtUtcMs = plan.CreatedAtUtcMs,
        });
    }

    public void UnlinkDefinitionAsset(Guid projectId, Guid objectIdentityId, Guid assetId, string role)
    {
        var link = FindDefinitionAssetLink(projectId, objectIdentityId, assetId, role)
            ?? throw new CoreDomainConflictException("definition Asset link 不存在。");
        DefinitionAssetLinks.Remove(link);
    }

    // ---- 内存写辅助（与适配器规则一致） ----

    private void RegisterInMemory(Guid projectId, Guid mapId, Guid objectIdentityId, long nowUtcMs, Guid refId)
    {
        var identity = Objects.FirstOrDefault(o =>
                o.ProjectId == projectId && o.ObjectIdentityId == objectIdentityId)
            ?? throw new CoreDomainConflictException("Object 不存在。");
        if (FindMap(projectId, mapId) is null)
        {
            throw new CoreDomainConflictException("Map 不存在。");
        }

        if (identity.SharingScope == ObjectSharingScope.MapLocal && identity.OwnerMapId != mapId)
        {
            throw new CoreDomainConflictException("map_local Object 只能注册到 owner Map。");
        }

        if (FindMapObjectRef(projectId, mapId, objectIdentityId) is not null)
        {
            throw new CoreDomainConflictException("已注册，拒绝重复注册。");
        }

        MapObjectRefs.Add(new MapObjectRefRecord
        {
            MapObjectRefId = refId,
            ProjectId = projectId,
            MapId = mapId,
            ObjectIdentityId = objectIdentityId,
            CreatedAtUtcMs = nowUtcMs,
        });
    }

    private void AppendInitialMember(Guid projectId, Guid mapId, Guid objectIdentityId, InitialObjectWrite member, long nowUtcMs)
    {
        var baseline = FindMapInitialState(projectId, mapId);
        if (baseline is null)
        {
            baseline = new MapInitialStateRecord
            {
                InitialStateId = Guid.NewGuid(),
                ProjectId = projectId,
                MapId = mapId,
                BaselineSchemaVersion = MapInitialStateService.StructuralBaselineSchemaVersion,
                ContentFingerprint = "-",
                CreatedAtUtcMs = nowUtcMs,
                UpdatedAtUtcMs = nowUtcMs,
            };
            InitialStates.Add(baseline);
        }

        InitialObjects.Add(new InitialObjectRecord
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = baseline.InitialStateId,
            ObjectIdentityId = objectIdentityId,
            MembershipPayloadJson = CanonicalJson.Canonicalize(member.MembershipPayloadJson),
            PlacementPayloadJson = member.PlacementPayloadJson is null
                ? null
                : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
            PlacementSchemaVersion = member.PlacementSchemaVersion,
            CreatedAtUtcMs = nowUtcMs,
        });

        var members = ListInitialObjects(projectId, mapId);
        var fingerprint = InitialStateFingerprint.Compute(baseline.BaselineSchemaVersion, members);
        InitialStates.Remove(baseline);
        InitialStates.Add(baseline with { ContentFingerprint = fingerprint, UpdatedAtUtcMs = nowUtcMs });
    }
}
