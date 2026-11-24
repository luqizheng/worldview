using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Publication;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Publication;

/// <summary>
/// 类别 E：latest-request-wins、last-known-good、语义切换、关闭/停机、取消与视口隔离。
/// 全部使用 TaskCompletionSource 门制造确定性交错——绝不用 Thread.Sleep。
/// </summary>
public sealed class PresentationRequestCoordinatorTests
{
    /// <summary>可编程的确定性准备替身：按请求序号设门/定行为。</summary>
    private sealed class TestPreparation
    {
        public sealed class RequestGate
        {
            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public GateAction Action { get; set; } = GateAction.CompleteNormally;
        }

        public enum GateAction
        {
            CompleteNormally,
            StampedWithMismatchedFingerprint,
            ThrowException,
            ThrowOperationCanceled,
            ReturnNull,
        }

        private readonly object _lock = new();
        private readonly Dictionary<long, RequestGate> _gates = new();

        /// <summary>为指定序号的请求设门（进入准备时挂起，直到 Release 放行）。</summary>
        public RequestGateAccessor Gate(long sequence)
        {
            lock (_lock)
            {
                if (!_gates.TryGetValue(sequence, out var gate))
                {
                    gate = new RequestGate();
                    _gates[sequence] = gate;
                }

                return new RequestGateAccessor(gate);
            }
        }

        public ScenePreparation Delegate => PrepareAsync;

        private async Task<ScenePreparationOutput> PrepareAsync(PresentationRequest request, CancellationToken cancellationToken)
        {
            RequestGate? gate;
            lock (_lock)
            {
                _gates.TryGetValue(request.Sequence, out gate);
            }

            if (gate is null)
            {
                // 无门 = 立即按正常行为完成。
                return Complete(request, GateAction.CompleteNormally);
            }

            gate.Entered.TrySetResult();
            await gate.Release.Task.ConfigureAwait(false);
            return Complete(request, gate.Action);
        }

        private static ScenePreparationOutput Complete(PresentationRequest request, GateAction action)
        {
            switch (action)
            {
                case GateAction.ThrowException:
                    throw new InvalidOperationException("preparation boom (deterministic)");
                case GateAction.ThrowOperationCanceled:
                    throw new OperationCanceledException();
                case GateAction.ReturnNull:
                    return null!;
                case GateAction.StampedWithMismatchedFingerprint:
                    // 印记为"另一个上下文"的结果 → 装配器按指纹不一致整体拒绝。
                    var wrong = PresentationResult<BlankSceneContent>.Completed(
                        ContextFingerprint.Compute(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }),
                        PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
                        new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
                    return new ScenePreparationOutput(wrong, null);
                default:
                    var content = PresentationResult<BlankSceneContent>.Completed(
                        request.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
                        new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
                    return new ScenePreparationOutput(content, null);
            }
        }

        public sealed class RequestGateAccessor
        {
            private readonly RequestGate _gate;
            public RequestGateAccessor(RequestGate gate) => _gate = gate;

            /// <summary>等待请求进入准备阶段（确定性交错锚点）。</summary>
            public Task WaitForEnteredAsync() => _gate.Entered.Task;

            public void MarkMismatchedStamp() => _gate.Action = GateAction.StampedWithMismatchedFingerprint;
            public void MarkThrow() => _gate.Action = GateAction.ThrowException;
            public void MarkThrowOperationCanceled() => _gate.Action = GateAction.ThrowOperationCanceled;
            public void MarkReturnNull() => _gate.Action = GateAction.ReturnNull;
            public void Release() => _gate.Release.TrySetResult();
        }
    }

    private static void AssertPublished(PresentationRequestOutcome outcome, PresentationContext context)
    {
        Assert.Equal(PresentationRequestState.Published, outcome.State);
        Assert.NotNull(outcome.PublishedSnapshotVersion);
        Assert.Equal(context.Fingerprint.ToHex(), outcome.FingerprintHex);
        Assert.Null(outcome.RejectionStage);
    }

