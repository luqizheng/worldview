using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Bootstrap;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Project 数据库的物理访问契约（Infrastructure 以 Microsoft.Data.Sqlite 实现）。
/// Application 层只通过本契约操作数据库；UI/ViewModel 永远不接触 SQL（AF-01 §7.2）。
///
/// 边界说明：本契约只覆盖 Application Bootstrap 拥有的元数据与迁移 ledger
/// （P2-09-13 §2：Application Bootstrap 仅拥有 schema_migration ledger、迁移排序
/// 与启动编排）。Core Domain 领域表由 Core Domain 后续工作包通过其自己的
/// 持久化契约实现，绝不经由本契约创建。
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

    /// <summary>读取迁移 ledger；ledger 表不存在时返回空列表并置 <paramref name="tableExists"/> 为 false。</summary>
    Task<System.Collections.Generic.IReadOnlyList<MigrationLedgerEntry>> ReadLedgerAsync(
        CancellationToken cancellationToken = default);

    /// <summary>迁移 ledger 表是否存在（区分“空 ledger”与“ledger 缺失”）。</summary>
    Task<bool> LedgerTableExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>确保迁移 ledger 表存在（新建库使用）。</summary>
    Task EnsureLedgerTableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在单个事务内执行模块迁移脚本并写入 ledger 行（P2-09-11 §7：迁移与 ledger 插入同事务）。
    /// 脚本失败时整体回滚并抛出异常。
    /// </summary>
    Task ExecuteMigrationAsync(ModuleMigration migration, CancellationToken cancellationToken = default);
}
