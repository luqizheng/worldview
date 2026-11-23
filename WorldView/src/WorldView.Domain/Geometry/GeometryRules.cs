using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;
using WorldView.Domain.Registry;

namespace WorldView.Domain.Geometry;

/// <summary>
/// Geometry Component 逻辑 manifest（P2-09-19 §6.2 十个规范字段；ME-05-02 §3）。
/// 存于 owner typed payload（definition/Initial State/Command payload）内——V1 无
/// core_geometry_component 表（P2-09-19 §3.2）。组件身份由 Core Domain 首次正式
/// 创建时生成（opaque GUID，scope 于所属 Object，永不复用、不得跨 Object 转移）。
/// </summary>
public sealed record GeometryComponentManifest
{
    /// <summary>稳定组件身份（不是 Object ID、不消耗 allocator、不随编辑改变）。</summary>
    public required Guid GeometryComponentId { get; init; }

    /// <summary>Object Type descriptor 声明的 role（普通编辑不得静默改变）。</summary>
    public required string GeometryRole { get; init; }

    /// <summary>几何族（point/line_string/polygon/multi_point/multi_line_string/multi_polygon/curve）。</summary>
    public required string GeometryFamily { get; init; }

    /// <summary>坐标维度（V1 正式几何为 2D working plane，P2-09-19 §2.11）。</summary>
    public required int Dimension { get; init; }

    /// <summary>确切域坐标定义 ID（项目级不可变 spatial reference；V1 结构校验为非空白）。</summary>
    public required string SpatialReferenceId { get; init; }

    /// <summary>canonical geometry payload schema 版本（>0）。</summary>
    public required long GeometrySchemaVersion { get; init; }

    /// <summary>不可变 Core Asset 引用（core_asset.asset_id）。</summary>
    public required Guid AssetId { get; init; }

    /// <summary>精确 geometry 字节 hash（与 core_asset.content_hash 一致性由 Domain 验证）。</summary>
    public required string ContentHash { get; init; }

    /// <summary>来源证据（authored/imported/refined；可空占位，语义随 provenance 契约关门）。</summary>
    public string? Provenance { get; init; }

    /// <summary>领域校验结果元数据（可空占位；随 geometry descriptor 校验深化）。</summary>
    public string? ValidityMetadata { get; init; }
}

/// <summary>
/// 几何领域规则（P2-09-19/ME-05-02；role/family 校验以 descriptor registry 为依据）。
/// split/merge 边界（P2-09-19 §7.5 deferred）：几何 split/merge 绝不隐式创建/删除/
/// 合并 Object identity；in-place 调整/refinement 保留组件 ID 与 role；add 分配新 ID；
/// remove 只结束所选 History 的 membership。
/// </summary>
public static class GeometryRules
{
    /// <summary>P2-09-25 §3 有界几何族词汇（V1 正式几何；未知族拒绝）。</summary>
    public static readonly IReadOnlyList<string> KnownFamilies =
    [
        "point", "line_string", "polygon", "multi_point", "multi_line_string", "multi_polygon", "curve",
    ];

    /// <summary>V1 正式几何维度（P2-09-19 §2.11：2D working plane；3D 延后）。</summary>
    public const int SupportedDimension = 2;

    /// <summary>生成新组件身份（首次正式创建时；opaque、永不复用）。</summary>
    public static Guid NewComponentId() => Guid.NewGuid();

    /// <summary>
    /// manifest 结构 + descriptor 依据校验：role 必须已声明、family 必须在该 role 的
    /// families 内、dimension 为 V1 支持值、hash 规范、schema 版本 >0、引用非空。
    /// </summary>
    public static void ValidateManifest(ObjectTypeDescriptor descriptor, GeometryComponentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.GeometryComponentId == Guid.Empty)
        {
            throw new CoreDomainConflictException("geometry_component_id 不能为空（必须是稳定的 opaque identity）。");
        }

        if (manifest.AssetId == Guid.Empty)
        {
            throw new CoreDomainConflictException("asset_id 不能为空（必须引用已登记的 Core Asset）。");
        }

        var role = descriptor.FindGeometryRole(manifest.GeometryRole)
            ?? throw new CoreDomainConflictException(
                $"geometry role \"{manifest.GeometryRole}\" 未在 {descriptor.TypeKey} descriptor 中声明（declared_geometry_roles_only）。");

        if (!role.Families.Contains(manifest.GeometryFamily, StringComparer.Ordinal))
        {
            throw new CoreDomainConflictException(
                $"geometry family \"{manifest.GeometryFamily}\" 不在 role \"{manifest.GeometryRole}\"（{descriptor.TypeKey}）"
                + $"声明的 families [{string.Join(", ", role.Families)}] 内。");
        }

        if (manifest.Dimension != SupportedDimension)
        {
            throw new CoreDomainConflictException(
                $"geometry dimension {manifest.Dimension} 不受 V1 支持（仅 2D working plane）。");
        }

        if (string.IsNullOrWhiteSpace(manifest.SpatialReferenceId))
        {
            throw new CoreDomainConflictException("spatial_reference_id 不能为空白（P2-09-25 §4）。");
        }

        if (manifest.GeometrySchemaVersion < 1)
        {
            throw new CoreDomainConflictException("geometry_schema_version 必须 >= 1。");
        }

        try
        {
            AssetRules.NormalizeContentHash(manifest.ContentHash);
        }
        catch (ArgumentException ex)
        {
            throw new CoreDomainConflictException($"manifest content_hash 非法：{ex.Message}");
        }

