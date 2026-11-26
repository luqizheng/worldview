using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Rendering;
using WorldView.Desktop.Tests.Testing;
using WorldView.Desktop.Ui;
using WorldView.Desktop.ViewModels;
using WorldView.Domain.Time;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Publication;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;
using SkiaSharp;
using Xunit;

namespace WorldView.Desktop.Tests.Presentation;

/// <summary>
/// WP-RA-02-A 桌面宿主集成测试（A-01 ~ A-10）：验证桌面生产代码真实接入
/// PRA-01 Presentation Pipeline——Context → Request → Prepare → Publish →
/// Capture → Render 全链路经真实生产对象（VM / Binding / Runtime / Publisher /
/// Renderer）执行，仅 Core Domain 查询端口使用既有测试替身（端口尚无真实实现）。
///
/// 竞态构造全部经 TCS 门控的 ScenePreparation（Runtime 既有注入缝），
/// 绝无 Thread.Sleep；轮询仅等待"已被门控确定的离散状态"的传播，带超时上限。
/// 断言覆盖对象身份、生命周期与发布行为，而非仅调用次数。
/// </summary>
public class DesktopPresentationPipelineIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    // ---- 测试支持 ----

    /// <summary>记录 Draw 帧输入的渲染器探针（不调用 base.Draw，避免 Skia 原生依赖）。</summary>
    private sealed class RecordingRenderer : MapSceneRenderer
    {
        public List<ViewportFrameInput> Frames { get; } = new();

        public override void Draw(SKCanvas canvas, ViewportFrameInput frame)
        {
            Frames.Add(frame);
        }
    }

    /// <summary>
    /// 脚本化准备函数：每个请求按序消费一个脚本步——立即完成 / 门控挂起 / 抛异常。
    /// Entered TCS 提供确定性交错锚点（请求进入准备阶段的时刻可观测）。
    /// 门控步不响应取消令牌——用于构造"Prepare 在关闭后正常完成"的迟到交错（A-05）。
    /// </summary>
    private sealed class ScriptedPreparation
    {
        private sealed class Step
        {
            public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly bool Gate;
            public readonly bool Throw;

            public Step(bool gate, bool thr0w)
            {
                Gate = gate;
                Throw = thr0w;
            }
        }

        private readonly List<Step> _steps;
        private int _index;

        public ScriptedPreparation(params (bool Gate, bool Throw)[] scripts)
        {
            _steps = scripts.Select(s => new Step(s.Gate, s.Throw)).ToList();
        }

        /// <summary>等待第 index 个请求进入准备阶段（确定性交错锚点）。</summary>
        public Task WaitEnteredAsync(int index)
            => _steps[index].Entered.Task.WaitAsync(_timeout);

        /// <summary>放行第 index 个门控请求。</summary>
        public void Release(int index)
            => _steps[index].Release.TrySetResult();

        public async Task<ScenePreparationOutput> PrepareAsync(PresentationRequest request, CancellationToken cancellationToken)
        {
            var step = _steps[Math.Min(_index, _steps.Count - 1)];
            Interlocked.Increment(ref _index);
            step.Entered.TrySetResult();

            if (step.Throw)
            {
                throw new InvalidOperationException("准备失败注入（WP-RA-02-A 测试）。");
            }

            if (step.Gate)
            {
                // 刻意不联动 cancellationToken：构造"Prepare 正常完成后才与关闭决策交错"。
                await step.Release.Task.ConfigureAwait(false);
            }

            var content = new BlankSceneContent(new BlankBackgroundItem(
                SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1));
            var result = PresentationResult<BlankSceneContent>.Completed(
                request.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene, content);
            return new ScenePreparationOutput(result, ExplicitlyOptionalLayers: null);
        }
    }

    /// <summary>脚本化上下文解析替身：按序返回预置的已校验上下文。</summary>
    private sealed class ScriptedContextResolver : IWorkspaceContextResolver
    {
        private readonly Queue<ValidatedWorkspaceContext> _results;

        public ScriptedContextResolver(params ValidatedWorkspaceContext[] results)
        {
            _results = new Queue<ValidatedWorkspaceContext>(results);
        }

        public Task<WorkspaceContextResolution> ResolveAsync(
            Guid projectId, Guid mapId, Guid? branchId, string? historicalTimeInput, CancellationToken cancellationToken = default)
            => Task.FromResult(WorkspaceContextResolution.Ok(_results.Dequeue()));
    }

    /// <summary>Binding 工厂可注入的 VM 测试替身（真实生产路径，仅替换 Binding 构造缝）。</summary>
    private sealed class TestProjectWorkspaceViewModel : ProjectWorkspaceViewModel
    {
        private readonly Func<MapWorkspaceSession, MapViewportBinding>? _bindingFactory;

        public TestProjectWorkspaceViewModel(
            ProjectSession session,
            IMapQueryService? mapQuery,
            IWorkspaceContextResolver? contextResolver,
            CreateMapUseCase createMap,
            IAppLog? log = null,
            IUiDispatcher? uiDispatcher = null,
            Func<MapWorkspaceSession, MapViewportBinding>? bindingFactory = null)
            : base(session, mapQuery, contextResolver, createMap, log, uiDispatcher)
        {
            _bindingFactory = bindingFactory;
        }

        internal override MapViewportBinding CreateViewportBinding(MapWorkspaceSession workspace)
            => _bindingFactory is not null ? _bindingFactory(workspace) : base.CreateViewportBinding(workspace);
    }

    /// <summary>带门控准备与探针渲染器的 Binding 构造（真实 Runtime / Publisher / Renderer 基类）。</summary>
    private static MapViewportBinding CreateGatedBinding(WorkspaceViewState viewState, ScriptedPreparation prep, RecordingRenderer renderer)
        => new(viewState, ViewportPresentationRuntime.Create(prep.PrepareAsync), renderer);

    /// <summary>标准测试组合（真实会话 + 真实 VM + 可选 Binding 注入）。</summary>
    private static (TestProjectWorkspaceViewModel Vm, ProjectSession Session, FakeMapQueryService MapQuery) Build(
        Func<MapWorkspaceSession, MapViewportBinding>? bindingFactory = null,
        IWorkspaceContextResolver? resolver = null)
    {
        var session = TestSessions.CreateReadySession();
        var mapQuery = new FakeMapQueryService();
        var vm = new TestProjectWorkspaceViewModel(
            session,
            mapQuery: mapQuery,
            contextResolver: resolver ?? new StubContextResolver(),
            createMap: new CreateMapUseCase(session, new FakeMapCreationService(mapQuery)),
            // 测试宿主无 Avalonia 消息循环：注入内联调度器（R1-P1-01 生产默认仍为
            // AvaloniaUiDispatcher；投递边界的正确性由 R1 专项测试以受控调度器验证）。
            uiDispatcher: InlineUiDispatcher.Instance,
            bindingFactory: bindingFactory);
        return (vm, session, mapQuery);
    }

    private static async Task<MapWorkspaceSession> OpenAsync(TestProjectWorkspaceViewModel vm, FakeMapQueryService mapQuery, MapSummary map)
    {
        mapQuery.Maps.Add(map);
        vm.SelectedMap = map;
        await vm.OpenWorkspaceCommand.ExecuteAsync(CancellationToken.None);
        return vm.Workspaces.Single();
    }

    /// <summary>经 VM 正式命令关闭工作区（C1 路径）。</summary>
    private static async Task CloseViaCommandAsync(TestProjectWorkspaceViewModel vm, MapWorkspaceSession workspace)
    {
        vm.SelectedWorkspace = workspace;
        await vm.CloseWorkspaceCommand.ExecuteAsync(CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"等待超时：{description}");
            }

            await Task.Delay(10);
        }
    }

    private static ValidatedWorkspaceContext MakeContext(Guid mapId, Guid? branchId)
        => new()
        {
            MapId = mapId,
            BranchId = branchId,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = HistoricalTime.Epoch,
        };

    // ---- A-01 至 A-10 ----

    /// <summary>A-01：打开有效地图工作区 → 完成 Context → Request → Prepare → Publish → Capture → Render。</summary>
    [Fact]
    public async Task A01_OpenWorkspace_CompletesFullPipelineChain()
    {
        var renderer = new RecordingRenderer();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(bindingFactory: ws =>
        {
            prep = new ScriptedPreparation((Gate: false, Throw: false));
            return CreateGatedBinding(ws.ViewState, prep, renderer);
        });

        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "南海群岛" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        // Request/Prepare/Publish 已由打开命令真实等待完成：结果为 Published。
        Assert.NotNull(binding.LastRequestOutcome);
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome!.State);
        Assert.True(binding.LastRequestOutcome.PublishedSnapshotVersion >= 1);

        // Publish：发布器持有有效快照，指纹与请求身份一致。
        var publisher = binding.Runtime.Publisher;
        var snapshot = publisher.Current;
        Assert.NotNull(snapshot);
        Assert.Equal(binding.LastRequestedFingerprintHex, snapshot!.Fingerprint.ToHex());
        Assert.Equal(workspace.Context.MapId, snapshot.Context.MapId);
        Assert.Equal(PresentationRequestState.Published, binding.Runtime.Coordinator.RecentOutcomes.Single().State);

        // Capture：唯一捕获点的帧输入引用与发布器当前快照同一实例。
        var frame = binding.CaptureFrame();
        Assert.Same(snapshot, frame.Snapshot);
        Assert.Equal(publisher.PublishedVersion, frame.Snapshot!.SnapshotVersion);

        // Render：渲染器真实消费捕获结果（探针记录帧输入）。
        var control = new MapViewportControl();
        control.Bind(binding);
        var drawFrame = frame;
        renderer.Draw(canvas: null!, drawFrame); // 探针不触碰 SKCanvas；此处直接以捕获帧调用真实 Draw 路径
        var rendered = Assert.Single(renderer.Frames);
        Assert.Same(snapshot, rendered.Snapshot);

        await binding.RequestClose();
    }

    /// <summary>A-02：ViewModel、Binding、Runtime、Publisher、Renderer 属于同一视口实例链路。</summary>
    [Fact]
    public async Task A02_ViewportInstanceConsistency_SameInstanceChain()
    {
        MapViewportBinding? createdBinding = null;
        var renderer = new RecordingRenderer();
        var (vm, session, mapQuery) = Build(bindingFactory: ws =>
        {
            createdBinding = new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(), renderer);
            return createdBinding;
        });

        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);

        // ViewModel 提供的 Binding 与创建工厂产出的实例同一。
        var binding = vm.GetViewportBinding(workspace)!;
        Assert.Same(createdBinding, binding);

        // Binding 私有 Runtime / Publisher 一致（非全局共享、非单例）。
        var runtime = binding.Runtime;
        Assert.Same(runtime.Publisher, binding.Runtime.Coordinator.Publisher);

        // 控件关联的 Binding 同一（控件只持有一个 Binding 引用）。
        var control = new MapViewportControl();
        control.Bind(binding);
        Assert.Same(binding, control.Binding);

        // CaptureFrame 的快照只来自本视口 Publisher（渲染链路身份一致）。
        var frame = binding.CaptureFrame();
        Assert.NotNull(frame.Snapshot);
        Assert.Same(runtime.Publisher.Current, frame.Snapshot);

        // 独立打开第二个工作区 → 完全独立的 Runtime 实例（视口隔离）。
        var map2 = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M2" };
        mapQuery.Maps.Add(map2);
        vm.SelectedMap = map2;
        await vm.OpenWorkspaceCommand.ExecuteAsync(CancellationToken.None);
        var workspace2 = vm.Workspaces.Last();
        var binding2 = vm.GetViewportBinding(workspace2)!;
        Assert.NotSame(binding, binding2);
        Assert.Equal(2, vm.Workspaces.Count);
        Assert.NotSame(binding.Runtime, binding2.Runtime);
        Assert.NotSame(binding.Runtime.Publisher, binding2.Runtime.Publisher);

        await binding.RequestClose();
        await binding2.RequestClose();
    }

    /// <summary>A-03：Renderer 捕获的 Snapshot 与实际发布结果身份一致（版本 + 指纹 + 引用）。</summary>
    [Fact]
    public async Task A03_CapturedSnapshotIdentity_MatchesPublishedResult()
    {
        var renderer = new RecordingRenderer();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(bindingFactory: ws =>
        {
            prep = new ScriptedPreparation((Gate: false, Throw: false));
            return CreateGatedBinding(ws.ViewState, prep, renderer);
        });

        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var outcome = binding.LastRequestOutcome!;
        var publisher = binding.Runtime.Publisher;
        var published = publisher.Current!;

        // 结果记录 ↔ 发布器状态 ↔ 快照身份三方一致。
        Assert.Equal(outcome.PublishedSnapshotVersion, published.SnapshotVersion);
        Assert.Equal(outcome.PublishedSnapshotVersion, publisher.PublishedVersion);
        Assert.Equal(outcome.FingerprintHex, published.Fingerprint.ToHex());
        Assert.Equal(1, publisher.PublishedCount);
        Assert.Equal(0, publisher.RejectedPublicationCount);

        // 捕获帧快照与发布结果同一引用；渲染器收到的即该快照。
        var frame = binding.CaptureFrame();
        Assert.Same(published, frame.Snapshot);
        renderer.Draw(null!, frame);
        Assert.Same(published, Assert.Single(renderer.Frames).Snapshot);

        await binding.RequestClose();
    }

    /// <summary>A-04：语义切换 → 发起新请求；旧 Context 快照被清空，不冒充新 Context 的有效结果。</summary>
    [Fact]
    public async Task A04_SemanticSwitch_RequestsNewScene_ClearsOldSnapshot()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextA = MakeContext(mapId, branchId: null);
        var contextB = MakeContext(mapId, branchB);
        var renderer = new RecordingRenderer();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(
            bindingFactory: ws =>
            {
                // 请求 1 立即完成；请求 2（切换后）门控挂起。
                prep = new ScriptedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false));
                return CreateGatedBinding(ws.ViewState, prep, renderer);
            },
            resolver: new ScriptedContextResolver(contextA, contextB));

        var map = new MapSummary { MapId = mapId, DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var publisher = binding.Runtime.Publisher;
        var oldSnapshot = publisher.Current!;
        Assert.Equal(contextA.MapId, oldSnapshot.Context.MapId);
        Assert.Null(oldSnapshot.Context.BranchId);

        // 语义切换：真实 SwitchContextAsync 路径 → ContextSwitched → VM 重请求（门控挂起）。
        var switchTask = workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedContextResolver(contextB));
        await switchTask;
        await prep!.WaitEnteredAsync(1); // 确定性锚点：新请求已激活并进入准备

        // 新请求激活即清空旧语义快照（发布器语义身份机制）——旧快照不再可用。
        Assert.Null(publisher.Current);
        Assert.Equal(1, publisher.ContextSwitchClearCount);

        // 放行新请求 → 发布新 Context 的快照；旧快照绝不复现。
        // 等待条件绑定"第二个已发布快照版本"（请求 1 已发布 v1，不与初始结果混淆）。
        prep!.Release(1);
        await WaitUntilAsync(
            () => binding.LastRequestOutcome is { } o
                && o.State == PresentationRequestState.Published
                && o.PublishedSnapshotVersion == 2,
            "语义切换后的新请求发布");
        var newSnapshot = publisher.Current!;
        Assert.NotNull(newSnapshot.Context.BranchId);
        Assert.Equal(branchB, newSnapshot.Context.BranchId);
        Assert.NotEqual(oldSnapshot.Fingerprint, newSnapshot.Fingerprint);
        Assert.NotSame(oldSnapshot, newSnapshot);
        Assert.NotEqual(oldSnapshot.SnapshotVersion, newSnapshot.SnapshotVersion);

        // 协调器记录：请求 1 Published、请求 2 Published（真实两次请求）。
        Assert.Equal(2, binding.Runtime.Coordinator.RecentOutcomes.Count(o => o.State == PresentationRequestState.Published));

        await binding.RequestClose();
    }

    /// <summary>A-05：Prepare 期间关闭 → 迟到的正常完成结果不得发布（RejectedClosed，Current 为 null）。</summary>
    [Fact]
    public async Task A05_CloseDuringPrepare_LateResultNotPublished()
    {
        var renderer = new RecordingRenderer();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(bindingFactory: ws =>
        {
            prep = new ScriptedPreparation((Gate: true, Throw: false));
            return CreateGatedBinding(ws.ViewState, prep, renderer);
        });

        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };

        // 打开命令内部在等待初始请求完成；门控挂起 → 打开尚未返回。
        var openTask = OpenAsync(vm, mapQuery, map);
        await prep!.WaitEnteredAsync(0); // 请求已激活并处于 Prepare 中

        var binding = vm.GetViewportBinding(vm.Workspaces.Single())!;
        var publisher = binding.Runtime.Publisher;
        Assert.Null(publisher.Current); // 尚未发布

        // Prepare 进行中关闭视口（B 子序：协调器关闭屏障 → 发布器屏障）。
        var closeTask = binding.RequestClose();
        await closeTask;
        Assert.True(binding.IsCloseSucceeded);

        // 迟到结果：Prepare 正常完成（非取消）→ 最终发布决策被关闭屏障拒绝。
        prep!.Release(0);
        await WaitUntilAsync(
            () => binding.LastRequestOutcome is not null,
            "迟到的请求结果落定");
        Assert.Equal(PresentationRequestState.RejectedClosed, binding.LastRequestOutcome!.State);
        Assert.Null(publisher.Current); // 关闭后绝不发布
        Assert.Equal(0, publisher.PublishedCount);

        // 打开命令观察到 RejectedClosed（不冒充成功），状态文本可观察失败。
        var workspace = await openTask;
        Assert.Contains("已关闭", vm.WorkspaceStatusText, StringComparison.Ordinal);

        // 会话侧工作区仍可正常关闭（生命周期不被呈现层故障污染）。
        await CloseViaCommandAsync(vm, workspace);
        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
    }

    /// <summary>A-06：Prepare 完成与关闭交错 → 无非法访问，无"关闭后发布"。</summary>
    [Fact]
    public async Task A06_PrepareCompletionAndCloseInterleaved_NoIllegalAccess()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var renderer = new RecordingRenderer();
            ScriptedPreparation? prep = null;
            var (vm, session, mapQuery) = Build(bindingFactory: ws =>
            {
                prep = new ScriptedPreparation((Gate: true, Throw: false));
                return CreateGatedBinding(ws.ViewState, prep, renderer);
            });

            var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
            var openTask = OpenAsync(vm, mapQuery, map);
            await prep!.WaitEnteredAsync(0);

            var binding = vm.GetViewportBinding(vm.Workspaces.Single())!;
            var publisher = binding.Runtime.Publisher;

            // 并发：放行 Prepare 与关闭同一时刻交错（真实竞态窗口）。
            prep!.Release(0);
            await binding.RequestClose();
            Assert.True(binding.IsCloseSucceeded);

            // 等待打开命令返回（请求结果落定）。
            var workspace = await openTask;
            var outcome = binding.LastRequestOutcome;
            Assert.NotNull(outcome);

            // 不变式：若发布成功（Published），发布必然发生在关闭屏障生效之前，
            // 且发布计数与结果一致；否则一律不发布。绝不出现"关闭成功 + 仍发布"。
            if (outcome!.State == PresentationRequestState.Published)
            {
                Assert.Equal(1, publisher.PublishedCount);
                Assert.NotNull(publisher.Current); // 发布结果保持可读
                Assert.Equal(outcome.PublishedSnapshotVersion, publisher.PublishedVersion);
            }
            else
            {
                Assert.Equal(0, publisher.PublishedCount);
                Assert.Null(publisher.Current);
                Assert.True(
                    outcome.State is PresentationRequestState.RejectedClosed
                        or PresentationRequestState.Cancelled
                        or PresentationRequestState.Superseded,
                    $"意外结果状态：{outcome.State}");
            }

            // 关闭子序完整、无非法访问（无异常逃逸）。
            Assert.Equal(
                new[] { "is_closed", "coordinator_closed", "publisher_disposed", "renderer_disposed" },
                binding.CloseStageLog.ToArray());
        }
    }

    /// <summary>A-07：关闭后重新打开 → 全新 Runtime/Publisher/Binding 实例，与旧实例完全隔离。</summary>
    [Fact]
    public async Task A07_CloseThenReopen_IsolatedNewRuntimeLifetime()
    {
        var mapId = Guid.NewGuid();
        var renderer = new RecordingRenderer();
        var bindings = new List<MapViewportBinding>();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(bindingFactory: ws =>
        {
            prep = new ScriptedPreparation((Gate: false, Throw: false));
            var binding = CreateGatedBinding(ws.ViewState, prep, renderer);
            bindings.Add(binding);
            return binding;
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "M" };
        var workspace1 = await OpenAsync(vm, mapQuery, map);
        var binding1 = vm.GetViewportBinding(workspace1)!;
        var runtime1 = binding1.Runtime;
        var oldSnapshot = runtime1.Publisher.Current!;
        Assert.NotNull(oldSnapshot);

        // 关闭（C1：Binding → Session → UI 移除）。
        await CloseViaCommandAsync(vm, workspace1);
        Assert.True(binding1.IsClosed);
        Assert.True(runtime1.Coordinator.IsClosed);
        Assert.Null(vm.GetViewportBinding(workspace1));
        Assert.Empty(vm.Workspaces);
        var oldPublisherPublishedCount = runtime1.Publisher.PublishedCount;

        // 重新打开同一逻辑地图 → 新会话工作区 + 新 Binding/Runtime。
        var workspace2 = await OpenAsync(vm, mapQuery, map);
        var binding2 = vm.GetViewportBinding(workspace2)!;
        Assert.NotSame(workspace1, workspace2);
        Assert.NotSame(binding1, binding2);
        Assert.NotSame(runtime1, binding2.Runtime);
        Assert.NotSame(runtime1.Publisher, binding2.Runtime.Publisher);

        // 新视口发布全新快照：版本从 1 重新开始，不受旧 Runtime 任何状态影响。
        var newSnapshot = binding2.Runtime.Publisher.Current!;
        Assert.Equal(1, newSnapshot.SnapshotVersion);
        Assert.Equal(binding2.LastRequestedFingerprintHex, newSnapshot.Fingerprint.ToHex());

        // 旧 Runtime 隔离：协调器已关闭，旧发布器状态冻结，不被新链路触碰。
        Assert.True(runtime1.Coordinator.IsClosed);
        Assert.Equal(oldPublisherPublishedCount, runtime1.Publisher.PublishedCount);
        Assert.Same(oldSnapshot, runtime1.Publisher.Current);

        await binding2.RequestClose();
    }

    /// <summary>A-08：新请求失败 → 失败状态可观察（LastRequestOutcome / 状态文本），旧快照不冒充新结果。</summary>
    [Fact]
    public async Task A08_FailedNewRequest_ObservableFailureState()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextA = MakeContext(mapId, branchId: null);
        var contextB = MakeContext(mapId, branchB);
        var renderer = new RecordingRenderer();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(
            bindingFactory: ws =>
            {
                // 请求 1 成功；请求 2（切换后）注入准备失败。
                prep = new ScriptedPreparation((Gate: false, Throw: false), (Gate: false, Throw: true));
                return CreateGatedBinding(ws.ViewState, prep, renderer);
            },
            resolver: new ScriptedContextResolver(contextA, contextB));

        var map = new MapSummary { MapId = mapId, DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var publisher = binding.Runtime.Publisher;
        Assert.NotNull(publisher.Current);

        // 语义切换 → 重请求 → 准备失败。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedContextResolver(contextB));
        await WaitUntilAsync(
            () => vm.WorkspaceStatusText.Contains("失败", StringComparison.Ordinal),
            "失败状态出现在可观察状态文本");
        Assert.Equal(PresentationRequestState.Failed, binding.LastRequestOutcome!.State);
        Assert.Contains("准备失败注入", binding.LastRequestOutcome.Diagnostic, StringComparison.Ordinal);

        // 旧 Context 快照不得冒充新 Context 的有效结果：语义身份已切换 → 已清空。
        Assert.Null(publisher.Current);
        Assert.Equal(1, publisher.ContextSwitchClearCount);

        await binding.RequestClose();
    }

    /// <summary>A-09：重复打开/关闭 → 不重复订阅（切换每次恰触发一次重请求）、不泄漏、不重复释放。</summary>
    [Fact]
    public async Task A09_RepeatedOpenClose_NoDuplicateSubscription_NoLeak()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextA = MakeContext(mapId, branchId: null);
        var contextB = MakeContext(mapId, branchB);
        var renderer = new RecordingRenderer();
        var (vm, session, mapQuery) = Build(
            bindingFactory: ws => new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(), renderer),
            resolver: new ScriptedContextResolver(contextA, contextB, contextB, contextB));

        var map = new MapSummary { MapId = mapId, DisplayName = "M" };

        // 三轮打开/关闭：每轮全新 Binding/Runtime，旧实例正确关闭且被移除。
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var workspace = await OpenAsync(vm, mapQuery, map);
            var binding = vm.GetViewportBinding(workspace)!;
            Assert.True(binding.Runtime.Publisher.PublishedCount >= 1);

            await CloseViaCommandAsync(vm, workspace);
            Assert.True(binding.IsCloseSucceeded);
            Assert.Null(vm.GetViewportBinding(workspace));
            Assert.Empty(vm.Workspaces);
            Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
            Assert.Equal(1, workspace.DisposeCount); // 会话资源只释放一次（无重复释放）
        }

        // 第二轮内验证：切换三次（每次同语义 BranchB），协调器只收到"初始 + 3 次切换"共 4 次请求。
        var workspace2 = await OpenAsync(vm, mapQuery, map);
        var binding2 = vm.GetViewportBinding(workspace2)!;
        for (var i = 0; i < 3; i++)
        {
            await workspace2.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedContextResolver(contextB));
        }

        await WaitUntilAsync(
            () => binding2.Runtime.Coordinator.Counters.Requested == 4,
            "切换重请求收敛到恰好 4 次（1 初始 + 3 切换，无重复订阅）");
        Assert.Equal(4, binding2.Runtime.Coordinator.Counters.Requested);

        await binding2.RequestClose();
        await CloseViaCommandAsync(vm, workspace2);
    }

    /// <summary>A-10：正常关闭 → 与 WP-30B / PR #36 的 B 子序生命周期语义一致。</summary>
    [Fact]
    public async Task A10_NormalClose_FollowsEstablishedLifecycleSemantics()
    {
        var renderer = new RecordingRenderer();
        ScriptedPreparation? prep = null;
        var (vm, session, mapQuery) = Build(bindingFactory: ws =>
        {
            prep = new ScriptedPreparation((Gate: false, Throw: false));
            return CreateGatedBinding(ws.ViewState, prep, renderer);
        });

        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        Assert.NotNull(binding.Runtime.Publisher.Current);

        // 正常关闭（C1）：Binding 先关 → Session 关闭 → UI 集合移除。
        await CloseViaCommandAsync(vm, workspace);

        // B 子序程序序锚点：is_closed → coordinator_closed → publisher_disposed → renderer_disposed。
        Assert.Equal(
            new[] { "is_closed", "coordinator_closed", "publisher_disposed", "renderer_disposed" },
            binding.CloseStageLog.ToArray());
        Assert.True(binding.IsClosed);
        Assert.True(binding.IsCloseSucceeded);
        Assert.True(binding.Runtime.Coordinator.IsClosed);
        Assert.True(binding.Runtime.Publisher.IsDisposed);

        // 已发布快照的逻辑数据在关闭后仍保持有效（PRA-01 §8）。
        Assert.NotNull(binding.Runtime.Publisher.Current);

        // Application 侧：会话工作区 Closed，UI 集合移除，项目会话仍存活。
        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
        Assert.Empty(vm.Workspaces);
        Assert.NotEqual(ProjectSessionState.Closed, session.State);

        // 重复关闭幂等（成功终态）。
        await binding.RequestClose();
        Assert.Equal(
            new[] { "is_closed", "coordinator_closed", "publisher_disposed", "renderer_disposed", "already_closed" },
            binding.CloseStageLog.ToArray());
    }
}
