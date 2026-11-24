using System;
using System.Collections.Generic;
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
/// WP-30B-01（F-1 关闭）：协调器关闭与最终发布决策之间的竞态修复。
/// 线性化点 = 协调器 CloseAsync 在发布器锁内置位关闭屏障
/// （SceneSnapshotPublisher.MarkCoordinatorClosed）；TryPublish 在同一锁内
/// 检查该屏障，因此"关闭 vs 发布决策"存在全序——本测试类分别锁定两个分支：
/// 关闭先至（拒绝 coordinator_closed，落定 RejectedClosed）与发布先至
/// （合法发布，close 后保持可读且不再接受后续发布）。
/// 全部使用 TaskCompletionSource 门控与程序序锚点制造确定性交错——绝无
/// Thread.Sleep、随机延迟或调度运气；所有等待带超时保护（仅真失败时触发，
/// 防止测试永久挂起）。装配替身经程序集内部抽象 ISceneAssembler 注入
/// （内部构造器），不依赖任何公共继承扩展点。
/// </summary>
public sealed class CoordinatorClosePublicationRaceTests
{
    /// <summary>确定性等待上限：只在真实失败（交错未按锚点发生）时触发。</summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 可门控装配替身（实现程序集内部抽象 ISceneAssembler）：进入 Assemble
    /// 时发信号并在放行前阻塞，随后**委托真实 SceneAssembler** 完成装配——
    /// 生产校验/装配语义不被替身绕过。程序序锚点——Assemble 只在协调器
    /// 完成门之后被调用，测试观察到信号即证明该请求已通过完成门（F-1 竞态
    /// 的"关闭前通过完成门"一侧由此代码顺序保证，而非时序运气）。
    /// </summary>
    private sealed class GatedSceneAssembler : ISceneAssembler
    {
        private readonly SceneAssembler _inner = new();
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();

        public SceneAssemblyResult Assemble(
            PresentationContext context,
            PresentationResult<BlankSceneContent> requiredBlankScene,
            IReadOnlyList<PresentationResult<SceneLayerDescriptor>>? explicitlyOptionalLayers,
            long candidateSnapshotVersion)
        {
            _entered.TrySetResult();
            if (!_release.Task.Wait(GateTimeout))
            {
                // 超时 = 确定性交错未按锚点发生（测试失败），不得静默继续装配。
                throw new TimeoutException("装配门未在超时内放行——确定性交错锚点未满足。");
            }

            return _inner.Assemble(context, requiredBlankScene, explicitlyOptionalLayers, candidateSnapshotVersion);
        }
    }

    /// <summary>可门控准备替身（本测试类专用的最小版本）：按序号挂起请求。</summary>
    private sealed class GatePreparation
    {
        public sealed class Gate
        {
            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private readonly object _lock = new();
        private readonly Dictionary<long, Gate> _gates = new();

        public Gate GateFor(long sequence)
        {
            lock (_lock)
            {
                if (!_gates.TryGetValue(sequence, out var gate))
                {
                    gate = new Gate();
                    _gates[sequence] = gate;
                }

                return gate;
            }
        }

        public ScenePreparation Delegate => PrepareAsync;

        private async Task<ScenePreparationOutput> PrepareAsync(PresentationRequest request, CancellationToken cancellationToken)
        {
            Gate? gate;
            lock (_lock)
            {
                _gates.TryGetValue(request.Sequence, out gate);
            }

            if (gate is null)
            {
                return CompleteNormally(request);
            }

            gate.Entered.TrySetResult();
            await gate.Release.Task.WaitAsync(GateTimeout).ConfigureAwait(false);
            return CompleteNormally(request);
        }

        private static ScenePreparationOutput CompleteNormally(PresentationRequest request)
        {
            var content = PresentationResult<BlankSceneContent>.Completed(
                request.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
                new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
            return new ScenePreparationOutput(content, null);
        }
    }

    private static SceneSnapshot AssembleSnapshot(PresentationContext context, long version)
    {
        var assembly = new SceneAssembler().Assemble(
            context,
            PresentationResult<BlankSceneContent>.Completed(
                context.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
                new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1))),
            null,
            version);
        Assert.True(assembly.Success, assembly.Diagnostic);
        return assembly.Snapshot!;
    }

    /// <summary>
    /// 异步让出一次的准备替身：默认准备是同步完成的（Task.FromResult），
    /// 若无此让出，RequestSceneAsync 会同步执行到装配门内部而阻塞调用线程。
    /// 让出后，完成门→分配→装配的后续区段运行在线程池线程上，测试线程
    /// 才能以确定性时序关闭协调器。
    /// </summary>
    private static async Task<ScenePreparationOutput> YieldThenCompleteAsync(
        PresentationRequest request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var content = new BlankSceneContent(new BlankBackgroundItem(
            SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1));
        var result = PresentationResult<BlankSceneContent>.Completed(
            request.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene, content);
        return new ScenePreparationOutput(result, null);
    }

