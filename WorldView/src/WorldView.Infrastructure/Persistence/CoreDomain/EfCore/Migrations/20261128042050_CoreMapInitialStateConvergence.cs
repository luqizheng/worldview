using Microsoft.EntityFrameworkCore.Migrations;
using WorldView.Domain.Objects;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <summary>
/// FOUNDATION-01B §9 convergence: retires the mutable baseline/member storage and
/// converts existing development data into the immutable revision/cursor model.
/// Executed in three ordered scripts: preparation (preserve + drop mutable storage,
/// rebuild the baseline identity table to its stable shape), revision storage creation
/// (<see cref="CoreDomainInitialSql.CoreMapInitialStateRevisionTablesSql"/>, identical
/// DDL to the interim migration so fresh and existing databases converge on one shape),
/// and backfill (<see cref="CoreDomainInitialSql.CoreMapInitialStateConvergenceBackfillSql"/>).
/// The empty revision-zero fingerprint constant is owned by Domain
/// (<see cref="MapInitialStateService.EmptySnapshotFingerprint"/>).
/// </summary>
public partial class _20261128042050_CoreMapInitialStateConvergence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreDomainInitialSql.CoreMapInitialStateConvergencePreparationSql);
        migrationBuilder.Sql(CoreDomainInitialSql.CoreMapInitialStateRevisionTablesSql);
        migrationBuilder.Sql(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            CoreDomainInitialSql.CoreMapInitialStateConvergenceBackfillSql,
            MapInitialStateService.EmptySnapshotFingerprint));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "WorldView 1.0 schema is forward-only. Recreate an unreleased development database instead of downgrading.");
    }
}
