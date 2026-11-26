using System;
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
using WorldView.Presentation.Publication;
using WorldView.Presentation.Results;
using Xunit;

namespace WorldView.Desktop.Tests.Presentation;

/// <summary>
/// WP-RA-02-A R1-P2-02 ViewportGeneration 时序契约测试（方案 B）。
///
/// 契约（PRA-01 M1 Profile §3.5 + R1 修复记录）：
/// 1. M1 空白场景内容与视口尺寸/代数无关——初始场景请求先于视口控件 Bind 发起
///    是合法时序；Bind / PushViewportSize 引起的代数推进不构成已发布快照的场景
///    失效，不触发新的场景请求。
/// 2. 重复/无效的视口尺寸通知不产生任何场景请求。
/// 3. 有效视口尺寸尚未初始化（宽高为 0）时，快照发布与捕获行为不受影响。
/// 4. ViewportGeneration 为每视口私有状态，不同 Workspace 相互独立。
///
/// 未来依赖视口的场景内容（WP-30C / B 阶段）必须建立去重刷新路径——本组测试
/// 锁定 M1 阶段"代数推进 ≠ 场景失效"，防止无条件昂贵请求回归。
/// </summary>
public class ViewportGenerationTimingTests
{
    private sealed class TestVm : ProjectWorkspaceViewModel
    {
        public TestVm(
            ProjectSession session,
            IMapQueryService? mapQuery,
            IWorkspaceContextResolver? contextResolver,
            CreateMapUseCase createMap,
            IAppLog? log = null,
            IUiDispatcher? uiDispatcher = null)
            : base(session, mapQuery, contextResolver, createMap, log, uiDispatcher)
        {
        }
    }

    private static (TestVm Vm, FakeMapQueryService MapQuery) Build()
    {
        var session = TestSessions.CreateReadySession();
        var mapQuery = new FakeMapQueryService();
        var vm = new TestVm(
            session,
            mapQuery: mapQuery,
            contextResolver: new StubContextResolver(),
            createMap: new CreateMapUseCase(session, new FakeMapCreationService(mapQuery)),
            uiDispatcher: InlineUiDispatcher.Instance);
        return (vm, mapQuery);
    }

    private static async Task<MapWorkspaceSession> OpenAsync(TestVm vm, FakeMapQueryService mapQuery, MapSummary map)
    {
        mapQuery.Maps.Add(map);
        vm.SelectedMap = map;
        await vm.OpenWorkspaceCommand.ExecuteAsync(CancellationToken.None);
        return vm.Workspaces[^1];
    }

