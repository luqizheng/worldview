using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Settings;

namespace WorldView.Infrastructure.Settings;

/// <summary>
/// Content-addressed settings document storage. Staging and final files live under the same
/// package Settings directory; this store never removes published bytes except through an
/// explicit orphan-collection call made after a fresh SQLite reference scan.
/// </summary>
public sealed class SettingsDocumentFileStore : ISettingsDocumentFileStore
{
    private readonly string _packageRoot;
    private readonly string _settingsRoot;
    private readonly string _stagingRoot;
    private readonly string _hashRoot;

    public SettingsDocumentFileStore(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        _packageRoot = Path.GetFullPath(packageRoot);
        _settingsRoot = Path.Combine(_packageRoot, SettingsDocumentPaths.SettingsDirectoryName);
        _stagingRoot = Path.Combine(_settingsRoot, SettingsDocumentPaths.StagingDirectoryName);
        _hashRoot = Path.Combine(_settingsRoot, "sha256");

        if (!Directory.Exists(_packageRoot))
            throw new DirectoryNotFoundException("Settings storage requires an existing package root.");
        EnsureNotReparsePoint(_packageRoot);
        Directory.CreateDirectory(_settingsRoot);
        EnsureNotReparsePoint(_settingsRoot);
        Directory.CreateDirectory(_stagingRoot);
        EnsureNotReparsePoint(_stagingRoot);
        Directory.CreateDirectory(_hashRoot);
        EnsureNotReparsePoint(_hashRoot);
    }

    public async Task<SettingsDocumentFilePutResult> PutAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var stagingPath = Path.Combine(_stagingRoot, $"{Guid.NewGuid():N}.tmp");
        long byteLength = 0;
        string hash;

        try
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            await using (var output = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, buffer.Length, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    byteLength = checked(byteLength + read);
                    if (byteLength > SettingsDocumentFormat.MaximumDocumentBytes)
                        throw new InvalidDataException("Settings document exceeds the V1 size limit.");
                    digest.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                if (byteLength == 0)
                    throw new InvalidDataException("Settings document cannot be empty.");

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            hash = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            await VerifyFileAsync(stagingPath, hash, byteLength, cancellationToken).ConfigureAwait(false);
            var relative = SettingsDocumentPaths.GetRelativePath(hash);
            if (!SettingsDocumentPaths.TryResolvePublishedPath(_packageRoot, relative, hash, out var finalPath))
                throw new InvalidDataException("Settings document destination is not confined to the package root.");

            var firstLevel = Path.Combine(_hashRoot, hash[..2]);
            EnsureDirectoryExistsSafe(firstLevel);
            var secondLevel = Path.Combine(firstLevel, hash[2..4]);
            EnsureDirectoryExistsSafe(secondLevel);
            EnsureDirectoryChainSafe(secondLevel);
            if (File.Exists(finalPath))
            {
                await VerifyFileAsync(finalPath, hash, byteLength, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    File.Move(stagingPath, finalPath);
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    await VerifyFileAsync(finalPath, hash, byteLength, cancellationToken).ConfigureAwait(false);
                }
            }

            TryDeleteStaging(stagingPath);
            return new SettingsDocumentFilePutResult
            {
                ContentHash = hash,
                ByteLength = byteLength,
                RelativePath = relative,
            };
        }
        catch
        {
            TryDeleteStaging(stagingPath);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relative = SettingsDocumentPaths.GetRelativePath(contentHash);
        if (!SettingsDocumentPaths.TryResolvePublishedPath(_packageRoot, relative, contentHash, out var path))
            throw new InvalidDataException("Settings document path is not confined to the package root.");
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        EnsureDirectoryChainSafe(Path.GetDirectoryName(path)!);
        EnsureNotReparsePoint(path);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public IReadOnlyList<string> ListPublishedContentHashes()
    {
        if (!Directory.Exists(_hashRoot))
            return Array.Empty<string>();
        EnsureDirectoryChainSafe(_hashRoot);

        var hashes = new System.Collections.Generic.List<string>();
        foreach (var firstLevel in Directory.EnumerateDirectories(_hashRoot))
        {
            EnsureNotReparsePoint(firstLevel);
            foreach (var secondLevel in Directory.EnumerateDirectories(firstLevel))
            {
                EnsureNotReparsePoint(secondLevel);
                foreach (var path in Directory.EnumerateFiles(secondLevel, "*.json", SearchOption.TopDirectoryOnly))
                {
                    EnsureNotReparsePoint(path);
                    var hash = Path.GetFileNameWithoutExtension(path);
                    if (hash.Length != 64 || !hash.All(IsLowerHex))
                        continue;
                    var relative = SettingsDocumentPaths.GetRelativePath(hash);
                    if (SettingsDocumentPaths.TryResolvePublishedPath(_packageRoot, relative, hash, out var expected)
                        && string.Equals(Path.GetFullPath(path), expected, PathComparison))
                        hashes.Add(hash);
                }
            }
        }

        return hashes.OrderBy(hash => hash, StringComparer.Ordinal).ToArray();
    }

    public int CleanupStagingFiles()
    {
        if (!Directory.Exists(_stagingRoot))
            return 0;
        EnsureDirectoryChainSafe(_stagingRoot);
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(_stagingRoot, "*.tmp", SearchOption.TopDirectoryOnly))
        {
            EnsureNotReparsePoint(path);
            File.Delete(path);
            deleted++;
        }
        return deleted;
    }

