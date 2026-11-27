using System;
using WorldView.Domain.Objects;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore.Migrations;

public partial class _20261129000000_DurableInitialStateRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var emptyFingerprint = InitialStateFingerprint.Compute(
            MapInitialStateService.StructuralBaselineSchemaVersion,
            Array.Empty<InitialObjectRecord>());

        migrationBuilder.Sql("""
            ALTER TABLE core_map_initial_state RENAME TO core_map_initial_state_legacy;
            ALTER TABLE core_map_initial_object RENAME TO core_map_initial_object_legacy;

            CREATE TEMP TABLE _wv_initial_state_revision_migration (
                project_id TEXT NOT NULL,
                map_id TEXT NOT NULL,
                initial_state_id TEXT NOT NULL,
                revision_id TEXT NOT NULL,
                baseline_schema_version INTEGER NOT NULL,
                content_fingerprint TEXT NOT NULL,
                baseline_created_at_utc_ms INTEGER NOT NULL,
                cursor_updated_at_utc_ms INTEGER NOT NULL,
                PRIMARY KEY (project_id, map_id)
            );

            INSERT INTO _wv_initial_state_revision_migration
                (project_id, map_id, initial_state_id, revision_id, baseline_schema_version,
                 content_fingerprint, baseline_created_at_utc_ms, cursor_updated_at_utc_ms)
            SELECT
                project_id, map_id, initial_state_id,
                lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))),
                baseline_schema_version, content_fingerprint, created_at_utc_ms, updated_at_utc_ms
            FROM core_map_initial_state_legacy;

            INSERT INTO _wv_initial_state_revision_migration
                (project_id, map_id, initial_state_id, revision_id, baseline_schema_version,
                 content_fingerprint, baseline_created_at_utc_ms, cursor_updated_at_utc_ms)
            SELECT
                m.project_id, m.map_id,
                lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))),
                lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))),
                1, '__EMPTY_FINGERPRINT__', m.created_at_utc_ms, m.updated_at_utc_ms
            FROM core_map m
            WHERE NOT EXISTS (
                SELECT 1 FROM core_map_initial_state_legacy s
                WHERE s.project_id = m.project_id AND s.map_id = m.map_id
            );

            CREATE TABLE core_map_initial_state (
                initial_state_id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                map_id TEXT NOT NULL,
                created_at_utc_ms INTEGER NOT NULL,
                UNIQUE (project_id, initial_state_id),
                UNIQUE (project_id, map_id, initial_state_id),
                UNIQUE (project_id, map_id),
                FOREIGN KEY (project_id, map_id) REFERENCES core_map (project_id, map_id)
            );

            CREATE TABLE core_map_initial_state_revision (
                revision_id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                map_id TEXT NOT NULL,
                initial_state_id TEXT NOT NULL,
                revision_number INTEGER NOT NULL CHECK (revision_number >= 0),
                parent_revision_id TEXT NULL,
                baseline_schema_version INTEGER NOT NULL,
                content_fingerprint TEXT NOT NULL CHECK (
                    length(content_fingerprint) = 64
                    AND content_fingerprint NOT GLOB '*[^0-9a-f]*'
                ),
                operation_kind TEXT NULL,
                operation_payload_json TEXT NULL,
                created_at_utc_ms INTEGER NOT NULL,
                UNIQUE (project_id, initial_state_id, revision_id),
                UNIQUE (project_id, map_id, initial_state_id, revision_id),
                UNIQUE (project_id, initial_state_id, revision_number),
                CHECK (
                    (revision_number = 0 AND parent_revision_id IS NULL AND operation_kind IS NULL AND operation_payload_json IS NULL)
                    OR (revision_number > 0 AND parent_revision_id IS NOT NULL AND operation_kind IS NOT NULL AND operation_payload_json IS NOT NULL)
                ),
                FOREIGN KEY (project_id, map_id, initial_state_id)
                    REFERENCES core_map_initial_state (project_id, map_id, initial_state_id),
                FOREIGN KEY (project_id, map_id, initial_state_id, parent_revision_id)
                    REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id)
            );

            CREATE INDEX IX_core_map_initial_state_revision_project_id_map_id_parent_revision_id
                ON core_map_initial_state_revision (project_id, map_id, parent_revision_id);

            CREATE TABLE core_map_initial_state_revision_object (
                project_id TEXT NOT NULL,
                map_id TEXT NOT NULL,
                initial_state_id TEXT NOT NULL,
                revision_id TEXT NOT NULL,
                object_identity_id TEXT NOT NULL,
                initial_membership_payload_json TEXT NOT NULL,
                initial_placement_payload_json TEXT NULL,
                initial_placement_schema_version INTEGER NULL,
                created_at_utc_ms INTEGER NOT NULL,
                PRIMARY KEY (revision_id, object_identity_id),
                CHECK (
                    (initial_placement_payload_json IS NULL AND initial_placement_schema_version IS NULL)
                    OR (initial_placement_payload_json IS NOT NULL AND initial_placement_schema_version IS NOT NULL)
                ),
                FOREIGN KEY (project_id, map_id, initial_state_id, revision_id)
                    REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id),
                FOREIGN KEY (project_id, map_id, object_identity_id)
                    REFERENCES core_map_object_ref (project_id, map_id, object_identity_id),
                FOREIGN KEY (project_id, object_identity_id)
                    REFERENCES core_object (project_id, object_identity_id)
            );

            CREATE INDEX IX_core_map_initial_state_revision_object_project_id_map_id_revision_id
                ON core_map_initial_state_revision_object (project_id, map_id, revision_id);
            CREATE INDEX IX_core_map_initial_state_revision_object_project_id_object_identity_id
                ON core_map_initial_state_revision_object (project_id, object_identity_id);

            CREATE TABLE core_map_initial_state_cursor (
                project_id TEXT NOT NULL,
                map_id TEXT NOT NULL,
                initial_state_id TEXT NOT NULL,
                active_revision_id TEXT NOT NULL,
                selected_redo_child_revision_id TEXT NULL,
                cursor_version INTEGER NOT NULL CHECK (cursor_version >= 0),
                updated_at_utc_ms INTEGER NOT NULL,
                PRIMARY KEY (project_id, map_id, initial_state_id),
                FOREIGN KEY (project_id, map_id, initial_state_id)
                    REFERENCES core_map_initial_state (project_id, map_id, initial_state_id),
                FOREIGN KEY (project_id, map_id, initial_state_id, active_revision_id)
                    REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id),
                FOREIGN KEY (project_id, map_id, initial_state_id, selected_redo_child_revision_id)
                    REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id)
            );

            INSERT INTO core_map_initial_state
                (initial_state_id, project_id, map_id, created_at_utc_ms)
            SELECT initial_state_id, project_id, map_id, baseline_created_at_utc_ms
            FROM _wv_initial_state_revision_migration;

            INSERT INTO core_map_initial_state_revision
                (revision_id, project_id, map_id, initial_state_id, revision_number, parent_revision_id,
                 baseline_schema_version, content_fingerprint, operation_kind, operation_payload_json, created_at_utc_ms)
            SELECT revision_id, project_id, map_id, initial_state_id, 0, NULL,
                   baseline_schema_version, content_fingerprint, NULL, NULL, baseline_created_at_utc_ms
            FROM _wv_initial_state_revision_migration;

            INSERT INTO core_map_initial_state_revision_object
                (project_id, map_id, initial_state_id, revision_id, object_identity_id,
                 initial_membership_payload_json, initial_placement_payload_json,
                 initial_placement_schema_version, created_at_utc_ms)
            SELECT old.project_id, old.map_id, old.initial_state_id, map.revision_id, old.object_identity_id,
                   old.initial_membership_payload_json, old.initial_placement_payload_json,
                   old.initial_placement_schema_version, old.created_at_utc_ms
            FROM core_map_initial_object_legacy old
            JOIN _wv_initial_state_revision_migration map
              ON map.project_id = old.project_id AND map.map_id = old.map_id
             AND map.initial_state_id = old.initial_state_id;

            INSERT INTO core_map_initial_state_cursor
                (project_id, map_id, initial_state_id, active_revision_id,
                 selected_redo_child_revision_id, cursor_version, updated_at_utc_ms)
            SELECT project_id, map_id, initial_state_id, revision_id, NULL, 0, cursor_updated_at_utc_ms
            FROM _wv_initial_state_revision_migration;

            DROP TABLE core_map_initial_object_legacy;
            DROP TABLE core_map_initial_state_legacy;
            DROP TABLE _wv_initial_state_revision_migration;
            """.Replace("__EMPTY_FINGERPRINT__", emptyFingerprint, StringComparison.Ordinal));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Downgrading would discard immutable Initial State revisions and cannot safely reconstruct the former mutable baseline.");
    }
}
