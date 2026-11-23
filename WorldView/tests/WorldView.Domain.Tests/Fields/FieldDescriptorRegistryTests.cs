using WorldView.Domain.Fields;
using Xunit;

namespace WorldView.Domain.Tests.Fields;

/// <summary>
/// Field Descriptor Registry evolution rules (ME-05-12 §8): the registry is a compiled,
/// purely semantic (field_key, schema_version) → semantics_id mapping with no runtime
/// mutation, override or deletion path. Published mappings are immutable forever; additions
/// are the only allowed evolution; unknown identities fail closed. These tests pin the
/// lookup contract that every publish and read path relies on.
/// </summary>
public sealed class FieldDescriptorRegistryTests
{
    [Fact]
    public void TryGetSemanticsId_ResolvesEveryRegisteredDescriptorDeterministically()
    {
        Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("elevation", 1, out var elevationSemantics));
        Assert.Equal("world-view.field.elevation.v1", elevationSemantics);
        Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("land_water", 1, out var landWaterSemantics));
        Assert.Equal("world-view.field.land_water.v1", landWaterSemantics);

        // The mapping is a pure function: repeated lookups of a published descriptor must
        // never drift, because every revision read re-derives its pin from this registry.
        for (var i = 0; i < 3; i++)
        {
            Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("elevation", 1, out var repeat));
            Assert.Equal(elevationSemantics, repeat);
            Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("land_water", 1, out var repeatLandWater));
            Assert.Equal(landWaterSemantics, repeatLandWater);
        }
    }

    [Theory]
    [InlineData("elevation", 2)]
    [InlineData("land_water", 2)]
    [InlineData("unknown_field", 1)]
    [InlineData("", 1)]
    public void TryGetSemanticsId_FailsClosedForUnregisteredIdentities(string fieldKey, int schemaVersion)
    {
        Assert.False(FieldDescriptorRegistry.TryGetSemanticsId(fieldKey, schemaVersion, out var semanticsId));
        Assert.Equal(string.Empty, semanticsId);
    }

    [Fact]
    public void RegisteredDescriptorsCoexistIndependently()
    {
        // Additive coexistence: resolving one registered descriptor neither depends on nor
        // changes the resolution of another, so a future schema_version 2 addition cannot
        // alter how existing version 1 revisions resolve.
        Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("elevation", 1, out var elevation));
        Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("land_water", 1, out var landWater));
        Assert.NotEqual(elevation, landWater);
        Assert.True(FieldDescriptorRegistry.TryGetSemanticsId("elevation", 1, out var elevationAgain));
        Assert.Equal(elevation, elevationAgain);
    }
}
