namespace WorldView.Application.Packaging;

/// <summary>
/// Project Package V1 布局常量（AF-01 §3.1）：
/// <code>
/// &lt;MyWorld&gt;.wvproject/
///   manifest.json
///   data/\n///     project.sqlite
///   Assets/sha256/
///   Staging/
///   Backups/
/// </code>
/// 目录式包使 SQLite 数据库与文件型不可变 Asset 可整体移动、备份与恢复。
/// </summary>
public static class ProjectPackageFormat
{
    /// <summary>包目录后缀。</summary>
    public const string PackageSuffix = ".wvproject";

    /// <summary>清单文件名（包元数据，不是第二项目身份权威）。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>数据库包内相对路径（v1 固定值，使用正斜杠）。</summary>
    public const string DatabaseFileName = "data/project.sqlite";

    /// <summary>Asset Store 根目录名。</summary>
    public const string AssetsDirectoryName = "Assets";

    /// <summary>Asset Store 内容寻址子目录名。</summary>
    public const string AssetHashDirectoryName = "sha256";

    /// <summary>暂存目录名（临时、非权威；可安全清理）。</summary>
    public const string StagingDirectoryName = "Staging";

    /// <summary>显式备份产物目录名（不是第二活动数据库）。</summary>
    public const string BackupsDirectoryName = "Backups";

    /// <summary>当前支持的包格式版本（manifest.packageFormatVersion）。</summary>
    public const int SupportedPackageFormatVersion = 1;

    /// <summary>当前支持的数据库格式版本（Bootstrap 元数据 database_format_version）。</summary>
    public const int SupportedDatabaseFormatVersion = 1;
}
