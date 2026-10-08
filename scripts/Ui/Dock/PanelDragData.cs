using Godot;

namespace WorldView.Ui.Dock;

/// <summary>
/// 面板拖放 payload（spec §4.1）。
///
/// <para><b>必须是 <c>partial class ... : RefCounted</c></b>：只有继承 Godot 的
/// <see cref="RefCounted"/> 才能被 <see cref="Variant.From(GodotObject)"/> 打包成
/// <c>Variant.Type.Object</c> 传递；普通 C# 类会让 payload 静默变成 null，
/// 接受方 <c>_CanDropData</c> 永远返回 false。</para>
/// </summary>
public partial class PanelDragData : RefCounted
{
    /// <summary>面板稳定 ID（如 <c>build_palette</c>），跨会话复用。</summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>来源区域（<c>left</c> / <c>right</c> / <c>top</c> / <c>bottom</c> / <c>center</c>）。</summary>
    public string SourceZone { get; set; } = string.Empty;
}

/// <summary>
/// 拖放 payload 的类型安全取值工具。
///
/// <para>不要直接写 <c>data.As&lt;PanelDragData&gt;()</c>：跨 <see cref="Window"/>
/// 或类型不符时它会抛异常。这里先降到 <see cref="GodotObject"/> 再用 <c>as</c> 判定，
/// 任何不匹配都安全返回 null。</para>
/// </summary>
public static class DragPayload
{
    public static T? Get<T>(Variant data)
        where T : GodotObject
        => data.VariantType == Variant.Type.Object ? data.As<GodotObject>() as T : null;
}
