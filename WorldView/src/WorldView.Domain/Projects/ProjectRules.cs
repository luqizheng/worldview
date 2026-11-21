using System;
using System.Collections.Generic;

namespace WorldView.Domain.Projects;

/// <summary>
/// Project 身份规则（Core Domain 独占）：创建计划与单行身份校验。
/// Project 是 ID 分配命名空间（P2-09-11 §2 决策 4）；创建只写 core_project 行，
/// 不自动创建 Map（无 ADR 授权的"初始地图"业务规则，空地图列表是合法状态）。
/// </summary>
public static class ProjectRules
{
    /// <summary>CD-01 写入的项目格式版本。</summary>
    public const long CurrentProjectFormatVersion = 1;

    /// <summary>规范化项目名：非空白，去首尾空白；违反时抛 ArgumentException。</summary>
    public static string NormalizeName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("项目名不能为空白。", nameof(displayName));
        }

        return displayName.Trim();
    }

    /// <summary>
    /// 创建项目身份：生成新的权威 UUID（存储形态为 canonical 小写连字符文本，由持久化适配器负责）。
    /// </summary>
    public static ProjectRecord CreateProject(string displayName, long nowUtcMs)
    {
        var name = NormalizeName(displayName);
        return new ProjectRecord
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = name,
            ProjectFormatVersion = CurrentProjectFormatVersion,
            CreatedAtUtcMs = nowUtcMs,
            UpdatedAtUtcMs = nowUtcMs,
        };
    }

    /// <summary>
    /// 校验 core_project 原始行集合：恰好一行且 UUID 可解析 → Ok；
    /// 零行 → NotEstablished；多行 → MultipleProjects；格式非法 → Invalid。
    /// </summary>
    public static (ProjectIdentityOutcome Outcome, ProjectRecord? Record) ValidateIdentityRows(
        IReadOnlyList<RawProjectIdentityRow> rows)
    {
        if (rows.Count == 0)
        {
            return (ProjectIdentityOutcome.NotEstablished, null);
        }

        if (rows.Count > 1)
        {
            return (ProjectIdentityOutcome.MultipleProjects, null);
        }

        var row = rows[0];
        if (!Guid.TryParseExact(row.ProjectIdText, "D", out var projectId))
        {
            return (ProjectIdentityOutcome.Invalid, null);
        }

        if (string.IsNullOrWhiteSpace(row.ProjectName) || row.ProjectFormatVersion < 1)
        {
            return (ProjectIdentityOutcome.Invalid, null);
        }

        return (ProjectIdentityOutcome.Ok, new ProjectRecord
        {
            ProjectId = projectId,
            ProjectName = row.ProjectName.Trim(),
            ProjectFormatVersion = row.ProjectFormatVersion,
            CreatedAtUtcMs = row.CreatedAtUtcMs,
            UpdatedAtUtcMs = row.UpdatedAtUtcMs,
        });
    }
}
