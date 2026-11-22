using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// The single EF Core relational model for all currently accepted WorldView 1.0 Core Domain tables.
/// Domain and Application continue to depend on ICoreDomainDatabase, never DbContext.
/// </summary>
public class CoreDomainDbContext(DbContextOptions<CoreDomainDbContext> options) : DbContext(options)
{
    internal DbSet<CoreProjectEntity> Projects => Set<CoreProjectEntity>();
    internal DbSet<CoreMapEntity> Maps => Set<CoreMapEntity>();
    internal DbSet<CoreSpatialReferenceEntity> SpatialReferences => Set<CoreSpatialReferenceEntity>();
    internal DbSet<CoreAssetEntity> Assets => Set<CoreAssetEntity>();
    internal DbSet<CoreMapFieldDatasetEntity> FieldDatasets => Set<CoreMapFieldDatasetEntity>();
    internal DbSet<CoreMapFieldRevisionEntity> FieldRevisions => Set<CoreMapFieldRevisionEntity>();
    internal DbSet<CoreMapFieldRevisionPlaneEntity> FieldRevisionPlanes => Set<CoreMapFieldRevisionPlaneEntity>();
    internal DbSet<CoreMapFieldRevisionCategoryCodeEntity> FieldRevisionCategoryCodes => Set<CoreMapFieldRevisionCategoryCodeEntity>();
    internal DbSet<CoreMapFieldRevisionCodecEntity> FieldRevisionCodecs => Set<CoreMapFieldRevisionCodecEntity>();
    internal DbSet<CoreHistoryEntity> Histories => Set<CoreHistoryEntity>();
    internal DbSet<CoreBranchEntity> Branches => Set<CoreBranchEntity>();
    internal DbSet<CoreCalendarEntity> Calendars => Set<CoreCalendarEntity>();
    internal DbSet<CoreCalendarConfigurationEntity> CalendarConfigurations => Set<CoreCalendarConfigurationEntity>();
    internal DbSet<CoreObjectTypeEntity> ObjectTypes => Set<CoreObjectTypeEntity>();
    internal DbSet<CoreObjectEntity> Objects => Set<CoreObjectEntity>();
    internal DbSet<CoreObjectDefinitionEntity> ObjectDefinitions => Set<CoreObjectDefinitionEntity>();
    internal DbSet<CoreObjectIdAllocatorEntity> ObjectIdAllocators => Set<CoreObjectIdAllocatorEntity>();
    internal DbSet<CoreObjectIdReservationEntity> ObjectIdReservations => Set<CoreObjectIdReservationEntity>();
    internal DbSet<CoreMapObjectRefEntity> MapObjectRefs => Set<CoreMapObjectRefEntity>();
    internal DbSet<CoreMapInitialStateEntity> MapInitialStates => Set<CoreMapInitialStateEntity>();
    internal DbSet<CoreMapInitialObjectEntity> MapInitialObjects => Set<CoreMapInitialObjectEntity>();
    internal DbSet<CoreObjectDefinitionAssetEntity> ObjectDefinitionAssets => Set<CoreObjectDefinitionAssetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The migration snapshot and runtime context must be the same relational model.
        // A schema change updates this canonical configuration together with a forward migration.
        CoreDomainV1ModelConfiguration.Configure(modelBuilder);
    }
}

internal static class CoreDomainValueConverters
{
    internal static readonly ValueConverter<Guid, string> GuidToText = new(
        value => value.ToString("D"),
        value => ParseCanonicalGuid(value));

    internal static Guid ParseCanonicalGuid(string value) =>
        Guid.TryParseExact(value, "D", out var parsed)
        && string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal)
            ? parsed
            : throw new InvalidOperationException($"Persisted UUID '{value}' is not canonical lowercase D format.");
}
