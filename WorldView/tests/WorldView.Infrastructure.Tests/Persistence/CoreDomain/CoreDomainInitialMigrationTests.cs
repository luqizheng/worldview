using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// Verifies that the 1.0 EF migration is a real empty-database creation path and does not
/// depend on ApplicationBootstrapper or the development SQL migration ledger.
/// </summary>
public sealed class CoreDomainInitialMigrationTests : IDisposable
{
    private readonly string _root;
    private readonly SqlitePackageDatabase _database;

    public CoreDomainInitialMigrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-efcore-v1-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)new SqlitePackageDatabaseFactory()
            .CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void EfModel_MatchesFrozenSnapshotAndInitialMigration()
    {
        var session = new CoreDomainEfSession(((ISqliteConnectionProvider)_database).GetAccess());
        var differences = session.Execute(db =>
        {
            var connection = db.Database.GetDbConnection();
            var snapshotOptions = new DbContextOptionsBuilder<SnapshotModelContext>()
                .UseSqlite(connection, contextOwnsConnection: false)
                .Options;
            using var snapshotContext = new SnapshotModelContext(snapshotOptions);

            var differ = db.GetService<IMigrationsModelDiffer>();
            var runtimeModel = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
            var snapshotModel = snapshotContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();
            static string Describe(Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation operation)
            {
                var details = operation.GetType().GetProperties()
                    .Select(property => (property.Name, Value: property.GetValue(operation)))
                    .Where(item => item.Value is string or bool or int or long or string[])
                    .Select(item => $"{item.Name}={item.Value is string[] values ? string.Join("|", values) : item.Value}");
                return $"{operation.GetType().Name}({string.Join(", ", details)})";
            }

            return differ.GetDifferences(snapshotModel, runtimeModel)
                .Select(Describe)
                .ToArray();
        });

        Assert.True(differences.Length == 0,
            $"EF model differs from the frozen migration snapshot: {string.Join(", ", differences)}");
    }

    private sealed class SnapshotModelContext(DbContextOptions<SnapshotModelContext> options) : DbContext(options)
    {
        internal DbSet<CoreProjectEntity> Projects => Set<CoreProjectEntity>();
        internal DbSet<CoreMapEntity> Maps => Set<CoreMapEntity>();
        internal DbSet<CoreAssetEntity> Assets => Set<CoreAssetEntity>();
        internal DbSet<CoreMapFieldDatasetEntity> FieldDatasets => Set<CoreMapFieldDatasetEntity>();
        internal DbSet<CoreMapFieldRevisionEntity> FieldRevisions => Set<CoreMapFieldRevisionEntity>();
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
            CoreDomainV1FullModelSnapshotConfiguration.Configure(modelBuilder);
        }
    }

    [Fact]
    public void InitialMigration_CreatesCoreDomainSchemaFromEmptyDatabase()
    {
        _ = new SqliteCoreDomainDatabase(_database);
        var session = new CoreDomainEfSession(((ISqliteConnectionProvider)_database).GetAccess());

        var tables = ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT name FROM sqlite_master
                WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name;
                """;
            using var reader = command.ExecuteReader();
            var names = new System.Collections.Generic.List<string>();
            while (reader.Read()) names.Add(reader.GetString(0));
            return names;
        });

        Assert.Contains("__EFMigrationsHistory", tables);
        Assert.DoesNotContain("schema_migration", tables);
        Assert.Contains("core_project", tables);
        Assert.Contains("core_map", tables);
        Assert.Contains("core_history", tables);
        Assert.Contains("core_branch", tables);
        Assert.Contains("core_calendar", tables);
        Assert.Contains("core_calendar_configuration", tables);
        Assert.Contains("core_object_type", tables);
        Assert.Contains("core_object", tables);
        Assert.Contains("core_object_definition", tables);
        Assert.Contains("core_object_id_allocator", tables);
        Assert.Contains("core_object_id_reservation", tables);
        Assert.Contains("core_map_object_ref", tables);
        Assert.Contains("core_map_initial_state", tables);
        Assert.Contains("core_map_initial_object", tables);
        Assert.Contains("core_asset", tables);
        Assert.Contains("core_object_definition_asset", tables);
        Assert.Contains("core_map_field_dataset", tables);
        Assert.Contains("core_map_field_revision", tables);

        var mappedTables = session.Execute(db => db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .Distinct()
            .ToArray());
        foreach (var table in tables.Where(name => name.StartsWith("core_", StringComparison.Ordinal)))
        {
            Assert.Contains(table, mappedTables);
        }

        var migrationIds = session.Execute(db => db.Database.GetAppliedMigrations().ToArray());
        Assert.Equal(new[] { "202611220001_CoreDomainInitial" }, migrationIds);

        var triggerCount = ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'trigger' AND name IN (
                    'trg_core_map_object_ref_insert_scope',
                    'trg_core_map_object_ref_update_scope',
                    'trg_core_map_field_revision_immutable_update',
                    'trg_core_map_field_revision_immutable_delete',
                    'trg_core_map_field_dataset_pin_immutable'
                );
                """;
            return Convert.ToInt32(command.ExecuteScalar());
        });
        Assert.Equal(5, triggerCount);
    }
}
