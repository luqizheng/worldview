using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Bootstrap;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Project 数据库的物理访问契约（Infrastructure 以 Microsoft.Data.Sqlite 实现）。
/// Application 层只通过本契约操作数据库；UI/ViewModel 永远不接触 SQL（AF-01 §7.2）。
///
/// 边界说明：本契约只覆盖包级元数据与 SQLite 完整性检查。Core Domain Schema
/// 完全由 Infrastructure 的 EF Core Migrations 拥有。
/// </summary>
public interface IPackageDatabase : IAsyncDisposable
{
    /// <summary>数据库文件路径（package 内相对路径已解析后的绝对路径）。</summary>
    string DatabasePath { get; }

    /// <summary>
    /// 打开连接并启用外键约束。文件不存在时由实现决定是否创建
    /// （Microsoft.Data.Sqlite 打开即创建空文件）。
    /// </summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>SQLite 快速完整性检查（PRAGMA quick_check）。库损坏时返回 false。</summary>
    Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>读取 Bootstrap 元数据；元数据表不存在时返回 null（由编排层判为 fail-closed 问题）。</summary>
    Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>创建（若缺失）元数据表并写入元数据。</summary>
    Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default);


}
