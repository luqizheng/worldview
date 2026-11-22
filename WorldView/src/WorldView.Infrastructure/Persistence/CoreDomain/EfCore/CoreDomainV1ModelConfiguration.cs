using Microsoft.EntityFrameworkCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// WorldView 1.0 runtime relational model configuration.
/// This is deliberately separate from the mutable runtime model configuration.
/// </summary>
internal static class CoreDomainV1ModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);
        var guidToText = CoreDomainValueConverters.GuidToText;

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
            entity.HasAlternateKey(x => new { x.ProjectId, x.MapId });
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
            entity.HasAlternateKey(x => new { x.ProjectId, x.AssetId });
            entity.HasAlternateKey(x => new { x.ProjectId, x.HashAlgorithm, x.ContentHash });
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_core_asset_core_project_project_id");
        });

        modelBuilder.Entity<CoreMapFieldDatasetEntity>(entity =>
        {
            entity.ToTable("core_map_field_dataset", table =>
            {
                table.HasCheckConstraint("CK_core_map_field_dataset_field_key", "field_key IN ('elevation', 'land_water')");
                table.HasCheckConstraint("CK_core_map_field_dataset_schema_version", "descriptor_schema_version >= 1");
            });
            entity.HasKey(x => x.FieldDatasetId);
            entity.Property(x => x.FieldDatasetId).HasColumnName("field_dataset_id").HasColumnType("TEXT").HasConversion(guidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.MapId).HasColumnName("map_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.FieldKey).HasColumnName("field_key").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.DescriptorSchemaVersion).HasColumnName("descriptor_schema_version").HasColumnType("INTEGER");
            entity.Property(x => x.SemanticsId).HasColumnName("semantics_id").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CurrentFieldRevisionId).HasColumnName("current_field_revision_id").HasColumnType("TEXT").HasConversion(guidToText).IsConcurrencyToken();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.Property(x => x.UpdatedAtUtcMs).HasColumnName("updated_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldDatasetId });
            entity.HasAlternateKey(x => new { x.ProjectId, x.MapId, x.FieldKey });
            entity.HasOne<CoreMapEntity>().WithMany().HasForeignKey(x => new { x.ProjectId, x.MapId })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldDatasetId, x.CurrentFieldRevisionId })
                .HasPrincipalKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId })
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapFieldRevisionEntity>(entity =>
        {
            entity.ToTable("core_map_field_revision", table =>
            {
                table.HasCheckConstraint("CK_core_map_field_revision_number", "revision_number >= 1");
                table.HasCheckConstraint("CK_core_map_field_revision_fingerprint",
                    "length(content_fingerprint) = 64 AND content_fingerprint NOT GLOB '*[^0-9a-f]*'");
            });
            entity.HasKey(x => x.FieldRevisionId);
            entity.Property(x => x.FieldRevisionId).HasColumnName("field_revision_id").HasColumnType("TEXT").HasConversion(guidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.FieldDatasetId).HasColumnName("field_dataset_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.RevisionNumber).HasColumnName("revision_number").HasColumnType("INTEGER");
            entity.Property(x => x.ParentRevisionId).HasColumnName("parent_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.FieldRevisionAssetId).HasColumnName("field_revision_asset_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.ContentFingerprint).HasColumnName("content_fingerprint").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ManifestJson).HasColumnName("manifest_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ProvenanceJson).HasColumnName("provenance_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId });
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldDatasetId, x.RevisionNumber });
            entity.HasIndex(x => new { x.ProjectId, x.FieldRevisionAssetId }).HasDatabaseName("ix_core_map_field_revision_asset");
            entity.HasOne<CoreMapFieldDatasetEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldDatasetId })
                .HasPrincipalKey(x => new { x.ProjectId, x.FieldDatasetId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CoreAssetEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldRevisionAssetId })
                .HasPrincipalKey(x => new { x.ProjectId, x.AssetId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldDatasetId, x.ParentRevisionId })
                .HasPrincipalKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId })
                .OnDelete(DeleteBehavior.NoAction);
        });
        CoreDomainV1ModelSnapshotConfiguration.Configure(modelBuilder);
    }
}
