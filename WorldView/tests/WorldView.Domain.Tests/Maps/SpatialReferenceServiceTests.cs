using System;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Tests.TestDoubles;
using Xunit;

namespace WorldView.Domain.Tests.Maps;

public sealed class SpatialReferenceServiceTests
{
    private const long Now = 1_000;

    private static SpatialReferenceDefinitionRecord Candidate(Guid projectId, string id) => new()
    {
        ProjectId = projectId,
        SpatialReferenceId = id,
        DefinitionVersion = 1,
        Kind = SpatialReferenceKind.LocalCartesian,
        CoordinateUnit = "m",
        AxisOrder = SpatialAxisOrder.XY,
        Dimension = 2,
        ValidDomainJson = """{"max_x":"1000","max_y":"1000","min_x":"-1000","min_y":"-1000"}""",
        WrapBehavior = SpatialWrapBehavior.None,
        PrecisionProfile = new CoordinatePrecisionProfileRecord
        {
            ProfileId = "local-precision-v1",
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
        MeasurementCapabilitiesJson = """{"area":true,"distance":true}""",
        ProvenanceJson = """{"source":"user"}""",
        DefinitionFingerprint = "",
        CreatedAtUtcMs = Now,
    };

    [Fact]
    public void RegisterAndBind_UsesProjectScopedImmutableDefinition()
    {
        var database = new FakeCoreDomainDatabase();
        var project = ProjectRules.CreateProject("Spatial", Now);
        database.InsertProject(ProjectCreationPlan.Create(project));

        var definition = SpatialReferenceService.RegisterDefinition(
            database, Candidate(project.ProjectId, "local:world-v1"));
        var mapPlan = MapRules.CreateMap(project, "World", Now + 1);
        database.CreateMapStructure(mapPlan);

        var bound = database.FindMap(project.ProjectId, mapPlan.Map.MapId)!;
        var defaultDefinition = SpatialReferenceService.FindDefinition(
            database, project.ProjectId, bound.SpatialReferenceId!)!;

        Assert.True(Guid.TryParseExact(bound.MapSpaceId, "D", out _));
        Assert.Equal(mapPlan.DefaultSpatialReference.SpatialReferenceId, bound.SpatialReferenceId);
        Assert.Equal(SpatialReferenceKind.LocalCartesian, defaultDefinition.Kind);
        Assert.Equal("world-unit", defaultDefinition.CoordinateUnit);
        Assert.Null(defaultDefinition.PrecisionProfile);
        Assert.Throws<CoreDomainConflictException>(() =>
            MapSpatialReferenceService.BindInitial(
                database, project.ProjectId, mapPlan.Map.MapId, definition.SpatialReferenceId, Now + 2));
        Assert.Equal(definition.DefinitionFingerprint,
            SpatialReferenceService.FindDefinition(database, project.ProjectId, definition.SpatialReferenceId)!.DefinitionFingerprint);
        Assert.Throws<CoreDomainConflictException>(() =>
            SpatialReferenceService.RegisterDefinition(database, Candidate(project.ProjectId, "local:world-v1") with
            {
                CoordinateUnit = "ft",
                PrecisionProfile = Candidate(project.ProjectId, "local:world-v1").PrecisionProfile with
                {
                    CoordinateUnit = "ft",
                },
            }));
    }

    [Fact]
    public void BindInitial_DoesNotReplaceAnExistingSpatialReference()
    {
        var database = new FakeCoreDomainDatabase();
        var project = ProjectRules.CreateProject("Spatial", Now);
        database.InsertProject(ProjectCreationPlan.Create(project));
        var first = SpatialReferenceService.RegisterDefinition(database, Candidate(project.ProjectId, "local:first"));
        var second = SpatialReferenceService.RegisterDefinition(database, Candidate(project.ProjectId, "local:second"));
        var map = MapRules.CreateMap(project, "World", Now + 1);
        database.CreateMapStructure(map);

        _ = MapSpatialReferenceService.BindInitial(database, project.ProjectId, map.Map.MapId, first.SpatialReferenceId, Now + 2);

        Assert.Throws<CoreDomainConflictException>(() =>
            MapSpatialReferenceService.BindInitial(database, project.ProjectId, map.Map.MapId, second.SpatialReferenceId, Now + 3));
    }

    [Fact]
    public void MapSpaceIdentity_IsDistinctAndStableForMapLifetime()
    {
        var project = ProjectRules.CreateProject("Spatial", Now);
        var first = MapRules.CreateMap(project, "A", Now + 1);
        var second = MapRules.CreateMap(project, "B", Now + 2);

        Assert.NotEqual(first.Map.MapId.ToString("D"), first.Map.MapSpaceId);
        Assert.NotEqual(first.Map.MapSpaceId, second.Map.MapSpaceId);
        Assert.Equal(first.Map.MapSpaceId, first.Map.MapSpaceId);
    }
}
