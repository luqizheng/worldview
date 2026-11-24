using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WorldView.Presentation.Identity;

namespace WorldView.Presentation.Scene;

/// <summary>
/// 场景装配诊断（快照级诊断；不参与上下文指纹）。
/// </summary>
public sealed record SceneSnapshotDiagnostics
{
    /// <summary>装配完成时刻（诊断用；绝不进入指纹）。</summary>
    public DateTimeOffset UtcAssembledAt { get; init; }

    /// <summary>图层数。</summary>
    public int LayerCount { get; init; }

    /// <summary>场景项总数。</summary>
    public int ItemCount { get; init; }
}

/// <summary>
/// 不可变、渲染中立的场景快照（PRA-01 契约 §4 / M1 Profile §5）。
///
/// 所有权与不可变性：图层集合在构造时复制为私有数组；图层内的场景项集合
/// 同样私有复制；上下文与项均为不可变值——不存在经保留的可变引用改写
/// 快照内容的路径。快照不包含 Avalonia/Skia 后端资源、数据库句柄、
/// ViewModel、会话或任何地图 bounds/extent（当前 MapRecord 无边界数据）。
///
/// 生命周期：快照逻辑数据在被引用期间保持有效；发布器 Dispose 不使其失效；
/// 后端资源的所有权与释放归渲染管线（WP-30B），不进入本类型。
/// </summary>
public sealed class SceneSnapshot
{
    private readonly ImmutableArray<SceneLayer> _layers;

    /// <summary>上下文指纹（与 <see cref="Context"/> 一致；装配器校验）。</summary>
    public ContextFingerprint Fingerprint { get; }

    /// <summary>完整不可变上下文（安全身份投影；提供 Map/Branch/时间溯源）。</summary>
    public PresentationContext Context { get; }

    /// <summary>发布器分配的单调快照版本（发布器生命周期内严格递增）。</summary>
    public long SnapshotVersion { get; }

    /// <summary>装配语义版本（与上下文的 SceneAssemblerVersion 一致）。</summary>
    public int SceneAssemblerVersion { get; }

    /// <summary>显式稳定排序的图层（私有副本；外部无法修改）。</summary>
    public IReadOnlyList<SceneLayer> Layers => _layers;

    /// <summary>装配诊断。</summary>
    public SceneSnapshotDiagnostics Diagnostics { get; }

    internal SceneSnapshot(
        PresentationContext context,
        long snapshotVersion,
        IEnumerable<SceneLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (snapshotVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshotVersion), snapshotVersion, "快照版本必须为正整数（由发布器分配）。");
        }

        ArgumentNullException.ThrowIfNull(layers);
        var owned = layers.ToImmutableArray();
        foreach (var layer in owned)
        {
            ArgumentNullException.ThrowIfNull(layer, nameof(layers));
        }

        Fingerprint = context.Fingerprint;
        Context = context;
        SnapshotVersion = snapshotVersion;
        SceneAssemblerVersion = context.SceneAssemblerVersion;
        _layers = owned;
        Diagnostics = new SceneSnapshotDiagnostics
        {
            UtcAssembledAt = DateTimeOffset.UtcNow,
            LayerCount = owned.Length,
            ItemCount = owned.Sum(static layer => layer.Items.Count),
        };
    }

    public override string ToString()
        => $"SceneSnapshot(v{SnapshotVersion}, fp={Fingerprint.ToString()[..12]}…, layers={_layers.Length}, items={Diagnostics.ItemCount})";
}