    [Fact]
    public async Task FirstRequest_PublishesVersionOne()
    {
        var runtime = ViewportPresentationRuntime.Create();
        var context = TestContexts.Create(viewportGeneration: 1);

        var outcome = await runtime.RequestSceneAsync(context);

        AssertPublished(outcome, context);
        Assert.Equal(1, outcome.Sequence);
        Assert.Equal(1, outcome.PublishedSnapshotVersion);
        Assert.NotNull(runtime.Publisher.Current);
        Assert.Equal(context.Fingerprint, runtime.Publisher.Current!.Fingerprint);
        Assert.Single(runtime.Publisher.Current.Layers);
        Assert.Equal(1, runtime.Publisher.Current.Diagnostics.LayerCount);
        Assert.Equal(1, runtime.Publisher.Current.Diagnostics.ItemCount);
    }

    [Fact]
    public async Task SlowOldRequest_CannotOverwriteNewerRequest()
    {
        // E1/E2/E3：A 先发（挂起），B 后发并先完成，A 晚完成。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var contextA = TestContexts.Create(viewportGeneration: 1);
        var contextB = TestContexts.Create(viewportGeneration: 2);

        Assert.NotEqual(contextA.Fingerprint, contextB.Fingerprint);

        var gateA = preparation.Gate(1);   // 先设门再发起，保证请求必然挂起
        var taskA = runtime.RequestSceneAsync(contextA);
        await gateA.WaitForEnteredAsync();

        var taskB = runtime.RequestSceneAsync(contextB);   // 立即完成
        var outcomeB = await taskB;
        AssertPublished(outcomeB, contextB);
        Assert.Same(runtime.Publisher.Current, runtime.Publisher.Current);

        // A 晚完成：必须被拒绝，不得覆盖 B。
        preparation.Gate(1).Release();
        var outcomeA = await taskA;
        Assert.Equal(PresentationRequestState.Superseded, outcomeA.State);
        Assert.Null(outcomeA.PublishedSnapshotVersion);

        Assert.Equal(contextB.Fingerprint, runtime.Publisher.Current!.Fingerprint);
        Assert.Equal(contextB.ViewportGeneration, runtime.Publisher.Current.Context.ViewportGeneration);
        Assert.Equal(1, runtime.Publisher.PublishedCount);
        Assert.Equal(outcomeB.PublishedSnapshotVersion, runtime.Publisher.PublishedVersion);
    }

    [Fact]
    public async Task BuildFailure_KeepsSemanticLastKnownGood()
    {
        // E5：同语义上下文（仅视口代变化）中第 2 次构建失败 → 保留第 1 次快照。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var contextGen1 = TestContexts.Create(viewportGeneration: 1);
        var contextGen2 = TestContexts.Create(viewportGeneration: 2);

        var outcome1 = await runtime.RequestSceneAsync(contextGen1);
        AssertPublished(outcome1, contextGen1);
        var lastKnownGood = runtime.Publisher.Current;

        preparation.Gate(2).MarkThrow();
        var task2 = runtime.RequestSceneAsync(contextGen2);
        await preparation.Gate(2).WaitForEnteredAsync();
        preparation.Gate(2).Release();
        var outcome2 = await task2;

        Assert.Equal(PresentationRequestState.Failed, outcome2.State);
        Assert.Contains("preparation boom", outcome2.Diagnostic, StringComparison.Ordinal);
        // last-known-good（同语义）保留，未被失败破坏。
        Assert.Same(lastKnownGood, runtime.Publisher.Current);
        Assert.Equal(contextGen1.ViewportGeneration, runtime.Publisher.Current!.Context.ViewportGeneration);
    }

