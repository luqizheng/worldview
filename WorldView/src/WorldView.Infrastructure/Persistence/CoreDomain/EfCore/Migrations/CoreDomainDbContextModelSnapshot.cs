using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

[DbContext(typeof(CoreDomainDbContext))]
public sealed class CoreDomainDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        CoreDomainV1FullModelSnapshotConfiguration.Configure(modelBuilder);
    }
}
