using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;
using Xunit;
using Xunit.Abstractions;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

public sealed class CoreDomainModelSnapshotDiagnosticsTests(ITestOutputHelper output)
{
    [Fact]
    public void RuntimeModel_MatchesLastMigrationSnapshot()
    {
        var options = new DbContextOptionsBuilder<CoreDomainDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new CoreDomainDbContext(options);
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var snapshot = Assert.IsType<CoreDomainDbContextModelSnapshot>(migrationsAssembly.ModelSnapshot);
        var snapshotModel = snapshot.Model;
        var runtimeModel = context.GetService<IDesignTimeModel>().Model;
        var differ = context.GetService<IMigrationsModelDiffer>();

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            runtimeModel.GetRelationalModel());

        foreach (var difference in differences)
            output.WriteLine(difference.ToString());

        Assert.True(
            differences.Count == 0,
            $"EF Core found {differences.Count} pending relational model operation(s). See test output for exact differences.");
    }
}
