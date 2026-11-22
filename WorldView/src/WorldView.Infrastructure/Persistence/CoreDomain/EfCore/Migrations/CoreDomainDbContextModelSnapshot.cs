using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <summary>
/// Frozen EF Core model snapshot for baseline V1. Update this snapshot only through
/// reviewed EF Core migrations; never share a mutable model-building helper with DbContext.
/// </summary>
[DbContext(typeof(CoreDomainDbContext))]
public sealed class CoreDomainDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity<CoreProjectEntity>(entity =>
        {
            entity.ToTable("core_project");
            entity.HasKey(x => x.ProjectId);
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectName).HasColumnName("project_name").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ProjectFormatVersion).HasColumnName("project_format_version").HasColumnType("INTEGER");
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.Property(x => x.UpdatedAtUtcMs).HasColumnName("updated_at_utc_ms").HasColumnType("INTEGER");
        });

        modelBuilder.Entity<CoreMapEntity>(entity =>
        {
            entity.ToTable("core_map");
            entity.HasKey(x => x.MapId);
            entity.Property(x => x.MapId).HasColumnName("map_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText);
            entity.Property(x => x.MapName).HasColumnName("map_name").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.LifecycleStatus).HasColumnName("lifecycle_status").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.Property(x => x.UpdatedAtUtcMs).HasColumnName("updated_at_utc_ms").HasColumnType("INTEGER");
            entity.HasIndex(x => new { x.ProjectId, x.MapId }).IsUnique();
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_core_map_core_project_project_id");
        });

        modelBuilder.Entity<CoreAssetEntity>(entity =>
        {
            entity.ToTable("core_asset", table =>
            {
                table.HasCheckConstraint("CK_core_asset_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.HasCheckConstraint("CK_core_asset_byte_length", "byte_length >= 0");
                table.HasCheckConstraint("CK_core_asset_publication_state", "publication_state IN ('published')");
                table.HasCheckConstraint("CK_core_asset_integrity_state", "integrity_state IN ('verified', 'unverified', 'corrupt', 'missing')");
            });
            entity.HasKey(x => x.AssetId);
            entity.Property(x => x.AssetId).HasColumnName("asset_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText);
            entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.HashAlgorithm).HasColumnName("hash_algorithm").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ByteLength).HasColumnName("byte_length").HasColumnType("INTEGER");
            entity.Property(x => x.MediaType).HasColumnName("media_type").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.FormatVersion).HasColumnName("format_version").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.RelativePath).HasColumnName("relative_path").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.PublicationState).HasColumnName("publication_state").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.IntegrityState).HasColumnName("integrity_state").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.HasIndex(x => new { x.ProjectId, x.AssetId }).IsUnique();
            entity.HasIndex(x => new { x.ProjectId, x.HashAlgorithm, x.ContentHash }).IsUnique();
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_core_asset_core_project_project_id");
        });
    }
}
