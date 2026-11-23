using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var differ = context.GetService<IMigrationsModelDiffer>();

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            designTimeModel.GetRelationalModel());

        static string Describe(MigrationOperation operation)
        {
            var details = operation.GetType().GetProperties()
                .Where(property => property.GetIndexParameters().Length == 0)
                .Select(property => (property.Name, Value: property.GetValue(operation)))
                .Where(item => item.Value is string or bool or int or long or string[])
                .Select(item => $"{item.Name}={(item.Value is string[] values ? string.Join("|", values) : item.Value)}");

            return $"{operation.GetType().Name}({string.Join(", ", details)})";
        }

        Assert.True(
            differences.Count == 0,
            $"EF Core found {differences.Count} pending relational model operation(s):{Environment.NewLine}{string.Join(Environment.NewLine, differences.Select(Describe))}");
    }
}
