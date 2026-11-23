using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261122141903_FieldRevisionTypedMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "coordinate_precision_profile_id",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "coverage_mask_plane_name",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "coverage_mode",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "descriptor_schema_version",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "field_key",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "grid_axis_orientation",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "grid_cell_registration",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "grid_height",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "grid_origin_x",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "grid_origin_y",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "grid_spacing_x",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "grid_spacing_y",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "grid_tile_height",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "grid_tile_width",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "grid_width",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "map_edge_semantics",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "map_space_id",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "no_data_invalid_value",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "no_data_mask_plane_name",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "no_data_policy",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "no_data_valid_value",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "semantics_id",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "spatial_reference_id",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "tile_hash_algorithm",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "tile_hash_version",
            table: "core_map_field_revision",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "unit",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "value_profile_id",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "value_profile_scope",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "vertical_reference_id",
            table: "core_map_field_revision",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "core_map_field_revision_category_code",
            columns: table => new
            {
                field_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                category_key = table.Column<string>(type: "TEXT", nullable: false),
                category_code = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_field_revision_category_code", x => new { x.field_revision_id, x.category_key });
                table.ForeignKey(
                    name: "FK_core_map_field_revision_category_code_core_map_field_revisio~",
                    column: x => x.field_revision_id,
                    principalTable: "core_map_field_revision",
                    principalColumn: "field_revision_id");
            });

        migrationBuilder.CreateTable(
            name: "core_map_field_revision_codec",
            columns: table => new
            {
                field_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                codec_id = table.Column<int>(type: "INTEGER", nullable: false),
                codec_name = table.Column<string>(type: "TEXT", nullable: false),
                version = table.Column<string>(type: "TEXT", nullable: false),
                parameters_json = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_field_revision_codec", x => new { x.field_revision_id, x.ordinal });
                table.CheckConstraint("CK_core_map_field_revision_codec_ordinal", "ordinal >= 0");
                table.ForeignKey(
                    name: "FK_core_map_field_revision_codec_core_map_field_revision_field_~",
                    column: x => x.field_revision_id,
                    principalTable: "core_map_field_revision",
                    principalColumn: "field_revision_id");
            });

        migrationBuilder.CreateTable(
            name: "core_map_field_revision_plane",
            columns: table => new
            {
                field_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                name = table.Column<string>(type: "TEXT", nullable: false),
                sample_format = table.Column<string>(type: "TEXT", nullable: false),
                bytes_per_cell = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_field_revision_plane", x => new { x.field_revision_id, x.ordinal });
                table.CheckConstraint("CK_core_map_field_revision_plane_bytes", "bytes_per_cell > 0");
                table.CheckConstraint("CK_core_map_field_revision_plane_ordinal", "ordinal >= 0");
                table.ForeignKey(
                    name: "FK_core_map_field_revision_plane_core_map_field_revision_field_~",
                    column: x => x.field_revision_id,
                    principalTable: "core_map_field_revision",
                    principalColumn: "field_revision_id");
            });

        migrationBuilder.CreateIndex(
            name: "ux_core_map_field_revision_category_code_value",
            table: "core_map_field_revision_category_code",
            columns: new[] { "field_revision_id", "category_code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_core_map_field_revision_codec_id",
            table: "core_map_field_revision_codec",
            columns: new[] { "field_revision_id", "codec_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_core_map_field_revision_plane_name",
            table: "core_map_field_revision_plane",
            columns: new[] { "field_revision_id", "name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "core_map_field_revision_category_code");

        migrationBuilder.DropTable(
            name: "core_map_field_revision_codec");

        migrationBuilder.DropTable(
            name: "core_map_field_revision_plane");

        migrationBuilder.DropColumn(
            name: "coordinate_precision_profile_id",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "coverage_mask_plane_name",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "coverage_mode",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "descriptor_schema_version",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "field_key",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_axis_orientation",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_cell_registration",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_height",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_origin_x",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_origin_y",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_spacing_x",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_spacing_y",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_tile_height",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_tile_width",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "grid_width",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "map_edge_semantics",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "map_space_id",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "no_data_invalid_value",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "no_data_mask_plane_name",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "no_data_policy",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "no_data_valid_value",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "semantics_id",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "spatial_reference_id",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "tile_hash_algorithm",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "tile_hash_version",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "unit",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "value_profile_id",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "value_profile_scope",
            table: "core_map_field_revision");

        migrationBuilder.DropColumn(
            name: "vertical_reference_id",
            table: "core_map_field_revision");
    }
}
