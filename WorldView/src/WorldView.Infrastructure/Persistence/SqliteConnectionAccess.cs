using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace WorldView.Infrastructure.Persistence;

/// <summary>
/// 单一 SQLite 连接的统一访问协调器（CD-01-F1 V1 并发访问模型）。
///
/// 规则（一个连接恰有一个协调器，所有使用该连接者共享同一协调器）：
/// - 任何对底层 <see cref="SqliteConnection"/> 的使用（包数据库自身操作、
///   Core Domain 适配器读写、迁移事务、测试数据注入）都必须在
///   <see cref="Execute{T}"/> 的临界区内**完整**执行——命令创建、执行、
///   读取器物化、事务 Begin/Commit/Rollback 全程持锁，串行访问连接；
/// - <see cref="Execute{T}"/> 基于 <see langword="lock"/>（Monitor），同线程可重入：
///   临界区内经 <see cref="WorldView.Infrastructure.Persistence.CoreDomain.SqliteCoreDomainDatabase"/>
///   的嵌套调用安全，多步读序列可组成单一临界区（InSnapshotScope）；
/// - <see cref="DisposeAsync"/> 在锁内完成状态翻转（之后的进入立即
///   <see cref="ObjectDisposedException"/> fail-closed），随后等待在飞操作
///   自然退出（在飞操作持有同一锁）再释放连接——不存在连接提前释放；
/// - 打开策略与 PRAGMA（foreign_keys=ON、busy_timeout=5000、Pool=False）
///   与 AF-02/CD-01 既定连接策略一致。
/// </summary>
internal sealed class SqliteConnectionAccess : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly string _databasePath;
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqliteConnectionAccess(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径缺失。", nameof(databasePath));
        }

        _databasePath = databasePath;
    }

    /// <summary>入口拒绝：已随会话关闭（区别于在飞操作撞上原生句柄释放的失控异常）。</summary>
    private ObjectDisposedException Disposed() =>
        new(nameof(SqliteConnectionAccess), "数据库访问协调器已随会话关闭，拒绝新的数据库操作。");

    /// <summary>打开连接（幂等；重复调用为空操作）。已释放后调用抛 <see cref="ObjectDisposedException"/>。</summary>
    public void Open()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw Disposed();
            }
            if (_connection is not null)
            {
                return;
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            };
            var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();

            using (var fk = connection.CreateCommand())
            {
                fk.CommandText = "PRAGMA foreign_keys = ON;";
                fk.ExecuteNonQuery();
            }

            using (var busy = connection.CreateCommand())
            {
                busy.CommandText = "PRAGMA busy_timeout = 5000;";
                busy.ExecuteNonQuery();
            }

            _connection = connection;
        }
    }

    /// <summary>
    /// 在统一临界区内执行一段完整的连接访问。未打开/已释放时抛
    /// <see cref="InvalidOperationException"/> / <see cref="ObjectDisposedException"/>；
    /// 事务必须整体置于本临界区内（Commit/Rollback 含在 action 内）。
    /// </summary>
    public T Execute<T>(Func<SqliteConnection, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_gate)
        {
            if (_disposed)
            {
                throw Disposed();
            }

            var connection = _connection
                ?? throw new InvalidOperationException("数据库未打开。");

            return action(connection);
        }
    }

    /// <summary><see cref="Execute{T}"/> 的无返回值形式。</summary>
    public void Execute(Action<SqliteConnection> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Execute<object?>(connection =>
        {
            action(connection);
            return null;
        });
    }

    /// <summary>
    /// 关闭协调器：锁内翻转状态（后续进入立即 fail-closed），锁外等待并释放连接
    /// （此刻在飞操作已持锁完成退出，新操作见不到连接）。幂等；并发关闭安全。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        SqliteConnection? connection;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            connection = _connection;
            _connection = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
