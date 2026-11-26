using System;
using System.Threading.Tasks;
using SkiaSharp;
using WorldView.Application.Abstractions;
using WorldView.Desktop.Rendering;
using WorldView.Domain.Time;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Publication;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;
using Xunit;

namespace WorldView.Desktop.Tests.Presentation;

/// <summary>
/// WP-RA-02-A R1-P2-03 MapSceneRenderer 真实 Skia 绘制像素测试。
///
/// 既有 A-01/A-03 的 RecordingRenderer 覆写 Draw、以 <c>Draw(canvas: null!)</c>
/// 验证快照引用传递——从不执行基类绘制路径，也不验证 ResolveBackground 的
/// 颜色解析行为。本组测试以真实 <see cref="MapSceneRenderer"/>（不覆写）在
/// Skia CPU Surface（纯软件光栅，无 GPU / 无平台图形设备依赖、无需 GUI）上
/// 实际执行绘制命令，对实际像素输出断言：
/// 1. null Snapshot → 空态契约背景（#202226）；
/// 2. 已发布、含 BuiltinNeutralV1 背景 token 的 Snapshot → 中性背景（#E8E9EB）；
/// 3. 两种状态的实际像素输出可区分。
/// 资源（Surface / Bitmap / Pixmap / Runtime）全部 using / finally 释放。
/// 本组测试不是正式地图几何绘制测试（几何属 WP-30C / B 阶段）。
/// </summary>
public class MapSceneRendererPixelTests
{
    private static readonly int _size = 16;

    /// <summary>空态背景（MapSceneRenderer.EmptyStateBackground 的契约值）。</summary>
    private static readonly SKColor EmptyStateBackground = new(0x20, 0x22, 0x26);

    /// <summary>已发布中性背景（builtin-neutral-background/v1 的内置基线解释）。</summary>
    private static readonly SKColor PublishedNeutralBackground = new(0xE8, 0xE9, 0xEB);

    private static ViewportFrameInput Frame(SceneSnapshot? snapshot) => new(
        Snapshot: snapshot,
        Width: _size,
        Height: _size,
        RenderScaling: 1.0,
        CameraX: 0.0,
        CameraY: 0.0,
        Zoom: 1.0,
        ViewportGeneration: 1);

    /// <summary>
    /// 经真实生产管线发布一帧含已知背景 token 的快照（Context → Request →
    /// Prepare → Publish，M1 默认空白场景准备）。runtime 由调用方释放。
    /// </summary>
    private static async Task<(ViewportPresentationRuntime Runtime, SceneSnapshot Snapshot)> PublishBlankSnapshotAsync()
    {
        var runtime = ViewportPresentationRuntime.Create();
        var context = PresentationContextFactory.FromValidatedWorkspace(
            new ValidatedWorkspaceContext
            {
                MapId = Guid.NewGuid(),
                BranchId = null,
                RootHistoryId = Guid.NewGuid(),
                HistoricalTime = HistoricalTime.Epoch,
            },
            viewportGeneration: 1);
        var outcome = await runtime.RequestSceneAsync(context);
        Assert.Equal(PresentationRequestState.Published, outcome.State);
        Assert.NotNull(runtime.Publisher.Current);
        return (runtime, runtime.Publisher.Current!);
    }

    /// <summary>以真实渲染器在 CPU Surface 上绘制并取中心像素（资源就地释放）。</summary>
    private static SKColor RenderAndProbe(SceneSnapshot? snapshot)
    {
        using var surface = SKSurface.Create(new SKImageInfo(_size, _size, SKColorType.Bgra8888, SKAlphaType.Premul));
        Assert.NotNull(surface); // CPU 软件光栅可用（无 GPU / 平台图形设备依赖）

        new MapSceneRenderer().Draw(surface.Canvas, Frame(snapshot));

        using var pixmap = surface.PeekPixels()!;
        return pixmap.GetPixelColor(_size / 2, _size / 2);
    }

    [Fact]
    public void Draw_NullSnapshot_ProducesEmptyStateBackgroundPixels()
    {
        var pixel = RenderAndProbe(snapshot: null);

        Assert.Equal(EmptyStateBackground, pixel);
    }

    [Fact]
    public async Task Draw_PublishedNeutralSnapshot_ProducesNeutralBackgroundPixels()
    {
        var (runtime, snapshot) = await PublishBlankSnapshotAsync();
        try
        {
            var pixel = RenderAndProbe(snapshot);

            Assert.Equal(PublishedNeutralBackground, pixel);
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public async Task Draw_EmptyStateVsPublished_PixelOutputsDistinguishable()
    {
        var (runtime, snapshot) = await PublishBlankSnapshotAsync();
        try
        {
            var emptyPixel = RenderAndProbe(snapshot: null);
            var publishedPixel = RenderAndProbe(snapshot);

            // 两态在实际像素输出上可区分（不是"永远同色"的假渲染）。
            Assert.Equal(EmptyStateBackground, emptyPixel);
            Assert.Equal(PublishedNeutralBackground, publishedPixel);
            Assert.NotEqual(emptyPixel, publishedPixel);
        }
        finally
        {
            await runtime.DisposeAsync();
        }
    }
}