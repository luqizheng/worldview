using System;

namespace WorldView.Domain.Projects;

/// <summary>
/// Project 持久化行（core_project）：一个 V1 Project 数据库恰好一行
/// （P2-09-11 V0.11 §4.1；单行规则由 Core Domain 服务强制）。
/// </summary>
public sealed record ProjectRecord
{
    /// <summary>项目 ID（Core Domain 权威身份；存储为 canonical 小写连字符 UUID 文本）。</summary>
    public required Guid ProjectId { get; init; }

    /// <summary>项目显示名（非空白，去除首尾空白后保存）。</summary>
    public required string ProjectName { get; init; }

    /// <summary>项目格式版本（CD-01 写 1）。</summary>
    public required long ProjectFormatVersion { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>core_project 的原始行（ID 尚未解析；解析/校验规则归 Core Domain）。</summary>
public sealed record RawProjectIdentityRow
{
    public required string ProjectIdText { get; init; }

    public required string ProjectName { get; init; }

    public required long ProjectFormatVersion { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>项目身份读取校验结果类别（P2-09-11 §4.1 单行规则的领域形式）。</summary>
public enum ProjectIdentityOutcome
{
    /// <summary>恰好一行且身份格式合法。</summary>
    Ok,

    /// <summary>零行：数据库尚未经 Core Domain 创建流程建立项目身份。</summary>
    NotEstablished,

    /// <summary>多于一行：违反一库一项目规则，fail-closed。</summary>
    MultipleProjects,

    /// <summary>身份文本不可解析为 UUID 或名称非法。</summary>
    Invalid,
}
