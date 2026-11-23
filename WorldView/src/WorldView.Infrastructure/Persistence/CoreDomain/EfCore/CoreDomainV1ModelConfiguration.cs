using Microsoft.EntityFrameworkCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// Authoritative WorldView 1.0 runtime relational model configuration.
/// EF Core's generated ModelSnapshot records the design-time representation of this model.
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
            entity.ToTable("core_map", table =>
                table.HasCheckConstraint("CK_core_map_map_space_id", "length(map_space_id) BETWEEN 1 AND 128"));
            entity.HasKey(x => x.MapId);
            entity.Property(x => x.MapId).HasColumnName("map_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT")
                .HasConversion(CoreDomainValueConverters.GuidToText);
            entity.Property(x => x.MapSpaceId).HasColumnName("map_space_id").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.SpatialReferenceId).HasColumnName("spatial_reference_id").HasColumnType("TEXT");
            entity.Property(x => x.MapName).HasColumnName("map_name").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.LifecycleStatus).HasColumnName("lifecycle_status").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.Property(x => x.UpdatedAtUtcMs).HasColumnName("updated_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.MapId });
            entity.HasIndex(x => x.MapSpaceId).IsUnique().HasDatabaseName("ux_core_map_map_space_id");
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_core_map_core_project_project_id");
            entity.HasOne<CoreSpatialReferenceEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.SpatialReferenceId })
                .HasPrincipalKey(x => new { x.ProjectId, x.SpatialReferenceId })
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreProjectSettingsRevisionEntity>(entity =>
        {
            entity.ToTable("core_project_settings_revision", table =>
            {
                table.HasCheckConstraint("CK_core_project_settings_revision_version", "version >= 1");
                table.HasCheckConstraint("CK_core_project_settings_revision_schema", "schema_version >= 1");
                table.HasCheckConstraint("CK_core_project_settings_revision_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.HasCheckConstraint("CK_core_project_settings_revision_hash", "length(content_hash) = 64 AND content_hash NOT GLOB '*[^0-9a-f]*'");
                table.HasCheckConstraint("CK_core_project_settings_revision_length", "byte_length > 0");
                table.HasCheckConstraint("CK_core_project_settings_revision_path", "relative_path GLOB 'Settings/sha256/*'");
            });
            entity.HasKey(x => x.ProjectSettingsRevisionId);
            entity.Property(x => x.ProjectSettingsRevisionId).HasColumnName("project_settings_revision_id").HasColumnType("TEXT").HasConversion(guidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.Version).HasColumnName("version").HasColumnType("INTEGER");
            entity.Property(x => x.SchemaVersion).HasColumnName("schema_version").HasColumnType("INTEGER");
            entity.Property(x => x.RelativePath).HasColumnName("relative_path").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.HashAlgorithm).HasColumnName("hash_algorithm").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ByteLength).HasColumnName("byte_length").HasColumnType("INTEGER");
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.ProjectSettingsRevisionId });
            entity.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_core_project_settings_revision_core_project_project_id");
        });

        modelBuilder.Entity<CoreProjectSettingsCurrentEntity>(entity =>
        {
            entity.ToTable("core_project_settings_current");
            entity.HasKey(x => x.ProjectId);
            entity.HasIndex(x => new { x.ProjectId, x.ProjectSettingsRevisionId });
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.ProjectSettingsRevisionId).HasColumnName("project_settings_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_core_project_settings_current_core_project_project_id");
            entity.HasOne<CoreProjectSettingsRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.ProjectSettingsRevisionId })
                .HasPrincipalKey(x => new { x.ProjectId, x.ProjectSettingsRevisionId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_core_project_settings_current_project_settings_revision");
        });

        modelBuilder.Entity<CoreMapSettingsRevisionEntity>(entity =>
        {
            entity.ToTable("core_map_settings_revision", table =>
            {
                table.HasCheckConstraint("CK_core_map_settings_revision_version", "version >= 1");
                table.HasCheckConstraint("CK_core_map_settings_revision_schema", "schema_version >= 1");
                table.HasCheckConstraint("CK_core_map_settings_revision_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.HasCheckConstraint("CK_core_map_settings_revision_hash", "length(content_hash) = 64 AND content_hash NOT GLOB '*[^0-9a-f]*'");
                table.HasCheckConstraint("CK_core_map_settings_revision_length", "byte_length > 0");
                table.HasCheckConstraint("CK_core_map_settings_revision_path", "relative_path GLOB 'Settings/sha256/*'");
            });
            entity.HasKey(x => x.MapSettingsRevisionId);
            entity.Property(x => x.MapSettingsRevisionId).HasColumnName("map_settings_revision_id").HasColumnType("TEXT").HasConversion(guidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.MapId).HasColumnName("map_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.Version).HasColumnName("version").HasColumnType("INTEGER");
            entity.Property(x => x.SchemaVersion).HasColumnName("schema_version").HasColumnType("INTEGER");
            entity.Property(x => x.RelativePath).HasColumnName("relative_path").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.HashAlgorithm).HasColumnName("hash_algorithm").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ByteLength).HasColumnName("byte_length").HasColumnType("INTEGER");
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.MapId, x.MapSettingsRevisionId });
            entity.HasIndex(x => new { x.ProjectId, x.MapId, x.Version }).IsUnique();
            entity.HasOne<CoreMapEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.MapId })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_core_map_settings_revision_core_map_project_map");
        });

        modelBuilder.Entity<CoreMapSettingsCurrentEntity>(entity =>
        {
            entity.ToTable("core_map_settings_current");
            entity.HasKey(x => new { x.ProjectId, x.MapId });
            entity.HasIndex(x => new { x.ProjectId, x.MapId, x.MapSettingsRevisionId });
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.MapId).HasColumnName("map_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.MapSettingsRevisionId).HasColumnName("map_settings_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.HasOne<CoreMapEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.MapId })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_core_map_settings_current_core_map_project_map");
            entity.HasOne<CoreMapSettingsRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.MapId, x.MapSettingsRevisionId })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId, x.MapSettingsRevisionId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_core_map_settings_current_map_settings_revision");
        });

        modelBuilder.Entity<CoreSpatialReferenceEntity>(entity =>
        {
            entity.ToTable("core_spatial_reference", table =>
            {
                table.HasCheckConstraint("CK_core_spatial_reference_definition_version", "definition_version >= 1");
                table.HasCheckConstraint("CK_core_spatial_reference_kind", "reference_kind IN ('Geographic', 'Projected', 'LocalCartesian')");
                table.HasCheckConstraint("CK_core_spatial_reference_axis_order", "axis_order IN ('XY', 'YX')");
                table.HasCheckConstraint("CK_core_spatial_reference_dimension", "dimension IN (2, 3)");
                table.HasCheckConstraint("CK_core_spatial_reference_wrap", "wrap_behavior IN ('None', 'WrapX', 'WrapY', 'WrapXY')");
                table.HasCheckConstraint("CK_core_spatial_reference_precision_version", "precision_profile_version >= 1");
            });
            entity.HasKey(x => new { x.ProjectId, x.SpatialReferenceId });
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.SpatialReferenceId).HasColumnName("spatial_reference_id").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.DefinitionVersion).HasColumnName("definition_version").HasColumnType("INTEGER");
            entity.Property(x => x.ReferenceKind).HasColumnName("reference_kind").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CoordinateUnit).HasColumnName("coordinate_unit").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.AxisOrder).HasColumnName("axis_order").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.Dimension).HasColumnName("dimension").HasColumnType("INTEGER");
            entity.Property(x => x.ValidDomainJson).HasColumnName("valid_domain_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.WrapBehavior).HasColumnName("wrap_behavior").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.VerticalReferenceId).HasColumnName("vertical_reference_id").HasColumnType("TEXT");
            entity.Property(x => x.PrecisionProfileId).HasColumnName("precision_profile_id").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.PrecisionProfileVersion).HasColumnName("precision_profile_version").HasColumnType("INTEGER");
            entity.Property(x => x.PrecisionCoordinateUnit).HasColumnName("precision_coordinate_unit").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.PrecisionAxisOrder).HasColumnName("precision_axis_order").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.NumericRepresentation).HasColumnName("numeric_representation").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.MaximumAbsoluteQuantizationError).HasColumnName("maximum_absolute_quantization_error").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.RoundingMode).HasColumnName("rounding_mode").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.MinimumAuthoringTolerance).HasColumnName("minimum_authoring_tolerance").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.SmallestMeaningfulFeatureSize).HasColumnName("smallest_meaningful_feature_size").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.DefinitionParametersJson).HasColumnName("definition_parameters_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.MeasurementCapabilitiesJson).HasColumnName("measurement_capabilities_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ProvenanceJson).HasColumnName("provenance_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.DefinitionFingerprint).HasColumnName("definition_fingerprint").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
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
            entity.Property(x => x.ContentFingerprint).HasColumnName("content_fingerprint").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ManifestJson).HasColumnName("manifest_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ProvenanceJson).HasColumnName("provenance_json").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.Property(x => x.FieldKey).HasColumnName("field_key").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.DescriptorSchemaVersion).HasColumnName("descriptor_schema_version").HasColumnType("INTEGER").HasDefaultValue(0);
            entity.Property(x => x.SemanticsId).HasColumnName("semantics_id").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("TEXT");
            entity.Property(x => x.MapSpaceId).HasColumnName("map_space_id").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.SpatialReferenceId).HasColumnName("spatial_reference_id").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.CoordinatePrecisionProfileId).HasColumnName("coordinate_precision_profile_id").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.VerticalReferenceId).HasColumnName("vertical_reference_id").HasColumnType("TEXT");
            entity.Property(x => x.GridWidth).HasColumnName("grid_width").HasColumnType("INTEGER").HasDefaultValue(0L);
            entity.Property(x => x.GridHeight).HasColumnName("grid_height").HasColumnType("INTEGER").HasDefaultValue(0L);
            entity.Property(x => x.GridTileWidth).HasColumnName("grid_tile_width").HasColumnType("INTEGER").HasDefaultValue(0);
            entity.Property(x => x.GridTileHeight).HasColumnName("grid_tile_height").HasColumnType("INTEGER").HasDefaultValue(0);
            entity.Property(x => x.GridOriginX).HasColumnName("grid_origin_x").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.GridOriginY).HasColumnName("grid_origin_y").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.GridSpacingX).HasColumnName("grid_spacing_x").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.GridSpacingY).HasColumnName("grid_spacing_y").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.GridAxisOrientation).HasColumnName("grid_axis_orientation").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.GridCellRegistration).HasColumnName("grid_cell_registration").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.CoverageMode).HasColumnName("coverage_mode").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.CoverageMaskPlaneName).HasColumnName("coverage_mask_plane_name").HasColumnType("TEXT");
            entity.Property(x => x.MapEdgeSemantics).HasColumnName("map_edge_semantics").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.NoDataPolicy).HasColumnName("no_data_policy").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.NoDataMaskPlaneName).HasColumnName("no_data_mask_plane_name").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.NoDataValidValue).HasColumnName("no_data_valid_value").HasColumnType("INTEGER").HasDefaultValue(0);
            entity.Property(x => x.NoDataInvalidValue).HasColumnName("no_data_invalid_value").HasColumnType("INTEGER").HasDefaultValue(0);
            entity.Property(x => x.ValueProfileId).HasColumnName("value_profile_id").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.ValueProfileScope).HasColumnName("value_profile_scope").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.TileHashAlgorithm).HasColumnName("tile_hash_algorithm").HasColumnType("TEXT").IsRequired().HasDefaultValue("");
            entity.Property(x => x.TileHashVersion).HasColumnName("tile_hash_version").HasColumnType("INTEGER").HasDefaultValue(0);
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId });
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldDatasetId, x.RevisionNumber });
            entity.HasOne<CoreMapFieldDatasetEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldDatasetId })
                .HasPrincipalKey(x => new { x.ProjectId, x.FieldDatasetId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldDatasetId, x.ParentRevisionId })
                .HasPrincipalKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId })
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapFieldRevisionPlaneEntity>(entity =>
        {
            entity.ToTable("core_map_field_revision_plane", table =>
            {
                table.HasCheckConstraint("CK_core_map_field_revision_plane_ordinal", "ordinal >= 0");
                table.HasCheckConstraint("CK_core_map_field_revision_plane_bytes", "bytes_per_cell > 0");
            });
            entity.HasKey(x => new { x.FieldRevisionId, x.Ordinal });
            entity.Property(x => x.FieldRevisionId).HasColumnName("field_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.Ordinal).HasColumnName("ordinal").HasColumnType("INTEGER");
            entity.Property(x => x.Name).HasColumnName("name").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.SampleFormat).HasColumnName("sample_format").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.BytesPerCell).HasColumnName("bytes_per_cell").HasColumnType("INTEGER");
            entity.HasIndex(x => new { x.FieldRevisionId, x.Name }).IsUnique().HasDatabaseName("ux_core_map_field_revision_plane_name");
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany().HasForeignKey(x => x.FieldRevisionId)
                .HasPrincipalKey(x => x.FieldRevisionId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapFieldRevisionCategoryCodeEntity>(entity =>
        {
            entity.ToTable("core_map_field_revision_category_code");
            entity.HasKey(x => new { x.FieldRevisionId, x.CategoryKey });
            entity.Property(x => x.FieldRevisionId).HasColumnName("field_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.CategoryKey).HasColumnName("category_key").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CategoryCode).HasColumnName("category_code").HasColumnType("INTEGER");
            entity.HasIndex(x => new { x.FieldRevisionId, x.CategoryCode }).IsUnique().HasDatabaseName("ux_core_map_field_revision_category_code_value");
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany().HasForeignKey(x => x.FieldRevisionId)
                .HasPrincipalKey(x => x.FieldRevisionId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapFieldRevisionCodecEntity>(entity =>
        {
            entity.ToTable("core_map_field_revision_codec", table =>
                table.HasCheckConstraint("CK_core_map_field_revision_codec_ordinal", "ordinal >= 0"));
            entity.HasKey(x => new { x.FieldRevisionId, x.Ordinal });
            entity.Property(x => x.FieldRevisionId).HasColumnName("field_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.Ordinal).HasColumnName("ordinal").HasColumnType("INTEGER");
            entity.Property(x => x.CodecId).HasColumnName("codec_id").HasColumnType("INTEGER");
            entity.Property(x => x.CodecName).HasColumnName("codec_name").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ParametersJson).HasColumnName("parameters_json").HasColumnType("TEXT").IsRequired();
            entity.HasIndex(x => new { x.FieldRevisionId, x.CodecId }).IsUnique().HasDatabaseName("ux_core_map_field_revision_codec_id");
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany().HasForeignKey(x => x.FieldRevisionId)
                .HasPrincipalKey(x => x.FieldRevisionId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreFieldPayloadEntity>(entity =>
        {
            entity.ToTable("core_field_payload", table =>
            {
                table.HasCheckConstraint("CK_core_field_payload_storage_kind",
                    "storage_kind IN ('sqlite_blob', 'managed_file')");
                table.HasCheckConstraint("CK_core_field_payload_storage_location",
                    "(storage_kind = 'sqlite_blob' AND payload_blob IS NOT NULL AND relative_path IS NULL) OR (storage_kind = 'managed_file' AND payload_blob IS NULL AND relative_path IS NOT NULL)");
                table.HasCheckConstraint("CK_core_field_payload_blob_length",
                    "storage_kind <> 'sqlite_blob' OR length(payload_blob) = byte_length");
                table.HasCheckConstraint("CK_core_field_payload_byte_length", "byte_length > 0");
                table.HasCheckConstraint("CK_core_field_payload_hash",
                    "length(content_hash) = 64 AND content_hash NOT GLOB '*[^0-9a-f]*'");
                table.HasCheckConstraint("CK_core_field_payload_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.HasCheckConstraint("CK_core_field_payload_integrity",
                    "integrity_state IN ('verified', 'corrupt', 'missing')");
            });
            entity.HasKey(x => x.FieldPayloadId);
            entity.Property(x => x.FieldPayloadId).HasColumnName("field_payload_id").HasColumnType("TEXT").HasConversion(guidToText).ValueGeneratedNever();
            entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.FieldDatasetId).HasColumnName("field_dataset_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.FieldRevisionId).HasColumnName("field_revision_id").HasColumnType("TEXT").HasConversion(guidToText);
            entity.Property(x => x.StorageKind).HasColumnName("storage_kind").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.PayloadFormat).HasColumnName("payload_format").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.FormatVersion).HasColumnName("format_version").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.HashAlgorithm).HasColumnName("hash_algorithm").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.ByteLength).HasColumnName("byte_length").HasColumnType("INTEGER");
            entity.Property(x => x.PayloadBlob).HasColumnName("payload_blob").HasColumnType("BLOB");
            entity.Property(x => x.RelativePath).HasColumnName("relative_path").HasColumnType("TEXT");
            entity.Property(x => x.IntegrityState).HasColumnName("integrity_state").HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.CreatedAtUtcMs).HasColumnName("created_at_utc_ms").HasColumnType("INTEGER");
            entity.HasAlternateKey(x => new { x.ProjectId, x.FieldPayloadId });
            entity.HasIndex(x => x.FieldRevisionId).IsUnique().HasDatabaseName("ux_core_field_payload_revision");
            entity.HasOne<CoreMapFieldRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId })
                .HasPrincipalKey(x => new { x.ProjectId, x.FieldDatasetId, x.FieldRevisionId })
                .OnDelete(DeleteBehavior.NoAction);
        });

        CoreDomainV1AggregateModelConfiguration.Configure(modelBuilder);
    }
}
