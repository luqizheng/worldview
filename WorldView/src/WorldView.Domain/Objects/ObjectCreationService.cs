using System;
using System.Text.Json;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Objects;

/// <summary>
/// Object 正式创建服务（Bound→Inserted 原子边界，P2-09-12 TX-55；CD-02-A §13.2）。
/// 定义首行、Map 注册、Initial State 成员 + immutable revision 追加与身份激活/状态转换
/// 同事务提交；失败整体回滚、无半成品；重试复用同一 reservation/identity（绝不分配替代 ID）。
/// 携带 Initial State 成员时 revision/cursor authority 不可绕过（FOUNDATION-01B §10）。
/// </summary>
public static class ObjectCreationService
{
    /// <summary>
    /// 正式创建 Object（Bound→Inserted）。
    /// <paramref name="definition"/>：首行定义（schema 版本须与身份 pin 一致；
    /// payload canonical 化与 hash 计算归 Domain/适配器路径）。
    /// <paramref name="registration"/>：可选 Map 注册（map_local 仅 owner Map）。
    /// <paramref name="initialMembership"/>：可选 time-zero 成员；指定时必须同时注册到
    /// 同一 Map（P3⇒P2），并在同事务内追加 Initial State child revision（expected
    /// active revision/cursor 校验，stale 拒绝；Map 创建事务已建立 baseline/revision
    /// zero/cursor，无 lazy 建立）。
    /// </summary>
    public static void InsertObject(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid reservationId,
        ObjectDefinitionRecord definition,
        MapObjectRegistration? registration,
        InitialObjectMembership? initialMembership,
        long nowUtcMs)
    {
        var reservation = database.FindReservation(projectId, reservationId)
            ?? throw new CoreDomainConflictException($"Reservation {reservationId} 不存在或不属于项目 {projectId}，拒绝插入。");

        if (reservation.State != ReservationState.Bound || reservation.ObjectIdentityId is null)
        {
            throw new CoreDomainConflictException(
                $"Reservation {reservationId} 处于 {reservation.State} 状态，仅 Bound 可正式创建（TX-55）。");
        }

        var identityId = reservation.ObjectIdentityId.Value;
        var identity = database.FindObject(projectId, identityId)
            ?? throw new CoreDomainDataException($"Reservation {reservationId} 绑定的身份 {identityId} 不存在（数据损坏）。");

        if (identity.LifecycleStatus != ObjectLifecycleStatus.Draft)
        {
            throw new CoreDomainConflictException(
                $"身份 {identityId} 处于 {identity.LifecycleStatus} 状态，仅 draft 可正式创建（R-状态机）。");
        }

        // R-定义：definition schema 版本必须与身份 pin 一致；typed 字段/聚合校验经
        // descriptor registry 统一路由（CD-02-C/E：采纳类型走 P2-09-38/39 聚合 schema）。
        ObjectRules.ValidateDefinitionSchemaVersion(identity, definition.DefinitionSchemaVersion);
        var type = database.FindObjectType(projectId, identity.ObjectTypeId)
            ?? throw new CoreDomainDataException($"身份 {identityId} 引用的 Object Type {identity.ObjectTypeId} 不存在（数据损坏）。");
        var canonicalDefinition = CanonicalJson.Canonicalize(definition.DefinitionPayloadJson);
        var descriptor = Registry.ObjectTypeRegistry.Require(type.TypeKey);
        Registry.ObjectTypeRegistry.ValidateDefinition(type.TypeKey, canonicalDefinition);
        if (descriptor.HasTypedAggregateSchema && HasGeometryComponents(canonicalDefinition))
        {
            throw new CoreDomainConflictException(
                "Building/Cave Object creation must start with an empty geometry_components array; "
                + "publish formal Geometry Assets afterward through PublishDefinitionWithGeometryAssets.");
        }

        ObjectInsertInitialRevisionAppend? initialRevisionAppend = null;
        if (initialMembership is not null)
        {
            if (registration is null)
            {
                throw new CoreDomainConflictException("Initial State 成员要求同事务的 Map 注册（P3⇒P2），拒绝插入。");
            }

            if (identity.SharingScope == ObjectSharingScope.MapLocal && identity.OwnerMapId != registration.MapId)
            {
                throw new CoreDomainConflictException("map_local Object 不得注册到 owner Map 之外的 Map（R-注册）。");
            }

            if (database.FindMap(projectId, registration.MapId) is null)
            {
                throw new CoreDomainConflictException($"目标 Map {registration.MapId} 不存在或不属于项目 {projectId}。");
            }

            // 活动快照推导 revision 追加上下文（父快照 + 新成员）；持久化层事务内
            // 重读 cursor 重验 CAS 并重算指纹/操作描述，双重 fail-closed。
            initialRevisionAppend = MapInitialStateService.BuildInsertObjectAppend(
                database, projectId, registration.MapId, identityId, initialMembership);
        }

        database.InsertObject(new ObjectInsertPlan
        {
            ProjectId = projectId,
            ObjectIdentityId = identityId,
            ReservationId = reservationId,
            Definition = definition,
            Registration = registration,
            InitialMembership = initialMembership,
            InitialRevisionAppend = initialRevisionAppend,
            NowUtcMs = nowUtcMs,
        });
    }

    private static bool HasGeometryComponents(string canonicalPayload)
    {
        using var document = JsonDocument.Parse(canonicalPayload);
        return document.RootElement.TryGetProperty("geometry_components", out var components)
            && components.ValueKind == JsonValueKind.Array
            && components.GetArrayLength() > 0;
    }
}
