namespace WorldView.Domain.Validation;

/// <summary>
/// 通用校验器接口。MVP 实现：<c>MapValidator</c> / <c>ProjectValidator</c>。
/// <c>ProjectValidator</c> 在 Load 后调用每个 MapValidator 聚合结果。
/// </summary>
public interface IValidator<T>
{
    /// <summary>
    /// 校验目标对象，返回聚合结果。
    /// </summary>
    /// <param name="target">待校验对象（不可为 null）。</param>
    /// <param name="path">目标对象对应的 YAML 路径（相对工程根），用于错误定位。</param>
    /// <param name="rootPath">工程根绝对路径，用于解析 <c>ref</c> 时检查文件存在。</param>
    ValidationResult Validate(T target, string path, string rootPath);
}