namespace WorldView.Domain.Validation;

/// <summary>
/// 校验结果聚合（<c>IValidator</c> 的返回值）。
/// MVP 规则：<c>Errors</c> 与 <c>Warnings</c> 都按 <see cref="ValidationError.Code"/> 自然序排列，便于断言。
/// </summary>
public sealed record ValidationResult(
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationError> Warnings)
{
    /// <summary>
    /// MVP 是否通过：<c>Errors.IsEmpty</c>。Warnings 不阻塞但会呈现到 UI。
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// 空结果（无错也无警）。
    /// </summary>
    public static ValidationResult Ok { get; } = new(Array.Empty<ValidationError>(), Array.Empty<ValidationError>());

    /// <summary>
    /// 合并多个校验结果（用于 ProjectValidator 串联 MapValidator + MapValidator）。
    /// </summary>
    public static ValidationResult Merge(params ValidationResult[] results)
    {
        if (results is null || results.Length == 0) return Ok;
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();
        foreach (var r in results)
        {
            if (r is null) continue;
            errors.AddRange(r.Errors);
            warnings.AddRange(r.Warnings);
        }
        return new ValidationResult(
            errors.OrderBy(e => e.Code, StringComparer.Ordinal).ToArray(),
            warnings.OrderBy(e => e.Code, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// 把 <c>Errors</c> + <c>Warnings</c> 拼成可读字符串（UI 错误面板 + 测试断言）。
    /// </summary>
    public override string ToString()
    {
        if (IsValid && Warnings.Count == 0) return "OK";
        var sb = new System.Text.StringBuilder();
        foreach (var e in Errors) sb.Append("ERROR ").AppendLine(e.ToString());
        foreach (var w in Warnings) sb.Append("WARN  ").AppendLine(w.ToString());
        return sb.ToString().TrimEnd();
    }
}