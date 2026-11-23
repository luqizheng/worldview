using System;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Maps;

/// <summary>Registers immutable project-scoped Spatial Reference definitions.</summary>
public static class SpatialReferenceService
{
    public static SpatialReferenceDefinitionRecord RegisterDefinition(
        ICoreDomainDatabase database,
        SpatialReferenceDefinitionRecord candidate)
    {
        ArgumentNullException.ThrowIfNull(database);
        var definition = SpatialReferenceRules.Create(candidate);
        if (database.FindProject(definition.ProjectId) is null)
            throw new CoreDomainConflictException(
                $"Project {definition.ProjectId:D} does not exist; Spatial Reference cannot be registered.");

        database.InsertSpatialReferenceDefinition(definition);
        return definition;
    }

    public static SpatialReferenceDefinitionRecord? FindDefinition(
        ICoreDomainDatabase database,
        Guid projectId,
        string spatialReferenceId)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(spatialReferenceId);
        var definition = database.FindSpatialReferenceDefinition(projectId, spatialReferenceId);
        return definition is null ? null : SpatialReferenceRules.Validate(definition);
    }
}

/// <summary>
/// Establishes a Map's initial immutable Spatial Reference binding. A later change is not
/// exposed here: it requires a separate coordinate-conversion workflow and new immutable data.
/// </summary>
public static class MapSpatialReferenceService
{
    public static MapRecord BindInitial(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        string spatialReferenceId,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(spatialReferenceId);
        if (nowUtcMs < 0)
            throw new ArgumentOutOfRangeException(nameof(nowUtcMs));

        var definition = SpatialReferenceService.FindDefinition(database, projectId, spatialReferenceId)
            ?? throw new CoreDomainConflictException(
                $"Spatial Reference '{spatialReferenceId}' does not exist in Project {projectId:D}.");

        return database.BindMapSpatialReference(projectId, mapId, definition.SpatialReferenceId, nowUtcMs);
    }
}