    [Fact]
    public async Task R1P2_02_InitialRequestBeforeBind_GenerationAdvanceIsNotSceneInvalidation()
    {
        var (vm, mapQuery) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var publisher = binding.Runtime.Publisher;

        // 初始请求先于 Bind 完成：快照与请求身份一致，代数捕获为 1。
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome!.State);
        var snapshot = publisher.Current!;
        Assert.Equal(binding.LastRequestedFingerprintHex, snapshot.Fingerprint.ToHex());
        Assert.Equal(1, snapshot.Context.ViewportGeneration);
        Assert.Equal(1, binding.Runtime.ViewportGeneration.Current);
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.Requested);

        // Bind → PushViewportSize → 代数推进到 2：契约 B 下不构成场景失效。
        var control = new MapViewportControl();
        control.Bind(binding);
        Assert.Equal(2, binding.Runtime.ViewportGeneration.Current);

        // 无新请求、无新发布、快照身份不变、LastRequestOutcome 不变。
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.Requested);
        Assert.Equal(1, publisher.PublishedCount);
        Assert.Same(snapshot, publisher.Current);
        Assert.Equal(snapshot.Fingerprint.ToHex(), publisher.Current!.Fingerprint.ToHex());
        Assert.Equal(1, binding.LastRequestOutcome!.PublishedSnapshotVersion);

        // 捕获帧如实携带双方代数：当前代 2 与快照请求代 1 的差异是合法状态，
        // 不被当作"快照与视口不一致"的场景失效（M1 契约 B）。
        var frame = binding.CaptureFrame();
        Assert.Same(snapshot, frame.Snapshot);
        Assert.Equal(2, frame.ViewportGeneration);
        Assert.Equal(1, frame.Snapshot!.Context.ViewportGeneration);

        await binding.RequestClose();
    }

    [Fact]
    public async Task R1P2_02_RepeatedViewportNotifications_DoNotGenerateRequests()
    {
        var (vm, mapQuery) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var snapshotBefore = binding.Runtime.Publisher.Current!;
        var fingerprintBefore = snapshotBefore.Fingerprint.ToHex();

        // Bind（代数推进 1 次）+ 重复/变化尺寸通知（模拟 SizeChanged / DPI 抖动）。
        var control = new MapViewportControl();
        control.Bind(binding);
        for (var i = 0; i < 5; i++)
        {
            binding.OnViewportChanged(100 + i, 80, renderScaling: 1.0);
        }

        binding.OnViewportChanged(100, 80, renderScaling: 1.0); // 与上一帧相同尺寸的重复通知

        // 契约 B：代数照实推进（每次通知 +1），但绝不产生场景请求或新发布。
        Assert.Equal(8, binding.Runtime.ViewportGeneration.Current); // 1 + bind(1) + 5 + 1
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.Requested);
        Assert.Equal(1, binding.Runtime.Publisher.PublishedCount);
        Assert.Equal(fingerprintBefore, binding.Runtime.Publisher.Current!.Fingerprint.ToHex());
        Assert.Equal(1, binding.LastRequestOutcome!.PublishedSnapshotVersion);

        await binding.RequestClose();
    }

    [Fact]
    public async Task R1P2_02_NoEffectiveViewportSize_SnapshotStillPublishedAndCapturable()
    {
        var (vm, mapQuery) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "M" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        // 有效视口尺寸尚未初始化：不经 Bind / OnViewportChanged（宽高保持 0）。
        Assert.Equal(0, binding.Runtime.Coordinator.Counters.RejectedClosed);
        Assert.Equal(0, binding.Runtime.Coordinator.Counters.PublicationRejections);

        // 初始请求照常发布；捕获帧宽高为 0（布局尚未发生）且快照有效。
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome!.State);
        var frame = binding.CaptureFrame();
        Assert.NotNull(frame.Snapshot);
        Assert.Equal(0, frame.Width);
        Assert.Equal(0, frame.Height);
        Assert.Equal(1, frame.ViewportGeneration);

        // 后续首次 Bind（首次有效尺寸到达）照常推进代数，不产生请求（契约 B）。
        var control = new MapViewportControl();
        control.Bind(binding);
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.Requested);
        Assert.Same(frame.Snapshot, binding.CaptureFrame().Snapshot);

        await binding.RequestClose();
    }

    [Fact]
    public async Task R1P2_02_ViewportGenerationIndependentAcrossWorkspaces()
    {
        var (vm, mapQuery) = Build();
        var map1 = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "WS1" };
        var map2 = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "WS2" };
        var workspace1 = await OpenAsync(vm, mapQuery, map1);
        var workspace2 = await OpenAsync(vm, mapQuery, map2);
        var binding1 = vm.GetViewportBinding(workspace1)!;
        var binding2 = vm.GetViewportBinding(workspace2)!;
        Assert.NotSame(binding1.Runtime.ViewportGeneration, binding2.Runtime.ViewportGeneration);

        // ws1 连续推进 3 代：ws2 代数不受影响（视口私有状态，非共享计数）。
        binding1.OnViewportChanged(10, 10, 1.0);
        binding1.OnViewportChanged(20, 10, 1.0);
        binding1.OnViewportChanged(30, 10, 1.0);
        Assert.Equal(4, binding1.Runtime.ViewportGeneration.Current);
        Assert.Equal(1, binding2.Runtime.ViewportGeneration.Current);

        // 反向：ws2 推进不影响 ws1。
        binding2.OnViewportChanged(50, 50, 1.0);
        Assert.Equal(2, binding2.Runtime.ViewportGeneration.Current);
        Assert.Equal(4, binding1.Runtime.ViewportGeneration.Current);

        // 请求创建捕获各自当时代（快照身份独立）。
        Assert.Equal(1, binding1.Runtime.Publisher.Current!.Context.ViewportGeneration);
        Assert.Equal(1, binding2.Runtime.Publisher.Current!.Context.ViewportGeneration);

        await binding1.RequestClose();
        await binding2.RequestClose();
    }
}
