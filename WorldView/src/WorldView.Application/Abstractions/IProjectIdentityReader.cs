using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Core Domain 项目身份读取端口（AF-01 §3.3 验证第 4/5 步的支撑边界）。
///
/// 实现者应为 Core Domain 持久化工作包提供的正式读取服务；AF-02 阶段真实实现不存在，
/// 运行时组合根不注册本接口，打开流程在该门槛处 fail-closed 并报告阻塞项。
/// 测试替身仅限单元测试使用，不得作为真实集成证据（AF-02 §4.6）。
/// </summary>
public interface IProjectIdentityReader
{
    /// <summary>从已打开的 Project 数据库读取逻辑项目身份。</summary>
    Task<ProjectIdentityReadResult> ReadAsync(IPackageDatabase database, CancellationToken cancellationToken = default);
}

/// <summary>项目身份读取结果。</summary>
public sealed class ProjectIdentityReadResult
{
    private ProjectIdentityReadResult()
    {
    }

    /// <summary>是否成功读取到唯一项目身份。</summary>
    public bool Success { get; private init; }

    /// <summary>诊断类别（失败时）。</summary>
    public ProjectIdentityFailure Failure { get; private init; }

    /// <summary>诊断说明（失败时）。</summary>
    public string? Diagnostic { get; private init; }

    /// <summary>项目身份（成功时）。</summary>
    public ProjectIdentity? Identity { get; private init; }

    public static ProjectIdentityReadResult Ok(ProjectIdentity identity) => new()
    {
        Success = true,
        Identity = identity,
    };

    public static ProjectIdentityReadResult Failed(ProjectIdentityFailure failure, string diagnostic) => new()
    {
        Success = false,
        Failure = failure,
        Diagnostic = diagnostic,
    };
}

/// <summary>项目身份读取失败类别。</summary>
public enum ProjectIdentityFailure
{
    /// <summary>数据库中不存在项目身份（尚未经 Core Domain 创建流程建立）。</summary>
    NotEstablished,

    /// <summary>存在多于一个逻辑项目（违反一库一项目规则）。</summary>
    MultipleProjects,

    /// <summary>身份格式不可读/校验失败。</summary>
    Invalid,
}

/// <summary>Core Domain 拥有的项目身份（由 Core Domain 读取服务给出）。</summary>
public sealed record ProjectIdentity
{
    /// <summary>项目 ID（Core Domain 权威身份）。</summary>
    public required Guid ProjectId { get; init; }

    /// <summary>项目显示名。</summary>
    public required string DisplayName { get; init; }
}
