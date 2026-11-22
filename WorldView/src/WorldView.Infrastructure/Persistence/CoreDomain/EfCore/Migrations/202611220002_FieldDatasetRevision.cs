using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <summary>
/// Creates Field Dataset / immutable Revision tables. The Dataset current-pointer FK is
/// deliberately deferred and therefore created with reviewed SQLite SQL inside this EF migration.
/// </summary>
[DbContext(typeof(CoreDomainDbContext))]
[Migration("202611220002_FieldDatasetRevision")]
internal sealed class FieldDatasetRevision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE core_map_field_dataset (
                field_dataset_id          TEXT    NOT NULL PRIMARY KEY,
                project_id                TEXT    NOT NULL,
                map_id                    TEXT    NOT NULL,
                field_key                 TEXT    NOT NULL CHECK (field_key IN ('elevation', 'land_water')),
                descriptor_schema_version INTEGER NOT NULL CHECK (descriptor_schema_version >= 1),
                semantics_id              TEXT    NOT NULL,
                current_field_revision_id TEXT    NULL,
                created_at_utc_ms          INTEGER NOT NULL,
                updated_at_utc_ms         INTEGER NOT NULL,
                UNIQUE (project_id, field_dataset_id),
                UNIQUE (project_id, map_id, field_key),
                FOREIGN KEY (project_id, map_id)
                    REFERENCES core_map (project_id, map_id),
                FOREIGN KEY (project_id, field_dataset_id, current_field_revision_id)
                    REFERENCES core_map_field_revision (project_id, field_dataset_id, field_revision_id)
                    DEFERRABLE INITIALLY DEFERRED
            );
            """);

        migrationBuilder.Sql("""
            CREATE TABLE core_map_field_revision (
                field_revision_id       TEXT    NOT NULL PRIMARY KEY,
                project_id              TEXT    NOT NULL,
                field_dataset_id        TEXT    NOT NULL,
                revision_number         INTEGER NOT NULL CHECK (revision_number >= 1),
                parent_revision_id      TEXT    NULL,
                field_revision_asset_id TEXT    NOT NULL,
                content_fingerprint     TEXT    NOT NULL CHECK (
                    length(content_fingerprint) = 64
                    AND content_fingerprint NOT GLOB '*[^0-9a-f]*'
                ),
                manifest_json           TEXT    NOT NULL,
                provenance_json         TEXT    NOT NULL,
                created_at_utc_ms       INTEGER NOT NULL,
                UNIQUE (project_id, field_dataset_id, field_revision_id),
                UNIQUE (project_id, field_dataset_id, revision_number),
                FOREIGN KEY (project_id, field_dataset_id)
                    REFERENCES core_map_field_dataset (project_id, field_dataset_id),
                FOREIGN KEY (project_id, field_revision_asset_id)
                    REFERENCES core_asset (project_id, asset_id),
                FOREIGN KEY (project_id, field_dataset_id, parent_revision_id)
                    REFERENCES core_map_field_revision (project_id, field_dataset_id, field_revision_id)
            );

            CREATE INDEX ix_core_map_field_revision_asset
                ON core_map_field_revision (project_id, field_revision_asset_id);

            CREATE TRIGGER trg_core_map_field_revision_immutable_update
            BEFORE UPDATE ON core_map_field_revision
            BEGIN
                SELECT RAISE(ABORT, 'core_map_field_revision: published revisions are immutable.');
            END;

            CREATE TRIGGER trg_core_map_field_revision_immutable_delete
            BEFORE DELETE ON core_map_field_revision
            BEGIN
                SELECT RAISE(ABORT, 'core_map_field_revision: published revisions are retained and immutable.');
            END;

            CREATE TRIGGER trg_core_map_field_dataset_pin_immutable
            BEFORE UPDATE OF project_id, map_id, field_key, descriptor_schema_version, semantics_id
            ON core_map_field_dataset
            BEGIN
                SELECT RAISE(ABORT, 'core_map_field_dataset: owner and descriptor pin are immutable.');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_core_map_field_dataset_pin_immutable;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_core_map_field_revision_immutable_delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_core_map_field_revision_immutable_update;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS core_map_field_dataset;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS core_map_field_revision;");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);

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
            entity.HasIndex(x => new { x.ProjectId, x.HashAlgorithm, x.ContentHash }).IsUnique();
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
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText).IsConcurrencyToken();
            entity.Property(x => x.MapId).HasColumnName("map_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.FieldKey).HasColumnName("field_key").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.DescriptorSchemaVersion).HasColumnName("descriptor_schema_version").HasColumnType("INTEGER");
            entity.Property(x => x.SemanticsId).HasColumnName("semantics_id").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CurrentFieldRevisionId).HasColumnName("current_field_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.Property(x => x.UpdatedAtUtcMs).HasColumnName("updated_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldDatasetId });
            entity.HasIndex(x => new { x.ProjectId, x.MapId, x.FieldKey }).IsUnique();
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
            entity.HasIndex(x => new { x.ProjectId, x.FieldDatasetId, x.RevisionNumber }).IsUnique();
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
    }
}
