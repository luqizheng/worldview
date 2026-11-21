using System;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Objects;

/// <summary>
/// Object 正式创建服务（Bound→Inserted 原子边界，P2-09-12 TX-55；CD-02-A §13.2）。
/// 定义首行、Map 注册、Initial State 成员与身份激活/状态转换同事务提交；失败整体
/// 回滚、无半成品；重试复用同一 reservation/identity（绝不分配替代 ID）。
/// </summary>
public static class ObjectCreationService
{
    /// <summary>
    /// 正式创建 Object（Bound→Inserted）。
    /// <paramref name="definition"/>：首行定义（schema 版本须与身份 pin 一致；
    /// payload canonical 化与 hash 计算归 Domain/适配器路径）。
    /// <paramref name="registration"/>：可选 Map 注册（map_local 仅 owner Map）。
    /// <paramref name="initialMembership"/>：可选 time-zero 成员；指定时必须同时
    /// 注册到同一 Map（P3⇒P2），基线缺失时同事务建立。
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

        // R-定义：definition schema 版本必须与身份 pin 一致。
        ObjectRules.ValidateDefinitionSchemaVersion(identity, definition.DefinitionSchemaVersion);
        ValidatePlacementPairing(initialMembership);

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
        }

        database.InsertObject(new ObjectInsertPlan
        {
            ProjectId = projectId,
            ObjectIdentityId = identityId,
            ReservationId = reservationId,
            Definition = definition,
            Registration = registration,
            InitialMembership = initialMembership,
            NowUtcMs = nowUtcMs,
        });
    }

    /// <summary>placement payload 与 schema version 必须成对出现（迁移 CHECK 的服务层前置）。</summary>
    private static void ValidatePlacementPairing(InitialObjectMembership? membership)
    {
        if (membership is null)
        {
            return;
        }

        var hasPayload = membership.PlacementPayloadJson is not null;
        var hasVersion = membership.PlacementSchemaVersion is not null;
        if (hasPayload != hasVersion)
        {
            throw new ArgumentException("初始 placement payload 与 schema version 必须成对提供。", nameof(membership));
        }
    }
}
