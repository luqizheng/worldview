using Microsoft.EntityFrameworkCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <summary>
/// Frozen WorldView 1.0 relational model used by the initial migration and model snapshot.
/// This is deliberately separate from the mutable runtime model configuration.
/// </summary>
internal static class CoreDomainV1FullModelSnapshotConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        // Keep provider metadata that EF's generated SQLite snapshots include.
        // Without this, the design-time snapshot and runtime relational model differ
        // even when all entity mappings are otherwise identical, causing
        // PendingModelChangesWarning on every fresh database migration.
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);
        var guidToText = CoreDomainValueConverters.GuidToText;
