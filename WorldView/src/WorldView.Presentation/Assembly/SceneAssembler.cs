using System;
using System.Collections.Generic;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;

namespace WorldView.Presentation.Assembly;

/// <summary>
/// 场景装配器（PRA-01 契约 §5 / M1 Profile §6.1）：接收经校验的
/// PresentationContext 与上下文印记的子系统结果，整体校验后产出完整
/// 不可变 SceneSnapshot。不访问数据库、不做隐式 Domain 查询、不创建
/// 地图业务数据、不承担绘制。混合上下文、缺失必填、不支持的 schema
/// 或装配版本一律整体拒绝（拒绝是错误，不是可忽略的警告）。
///
/// M1 只组装空白场景：背景图层（Order 0）+ 显式可选图层（按输入列表的
/// 显式顺序依次编排序号；与完成顺序无关）。
/// </summary>
public sealed class SceneAssembler
{
    /// <summary>本装配器支持的装配语义版本（与上下文 SceneAssemblerVersion 必须一致）。</summary>
    public const int SupportedSceneAssemblerVersion = PresentationAssemblerVersions.BlankScene;

    /// <summary>
    /// 装配 M1 空白场景快照。
    /// </summary>
    /// <param name="context">经校验的呈现上下文（快照身份来源）。</param>
    /// <param name="requiredBlankScene">必填的空白场景子系统结果（必须与上下文同指纹、Completed、schema 受支持）。</param>
    /// <param name="explicitlyOptionalLayers">显式声明为可选的图层结果；null = 显式缺省（合法）。
    /// 出现的元素仍必须完整、同指纹、schema 受支持——可选豁免的是缺失，不是损坏。</param>
    /// <param name="candidateSnapshotVersion">发布器预分配的候选快照版本（≥ 1）。</param>
    public SceneAssemblyResult Assemble(
        PresentationContext context,
        PresentationResult<BlankSceneContent> requiredBlankScene,
        IReadOnlyList<PresentationResult<SceneLayerDescriptor>>? explicitlyOptionalLayers,
        long candidateSnapshotVersion)
    {
        if (context is null)
        {
            return SceneAssemblyResult.Rejected("context_missing", "缺少 PresentationContext，无法装配。");
        }

        if (candidateSnapshotVersion < 1)
        {
            return SceneAssemblyResult.Rejected(
                "invalid_snapshot_version",
                $"候选快照版本非法（{candidateSnapshotVersion}）；版本必须由发布器分配（≥ 1）。");
        }

        if (context.SceneAssemblerVersion != SupportedSceneAssemblerVersion)
        {
            return SceneAssemblyResult.Rejected(
                "scene_assembler_version_unsupported",
                $"上下文装配版本 v{context.SceneAssemblerVersion} 不受本装配器（v{SupportedSceneAssemblerVersion}）支持。");
        }

        if (requiredBlankScene is null)
        {
            return SceneAssemblyResult.Rejected("required_input_missing", "缺少必填的空白场景子系统结果。");
        }

        if (requiredBlankScene.Status != PresentationResultStatus.Completed || requiredBlankScene.Payload is null)
        {
            return SceneAssemblyResult.Rejected(
                "required_input_incomplete",
                $"空白场景子系统结果不完整（{requiredBlankScene.Status}）：{requiredBlankScene.Diagnostic}。");
        }

        if (requiredBlankScene.Fingerprint != context.Fingerprint)
        {
            return SceneAssemblyResult.Rejected(
                "required_input_context_mismatch",
                $"空白场景结果指纹（{requiredBlankScene.Fingerprint}）与上下文指纹（{context.Fingerprint}）不一致。");
        }

        if (requiredBlankScene.SchemaVersion != PresentationResultSchemas.BlankScene)
        {
            return SceneAssemblyResult.Rejected(
                "required_input_schema_unsupported",
                $"空白场景 schema v{requiredBlankScene.SchemaVersion} 不受支持（期望 v{PresentationResultSchemas.BlankScene}）。");
        }

        if (!string.Equals(requiredBlankScene.SubsystemName, PresentationSubsystems.BlankScene, StringComparison.Ordinal))
        {
            return SceneAssemblyResult.Rejected(
                "required_input_subsystem_unexpected",
                $"子系统名 \"{requiredBlankScene.SubsystemName}\" 非空白场景子系统（期望 \"{PresentationSubsystems.BlankScene}\"）。");
        }

        // 顺序权威：背景 Order 固定为 0；可选图层按输入列表的显式顺序依次编号，
        // 与任何任务完成顺序无关（PRA-01 §4.3）。
        var layers = new List<SceneLayer>
        {
            new(SceneLayerIds.Background, order: 0, new SceneItem[] { requiredBlankScene.Payload.Background }),
        };

        if (explicitlyOptionalLayers is not null)
        {
            for (var i = 0; i < explicitlyOptionalLayers.Count; i++)
            {
                var optional = explicitlyOptionalLayers[i];
                if (optional is null)
                {
                    return SceneAssemblyResult.Rejected(
                        "optional_input_null_element",
                        $"显式可选图层列表第 {i} 项为 null（缺失必须以省略表达，不得以 null 占位）。");
                }

                if (optional.Status != PresentationResultStatus.Completed || optional.Payload is null)
                {
                    return SceneAssemblyResult.Rejected(
                        "optional_input_incomplete",
                        $"显式可选图层 \"{optional.SubsystemName}\" 不完整（{optional.Status}）：{optional.Diagnostic}。");
                }

                if (optional.Fingerprint != context.Fingerprint)
                {
                    return SceneAssemblyResult.Rejected(
                        "optional_input_context_mismatch",
                        $"显式可选图层 \"{optional.SubsystemName}\" 指纹（{optional.Fingerprint}）与上下文指纹（{context.Fingerprint}）不一致。");
                }

                if (optional.SchemaVersion != PresentationResultSchemas.SceneLayerDescriptor)
                {
                    return SceneAssemblyResult.Rejected(
                        "optional_input_schema_unsupported",
                        $"显式可选图层 \"{optional.SubsystemName}\" schema v{optional.SchemaVersion} 不受支持。");
                }

                layers.Add(new SceneLayer(optional.Payload.LayerId, order: i + 1, optional.Payload.Items));
            }
        }

        var snapshot = new SceneSnapshot(context, candidateSnapshotVersion, layers);
        return SceneAssemblyResult.Ok(snapshot);
    }
}
