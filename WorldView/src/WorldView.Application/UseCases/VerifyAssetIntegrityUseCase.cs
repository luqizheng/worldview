using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;

namespace WorldView.Application.UseCases;

/// <summary>
/// 以 Project Asset Store 中的真实不可变字节对账 Core Asset 完整性元数据。
/// 文件存在本身绝不等于 Asset Verified；hash 与 length 必须从实际字节计算。
/// </summary>
public sealed class VerifyAssetIntegrityUseCase
{
    private readonly ICoreDomainDatabase _database;
    private readonly IAssetStore _assetStore;
    private readonly TimeProvider _timeProvider;

    public VerifyAssetIntegrityUseCase(
        ICoreDomainDatabase database,
        IAssetStore assetStore,
        TimeProvider? timeProvider = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _assetStore = assetStore ?? throw new ArgumentNullException(nameof(assetStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AssetIntegrityVerificationResult> ExecuteAsync(
        Guid projectId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = _database.FindAsset(projectId, assetId);
        if (asset is null)
        {
            return new AssetIntegrityVerificationResult(
                AssetIntegrityVerificationOutcome.AssetNotFound,
                assetId,
                null,
                null,
                "Asset 不存在或不属于当前 Project。");
        }

        Stream? content;
        try
        {
            content = await _assetStore.OpenReadAsync(asset.ContentHash, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _database.RecordAssetIntegrityFailure(
                projectId, assetId, AssetIntegrityState.Corrupt, NowUtcMs());

            return new AssetIntegrityVerificationResult(
                AssetIntegrityVerificationOutcome.ReadFailed,
                assetId,
                asset.ContentHash,
                null,
                $"Asset 字节读取失败：{ex.Message}");
        }

        if (content is null)
        {
            _database.RecordAssetIntegrityFailure(
                projectId, assetId, AssetIntegrityState.Missing, NowUtcMs());

            return new AssetIntegrityVerificationResult(
                AssetIntegrityVerificationOutcome.Missing,
                assetId,
                asset.ContentHash,
                null,
                "Asset Store 中不存在对应内容。");
        }

        string actualHash;
        long actualLength;
        try
        {
            await using (content)
            {
                (actualHash, actualLength) = await ComputeHashAsync(content, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _database.RecordAssetIntegrityFailure(
                projectId, assetId, AssetIntegrityState.Corrupt, NowUtcMs());

            return new AssetIntegrityVerificationResult(
                AssetIntegrityVerificationOutcome.ReadFailed,
                assetId,
                asset.ContentHash,
                null,
                $"Asset 字节读取失败：{ex.Message}");
        }

        var matches = string.Equals(actualHash, asset.ContentHash, StringComparison.Ordinal)
            && actualLength == asset.ByteLength;

        if (matches)
        {
            _database.MarkAssetVerified(
                projectId,
                assetId,
                new AssetVerificationEvidence(asset.ContentHash, actualHash, asset.ByteLength, actualLength),
                NowUtcMs());
        }
        else
        {
            _database.RecordAssetIntegrityFailure(
                projectId, assetId, AssetIntegrityState.Corrupt, NowUtcMs());
        }

        return new AssetIntegrityVerificationResult(
            matches ? AssetIntegrityVerificationOutcome.Verified : AssetIntegrityVerificationOutcome.Corrupt,
            assetId,
            asset.ContentHash,
            actualHash,
            matches
                ? null
                : $"Asset 字节与元数据不一致（预期长度 {asset.ByteLength}，实际长度 {actualLength}；"
                    + $"预期 hash {asset.ContentHash}，实际 hash {actualHash}）。");
    }

    private long NowUtcMs() => _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

    private static async Task<(string Hash, long Length)> ComputeHashAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            length = checked(length + read);
        }

        return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length);
    }
}

/// <summary>字节验证结果（缺失/长度不符/hash 不符/读取失败分别可区分）。</summary>
public enum AssetIntegrityVerificationOutcome
{
    Verified,
    Missing,
    Corrupt,
    ReadFailed,
    AssetNotFound,
}

/// <summary>验证结果与证据摘要（可追溯到对应 Asset 与实际验证值）。</summary>
public sealed record AssetIntegrityVerificationResult(
    AssetIntegrityVerificationOutcome Outcome,
    Guid AssetId,
    string? ExpectedHash,
    string? ActualHash,
    string? Diagnostic);