    [Fact]
    public async Task CloseAfterCompletionGate_BlocksFinalPublicationDecision_RejectedClosed()
    {
        // F-1 时序的确定性重建：请求通过完成门（程序序锚点证明）→ 协调器关闭
        // （发布器尚未释放——即原竞态窗口）→ 最终发布决策 → 必须 RejectedClosed。
        var publisher = new SceneSnapshotPublisher();
        var assembler = new GatedSceneAssembler();
        var coordinator = new PresentationRequestCoordinator(publisher, assembler, YieldThenCompleteAsync);
        var context = TestContexts.Create(viewportGeneration: 1);

        var task = coordinator.RequestSceneAsync(context);   // 在准备让出处返回
        await assembler.Entered.WaitAsync(GateTimeout);   // 已挂起在 Assemble 内 ⇒ 完成门已在 open 状态下通过

        Assert.False(publisher.IsDisposed);        // 原竞态窗口前提：发布器未释放
        Assert.True(publisher.VersionsAllocated >= 1);   // 版本已在关闭前分配（原 F-1 步骤）

        await coordinator.CloseAsync();            // 关闭生效（线性化点）
        Assert.True(coordinator.IsClosed);
        Assert.True(publisher.IsCoordinatorClosed);
        Assert.False(publisher.IsDisposed);        // 窗口内：仅关闭、尚未释放

        assembler.Release();
        var outcome = await task;

        Assert.Equal(PresentationRequestState.RejectedClosed, outcome.State);
        Assert.Contains(
            SceneSnapshotPublisher.RejectCoordinatorClosed,
            outcome.Diagnostic,
            StringComparison.Ordinal);
        Assert.Null(outcome.PublishedSnapshotVersion);
        Assert.Null(publisher.Current);            // 未发布任何内容
        Assert.Equal(0, publisher.PublishedCount);
        Assert.Equal(1, coordinator.Counters.RejectedClosed);
        Assert.Equal(1, publisher.RejectedPublicationCount);
    }

    [Fact]
    public async Task PublishCompletedBeforeClose_RemainsReadable_AllLaterPublicationRejected()
    {
        // 全序的另一分支：发布决策先于关闭（合法）；close 后一切后续发布拒绝。
        var publisher = new SceneSnapshotPublisher();
        var coordinator = new PresentationRequestCoordinator(publisher, new SceneAssembler());
        var context = TestContexts.Create(viewportGeneration: 1);

        var outcome = await coordinator.RequestSceneAsync(context);
        Assert.Equal(PresentationRequestState.Published, outcome.State);
        var publishedBeforeClose = publisher.Current;
        Assert.NotNull(publishedBeforeClose);

        // close 前合法分配候选（验证"close 后 Allocate 拒绝"之外的合法路径）。
        var candidate = AssembleSnapshot(context, publisher.AllocateCandidateVersion());

        await coordinator.CloseAsync();

        // 已发布快照保持可读（同一引用，PRA-01 §8），关闭不清空。
        Assert.Same(publishedBeforeClose, publisher.Current);

        // close 后的直接发布决策：coordinator_closed，当前快照不被改写。
        Assert.False(publisher.TryPublish(candidate, requestSequence: 1, out _, out var reason));
        Assert.Equal(SceneSnapshotPublisher.RejectCoordinatorClosed, reason);
        Assert.Same(publishedBeforeClose, publisher.Current);

        // close 后的新请求：RejectedClosed。
        var late = await coordinator.RequestSceneAsync(TestContexts.Create(viewportGeneration: 2));
        Assert.Equal(PresentationRequestState.RejectedClosed, late.State);
    }

    [Fact]
    public async Task CloseAsync_Twice_IsIdempotent_BarrierHolds()
    {
        var preparation = new GatePreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var context = TestContexts.Create(viewportGeneration: 1);

        // 先让一个请求完整发布（存在 last-known-good）。
        var published = await runtime.RequestSceneAsync(context);
        Assert.Equal(PresentationRequestState.Published, published.State);

        var gate2 = preparation.GateFor(2);
        var task2 = runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 2));
        await gate2.Entered.Task.WaitAsync(GateTimeout);

        await runtime.Coordinator.CloseAsync();
        await runtime.Coordinator.CloseAsync();   // 重复关闭必须安全（幂等）

        Assert.True(runtime.Coordinator.IsClosed);
        Assert.True(runtime.Publisher.IsCoordinatorClosed);
        Assert.False(runtime.Publisher.IsDisposed);   // 仅关闭：发布器未释放

        gate2.Release.TrySetResult();
        var outcome2 = await task2;
        Assert.Equal(PresentationRequestState.RejectedClosed, outcome2.State);

        var late = await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 3));
        Assert.Equal(PresentationRequestState.RejectedClosed, late.State);
        Assert.Equal(2, runtime.Coordinator.Counters.RejectedClosed);
        Assert.Equal(1, runtime.Publisher.PublishedCount);   // 重复关闭不重复触发拒绝路径之外的效果

        runtime.Publisher.Dispose();
    }

    [Fact]
    public async Task RuntimeDisposeAsync_Twice_IsIdempotent_LateCompletionRejectedClosed()
    {
        var preparation = new GatePreparation();
        var runtime = ViewportPresentationRuntime.Create(preparation.Delegate);
        var context = TestContexts.Create(viewportGeneration: 1);

        var gate1 = preparation.GateFor(1);
        var task1 = runtime.RequestSceneAsync(context);
        await gate1.Entered.Task.WaitAsync(GateTimeout);

        await runtime.DisposeAsync();
        await runtime.DisposeAsync();   // 重复 DisposeAsync 必须安全（幂等）

        Assert.True(runtime.Coordinator.IsClosed);
        Assert.True(runtime.Publisher.IsCoordinatorClosed);
        Assert.True(runtime.Publisher.IsDisposed);

        gate1.Release.TrySetResult();
        var outcome1 = await task1;
        Assert.Equal(PresentationRequestState.RejectedClosed, outcome1.State);
        Assert.Null(runtime.Publisher.Current);

        var late = await runtime.RequestSceneAsync(TestContexts.Create(viewportGeneration: 2));
        Assert.Equal(PresentationRequestState.RejectedClosed, late.State);
        Assert.Equal(2, runtime.Coordinator.Counters.RejectedClosed);
    }
}
