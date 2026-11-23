using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Packaging;

namespace WorldView.Application.Bootstrap;

/// <summary>
/// Bootstrap 元数据：数据库格式版本与创建时间等 Application Bootstrap 拥有的元数据。
/// 不包含任何 Core Domain 项目身份或领域状态（manifest 不做第二身份权威，库内元数据同理）。
/// </summary>
public sealed record BootstrapMetadata
{
    /// <summary>数据库格式版本（当前支持 1）。</summary>
    public required int DatabaseFormatVersion { get; init; }

    /// <summary>数据库创建时间（UTC）。</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

/// <summary>
/// Application Bootstrap 编排结果。
/// </summary>
public sealed class BootstrapResult
{
    private readonly List<PackageIssue> _issues = new();

    public bool Success => _issues.Count == 0;

    public IReadOnlyList<PackageIssue> Issues => _issues;

    public static BootstrapResult Ok() => new();

    public static BootstrapResult Failed(IEnumerable<PackageIssue> issues)
    {
        var result = new BootstrapResult();
        result._issues.AddRange(issues);
        return result;
    }
}
