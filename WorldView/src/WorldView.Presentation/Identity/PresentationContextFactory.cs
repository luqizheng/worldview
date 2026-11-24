using System;
using WorldView.Application.Abstractions;

namespace WorldView.Presentation.Identity;

/// <summary>
/// M1 呈现上下文工厂：从经 Core Domain 校验并经 Application 边界保留的
/// <see cref="ValidatedWorkspaceContext"/> 构建不可变 PresentationContext。
///
/// M1 固定语义（Profile §3.2/§3.3/§3.4，不得伪造）：
///   - WorldStateRevision = NotEvaluated（无权威 World State 读模型）；
///   - Projection = NotConfigured（无权威投影定义）；
///   - Style/Font = 内置基线 token；SceneAssemblerVersion = M1 空白场景版本。
/// 视口代由调用方的视口适配器捕获（请求创建时捕获为不可变值）。
/// 本工厂不访问持久化、不回查 Domain——身份只来自已验证上下文。
/// </summary>
public static class PresentationContextFactory
{
    /// <summary>构建 M1 空白场景的 PresentationContext。</summary>
    /// <param name="validated">经 Core Domain 校验的工作区上下文（含 RootHistoryId 与 typed HistoricalTime）。</param>
    /// <param name="viewportGeneration">本次请求捕获的视口代（≥ 1）。</param>
    public static PresentationContext FromValidatedWorkspace(
        ValidatedWorkspaceContext validated,
        long viewportGeneration)
    {
        ArgumentNullException.ThrowIfNull(validated);
        if (validated.MapId == Guid.Empty)
        {
            throw new ArgumentException("经校验上下文的 MapId 不能为空 GUID。", nameof(validated));
        }

        if (validated.RootHistoryId == Guid.Empty)
        {
            throw new ArgumentException("经校验上下文的 RootHistoryId 不能为空 GUID（Application 边界必须保留权威解析结果）。", nameof(validated));
        }

        if (validated.HistoricalTime is null)
        {
            throw new ArgumentException("经校验上下文必须携带 typed HistoricalTime（不允许字符串降级）。", nameof(validated));
        }

        return new PresentationContext(
            mapId: validated.MapId,
            branchId: validated.BranchId,
            rootHistoryId: validated.RootHistoryId,
            historicalTime: validated.HistoricalTime,
            worldStateRevision: WorldStateRevision.NotEvaluated.Instance,
            projection: ProjectionDefinition.NotConfigured.Instance,
            styleToken: PresentationTokens.BuiltinNeutralStyle,
            fontToken: PresentationTokens.BuiltinFontBaseline,
            viewportGeneration: viewportGeneration,
            sceneAssemblerVersion: PresentationAssemblerVersions.BlankScene);
    }
}

/// <summary>M1 场景装配语义版本常量。</summary>
public static class PresentationAssemblerVersions
{
    /// <summary>M1 空白场景装配（行为影响输出时递增）。</summary>
    public const int BlankScene = 1;
}
