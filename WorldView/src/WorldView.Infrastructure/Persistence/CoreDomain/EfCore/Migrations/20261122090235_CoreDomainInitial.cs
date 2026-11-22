using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

/// <summary>
/// The single EF Core CLI-generated initial migration for the fresh WorldView 1.0 schema.
/// Provider-specific SQLite invariants remain explicit SQL because EF cannot represent
/// every required SQLite feature (including deferred foreign keys and immutability triggers).
/// </summary>
public partial class _20261122090235_CoreDomainInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in CoreDomainInitialSql.Statements)
        {
            migrationBuilder.Sql(sql);
        }

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

            CREATE TABLE core_map_field_revision (
                field_revision_id       TEXT    NOT NULL PRIMARY KEY,
                project_id              TEXT    NOT NULL,
                field_dataset_id        TEXT    NOT NULL,
                revision_number         INTEGER NOT NULL CHECK (revision_number >= 1),
                parent_revision_id      TEXT    NULL,
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
                FOREIGN KEY (project_id, field_dataset_id, parent_revision_id)
                    REFERENCES core_map_field_revision (project_id, field_dataset_id, field_revision_id)
            );

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
        throw new NotSupportedException(
            "WorldView 1.0 schema is forward-only. Recreate an unreleased development database instead of downgrading.");
    }
}
