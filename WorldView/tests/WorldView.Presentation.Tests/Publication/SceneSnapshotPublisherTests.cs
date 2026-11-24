using System;
using System.Threading.Tasks;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Publication;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Publication;

/// <summary>类别 D：发布器原子发布、单调版本、旧版本拒绝、原子读取、关闭/停机拒绝、语义切换清空。</summary>
public sealed class SceneSnapshotPublisherTests
{
    private static SceneSnapshot AssembleSnapshot(PresentationContext context, long version)
    {
        var assembler = new SceneAssembler();
        var contentResult = PresentationResult<BlankSceneContent>.Completed(
            context.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
            new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
        var assembly = assembler.Assemble(context, contentResult, null, version);
        Assert.True(assembly.Success, assembly.Diagnostic);
        return assembly.Snapshot!;
    }

    [Fact]
    public void FirstPublish_ReplacesNullCurrent_AndPublishes()
    {
        var publisher = new SceneSnapshotPublisher();
        var context = TestContexts.Create();

        Assert.Null(publisher.Current);

        publisher.ActivateRequest(1, context.Fingerprint, context.SemanticIdentity);
        var snapshot = AssembleSnapshot(context, publisher.AllocateCandidateVersion());
        Assert.True(publisher.TryPublish(snapshot, requestSequence: 1, out var published, out var rejection));
        Assert.Null(rejection);
        Assert.Same(snapshot, published);
        Assert.Same(snapshot, publisher.Current);
        Assert.Equal(snapshot.SnapshotVersion, publisher.PublishedVersion);
        Assert.Equal(1, publisher.PublishedCount);
    }

    [Fact]
    public void ReaderCapturesReferenceOnce_ReferenceRemainsValidAndConsistent()
    {
        var publisher = new SceneSnapshotPublisher();
        var contextA = TestContexts.Create(viewportGeneration: 1);
        var contextB = TestContexts.Create(viewportGeneration: 2);

        publisher.ActivateRequest(1, contextA.Fingerprint, contextA.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(contextA, publisher.AllocateCandidateVersion()), 1, out _, out _));

        // 读者捕获一次引用（PRA-01 §6.4），本地持有。
        var captured = publisher.Current;
        Assert.NotNull(captured);
        Assert.Equal(contextA.Fingerprint, captured!.Fingerprint);

        publisher.ActivateRequest(2, contextB.Fingerprint, contextB.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(contextB, publisher.AllocateCandidateVersion()), 2, out _, out _));

        // 捕获的旧引用不被后续发布改写——整次读取使用同一引用，内容自洽完整。
        Assert.Equal(contextA.Fingerprint, captured.Fingerprint);
        Assert.Equal(contextA.Fingerprint, captured.Context.Fingerprint);
        Assert.Single(captured.Layers);
        Assert.Equal(SceneLayerIds.Background, captured.Layers[0].LayerId);

