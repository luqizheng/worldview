using System;
using System.Threading;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Scene;

namespace WorldView.Presentation.Publication;

/// <summary>
/// 每-视口快照发布器（PRA-01 契约 §6）：发布正确性的唯一权威。
/// 拥有当前不可变快照引用、单调快照版本计数、活动请求序号、期望上下文
/// 指纹与诊断计数。发布路径单写者（协调器串行决定），读者无锁——
/// <see cref="Current"/> 是纯 Volatile.Read，渲染路径上无锁、无等待、无准备工作。
///
/// 生命周期：Dispose 后拒绝一切后续发布（迟到的候选不得复活状态）；
/// 已发布快照的逻辑数据在被引用期间保持有效（Dispose 不使其失效）。
/// 实例必须按视口私有，不得跨视口共享（视口隔离）。
/// </summary>
public sealed class SceneSnapshotPublisher : IDisposable
{
    /// <summary>发布拒绝原因码：发布器已释放（生命周期类，稳定字符串，测试锁定）。</summary>
    internal const string RejectPublisherDisposed = "publisher_disposed";

    /// <summary>发布拒绝原因码：协调器已关闭（生命周期类，F-1 修复引入，测试锁定）。</summary>
    internal const string RejectCoordinatorClosed = "coordinator_closed";

    private readonly object _gate = new();

    // 当前快照引用：写入为 Interlocked.Exchange（原子交换），读取为 Volatile.Read（无锁原子捕获）。
    private SceneSnapshot? _current;
    private long _lastAssignedVersion;
    private long _publishedVersion;
    private long _activeRequestSequence;
    private ContextFingerprint _desiredFingerprint;
    private WorkspaceSemanticIdentity _activeSemanticIdentity;
    private bool _hasActiveSemanticIdentity;
    private bool _coordinatorClosed;
    private bool _disposed;

    private long _versionsAllocated;
    private long _publishedCount;
    private long _rejectedCount;
    private long _contextSwitchClears;
    private string? _lastRejectionReason;

    /// <summary>当前快照（原子引用捕获；读者必须对整个读取/绘制过程复用同一引用）。未发布过时为 null。</summary>
    public SceneSnapshot? Current => Volatile.Read(ref _current);

    /// <summary>是否已释放（释放后拒绝一切发布与版本分配）。</summary>
    public bool IsDisposed
    {
        get { lock (_gate) { return _disposed; } }
    }

    /// <summary>
    /// 协调器是否已关闭（关闭屏障置位后拒绝一切后续发布与候选版本分配）。
    /// 与 <see cref="IsDisposed"/> 区分：关闭 ≠ 释放——已发布快照仍保持可读，
    /// 诊断上可分辨"协调器关闭"与"发布器已释放"两级拒绝。
    /// internal：仅程序集内（含测试程序集，经 InternalsVisibleTo）可见，
    /// 不构成公共 API（WP-30B Phase 1 F-1 修订 WP30B-F1-R1）。
    /// </summary>
    internal bool IsCoordinatorClosed
    {
        get { lock (_gate) { return _coordinatorClosed; } }
    }

    /// <summary>
    /// 关闭屏障（F-1 修复的线性化点）：协调器关闭时调用，在本发布器锁内置位。
    /// 与 <see cref="TryPublish"/> 在同一锁上串行化，形成全序——任何发布决策
    /// 要么先于本调用（发布在关闭生效前完成，合法），要么后于本调用
    /// （一律拒绝 <c>coordinator_closed</c>）。幂等。
    /// </summary>
    internal void MarkCoordinatorClosed()
    {
        lock (_gate)
        {
            _coordinatorClosed = true;
        }
    }

    /// <summary>最近一次发布成功的快照版本（未发布过为 0）。</summary>
    public long PublishedVersion
    {
        get { lock (_gate) { return _publishedVersion; } }
    }

    /// <summary>已分配的候选版本数（含被拒绝的候选——允许缺口）。</summary>
    public long VersionsAllocated
    {
        get { lock (_gate) { return _versionsAllocated; } }
    }

    /// <summary>发布成功次数。</summary>
    public long PublishedCount
    {
        get { lock (_gate) { return _publishedCount; } }
    }

    /// <summary>发布被拒绝的候选数。</summary>
    public long RejectedPublicationCount
    {
        get { lock (_gate) { return _rejectedCount; } }
    }

