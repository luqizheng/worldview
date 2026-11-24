using System;
using WorldView.Presentation.Identity;

namespace WorldView.Presentation.Results;

/// <summary>呈现子系统输出完成状态。</summary>
public enum PresentationResultStatus
{
    /// <summary>已完成且完整（载荷就绪）。</summary>
    Completed,

    /// <summary>失败（无载荷；诊断必填）。</summary>
    Failed,
}

/// <summary>
/// 上下文印记的不可变结果信封（PRA-01 契约 §3.2）：子系统输出必须携带
/// 计算时的 ContextFingerprint、子系统名与输出 schema 版本；输出绝不允许在
/// 计算后被悄悄改贴新指纹。载荷类型必须自身满足不可变性（私有所有权）。
/// </summary>
public sealed class PresentationResult<T> where T : class
{
    private PresentationResult(
        ContextFingerprint fingerprint,
        string subsystemName,
        int schemaVersion,
        PresentationResultStatus status,
        T? payload,
        string? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(subsystemName))
        {
            throw new ArgumentException("子系统名不能为空白。", nameof(subsystemName));
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "schema 版本必须为正整数。");
        }

        if (status == PresentationResultStatus.Completed && payload is null)
        {
            throw new ArgumentException("Completed 结果必须携带非空载荷。", nameof(payload));
        }

        if (status == PresentationResultStatus.Failed && string.IsNullOrEmpty(diagnostic))
        {
            throw new ArgumentException("Failed 结果必须携带诊断说明。", nameof(diagnostic));
        }

        Fingerprint = fingerprint;
        SubsystemName = subsystemName;
        SchemaVersion = schemaVersion;
        Status = status;
        Payload = payload;
        Diagnostic = diagnostic;
    }

    /// <summary>计算时的上下文指纹（印记；不可改贴）。</summary>
    public ContextFingerprint Fingerprint { get; }

    /// <summary>子系统名。</summary>
    public string SubsystemName { get; }

    /// <summary>输出 schema/算法版本（≥ 1）。</summary>
    public int SchemaVersion { get; }

    /// <summary>完成状态。</summary>
    public PresentationResultStatus Status { get; }

    /// <summary>不可变载荷（仅 Completed 状态非 null）。</summary>
    public T? Payload { get; }

    /// <summary>诊断说明（Failed 必填；Completed 可空）。</summary>
    public string? Diagnostic { get; }

    public static PresentationResult<T> Completed(
        ContextFingerprint fingerprint, string subsystemName, int schemaVersion, T payload)
        => new(fingerprint, subsystemName, schemaVersion, PresentationResultStatus.Completed, payload, diagnostic: null);

    public static PresentationResult<T> Failed(
        ContextFingerprint fingerprint, string subsystemName, int schemaVersion, string diagnostic)
        => new(fingerprint, subsystemName, schemaVersion, PresentationResultStatus.Failed, payload: null, diagnostic);

    public override string ToString()
        => $"{SubsystemName}/v{SchemaVersion} [{Status}] fp={Fingerprint.ToString()}{(Diagnostic is null ? string.Empty : $" diag={Diagnostic}")}";
}
