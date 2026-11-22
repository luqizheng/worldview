using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

public sealed class CoreDomainModelSnapshotDiagnosticsTests
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
        var snapshotModel = context.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshot.Model, designTime: true);
        var runtimeModel = context.Model;
        var differ = context.GetService<IMigrationsModelDiffer>();

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            runtimeModel.GetRelationalModel());

        Assert.True(
            differences.Count == 0,
            $"EF Core found {differences.Count} pending relational model operation(s): {string.Join(Environment.NewLine, differences.Select(d => d.ToString()))}");
    }
}
