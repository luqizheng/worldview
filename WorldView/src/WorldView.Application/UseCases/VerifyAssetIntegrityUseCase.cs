using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;

namespace WorldView.Application.UseCases;

/// <summary>
/// Reconciles Core Asset integrity metadata against the actual immutable bytes in the
/// current Project Asset Store. A file's existence alone never makes an Asset Verified.
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

        await using var content = await _assetStore.OpenReadAsync(asset.ContentHash, cancellationToken)
            .ConfigureAwait(false);

        if (content is null)
        {
            _database.RecordAssetIntegrityFailure(
                projectId, assetId, AssetIntegrityState.Missing, _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());

            return new AssetIntegrityVerificationResult(
                AssetIntegrityVerificationOutcome.Missing,
                assetId,
                asset.ContentHash,
                null,
                "Asset Store 中不存在对应内容。");
        }

        var (actualHash, actualLength) = await ComputeHashAsync(content, cancellationToken).ConfigureAwait(false);
        var matches = string.Equals(actualHash, asset.ContentHash, StringComparison.Ordinal)
            && actualLength == asset.ByteLength;

        if (matches)
        {
            _database.MarkAssetVerified(
                projectId,
                assetId,
                new AssetVerificationEvidence(asset.ContentHash, actualHash, asset.ByteLength, actualLength),
                _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        }
        else
        {
            _database.RecordAssetIntegrityFailure(
                projectId, assetId, AssetIntegrityState.Corrupt, _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        }

        return new AssetIntegrityVerificationResult(
            matches ? AssetIntegrityVerificationOutcome.Verified : AssetIntegrityVerificationOutcome.Corrupt,
            assetId,
            asset.ContentHash,
            actualHash,
            matches ? null : $"Asset 字节与元数据不一致（预期长度 {asset.ByteLength}，实际长度 {actualLength}）。");
    }

    private static async Task<(string Hash, long Length)> ComputeHashAsync(
        System.IO.Stream content,
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

public enum AssetIntegrityVerificationOutcome
{
    Verified,
    Missing,
    Corrupt,
    AssetNotFound,
}

public sealed record AssetIntegrityVerificationResult(
    AssetIntegrityVerificationOutcome Outcome,
    Guid AssetId,
    string? ExpectedHash,
    string? ActualHash,
    string? Diagnostic);
