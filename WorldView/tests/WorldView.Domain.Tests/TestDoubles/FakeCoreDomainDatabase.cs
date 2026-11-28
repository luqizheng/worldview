using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WorldView.Domain.Aggregates;
using WorldView.Domain.Assets;
using WorldView.Domain.Registry;
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

    public List<FieldPayloadRecord> FieldPayloads { get; } = new();

    public Dictionary<Guid, byte[]> FieldPayloadBlobs { get; } = new();

    public List<MapInitialStateBaselineRecord> InitialStateBaselines { get; } = new();

    public List<MapInitialStateRevisionRecord> InitialStateRevisions { get; } = new();

    public List<InitialObjectRecord> InitialStateRevisionMembers { get; } = new();

    public List<MapInitialStateCursorRecord> InitialStateCursors { get; } = new();

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
            var current = FindSpatialReferenceDefinition(projectId, map.SpatialReferenceId)
                ?? throw new CoreDomainDataException("Map 当前绑定的 Spatial Reference 定义缺失。");
            _ = SpatialReferenceRules.ValidateDefaultLocalCartesian(current, mapId);
            if (MapObjectRefs.Any(x => x.ProjectId == projectId && x.MapId == mapId)
                || InitialStateRevisionMembers.Any(x => x.ProjectId == projectId && x.MapId == mapId)
                || FieldDatasets.Any(x => x.ProjectId == projectId && x.MapId == mapId))
                throw new CoreDomainConflictException("Map 已有内容；更换 Spatial Reference 必须经过坐标转换流程。");
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
        // 模拟真实实现的名称唯一性约束（WP-20a：同 Project 内唯一，MapRules 语义）。
        if (Maps.Any(m => m.ProjectId == plan.Map.ProjectId
            && MapRules.MapNameComparer.Equals(m.MapName, plan.Map.MapName)))
        {
            throw new CoreDomainConflictException(
                $"同一项目内已存在同名地图「{plan.Map.MapName}」（名称唯一，不区分大小写）。");
        }

        var spatialReference = SpatialReferenceRules.ValidateDefaultLocalCartesian(plan.DefaultSpatialReference, plan.Map.MapId);
        if (spatialReference.ProjectId != plan.Map.ProjectId
            || spatialReference.SpatialReferenceId != plan.Map.SpatialReferenceId)
            throw new CoreDomainConflictException("Map 默认 Spatial Reference 与 Map 绑定不一致。");
        InsertSpatialReferenceDefinition(spatialReference);
        Maps.Add(plan.Map);
        Histories.Add(plan.RootHistory);
        Calendars.Add(plan.DefaultCalendar);
        CalendarConfigurations.Add(plan.DayZeroConfiguration);

        // FOUNDATION-01B §5：baseline identity + revision zero + cursor 同事务建立。
        var baseline = plan.InitialStateBaseline;
        if (baseline.BaselineSchemaVersion != MapInitialStateService.StructuralBaselineSchemaVersion
            || !string.Equals(
                baseline.RevisionZeroFingerprint,
                InitialStateFingerprint.Compute(baseline.BaselineSchemaVersion, []),
                StringComparison.Ordinal))
        {
            throw new CoreDomainDataException("revision zero 计划指纹与 Domain 空快照重算值不一致。");
        }

        InitialStateBaselines.Add(new MapInitialStateBaselineRecord
        {
            InitialStateId = baseline.InitialStateId,
            ProjectId = plan.Map.ProjectId,
            MapId = plan.Map.MapId,
            CreatedAtUtcMs = plan.Map.CreatedAtUtcMs,
        });
        InitialStateRevisions.Add(new MapInitialStateRevisionRecord
        {
            RevisionId = baseline.RevisionZeroId,
            ProjectId = plan.Map.ProjectId,
            MapId = plan.Map.MapId,
            InitialStateId = baseline.InitialStateId,
            RevisionNumber = 0,
            ParentRevisionId = null,
            BaselineSchemaVersion = baseline.BaselineSchemaVersion,
            ContentFingerprint = baseline.RevisionZeroFingerprint,
            OperationKind = null,
            OperationPayloadJson = null,
            CreatedAtUtcMs = plan.Map.CreatedAtUtcMs,
        });
        InitialStateCursors.Add(new MapInitialStateCursorRecord
        {
            ProjectId = plan.Map.ProjectId,
            MapId = plan.Map.MapId,
            InitialStateId = baseline.InitialStateId,
            ActiveRevisionId = baseline.RevisionZeroId,
            SelectedRedoChildRevisionId = null,
            CursorVersion = 0,
            UpdatedAtUtcMs = plan.Map.CreatedAtUtcMs,
        });
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
                || plan.InitialRevisionAppend is null
                || !MapObjectRefs.Any(r =>
                    r.ProjectId == plan.ProjectId && r.MapId == plan.Registration.MapId
                    && r.ObjectIdentityId == plan.ObjectIdentityId))
            {
                throw new CoreDomainConflictException("Initial State 成员缺少同 Map 注册行或 revision 追加上下文（P3⇒P2）。");
            }

            AppendRevisionForInsertObject(
                plan, plan.Registration.MapId, new InitialObjectWrite
                {
                    ObjectIdentityId = plan.ObjectIdentityId,
                    MembershipPayloadJson = plan.InitialMembership.MembershipPayloadJson,
                    PlacementPayloadJson = plan.InitialMembership.PlacementPayloadJson,
                    PlacementSchemaVersion = plan.InitialMembership.PlacementSchemaVersion,
                });
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

    public void PublishObjectDefinitionWithGeometryAssets(ObjectDefinitionPublicationPlan plan)
    {
        var identity = Objects.FirstOrDefault(o =>
                o.ProjectId == plan.ProjectId && o.ObjectIdentityId == plan.ObjectIdentityId)
            ?? throw new CoreDomainConflictException("Object 不存在。");
        if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
        {
            throw new CoreDomainConflictException("仅 active Object 可发布 Geometry Definition。");
        }

        ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.DefinitionSchemaVersion);
        var typeKey = ObjectTypes.FirstOrDefault(t =>
                t.ProjectId == plan.ProjectId && t.ObjectTypeId == identity.ObjectTypeId)?.TypeKey
            ?? throw new CoreDomainDataException("Object Type 不存在。");

        var current = Definitions.FirstOrDefault(d =>
                d.ProjectId == plan.ProjectId && d.ObjectIdentityId == plan.ObjectIdentityId)
            ?? throw new CoreDomainDataException("active Object 缺少定义行。");
        if (!string.Equals(current.DefinitionHash, plan.ExpectedDefinitionHash, StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException("定义 CAS 冲突。");
        }

        var (canonical, hash) = CanonicalJson.CanonicalizeAndHash(plan.DefinitionPayloadJson);
        ObjectTypeRegistry.ValidateDefinition(typeKey, canonical);

        var desired = ReadGeometryAssets(canonical);
        foreach (var assetId in desired.Keys)
        {
            var asset = FindAsset(plan.ProjectId, assetId)
                ?? throw new CoreDomainConflictException($"Asset {assetId} 不存在。");
            if (asset.IntegrityState != AssetIntegrityState.Verified)
            {
                throw new CoreDomainConflictException($"Asset {assetId} 未验证，拒绝发布。");
            }

            if (!string.Equals(asset.ContentHash, desired[assetId], StringComparison.Ordinal))
            {
                throw new CoreDomainConflictException($"Asset {assetId} hash 不一致。");
            }
        }

        var links = desired.Keys
            .Select(assetId => (AssetId: assetId.ToString("D"), Role: "geometry_component"))
            .ToArray();
        var issues = BuildingCaveAggregateValidator.Validate(
            typeKey,
            canonical,
            new AggregatePublicationContext
            {
                AssetLinks = links,
                AssetCatalog = desired.Select(x => (x.Key.ToString("D"), x.Value)).ToArray(),
            });
        if (issues.Count > 0)
        {
            throw new CoreDomainConflictException(string.Join("; ", issues.Select(i => i.ErrorCode)));
        }

        DefinitionAssetLinks.RemoveAll(l =>
            l.ProjectId == plan.ProjectId
            && l.ObjectIdentityId == plan.ObjectIdentityId
            && l.Role == "geometry_component");
        foreach (var assetId in desired.Keys.OrderBy(id => id))
        {
            DefinitionAssetLinks.Add(new ObjectDefinitionAssetLinkRecord
            {
                ObjectDefinitionAssetId = Guid.NewGuid(),
                ProjectId = plan.ProjectId,
                ObjectIdentityId = plan.ObjectIdentityId,
                AssetId = assetId,
                Role = "geometry_component",
                CreatedAtUtcMs = plan.NowUtcMs,
            });
        }

        Definitions.Remove(current);
        Definitions.Add(current with
        {
            DefinitionSchemaVersion = plan.DefinitionSchemaVersion,
            DefinitionPayloadJson = canonical,
            DefinitionHash = hash,
            UpdatedAtUtcMs = plan.NowUtcMs,
        });
    }

    private static Dictionary<Guid, string> ReadGeometryAssets(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var assets = new Dictionary<Guid, string>();
        if (!document.RootElement.TryGetProperty("geometry_components", out var components)
            || components.ValueKind != JsonValueKind.Array)
        {
            return assets;
        }

        foreach (var component in components.EnumerateArray())
        {
            if (component.TryGetProperty("asset_id", out var assetElement)
                && Guid.TryParseExact(assetElement.GetString(), "D", out var assetId)
                && component.TryGetProperty("content_hash", out var hashElement)
                && hashElement.GetString() is { } contentHash)
            {
                assets[assetId] = contentHash;
            }
        }

        return assets;
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
        if (InitialStateRevisionMembers.Any(m =>
                m.ProjectId == projectId && m.MapId == mapId && m.ObjectIdentityId == objectIdentityId))
        {
            throw new CoreDomainConflictException("仍是某个 Initial State revision 的成员，取消注册被拒绝（P3⇒P2）。");
        }

        MapObjectRefs.Remove(reference);
    }

    // ---- Initial State 修订（FOUNDATION-01B §3/§6） ----

    public MapInitialStateBaselineRecord? FindMapInitialStateBaseline(Guid projectId, Guid mapId) =>
        InitialStateBaselines.FirstOrDefault(s => s.ProjectId == projectId && s.MapId == mapId);

    public MapInitialStateRevisionSnapshot? FindMapInitialStateRevisionSnapshot(
        Guid projectId, Guid mapId, Guid revisionId)
    {
        var revision = InitialStateRevisions.FirstOrDefault(x =>
            x.ProjectId == projectId && x.MapId == mapId && x.RevisionId == revisionId);
        if (revision is null)
        {
            return null;
        }

        return new MapInitialStateRevisionSnapshot
        {
            Revision = revision,
            Members = ListRevisionMembers(projectId, mapId, revisionId),
        };
    }

    public IReadOnlyList<MapInitialStateRevisionRecord> ListMapInitialStateRevisions(Guid projectId, Guid mapId) =>
        InitialStateRevisions.Where(x => x.ProjectId == projectId && x.MapId == mapId)
            .OrderBy(x => x.RevisionNumber)
            .ToArray();

    public MapInitialStateRevisionState? FindMapInitialStateRevisionState(Guid projectId, Guid mapId)
    {
        var baseline = FindMapInitialStateBaseline(projectId, mapId);
        if (baseline is null)
        {
            return null;
        }

        var cursor = InitialStateCursors.FirstOrDefault(x =>
            x.ProjectId == projectId && x.MapId == mapId)
            ?? throw new CoreDomainDataException("存在 baseline 但缺少 cursor 行（数据损坏）。");

        var activeRevision = InitialStateRevisions.FirstOrDefault(x =>
            x.ProjectId == projectId
            && x.MapId == mapId
            && x.InitialStateId == cursor.InitialStateId
            && x.RevisionId == cursor.ActiveRevisionId)
            ?? throw new CoreDomainDataException("cursor 指向不存在的 active revision（数据损坏）。");

        return new MapInitialStateRevisionState
        {
            Snapshot = new MapInitialStateRevisionSnapshot
            {
                Revision = activeRevision,
                Members = ListRevisionMembers(projectId, mapId, cursor.ActiveRevisionId),
            },
            Cursor = cursor,
            RedoChildren = InitialStateRevisions
                .Where(x => x.ProjectId == projectId
                    && x.MapId == mapId
                    && x.InitialStateId == cursor.InitialStateId
                    && x.ParentRevisionId == cursor.ActiveRevisionId)
                .OrderBy(x => x.RevisionNumber)
                .ToArray(),
        };
    }

    public void AppendMapInitialStateRevision(AppendMapInitialStateRevisionPlan plan)
    {
        var cursor = RequireCursor(plan.ProjectId, plan.MapId);
        ValidateExpectedCursor(cursor, plan.ExpectedActiveRevisionId, plan.ExpectedCursorVersion);
        if (cursor.InitialStateId != plan.InitialStateId)
        {
            throw new CoreDomainDataException("追加计划 baseline 与既有 baseline 不一致（fail-closed）。");
        }

        var parent = InitialStateRevisions.FirstOrDefault(x =>
            x.ProjectId == plan.ProjectId
            && x.MapId == plan.MapId
            && x.InitialStateId == cursor.InitialStateId
            && x.RevisionId == cursor.ActiveRevisionId)
            ?? throw new CoreDomainDataException("cursor 指向不存在的 active revision（数据损坏）。");

        var parentMembers = ListRevisionMembers(plan.ProjectId, plan.MapId, parent.RevisionId);
        var childMembers = CanonicalizeMembers(plan.ProjectId, plan.MapId, plan.Members);
        var childFingerprint = InitialStateFingerprint.Compute(parent.BaselineSchemaVersion, childMembers);
        if (!string.Equals(childFingerprint, plan.ContentFingerprint, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException("fingerprint 与 Domain 计划不一致（fail-closed）。");
        }

        var operationPayload = InitialStateRevisionRules.BuildOperationPayload(
            parent.ContentFingerprint, childFingerprint, parentMembers, childMembers);
        if (!string.Equals(operationPayload, plan.OperationPayloadJson, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException("operation payload 与 Domain 计划不一致（fail-closed）。");
        }

        InsertRevisionInMemory(plan.ProjectId, plan.MapId, cursor.InitialStateId, parent.RevisionId,
            plan.RevisionId, parent.BaselineSchemaVersion, childFingerprint, operationPayload, childMembers, plan.NowUtcMs);
        SetCursor(cursor, plan.RevisionId, null, plan.ExpectedCursorVersion + 1, plan.NowUtcMs);
    }

    public void MoveMapInitialStateCursor(MoveMapInitialStateCursorPlan plan)
    {
        var cursor = RequireCursor(plan.ProjectId, plan.MapId);
        ValidateExpectedCursor(cursor, plan.ExpectedActiveRevisionId, plan.ExpectedCursorVersion);
        if (cursor.InitialStateId != plan.InitialStateId)
        {
            throw new CoreDomainDataException("cursor 计划 baseline 与既有 baseline 不一致（fail-closed）。");
        }

        var current = InitialStateRevisions.FirstOrDefault(x =>
            x.ProjectId == plan.ProjectId
            && x.MapId == plan.MapId
            && x.InitialStateId == cursor.InitialStateId
            && x.RevisionId == cursor.ActiveRevisionId)
            ?? throw new CoreDomainDataException("cursor 指向不存在的 active revision（数据损坏）。");

        if (plan.IsRedo)
        {
            var target = InitialStateRevisions.FirstOrDefault(x =>
                x.ProjectId == plan.ProjectId
                && x.MapId == plan.MapId
                && x.InitialStateId == cursor.InitialStateId
                && x.RevisionId == plan.TargetRevisionId);
            InitialStateRevisionRules.ValidateDirectChild(current.RevisionId, plan.TargetRevisionId, target?.ParentRevisionId);
            SetCursor(cursor, plan.TargetRevisionId, null, plan.ExpectedCursorVersion + 1, plan.NowUtcMs);
        }
        else
        {
            if (current.ParentRevisionId is null)
            {
                throw new CoreDomainConflictException("Revision zero（baseline root）不可 Undo。");
            }

            SetCursor(cursor, current.ParentRevisionId.Value, current.RevisionId, plan.ExpectedCursorVersion + 1, plan.NowUtcMs);
        }
    }

    public void SelectMapInitialStateRedoChild(SelectMapInitialStateRedoChildPlan plan)
    {
        var cursor = RequireCursor(plan.ProjectId, plan.MapId);
        ValidateExpectedCursor(cursor, plan.ExpectedActiveRevisionId, plan.ExpectedCursorVersion);
        if (cursor.InitialStateId != plan.InitialStateId)
        {
            throw new CoreDomainDataException("redo 选择计划 baseline 与既有 baseline 不一致（fail-closed）。");
        }

        var child = InitialStateRevisions.FirstOrDefault(x =>
            x.ProjectId == plan.ProjectId
            && x.MapId == plan.MapId
            && x.InitialStateId == cursor.InitialStateId
            && x.RevisionId == plan.ChildRevisionId);
        InitialStateRevisionRules.ValidateDirectChild(cursor.ActiveRevisionId, plan.ChildRevisionId, child?.ParentRevisionId);
        SetCursor(cursor, cursor.ActiveRevisionId, plan.ChildRevisionId, plan.ExpectedCursorVersion + 1, plan.NowUtcMs);
    }

    // ---- 内存写辅助（与适配器规则一致） ----

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

    public void RecordAssetIntegrityFailure(Guid projectId, Guid assetId, AssetIntegrityState integrityState, long nowUtcMs)
    {
        if (integrityState is not (AssetIntegrityState.Corrupt or AssetIntegrityState.Missing))
        {
            throw new CoreDomainConflictException("完整性失败接口只接受 Corrupt 或 Missing。");
        }

        var asset = FindAsset(projectId, assetId)
            ?? throw new CoreDomainConflictException("Asset 不存在。");
        Assets.Remove(asset);
        Assets.Add(asset with { IntegrityState = integrityState });
    }

    public void MarkAssetVerified(Guid projectId, Guid assetId, AssetByteDigest digest, long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(digest);

        var asset = FindAsset(projectId, assetId)
            ?? throw new CoreDomainConflictException("Asset 不存在。");
        if (!string.Equals(asset.ContentHash, digest.ContentHash, StringComparison.Ordinal)
            || asset.ByteLength != digest.ByteLength)
        {
            throw new CoreDomainConflictException("Asset 实测字节摘要与当前元数据不匹配。");
        }

        Assets.Remove(asset);
        Assets.Add(asset with { IntegrityState = AssetIntegrityState.Verified });
    }

    public IReadOnlyList<Guid> FindOrphanAssetIds(Guid projectId)
    {
        var linked = DefinitionAssetLinks.Where(l => l.ProjectId == projectId)
            .Select(l => l.AssetId).ToHashSet();
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

    public FieldPayloadRecord? FindFieldPayload(Guid projectId, Guid fieldRevisionId) =>
        FieldPayloads.FirstOrDefault(x => x.ProjectId == projectId && x.FieldRevisionId == fieldRevisionId);

    public IReadOnlyList<FieldPayloadRecord> ListFieldPayloads(Guid projectId) =>
        FieldPayloads.Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.FieldPayloadId).ToArray();

    public void SetFieldPayloadIntegrityState(Guid projectId, Guid fieldPayloadId, FieldPayloadIntegrityState state)
    {
        var index = FieldPayloads.FindIndex(x => x.ProjectId == projectId && x.FieldPayloadId == fieldPayloadId);
        if (index < 0)
            throw new CoreDomainConflictException("Field Payload does not exist in the requested Project.");
        FieldPayloads[index] = FieldPayloads[index] with { IntegrityState = state };
    }

    public byte[]? ReadFieldPayloadBlob(Guid projectId, Guid fieldRevisionId)
    {
        var payload = FindFieldPayload(projectId, fieldRevisionId);
        if (payload is null)
            return null;
        if (payload.StorageKind != FieldPayloadStorageKind.SqliteBlob)
            throw new InvalidOperationException("Field Payload is stored as ManagedFile, not SQLite BLOB.");
        return FieldPayloadBlobs.TryGetValue(payload.FieldPayloadId, out var bytes)
            ? bytes.ToArray()
            : throw new CoreDomainDataException("SQLite BLOB Field Payload is missing from the fake database.");
    }

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

        var payload = FieldPayloadRules.Create(plan.Payload);
        FieldPayloads.Add(payload);
        if (payload.StorageKind == FieldPayloadStorageKind.SqliteBlob)
            FieldPayloadBlobs.Add(payload.FieldPayloadId, plan.PayloadBytes!.ToArray());

        var revision = new FieldRevisionRecord
        {
            FieldRevisionId = plan.FieldRevisionId,
            ProjectId = plan.ProjectId,
            FieldDatasetId = plan.FieldDatasetId,
            RevisionNumber = FieldRevisions.Where(x => x.ProjectId == plan.ProjectId && x.FieldDatasetId == plan.FieldDatasetId)
                .Select(x => x.RevisionNumber).DefaultIfEmpty(0).Max() + 1,
            ParentRevisionId = dataset.CurrentFieldRevisionId,
            Payload = payload,
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

    /// <summary>InsertObject 事务内的 revision 追加（FOUNDATION-01B §10；与 Sqlite 适配器同规则）。</summary>
    private void AppendRevisionForInsertObject(ObjectInsertPlan plan, Guid mapId, InitialObjectWrite newMember)
    {
        var append = plan.InitialRevisionAppend!;
        var cursor = RequireCursor(plan.ProjectId, mapId);
        ValidateExpectedCursor(cursor, append.ExpectedActiveRevisionId, append.ExpectedCursorVersion);
        if (cursor.InitialStateId != append.InitialStateId)
        {
            throw new CoreDomainDataException("Insert 计划 baseline 与既有 baseline 不一致（fail-closed）。");
        }

        var parent = InitialStateRevisions.FirstOrDefault(x =>
            x.ProjectId == plan.ProjectId
            && x.MapId == mapId
            && x.InitialStateId == cursor.InitialStateId
            && x.RevisionId == cursor.ActiveRevisionId)
            ?? throw new CoreDomainDataException("cursor 指向不存在的 active revision（数据损坏）。");

        var parentMembers = ListRevisionMembers(plan.ProjectId, mapId, parent.RevisionId);
        if (parentMembers.Any(m => m.ObjectIdentityId == plan.ObjectIdentityId))
        {
            throw new CoreDomainConflictException("身份已是活动 revision 成员，拒绝重复追加。");
        }

        // Insert 路径放宽：父快照成员在各自提交时已校验；新成员身份在本事务结束才
        // draft→active（与 Sqlite 适配器一致），此处只做 canonical 化与成对校验。
        if (newMember.PlacementPayloadJson is null != newMember.PlacementSchemaVersion is null)
        {
            throw new CoreDomainConflictException("placement payload 与 schema version 必须成对提供。");
        }

        var childMembers = parentMembers
            .Select(m => new InitialObjectWrite
            {
                ObjectIdentityId = m.ObjectIdentityId,
                MembershipPayloadJson = m.MembershipPayloadJson,
                PlacementPayloadJson = m.PlacementPayloadJson,
                PlacementSchemaVersion = m.PlacementSchemaVersion,
            })
            .Append(new InitialObjectWrite
            {
                ObjectIdentityId = newMember.ObjectIdentityId,
                MembershipPayloadJson = CanonicalJson.Canonicalize(newMember.MembershipPayloadJson),
                PlacementPayloadJson = newMember.PlacementPayloadJson is null
                    ? null
                    : CanonicalJson.Canonicalize(newMember.PlacementPayloadJson),
                PlacementSchemaVersion = newMember.PlacementSchemaVersion,
            })
            .ToArray();
        var childFingerprint = InitialStateFingerprint.Compute(parent.BaselineSchemaVersion, childMembers);
        if (!string.Equals(childFingerprint, append.ContentFingerprint, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException("fingerprint 与 Domain 计划不一致（fail-closed）。");
        }

        var operationPayload = InitialStateRevisionRules.BuildOperationPayload(
            parent.ContentFingerprint, childFingerprint, parentMembers, childMembers);
        if (!string.Equals(operationPayload, append.OperationPayloadJson, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException("operation payload 与 Domain 计划不一致（fail-closed）。");
        }

        InsertRevisionInMemory(plan.ProjectId, mapId, cursor.InitialStateId, parent.RevisionId,
            append.NewRevisionId, parent.BaselineSchemaVersion, childFingerprint, operationPayload, childMembers, plan.NowUtcMs);
        SetCursor(cursor, append.NewRevisionId, null, append.ExpectedCursorVersion + 1, plan.NowUtcMs);
    }

    private MapInitialStateCursorRecord RequireCursor(Guid projectId, Guid mapId)
    {
        if (FindMapInitialStateBaseline(projectId, mapId) is null)
        {
            throw new CoreDomainDataException("缺少 Initial State baseline 身份行（数据损坏）。");
        }

        return InitialStateCursors.FirstOrDefault(x => x.ProjectId == projectId && x.MapId == mapId)
            ?? throw new CoreDomainDataException("存在 baseline 但缺少 cursor 行（数据损坏）。");
    }

    private static void ValidateExpectedCursor(
        MapInitialStateCursorRecord cursor, Guid expectedActiveRevisionId, long expectedCursorVersion)
    {
        if (cursor.ActiveRevisionId != expectedActiveRevisionId || cursor.CursorVersion != expectedCursorVersion)
        {
            throw new CoreDomainConflictException("Initial State cursor 冲突：expected revision/version 不匹配。");
        }
    }

    private void SetCursor(
        MapInitialStateCursorRecord cursor,
        Guid activeRevisionId,
        Guid? selectedRedoChildRevisionId,
        long cursorVersion,
        long nowUtcMs)
    {
        InitialStateCursors.Remove(cursor);
        InitialStateCursors.Add(cursor with
        {
            ActiveRevisionId = activeRevisionId,
            SelectedRedoChildRevisionId = selectedRedoChildRevisionId,
            CursorVersion = cursorVersion,
            UpdatedAtUtcMs = nowUtcMs,
        });
    }

    private void InsertRevisionInMemory(
        Guid projectId,
        Guid mapId,
        Guid initialStateId,
        Guid parentRevisionId,
        Guid newRevisionId,
        long baselineSchemaVersion,
        string contentFingerprint,
        string operationPayloadJson,
        IReadOnlyList<InitialObjectWrite> members,
        long nowUtcMs)
    {
        if (InitialStateRevisions.Any(x =>
            x.ProjectId == projectId && x.MapId == mapId && x.InitialStateId == initialStateId && x.RevisionId == newRevisionId))
        {
            throw new CoreDomainConflictException("revision 身份重复（UNIQUE 语义）。");
        }

        var nextRevisionNumber = InitialStateRevisions
            .Where(x => x.ProjectId == projectId && x.MapId == mapId && x.InitialStateId == initialStateId)
            .Select(x => x.RevisionNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;
        InitialStateRevisions.Add(new MapInitialStateRevisionRecord
        {
            RevisionId = newRevisionId,
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = initialStateId,
            RevisionNumber = nextRevisionNumber,
            ParentRevisionId = parentRevisionId,
            BaselineSchemaVersion = baselineSchemaVersion,
            ContentFingerprint = contentFingerprint,
            OperationKind = InitialStateRevisionRules.SnapshotReplaceOperationKind,
            OperationPayloadJson = operationPayloadJson,
            CreatedAtUtcMs = nowUtcMs,
        });

        foreach (var member in members)
        {
            InitialStateRevisionMembers.Add(new InitialObjectRecord
            {
                ProjectId = projectId,
                MapId = mapId,
                InitialStateId = initialStateId,
                RevisionId = newRevisionId,
                ObjectIdentityId = member.ObjectIdentityId,
                MembershipPayloadJson = member.MembershipPayloadJson,
                PlacementPayloadJson = member.PlacementPayloadJson,
                PlacementSchemaVersion = member.PlacementSchemaVersion,
                CreatedAtUtcMs = nowUtcMs,
            });
        }
    }

    private IReadOnlyList<InitialObjectRecord> ListRevisionMembers(Guid projectId, Guid mapId, Guid revisionId) =>
        InitialStateRevisionMembers
            .Where(m => m.ProjectId == projectId && m.MapId == mapId && m.RevisionId == revisionId)
            .OrderBy(m => m.ObjectIdentityId)
            .ToArray();

    /// <summary>成员前置校验 + canonical 化（与 Sqlite 适配器同规则）。</summary>
    private List<InitialObjectWrite> CanonicalizeMembers(
        Guid projectId, Guid mapId, IReadOnlyList<InitialObjectWrite> members)
    {
        var seen = new HashSet<Guid>();
        var result = new List<InitialObjectWrite>(members.Count);
        foreach (var member in members)
        {
            if (member.PlacementPayloadJson is null != member.PlacementSchemaVersion is null)
            {
                throw new CoreDomainConflictException("placement payload 与 schema version 必须成对提供。");
            }

            if (!seen.Add(member.ObjectIdentityId))
            {
                throw new CoreDomainConflictException("成员集合含重复身份。");
            }

            var identity = Objects.FirstOrDefault(o =>
                    o.ProjectId == projectId && o.ObjectIdentityId == member.ObjectIdentityId)
                ?? throw new CoreDomainConflictException("成员身份不存在。");
            if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
            {
                throw new CoreDomainConflictException("draft/abandoned 身份不得进入 Initial State。");
            }

            if (FindMapObjectRef(projectId, mapId, member.ObjectIdentityId) is null)
            {
                throw new CoreDomainConflictException("成员缺少注册行（P3⇒P2）。");
            }

            result.Add(new InitialObjectWrite
            {
                ObjectIdentityId = member.ObjectIdentityId,
                MembershipPayloadJson = CanonicalJson.Canonicalize(member.MembershipPayloadJson),
                PlacementPayloadJson = member.PlacementPayloadJson is null
                    ? null
                    : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
                PlacementSchemaVersion = member.PlacementSchemaVersion,
            });
        }

        return result;
    }
}
