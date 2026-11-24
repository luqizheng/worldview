using System;
using WorldView.Domain.Time;

namespace WorldView.Presentation.Identity;

/// <summary>
/// 不可变、强类型的呈现上下文（PRA-01 契约 §2 / M1 Profile §3）。
/// 只承载已校验的身份值：不保留 ProjectSession、MapWorkspaceSession、
/// WorkspaceViewState、ViewModel、数据库对象或可变 Domain 实体；
/// HistoricalTime 为 Core Domain 的不可变 typed 值（保留 day/offset 全精度）。
///
/// 构造时即计算确定性指纹与语义身份。相等性按全部身份字段的规范值比较
/// （非引用比较）；指纹是快速身份 token，字段相等与指纹一致由测试锁定。
/// </summary>
public sealed class PresentationContext : IEquatable<PresentationContext>
{
    /// <summary>权威 Map 身份。</summary>
    public Guid MapId { get; }

    /// <summary>选定 Branch；null = 根历史（默认/主历史）。</summary>
    public Guid? BranchId { get; }

    /// <summary>Map 根历史 ID（权威解析，永不缺失）。</summary>
    public Guid RootHistoryId { get; }

    /// <summary>typed 规范历史时间（Core Domain 权威值；不是显示文本）。</summary>
    public HistoricalTime HistoricalTime { get; }

    /// <summary>typed World State 评估状态（M1 恒为 <see cref="WorldStateRevision.NotEvaluated"/>）。</summary>
    public WorldStateRevision WorldStateRevision { get; }

    /// <summary>typed 投影状态（M1 恒为 <see cref="ProjectionDefinition.NotConfigured"/>）。</summary>
    public ProjectionDefinition Projection { get; }

    /// <summary>显式样式基线 token（如 <see cref="PresentationTokens.BuiltinNeutralStyle"/>）。</summary>
    public string StyleToken { get; }

    /// <summary>显式字体基线 token（如 <see cref="PresentationTokens.BuiltinFontBaseline"/>）。</summary>
    public string FontToken { get; }

    /// <summary>视口代（视口适配器拥有的单调递增代；进程内呈现状态，非持久化 Map revision）。</summary>
    public long ViewportGeneration { get; }

    /// <summary>场景装配语义版本。</summary>
    public int SceneAssemblerVersion { get; }

    /// <summary>确定性上下文指纹（构造时计算；相同语义上下文必然相同）。</summary>
    public ContextFingerprint Fingerprint { get; }

    /// <summary>语义工作区身份（Map/Branch/RootHistory/HistoricalTime；语义切换判定依据）。</summary>
    public WorkspaceSemanticIdentity SemanticIdentity { get; }

    public PresentationContext(
        Guid mapId,
        Guid? branchId,
        Guid rootHistoryId,
        HistoricalTime historicalTime,
        WorldStateRevision worldStateRevision,
        ProjectionDefinition projection,
        string styleToken,
        string fontToken,
        long viewportGeneration,
        int sceneAssemblerVersion)
    {
        MapId = mapId != Guid.Empty ? mapId : throw new ArgumentException("MapId 不能为空 GUID。", nameof(mapId));
        BranchId = branchId;
        RootHistoryId = rootHistoryId != Guid.Empty
            ? rootHistoryId
            : throw new ArgumentException("RootHistoryId 不能为空 GUID。", nameof(rootHistoryId));
        HistoricalTime = historicalTime ?? throw new ArgumentNullException(nameof(historicalTime));
        WorldStateRevision = worldStateRevision ?? throw new ArgumentNullException(nameof(worldStateRevision));
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        StyleToken = RequireToken(styleToken, nameof(styleToken));
        FontToken = RequireToken(fontToken, nameof(fontToken));
        ViewportGeneration = viewportGeneration >= 1
            ? viewportGeneration
            : throw new ArgumentOutOfRangeException(nameof(viewportGeneration), viewportGeneration, "ViewportGeneration 必须从 1 开始单调递增。");
        SceneAssemblerVersion = sceneAssemblerVersion >= 1
            ? sceneAssemblerVersion
            : throw new ArgumentOutOfRangeException(nameof(sceneAssemblerVersion), sceneAssemblerVersion, "SceneAssemblerVersion 必须为正整数。");

        SemanticIdentity = new WorkspaceSemanticIdentity(MapId, BranchId, RootHistoryId, HistoricalTime);
        Fingerprint = PresentationContextCanonicalizer.FingerprintOf(this);
    }

    private static string RequireToken(string value, string paramName)
        => !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("token 不能为空白。", paramName);

    /// <summary>按全部身份字段的规范值比较（PRA-01 §2.5：相等性基于规范上下文身份）。</summary>
    public bool Equals(PresentationContext? other)
        => other is not null
            && MapId == other.MapId
            && BranchId == other.BranchId
            && RootHistoryId == other.RootHistoryId
            && HistoricalTime.Equals(other.HistoricalTime)
            && WorldStateRevision.Equals(other.WorldStateRevision)
            && Projection.Equals(other.Projection)
            && string.Equals(StyleToken, other.StyleToken, StringComparison.Ordinal)
            && string.Equals(FontToken, other.FontToken, StringComparison.Ordinal)
            && ViewportGeneration == other.ViewportGeneration
            && SceneAssemblerVersion == other.SceneAssemblerVersion;

    public override bool Equals(object? obj) => obj is PresentationContext other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            MapId,
            BranchId,
            RootHistoryId,
            HistoricalTime,
            WorldStateRevision,
            Projection,
            StyleToken,
            FontToken);

    public override string ToString()
        => $"PresentationContext({SemanticIdentity}, gen={ViewportGeneration}, {WorldStateRevision}, {Projection}, {StyleToken}, {FontToken}, asm=v{SceneAssemblerVersion}, fp={Fingerprint.ToString()[..12]}…)";
}
