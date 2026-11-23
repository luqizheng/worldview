using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// Supplies a provider-backed context to the EF Core CLI. Runtime composition continues
/// to use the package database's single shared SQLite connection.
/// </summary>
public sealed class CoreDomainDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<CoreDomainDbContext>
{
    public CoreDomainDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CoreDomainDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new CoreDomainDbContext(options);
    }
}
