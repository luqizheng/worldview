using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261122140144_MapSpatialReferenceOwnership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "map_space_id",
            table: "core_map",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "spatial_reference_id",
            table: "core_map",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "core_spatial_reference",
            columns: table => new
            {
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                spatial_reference_id = table.Column<string>(type: "TEXT", nullable: false),
                definition_version = table.Column<int>(type: "INTEGER", nullable: false),
                reference_kind = table.Column<string>(type: "TEXT", nullable: false),
                coordinate_unit = table.Column<string>(type: "TEXT", nullable: false),
                axis_order = table.Column<string>(type: "TEXT", nullable: false),
                dimension = table.Column<int>(type: "INTEGER", nullable: false),
                valid_domain_json = table.Column<string>(type: "TEXT", nullable: false),
                wrap_behavior = table.Column<string>(type: "TEXT", nullable: false),
                vertical_reference_id = table.Column<string>(type: "TEXT", nullable: true),
                precision_profile_id = table.Column<string>(type: "TEXT", nullable: false),
                precision_profile_version = table.Column<int>(type: "INTEGER", nullable: false),
                precision_coordinate_unit = table.Column<string>(type: "TEXT", nullable: false),
                precision_axis_order = table.Column<string>(type: "TEXT", nullable: false),
                numeric_representation = table.Column<string>(type: "TEXT", nullable: false),
                maximum_absolute_quantization_error = table.Column<string>(type: "TEXT", nullable: false),
                rounding_mode = table.Column<string>(type: "TEXT", nullable: false),
                minimum_authoring_tolerance = table.Column<string>(type: "TEXT", nullable: false),
                smallest_meaningful_feature_size = table.Column<string>(type: "TEXT", nullable: false),
                definition_parameters_json = table.Column<string>(type: "TEXT", nullable: false),
                measurement_capabilities_json = table.Column<string>(type: "TEXT", nullable: false),
                provenance_json = table.Column<string>(type: "TEXT", nullable: false),
                definition_fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_spatial_reference", x => new { x.project_id, x.spatial_reference_id });
                table.CheckConstraint("CK_core_spatial_reference_axis_order", "axis_order IN ('XY', 'YX')");
                table.CheckConstraint("CK_core_spatial_reference_definition_version", "definition_version >= 1");
                table.CheckConstraint("CK_core_spatial_reference_dimension", "dimension IN (2, 3)");
                table.CheckConstraint("CK_core_spatial_reference_kind", "reference_kind IN ('Geographic', 'Projected', 'LocalCartesian')");
                table.CheckConstraint("CK_core_spatial_reference_precision_version", "precision_profile_version >= 1");
                table.CheckConstraint("CK_core_spatial_reference_wrap", "wrap_behavior IN ('None', 'WrapX', 'WrapY', 'WrapXY')");
                table.ForeignKey(
                    name: "FK_core_spatial_reference_core_project_project_id",
                    column: x => x.project_id,
                    principalTable: "core_project",
                    principalColumn: "project_id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_project_id_spatial_reference_id",
            table: "core_map",
            columns: new[] { "project_id", "spatial_reference_id" });

        migrationBuilder.CreateIndex(
            name: "ux_core_map_map_space_id",
            table: "core_map",
            column: "map_space_id",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_core_map_map_space_id",
            table: "core_map",
            sql: "length(map_space_id) BETWEEN 1 AND 128");

        migrationBuilder.AddForeignKey(
            name: "FK_core_map_core_spatial_reference_project_id_spatial_reference~",
            table: "core_map",
            columns: new[] { "project_id", "spatial_reference_id" },
            principalTable: "core_spatial_reference",
            principalColumns: new[] { "project_id", "spatial_reference_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_core_map_core_spatial_reference_project_id_spatial_reference~",
            table: "core_map");

        migrationBuilder.DropTable(
            name: "core_spatial_reference");

        migrationBuilder.DropIndex(
            name: "IX_core_map_project_id_spatial_reference_id",
            table: "core_map");

        migrationBuilder.DropIndex(
            name: "ux_core_map_map_space_id",
            table: "core_map");

        migrationBuilder.DropCheckConstraint(
            name: "CK_core_map_map_space_id",
            table: "core_map");

        migrationBuilder.DropColumn(
            name: "map_space_id",
            table: "core_map");

        migrationBuilder.DropColumn(
            name: "spatial_reference_id",
            table: "core_map");
    }
}
