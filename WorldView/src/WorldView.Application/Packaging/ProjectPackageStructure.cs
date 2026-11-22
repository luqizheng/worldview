using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WorldView.Application.Packaging;

/// <summary>包内规范路径与校验辅助。</summary>
public static class ProjectPackageStructure
{
    /// <summary>manifest.json 绝对路径。</summary>
    public static string ManifestPath(string packageRoot) => Path.Combine(packageRoot, ProjectPackageFormat.ManifestFileName);

    /// <summary>按 manifest 声明解析数据库绝对路径。</summary>
    public static string DatabasePath(string packageRoot, PackageManifest manifest)
        => Path.Combine(packageRoot, manifest.DatabaseRelativePath);

    /// <summary>Asset Store 内容寻址根目录。</summary>
    public static string AssetHashRoot(string packageRoot)
        => Path.Combine(packageRoot, ProjectPackageFormat.AssetsDirectoryName, ProjectPackageFormat.AssetHashDirectoryName);

    /// <summary>Staging 目录。</summary>
    public static string StagingRoot(string packageRoot) => Path.Combine(packageRoot, ProjectPackageFormat.StagingDirectoryName);

    /// <summary>Backups 目录。</summary>
    public static string BackupsRoot(string packageRoot) => Path.Combine(packageRoot, ProjectPackageFormat.BackupsDirectoryName);

    /// <summary>若名称缺省包后缀则补上。</summary>
    public static string WithPackageSuffix(string packageName)
        => packageName.EndsWith(ProjectPackageFormat.PackageSuffix, StringComparison.OrdinalIgnoreCase)
            ? packageName
            : packageName + ProjectPackageFormat.PackageSuffix;

    /// <summary>
    /// 规范化包路径：全路径 + 去尾部分隔符。跨会话/注册表比较使用
    /// <see cref="Comparer"/>（Windows 大小写不敏感，其余平台敏感）。
    /// </summary>
    public static string NormalizePackagePath(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>包路径比较语义（Windows 大小写不敏感，其余平台敏感）。</summary>
    public static StringComparison PathComparison { get; } =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>包路径比较器（与 NormalizePackagePath/PathComparison 配套）。</summary>
    public static StringComparer Comparer { get; } =
        PathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// 判断 <paramref name="candidateRelativePath"/> 是否为安全的包内相对路径。
    /// 同时识别当前 OS 与 Windows 的 rooted/drive/UNC 语法，避免跨平台导入时
    /// 把 Windows 绝对路径当成普通文件名。
    /// </summary>
    public static bool IsRelativePathConfinedToPackage(string packageRoot, string candidateRelativePath)
    {
        if (string.IsNullOrWhiteSpace(candidateRelativePath))
        {
            return false;
        }

        string candidate = candidateRelativePath.Trim();
        if (Path.IsPathRooted(candidate) || IsWindowsRootedPath(candidate))
        {
            return false;
        }

        // 显式拒绝任何 ".." 段，再由规范化兜底。
        if (candidate.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var combined = Path.GetFullPath(Path.Combine(packageRoot, candidate));
            var root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            return combined.StartsWith(root, PathComparison);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    private static bool IsWindowsRootedPath(string path)
    {
        // Drive-qualified paths (both C:\\foo and drive-relative C:foo) are never
        // valid package-relative paths, regardless of the host operating system.
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            return true;
        }

        // Windows root-relative and UNC paths use backslashes, which Unix Path APIs
        // do not consider directory separators.
        return path[0] == '\\' || path[0] == '/';
    }
}