    /// <summary>语义上下文切换时的原子清空次数。</summary>
    public long ContextSwitchClearCount
    {
        get { lock (_gate) { return _contextSwitchClears; } }
    }

    /// <summary>最近一次发布拒绝原因（诊断）。</summary>
    public string? LastRejectionReason
    {
        get { lock (_gate) { return _lastRejectionReason; } }
    }

    /// <summary>
    /// 为已成功构建的候选分配快照版本（发布器是唯一版本权威）。
    /// 协调器关闭屏障置位或释放后抛出——协调器将其归类为 RejectedClosed。
    /// </summary>
    public long AllocateCandidateVersion()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("SceneSnapshotPublisher 已释放，不能再分配候选版本。");
            }

            if (_coordinatorClosed)
            {
                throw new InvalidOperationException("协调器已关闭，不能再分配候选版本。");
            }

            _lastAssignedVersion += 1;
            _versionsAllocated += 1;
            return _lastAssignedVersion;
        }
    }

    /// <summary>
    /// 协调器激活新请求时调用（协调器串行调用）：更新活动请求序号与期望指纹；
    /// 若语义身份（Map/Branch/RootHistory/HistoricalTime）相对上次激活发生变化，
    /// 原子清空当前快照——旧上下文的场景不得在新语义下冒充有效结果
    /// （M1 Profile §6.4；同语义刷新保留 last-known-good）。
    /// </summary>
    internal void ActivateRequest(long requestSequence, ContextFingerprint desiredFingerprint, WorkspaceSemanticIdentity semanticIdentity)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return; // 协调器随后会以 RejectedClosed 拒绝该请求。
            }

            _activeRequestSequence = requestSequence;
            _desiredFingerprint = desiredFingerprint;

            if (_hasActiveSemanticIdentity
                && !_activeSemanticIdentity.Equals(semanticIdentity)
                && Volatile.Read(ref _current) is not null)
            {
                Interlocked.Exchange(ref _current, null);
                _contextSwitchClears += 1;
            }

            _activeSemanticIdentity = semanticIdentity;
            _hasActiveSemanticIdentity = true;
        }
    }

    /// <summary>
    /// 发布校验与原子交换（PRA-01 契约 §6.2 六步 + 协调器关闭屏障扩展）：
    /// 发布器未释放、协调器未关闭、候选完整（装配器保证）、指纹等于期望、
    /// 请求序号仍是活动请求、版本严格大于已发布版本，全部通过才原子交换
    /// 引用；任一失败即整体拒绝并保留当前快照不变。
    /// 关闭状态在本锁内检查——最终发布决策与协调器关闭之间因此存在全序
    /// （F-1 修复：完成门预检查只是快速路径，正确性不再依赖它）。
    /// </summary>
    internal bool TryPublish(SceneSnapshot candidate, long requestSequence, out SceneSnapshot? published, out string? rejectionReason)
    {
        lock (_gate)
        {
            published = null;
            rejectionReason = null;

            if (_disposed)
            {
                rejectionReason = Reject(RejectPublisherDisposed);
                return false;
            }

            if (_coordinatorClosed)
            {
                rejectionReason = Reject(RejectCoordinatorClosed);
                return false;
            }

            if (candidate is null)
            {
                rejectionReason = Reject("candidate_missing");
                return false;
            }

            if (candidate.Fingerprint != _desiredFingerprint)
            {
                rejectionReason = Reject("context_mismatch");
                return false;
            }

            if (requestSequence != _activeRequestSequence)
            {
                rejectionReason = Reject("stale_request");
                return false;
            }

            if (candidate.SnapshotVersion <= _publishedVersion)
            {
                rejectionReason = Reject("non_monotonic_version");
                return false;
            }

            // 原子引用交换：单写者 + Interlocked.Exchange；读者 Volatile.Read 捕获一次引用后全程复用。
            Interlocked.Exchange(ref _current, candidate);
            _publishedVersion = candidate.SnapshotVersion;
            _publishedCount += 1;
            published = candidate;
            return true;
        }
    }

    /// <summary>记录拒绝原因并返回（诊断计数 + 最近拒绝原因）。</summary>
    private string Reject(string reason)
    {
        _rejectedCount += 1;
        _lastRejectionReason = reason;
        return reason;
    }

    /// <summary>
    /// 释放发布器：此后一切发布与版本分配被拒绝（迟到的候选不得复活状态）。
    /// 不清空当前快照——快照逻辑数据在被引用期间保持有效（PRA-01 §8）。
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}
