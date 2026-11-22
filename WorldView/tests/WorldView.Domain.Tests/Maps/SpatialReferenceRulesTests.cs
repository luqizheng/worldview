using System;
using WorldView.Domain.Maps;
using Xunit;

namespace WorldView.Domain.Tests.Maps;

public sealed class SpatialReferenceRulesTests
{
    private static SpatialReferenceDefinitionRecord Candidate() => new()
    {
        ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        SpatialReferenceId = "local-cartesian:world-v1",
        DefinitionVersion = 1,
        Kind = SpatialReferenceKind.LocalCartesian,
        CoordinateUnit = "m",
        AxisOrder = SpatialAxisOrder.XY,
        Dimension = 2,
        ValidDomainJson = """{"max_x":"1000000","max_y":"1000000","min_x":"-1000000","min_y":"-1000000"}""",
        WrapBehavior = SpatialWrapBehavior.None,
        VerticalReferenceId = null,
        PrecisionProfile = new CoordinatePrecisionProfileRecord
        {
            ProfileId = "local-world-precision-v1",
            Version = 1,
            CoordinateUnit = "m",
            AxisOrder = SpatialAxisOrder.XY,
            NumericRepresentation = "decimal",
            MaximumAbsoluteQuantizationError = 0.000001m,
            RoundingMode = MidpointRounding.ToEven,
            MinimumAuthoringTolerance = 0.001m,
            SmallestMeaningfulFeatureSize = 0.01m,
        },
        DefinitionParametersJson = """{"origin_x":"0","origin_y":"0"}""",
        MeasurementCapabilitiesJson = """{"area":true,"distance":true,"direction":true}""",
        ProvenanceJson = """{"source":"explicit-user-definition"}""",
        DefinitionFingerprint = "",
        CreatedAtUtcMs = 100,
    };

    [Fact]
    public void Create_ComputesStableFingerprintFromCanonicalDefinition()
    {
        var definition = SpatialReferenceRules.Create(Candidate());
        var validated = SpatialReferenceRules.Validate(definition);

        Assert.Equal(64, definition.DefinitionFingerprint.Length);
        Assert.Matches("^[0-9a-f]{64}$", definition.DefinitionFingerprint);
        Assert.Equal(definition.DefinitionFingerprint, validated.DefinitionFingerprint);
        Assert.Equal(
            definition.DefinitionFingerprint,
            SpatialReferenceRules.Create(Candidate() with
            {
                ValidDomainJson = """{"min_y":"-1000000","min_x":"-1000000","max_y":"1000000","max_x":"1000000"}""",
            }).DefinitionFingerprint);
    }

    [Fact]
    public void Validate_RejectsChangedDefinitionContent()
    {
        var definition = SpatialReferenceRules.Create(Candidate());

        Assert.Throws<ArgumentException>(() =>
            SpatialReferenceRules.Validate(definition with { CoordinateUnit = "ft" }));
    }

    [Fact]
    public void Create_RejectsNonPositivePrecisionValues()
    {
        var candidate = Candidate() with
        {
            PrecisionProfile = Candidate().PrecisionProfile with
            {
                MaximumAbsoluteQuantizationError = 0,
            },
        };

        Assert.Throws<ArgumentException>(() => SpatialReferenceRules.Create(candidate));
    }

    [Fact]
    public void Create_RejectsNonObjectDefinitionExtensions()
    {
        var candidate = Candidate() with { DefinitionParametersJson = "[]" };

        Assert.Throws<ArgumentException>(() => SpatialReferenceRules.Create(candidate));
    }

    [Fact]
    public void Create_RejectsCrossDimensionalProfileValuesOnlyWhenStructurallyInvalid()
    {
        // Numeric adequacy thresholds are intentionally not guessed here. The profile carries
        // explicit values; representative-scale acceptance is a separate fixture gate.
        var definition = SpatialReferenceRules.Create(Candidate() with { Dimension = 3 });

        Assert.Equal(3, definition.Dimension);
    }
}
