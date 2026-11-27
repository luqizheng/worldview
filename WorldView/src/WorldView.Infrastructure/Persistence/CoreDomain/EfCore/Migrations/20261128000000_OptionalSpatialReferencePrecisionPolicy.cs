using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

public partial class _20261128000000_OptionalSpatialReferencePrecisionPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_core_spatial_reference_precision_version", "core_spatial_reference");

        migrationBuilder.AlterColumn<string>("precision_profile_id", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<int>("precision_profile_version", "core_spatial_reference", type: "INTEGER", nullable: true, oldClrType: typeof(int), oldType: "INTEGER");
        migrationBuilder.AlterColumn<string>("precision_coordinate_unit", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<string>("precision_axis_order", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<string>("numeric_representation", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<string>("maximum_absolute_quantization_error", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<string>("rounding_mode", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<string>("minimum_authoring_tolerance", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
        migrationBuilder.AlterColumn<string>("smallest_meaningful_feature_size", "core_spatial_reference", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");

        migrationBuilder.AddCheckConstraint(
            name: "CK_core_spatial_reference_precision_version",
            table: "core_spatial_reference",
            sql: "precision_profile_version IS NULL OR precision_profile_version >= 1");
        migrationBuilder.AddCheckConstraint(
            name: "CK_core_spatial_reference_precision_profile_complete",
            table: "core_spatial_reference",
            sql: "(precision_profile_id IS NULL AND precision_profile_version IS NULL AND precision_coordinate_unit IS NULL AND precision_axis_order IS NULL AND numeric_representation IS NULL AND maximum_absolute_quantization_error IS NULL AND rounding_mode IS NULL AND minimum_authoring_tolerance IS NULL AND smallest_meaningful_feature_size IS NULL) OR (precision_profile_id IS NOT NULL AND precision_profile_version IS NOT NULL AND precision_coordinate_unit IS NOT NULL AND precision_axis_order IS NOT NULL AND numeric_representation IS NOT NULL AND maximum_absolute_quantization_error IS NOT NULL AND rounding_mode IS NOT NULL AND minimum_authoring_tolerance IS NOT NULL AND smallest_meaningful_feature_size IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Downgrading would require inventing a precision profile for coordinate frames that intentionally have none.");
    }
}
