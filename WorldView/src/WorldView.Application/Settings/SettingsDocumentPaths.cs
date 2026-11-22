using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace WorldView.Application.Settings;

/// <summary>
/// Canonical package-relative paths for immutable settings document bytes.
/// Paths persisted in SQLite always use forward slashes on every platform.
/// </summary>
public static class SettingsDocumentPaths
{
    public const string SettingsDirectoryName = "Settings";
    public const string StagingDirectoryName = ".staging";

    public static string GetRelativePath(string sha256)
    {
        ValidateHash(sha256);
        return $"{SettingsDirectoryName}/sha256/{sha256[..2]}/{sha256[2..4]}/{sha256}.json";
    }

    public static string GetStagingRelativeDirectory() =>
        $"{SettingsDirectoryName}/{StagingDirectoryName}";

    public static bool TryResolvePublishedPath(
        string packageRoot,
        string relativePath,
        string expectedSha256,
        out string absolutePath)
    {
        absolutePath = string.Empty;
        if (string.IsNullOrWhiteSpace(packageRoot) || string.IsNullOrWhiteSpace(relativePath))
            return false;

        try
        {
            ValidateHash(expectedSha256);
            var expectedRelative = GetRelativePath(expectedSha256);
            if (!string.Equals(relativePath, expectedRelative, StringComparison.Ordinal))
                return false;

            // Database paths have one portable representation. Do not accept rooted,
            // alternate-separator, dot-segment, or platform-specific path syntax.
            if (relativePath.Contains('\\') || Path.IsPathRooted(relativePath)
                || relativePath.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
                return false;

            var root = Path.GetFullPath(packageRoot);
            var candidate = Path.GetFullPath(Path.Combine(root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!candidate.StartsWith(rootPrefix, comparison))
                return false;

            if (ContainsReparsePoint(root, candidate))
                return false;

            absolutePath = candidate;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ContainsReparsePoint(string root, string candidate)
    {
        var current = Path.GetDirectoryName(candidate);
        while (current is not null)
        {
            if (Directory.Exists(current)
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
            if (string.Equals(Path.TrimEndingDirectorySeparator(current),
                    Path.TrimEndingDirectorySeparator(root),
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
                return false;
            current = Path.GetDirectoryName(current);
        }

        // A package root that itself is a symlink/reparse point is not an allowed anchor.
        return Directory.Exists(root)
            && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0;
    }

    private static void ValidateHash(string hash)
    {
        if (hash is null || hash.Length != 64)
            throw new ArgumentException("Settings SHA-256 must contain 64 lowercase hexadecimal characters.", nameof(hash));
        foreach (var character in hash)
        {
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                throw new ArgumentException("Settings SHA-256 must contain 64 lowercase hexadecimal characters.", nameof(hash));
        }
    }
}
