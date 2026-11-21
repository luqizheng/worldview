using System;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Objects;

/// <summary>
/// Object 身份/定义/注册领域规则（CD-02-A §14 校验清单 R-钉住/R-scope/R-状态机/R-定义）。
/// 全部 fail-closed：违规抛 <see cref="CoreDomainConflictException"/>（状态/CAS 冲突）或
/// <see cref="ArgumentException"/>（输入非法）。
/// </summary>
public static class ObjectRules
{
    /// <summary>type_key 词汇：小写字母/数字/下划线，1–64 字符（十内置类型均符合）。</summary>
    public static string NormalizeTypeKey(string? typeKey)
    {
        if (string.IsNullOrWhiteSpace(typeKey))
        {
            throw new ArgumentException("type_key 不能为空白。", nameof(typeKey));
        }

        var key = typeKey.Trim();
        if (key.Length > 64)
        {
            throw new ArgumentException("type_key 超过 64 字符上限。", nameof(typeKey));
        }

        foreach (var c in key)
        {
            var isAllowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
            if (!isAllowed)
            {
                throw new ArgumentException($"type_key \"{key}\" 含非法字符（仅允许小写字母/数字/下划线）。", nameof(typeKey));
            }
        }

        return key;
    }

    /// <summary>显示名规范化：非空白、去首尾空白。</summary>
    public static string NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("显示名不能为空白。", nameof(displayName));
        }

        return displayName.Trim();
    }

    /// <summary>R-scope 配对：ProjectShared⇒owner_map 为空；MapLocal⇒owner_map 必填。</summary>
    public static void ValidateScopePairing(ObjectSharingScope scope, Guid? ownerMapId)
    {
        switch (scope)
        {
            case ObjectSharingScope.ProjectShared when ownerMapId is not null:
                throw new CoreDomainConflictException("project_shared Object 不允许指定 owner Map（R-scope）。");
            case ObjectSharingScope.MapLocal when ownerMapId is null:
                throw new CoreDomainConflictException("map_local Object 必须指定 owner Map（R-scope）。");
        }
    }

    /// <summary>
    /// R-状态机（身份级）：仅 draft→active（Insert 路径）、draft→abandoned、active→retired。
    /// retired 本阶段无服务入口（仅保留枚举与 CHECK，Handoff §5.8）。
    /// </summary>
    public static bool CanTransitionLifecycle(ObjectLifecycleStatus from, ObjectLifecycleStatus to) =>
        (from, to) switch
        {
            (ObjectLifecycleStatus.Draft, ObjectLifecycleStatus.Active) => true,
            (ObjectLifecycleStatus.Draft, ObjectLifecycleStatus.Abandoned) => true,
            (ObjectLifecycleStatus.Active, ObjectLifecycleStatus.Retired) => true,
            _ => false,
        };

    /// <summary>R-成员前置：draft/abandoned 身份不得进入 membership/注册/Initial State。</summary>
    public static void ValidateUsableForMapPresence(ObjectRecord identity)
    {
        if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
        {
            throw new CoreDomainConflictException(
                $"身份 {identity.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态，"
                + "draft/abandoned 身份不得进入 Map 注册或 Initial State（R-成员前置）。");
        }
    }

    /// <summary>
    /// R-钉住：pin 值必须与内置目录声明一致（CD-02-B 十类型 = P2-09-26 V0.2：
    /// schema_version=2 + world-view.object-type.&lt;key&gt;.v1）；不一致 fail-closed。
    /// </summary>
    public static void ValidatePin(string typeKey, long schemaVersion, string semanticsId)
    {
        var expectedSemanticsId = BuiltinObjectTypes.SemanticsIdFor(typeKey);
        if (schemaVersion != BuiltinObjectTypes.DescriptorSchemaVersion
            || !string.Equals(semanticsId, expectedSemanticsId, StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException(
                $"Object Type \"{typeKey}\" 的 pin (schema_version={schemaVersion}, semantics_id=\"{semanticsId}\") "
                + $"与内置 descriptor 声明不一致（预期 {BuiltinObjectTypes.DescriptorSchemaVersion} / \"{expectedSemanticsId}\"），fail-closed（R-钉住）。");
        }
    }

    /// <summary>读侧 fail-closed：身份行的 pin 不被当前二进制认识时拒绝解释（Handoff §5.4）。</summary>
    public static void ValidateKnownPin(string typeKey, long schemaVersion, string semanticsId) =>
        ValidatePin(typeKey, schemaVersion, semanticsId);

    /// <summary>
    /// R-定义：definition_schema_version 必须与身份行钉住的 object_type_schema_version 一致；
    /// 不支持版本 fail-closed（P2-09-18 §10 pin 规则）。
    /// </summary>
    public static void ValidateDefinitionSchemaVersion(ObjectRecord identity, long definitionSchemaVersion)
    {
        if (definitionSchemaVersion != identity.ObjectTypeSchemaVersion)
        {
            throw new CoreDomainConflictException(
                $"definition_schema_version ({definitionSchemaVersion}) 与身份钉住的 object_type_schema_version "
                + $"({identity.ObjectTypeSchemaVersion}) 不一致，拒绝（R-定义）。");
        }
    }
}

/// <summary>
/// Reservation 状态机规则（P2-09-04 §5/§7 七条硬约束的领域形式）。
/// </summary>
public static class ReservationRules
{
    /// <summary>合法转换：Reserved→Bound、Reserved→Abandoned、Bound→Inserted、Bound→Abandoned。</summary>
    public static bool CanTransition(ReservationState from, ReservationState to) =>
        (from, to) switch
        {
            (ReservationState.Reserved, ReservationState.Bound) => true,
            (ReservationState.Reserved, ReservationState.Abandoned) => true,
            (ReservationState.Bound, ReservationState.Inserted) => true,
            (ReservationState.Bound, ReservationState.Abandoned) => true,
            _ => false,
        };

    /// <summary>非法转换抛冲突异常（终态不可重开，P2-09-04 §7.6）。</summary>
    public static void ValidateTransition(ReservationState from, ReservationState to)
    {
        if (!CanTransition(from, to))
        {
            throw new CoreDomainConflictException($"Reservation 状态转换 {from} → {to} 非法（P2-09-04 §5 状态机）。");
        }
    }

    /// <summary>
    /// request_key 规范化：空白 → null（无幂等要求）；非空白去首尾空白。
    /// 提供 (project, request_key) 唯一（P2-09-04 §6）。
    /// </summary>
    public static string? NormalizeRequestKey(string? requestKey)
    {
        if (string.IsNullOrWhiteSpace(requestKey))
        {
            return null;
        }

        var key = requestKey.Trim();
        if (key.Length > 256)
        {
            throw new ArgumentException("request_key 超过 256 字符上限。", nameof(requestKey));
        }

        return key;
    }

    /// <summary>放弃原因规范化：空白 → null；非空白去首尾空白。</summary>
    public static string? NormalizeAbandonReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        return reason.Trim();
    }
}