    public bool Delete(string contentHash)
    {
        var relative = SettingsDocumentPaths.GetRelativePath(contentHash);
        if (!SettingsDocumentPaths.TryResolvePublishedPath(_packageRoot, relative, contentHash, out var path))
            throw new InvalidDataException("Settings document path is not confined to the package root.");
        if (!File.Exists(path))
            return false;
        EnsureDirectoryChainSafe(Path.GetDirectoryName(path)!);
        EnsureNotReparsePoint(path);
        File.Delete(path);
        return true;
    }

    private static void EnsureDirectoryExistsSafe(string directory)
    {
        if (Directory.Exists(directory))
        {
            EnsureNotReparsePoint(directory);
            return;
        }

        var parent = Path.GetDirectoryName(directory);
        if (parent is null)
            throw new InvalidDataException("Settings directory has no package parent.");
        EnsureNotReparsePoint(parent);
        Directory.CreateDirectory(directory);
        EnsureNotReparsePoint(directory);
    }

    private void EnsureDirectoryChainSafe(string directory)
    {
        var root = Path.TrimEndingDirectorySeparator(_packageRoot);
        var rootPrefix = root + Path.DirectorySeparatorChar;
        var current = Path.GetFullPath(directory);
        while (true)
        {
            if (!string.Equals(Path.TrimEndingDirectorySeparator(current), root, PathComparison)
                && !current.StartsWith(rootPrefix, PathComparison))
                throw new InvalidDataException("Settings directory escaped the package root.");

            if (Directory.Exists(current))
                EnsureNotReparsePoint(current);
            if (string.Equals(Path.TrimEndingDirectorySeparator(current), root, PathComparison))
                return;

            var parent = Path.GetDirectoryName(current);
            if (parent is null)
                throw new InvalidDataException("Settings directory escaped the package root.");
            current = parent;
        }
    }

    private static async Task VerifyFileAsync(string path, string expectedHash, long expectedLength,
        CancellationToken cancellationToken)
    {
        EnsureNotReparsePoint(path);
        var info = new FileInfo(path);
        if (info.Length != expectedLength)
            throw new InvalidDataException("Settings document file has an unexpected byte length.");

        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            digest.AppendData(buffer, 0, read);
        }

        var actual = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
            throw new InvalidDataException("Settings document file does not match its SHA-256 path.");
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path))
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Settings storage must not traverse symbolic links or reparse points.");
    }

    private static bool IsLowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void TryDeleteStaging(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Startup recovery removes stale staging files after a fresh package open.
        }
    }
}
