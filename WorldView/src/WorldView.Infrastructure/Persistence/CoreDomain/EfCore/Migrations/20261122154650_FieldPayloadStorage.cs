using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261122154650_FieldPayloadStorage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "core_field_payload",
            columns: table => new
            {
                field_payload_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                field_dataset_id = table.Column<string>(type: "TEXT", nullable: false),
                field_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                storage_kind = table.Column<string>(type: "TEXT", nullable: false),
                payload_format = table.Column<string>(type: "TEXT", nullable: false),
                format_version = table.Column<string>(type: "TEXT", nullable: false),
                hash_algorithm = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                byte_length = table.Column<long>(type: "INTEGER", nullable: false),
                payload_blob = table.Column<byte[]>(type: "BLOB", nullable: true),
                relative_path = table.Column<string>(type: "TEXT", nullable: true),
                integrity_state = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_field_payload", x => x.field_payload_id);
                table.UniqueConstraint("AK_core_field_payload_project_id_field_payload_id", x => new { x.project_id, x.field_payload_id });
                table.CheckConstraint("CK_core_field_payload_blob_length", "storage_kind <> 'sqlite_blob' OR length(payload_blob) = byte_length");
                table.CheckConstraint("CK_core_field_payload_byte_length", "byte_length > 0");
                table.CheckConstraint("CK_core_field_payload_hash", "length(content_hash) = 64 AND content_hash NOT GLOB '*[^0-9a-f]*'");
                table.CheckConstraint("CK_core_field_payload_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.CheckConstraint("CK_core_field_payload_integrity", "integrity_state IN ('verified', 'corrupt', 'missing')");
                table.CheckConstraint("CK_core_field_payload_storage_kind", "storage_kind IN ('sqlite_blob', 'managed_file')");
                table.CheckConstraint("CK_core_field_payload_storage_location", "(storage_kind = 'sqlite_blob' AND payload_blob IS NOT NULL AND relative_path IS NULL) OR (storage_kind = 'managed_file' AND payload_blob IS NULL AND relative_path IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_core_field_payload_core_map_field_revision_project_id_field_~",
                    columns: x => new { x.project_id, x.field_dataset_id, x.field_revision_id },
                    principalTable: "core_map_field_revision",
                    principalColumns: new[] { "project_id", "field_dataset_id", "field_revision_id" });
            });

        migrationBuilder.CreateIndex(
            name: "IX_core_field_payload_project_id_field_dataset_id_field_revisio~",
            table: "core_field_payload",
            columns: new[] { "project_id", "field_dataset_id", "field_revision_id" });

        migrationBuilder.CreateIndex(
            name: "ux_core_field_payload_revision",
            table: "core_field_payload",
            column: "field_revision_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "core_field_payload");
    }
}
