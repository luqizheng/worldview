using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
            var initializer = db.GetService<IModelRuntimeInitializer>();
            var runtimeModel = db.GetService<IDesignTimeModel>().Model;
            var snapshotModel = initializer.Initialize(new CoreDomainDbContextModelSnapshot().Model, designTime: true);
            var migrationModel = initializer.Initialize(new CoreDomainInitial().TargetModel, designTime: true);

            static string FirstDifference(string label, string left, string right)
            {
                var leftLines = left.Split('\n');
                var rightLines = right.Split('\n');
                var count = Math.Min(leftLines.Length, rightLines.Length);
                for (var i = 0; i < count; i++)
                {
                    if (!string.Equals(leftLines[i], rightLines[i], StringComparison.Ordinal))
                        return $"{label} line {i + 1}: snapshot/target=[{leftLines[i]}] runtime=[{rightLines[i]}]";
                }

                return leftLines.Length == rightLines.Length
                    ? string.Empty
                    : $"{label} line count: snapshot/target={leftLines.Length}, runtime={rightLines.Length}";
            }

            var runtimeText = runtimeModel.ToDebugString();
            return new[]
            {
                FirstDifference("snapshot", snapshotModel.ToDebugString(), runtimeText),
                FirstDifference("migration", migrationModel.ToDebugString(), runtimeText),
            }.Where(value => !string.IsNullOrEmpty(value)).ToArray();
        });

        Assert.True(differences.Length == 0,
            $"EF model differs from the frozen migration snapshot: {string.Join(Environment.NewLine, differences)}");
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
