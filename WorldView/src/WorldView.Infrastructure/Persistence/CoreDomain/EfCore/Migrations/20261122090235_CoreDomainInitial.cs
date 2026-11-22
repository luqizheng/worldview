using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261122090235_CoreDomainInitial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "core_project",
            columns: table => new
            {
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                project_name = table.Column<string>(type: "TEXT", nullable: false),
                project_format_version = table.Column<long>(type: "INTEGER", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_project", x => x.project_id);
            });

        migrationBuilder.CreateTable(
            name: "core_asset",
            columns: table => new
            {
                asset_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                hash_algorithm = table.Column<string>(type: "TEXT", nullable: false),
                byte_length = table.Column<long>(type: "INTEGER", nullable: false),
                media_type = table.Column<string>(type: "TEXT", nullable: false),
                format_version = table.Column<string>(type: "TEXT", nullable: false),
                relative_path = table.Column<string>(type: "TEXT", nullable: false),
                publication_state = table.Column<string>(type: "TEXT", nullable: false),
                integrity_state = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_asset", x => x.asset_id);
                table.UniqueConstraint("AK_core_asset_project_id_asset_id", x => new { x.project_id, x.asset_id });
                table.UniqueConstraint("AK_core_asset_project_id_hash_algorithm_content_hash", x => new { x.project_id, x.hash_algorithm, x.content_hash });
                table.CheckConstraint("CK_core_asset_byte_length", "byte_length >= 0");
                table.CheckConstraint("CK_core_asset_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.CheckConstraint("CK_core_asset_integrity_state", "integrity_state IN ('verified', 'unverified', 'corrupt', 'missing')");
                table.CheckConstraint("CK_core_asset_publication_state", "publication_state IN ('published')");
                table.ForeignKey(
                    name: "FK_core_asset_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_map",
            columns: table => new
            {
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_name = table.Column<string>(type: "TEXT", nullable: false),
                lifecycle_status = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map", x => x.map_id);
                table.UniqueConstraint("AK_core_map_project_id_map_id", x => new { x.project_id, x.map_id });
                table.ForeignKey(
                    name: "FK_core_map_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_object_type",
            columns: table => new
            {
                object_type_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                type_key = table.Column<string>(type: "TEXT", nullable: false),
                display_name = table.Column<string>(type: "TEXT", nullable: false),
                allocatable = table.Column<long>(type: "INTEGER", nullable: false),
                allocation_format_json = table.Column<string>(type: "TEXT", nullable: false),
                allocation_format_version = table.Column<long>(type: "INTEGER", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_object_type", x => x.object_type_id);
                table.UniqueConstraint("AK_core_object_type_project_id_object_type_id", x => new { x.project_id, x.object_type_id });
                table.UniqueConstraint("AK_core_object_type_project_id_type_key", x => new { x.project_id, x.type_key });
                table.CheckConstraint("CK_core_object_type_allocatable", "allocatable IN (0, 1)");
                table.ForeignKey(
                    name: "FK_core_object_type_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_calendar",
            columns: table => new
            {
                calendar_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                calendar_role = table.Column<string>(type: "TEXT", nullable: false),
                calendar_type = table.Column<string>(type: "TEXT", nullable: true),
                calendar_name = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_calendar", x => x.calendar_id);
                table.UniqueConstraint("AK_core_calendar_project_id_calendar_id", x => new { x.project_id, x.calendar_id });
                table.UniqueConstraint("AK_core_calendar_project_id_map_id_calendar_id", x => new { x.project_id, x.map_id, x.calendar_id });
                table.CheckConstraint("CK_core_calendar_role", "calendar_role IN ('default', 'auxiliary')");
                table.ForeignKey(
                    name: "FK_core_calendar_core_map_project_id_map_id",
                    columns: x => new { x.project_id, x.map_id },
                    principalTable: "core_map",
                    principalColumns: new[] { "project_id", "map_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_history",
            columns: table => new
            {
                history_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                history_kind = table.Column<string>(type: "TEXT", nullable: false),
                owner_map_id = table.Column<string>(type: "TEXT", nullable: true),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_history", x => x.history_id);
                table.UniqueConstraint("AK_core_history_project_id_history_id", x => new { x.project_id, x.history_id });
                table.CheckConstraint("CK_core_history_kind", "history_kind IN ('map_root', 'branch_owned')");
                table.CheckConstraint("CK_core_history_owner_kind", "(history_kind = 'map_root' AND owner_map_id IS NOT NULL) OR (history_kind = 'branch_owned' AND owner_map_id IS NULL)");
                table.ForeignKey(
                    name: "FK_core_history_core_map_project_id_owner_map_id",
                    columns: x => new { x.project_id, x.owner_map_id },
                    principalTable: "core_map",
                    principalColumns: new[] { "project_id", "map_id" });
                table.ForeignKey(
                    name: "FK_core_history_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_map_initial_state",
            columns: table => new
            {
                initial_state_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                baseline_schema_version = table.Column<long>(type: "INTEGER", nullable: false),
                content_fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_initial_state", x => x.initial_state_id);
                table.UniqueConstraint("AK_core_map_initial_state_project_id_initial_state_id", x => new { x.project_id, x.initial_state_id });
                table.UniqueConstraint("AK_core_map_initial_state_project_id_map_id", x => new { x.project_id, x.map_id });
                table.UniqueConstraint("AK_core_map_initial_state_project_id_map_id_initial_state_id", x => new { x.project_id, x.map_id, x.initial_state_id });
                table.ForeignKey(
                    name: "FK_core_map_initial_state_core_map_project_id_map_id",
                    columns: x => new { x.project_id, x.map_id },
                    principalTable: "core_map",
                    principalColumns: new[] { "project_id", "map_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_object",
            columns: table => new
            {
                object_identity_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                object_type_id = table.Column<string>(type: "TEXT", nullable: false),
                object_type_schema_version = table.Column<long>(type: "INTEGER", nullable: false),
                object_type_semantics_id = table.Column<string>(type: "TEXT", nullable: false),
                sharing_scope = table.Column<string>(type: "TEXT", nullable: false),
                owner_map_id = table.Column<string>(type: "TEXT", nullable: true),
                lifecycle_status = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_object", x => x.object_identity_id);
                table.UniqueConstraint("AK_core_object_project_id_object_identity_id", x => new { x.project_id, x.object_identity_id });
                table.UniqueConstraint("AK_core_object_project_id_object_type_id_object_identity_id", x => new { x.project_id, x.object_type_id, x.object_identity_id });
                table.CheckConstraint("CK_core_object_lifecycle", "lifecycle_status IN ('draft', 'active', 'abandoned', 'retired')");
                table.CheckConstraint("CK_core_object_scope", "sharing_scope IN ('project_shared', 'map_local')");
                table.CheckConstraint("CK_core_object_scope_owner", "(sharing_scope = 'project_shared' AND owner_map_id IS NULL) OR (sharing_scope = 'map_local' AND owner_map_id IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_core_object_core_map_project_id_owner_map_id",
                    columns: x => new { x.project_id, x.owner_map_id },
                    principalTable: "core_map",
                    principalColumns: new[] { "project_id", "map_id" });
                table.ForeignKey(
                    name: "FK_core_object_core_object_type_project_id_object_type_id",
                    columns: x => new { x.project_id, x.object_type_id },
                    principalTable: "core_object_type",
                    principalColumns: new[] { "project_id", "object_type_id" });
                table.ForeignKey(
                    name: "FK_core_object_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_object_id_allocator",
            columns: table => new
            {
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                object_type_id = table.Column<string>(type: "TEXT", nullable: false),
                next_sequence = table.Column<long>(type: "INTEGER", nullable: false),
                allocator_format_version = table.Column<long>(type: "INTEGER", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_object_id_allocator", x => new { x.project_id, x.object_type_id });
                table.CheckConstraint("CK_core_object_id_allocator_sequence", "next_sequence >= 1");
                table.ForeignKey(
                    name: "FK_core_object_id_allocator_core_object_type_project_id_object_~",
                    columns: x => new { x.project_id, x.object_type_id },
                    principalTable: "core_object_type",
                    principalColumns: new[] { "project_id", "object_type_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_branch",
            columns: table => new
            {
                branch_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                parent_history_id = table.Column<string>(type: "TEXT", nullable: false),
                own_history_id = table.Column<string>(type: "TEXT", nullable: false),
                parent_day_number = table.Column<long>(type: "INTEGER", nullable: false),
                parent_offset_seconds = table.Column<long>(type: "INTEGER", nullable: false),
                branch_name = table.Column<string>(type: "TEXT", nullable: false),
                lifecycle_status = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_branch", x => x.branch_id);
                table.UniqueConstraint("AK_core_branch_project_id_branch_id", x => new { x.project_id, x.branch_id });
                table.UniqueConstraint("AK_core_branch_project_id_own_history_id", x => new { x.project_id, x.own_history_id });
                table.CheckConstraint("CK_core_branch_distinct_histories", "parent_history_id <> own_history_id");
                table.CheckConstraint("CK_core_branch_parent_time", "parent_day_number >= 0 AND parent_offset_seconds BETWEEN 0 AND 86399");
                table.ForeignKey(
                    name: "FK_core_branch_core_history_project_id_own_history_id",
                    columns: x => new { x.project_id, x.own_history_id },
                    principalTable: "core_history",
                    principalColumns: new[] { "project_id", "history_id" });
                table.ForeignKey(
                    name: "FK_core_branch_core_history_project_id_parent_history_id",
                    columns: x => new { x.project_id, x.parent_history_id },
                    principalTable: "core_history",
                    principalColumns: new[] { "project_id", "history_id" });
                table.ForeignKey(
                    name: "FK_core_branch_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_calendar_configuration",
            columns: table => new
            {
                calendar_configuration_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                calendar_id = table.Column<string>(type: "TEXT", nullable: false),
                owner_history_id = table.Column<string>(type: "TEXT", nullable: false),
                source_command_id = table.Column<string>(type: "TEXT", nullable: true),
                configuration_origin = table.Column<string>(type: "TEXT", nullable: false),
                effective_from_day_number = table.Column<long>(type: "INTEGER", nullable: false),
                effective_from_offset_seconds = table.Column<long>(type: "INTEGER", nullable: false),
                effective_until_day_number = table.Column<long>(type: "INTEGER", nullable: true),
                effective_until_offset_seconds = table.Column<long>(type: "INTEGER", nullable: true),
                rules_schema_version = table.Column<long>(type: "INTEGER", nullable: false),
                rules_payload_json = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_calendar_configuration", x => x.calendar_configuration_id);
                table.UniqueConstraint("AK_core_calendar_configuration_project_id_calendar_configuratio~", x => new { x.project_id, x.calendar_configuration_id });
                table.UniqueConstraint("AK_core_calendar_configuration_project_id_map_id_calendar_id_ow~", x => new { x.project_id, x.map_id, x.calendar_id, x.owner_history_id, x.effective_from_day_number, x.effective_from_offset_seconds });
                table.CheckConstraint("CK_core_calendar_configuration_from", "effective_from_day_number >= 0 AND effective_from_offset_seconds BETWEEN 0 AND 86399");
                table.CheckConstraint("CK_core_calendar_configuration_origin", "(configuration_origin = 'user_created' AND source_command_id IS NULL) OR (configuration_origin = 'command_managed' AND source_command_id IS NOT NULL)");
                table.CheckConstraint("CK_core_calendar_configuration_until", "effective_until_day_number IS NULL OR (effective_until_day_number >= 0 AND effective_until_offset_seconds BETWEEN 0 AND 86399)");
                table.CheckConstraint("CK_core_calendar_configuration_until_pair", "(effective_until_day_number IS NULL AND effective_until_offset_seconds IS NULL) OR (effective_until_day_number IS NOT NULL AND effective_until_offset_seconds IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_core_calendar_configuration_core_calendar_project_id_map_id_~",
                    columns: x => new { x.project_id, x.map_id, x.calendar_id },
                    principalTable: "core_calendar",
                    principalColumns: new[] { "project_id", "map_id", "calendar_id" });
                table.ForeignKey(
                    name: "FK_core_calendar_configuration_core_history_project_id_owner_hi~",
                    columns: x => new { x.project_id, x.owner_history_id },
                    principalTable: "core_history",
                    principalColumns: new[] { "project_id", "history_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_map_object_ref",
            columns: table => new
            {
                map_object_ref_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                object_identity_id = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_object_ref", x => x.map_object_ref_id);
                table.UniqueConstraint("AK_core_map_object_ref_project_id_map_id_object_identity_id", x => new { x.project_id, x.map_id, x.object_identity_id });
                table.UniqueConstraint("AK_core_map_object_ref_project_id_map_object_ref_id", x => new { x.project_id, x.map_object_ref_id });
                table.ForeignKey(
                    name: "FK_core_map_object_ref_core_map_project_id_map_id",
                    columns: x => new { x.project_id, x.map_id },
                    principalTable: "core_map",
                    principalColumns: new[] { "project_id", "map_id" });
                table.ForeignKey(
                    name: "FK_core_map_object_ref_core_object_project_id_object_identity_id",
                    columns: x => new { x.project_id, x.object_identity_id },
                    principalTable: "core_object",
                    principalColumns: new[] { "project_id", "object_identity_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_object_definition",
            columns: table => new
            {
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                object_identity_id = table.Column<string>(type: "TEXT", nullable: false),
                definition_schema_version = table.Column<long>(type: "INTEGER", nullable: false),
                definition_payload_json = table.Column<string>(type: "TEXT", nullable: false),
                definition_hash = table.Column<string>(type: "TEXT", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_object_definition", x => new { x.project_id, x.object_identity_id });
                table.ForeignKey(
                    name: "FK_core_object_definition_core_object_project_id_object_identit~",
                    columns: x => new { x.project_id, x.object_identity_id },
                    principalTable: "core_object",
                    principalColumns: new[] { "project_id", "object_identity_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_object_definition_asset",
            columns: table => new
            {
                object_definition_asset_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                object_identity_id = table.Column<string>(type: "TEXT", nullable: false),
                asset_id = table.Column<string>(type: "TEXT", nullable: false),
                role = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_object_definition_asset", x => x.object_definition_asset_id);
                table.UniqueConstraint("AK_core_object_definition_asset_project_id_object_definition_as~", x => new { x.project_id, x.object_definition_asset_id });
                table.UniqueConstraint("AK_core_object_definition_asset_project_id_object_identity_id_a~", x => new { x.project_id, x.object_identity_id, x.asset_id, x.role });
                table.ForeignKey(
                    name: "FK_core_object_definition_asset_core_asset_project_id_asset_id",
                    columns: x => new { x.project_id, x.asset_id },
                    principalTable: "core_asset",
                    principalColumns: new[] { "project_id", "asset_id" });
                table.ForeignKey(
                    name: "FK_core_object_definition_asset_core_object_project_id_object_i~",
                    columns: x => new { x.project_id, x.object_identity_id },
                    principalTable: "core_object",
                    principalColumns: new[] { "project_id", "object_identity_id" });
                table.ForeignKey(
                    name: "FK_core_object_definition_asset_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_object_id_reservation",
            columns: table => new
            {
                reservation_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                object_type_id = table.Column<string>(type: "TEXT", nullable: false),
                object_identity_id = table.Column<string>(type: "TEXT", nullable: true),
                allocated_sequence = table.Column<long>(type: "INTEGER", nullable: false),
                allocated_id_value = table.Column<string>(type: "TEXT", nullable: false),
                reservation_state = table.Column<string>(type: "TEXT", nullable: false),
                request_key = table.Column<string>(type: "TEXT", nullable: true),
                abandon_reason = table.Column<string>(type: "TEXT", nullable: true),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_object_id_reservation", x => x.reservation_id);
                table.UniqueConstraint("AK_core_object_id_reservation_project_id_object_type_id_alloca~1", x => new { x.project_id, x.object_type_id, x.allocated_sequence });
                table.UniqueConstraint("AK_core_object_id_reservation_project_id_object_type_id_allocat~", x => new { x.project_id, x.object_type_id, x.allocated_id_value });
                table.UniqueConstraint("AK_core_object_id_reservation_project_id_reservation_id", x => new { x.project_id, x.reservation_id });
                table.CheckConstraint("CK_core_object_id_reservation_abandon_reason", "abandon_reason IS NULL OR reservation_state = 'Abandoned'");
                table.CheckConstraint("CK_core_object_id_reservation_sequence", "allocated_sequence >= 1");
                table.CheckConstraint("CK_core_object_id_reservation_state", "(reservation_state = 'Reserved' AND object_identity_id IS NULL) OR (reservation_state IN ('Bound', 'Inserted') AND object_identity_id IS NOT NULL) OR reservation_state = 'Abandoned'");
                table.ForeignKey(
                    name: "FK_core_object_id_reservation_core_object_project_id_object_typ~",
                    columns: x => new { x.project_id, x.object_type_id, x.object_identity_id },
                    principalTable: "core_object",
                    principalColumns: new[] { "project_id", "object_type_id", "object_identity_id" });
                table.ForeignKey(
                    name: "FK_core_object_id_reservation_core_object_type_project_id_objec~",
                    columns: x => new { x.project_id, x.object_type_id },
                    principalTable: "core_object_type",
                    principalColumns: new[] { "project_id", "object_type_id" });
                table.ForeignKey(
                    name: "FK_core_object_id_reservation_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateTable(
            name: "core_map_initial_object",
            columns: table => new
            {
                initial_state_id = table.Column<string>(type: "TEXT", nullable: false),
                object_identity_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                initial_membership_payload_json = table.Column<string>(type: "TEXT", nullable: false),
                initial_placement_payload_json = table.Column<string>(type: "TEXT", nullable: true),
                initial_placement_schema_version = table.Column<long>(type: "INTEGER", nullable: true),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_initial_object", x => new { x.initial_state_id, x.object_identity_id });
                table.CheckConstraint("CK_core_map_initial_object_placement_pair", "(initial_placement_payload_json IS NULL AND initial_placement_schema_version IS NULL) OR (initial_placement_payload_json IS NOT NULL AND initial_placement_schema_version IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_core_map_initial_object_core_map_initial_state_project_id_ma~",
                    columns: x => new { x.project_id, x.map_id, x.initial_state_id },
                    principalTable: "core_map_initial_state",
                    principalColumns: new[] { "project_id", "map_id", "initial_state_id" });
                table.ForeignKey(
                    name: "FK_core_map_initial_object_core_map_object_ref_project_id_map_i~",
                    columns: x => new { x.project_id, x.map_id, x.object_identity_id },
                    principalTable: "core_map_object_ref",
                    principalColumns: new[] { "project_id", "map_id", "object_identity_id" });
                table.ForeignKey(
                    name: "FK_core_map_initial_object_core_object_project_id_object_identi~",
                    columns: x => new { x.project_id, x.object_identity_id },
                    principalTable: "core_object",
                    principalColumns: new[] { "project_id", "object_identity_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_map_field_dataset",
            columns: table => new
            {
                field_dataset_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                field_key = table.Column<string>(type: "TEXT", nullable: false),
                descriptor_schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                semantics_id = table.Column<string>(type: "TEXT", nullable: false),
                current_field_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_field_dataset", x => x.field_dataset_id);
                table.UniqueConstraint("AK_core_map_field_dataset_project_id_field_dataset_id", x => new { x.project_id, x.field_dataset_id });
                table.UniqueConstraint("AK_core_map_field_dataset_project_id_map_id_field_key", x => new { x.project_id, x.map_id, x.field_key });
                table.CheckConstraint("CK_core_map_field_dataset_field_key", "field_key IN ('elevation', 'land_water')");
                table.CheckConstraint("CK_core_map_field_dataset_schema_version", "descriptor_schema_version >= 1");
                table.ForeignKey(
                    name: "FK_core_map_field_dataset_core_map_project_id_map_id",
                    columns: x => new { x.project_id, x.map_id },
                    principalTable: "core_map",
                    principalColumns: new[] { "project_id", "map_id" });
            });

        migrationBuilder.CreateTable(
            name: "core_map_field_revision",
            columns: table => new
            {
                field_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                field_dataset_id = table.Column<string>(type: "TEXT", nullable: false),
                revision_number = table.Column<long>(type: "INTEGER", nullable: false),
                parent_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                field_revision_asset_id = table.Column<string>(type: "TEXT", nullable: false),
                content_fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                manifest_json = table.Column<string>(type: "TEXT", nullable: false),
                provenance_json = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_field_revision", x => x.field_revision_id);
                table.UniqueConstraint("AK_core_map_field_revision_project_id_field_dataset_id_field_re~", x => new { x.project_id, x.field_dataset_id, x.field_revision_id });
                table.UniqueConstraint("AK_core_map_field_revision_project_id_field_dataset_id_revision~", x => new { x.project_id, x.field_dataset_id, x.revision_number });
                table.CheckConstraint("CK_core_map_field_revision_fingerprint", "length(content_fingerprint) = 64 AND content_fingerprint NOT GLOB '*[^0-9a-f]*'");
                table.CheckConstraint("CK_core_map_field_revision_number", "revision_number >= 1");
                table.ForeignKey(
                    name: "FK_core_map_field_revision_core_asset_project_id_field_revision~",
                    columns: x => new { x.project_id, x.field_revision_asset_id },
                    principalTable: "core_asset",
                    principalColumns: new[] { "project_id", "asset_id" });
                table.ForeignKey(
                    name: "FK_core_map_field_revision_core_map_field_dataset_project_id_fi~",
                    columns: x => new { x.project_id, x.field_dataset_id },
                    principalTable: "core_map_field_dataset",
                    principalColumns: new[] { "project_id", "field_dataset_id" });
                table.ForeignKey(
                    name: "FK_core_map_field_revision_core_map_field_revision_project_id_f~",
                    columns: x => new { x.project_id, x.field_dataset_id, x.parent_revision_id },
                    principalTable: "core_map_field_revision",
                    principalColumns: new[] { "project_id", "field_dataset_id", "field_revision_id" });
            });

        migrationBuilder.CreateIndex(
            name: "IX_core_branch_project_id_parent_history_id",
            table: "core_branch",
            columns: new[] { "project_id", "parent_history_id" });

        migrationBuilder.CreateIndex(
            name: "ux_core_calendar_default_per_map",
            table: "core_calendar",
            columns: new[] { "project_id", "map_id" },
            unique: true,
            filter: "calendar_role = 'default'");

        migrationBuilder.CreateIndex(
            name: "IX_core_calendar_configuration_project_id_owner_history_id",
            table: "core_calendar_configuration",
            columns: new[] { "project_id", "owner_history_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_history_project_id_owner_map_id",
            table: "core_history",
            columns: new[] { "project_id", "owner_map_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_core_map_field_dataset_project_id_field_dataset_id_current_f~",
            table: "core_map_field_dataset",
            columns: new[] { "project_id", "field_dataset_id", "current_field_revision_id" });

        migrationBuilder.CreateIndex(
            name: "ix_core_map_field_revision_asset",
            table: "core_map_field_revision",
            columns: new[] { "project_id", "field_revision_asset_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_field_revision_project_id_field_dataset_id_parent_r~",
            table: "core_map_field_revision",
            columns: new[] { "project_id", "field_dataset_id", "parent_revision_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_initial_object_project_id_map_id_initial_state_id",
            table: "core_map_initial_object",
            columns: new[] { "project_id", "map_id", "initial_state_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_initial_object_project_id_map_id_object_identity_id",
            table: "core_map_initial_object",
            columns: new[] { "project_id", "map_id", "object_identity_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_initial_object_project_id_object_identity_id",
            table: "core_map_initial_object",
            columns: new[] { "project_id", "object_identity_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_object_ref_project_id_object_identity_id",
            table: "core_map_object_ref",
            columns: new[] { "project_id", "object_identity_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_object_project_id_owner_map_id",
            table: "core_object",
            columns: new[] { "project_id", "owner_map_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_object_definition_asset_project_id_asset_id",
            table: "core_object_definition_asset",
            columns: new[] { "project_id", "asset_id" });

        migrationBuilder.CreateIndex(
            name: "IX_core_object_id_reservation_project_id_object_type_id_object_~",
            table: "core_object_id_reservation",
            columns: new[] { "project_id", "object_type_id", "object_identity_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_core_object_id_reservation_project_id_request_key",
            table: "core_object_id_reservation",
            columns: new[] { "project_id", "request_key" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_core_map_field_dataset_core_map_field_revision_project_id_fi~",
            table: "core_map_field_dataset",
            columns: new[] { "project_id", "field_dataset_id", "current_field_revision_id" },
            principalTable: "core_map_field_revision",
            principalColumns: new[] { "project_id", "field_dataset_id", "field_revision_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_core_asset_core_project_project_id",
            table: "core_asset");

        migrationBuilder.DropForeignKey(
            name: "FK_core_map_core_project_project_id",
            table: "core_map");

        migrationBuilder.DropForeignKey(
            name: "FK_core_map_field_dataset_core_map_project_id_map_id",
            table: "core_map_field_dataset");

        migrationBuilder.DropForeignKey(
            name: "FK_core_map_field_dataset_core_map_field_revision_project_id_fi~",
            table: "core_map_field_dataset");

        migrationBuilder.DropTable(
            name: "core_branch");

        migrationBuilder.DropTable(
            name: "core_calendar_configuration");

        migrationBuilder.DropTable(
            name: "core_map_initial_object");

        migrationBuilder.DropTable(
            name: "core_object_definition");

        migrationBuilder.DropTable(
            name: "core_object_definition_asset");

        migrationBuilder.DropTable(
            name: "core_object_id_allocator");

        migrationBuilder.DropTable(
            name: "core_object_id_reservation");

        migrationBuilder.DropTable(
            name: "core_calendar");

        migrationBuilder.DropTable(
            name: "core_history");

        migrationBuilder.DropTable(
            name: "core_map_initial_state");

        migrationBuilder.DropTable(
            name: "core_map_object_ref");

        migrationBuilder.DropTable(
            name: "core_object");

        migrationBuilder.DropTable(
            name: "core_object_type");

        migrationBuilder.DropTable(
            name: "core_project");

        migrationBuilder.DropTable(
            name: "core_map");

        migrationBuilder.DropTable(
            name: "core_map_field_revision");

        migrationBuilder.DropTable(
            name: "core_asset");

        migrationBuilder.DropTable(
            name: "core_map_field_dataset");
    }
}
