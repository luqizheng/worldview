using WorldView.Presentation.Scene;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// 不可变帧输入（WP-30B ADR-11）：一次 <see cref="MapViewportBinding.CaptureFrame"/>
/// 同步值拷贝的产物，是 Renderer / draw operation 的唯一渲染输入。
/// 只存快照引用（不深拷贝场景内容），不含 Avalonia 控件或绘图上下文、不含任何
/// Skia 对象、不含 session / ViewModel / 可变集合引用（SelectedItemIds 从不进入渲染路径）。
/// 相机数值为捕获瞬间的值拷贝；绘制期间 UI 可继续修改 ViewState，当前帧不受影响。
/// </summary>
/// <param name="Snapshot">
/// 当前场景快照引用（来自 <c>Publisher.Current</c> 的一次原子读取）。null = 加载/空态。
/// </param>
/// <param name="Width">视口逻辑像素宽度（捕获时缓存）。</param>
/// <param name="Height">视口逻辑像素高度（捕获时缓存）。</param>
/// <param name="RenderScaling">DPI 缩放（数值表达，不传 Avalonia 对象）。</param>
/// <param name="CameraX">相机 X（捕获自 WorkspaceViewState，占位单位）。</param>
/// <param name="CameraY">相机 Y（捕获自 WorkspaceViewState，占位单位）。</param>
/// <param name="Zoom">缩放（捕获自 WorkspaceViewState，占位）。</param>
/// <param name="ViewportGeneration">视口代（binding 持有的单调计数；诊断/一致性核对用）。</param>
internal readonly record struct ViewportFrameInput(
    SceneSnapshot? Snapshot,
    double Width,
    double Height,
    double RenderScaling,
    double CameraX,
    double CameraY,
    double Zoom,
    long ViewportGeneration);
