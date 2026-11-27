using System;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>Persistence operations for immutable project-scoped Spatial Reference definitions.</summary>
public sealed partial class SqliteCoreDomainDatabase
{
    public SpatialReferenceDefinitionRecord? FindSpatialReferenceDefinition(Guid projectId, string spatialReferenceId) =>
        _ef.Execute(db =>
        {
            var entity = db.SpatialReferences.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId && x.SpatialReferenceId == spatialReferenceId);
            return entity is null ? null : MapSpatialReference(entity);
        });

    public void InsertSpatialReferenceDefinition(SpatialReferenceDefinitionRecord definition)
    {
        var validated = SpatialReferenceRules.Validate(definition);
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                if (!db.Projects.Any(x => x.ProjectId == validated.ProjectId))
                    throw new CoreDomainConflictException("Spatial Reference Project 不存在。");
                if (db.SpatialReferences.Any(x => x.ProjectId == validated.ProjectId
                    && x.SpatialReferenceId == validated.SpatialReferenceId))
                    throw new CoreDomainConflictException("Spatial Reference identity 已存在且不可覆盖。");

                db.SpatialReferences.Add(new CoreSpatialReferenceEntity
                {
                    ProjectId = validated.ProjectId,
                    SpatialReferenceId = validated.SpatialReferenceId,
                    DefinitionVersion = validated.DefinitionVersion,
                    ReferenceKind = validated.Kind.ToString(),
                    CoordinateUnit = validated.CoordinateUnit,
                    AxisOrder = validated.AxisOrder.ToString(),
                    Dimension = validated.Dimension,
                    ValidDomainJson = validated.ValidDomainJson,
                    WrapBehavior = validated.WrapBehavior.ToString(),
                    VerticalReferenceId = validated.VerticalReferenceId,
                    PrecisionProfileId = validated.PrecisionProfile?.ProfileId,
                    PrecisionProfileVersion = validated.PrecisionProfile?.Version,
                    PrecisionCoordinateUnit = validated.PrecisionProfile?.CoordinateUnit,
                    PrecisionAxisOrder = validated.PrecisionProfile?.AxisOrder.ToString(),
                    NumericRepresentation = validated.PrecisionProfile?.NumericRepresentation,
                    MaximumAbsoluteQuantizationError = validated.PrecisionProfile is null ? null : FormatDecimal(validated.PrecisionProfile.MaximumAbsoluteQuantizationError),
                    RoundingMode = validated.PrecisionProfile?.RoundingMode.ToString(),
                    MinimumAuthoringTolerance = validated.PrecisionProfile is null ? null : FormatDecimal(validated.PrecisionProfile.MinimumAuthoringTolerance),
                    SmallestMeaningfulFeatureSize = validated.PrecisionProfile is null ? null : FormatDecimal(validated.PrecisionProfile.SmallestMeaningfulFeatureSize),
                    DefinitionParametersJson = validated.DefinitionParametersJson,
                    MeasurementCapabilitiesJson = validated.MeasurementCapabilitiesJson,
                    ProvenanceJson = validated.ProvenanceJson,
                    DefinitionFingerprint = validated.DefinitionFingerprint,
                    CreatedAtUtcMs = validated.CreatedAtUtcMs,
                });
                db.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
            {
                transaction.Rollback();
                throw sqlite;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public MapRecord BindMapSpatialReference(Guid projectId, Guid mapId, string spatialReferenceId, long nowUtcMs) =>
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var map = db.Maps.SingleOrDefault(x => x.ProjectId == projectId && x.MapId == mapId)
                    ?? throw new CoreDomainConflictException("Map 不存在或不属于当前 Project。");
                if (!db.SpatialReferences.Any(x => x.ProjectId == projectId && x.SpatialReferenceId == spatialReferenceId))
                    throw new CoreDomainConflictException("Spatial Reference 不存在或不属于当前 Project。");

                if (map.SpatialReferenceId is not null)
                {
                    if (string.Equals(map.SpatialReferenceId, spatialReferenceId, StringComparison.Ordinal))
                    {
                        transaction.Commit();
                        return MapMap(map);
                    }

                    var currentEntity = db.SpatialReferences.SingleOrDefault(x =>
                        x.ProjectId == projectId && x.SpatialReferenceId == map.SpatialReferenceId)
                        ?? throw new CoreDomainDataException("Map 当前绑定的 Spatial Reference 定义缺失。");
                    _ = SpatialReferenceRules.ValidateDefaultLocalCartesian(MapSpatialReference(currentEntity), mapId);

                    var hasMapContent = db.MapObjectRefs.Any(x => x.project_id == projectId && x.map_id == mapId)
                        || db.MapInitialObjects.Any(x => x.project_id == projectId && x.map_id == mapId)
                        || db.FieldDatasets.Any(x => x.ProjectId == projectId && x.MapId == mapId);
                    if (hasMapContent)
                        throw new CoreDomainConflictException(
                            "Map 已有对象、Initial State 成员或 Field Dataset；更换 Spatial Reference 必须经过坐标转换流程。");
                }

                map.SpatialReferenceId = spatialReferenceId;
                map.UpdatedAtUtcMs = nowUtcMs;
                db.SaveChanges();
                transaction.Commit();
                return MapMap(map);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
            {
                transaction.Rollback();
                throw sqlite;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });

    private static SpatialReferenceDefinitionRecord MapSpatialReference(CoreSpatialReferenceEntity entity)
    {
        if (!Enum.TryParse<SpatialReferenceKind>(entity.ReferenceKind, false, out var kind)
            || !Enum.IsDefined(kind)
            || !Enum.TryParse<SpatialAxisOrder>(entity.AxisOrder, false, out var axisOrder)
            || !Enum.IsDefined(axisOrder)
            || !Enum.TryParse<SpatialWrapBehavior>(entity.WrapBehavior, false, out var wrapBehavior)
            || !Enum.IsDefined(wrapBehavior))
            throw new CoreDomainDataException($"Spatial Reference {entity.SpatialReferenceId} contains invalid coordinate-frame values.");

        CoordinatePrecisionProfileRecord? profile = null;
        var precisionValues = new[] { entity.PrecisionProfileId, entity.PrecisionCoordinateUnit, entity.PrecisionAxisOrder,
            entity.NumericRepresentation, entity.MaximumAbsoluteQuantizationError, entity.RoundingMode,
            entity.MinimumAuthoringTolerance, entity.SmallestMeaningfulFeatureSize };
        var hasAnyPrecisionValue = precisionValues.Any(x => x is not null) || entity.PrecisionProfileVersion is not null;
        if (hasAnyPrecisionValue)
        {
            if (precisionValues.Any(x => x is null) || entity.PrecisionProfileVersion is null
                || !Enum.TryParse<SpatialAxisOrder>(entity.PrecisionAxisOrder, false, out var precisionAxisOrder)
                || !Enum.IsDefined(precisionAxisOrder)
                || !Enum.TryParse<MidpointRounding>(entity.RoundingMode, false, out var roundingMode)
                || !Enum.IsDefined(roundingMode)
                || !decimal.TryParse(entity.MaximumAbsoluteQuantizationError, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxError)
                || !decimal.TryParse(entity.MinimumAuthoringTolerance, NumberStyles.Float, CultureInfo.InvariantCulture, out var minTolerance)
                || !decimal.TryParse(entity.SmallestMeaningfulFeatureSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var minFeature))
                throw new CoreDomainDataException($"Spatial Reference {entity.SpatialReferenceId} contains an incomplete or invalid precision profile.");
            profile = new CoordinatePrecisionProfileRecord
            {
                ProfileId = entity.PrecisionProfileId!, Version = entity.PrecisionProfileVersion.Value,
                CoordinateUnit = entity.PrecisionCoordinateUnit!, AxisOrder = precisionAxisOrder,
                NumericRepresentation = entity.NumericRepresentation!, MaximumAbsoluteQuantizationError = maxError,
                RoundingMode = roundingMode, MinimumAuthoringTolerance = minTolerance, SmallestMeaningfulFeatureSize = minFeature,
            };
        }

        var definition = new SpatialReferenceDefinitionRecord
        {
            ProjectId = entity.ProjectId, SpatialReferenceId = entity.SpatialReferenceId,
            DefinitionVersion = entity.DefinitionVersion, Kind = kind, CoordinateUnit = entity.CoordinateUnit,
            AxisOrder = axisOrder, Dimension = entity.Dimension, ValidDomainJson = entity.ValidDomainJson,
            WrapBehavior = wrapBehavior, VerticalReferenceId = entity.VerticalReferenceId, PrecisionProfile = profile,
            DefinitionParametersJson = entity.DefinitionParametersJson, MeasurementCapabilitiesJson = entity.MeasurementCapabilitiesJson,
            ProvenanceJson = entity.ProvenanceJson, DefinitionFingerprint = entity.DefinitionFingerprint, CreatedAtUtcMs = entity.CreatedAtUtcMs,
        };
        try { return SpatialReferenceRules.Validate(definition); }
        catch (ArgumentException ex) { throw new CoreDomainDataException($"Spatial Reference {entity.SpatialReferenceId} violates its immutable definition contract.", ex); }
    }

    private static string FormatDecimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
