using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using WorldView.Application.Packaging;
using System.Threading.Tasks;

namespace WorldView.Application.Sessions;

/// <summary>
/// 进程内 Project Session 注册表（AF-01 §8.1）：
/// 阻止同一包在同一进程内被打开为两个独立可写会话（先到者占位，后来者收到重复打开结果）；
/// 每个会话的数据库/Asset Store 资源彼此隔离，注册表只管路径占用与关闭编排。
/// 无静态全局状态；实例由组合根注入。
/// </summary>
public sealed class ProjectSessionRegistry
{
    private readonly ConcurrentDictionary<string, ProjectSession> _openSessions =
        new(ProjectPackageStructure.Comparer);
    private volatile bool _shutdown;

    /// <summary>
    /// 占位或登记一个包路径。返回 false 表示该包已有会话（无论其处于建立中还是 Ready），
    /// 或应用已进入停机（不再接受新登记）。会话完全关闭后登记自动解除
    /// （<see cref="ProjectSession.SessionClosed"/>），同一包即可再次打开。
    /// </summary>
    public bool TryRegister(ProjectSession session)
    {
        if (_shutdown)
        {
            return false;
        }

        if (!_openSessions.TryAdd(session.CanonicalPackagePath, session))
        {
            return false;
        }

        session.SessionClosed += OnSessionClosed;
        return true;
    }

    private void OnSessionClosed(object? sender, EventArgs e)
    {
        if (sender is ProjectSession session)
        {
            Remove(session);
        }
    }

    /// <summary>按规范包路径查找已登记会话。</summary>
    public ProjectSession? FindByPackagePath(string canonicalPackagePath)
    {
        return _openSessions.TryGetValue(canonicalPackagePath, out var session) ? session : null;
    }

    /// <summary>移除登记（建立失败或关闭完成后），同时解除 SessionClosed 订阅。</summary>
    public bool Remove(ProjectSession session)
    {
        session.SessionClosed -= OnSessionClosed;
        return ((ICollection<KeyValuePair<string, ProjectSession>>)_openSessions)
            .Remove(new(session.CanonicalPackagePath, session));
    }

    /// <summary>全部已登记会话快照。</summary>
    public IReadOnlyList<ProjectSession> Sessions => _openSessions.Values.ToArray();

    /// <summary>
    /// 关闭全部会话（应用停机）：先停止接受新登记，再逐个关闭。
    /// 已占用的事务由各会话的关闭序列在释放数据库前自然完成（写协调不半途放弃）。
    /// </summary>
    public async Task CloseAllAsync(CancellationToken cancellationToken = default)
    {
        _shutdown = true;
        foreach (var session in _openSessions.Values)
        {
            await session.CloseAsync(cancellationToken).ConfigureAwait(false);
            // 只有真正关闭才解除占用；关闭失败保留登记，异常继续向上传递。
            if (session.State == ProjectSessionState.Closed)
            {
                Remove(session);
            }
        }
    }
}
