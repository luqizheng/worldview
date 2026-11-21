using System;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Objects;

/// <summary>
/// Object ID Reservation 服务（P2-09-04 §5/§6/§7 状态机与幂等语义；CD-02-A §5/§8）。
/// 事务边界归持久化适配器（事务内重读校验前置状态）；本服务负责输入规范化、
/// 目录/配置解析与领域规则，是 Reservation 状态机的唯一服务入口。
/// </summary>
public static class ObjectIdReservationService
{
    /// <summary>
    /// 按 Object Type 行分配 ID。类型必须存在、属于该 Project 且 allocatable。
    /// request_key 提供时幂等对账（同键返回既有分配，不重复消耗 ID）。
    /// </summary>
    public static ReservationAllocation Reserve(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid objectTypeId,
        string? requestKey,
        long nowUtcMs)
    {
        var type = database.FindObjectType(projectId, objectTypeId)
            ?? throw new CoreDomainConflictException($"Object Type {objectTypeId} 不存在或不属于项目 {projectId}，拒绝分配。");

        if (!type.Allocatable)
        {
            throw new CoreDomainConflictException($"Object Type \"{type.TypeKey}\" 不可分配（allocatable=false），拒绝分配。");
        }

        return database.ReserveObjectId(new ObjectIdReservationRequest
        {
            ProjectId = projectId,
            ObjectTypeId = objectTypeId,
            RequestKey = ReservationRules.NormalizeRequestKey(requestKey),
            NowUtcMs = nowUtcMs,
        });
    }

    /// <summary>按稳定 type_key 分配（先解析 Project 内类型行；未知 key fail-closed）。</summary>
    public static ReservationAllocation ReserveByTypeKey(
        ICoreDomainDatabase database,
        Guid projectId,
        string typeKey,
        string? requestKey,
        long nowUtcMs)
    {
        var normalizedKey = ObjectRules.NormalizeTypeKey(typeKey);
        var type = database.FindObjectTypeByTypeKey(projectId, normalizedKey)
            ?? throw new CoreDomainConflictException(
                $"Project {projectId} 内不存在 Object Type \"{normalizedKey}\"（未播种或未知类型），拒绝分配。");

        return Reserve(database, projectId, type.ObjectTypeId, requestKey, nowUtcMs);
    }

    /// <summary>
    /// Reserved→Bound：生成身份键、按内置目录推导 pin、写 draft 身份行并转换状态
    /// （同事务）。scope 配对规则（R-scope）在服务层校验；事务内重读兜底并发。
    /// </summary>
    public static ReservationRecord Bind(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid reservationId,
        ObjectSharingScope sharingScope,
        Guid? ownerMapId,
        long nowUtcMs)
    {
        var reservation = database.FindReservation(projectId, reservationId)
            ?? throw new CoreDomainConflictException($"Reservation {reservationId} 不存在或不属于项目 {projectId}，拒绝绑定。");

        ReservationRules.ValidateTransition(reservation.State, ReservationState.Bound);

        var type = database.FindObjectType(projectId, reservation.ObjectTypeId)
            ?? throw new CoreDomainDataException(
                $"Reservation {reservationId} 引用的 Object Type {reservation.ObjectTypeId} 不存在（数据损坏）。");

        // pin 按内置目录推导（CD-02-B 无 registry runtime；未知类型 fail-closed）。
        ObjectRules.ValidatePin(type.TypeKey, BuiltinObjectTypes.DescriptorSchemaVersion, BuiltinObjectTypes.SemanticsIdFor(type.TypeKey));
        ObjectRules.ValidateScopePairing(sharingScope, ownerMapId);

        return database.BindReservation(new ObjectBindRequest
        {
            ProjectId = projectId,
            ReservationId = reservationId,
            ObjectIdentityId = Guid.NewGuid(),
            SharingScope = sharingScope,
            OwnerMapId = ownerMapId,
            NowUtcMs = nowUtcMs,
        });
    }

    /// <summary>
    /// 放弃 Reservation（Reserved/Bound→Abandoned）。Abandoned 为终态；Bound 后放弃
    /// 保留 identity 墓碑；已分配 ID 永不复用（P2-09-04 §5）。
    /// </summary>
    public static ReservationRecord Abandon(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid reservationId,
        string? abandonReason,
        long nowUtcMs)
    {
        var reservation = database.FindReservation(projectId, reservationId)
            ?? throw new CoreDomainConflictException($"Reservation {reservationId} 不存在或不属于项目 {projectId}，无法放弃。");

        ReservationRules.ValidateTransition(reservation.State, ReservationState.Abandoned);

        return database.AbandonReservation(projectId, reservationId, ReservationRules.NormalizeAbandonReason(abandonReason), nowUtcMs);
    }
}
