using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using WorldView.Domain.Time;
using WorldView.Presentation.Assembly;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Results;
using WorldView.Presentation.Scene;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Scene;

/// <summary>类别 C：空白场景快照结构、真正不可变性、无虚构几何/bounds、多次组装不改 Domain。</summary>
public sealed class SceneSnapshotTests
{
    private static (PresentationContext Context, SceneSnapshot Snapshot) AssembleBlank(long version = 1)
    {
        var context = TestContexts.Create();
        var assembler = new SceneAssembler();
        var contentResult = PresentationResult<BlankSceneContent>.Completed(
            context.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
            new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
        var assembly = assembler.Assemble(context, contentResult, null, version);
        Assert.True(assembly.Success, assembly.Diagnostic);
        return (context, assembly.Snapshot!);
    }

    [Fact]
    public void BlankScene_HasNeutralBackgroundOnly()
    {
        var (_, snapshot) = AssembleBlank();

        var layer = Assert.Single(snapshot.Layers);
        Assert.Equal(SceneLayerIds.Background, layer.LayerId);
        Assert.Equal(0, layer.Order);

        var item = Assert.Single(layer.Items);
        var background = Assert.IsType<BlankBackgroundItem>(item);
        Assert.Equal(SceneItemIds.BlankBackground, background.ItemId);
        Assert.Equal(NeutralBackgroundTokens.BuiltinNeutralV1, background.NeutralBackgroundToken);
    }

    [Fact]
    public void Snapshot_CarriesCoherentContextIdentity()
    {
        var (context, snapshot) = AssembleBlank(version: 4);

        Assert.Equal(context.Fingerprint, snapshot.Fingerprint);
        Assert.Same(context, snapshot.Context);
        Assert.Equal(4, snapshot.SnapshotVersion);
        Assert.Equal(context.SceneAssemblerVersion, snapshot.SceneAssemblerVersion);
    }

    [Fact]
    public void Snapshot_IsTrulyImmutable_NotJustInterfaceReadonly()
    {
        var (_, snapshot) = AssembleBlank();

        // ImmutableArray 拒绝经 ICollection/IList 转型写回。
        var layersAsCollection = (ICollection<SceneLayer>)snapshot.Layers;
        Assert.Throws<NotSupportedException>(() => layersAsCollection.Add(
            new SceneLayer("layer.rogue", 99, Array.Empty<SceneItem>())));

        var itemsAsCollection = (ICollection<SceneItem>)snapshot.Layers[0].Items;
        Assert.Throws<NotSupportedException>(() => itemsAsCollection.Add(
            new BlankBackgroundItem("scene.item.rogue", "token")));

        // 移除同样被拒绝。
        Assert.Throws<NotSupportedException>(() => layersAsCollection.Clear());
        Assert.Throws<NotSupportedException>(() => layersAsCollection.Remove(snapshot.Layers[0]));
    }

    [Fact]
    public void MutatingSourceCollections_AfterConstruction_CannotAffectOutputs()
    {
        var items = new List<SceneItem>
        {
            new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1),
        };
        var descriptor = new SceneLayerDescriptor("scene.layer.optional.v1", items);

        var context = TestContexts.Create();
        var assembler = new SceneAssembler();
        var contentResult = PresentationResult<BlankSceneContent>.Completed(
            context.Fingerprint, PresentationSubsystems.BlankScene, PresentationResultSchemas.BlankScene,
            new BlankSceneContent(new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1)));
        var assembly = assembler.Assemble(
            context, contentResult, new PresentationResult<SceneLayerDescriptor>[] { PresentationResult<SceneLayerDescriptor>.Completed(context.Fingerprint, "scene.optional", 1, descriptor) }, 1);
        Assert.True(assembly.Success, assembly.Diagnostic);

        // 构建后篡改源集合——结果与快照不受影响（所有权转移）。
        items.Clear();
        items.Add(new BlankBackgroundItem("scene.item.tampered", "tampered-token"));

        Assert.Single(descriptor.Items);
        Assert.Equal(SceneItemIds.BlankBackground, descriptor.Items[0].ItemId);
        var optionalLayer = assembly.Snapshot!.Layers.Single(layer => layer.Order == 1);
        Assert.Equal(SceneItemIds.BlankBackground, Assert.IsType<BlankBackgroundItem>(optionalLayer.Items[0]).ItemId);
        Assert.DoesNotContain(assembly.Snapshot.Layers.SelectMany(l => l.Items), item => item.ItemId.Contains("tampered", StringComparison.Ordinal));
    }

    [Fact]
    public void Snapshot_ContainsNoBoundsOrExtentFields()
    {
        // M1 Profile §5.2：MapRecord 无 extent/bounds——快照类型层面不得出现。
        foreach (var type in new[] { typeof(SceneSnapshot), typeof(SceneLayer), typeof(SceneItem), typeof(BlankSceneContent) })
        {
            var propertyNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
            Assert.DoesNotContain(propertyNames, name => name.Contains("Bounds", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(propertyNames, name => name.Contains("Extent", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(propertyNames, name => name.Contains("Width", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(propertyNames, name => name.Contains("Height", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Snapshot_ContainsNoFabricatedGeometryOrDecorations()
    {
        var (_, snapshot) = AssembleBlank();

        // 全部场景项都是中性背景；没有道路/建筑/地形/标签/装饰类内容。
        var allItems = snapshot.Layers.SelectMany(l => l.Items).ToList();
        Assert.All(allItems, item => Assert.IsType<BlankBackgroundItem>(item));
        Assert.All(allItems, item => Assert.False(
            item.ItemId.Contains("road", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("building", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("terrain", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("label", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("decoration", StringComparison.OrdinalIgnoreCase),
            $"场景项含虚构内容 ID：{item.ItemId}"));
    }

    [Fact]
    public void RepeatedAssembly_ProducesDeterministicContent_NoDomainMutation()
    {
        var (context, first) = AssembleBlank(version: 1);
        var (_, second) = AssembleBlank(version: 2);

        // 同一上下文多次组装：逻辑内容确定一致（顺序、ID、token）；版本按发布器分配不同。
        Assert.Equal(first.Layers.Count, second.Layers.Count);
        Assert.Equal(
            first.Layers.SelectMany(l => l.Items).Select(i => i.ItemId),
            second.Layers.SelectMany(l => l.Items).Select(i => i.ItemId));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.NotEqual(first.SnapshotVersion, second.SnapshotVersion);

        // 组装只读上下文，不触碰 Domain：Context 保持原值（Domain 类型未被复制或改写）。
        Assert.Equal(TestContexts.MapA, context.MapId);
        Assert.Equal(new HistoricalTime(1200, 43200), context.HistoricalTime);
    }

    [Fact]
    public void LayerBackingCollection_IsNotExposedMutableListOrArray()
    {
        var items = new List<SceneItem>
        {
            new BlankBackgroundItem(SceneItemIds.BlankBackground, NeutralBackgroundTokens.BuiltinNeutralV1),
        };
        var layer = new SceneLayer(SceneLayerIds.Background, 0, items);

        // 不是裸 List/数组（转型写回路径不存在）；构建后篡改源集合不渗透。
        Assert.Null(layer.Items as List<SceneItem>);
        Assert.Null(layer.Items as SceneItem[]);
        items.Clear();
        Assert.Single(layer.Items);
    }
}
