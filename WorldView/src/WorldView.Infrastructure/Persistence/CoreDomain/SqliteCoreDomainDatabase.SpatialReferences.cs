using System;
using System.Globalization;
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
                    PrecisionProfileId = validated.PrecisionProfile.ProfileId,
                    PrecisionProfileVersion = validated.PrecisionProfile.Version,
                    PrecisionCoordinateUnit = validated.PrecisionProfile.CoordinateUnit,
                    PrecisionAxisOrder = validated.PrecisionProfile.AxisOrder.ToString(),
                    NumericRepresentation = validated.PrecisionProfile.NumericRepresentation,
                    MaximumAbsoluteQuantizationError = FormatDecimal(validated.PrecisionProfile.MaximumAbsoluteQuantizationError),
                    RoundingMode = validated.PrecisionProfile.RoundingMode.ToString(),
                    MinimumAuthoringTolerance = FormatDecimal(validated.PrecisionProfile.MinimumAuthoringTolerance),
                    SmallestMeaningfulFeatureSize = FormatDecimal(validated.PrecisionProfile.SmallestMeaningfulFeatureSize),
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
                    throw new CoreDomainConflictException(
                        "Map 的 Spatial Reference 已绑定；更换必须经过坐标转换流程，不能原位重解释现有数据。");
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
        if (!Enum.TryParse<SpatialReferenceKind>(entity.ReferenceKind, ignoreCase: false, out var kind)
            || !Enum.IsDefined(kind)
            || !Enum.TryParse<SpatialAxisOrder>(entity.AxisOrder, ignoreCase: false, out var axisOrder)
            || !Enum.IsDefined(axisOrder)
            || !Enum.TryParse<SpatialWrapBehavior>(entity.WrapBehavior, ignoreCase: false, out var wrapBehavior)
            || !Enum.IsDefined(wrapBehavior)
            || !Enum.TryParse<SpatialAxisOrder>(entity.PrecisionAxisOrder, ignoreCase: false, out var precisionAxisOrder)
            || !Enum.IsDefined(precisionAxisOrder)
            || !Enum.TryParse<MidpointRounding>(entity.RoundingMode, ignoreCase: false, out var roundingMode)
            || !Enum.IsDefined(roundingMode)
            || !decimal.TryParse(entity.MaximumAbsoluteQuantizationError, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxError)
            || !decimal.TryParse(entity.MinimumAuthoringTolerance, NumberStyles.Number, CultureInfo.InvariantCulture, out var minTolerance)
            || !decimal.TryParse(entity.SmallestMeaningfulFeatureSize, NumberStyles.Number, CultureInfo.InvariantCulture, out var minFeature))
            throw new CoreDomainDataException(
                $"Spatial Reference {entity.SpatialReferenceId} contains an invalid enum or precision value.");

        var definition = new SpatialReferenceDefinitionRecord
        {
            ProjectId = entity.ProjectId,
            SpatialReferenceId = entity.SpatialReferenceId,
            DefinitionVersion = entity.DefinitionVersion,
            Kind = kind,
            CoordinateUnit = entity.CoordinateUnit,
            AxisOrder = axisOrder,
            Dimension = entity.Dimension,
            ValidDomainJson = entity.ValidDomainJson,
            WrapBehavior = wrapBehavior,
            VerticalReferenceId = entity.VerticalReferenceId,
            PrecisionProfile = new CoordinatePrecisionProfileRecord
            {
                ProfileId = entity.PrecisionProfileId,
                Version = entity.PrecisionProfileVersion,
                CoordinateUnit = entity.PrecisionCoordinateUnit,
                AxisOrder = precisionAxisOrder,
                NumericRepresentation = entity.NumericRepresentation,
                MaximumAbsoluteQuantizationError = maxError,
                RoundingMode = roundingMode,
                MinimumAuthoringTolerance = minTolerance,
                SmallestMeaningfulFeatureSize = minFeature,
            },
            DefinitionParametersJson = entity.DefinitionParametersJson,
            MeasurementCapabilitiesJson = entity.MeasurementCapabilitiesJson,
            ProvenanceJson = entity.ProvenanceJson,
            DefinitionFingerprint = entity.DefinitionFingerprint,
            CreatedAtUtcMs = entity.CreatedAtUtcMs,
        };

        try
        {
            return SpatialReferenceRules.Validate(definition);
        }
        catch (ArgumentException ex)
        {
            throw new CoreDomainDataException(
                $"Spatial Reference {entity.SpatialReferenceId} violates its immutable definition contract.", ex);
        }
    }

    private static string FormatDecimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