    [Fact]
    public async Task SemanticContextSwitch_OldSnapshotCannotMasqueradeAsNewContext()
    {
        // E6：切到另一张 Map → 旧快照在准备期间即被清空；新请求失败也不复活旧上下文。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var mapA = TestContexts.Create(mapId: TestContexts.MapA, viewportGeneration: 1);
        var mapB = TestContexts.Create(mapId: TestContexts.MapB, viewportGeneration: 1);

        var outcome1 = await runtime.RequestSceneAsync(mapA);
        AssertPublished(outcome1, mapA);
        Assert.NotNull(runtime.Publisher.Current);

        preparation.Gate(2).MarkMismatchedStamp();
        var taskB = runtime.RequestSceneAsync(mapB);
        await preparation.Gate(2).WaitForEnteredAsync();

        // 语义切换已在激活时原子清空：准备期间读者看到 null，不是 A 的旧场景。
        Assert.Null(runtime.Publisher.Current);
        Assert.Equal(1, runtime.Publisher.ContextSwitchClearCount);

        preparation.Gate(2).Release();
        var outcomeB = await taskB;
        Assert.Equal(PresentationRequestState.Failed, outcomeB.State);
        Assert.Equal("required_input_context_mismatch", outcomeB.RejectionStage);

        // 失败后：旧上下文不复活，读者看到 null（WP-30B 可显示加载态）。
        Assert.Null(runtime.Publisher.Current);

        // 切回 MapA：新请求成功后恢复该语义的有效场景。
        var mapA2 = TestContexts.Create(mapId: TestContexts.MapA, viewportGeneration: 2);
        var outcome3 = await runtime.RequestSceneAsync(mapA2);
        AssertPublished(outcome3, mapA2);
        Assert.Equal(mapA2.Fingerprint, runtime.Publisher.Current!.Fingerprint);
    }

