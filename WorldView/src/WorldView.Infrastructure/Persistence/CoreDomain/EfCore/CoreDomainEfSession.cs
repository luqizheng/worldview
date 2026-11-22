using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Persistence;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// Runs short-lived EF Core contexts on the package database's already-open physical
/// SQLite connection. Every context lifetime is contained within the existing connection
/// coordinator's serialization lock; EF never owns or closes the package connection.
/// </summary>
internal sealed class CoreDomainEfSession(SqliteConnectionAccess access)
{
    internal T Execute<T>(Func<CoreDomainDbContext, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return access.Execute(connection =>
        {
            var options = new DbContextOptionsBuilder<CoreDomainDbContext>()
                .UseSqlite(connection, contextOwnsConnection: false)
                // TEMPORARY: allow schema execution while the model/snapshot parity test
                // diagnoses EF-generated relational metadata. Remove before PR is marked Ready.
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .EnableDetailedErrors()
                .Options;
            using var context = new CoreDomainDbContext(options);
            return operation(context);
        });
    }

    internal void Execute(Action<CoreDomainDbContext> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Execute(context =>
        {
            operation(context);
            return true;
        });
    }

    internal void ApplyMigrations()
    {
        Execute(context =>
        {
            // EF's default migrator ignores history rows unknown to the current binary.
            // WorldView packages must fail closed on a database written by a newer schema version.
            var knownMigrations = context.Database.GetMigrations()
                .ToHashSet(StringComparer.Ordinal);
            using (var exists = context.Database.GetDbConnection().CreateCommand())
            {
                exists.CommandText = """
                    SELECT name FROM sqlite_master
                    WHERE type = 'table' AND name = '__EFMigrationsHistory'
                    LIMIT 1;
                    """;
                if (exists.ExecuteScalar() is string)
                {
                    using var history = context.Database.GetDbConnection().CreateCommand();
                    history.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";
                    using var reader = history.ExecuteReader();
                    var unknown = new List<string>();
                    while (reader.Read())
                    {
                        var migrationId = reader.GetString(0);
                        if (!knownMigrations.Contains(migrationId))
                            unknown.Add(migrationId);
                    }

                    if (unknown.Count != 0)
                        throw new CoreDomainDataException(
                            $"EF Core migration history contains unknown migration(s): {string.Join(", ", unknown)}. Refusing to open this database with an older schema model.");
                }
            }

            context.Database.Migrate();
            return true;
        });
    }
}
