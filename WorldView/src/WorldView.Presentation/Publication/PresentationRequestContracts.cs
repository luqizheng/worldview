using System;
using System.Threading;
using WorldView.Presentation.Identity;

namespace WorldView.Presentation.Publication;

/// <summary>
/// 请求生命周期状态（PRA-01 契约 §7.1；这些是呈现请求生命周期状态，
/// 不是 Core Domain 实体状态）。
/// 主路径：Created → Preparing → Assembling → Ready → Published。
/// 终态替代：Cancelled / Superseded / Failed / RejectedStale / RejectedClosed
/// （RejectedClosed 为本实现扩展：协调器关闭或发布器释放后的到达拒绝）。
/// </summary>
public enum PresentationRequestState
{
    Created,
    Preparing,
    Assembling,
    Ready,
    Published,

    /// <summary>被协作取消（取消只省功，正确性不依赖取消）。</summary>
    Cancelled,

    /// <summary>被更新的请求取代（即使晚完成也不得发布）。</summary>
    Superseded,

    /// <summary>准备/装配失败（保留 last-known-good）。</summary>
    Failed,

    /// <summary>发布校验拒绝（指纹不一致/已被取代/版本非单调等）。</summary>
    RejectedStale,

    /// <summary>视口关闭或停机后到达（一切迟到结果不得发布）。</summary>
    RejectedClosed,
}

/// <summary>
/// 一次场景请求的不可变身份（视口内单调序号 + 已捕获的不可变上下文 +
/// 请求取消令牌）。准备函数只读取本对象——不得读取可变视图状态或查询
/// 动态 UI 状态；只有协调器/发布器可决定发布。
/// </summary>
public sealed class PresentationRequest
{
    private int _state = (int)PresentationRequestState.Created;
    private bool _superseded;

    internal PresentationRequest(long sequence, PresentationContext context, CancellationTokenSource cancellationSource)
    {
        Sequence = sequence;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        CancellationSource = cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource));
        CancellationToken = cancellationSource.Token;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>视口内单调请求序号（从 1 开始）。</summary>
    public long Sequence { get; }

    /// <summary>请求创建时捕获的不可变上下文（含指纹与语义身份）。</summary>
    public PresentationContext Context { get; }

    /// <summary>上下文指纹（便捷属性）。</summary>
    public ContextFingerprint Fingerprint => Context.Fingerprint;

    /// <summary>协作取消令牌（超替/关闭时触发；正确性不依赖它生效）。</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>请求创建时刻（诊断）。</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    internal CancellationTokenSource CancellationSource { get; }

    /// <summary>当前生命周期状态（诊断视图；终态不可逆）。</summary>
    public PresentationRequestState State => (PresentationRequestState)Volatile.Read(ref _state);

    /// <summary>是否已被更新请求取代（决定性正确性检查之一）。</summary>
    public bool IsSuperseded => Volatile.Read(ref _superseded);

    internal void MarkSuperseded()
    {
        Volatile.Write(ref _superseded, true);
        TransitionTo(PresentationRequestState.Superseded);
        TryCancel();
    }

    internal void TransitionTo(PresentationRequestState newState)
    {
        var current = (PresentationRequestState)Volatile.Read(ref _state);
        if (IsTerminal(current) || current == newState)
        {
            return;
        }

        Volatile.Write(ref _state, (int)newState);
    }

    internal void TryCancel()
    {
        try
        {
            CancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已释放：无需再取消。
        }
    }

    internal void DisposeCancellationSource()
    {
        CancellationSource.Dispose();
    }

    internal static bool IsTerminal(PresentationRequestState state)
        => state is PresentationRequestState.Published
            or PresentationRequestState.Cancelled
            or PresentationRequestState.Superseded
            or PresentationRequestState.Failed
            or PresentationRequestState.RejectedStale
            or PresentationRequestState.RejectedClosed;
}

/// <summary>
/// 请求结果记录（不可变；进入协调器诊断环形缓冲）。
/// </summary>
public sealed record PresentationRequestOutcome
{
    /// <summary>视口内请求序号。</summary>
    public long Sequence { get; init; }

    /// <summary>上下文指纹（十六进制）。</summary>
    public string FingerprintHex { get; init; } = string.Empty;

    /// <summary>语义身份（MapId）。</summary>
    public Guid MapId { get; init; }

    /// <summary>语义身份（BranchId；null = 根历史）。</summary>
    public Guid? BranchId { get; init; }

    /// <summary>语义身份（规范历史时间文本）。</summary>
    public string HistoricalTimeCanonical { get; init; } = string.Empty;

    /// <summary>最终生命周期状态。</summary>
    public PresentationRequestState State { get; init; }

    /// <summary>诊断说明（失败/拒绝时）。</summary>
    public string? Diagnostic { get; init; }

    /// <summary>装配拒绝阶段（仅装配被拒时非 null；用于诊断分类）。</summary>
    public string? RejectionStage { get; init; }

    /// <summary>发布成功的快照版本（仅 Published）。</summary>
    public long? PublishedSnapshotVersion { get; init; }

    /// <summary>结果记录时刻（诊断；不参与指纹）。</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }
}

/// <summary>协调器诊断计数快照（PRA-01 契约 §9）。</summary>
public sealed record PresentationCoordinatorCounters
{
    /// <summary>已受理的请求数。</summary>
    public long Requested { get; init; }

    /// <summary>关闭/停机后到达而被拒绝的请求数。</summary>
    public long RejectedClosed { get; init; }

    /// <summary>准备成功完成的请求数。</summary>
    public long Prepared { get; init; }

    /// <summary>发布成功的快照数。</summary>
    public long Published { get; init; }

    /// <summary>被超替的请求数（以落定的结果为准）。</summary>
    public long Superseded { get; init; }

    /// <summary>被协作取消的请求数。</summary>
    public long Cancelled { get; init; }

    /// <summary>准备阶段失败的请求数。</summary>
    public long PreparationFailures { get; init; }

    /// <summary>装配被拒绝的次数。</summary>
    public long AssemblyRejections { get; init; }

    /// <summary>发布校验被拒绝的候选数。</summary>
    public long PublicationRejections { get; init; }
}
