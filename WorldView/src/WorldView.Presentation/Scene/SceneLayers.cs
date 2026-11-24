using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WorldView.Presentation.Scene;

/// <summary>
/// 装配后的最终图层：稳定 LayerId + 由 SceneAssembler 指定的显式 Order
/// （顺序权威在装配器，与子系统完成顺序无关；PRA-01 契约 §4.3）。
/// 项集合以 <see cref="ImmutableArray{T}"/> 私有持有——真正的所有权不可变
/// （经接口转型写回会抛出），而非接口性只读。
/// </summary>
public sealed class SceneLayer
{
    private readonly ImmutableArray<SceneItem> _items;

    /// <summary>稳定图层 ID。</summary>
    public string LayerId { get; }

    /// <summary>显式稳定顺序（由装配器指定）。</summary>
    public int Order { get; }

    /// <summary>场景项（不可变集合；外部无法修改）。</summary>
    public IReadOnlyList<SceneItem> Items => _items;

    public SceneLayer(string layerId, int order, IEnumerable<SceneItem> items)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            throw new ArgumentException("图层 ID 不能为空白。", nameof(layerId));
        }

        ArgumentNullException.ThrowIfNull(items);
        var owned = items.ToImmutableArray();
        foreach (var item in owned)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
        }

        _items = owned;
        LayerId = layerId;
        Order = order;
    }

    public override string ToString() => $"{LayerId}#{Order} [{_items.Length} items]";
}

/// <summary>
/// 图层描述（装配输入）：不含顺序——顺序由 SceneAssembler 在装配时显式指定，
/// 因此生产者完成顺序不可能影响最终场景排序。项集合同样以不可变集合私有持有。
/// </summary>
public sealed class SceneLayerDescriptor
{
    private readonly ImmutableArray<SceneItem> _items;

    /// <summary>稳定图层 ID。</summary>
    public string LayerId { get; }

    /// <summary>场景项（不可变集合）。</summary>
    public IReadOnlyList<SceneItem> Items => _items;

    public SceneLayerDescriptor(string layerId, IEnumerable<SceneItem> items)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            throw new ArgumentException("图层 ID 不能为空白。", nameof(layerId));
        }

        ArgumentNullException.ThrowIfNull(items);
        var owned = items.ToImmutableArray();
        foreach (var item in owned)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
        }

        _items = owned;
        LayerId = layerId;
    }

    public override string ToString() => $"{LayerId} [{_items.Length} items]";
}

/// <summary>稳定图层 ID。</summary>
public static class SceneLayerIds
{
    /// <summary>M1 背景图层（唯一背景权威）。</summary>
    public const string Background = "scene.layer.background.v1";
}
