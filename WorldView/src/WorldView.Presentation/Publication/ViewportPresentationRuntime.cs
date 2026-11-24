using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Identity;

namespace WorldView.Presentation.Publication;

/// <summary>
/// 每-视口呈现运行时（WP-30A 的可测试集成边界；M1 Profile §6.1）：
/// 一个视口实例私有的视口代计数器 + 快照发布器 + 请求协调器。
/// 实例必须按视口创建，绝不全局共享（视口隔离）；关闭时先停协调器再释放发布器，
/// 释放后一切迟到结果不得发布。WP-30B 的 MapViewportControl 将持有本运行时实例
/// （或其等价接线）作为其呈现管线入口。
/// </summary>
public sealed class ViewportPresentationRuntime : IAsyncDisposable
{
    private ViewportPresentationRuntime(
        SceneSnapshotPublisher publisher,
        PresentationRequestCoordinator coordinator,
        ViewportGenerationTracker viewportGeneration)
    {
        Publisher = publisher;
        Coordinator = coordinator;
        ViewportGeneration = viewportGeneration;
    }

    /// <summary>创建一个独立视口运行时（每视口一个；不得共享）。</summary>
    /// <param name="preparationOverride">可注入准备函数（测试确定性交错用；null = M1 默认空白场景准备）。</param>
    public static ViewportPresentationRuntime Create(ScenePreparation? preparationOverride = null)
    {
        var publisher = new SceneSnapshotPublisher();
        var coordinator = new PresentationRequestCoordinator(publisher, new SceneAssembler(), preparationOverride);
        return new ViewportPresentationRuntime(publisher, coordinator, new ViewportGenerationTracker());
    }

    /// <summary>视口私有发布器。</summary>
    public SceneSnapshotPublisher Publisher { get; }

    /// <summary>视口私有协调器。</summary>
    public PresentationRequestCoordinator Coordinator { get; }

    /// <summary>视口代计数器（视口相关输入变化时递增；请求创建时捕获）。</summary>
    public ViewportGenerationTracker ViewportGeneration { get; }

    /// <summary>便捷入口：以给定已捕获上下文发起一次场景请求。</summary>
    public Task<PresentationRequestOutcome> RequestSceneAsync(
        PresentationContext context, CancellationToken cancellationToken = default)
        => Coordinator.RequestSceneAsync(context, cancellationToken);

    /// <summary>
    /// 关闭视口呈现管线：先关协调器（停止接受、协作取消未完成请求），
    /// 再释放发布器（其后一切发布被拒绝）。幂等。
    /// 已发布快照的逻辑数据在被引用期间保持有效。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Coordinator.DisposeAsync().ConfigureAwait(false);
        Publisher.Dispose();
    }
}
