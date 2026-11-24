using System;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Scene;

namespace WorldView.Presentation.Assembly;

/// <summary>呈现子系统名（M1 唯一子系统：空白场景）。</summary>
public static class PresentationSubsystems
{
    /// <summary>M1 空白场景（中性背景）子系统。</summary>
    public const string BlankScene = "scene.blank";
}

/// <summary>子系统输出 schema 版本。</summary>
public static class PresentationResultSchemas
{
    /// <summary>空白场景载荷 schema V1。</summary>
    public const int BlankScene = 1;

    /// <summary>可选图层描述 schema V1。</summary>
    public const int SceneLayerDescriptor = 1;
}

/// <summary>
/// 场景装配结果：成功携带完整快照；拒绝时零产出（不产生部分快照），
/// 携带拒绝阶段与诊断（PRA-01 契约 §5.4）。
/// </summary>
public sealed class SceneAssemblyResult
{
    private SceneAssemblyResult(SceneSnapshot? snapshot, string? rejectionStage, string? diagnostic)
    {
        Snapshot = snapshot;
        RejectionStage = rejectionStage;
        Diagnostic = diagnostic;
    }

    /// <summary>装配是否成功。</summary>
    public bool Success => Snapshot is not null;

    /// <summary>完整快照（仅成功时非 null；不存在部分快照）。</summary>
    public SceneSnapshot? Snapshot { get; }

    /// <summary>拒绝阶段（失败时非 null，如 required_input_missing / fingerprint_mismatch）。</summary>
    public string? RejectionStage { get; }

    /// <summary>诊断说明（失败时非 null）。</summary>
    public string? Diagnostic { get; }

    public static SceneAssemblyResult Ok(SceneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new SceneAssemblyResult(snapshot, rejectionStage: null, diagnostic: null);
    }

    public static SceneAssemblyResult Rejected(string rejectionStage, string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(rejectionStage))
        {
            throw new ArgumentException("拒绝阶段不能为空白。", nameof(rejectionStage));
        }

        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            throw new ArgumentException("拒绝诊断不能为空白。", nameof(diagnostic));
        }

        return new SceneAssemblyResult(snapshot: null, rejectionStage, diagnostic);
    }
}
