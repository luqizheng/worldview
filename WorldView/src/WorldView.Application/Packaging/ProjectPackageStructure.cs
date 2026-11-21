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
    /// 判断 <paramref name="candidateRelativePath"/> 是否为安全的包内相对路径：
    /// 非空、非绝对、无 ".." 跨越、规范化后仍位于包根之内（拒绝符号化/遍历逃逸）。
    /// </summary>
    public static bool IsRelativePathConfinedToPackage(string packageRoot, string candidateRelativePath)
    {
        if (string.IsNullOrWhiteSpace(candidateRelativePath))
        {
            return false;
        }

        string candidate = candidateRelativePath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            return false;
        }

        // 显式拒绝任何 ".." 段与盘符/UNC 形式，再由规范化兜底。
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
}
