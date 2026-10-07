using YamlDotNet.Serialization;

namespace WorldView.Domain;

/// <summary>
/// MVP 关心的 4 类图层（见 spec.md §2.3 + R-SAMPLE-02）。
/// 完整图层字段（含 <c>valid_from</c> / <c>valid_to</c> / <c>clip_to_faction</c> 等）见 <c>docs/图层系统规格_v0.1.md</c>。
/// </summary>
public enum LayerKind
{
    Terrain,
    Road,
    Building,
    Marker,
}

/// <summary>
/// 单个图层定义（独立 YAML：<c>layers/<kind>.yaml</c>）。
/// 字段定义见 spec.md §2.3。
///
/// MVP 字段：<c>id</c> / <c>name</c> / <c>type</c> / <c>visible</c> / <c>opacity</c> / <c>z_order</c> / <c>locked</c> / <c>default_style</c>。
/// </summary>
public sealed record LayerDoc(
    string Id,
    string Name,
    LayerKind Type,
    bool Visible = true,
    double Opacity = 1.0,
    int ZOrder = 0,
    bool Locked = false,
    [property: YamlMember(Alias = "default_style")] string? DefaultStyle = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public LayerDoc() : this("", "", LayerKind.Terrain) { }
}

/// <summary>
/// Map YAML 中通过 <c>layers: - ref: ../layers/<file>.yaml</c> 引用图层定义时的最小载体。
/// 实际加载在 <c>Storage/ProjectLoader.cs</c> 中：先收集所有 <c>LayerRef</c>，再批量解析 <c>ref</c> 指向的图层文件。
/// </summary>
public sealed record LayerRef(
    [property: YamlMember(Alias = "ref")] string RefPath)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public LayerRef() : this("") { }
}