    [Fact]
    public async Task CloseWithInFlightRequest_LateCompletionCannotPublish()
    {
        // E7：视口关闭后，迟到的完成一律 RejectedClosed。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var context = TestContexts.Create(viewportGeneration: 1);

        var gate1 = preparation.Gate(1);   // 先设门再发起
        var task1 = runtime.RequestSceneAsync(context);
        await gate1.WaitForEnteredAsync();

        await runtime.Coordinator.CloseAsync();

        gate1.Release();
        var outcome1 = await task1;
        Assert.Equal(PresentationRequestState.RejectedClosed, outcome1.State);
        Assert.Null(runtime.Publisher.Current);   // 未发布任何内容

        // 关闭后新请求同样拒绝。
        var outcome2 = await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 2));
        Assert.Equal(PresentationRequestState.RejectedClosed, outcome2.State);
        Assert.True(runtime.Coordinator.IsClosed);
    }

    [Fact]
    public async Task Shutdown_RejectsEverything_NotYetCompleted()
    {
        // E8：停机（Dispose）后所有未完成/新请求均不能发布。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);

        var context = TestContexts.Create(viewportGeneration: 1);
        var gate1 = preparation.Gate(1);   // 先设门再发起
        var task1 = runtime.RequestSceneAsync(context);
        await gate1.WaitForEnteredAsync();

        await runtime.DisposeAsync();

        gate1.Release();
        var outcome1 = await task1;
        Assert.Equal(PresentationRequestState.RejectedClosed, outcome1.State);

        var outcome2 = await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 2));
        Assert.Equal(PresentationRequestState.RejectedClosed, outcome2.State);
        Assert.True(runtime.Publisher.IsDisposed);
        Assert.Null(runtime.Publisher.Current);
    }

    [Fact]
    public async Task CancellationNotHonoredByWorker_CorrectnessStillHolds()
    {
        // E9：超替触发协作取消，但准备函数忽略取消令牌并正常完成——
        // 正确性不依赖取消：仍按请求身份拒绝。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var contextGen1 = TestContexts.Create(viewportGeneration: 1);
        var contextGen2 = TestContexts.Create(viewportGeneration: 2);

        var gate1 = preparation.Gate(1);   // 先设门再发起
        var task1 = runtime.RequestSceneAsync(contextGen1);
        await gate1.WaitForEnteredAsync();

        var task2 = runtime.RequestSceneAsync(contextGen2);   // 取代 1 并触发其取消令牌
        await task2;
        AssertPublished(task2.Result, contextGen2);

        // 门照常放行（准备函数无视已触发的取消令牌、结果照常产出）：
        // 正确性由请求身份保证——结果为 Superseded，不发布。
        gate1.Release();
        var outcome1 = await task1;
        Assert.Equal(PresentationRequestState.Superseded, outcome1.State);
        Assert.Null(outcome1.PublishedSnapshotVersion);
        Assert.Equal(contextGen2.Fingerprint, runtime.Publisher.Current!.Fingerprint);
    }

    [Fact]
    public async Task CallerCancellationMidFlight_YieldsCancelledOutcome_NoPublish()
    {
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var context = TestContexts.Create(viewportGeneration: 1);
        using var callerCts = new CancellationTokenSource();

        preparation.Gate(1).MarkThrowOperationCanceled();
        var task1 = runtime.RequestSceneAsync(context, callerCts.Token);
        await preparation.Gate(1).WaitForEnteredAsync();

        callerCts.Cancel();
        preparation.Gate(1).Release();
        var outcome1 = await task1;

        Assert.Equal(PresentationRequestState.Cancelled, outcome1.State);
        Assert.Null(runtime.Publisher.Current);
    }

    [Fact]
    public async Task PreCancelledCallerToken_DoesNotActivateOrSupersede()
    {
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var contextGen1 = TestContexts.Create(viewportGeneration: 1);
        var contextGen2 = TestContexts.Create(viewportGeneration: 2);

        await runtime.RequestSceneAsync(contextGen1);
        var preCancelled = new CancellationToken(canceled: true);

        var outcome = await runtime.RequestSceneAsync(contextGen2, preCancelled);

        Assert.Equal(PresentationRequestState.Cancelled, outcome.State);
        // 未激活：当前快照仍是 gen1 的 last-known-good。
        Assert.Equal(contextGen1.Fingerprint, runtime.Publisher.Current!.Fingerprint);
    }

    [Fact]
    public async Task RapidGenerationChanges_OnlyLatestValidResultPublishes()
    {
        // E10：gen1..4 全部挂起，gen5 立即完成并发布；随后依次放行 1..4 全部被拒。
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);

        var contexts = Enumerable.Range(1, 5)
            .Select(gen => TestContexts.Create(viewportGeneration: gen))
            .ToArray();

        var tasks = new List<Task<PresentationRequestOutcome>>();
        for (var gen = 1; gen <= 4; gen++)
        {
            var gate = preparation.Gate(gen);   // 先设门再发起
            tasks.Add(runtime.RequestSceneAsync(contexts[gen - 1]));
            await gate.WaitForEnteredAsync();
        }

        var outcome5 = await runtime.RequestSceneAsync(contexts[4]);
        AssertPublished(outcome5, contexts[4]);
        Assert.Equal(5, outcome5.Sequence);

        for (var gen = 4; gen >= 1; gen--)
        {
            preparation.Gate(gen).Release();
            var outcome = await tasks[gen - 1];
            Assert.Equal(PresentationRequestState.Superseded, outcome.State);
        }

        Assert.Equal(contexts[4].Fingerprint, runtime.Publisher.Current!.Fingerprint);
        Assert.Equal(5, runtime.Publisher.Current.Context.ViewportGeneration);
        Assert.Equal(1, runtime.Publisher.PublishedCount);
    }

    [Fact]
    public async Task IndependentViewports_DoNotSupersedeOrOverwriteEachOther()
    {
        var preparation1 = new TestPreparation();
        var preparation2 = new TestPreparation();
        var runtime1 = ViewportPresentationRuntime.Create(preparation1.Delegate);
        var runtime2 = ViewportPresentationRuntime.Create(preparation2.Delegate);

        var contextV1 = TestContexts.Create(viewportGeneration: 1);
        var contextV2 = TestContexts.Create(viewportGeneration: 1);

        var gateV1 = preparation1.Gate(1);   // 先设门再发起（确定性交错）
        var taskV1 = runtime1.RequestSceneAsync(contextV1);
        await gateV1.WaitForEnteredAsync();

        // 视口 2 完成发布，不影响视口 1 的挂起请求。
        var outcomeV2 = await runtime2.RequestSceneAsync(contextV2);
        AssertPublished(outcomeV2, contextV2);
        Assert.Null(runtime1.Publisher.Current);

        preparation1.Gate(1).Release();
        var outcomeV1 = await taskV1;
        AssertPublished(outcomeV1, contextV1);

        // 各自发布各自的上下文；版本空间独立。
        Assert.Equal(contextV1.Fingerprint, runtime1.Publisher.Current!.Fingerprint);
        Assert.Equal(contextV2.Fingerprint, runtime2.Publisher.Current!.Fingerprint);
        Assert.Equal(1, runtime1.Publisher.PublishedVersion);
        Assert.Equal(1, runtime2.Publisher.PublishedVersion);
        Assert.Equal(1, runtime1.Coordinator.Counters.Requested);
        Assert.Equal(1, runtime2.Coordinator.Counters.Requested);
    }

    [Fact]
    public async Task Diagnostics_CountersAndBoundedRingBufferReflectOutcomes()
    {
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);

        // 3 发布 + 1 失败 + 1 超替。
        await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 1));
        await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 2));

        preparation.Gate(3).MarkReturnNull();
        var task3 = runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 3));
        await preparation.Gate(3).WaitForEnteredAsync();
        preparation.Gate(3).Release();
        var outcome3 = await task3;
        Assert.Equal(PresentationRequestState.Failed, outcome3.State);

        preparation.Gate(4);
        var task4 = runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 4));
        await preparation.Gate(4).WaitForEnteredAsync();
        var task5 = runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 5));
        await task5;
        preparation.Gate(4).Release();
        var outcome4 = await task4;
        Assert.Equal(PresentationRequestState.Superseded, outcome4.State);

        var counters = runtime.Coordinator.Counters;
        Assert.Equal(5, counters.Requested);
        Assert.Equal(3, counters.Published);
        Assert.Equal(1, counters.Superseded);
        Assert.Equal(1, counters.PreparationFailures);
        Assert.Equal(0, counters.RejectedClosed);

        // 环形缓冲有界且保序。注意：请求 4 被 5 取代，其结果在 5 发布后才落定
        // （4 晚完成）——环形缓冲按结果落定顺序记录 [1,2,3,5,4]，恰好证明
        // 晚完成的 4 没有覆盖 5。
        var recent = runtime.Coordinator.RecentOutcomes;
        Assert.Equal(5, recent.Count);
        Assert.Equal(new[] { 1L, 2, 3, 5, 4 }, recent.Select(o => o.Sequence).ToArray());

        // 容量不增长：灌入超过容量的请求后仍只保留最近 64 条。
        for (var gen = 10; gen < 10 + PresentationRequestCoordinator.RecentOutcomeCapacity + 10; gen++)
        {
            await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: gen));
        }

        Assert.Equal(PresentationRequestCoordinator.RecentOutcomeCapacity, runtime.Coordinator.RecentOutcomes.Count);
        Assert.True(runtime.Coordinator.Counters.Published >= PresentationRequestCoordinator.RecentOutcomeCapacity + 10);
    }

    [Fact]
    public async Task AssemblyRejection_ClassifiedWithStage()
    {
        var preparation = new TestPreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var context = TestContexts.Create(viewportGeneration: 1);

        preparation.Gate(1).MarkMismatchedStamp();
        var task1 = runtime.RequestSceneAsync(context);
        await preparation.Gate(1).WaitForEnteredAsync();
        preparation.Gate(1).Release();
        var outcome = await task1;

        Assert.Equal(PresentationRequestState.Failed, outcome.State);
        Assert.Equal("required_input_context_mismatch", outcome.RejectionStage);
        Assert.Null(runtime.Publisher.Current);
        Assert.Equal(1, runtime.Coordinator.Counters.AssemblyRejections);
    }

    [Fact]
    public async Task Outcomes_CarryContextIdentity()
    {
        var runtime = ViewportPresentationRuntime.Create();
        var context = TestContexts.Create(
            branchId: TestContexts.BranchA,
            historicalTime: new WorldView.Domain.Time.HistoricalTime(1300, 43200));

        var outcome = await runtime.RequestSceneAsync(context);

        Assert.Equal(1, outcome.Sequence);
        Assert.Equal(64, outcome.FingerprintHex.Length);
        Assert.Equal(TestContexts.MapA, outcome.MapId);
        Assert.Equal(TestContexts.BranchA, outcome.BranchId);
        Assert.Equal("1300:43200", outcome.HistoricalTimeCanonical);
        Assert.NotNull(outcome.CompletedAtUtc);
    }
}
