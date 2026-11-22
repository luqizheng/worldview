using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// Explicit relational mappings for the Core Domain aggregates that were previously
/// represented only by SQLite DDL. Column names are already snake_case on storage shapes.
/// Database-only CHECK/trigger invariants remain authored by the initial migration.
/// </summary>
internal static class CoreDomainAggregateModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoreHistoryEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_history", t =>
            {
                t.HasCheckConstraint("CK_core_history_kind", "history_kind IN ('map_root', 'branch_owned')");
                t.HasCheckConstraint("CK_core_history_owner_kind",
                    "(history_kind = 'map_root' AND owner_map_id IS NOT NULL) OR (history_kind = 'branch_owned' AND owner_map_id IS NULL)");
            });
            e.HasKey(x => x.history_id);
            e.HasAlternateKey(x => new { x.project_id, x.history_id });
            e.HasIndex(x => new { x.project_id, x.owner_map_id }).IsUnique();
            e.Property(x => x.history_id).HasColumnType("TEXT");
            e.Property(x => x.project_id).HasColumnType("TEXT");
            e.Property(x => x.history_kind).HasColumnType("TEXT");
            e.Property(x => x.owner_map_id).HasColumnType("TEXT");
            e.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.project_id).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreMapEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.owner_map_id })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreBranchEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_branch", t =>
            {
                t.HasCheckConstraint("CK_core_branch_parent_time", "parent_day_number >= 0 AND parent_offset_seconds BETWEEN 0 AND 86399");
                t.HasCheckConstraint("CK_core_branch_distinct_histories", "parent_history_id <> own_history_id");
            });
            e.HasKey(x => x.branch_id);
            e.HasAlternateKey(x => new { x.project_id, x.branch_id });
            e.HasAlternateKey(x => new { x.project_id, x.own_history_id });
            e.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.project_id).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreHistoryEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.parent_history_id })
                .HasPrincipalKey(x => new { x.project_id, x.history_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreHistoryEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.own_history_id })
                .HasPrincipalKey(x => new { x.project_id, x.history_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreCalendarEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_calendar", t => t.HasCheckConstraint("CK_core_calendar_role", "calendar_role IN ('default', 'auxiliary')"));
            e.HasKey(x => x.calendar_id);
            e.HasAlternateKey(x => new { x.project_id, x.calendar_id });
            e.HasAlternateKey(x => new { x.project_id, x.map_id, x.calendar_id });
            e.HasIndex(x => new { x.project_id, x.map_id }).IsUnique()
                .HasFilter("calendar_role = 'default'").HasDatabaseName("ux_core_calendar_default_per_map");
            e.HasOne<CoreMapEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.map_id })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreCalendarConfigurationEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_calendar_configuration", t =>
            {
                t.HasCheckConstraint("CK_core_calendar_configuration_origin",
                    "(configuration_origin = 'user_created' AND source_command_id IS NULL) OR (configuration_origin = 'command_managed' AND source_command_id IS NOT NULL)");
                t.HasCheckConstraint("CK_core_calendar_configuration_until_pair",
                    "(effective_until_day_number IS NULL AND effective_until_offset_seconds IS NULL) OR (effective_until_day_number IS NOT NULL AND effective_until_offset_seconds IS NOT NULL)");
                t.HasCheckConstraint("CK_core_calendar_configuration_from",
                    "effective_from_day_number >= 0 AND effective_from_offset_seconds BETWEEN 0 AND 86399");
                t.HasCheckConstraint("CK_core_calendar_configuration_until",
                    "effective_until_day_number IS NULL OR (effective_until_day_number >= 0 AND effective_until_offset_seconds BETWEEN 0 AND 86399)");
            });
            e.HasKey(x => x.calendar_configuration_id);
            e.HasAlternateKey(x => new { x.project_id, x.calendar_configuration_id });
            e.HasAlternateKey(x => new { x.project_id, x.map_id, x.calendar_id, x.owner_history_id, x.effective_from_day_number, x.effective_from_offset_seconds });
            e.HasOne<CoreCalendarEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.map_id, x.calendar_id })
                .HasPrincipalKey(x => new { x.project_id, x.map_id, x.calendar_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreHistoryEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.owner_history_id })
                .HasPrincipalKey(x => new { x.project_id, x.history_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreObjectTypeEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_object_type", t => t.HasCheckConstraint("CK_core_object_type_allocatable", "allocatable IN (0, 1)"));
            e.HasKey(x => x.object_type_id);
            e.HasAlternateKey(x => new { x.project_id, x.object_type_id });
            e.HasAlternateKey(x => new { x.project_id, x.type_key });
            e.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.project_id).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreObjectEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_object", t =>
            {
                t.HasCheckConstraint("CK_core_object_scope", "sharing_scope IN ('project_shared', 'map_local')");
                t.HasCheckConstraint("CK_core_object_lifecycle", "lifecycle_status IN ('draft', 'active', 'abandoned', 'retired')");
                t.HasCheckConstraint("CK_core_object_scope_owner",
                    "(sharing_scope = 'project_shared' AND owner_map_id IS NULL) OR (sharing_scope = 'map_local' AND owner_map_id IS NOT NULL)");
            });
            e.HasKey(x => x.object_identity_id);
            e.HasAlternateKey(x => new { x.project_id, x.object_identity_id });
            e.HasAlternateKey(x => new { x.project_id, x.object_type_id, x.object_identity_id });
            e.HasOne<CoreProjectEntity>().WithMany().HasForeignKey(x => x.project_id).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreObjectTypeEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_type_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_type_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreMapEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.owner_map_id })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreObjectDefinitionEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_object_definition");
            e.HasKey(x => new { x.project_id, x.object_identity_id });
            e.HasOne<CoreObjectEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_identity_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_identity_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreObjectIdAllocatorEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_object_id_allocator", t => t.HasCheckConstraint("CK_core_object_id_allocator_sequence", "next_sequence >= 1"));
            e.HasKey(x => new { x.project_id, x.object_type_id });
            e.HasOne<CoreObjectTypeEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_type_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_type_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreObjectIdReservationEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_object_id_reservation", t =>
            {
                t.HasCheckConstraint("CK_core_object_id_reservation_sequence", "allocated_sequence >= 1");
                t.HasCheckConstraint("CK_core_object_id_reservation_state",
                    "(reservation_state = 'Reserved' AND object_identity_id IS NULL) OR (reservation_state IN ('Bound', 'Inserted') AND object_identity_id IS NOT NULL) OR reservation_state = 'Abandoned'");
                t.HasCheckConstraint("CK_core_object_id_reservation_abandon_reason", "abandon_reason IS NULL OR reservation_state = 'Abandoned'");
            });
            e.HasKey(x => x.reservation_id);
            e.HasAlternateKey(x => new { x.project_id, x.reservation_id });
            e.HasAlternateKey(x => new { x.project_id, x.object_type_id, x.allocated_sequence });
            e.HasAlternateKey(x => new { x.project_id, x.object_type_id, x.allocated_id_value });
            e.HasIndex(x => new { x.project_id, x.request_key }).IsUnique();
            e.HasIndex(x => new { x.project_id, x.object_type_id, x.object_identity_id }).IsUnique();
            e.HasOne<CoreObjectTypeEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_type_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_type_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreObjectEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_type_id, x.object_identity_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_type_id, x.object_identity_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapObjectRefEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_map_object_ref");
            e.HasKey(x => x.map_object_ref_id);
            e.HasAlternateKey(x => new { x.project_id, x.map_object_ref_id });
            e.HasAlternateKey(x => new { x.project_id, x.map_id, x.object_identity_id });
            e.HasOne<CoreMapEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.map_id })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreObjectEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.object_identity_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_identity_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapInitialStateEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_map_initial_state");
            e.HasKey(x => x.initial_state_id);
            e.HasAlternateKey(x => new { x.project_id, x.initial_state_id });
            e.HasAlternateKey(x => new { x.project_id, x.map_id, x.initial_state_id });
            e.HasAlternateKey(x => new { x.project_id, x.map_id });
            e.HasOne<CoreMapEntity>().WithMany().HasForeignKey(x => new { x.project_id, x.map_id })
                .HasPrincipalKey(x => new { x.ProjectId, x.MapId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreMapInitialObjectEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_map_initial_object", t => t.HasCheckConstraint("CK_core_map_initial_object_placement_pair",
                "(initial_placement_payload_json IS NULL AND initial_placement_schema_version IS NULL) OR (initial_placement_payload_json IS NOT NULL AND initial_placement_schema_version IS NOT NULL)"));
            e.HasKey(x => new { x.initial_state_id, x.object_identity_id });
            e.HasOne<CoreMapInitialStateEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.map_id, x.initial_state_id })
                .HasPrincipalKey(x => new { x.project_id, x.map_id, x.initial_state_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreMapObjectRefEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.map_id, x.object_identity_id })
                .HasPrincipalKey(x => new { x.project_id, x.map_id, x.object_identity_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreObjectEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_identity_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_identity_id }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CoreObjectDefinitionAssetEntity>(e =>
        {
            ConfigureGuidText(e);
            e.ToTable("core_object_definition_asset");
            e.HasKey(x => x.object_definition_asset_id);
            e.HasAlternateKey(x => new { x.project_id, x.object_definition_asset_id });
            e.HasAlternateKey(x => new { x.project_id, x.object_identity_id, x.asset_id, x.role });
            e.HasOne<CoreObjectEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.object_identity_id })
                .HasPrincipalKey(x => new { x.project_id, x.object_identity_id }).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CoreAssetEntity>().WithMany()
                .HasForeignKey(x => new { x.project_id, x.asset_id })
                .HasPrincipalKey(x => new { x.ProjectId, x.AssetId }).OnDelete(DeleteBehavior.NoAction);
        });
    }
    
    private static void ConfigureGuidText<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        foreach (var property in typeof(TEntity).GetProperties())
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type == typeof(Guid))
            {
                entity.Property(property.Name)
                    .HasColumnType("TEXT")
                    .HasConversion(CoreDomainValueConverters.GuidToText);
            }
        }
    }

}