        // Current 已原子切换为新快照（捕获者不受影响）。
        Assert.NotSame(captured, publisher.Current);
        Assert.Equal(contextB.Fingerprint, publisher.Current!.Fingerprint);
    }

    [Fact]
    public void SnapshotVersion_StrictlyIncreases()
    {
        var publisher = new SceneSnapshotPublisher();
        var context = TestContexts.Create(viewportGeneration: 1);
        publisher.ActivateRequest(1, context.Fingerprint, context.SemanticIdentity);

        var v1 = publisher.AllocateCandidateVersion();
        Assert.True(publisher.TryPublish(AssembleSnapshot(context, v1), 1, out _, out _));
        Assert.Equal(1, publisher.PublishedVersion);

        publisher.ActivateRequest(2, context.Fingerprint, context.SemanticIdentity);
        var v2 = publisher.AllocateCandidateVersion();
        Assert.True(v2 > v1);
        Assert.True(publisher.TryPublish(AssembleSnapshot(context, v2), 2, out _, out _));
        Assert.Equal(2, publisher.PublishedVersion);
    }

    [Fact]
    public void OldOrEqualVersionCandidate_IsRejected_LastKnownGoodKept()
    {
        var publisher = new SceneSnapshotPublisher();
        var contextGen1 = TestContexts.Create(viewportGeneration: 1);
        var contextGen2 = TestContexts.Create(viewportGeneration: 2);

        publisher.ActivateRequest(1, contextGen1.Fingerprint, contextGen1.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(contextGen1, publisher.AllocateCandidateVersion()), 1, out _, out _));
        Assert.Equal(1, publisher.PublishedVersion);

        publisher.ActivateRequest(2, contextGen2.Fingerprint, contextGen2.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(contextGen2, publisher.AllocateCandidateVersion()), 2, out _, out _));

        // 版本检查在指纹检查之后：用"期望指纹 + 过旧版本"的候选触发版本拒绝。
        publisher.ActivateRequest(3, contextGen2.Fingerprint, contextGen2.SemanticIdentity);
        var staleVersionSnapshot = AssembleSnapshot(contextGen2, version: 1);
        Assert.False(publisher.TryPublish(staleVersionSnapshot, 3, out _, out var reason));
        Assert.Equal("non_monotonic_version", reason);
        Assert.NotSame(staleVersionSnapshot, publisher.Current);
        Assert.Equal(contextGen2.ViewportGeneration, publisher.Current!.Context.ViewportGeneration);
        Assert.Equal(1, publisher.RejectedPublicationCount);
    }

    [Fact]
    public void WrongContextCandidate_IsRejected()
    {
        var publisher = new SceneSnapshotPublisher();
        var contextA = TestContexts.Create(viewportGeneration: 1);
        var contextB = TestContexts.Create(viewportGeneration: 2);

        publisher.ActivateRequest(1, contextA.Fingerprint, contextA.SemanticIdentity);
        Assert.False(publisher.TryPublish(AssembleSnapshot(contextB, publisher.AllocateCandidateVersion()), 1, out _, out var reason));
        Assert.Equal("context_mismatch", reason);
        Assert.Null(publisher.Current);
    }

    [Fact]
    public void SupersededRequestCandidate_IsRejected()
    {
        var publisher = new SceneSnapshotPublisher();
        var contextA = TestContexts.Create(viewportGeneration: 1);
        var contextB = TestContexts.Create(viewportGeneration: 2);

        publisher.ActivateRequest(1, contextA.Fingerprint, contextA.SemanticIdentity);
        publisher.ActivateRequest(2, contextB.Fingerprint, contextB.SemanticIdentity);

        // 序号检查在指纹检查之后：用"期望指纹 + 旧请求序号"触发序号拒绝（旧请求晚到）。
        Assert.False(publisher.TryPublish(AssembleSnapshot(contextB, publisher.AllocateCandidateVersion()), 1, out _, out var reason));
        Assert.Equal("stale_request", reason);
        Assert.Null(publisher.Current);
    }

    [Fact]
    public void SemanticContextSwitch_ClearsCurrentAtomically()
    {
        var publisher = new SceneSnapshotPublisher();
        var mapA = TestContexts.Create(mapId: TestContexts.MapA, viewportGeneration: 1);
        var mapB = TestContexts.Create(mapId: TestContexts.MapB, viewportGeneration: 1);

        publisher.ActivateRequest(1, mapA.Fingerprint, mapA.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(mapA, publisher.AllocateCandidateVersion()), 1, out _, out _));
        Assert.NotNull(publisher.Current);
        Assert.Equal(0, publisher.ContextSwitchClearCount);

        // 语义切换（另一张 Map）：激活即原子清空——旧上下文场景不得冒充新语义结果。
        publisher.ActivateRequest(2, mapB.Fingerprint, mapB.SemanticIdentity);
        Assert.Null(publisher.Current);
        Assert.Equal(1, publisher.ContextSwitchClearCount);
    }

    [Fact]
    public void SameSemanticRefresh_RetainsLastKnownGood()
    {
        var publisher = new SceneSnapshotPublisher();
        var gen1 = TestContexts.Create(viewportGeneration: 1);
        var gen2 = TestContexts.Create(viewportGeneration: 2);

        publisher.ActivateRequest(1, gen1.Fingerprint, gen1.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(gen1, publisher.AllocateCandidateVersion()), 1, out _, out _));

        // 同语义刷新（仅视口代变化）：不清空，last-known-good 保留。
        publisher.ActivateRequest(2, gen2.Fingerprint, gen2.SemanticIdentity);
        Assert.NotNull(publisher.Current);
        Assert.Equal(0, publisher.ContextSwitchClearCount);
    }

    [Fact]
    public void TimeOnlySemanticChange_SameMapBranch_IsSemanticSwitch()
    {
        var publisher = new SceneSnapshotPublisher();
        var time1 = TestContexts.Create(historicalTime: new WorldView.Domain.Time.HistoricalTime(1200, 0));
        var time2 = TestContexts.Create(historicalTime: new WorldView.Domain.Time.HistoricalTime(1300, 0));

        publisher.ActivateRequest(1, time1.Fingerprint, time1.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(time1, publisher.AllocateCandidateVersion()), 1, out _, out _));

        // 同一 Map/Branch、不同历史时间：语义切换 → 清空。
        publisher.ActivateRequest(2, time2.Fingerprint, time2.SemanticIdentity);
        Assert.Null(publisher.Current);
        Assert.Equal(1, publisher.ContextSwitchClearCount);
    }

    [Fact]
    public void Dispose_RejectsAllFurtherPublication_ButCurrentStaysReadable()
    {
        var publisher = new SceneSnapshotPublisher();
        var context = TestContexts.Create();

        publisher.ActivateRequest(1, context.Fingerprint, context.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(context, publisher.AllocateCandidateVersion()), 1, out _, out _));
        var current = publisher.Current;

        publisher.Dispose();
        Assert.True(publisher.IsDisposed);

        Assert.Throws<InvalidOperationException>(() => publisher.AllocateCandidateVersion());
        Assert.False(publisher.TryPublish(AssembleSnapshot(context, 99), requestSequence: 1, out _, out var reason));
        Assert.Equal("publisher_disposed", reason);
        // 已发布快照的逻辑数据在被引用期间保持有效（PRA-01 §8）。
        Assert.Same(current, publisher.Current);
        Assert.Single(current!.Layers);
    }

    [Fact]
    public void ActivateRequest_AfterDispose_IsIgnored_NoClearNoPublish()
    {
        var publisher = new SceneSnapshotPublisher();
        var context = TestContexts.Create();
        // 预先在释放前构建候选（释放后版本分配被拒绝）。
        var candidate = AssembleSnapshot(context, 1);

        publisher.Dispose();
        publisher.ActivateRequest(1, context.Fingerprint, context.SemanticIdentity);
        Assert.False(publisher.TryPublish(candidate, requestSequence: 1, out _, out var reason));
        Assert.Equal("publisher_disposed", reason);
        Assert.Null(publisher.Current);
    }

    [Fact]
    public async Task ConcurrentReaders_AlwaysObserveCompleteOldOrNewSnapshot()
    {
        var publisher = new SceneSnapshotPublisher();
        var contextA = TestContexts.Create(viewportGeneration: 1);
        var contextB = TestContexts.Create(viewportGeneration: 2);

        publisher.ActivateRequest(1, contextA.Fingerprint, contextA.SemanticIdentity);
        Assert.True(publisher.TryPublish(AssembleSnapshot(contextA, publisher.AllocateCandidateVersion()), 1, out _, out _));

        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerMismatches = 0;
        var readerTask = Task.Run(async () =>
        {
            while (!stop.Task.IsCompleted)
            {
                // 每轮捕获一次引用，并验证引用内自洽（指纹一致、结构完整）。
                var snapshot = publisher.Current;
                if (snapshot is not null)
                {
                    var expectedFingerprint = snapshot.Context.Fingerprint;
                    if (snapshot.Fingerprint.ToHex() != expectedFingerprint.ToHex() || snapshot.Layers.Count != 1)
                    {
                        Interlocked.Add(ref readerMismatches, 1);
                    }
                }

                // 步进节流（非时序依赖；自洽性不变量与交错顺序无关）。
                await Task.Delay(1);
            }
        });

        publisher.ActivateRequest(2, contextB.Fingerprint, contextB.SemanticIdentity);
        for (var i = 0; i < 200; i++)
        {
            publisher.TryPublish(AssembleSnapshot(contextB, publisher.AllocateCandidateVersion()), 2, out _, out _);
            await Task.Yield();
        }

        stop.SetResult();
        await readerTask;
        Assert.Equal(0, readerMismatches);
        Assert.True(publisher.PublishedCount >= 1);
    }
}