        if (!KnownFamilies.Contains(manifest.GeometryFamily, StringComparer.Ordinal))
        {
            throw new CoreDomainConflictException($"geometry family \"{manifest.GeometryFamily}\" 不在 V1 词汇内。");
        }
    }

    /// <summary>
    /// 校验一个 owner payload 中的完整 Geometry Component manifest 集合：每项先执行
    /// descriptor role/family 校验，再执行各 role 的 min/max 基数、descriptor combined_min
    /// 和 owner 内 component identity 唯一性检查。本方法只校验单个 owner 快照；跨 revision
    /// 的 ID 永不复用仍由持久化 owner 服务保证。
    /// </summary>
    public static void ValidateManifestCollection(
        ObjectTypeDescriptor descriptor,
        IEnumerable<GeometryComponentManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(manifests);

        var items = manifests.ToArray();
        var componentIds = new HashSet<Guid>();

        foreach (var manifest in items)
        {
            ValidateManifest(descriptor, manifest);
            if (!componentIds.Add(manifest.GeometryComponentId))
            {
                throw new CoreDomainConflictException(
                    $"geometry_component_id \"{manifest.GeometryComponentId:D}\" 在同一 owner payload 中重复。");
            }
        }

        foreach (var role in descriptor.GeometryRoles)
        {
            var count = items.LongCount(m => string.Equals(m.GeometryRole, role.Role, StringComparison.Ordinal));
            if (count < role.Min || (role.Max is not null && count > role.Max.Value))
            {
                throw new CoreDomainConflictException(
                    $"Object Type \"{descriptor.TypeKey}\" 的 geometry role \"{role.Role}\" 数量 {count} "
                    + $"超出 descriptor 基数 [{role.Min}, {(role.Max?.ToString() ?? "unbounded")}]。");
            }
        }

        if (descriptor.GeometryCombinedMin is { } combinedMin && items.LongLength < combinedMin)
        {
            throw new CoreDomainConflictException(
                $"Object Type \"{descriptor.TypeKey}\" 的 geometry component 总数 {items.LongLength} "
                + $"小于 combined_min {combinedMin}。");
        }
    }

    /// <summary>
    /// manifest ↔ typed link ↔ core_asset 三方一致性（CD-02-A §12：两侧在 Core Domain
    /// 验证下必须一致；权威 hash 从 core_asset 解析比对；link role == manifest role）。
    /// </summary>
    public static void ValidateManifestConsistency(
        AssetRecord asset,
        ObjectDefinitionAssetLinkRecord? link,
        GeometryComponentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(manifest);

        if (asset.AssetId != manifest.AssetId)
        {
            throw new CoreDomainConflictException("manifest.asset_id 与被校验 Asset 不一致。");
        }

        if (!string.Equals(asset.ContentHash, manifest.ContentHash, StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException(
                $"manifest.content_hash 与 core_asset.content_hash 不一致（{asset.AssetId}）：fail-closed。");
        }

        if (link is not null)
        {
            if (link.AssetId != manifest.AssetId)
            {
                throw new CoreDomainConflictException("typed link.asset_id 与 manifest.asset_id 不一致。");
            }

            if (!string.Equals(link.Role, manifest.GeometryRole, StringComparison.Ordinal))
            {
                throw new CoreDomainConflictException(
                    $"typed link role \"{link.Role}\" 与 manifest geometry_role \"{manifest.GeometryRole}\" 不一致。");
            }
        }
    }
}

/// <summary>
/// 几何操作策略（P2-09-19 §7；Command 阶段的 core.geometry.* 描述符消费本分类；
/// 本阶段只落地分类与拒绝边界，不实现 Command 写路径）。
/// </summary>
public enum GeometryOperationKind
{
    /// <summary>就地调整：保留组件 ID 与 role，发布新 Asset。</summary>
    InPlaceAdjust,

    /// <summary>refinement：保留组件 ID 与 role（带源指纹政策）。</summary>
    Refinement,

    /// <summary>新增组件：分配新组件 ID。</summary>
    AddComponent,

    /// <summary>移除组件：结束所选 History 的 membership（身份不复用、不退役）。</summary>
    RemoveComponent,
}

public static class GeometryOperationRules
{
    /// <summary>InPlaceAdjust/Refinement 保留组件 ID（P2-09-19 §2.4/§7.1/§7.2）。</summary>
    public static bool PreservesComponentIdentity(GeometryOperationKind operation) =>
        operation is GeometryOperationKind.InPlaceAdjust or GeometryOperationKind.Refinement;

    /// <summary>AddComponent 分配新组件 ID（§7.3）。</summary>
    public static bool AllocatesNewComponentIdentity(GeometryOperationKind operation) =>
        operation == GeometryOperationKind.AddComponent;

    /// <summary>
    /// 几何 split/merge 在 V1 显式不支持（P2-09-19 §7.5 deferred；GEO-07/GEO-08）：
    /// 任何以几何 split/merge 改变语义 Object 数量/身份的请求按类型化拒绝——
    /// 绝不通过 add/remove 链式模拟。
    /// </summary>
    public static void RejectSplitMerge()
    {
        throw new CoreDomainConflictException(
            "geometry split/merge 在 V1 不支持（P2-09-19 §7.5 deferred；组件 lineage/映射规则未设计）；"
            + "几何 split/merge 不得创建/删除/合并 Object identity（GEO-07/GEO-08）。");
    }
}
