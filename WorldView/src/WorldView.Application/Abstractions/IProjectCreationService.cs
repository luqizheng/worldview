using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Core Domain 项目创建服务端口（AF-01 §3.4 第 6 步 / AF-02 §4.3 第 5 步）。
/// 项目身份必须经正式领域边界创建；Application 不允许手工插入 Project 行或
/// 造出替代 schema。
///
/// AF-02 阶段真实实现不存在：CreateProject 在 bootstrap 边界停止并报告阻塞项
/// （AF-01 §3.4）。测试替身仅限单元测试，用于驱动发布/清理路径。
/// </summary>
public interface IProjectCreationService
{
    /// <summary>
    /// 在指定包数据库内创建项目身份并建立初始领域结构。
    /// 实现方负责其自身的数据写入事务边界。
    /// </summary>
    Task<ProjectCreationResult> CreateAsync(ProjectCreationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>项目创建请求。</summary>
public sealed record ProjectCreationRequest
{
    /// <summary>包根目录（绝对路径）。</summary>
    public required string PackageRootPath { get; init; }

    /// <summary>包内数据库相对路径。</summary>
    public required string DatabaseRelativePath { get; init; }

    /// <summary>项目显示名（用户输入）。</summary>
    public required string DisplayName { get; init; }
}

/// <summary>项目创建结果。</summary>
public sealed class ProjectCreationResult
{
    private ProjectCreationResult()
    {
    }

    public bool Success { get; private init; }

    /// <summary>诊断说明（失败时）。</summary>
    public string? Diagnostic { get; private init; }

    /// <summary>创建出的项目身份（成功时）。</summary>
    public ProjectIdentity? Identity { get; private init; }

    public static ProjectCreationResult Ok(ProjectIdentity identity) => new()
    {
        Success = true,
        Identity = identity,
    };

    public static ProjectCreationResult Failed(string diagnostic) => new()
    {
        Success = false,
        Diagnostic = diagnostic,
    };
}
