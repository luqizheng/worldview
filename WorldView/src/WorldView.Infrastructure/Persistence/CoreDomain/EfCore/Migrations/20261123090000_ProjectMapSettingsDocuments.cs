using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

[DbContext(typeof(CoreDomainDbContext))]
[Migration("20261123090000_ProjectMapSettingsDocuments")]
public partial class _20261123090000_ProjectMapSettingsDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "core_project_settings_revision",
            columns: table => new
            {
                project_settings_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false),
                schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                relative_path = table.Column<string>(type: "TEXT", nullable: false),
                hash_algorithm = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                byte_length = table.Column<long>(type: "INTEGER", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_project_settings_revision", x => x.project_settings_revision_id);
                table.UniqueConstraint("AK_core_project_settings_revision_project_id_project_settings_revision_id",
                    x => new { x.project_id, x.project_settings_revision_id });
                table.CheckConstraint("CK_core_project_settings_revision_version", "version >= 1");
                table.CheckConstraint("CK_core_project_settings_revision_schema", "schema_version >= 1");
                table.CheckConstraint("CK_core_project_settings_revision_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.CheckConstraint("CK_core_project_settings_revision_hash",
                    "length(content_hash) = 64 AND content_hash NOT GLOB '*[^0-9a-f]*'");
                table.CheckConstraint("CK_core_project_settings_revision_length", "byte_length > 0");
                table.CheckConstraint("CK_core_project_settings_revision_path", "relative_path GLOB 'Settings/sha256/*'");
                table.ForeignKey("FK_core_project_settings_revision_core_project_project_id",
                    x => x.project_id, "core_project", "project_id", onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "IX_core_project_settings_revision_project_id_version",
            table: "core_project_settings_revision",
            columns: new[] { "project_id", "version" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "core_map_settings_revision",
            columns: table => new
            {
                map_settings_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false),
                schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                relative_path = table.Column<string>(type: "TEXT", nullable: false),
                hash_algorithm = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                byte_length = table.Column<long>(type: "INTEGER", nullable: false),
                created_at_utc_ms = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_settings_revision", x => x.map_settings_revision_id);
                table.UniqueConstraint("AK_core_map_settings_revision_project_map_revision",
                    x => new { x.project_id, x.map_id, x.map_settings_revision_id });
                table.CheckConstraint("CK_core_map_settings_revision_version", "version >= 1");
                table.CheckConstraint("CK_core_map_settings_revision_schema", "schema_version >= 1");
                table.CheckConstraint("CK_core_map_settings_revision_hash_algorithm", "hash_algorithm = 'sha-256'");
                table.CheckConstraint("CK_core_map_settings_revision_hash",
                    "length(content_hash) = 64 AND content_hash NOT GLOB '*[^0-9a-f]*'");
                table.CheckConstraint("CK_core_map_settings_revision_length", "byte_length > 0");
                table.CheckConstraint("CK_core_map_settings_revision_path", "relative_path GLOB 'Settings/sha256/*'");
                table.ForeignKey("FK_core_map_settings_revision_core_map_project_map",
                    x => new { x.project_id, x.map_id }, "core_map", new[] { "project_id", "map_id" },
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "IX_core_map_settings_revision_project_id_map_id_version",
            table: "core_map_settings_revision",
            columns: new[] { "project_id", "map_id", "version" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "core_project_settings_current",
            columns: table => new
            {
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                project_settings_revision_id = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_project_settings_current", x => x.project_id);
                table.ForeignKey("FK_core_project_settings_current_core_project_project_id",
                    x => x.project_id, "core_project", "project_id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_core_project_settings_current_project_settings_revision",
                    x => new { x.project_id, x.project_settings_revision_id },
                    "core_project_settings_revision", new[] { "project_id", "project_settings_revision_id" },
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            name: "IX_core_project_settings_current_revision",
            table: "core_project_settings_current",
            columns: new[] { "project_id", "project_settings_revision_id" });

        migrationBuilder.CreateTable(
            name: "core_map_settings_current",
            columns: table => new
            {
                project_id = table.Column<string>(type: "TEXT", nullable: false),
                map_id = table.Column<string>(type: "TEXT", nullable: false),
                map_settings_revision_id = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_core_map_settings_current", x => new { x.project_id, x.map_id });
                table.ForeignKey("FK_core_map_settings_current_core_map_project_map",
                    x => new { x.project_id, x.map_id }, "core_map", new[] { "project_id", "map_id" },
                    onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_core_map_settings_current_map_settings_revision",
                    x => new { x.project_id, x.map_id, x.map_settings_revision_id },
                    "core_map_settings_revision",
                    new[] { "project_id", "map_id", "map_settings_revision_id" },
                    onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateIndex(
            name: "IX_core_map_settings_current_revision",
            table: "core_map_settings_current",
            columns: new[] { "project_id", "map_id", "map_settings_revision_id" });

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_core_project_settings_revision_immutable_update
            BEFORE UPDATE ON core_project_settings_revision
            BEGIN SELECT RAISE(ABORT, 'Project Settings revisions are immutable'); END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_core_project_settings_revision_immutable_delete
            BEFORE DELETE ON core_project_settings_revision
            BEGIN SELECT RAISE(ABORT, 'Project Settings revisions are immutable'); END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_core_map_settings_revision_immutable_update
            BEFORE UPDATE ON core_map_settings_revision
            BEGIN SELECT RAISE(ABORT, 'Map Settings revisions are immutable'); END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_core_map_settings_revision_immutable_delete
            BEFORE DELETE ON core_map_settings_revision
            BEGIN SELECT RAISE(ABORT, 'Map Settings revisions are immutable'); END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "core_map_settings_current");
        migrationBuilder.DropTable(name: "core_project_settings_current");
        migrationBuilder.DropTable(name: "core_map_settings_revision");
        migrationBuilder.DropTable(name: "core_project_settings_revision");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder) =>
        CoreDomainV1ModelConfiguration.Configure(modelBuilder);
}
