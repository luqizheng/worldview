using System;
using System.Collections.Generic;
using System.Linq;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Assembly;

/// <summary>SceneAssembler：匹配装配、混合上下文/缺失/失败/schema/版本整体拒绝、显式可选图层、稳定排序。</summary>
public sealed class SceneAssemblerTests
{
    private static PresentationResult<BlankSceneContent> BlankResult(PresentationContext context)
        => PresentationResult<BlankSceneContent>.Completed(
            context.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
            new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));

    private static PresentationResult<SceneLayerDescriptor> OptionalLayerResult(
        PresentationContext context, string layerId = "scene.layer.optional.v1", int schemaVersion = PresentationResultSchemas.SceneLayerDescriptor)
    {
        var descriptor = new SceneLayerDescriptor(layerId, new SceneItem[]
        {
            new BlankBackgroundItem("scene.item.optional-marker.v1", NeutralBackgroundTokens.BuiltinNeutralV1),
        });
        return PresentationResult<SceneLayerDescriptor>.Completed(context.Fingerprint, "scene.optional-layers", schemaVersion, descriptor);
    }

    private readonly PresentationContext _context = TestContexts.Create();
    private readonly SceneAssembler _assembler = new();

    [Fact]
    public void MatchingInputs_AssembleSuccessfully()
    {
        var result = _assembler.Assemble(_context, BlankResult(_context), null, candidateSnapshotVersion: 1);

        Assert.True(result.Success, result.Diagnostic);
        Assert.NotNull(result.Snapshot);
        Assert.Single(result.Snapshot!.Layers);
        Assert.Equal(_context.Fingerprint, result.Snapshot.Fingerprint);
    }

    [Fact]
    public void NullContext_Rejected()
    {
        var result = _assembler.Assemble(null!, BlankResult(_context), null, 1);
        Assert.False(result.Success);
        Assert.Equal("context_missing", result.RejectionStage);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void MissingRequiredInput_Rejected()
    {
        var result = _assembler.Assemble(_context, null!, null, 1);
        Assert.False(result.Success);
        Assert.Equal("required_input_missing", result.RejectionStage);
        Assert.Null(result.Snapshot);   // 失败零产出
    }

    [Fact]
    public void FailedRequiredInput_Rejected_NotInterpretedAsEmptySuccess()
    {
        var failed = PresentationResult<BlankSceneContent>.Failed(
            _context.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene, "组合失败");
        var result = _assembler.Assemble(_context, failed, null, 1);

        Assert.False(result.Success);
        Assert.Equal("required_input_incomplete", result.RejectionStage);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void MismatchedFingerprint_Rejected()
    {
        var otherContext = TestContexts.Create(viewportGeneration: 2);
        Assert.NotEqual(_context.Fingerprint, otherContext.Fingerprint);

        var result = _assembler.Assemble(_context, BlankResult(otherContext), null, 1);
        Assert.False(result.Success);
        Assert.Equal("required_input_context_mismatch", result.RejectionStage);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void UnsupportedSchemaVersion_Rejected()
    {
        var future = PresentationResult<BlankSceneContent>.Completed(
            _context.Fingerprint, PresentationSubsystems.BlankScene, schemaVersion: 2,
            new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
        var result = _assembler.Assemble(_context, future, null, 1);

        Assert.False(result.Success);
        Assert.Equal("required_input_schema_unsupported", result.RejectionStage);
    }

    [Fact]
    public void WrongSubsystemName_Rejected()
    {
        var wrong = PresentationResult<BlankSceneContent>.Completed(
            _context.Fingerprint, "scene.not-blank", PresentationResultSchemas.BlankScene,
            new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
        var result = _assembler.Assemble(_context, wrong, null, 1);

        Assert.False(result.Success);
        Assert.Equal("required_input_subsystem_unexpected", result.RejectionStage);
    }

    [Fact]
    public void UnsupportedSceneAssemblerVersion_Rejected()
    {
        // 上下文携带的装配语义版本比本装配器新：整体拒绝（旧候选不得按新语义装配）。
        var futureContext = TestContexts.Create(sceneAssemblerVersion: SceneAssembler.SupportedSceneAssemblerVersion + 1);
        var result = _assembler.Assemble(futureContext, BlankResult(futureContext), null, 1);

        Assert.False(result.Success);
        Assert.Equal("scene_assembler_version_unsupported", result.RejectionStage);
    }

    [Fact]
    public void InvalidSnapshotVersion_Rejected()
    {
        var result = _assembler.Assemble(_context, BlankResult(_context), null, candidateSnapshotVersion: 0);
        Assert.False(result.Success);
        Assert.Equal("invalid_snapshot_version", result.RejectionStage);
    }

    [Fact]
    public void ExplicitlyAbsentOptionalLayers_IsLegal()
    {
        var result = _assembler.Assemble(_context, BlankResult(_context), explicitlyOptionalLayers: null, 1);
        Assert.True(result.Success, result.Diagnostic);
        Assert.Single(result.Snapshot!.Layers);
    }

    [Fact]
    public void EmptyOptionalLayerList_IsLegal()
    {
        var result = _assembler.Assemble(_context, BlankResult(_context), Array.Empty<PresentationResult<SceneLayerDescriptor>>(), 1);
        Assert.True(result.Success, result.Diagnostic);
    }

    [Fact]
    public void PresentOptionalLayers_AppendedInExplicitInputOrder()
    {
        var layerA = OptionalLayerResult(_context, "scene.layer.a.v1");
        var layerB = OptionalLayerResult(_context, "scene.layer.b.v1");

        var result = _assembler.Assemble(_context, BlankResult(_context), new[] { layerA, layerB }, 1);
        Assert.True(result.Success, result.Diagnostic);

        Assert.Equal(3, result.Snapshot!.Layers.Count);
        Assert.Equal(SceneLayerIds.Background, result.Snapshot.Layers[0].LayerId);
        Assert.Equal(0, result.Snapshot.Layers[0].Order);
        Assert.Equal("scene.layer.a.v1", result.Snapshot.Layers[1].LayerId);
        Assert.Equal(1, result.Snapshot.Layers[1].Order);
        Assert.Equal("scene.layer.b.v1", result.Snapshot.Layers[2].LayerId);
        Assert.Equal(2, result.Snapshot.Layers[2].Order);
    }

    [Fact]
    public void SceneOrdering_DependsOnExplicitInputOrder_NotCompletionOrder()
    {
        // 交换输入列表顺序 → 顺序随之交换：顺序权威在装配请求的显式顺序。
        var layerA = OptionalLayerResult(_context, "scene.layer.a.v1");
        var layerB = OptionalLayerResult(_context, "scene.layer.b.v1");

        var result = _assembler.Assemble(_context, BlankResult(_context), new[] { layerB, layerA }, 1);
        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal("scene.layer.b.v1", result.Snapshot!.Layers[1].LayerId);
        Assert.Equal("scene.layer.a.v1", result.Snapshot.Layers[2].LayerId);
    }

    [Fact]
    public void PresentButMismatchedOptionalLayer_Rejected()
    {
        var otherContext = TestContexts.Create(viewportGeneration: 5);
        var mismatched = OptionalLayerResult(otherContext);

        var result = _assembler.Assemble(_context, BlankResult(_context), new[] { mismatched }, 1);
        Assert.False(result.Success);
        Assert.Equal("optional_input_context_mismatch", result.RejectionStage);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void PresentButFailedOptionalLayer_Rejected()
    {
        var failed = PresentationResult<SceneLayerDescriptor>.Failed(
            _context.Fingerprint, "scene.optional-layers", PresentationResultSchemas.SceneLayerDescriptor, "可选图层失败");
        var result = _assembler.Assemble(_context, BlankResult(_context), new[] { failed }, 1);

        Assert.False(result.Success);
        Assert.Equal("optional_input_incomplete", result.RejectionStage);
    }

    [Fact]
    public void PresentButUnsupportedOptionalSchema_Rejected()
    {
        var future = OptionalLayerResult(_context, schemaVersion: 99);
        var result = _assembler.Assemble(_context, BlankResult(_context), new[] { future }, 1);

        Assert.False(result.Success);
        Assert.Equal("optional_input_schema_unsupported", result.RejectionStage);
    }

    [Fact]
    public void NullElementInOptionalList_Rejected()
    {
        var result = _assembler.Assemble(
            _context, BlankResult(_context), new[] { (PresentationResult<SceneLayerDescriptor>)null! }, 1);
        Assert.False(result.Success);
        Assert.Equal("optional_input_null_element", result.RejectionStage);
    }

    [Fact]
    public void AssemblerType_HasNoPersistenceOrUiDependencies()
    {
        // 装配器与上下文类型所在的程序集不得引用数据库/ORM/桌面技术栈。
        var referenced = typeof(SceneAssembler).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();
        Assert.DoesNotContain(referenced, name => name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.Contains("Skia", StringComparison.OrdinalIgnoreCase));
    }
}
