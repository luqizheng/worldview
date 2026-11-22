using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// 最近项目元数据存取（应用级 UI 元数据，AF-01 §13 开放门槛的实现基线）。
/// 与正式世界数据严格分离；条目缺失不视为领域事实错误。
/// </summary>
public interface IRecentProjectsStore
{
    /// <summary>读取最近项目（最近使用的在前）。存储不可用时返回空列表，不抛异常。</summary>
    Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>记录一次成功打开（置顶并去重）。存储不可用时静默跳过。</summary>
    Task RecordOpenedAsync(string packagePath, DateTimeOffset openedAtUtc, CancellationToken cancellationToken = default);
}

/// <summary>最近项目条目。</summary>
public sealed record RecentProjectEntry
{
    /// <summary>包目录绝对路径。</summary>
    public required string PackagePath { get; init; }

    /// <summary>最近一次打开时间（UTC）。</summary>
    public required DateTimeOffset LastOpenedAtUtc { get; init; }
}
