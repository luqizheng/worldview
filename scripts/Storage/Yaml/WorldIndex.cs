using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// <see cref="World"/> 索引操作的纯函数集合（不依赖 Godot，便于单测）。
///
/// 唯一允许「向工程 map_index 追加新图」的通道：<c>EditorContext.AddMap</c> 走
/// <see cref="AppendMap"/>，避免在多个写入点直接构造 <see cref="World"/> 时漏掉
/// <c>with { ... }</c> 不可变约束或漏发 <c>MapChanged</c> 信号。
/// </summary>
public static class WorldIndex
{
    /// <summary>
    /// 追加 <paramref name="refPath"/> 到 <paramref name="world"/> 的 <c>MapIndex</c> 末尾；
    /// 返回新的 <see cref="World"/> 实例（不改原对象）。
    /// 原 <c>MapIndex</c> 为 <c>null</c> 时按空列表处理。
    /// </summary>
    /// <param name="world">当前工程文档。</param>
    /// <param name="refPath">相对工程根的地图 yaml 路径（与 <c>MapIndexEntry.RefPath</c> 同义）。</param>
    public static World AppendMap(World world, string refPath)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (string.IsNullOrEmpty(refPath)) throw new ArgumentException("refPath 不可为空", nameof(refPath));

        var current = world.MapIndex ?? Array.Empty<MapIndexEntry>();
        var updated = current.Append(new MapIndexEntry(refPath)).ToList();
        return world with { MapIndex = updated };
    }
}
