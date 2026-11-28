using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <summary>
/// Forward migration that adds the immutable baseline revision/revision-member/cursor
/// storage for time-zero authoring. The storage shape (including SQLite-only immutability
/// triggers) stays authored in <see cref="CoreDomainInitialSql.CoreMapInitialStateRevisionTablesSql"/>
/// so the EF migration and the hand-written DDL cannot drift apart.
/// </summary>
public partial class _20261128014741_CoreMapInitialStateRevisions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreDomainInitialSql.CoreMapInitialStateRevisionTablesSql);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "WorldView 1.0 schema is forward-only. Recreate an unreleased development database instead of downgrading.");
    }
}
