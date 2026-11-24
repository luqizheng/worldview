using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;

namespace WorldView.Presentation.Publication;

/// <summary>一次准备的输出：空白场景结果 + 显式可选图层结果（可为 null = 显式缺省）。</summary>
public sealed record ScenePreparationOutput(
    PresentationResult<BlankSceneContent> BlankScene,
    IReadOnlyList<PresentationResult<SceneLayerDescriptor>>? ExplicitlyOptionalLayers);

/// <summary>
/// 场景准备函数（每请求一次；可注入替身做确定性交错测试）。
/// 只接收不可变请求身份与协作取消令牌；绝不直接发布——发布决定权只在协调器/发布器。
/// </summary>
public delegate Task<ScenePreparationOutput> ScenePreparation(PresentationRequest request, CancellationToken cancellationToken);

/// <summary>
/// 每-视口的轻量级 latest-request-wins 请求协调器（PRA-01 契约 §7 / M1 Profile §6.2）。
///
/// 正确性由三重身份共同保证，取消只用于节省工作、绝不作为正确性保障：
///   1. 请求身份——新请求立即取代旧的未发布请求（旧请求晚完成一律 Superseded）；
///   2. 上下文指纹——发布前校验候选指纹等于期望指纹（语义切换不冒充）；
///   3. 生命周期状态——视口关闭/停机后一切结果不得发布。
///
/// 本类串行化同一视口的请求身份与发布决定；独立视口使用独立实例
/// （互不取代、互不覆盖）。不引入通用调度器、任务队列、worker pool、
/// 事件总线或跨视口协调服务（M1 Profile §6.2 明令禁止）。
/// </summary>
public sealed class PresentationRequestCoordinator : IAsyncDisposable
{
    /// <summary>诊断环形缓冲容量（有界，不随请求量增长）。</summary>
    public const int RecentOutcomeCapacity = 64;

    private readonly SceneSnapshotPublisher _publisher;
    private readonly SceneAssembler _assembler;
    private readonly ScenePreparation? _preparationOverride;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _closeCts = new();

    private PresentationRequest? _activeRequest;
    private long _lastSequence;
    private bool _closed;

    private long _requested;
    private long _rejectedClosed;
    private long _prepared;
    private long _published;
    private long _superseded;
    private long _cancelled;
    private long _preparationFailures;
    private long _assemblyRejections;
    private long _publicationRejections;

    private readonly PresentationRequestOutcome?[] _recentOutcomes = new PresentationRequestOutcome?[RecentOutcomeCapacity];
    private int _recentIndex;

    public PresentationRequestCoordinator(
        SceneSnapshotPublisher publisher,
        SceneAssembler assembler,
        ScenePreparation? preparationOverride = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
        _preparationOverride = preparationOverride;
    }

    /// <summary>所属发布器（每视口私有）。</summary>
    public SceneSnapshotPublisher Publisher => _publisher;

    /// <summary>协调器是否已关闭（关闭后拒绝新请求，未完成请求一律不得发布）。</summary>
    public bool IsClosed
    {
        get { lock (_gate) { return _closed; } }
    }

    /// <summary>诊断计数快照。</summary>
    public PresentationCoordinatorCounters Counters
    {
        get
        {
            lock (_gate)
            {
                return new PresentationCoordinatorCounters
                {
                    Requested = _requested,
                    RejectedClosed = _rejectedClosed,
                    Prepared = _prepared,
                    Published = _published,
                    Superseded = _superseded,
                    Cancelled = _cancelled,
                    PreparationFailures = _preparationFailures,
                    AssemblyRejections = _assemblyRejections,
                    PublicationRejections = _publicationRejections,
                };
            }
        }
    }

    /// <summary>最近请求结果（旧→新；有界环形缓冲副本）。</summary>
    public IReadOnlyList<PresentationRequestOutcome> RecentOutcomes
    {
        get
        {
            lock (_gate)
            {
                var ordered = new List<PresentationRequestOutcome>(RecentOutcomeCapacity);
                for (var i = 0; i < RecentOutcomeCapacity; i++)
                {
                    var index = (_recentIndex + i) % RecentOutcomeCapacity;
                    if (_recentOutcomes[index] is { } outcome)
                    {
                        ordered.Add(outcome);
                    }
                }

                return ordered;
            }
        }
    }

