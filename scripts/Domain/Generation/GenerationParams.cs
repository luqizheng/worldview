namespace WorldView.Domain.Generation;

/// <summary>
/// 世界生成的**运行时**参数包（区别于持久化用的 <c>GenerationSpec</c>）。
///
/// <para>设计动机：<c>GenerationSpec</c> 只描述「用户/工程保存下来的偏好」，
/// 实际跑生成算法时还要叠加地图自身的 <c>width</c> / <c>height</c> / <c>cell_distance</c>，
/// 避免在算法入口处反复跨字段拼装。本 record 把这两类数据合并为单一不可变包，
/// 由 <c>WorldGenerator</c>（Task 5）作为唯一入口接收。</para>
///
/// <para>注意：本 record **不**参与 YAML 序列化（不是持久化形态），故不挂 <c>YamlMember</c>。</para>
/// </summary>
public sealed record GenerationParams(
    GenerationSpec Spec,
    int Width,
    int Height,
    double CellDistance);
