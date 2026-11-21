namespace WorldView.Application.Packaging;

/// <summary>包校验问题代码（AF-01 §3.3 分阶段校验 / §9 失败类别）。</summary>
public enum PackageIssueCode
{
    // ---- 包形状 ----
    PackageSuffixMissing,
    PackageDirectoryMissing,

    // ---- manifest ----
    ManifestMissing,
    ManifestMalformed,
    ManifestUnsupportedVersion,
    ManifestPathAbsolute,
    ManifestPathTraversal,
    ManifestPathOutsidePackage,

    // ---- 数据库 ----
    DatabaseMissing,
    DatabaseCorrupt,

    // ---- Bootstrap 元数据 / 迁移 ledger ----
    BootstrapMetadataMissing,
    DatabaseFormatUnsupported,
    LedgerMissing,
    LedgerUnknownMigration,
    LedgerChecksumMismatch,

    // ---- Asset Store ----
    AssetStoreRootMissing,

    // ---- Core Domain 身份 ----
    ProjectIdentityUnavailable,
    ProjectIdentityNotEstablished,
    ProjectIdentityMultiple,
    ProjectIdentityInvalid,

    // ---- 其他 ----
    Cancelled,
    InternalError,
}

/// <summary>问题类别：Failure 阻止打开；DependencyGate 是已登记的领域实现门槛（不影响包本身有效性）。</summary>
public enum PackageIssueCategory
{
    Failure,

    /// <summary>
    /// 已登记依赖门槛（如 Core Domain 项目身份读取服务未实现）。属于如实报告，
    /// 不是包本身损坏；验收口径为 PASS WITH CONDITIONS 的依据。
    /// </summary>
    DependencyGate,
}

/// <summary>单条校验问题。</summary>
public sealed record PackageIssue
{
    public required PackageIssueCode Code { get; init; }

    public required PackageIssueCategory Category { get; init; }

    /// <summary>发现问题的校验阶段（AF-01 §3.3 顺序）。</summary>
    public required string Stage { get; init; }

    /// <summary>面向诊断的说明（详细异常进日志，不进 UI）。</summary>
    public required string Message { get; init; }

    public static PackageIssue Failure(PackageIssueCode code, string stage, string message) => new()
    {
        Code = code,
        Category = PackageIssueCategory.Failure,
        Stage = stage,
        Message = message,
    };

    public static PackageIssue DependencyGate(PackageIssueCode code, string stage, string message) => new()
    {
        Code = code,
        Category = PackageIssueCategory.DependencyGate,
        Stage = stage,
        Message = message,
    };
}

/// <summary>分阶段校验报告。HasFailures 才阻止建立 Project Session；DependencyGate 单独如实报告。</summary>
public sealed class PackageValidationReport
{
    private readonly List<PackageIssue> _issues = new();

    public void Add(PackageIssue issue) => _issues.Add(issue);

    public IReadOnlyList<PackageIssue> Issues => _issues;

    public bool HasFailures => _issues.Exists(i => i.Category == PackageIssueCategory.Failure);

    public bool HasDependencyGates => _issues.Exists(i => i.Category == PackageIssueCategory.DependencyGate);

    /// <summary>首个 Failure 的代码（无则 null）。</summary>
    public PackageIssueCode? FirstFailureCode
        => _issues.Find(i => i.Category == PackageIssueCategory.Failure)?.Code;

    /// <summary>面向用户的一句话摘要（诊断详情进日志）。</summary>
    public string ToSummary()
    {
        if (_issues.Count == 0)
        {
            return "包校验通过。";
        }

        var first = _issues[0];
        return first.Category switch
        {
            PackageIssueCategory.Failure => $"包校验失败：{first.Message}",
            _ => $"存在依赖门槛：{first.Message}",
        };
    }
}
