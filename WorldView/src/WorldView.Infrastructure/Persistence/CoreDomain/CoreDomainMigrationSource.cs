using System.Collections.Generic;
using WorldView.Application.Bootstrap;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// Compatibility-free placeholder retained temporarily for existing test composition roots.
/// Production 1.0 composition does not register this source; Core Domain schema creation is
/// exclusively performed by CoreDomainDbContext EF migrations.
/// </summary>
[System.Obsolete("Core Domain SQL bootstrap migrations are retired; use CoreDomainDbContext EF migrations.")]
public sealed class CoreDomainMigrationSource : IMigrationSource
{
    public IReadOnlyList<ModuleMigration> DiscoverMigrations() => [];
}
