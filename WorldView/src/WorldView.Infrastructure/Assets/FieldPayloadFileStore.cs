using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;

namespace WorldView.Infrastructure.Assets;

/// <summary>
/// Package-managed content-addressed Field Payload files. Staging files live beside final
/// files so File.Move stays on the same filesystem. This store never deletes files autonomously;
/// explicit orphan collection must follow a fresh authoritative SQLite reference scan.
/// </summary>
public sealed class FieldPayloadFileStore : IFieldPayloadFileStore
{
    private const string RelativePrefix = "FieldPayloads/sha256/";
    public FieldPayloadFileStore(string packageRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRootPath);
        var packageRoot = Path.GetFullPath(packageRootPath);
        var payloadDirectory = Path.Combine(packageRoot, "FieldPayloads");
        RootPath = Path.Combine(payloadDirectory, "sha256");
        Directory.CreateDirectory(payloadDirectory);
        EnsureNotReparsePoint(payloadDirectory);
        Directory.CreateDirectory(RootPath);
        EnsureNotReparsePoint(RootPath);
    }

    public string RootPath { get; }

    public async Task<FieldPayloadFilePutResult> PutAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var stagingPath = Path.Combine(RootPath, $".staging-{Guid.NewGuid():N}.tmp");
        long byteLength = 0;
        string contentHash;

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            await using (var target = new FileStream(
                stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: buffer.Length, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    byteLength = checked(byteLength + read);
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                target.Flush(flushToDisk: true);
            }

            if (byteLength <= 0)
                throw new InvalidDataException("Field Payload cannot be empty.");

            contentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            await VerifyExistingAsync(stagingPath, contentHash, byteLength, cancellationToken).ConfigureAwait(false);
            var fileName = contentHash;
            var finalPath = Path.Combine(RootPath, fileName);

            if (File.Exists(finalPath))
            {
                await VerifyExistingAsync(finalPath, contentHash, byteLength, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    File.Move(stagingPath, finalPath);
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    // Content-addressed idempotency if another writer published the same hash.
                    await VerifyExistingAsync(finalPath, contentHash, byteLength, cancellationToken).ConfigureAwait(false);
                }
            }

            return new FieldPayloadFilePutResult
            {
                ContentHash = contentHash,
                ByteLength = byteLength,
                RelativePath = RelativePrefix + contentHash,
            };
        }
        finally
        {
            TryDeleteStaging(stagingPath);
        }
    }

    public Task<Stream?> OpenReadAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeHash(contentHash))
            throw new ArgumentException("Field Payload SHA-256 must be 64 lowercase hexadecimal characters.", nameof(contentHash));

        var path = Path.Combine(RootPath, contentHash);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        EnsureNotReparsePoint(path);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public bool Exists(string contentHash)
    {
        if (!IsSafeHash(contentHash))
            return false;
        var path = Path.Combine(RootPath, contentHash);
        if (!File.Exists(path))
            return false;
        EnsureNotReparsePoint(path);
        return true;
    }

    public IReadOnlyList<string> ListContentHashes()
    {
        return Directory.EnumerateFiles(RootPath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => IsSafeHash(name))
            .Select(name =>
            {
                var path = Path.Combine(RootPath, name!);
                EnsureNotReparsePoint(path);
                return name!;
            })
            .OrderBy(hash => hash, StringComparer.Ordinal)
            .ToArray();
    }

    public bool Delete(string contentHash)
    {
        if (!IsSafeHash(contentHash))
            throw new ArgumentException("Field Payload SHA-256 must be 64 lowercase hexadecimal characters.", nameof(contentHash));

        var path = Path.Combine(RootPath, contentHash);
        if (!File.Exists(path))
            return false;
        EnsureNotReparsePoint(path);
        File.Delete(path);
        return true;
    }

    private static async Task VerifyExistingAsync(
        string path,
        string expectedHash,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        EnsureNotReparsePoint(path);
        var info = new FileInfo(path);
        if (info.Length != expectedLength)
            throw new InvalidDataException("Existing Field Payload file has a conflicting byte length.");

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }

        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
            throw new InvalidDataException("Existing Field Payload file does not match its content-addressed SHA-256.");
    }


    private static void EnsureNotReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Field Payload managed storage must not traverse symbolic links or reparse points.");
    }

    private static bool IsSafeHash(string? value)
    {
        if (value is null || value.Length != 64)
            return false;
        foreach (var character in value)
        {
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                return false;
        }
        return true;
    }

    private static void TryDeleteStaging(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Preserve the primary write result; a later recovery scan handles stale staging files.
        }
    }
}
