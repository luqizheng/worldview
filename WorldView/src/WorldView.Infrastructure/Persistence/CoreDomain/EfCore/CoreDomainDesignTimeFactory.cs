using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// Design-time construction for `dotnet ef`. Runtime code must use CoreDomainEfSession
/// so it shares the package database connection coordinator.
/// </summary>
public sealed class CoreDomainDesignTimeFactory : IDesignTimeDbContextFactory<CoreDomainDbContext>
{
    public CoreDomainDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CoreDomainDbContext>()
            .UseSqlite("Data Source=worldview-efcore-design-time.db")
            .Options;
        return new CoreDomainDbContext(options);
    }
}
