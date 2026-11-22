using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

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
            context.Database.Migrate();
            return true;
        });
    }
}
