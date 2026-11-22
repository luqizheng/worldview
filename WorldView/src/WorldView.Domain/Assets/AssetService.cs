using System;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Registry;

namespace WorldView.Domain.Assets;

/// <summary>
/// Core Asset 元数据权威服务（CD-02-D）。发布流程（P2-09-12 TX-50；ME-05-02 §8）：
/// 字节先经 IAssetStore 不可变发布（staging → hash → 原子发布）→ 再经本服务登记
/// core_asset 元数据 → 建立 typed link。"file existence never implies successful
/// publication"——字节存在不等于登记完成；失败后未引用孤儿仅在安全对账后清理。
/// 本服务只管元数据与引用；字节验证组合在 Application/使用方层（IAssetStore）。
/// </summary>
public static class AssetService
{
    /// <summary>
    /// 登记 Asset 元数据（内容寻址幂等：同内容已登记 → 返回既有行，不新建）。
    /// hash 必须为 64 位小写十六进制 SHA-256；relative_path 按内容寻址约定派生并校验圈禁。
    /// </summary>
    public static AssetRecord RegisterAsset(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid assetId,
        string contentHash,
        long byteLength,
        string mediaType,
        string formatVersion,
        long nowUtcMs)
    {
        if (byteLength < 0)
        {
            throw new ArgumentException("byte_length 必须 >= 0。", nameof(byteLength));
        }

        var hash = AssetRules.NormalizeContentHash(contentHash);
        var relativePath = AssetRules.ContentAddressedRelativePath(hash);

        var existing = database.FindAssetByContentHash(projectId, "sha-256", hash);
        if (existing is not null)
        {
            return existing;
        }

        return database.RegisterAsset(new RegisterAssetPlan
        {
            AssetId = assetId,
            ProjectId = projectId,
            ContentHash = hash,
            ByteLength = byteLength,
            MediaType = mediaType,
            FormatVersion = formatVersion,
            CreatedAtUtcMs = nowUtcMs,
        });
    }

    /// <summary>
    /// 建立 definition geometry typed link。校验：Object 存在且 active（draft/abandoned
    /// 不得建立正式引用）、Object Type 的 role 已在 descriptor 声明（registry）、Asset
    /// 存在于同 Project 且完整性状态可用（corrupt/missing 拒绝）、重复 link 冲突。
    /// </summary>
    public static ObjectDefinitionAssetLinkRecord LinkDefinitionAsset(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid objectIdentityId,
        Guid assetId,
        string role,
        long nowUtcMs)
    {
        var identity = database.FindObject(projectId, objectIdentityId)
            ?? throw new CoreDomainConflictException($"Object {objectIdentityId} 不存在或不属于项目 {projectId}，拒绝建立 Asset link。");
        ObjectRules.ValidateUsableForMapPresence(identity);

        var type = database.FindObjectType(projectId, identity.ObjectTypeId)
            ?? throw new CoreDomainDataException($"身份 {objectIdentityId} 引用的 Object Type {identity.ObjectTypeId} 不存在（数据损坏）。");
        try
        {
            ObjectTypeRegistry.ValidateGeometryRole(type.TypeKey, role);
        }
        catch (RegistryException ex)
        {
            throw new CoreDomainConflictException(ex.Message, ex);
        }

        var asset = database.FindAsset(projectId, assetId)
            ?? throw new CoreDomainConflictException($"Asset {assetId} 不存在或不属于项目 {projectId}，拒绝建立 Asset link。");
        if (asset.IntegrityState != AssetIntegrityState.Verified)
        {
            throw new CoreDomainConflictException(
                $"Asset {assetId} 完整性状态为 {asset.IntegrityState}；只有已完成字节完整性验证的 Verified Asset 才能建立正式引用。");
        }

        if (database.FindDefinitionAssetLink(projectId, objectIdentityId, assetId, role) is not null)
        {
            throw new CoreDomainConflictException(
                $"Asset {assetId} 已以 role \"{role}\" 链接到 Object {objectIdentityId}，拒绝重复 link。");
        }

        var plan = new LinkDefinitionAssetPlan
        {
            ObjectDefinitionAssetId = Guid.NewGuid(),
            ProjectId = projectId,
            ObjectIdentityId = objectIdentityId,
            AssetId = assetId,
            Role = role,
            CreatedAtUtcMs = nowUtcMs,
        };
        database.LinkDefinitionAsset(plan);
        return new ObjectDefinitionAssetLinkRecord
        {
            ObjectDefinitionAssetId = plan.ObjectDefinitionAssetId,
            ProjectId = projectId,
            ObjectIdentityId = objectIdentityId,
            AssetId = assetId,
            Role = role,
            CreatedAtUtcMs = nowUtcMs,
        };
    }

    /// <summary>移除 definition Asset link（不存在 → 冲突）。被引用期间 Asset 字节保持可用；link 移除不触发字节清理（孤儿对账另行处理）。</summary>
    public static void UnlinkDefinitionAsset(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid objectIdentityId,
        Guid assetId,
        string role)
    {
        if (database.FindDefinitionAssetLink(projectId, objectIdentityId, assetId, role) is null)
        {
            throw new CoreDomainConflictException(
                $"Asset {assetId} 未以 role \"{role}\" 链接到 Object {objectIdentityId}，无法移除。");
        }

        database.UnlinkDefinitionAsset(projectId, objectIdentityId, assetId, role);
    }
}