    /// <summary>
    /// 发起一次场景请求（latest-request-wins 的入口）：激活请求（取代旧的、
    /// 必要时清空语义切换前的旧快照）→ 运行准备 → 装配 → 发布校验 → 原子发布。
    /// 返回该请求的最终结果记录。
    /// </summary>
    public async Task<PresentationRequestOutcome> RequestSceneAsync(
        PresentationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        PresentationRequest? request = null;
        try
        {
            lock (_gate)
            {
                if (_closed || _publisher.IsDisposed)
                {
                    return Record(outcome: RejectedClosedOutcome(sequence: 0, context, "协调器已关闭或发布器已释放：拒绝新请求。"));
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    // 调用方在发起前已取消：不激活、不取代旧请求、不触碰快照状态。
                    return Record(new PresentationRequestOutcome
                    {
                        Sequence = 0,
                        FingerprintHex = context.Fingerprint.ToHex(),
                        MapId = context.MapId,
                        BranchId = context.BranchId,
                        HistoricalTimeCanonical = context.HistoricalTime.Canonical(),
                        State = PresentationRequestState.Cancelled,
                        Diagnostic = "请求发起时取消令牌已触发。",
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                    });
                }

                _lastSequence += 1;
                request = new PresentationRequest(
                    _lastSequence,
                    context,
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closeCts.Token));

                if (_activeRequest is { } previous)
                {
                    previous.MarkSuperseded();
                }

                _activeRequest = request;
                _publisher.ActivateRequest(request.Sequence, context.Fingerprint, context.SemanticIdentity);
                _requested += 1;
            }

            var outcome = await PrepareAndPublishAsync(request).ConfigureAwait(false);
            Record(outcome);
            return outcome;
        }
        finally
        {
            if (request is not null)
            {
                request.DisposeCancellationSource();
                lock (_gate)
                {
                    if (ReferenceEquals(_activeRequest, request))
                    {
                        _activeRequest = null;
                    }
                }
            }
        }
    }

    private async Task<PresentationRequestOutcome> PrepareAndPublishAsync(PresentationRequest request)
    {
        var outcomeBase = new PresentationRequestOutcome
        {
            Sequence = request.Sequence,
            FingerprintHex = request.Context.Fingerprint.ToHex(),
            MapId = request.Context.MapId,
            BranchId = request.Context.BranchId,
            HistoricalTimeCanonical = request.Context.HistoricalTime.Canonical(),
        };

        // 1. 准备（可注入；M1 默认 = 空白场景组合）。
        request.TransitionTo(PresentationRequestState.Preparing);
        ScenePreparationOutput? output;
        try
        {
            var preparation = _preparationOverride ?? DefaultBlankScenePreparationAsync;
            output = await preparation(request, request.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (request.IsSuperseded)
        {
            return outcomeBase with
            {
                State = PresentationRequestState.Superseded,
                Diagnostic = "准备期间被更新请求取代（协作取消生效）。",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            return outcomeBase with
            {
                State = PresentationRequestState.Cancelled,
                Diagnostic = "准备期间协作取消令牌触发。",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }
        catch (OperationCanceledException)
        {
            // 取消异常但没有任何活动取消源——按失败处理，不吞掉异常。
            return outcomeBase with
            {
                State = PresentationRequestState.Failed,
                Diagnostic = "准备函数在无活动取消源的情况下抛出 OperationCanceledException。",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception ex)
        {
            return outcomeBase with
            {
                State = PresentationRequestState.Failed,
                Diagnostic = $"准备阶段失败：{ex.Message}",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        if (output is null)
        {
            return outcomeBase with
            {
                State = PresentationRequestState.Failed,
                Diagnostic = "准备函数返回 null 输出。",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        // 2. 完成门：关闭/停机后一切迟到结果不得发布（正确性检查，不依赖取消生效）。
        bool closed;
        lock (_gate)
        {
            closed = _closed;
        }

        if (closed || _publisher.IsDisposed)
        {
            return RejectedClosedOutcome(request.Sequence, request.Context, "协调器已关闭或发布器已释放：迟到结果不得发布。");
        }

        if (request.IsSuperseded)
        {
            return outcomeBase with
            {
                State = PresentationRequestState.Superseded,
                Diagnostic = "准备完成前已被更新请求取代。",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        // 3. 装配（版本由发布器预分配）。
        request.TransitionTo(PresentationRequestState.Assembling);
        long candidateVersion;
        try
        {
            candidateVersion = _publisher.AllocateCandidateVersion();
        }
        catch (InvalidOperationException)
        {
            return RejectedClosedOutcome(request.Sequence, request.Context, "发布器已释放：候选版本分配被拒绝。");
        }

        var assembly = _assembler.Assemble(
            request.Context, output.BlankScene, output.ExplicitlyOptionalLayers, candidateVersion);
        if (!assembly.Success || assembly.Snapshot is null)
        {
            return outcomeBase with
            {
                State = PresentationRequestState.Failed,
                Diagnostic = $"装配被拒绝（{assembly.RejectionStage}）：{assembly.Diagnostic}",
                RejectionStage = assembly.RejectionStage,
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        // 4. 发布校验 + 原子交换（发布器是唯一发布权威）。
        request.TransitionTo(PresentationRequestState.Ready);
        if (!_publisher.TryPublish(assembly.Snapshot, request.Sequence, out var published, out var rejectionReason))
        {
            return outcomeBase with
            {
                State = PresentationRequestState.RejectedStale,
                Diagnostic = $"发布被拒绝（{rejectionReason}）。",
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        request.TransitionTo(PresentationRequestState.Published);
        return outcomeBase with
        {
            State = PresentationRequestState.Published,
            PublishedSnapshotVersion = published!.SnapshotVersion,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static PresentationRequestOutcome RejectedClosedOutcome(long sequence, PresentationContext context, string diagnostic)
        => new()
        {
            Sequence = sequence,
            FingerprintHex = context.Fingerprint.ToHex(),
            MapId = context.MapId,
            BranchId = context.BranchId,
            HistoricalTimeCanonical = context.HistoricalTime.Canonical(),
            State = PresentationRequestState.RejectedClosed,
            Diagnostic = diagnostic,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// M1 默认准备：为上下文组合中性空白场景结果（上下文印记在组合时盖上，
    /// 绝不事后改贴）。无任何 IO/Domain 查询。
    /// </summary>
    private static Task<ScenePreparationOutput> DefaultBlankScenePreparationAsync(
        PresentationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = new BlankSceneContent(new BlankBackgroundItem(
            SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1));
        var result = PresentationResult<BlankSceneContent>.Completed(
            request.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene, content);
        return Task.FromResult(new ScenePreparationOutput(result, ExplicitlyOptionalLayers: null));
    }

    /// <summary>
    /// 关闭：停止接受新请求、协作取消未完成请求；其后的迟到完成一律 RejectedClosed。
    /// 幂等。发布器释放归视口运行时（<see cref="ViewportPresentationRuntime"/>）所有。
    /// </summary>
    public Task CloseAsync()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return Task.CompletedTask;
            }

            _closed = true;
            _activeRequest?.TryCancel();
        }

        _closeCts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>IAsyncDisposable 等价于 CloseAsync 并释放内部取消源。</summary>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _closeCts.Dispose();
    }

    private PresentationRequestOutcome Record(PresentationRequestOutcome outcome)
    {
        lock (_gate)
        {
            _recentOutcomes[_recentIndex] = outcome;
            _recentIndex = (_recentIndex + 1) % RecentOutcomeCapacity;

            switch (outcome.State)
            {
                case PresentationRequestState.Published:
                    _prepared += 1;
                    _published += 1;
                    break;
                case PresentationRequestState.RejectedStale:
                    // 已完成准备与装配、在发布校验处被拒。
                    _prepared += 1;
                    _publicationRejections += 1;
                    break;
                case PresentationRequestState.Superseded:
                    _superseded += 1;
                    break;
                case PresentationRequestState.Cancelled:
                    _cancelled += 1;
                    break;
                case PresentationRequestState.Failed:
                    if (outcome.RejectionStage is not null)
                    {
                        _prepared += 1;
                        _assemblyRejections += 1;
                    }
                    else
                    {
                        _preparationFailures += 1;
                    }

                    break;
                case PresentationRequestState.RejectedClosed:
                    _rejectedClosed += 1;
                    break;
            }

            return outcome;
        }
    }
}
