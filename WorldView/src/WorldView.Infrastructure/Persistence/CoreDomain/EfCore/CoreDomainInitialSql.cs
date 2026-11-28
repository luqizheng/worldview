using System;
using System.Collections.Generic;

namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>
/// EF Core-owned SQLite DDL for the initial WorldView 1.0 schema. The migration scripts are
/// executed only by the EF Core initial migration. Provider-specific constraints and triggers
/// remain explicit SQL because SQLite semantics cannot always be represented by the EF model.
/// </summary>
internal static class CoreDomainInitialSql
{
    /// <summary>
    /// core_project（P2-09-11 §4.1）。V1 每库恰好一行——单行规则由 Core Domain
    /// 服务强制（读取时 0 行→NotEstablished、&gt;1 行→MultipleProjects；插入前校验）。
    /// </summary>
    public const string CoreProjectTableSql = """
        CREATE TABLE core_project (
            project_id             TEXT    NOT NULL PRIMARY KEY,
            project_name           TEXT    NOT NULL,
            project_format_version INTEGER NOT NULL,
            created_at_utc_ms      INTEGER NOT NULL,
            updated_at_utc_ms      INTEGER NOT NULL
        );
        """;

    /// <summary>core_map（P2-09-11 §4.1）。lifecycle_status 词汇表未冻结（open gate），故不加 CHECK；CD-01 服务统一写 'active'。</summary>
    public const string CoreMapTableSql = """
        CREATE TABLE core_map (
            map_id            TEXT    NOT NULL PRIMARY KEY,
            project_id        TEXT    NOT NULL,
            map_name          TEXT    NOT NULL,
            lifecycle_status  TEXT    NOT NULL,
            created_at_utc_ms INTEGER NOT NULL,
            updated_at_utc_ms INTEGER NOT NULL,
            UNIQUE (project_id, map_id),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id)
        );
        """;

    /// <summary>
    /// core_history + core_branch（P2-09-11 §4.3，对应 P2-09-13 槽位 005 的 History/Branch 部分）。
    /// core_history 不回指 core_branch（Branch 行与自有 History 同事务创建，避免循环 FK）；
    /// 继承经 parent_history_id + parent_at 计算，祖先数据不复制。
    /// </summary>
    public const string CoreHistoryBranchTablesSql = """
        CREATE TABLE core_history (
            history_id        TEXT    NOT NULL PRIMARY KEY,
            project_id        TEXT    NOT NULL,
            history_kind      TEXT    NOT NULL CHECK (history_kind IN ('map_root', 'branch_owned')),
            owner_map_id      TEXT    NULL,
            created_at_utc_ms INTEGER NOT NULL,
            CHECK (
                (history_kind = 'map_root'    AND owner_map_id IS NOT NULL)
                OR (history_kind = 'branch_owned' AND owner_map_id IS NULL)
            ),
            UNIQUE (project_id, history_id),
            UNIQUE (project_id, owner_map_id),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id),
            FOREIGN KEY (project_id, owner_map_id) REFERENCES core_map (project_id, map_id)
        );

        CREATE TABLE core_branch (
            branch_id             TEXT    NOT NULL PRIMARY KEY,
            project_id            TEXT    NOT NULL,
            parent_history_id     TEXT    NOT NULL,
            own_history_id        TEXT    NOT NULL,
            parent_day_number     INTEGER NOT NULL CHECK (parent_day_number >= 0),
            parent_offset_seconds INTEGER NOT NULL CHECK (parent_offset_seconds BETWEEN 0 AND 86399),
            branch_name           TEXT    NOT NULL,
            lifecycle_status      TEXT    NOT NULL,
            created_at_utc_ms     INTEGER NOT NULL,
            updated_at_utc_ms     INTEGER NOT NULL,
            CHECK (parent_history_id <> own_history_id),
            UNIQUE (project_id, branch_id),
            UNIQUE (project_id, own_history_id),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id),
            FOREIGN KEY (project_id, parent_history_id) REFERENCES core_history (project_id, history_id),
            FOREIGN KEY (project_id, own_history_id)    REFERENCES core_history (project_id, history_id)
        );
        """;

    /// <summary>
    /// core_calendar + core_calendar_configuration（P2-09-11 §4.1/§4.4）。
    /// partial unique index 保证每 Map 恰好一个 default Calendar；
    /// 配置区间语义按 P2-09-14（半开区间；CD-01 仅写 day-zero 结构占位行）。
    /// </summary>
    public const string CoreCalendarTablesSql = """
        CREATE TABLE core_calendar (
            calendar_id      TEXT    NOT NULL PRIMARY KEY,
            project_id       TEXT    NOT NULL,
            map_id           TEXT    NOT NULL,
            calendar_role    TEXT    NOT NULL CHECK (calendar_role IN ('default', 'auxiliary')),
            calendar_type    TEXT    NULL,
            calendar_name    TEXT    NOT NULL,
            created_at_utc_ms INTEGER NOT NULL,
            updated_at_utc_ms INTEGER NOT NULL,
            UNIQUE (project_id, calendar_id),
            UNIQUE (project_id, map_id, calendar_id),
            FOREIGN KEY (project_id, map_id) REFERENCES core_map (project_id, map_id)
        );

        CREATE UNIQUE INDEX ux_core_calendar_default_per_map
            ON core_calendar (project_id, map_id)
            WHERE calendar_role = 'default';

        CREATE TABLE core_calendar_configuration (
            calendar_configuration_id   TEXT    NOT NULL PRIMARY KEY,
            project_id                  TEXT    NOT NULL,
            map_id                      TEXT    NOT NULL,
            calendar_id                 TEXT    NOT NULL,
            owner_history_id            TEXT    NOT NULL,
            source_command_id           TEXT    NULL,
            configuration_origin        TEXT    NOT NULL CHECK (configuration_origin IN ('user_created', 'command_managed')),
            effective_from_day_number      INTEGER NOT NULL CHECK (effective_from_day_number >= 0),
            effective_from_offset_seconds  INTEGER NOT NULL CHECK (effective_from_offset_seconds BETWEEN 0 AND 86399),
            effective_until_day_number     INTEGER NULL CHECK (effective_until_day_number IS NULL OR effective_until_day_number >= 0),
            effective_until_offset_seconds INTEGER NULL CHECK (effective_until_offset_seconds IS NULL OR effective_until_offset_seconds BETWEEN 0 AND 86399),
            rules_schema_version        INTEGER NOT NULL,
            rules_payload_json          TEXT    NOT NULL,
            created_at_utc_ms           INTEGER NOT NULL,
            updated_at_utc_ms           INTEGER NOT NULL,
            CHECK (
                (configuration_origin = 'user_created'     AND source_command_id IS NULL)
                OR (configuration_origin = 'command_managed' AND source_command_id IS NOT NULL)
            ),
            CHECK (
                (effective_until_day_number IS NULL AND effective_until_offset_seconds IS NULL)
                OR (effective_until_day_number IS NOT NULL AND effective_until_offset_seconds IS NOT NULL)
            ),
            UNIQUE (project_id, calendar_configuration_id),
            UNIQUE (project_id, map_id, calendar_id, owner_history_id, effective_from_day_number, effective_from_offset_seconds),
            FOREIGN KEY (project_id, map_id, calendar_id) REFERENCES core_calendar (project_id, map_id, calendar_id),
            FOREIGN KEY (project_id, owner_history_id)    REFERENCES core_history (project_id, history_id)
        );
        """;

    /// <summary>
    /// core_object_type（P2-09-11 §4.2；CD-02-A §7/§8）。Project 级 Object Type
    /// 配置权威：稳定 type_key + 可变显示名 + 分配格式配置。可执行 descriptor schema/
    /// semantics 由编译期 registry 提供，此行不是可执行 schema（P2-09-18 §4.2）。
    /// 十内置类型在 Project 创建事务内播种（OD-4 建议案；P2-09-21 V0.1 种子目录）。
    /// </summary>
    public const string CoreObjectTypeTableSql = """
        CREATE TABLE core_object_type (
            object_type_id            TEXT    NOT NULL PRIMARY KEY,
            project_id                TEXT    NOT NULL,
            type_key                  TEXT    NOT NULL,
            display_name              TEXT    NOT NULL,
            allocatable               INTEGER NOT NULL CHECK (allocatable IN (0, 1)),
            allocation_format_json    TEXT    NOT NULL,
            allocation_format_version INTEGER NOT NULL,
            created_at_utc_ms         INTEGER NOT NULL,
            updated_at_utc_ms         INTEGER NOT NULL,
            UNIQUE (project_id, object_type_id),
            UNIQUE (project_id, type_key),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id)
        );
        """;

    /// <summary>
    /// core_object + core_object_definition + core_object_id_allocator +
    /// core_object_id_reservation（P2-09-11 §4.2；CD-02-A §5/§6/§8）。
    /// core_object 持身份级状态（P1 存在层）；CHECK 强制 scope/owner 配对
    /// （project_shared⇒owner_map_id 为空，map_local⇒非空；owner_map_id 复合 FK
    /// 在 NULL 时跳过校验为 SQLite 语义，map_local 侧由 CHECK 保证强制）。
    /// core_object_definition 每 identity 至多一行当前定义（PK/FK 复合键天然防重）。
    /// allocator 的 next_sequence 仅在与 reservation 插入同事务内前进。
    /// reservation 状态机 CHECK：Reserved⇒identity 为空、Bound/Inserted⇒非空、
    /// Abandoned 皆可；identity 溯源复合 FK + 非空 UNIQUE（每 identity 至多一条）。
    /// request_key 求值 UNIQUE 在 SQLite 下 NULL 互相不冲突，等价"提供时唯一"。
    /// </summary>
    public const string CoreObjectIdentityTablesSql = """
        CREATE TABLE core_object (
            object_identity_id         TEXT    NOT NULL PRIMARY KEY,
            project_id                 TEXT    NOT NULL,
            object_type_id             TEXT    NOT NULL,
            object_type_schema_version INTEGER NOT NULL,
            object_type_semantics_id   TEXT    NOT NULL,
            sharing_scope              TEXT    NOT NULL CHECK (sharing_scope IN ('project_shared', 'map_local')),
            owner_map_id               TEXT    NULL,
            lifecycle_status           TEXT    NOT NULL CHECK (lifecycle_status IN ('draft', 'active', 'abandoned', 'retired')),
            created_at_utc_ms          INTEGER NOT NULL,
            updated_at_utc_ms          INTEGER NOT NULL,
            CHECK (
                (sharing_scope = 'project_shared' AND owner_map_id IS NULL)
                OR (sharing_scope = 'map_local' AND owner_map_id IS NOT NULL)
            ),
            UNIQUE (project_id, object_identity_id),
            UNIQUE (project_id, object_type_id, object_identity_id),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id),
            FOREIGN KEY (project_id, object_type_id) REFERENCES core_object_type (project_id, object_type_id),
            FOREIGN KEY (project_id, owner_map_id) REFERENCES core_map (project_id, map_id)
        );

        CREATE TABLE core_object_definition (
            project_id                TEXT    NOT NULL,
            object_identity_id        TEXT    NOT NULL,
            definition_schema_version INTEGER NOT NULL,
            definition_payload_json   TEXT    NOT NULL,
            definition_hash           TEXT    NOT NULL,
            updated_at_utc_ms         INTEGER NOT NULL,
            PRIMARY KEY (project_id, object_identity_id),
            FOREIGN KEY (project_id, object_identity_id) REFERENCES core_object (project_id, object_identity_id)
        );

        CREATE TABLE core_object_id_allocator (
            project_id               TEXT    NOT NULL,
            object_type_id           TEXT    NOT NULL,
            next_sequence            INTEGER NOT NULL CHECK (next_sequence >= 1),
            allocator_format_version INTEGER NOT NULL,
            created_at_utc_ms        INTEGER NOT NULL,
            updated_at_utc_ms        INTEGER NOT NULL,
            PRIMARY KEY (project_id, object_type_id),
            FOREIGN KEY (project_id, object_type_id) REFERENCES core_object_type (project_id, object_type_id)
        );

        CREATE TABLE core_object_id_reservation (
            reservation_id     TEXT    NOT NULL PRIMARY KEY,
            project_id         TEXT    NOT NULL,
            object_type_id     TEXT    NOT NULL,
            object_identity_id TEXT    NULL,
            allocated_sequence INTEGER NOT NULL CHECK (allocated_sequence >= 1),
            allocated_id_value TEXT    NOT NULL,
            reservation_state  TEXT    NOT NULL CHECK (reservation_state IN ('Reserved', 'Bound', 'Inserted', 'Abandoned')),
            request_key        TEXT    NULL,
            abandon_reason     TEXT    NULL,
            created_at_utc_ms  INTEGER NOT NULL,
            updated_at_utc_ms  INTEGER NOT NULL,
            CHECK (
                (reservation_state = 'Reserved' AND object_identity_id IS NULL)
                OR (reservation_state IN ('Bound', 'Inserted') AND object_identity_id IS NOT NULL)
                OR (reservation_state = 'Abandoned')
            ),
            CHECK (abandon_reason IS NULL OR reservation_state = 'Abandoned'),
            UNIQUE (project_id, reservation_id),
            UNIQUE (project_id, object_type_id, allocated_sequence),
            UNIQUE (project_id, object_type_id, allocated_id_value),
            UNIQUE (project_id, request_key),
            UNIQUE (project_id, object_type_id, object_identity_id),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id),
            FOREIGN KEY (project_id, object_type_id) REFERENCES core_object_type (project_id, object_type_id),
            FOREIGN KEY (project_id, object_type_id, object_identity_id)
                REFERENCES core_object (project_id, object_type_id, object_identity_id)
        );
        """;

    /// <summary>
    /// core_map_object_ref（P2-09-11 §4.1；CD-02-A §9）。Map 对正式 Object 的
    /// 显式注册（P2 存在层）；不复制 sharing_scope/owner_map_id（scope 从 core_object
    /// 解析）。map_local 跨 Map 注册拒绝由 Domain 服务强制；INSERT/UPDATE 触发器为
    /// 纵深防御（P2-09-11 §4.1 要求）。注册≠Initial State 成员。
    /// </summary>
    public const string CoreMapObjectRefTableSql = """
        CREATE TABLE core_map_object_ref (
            map_object_ref_id  TEXT    NOT NULL PRIMARY KEY,
            project_id         TEXT    NOT NULL,
            map_id             TEXT    NOT NULL,
            object_identity_id TEXT    NOT NULL,
            created_at_utc_ms  INTEGER NOT NULL,
            UNIQUE (project_id, map_object_ref_id),
            UNIQUE (project_id, map_id, object_identity_id),
            FOREIGN KEY (project_id, map_id) REFERENCES core_map (project_id, map_id),
            FOREIGN KEY (project_id, object_identity_id) REFERENCES core_object (project_id, object_identity_id)
        );

        CREATE TRIGGER trg_core_map_object_ref_insert_scope
        BEFORE INSERT ON core_map_object_ref
        WHEN EXISTS (
            SELECT 1 FROM core_object o
            WHERE o.project_id = NEW.project_id
              AND o.object_identity_id = NEW.object_identity_id
              AND o.sharing_scope = 'map_local'
              AND o.owner_map_id <> NEW.map_id
        )
        BEGIN
            SELECT RAISE(ABORT, 'core_map_object_ref: map_local object may only be registered in its owner map.');
        END;

        CREATE TRIGGER trg_core_map_object_ref_update_scope
        BEFORE UPDATE OF project_id, map_id, object_identity_id ON core_map_object_ref
        WHEN EXISTS (
            SELECT 1 FROM core_object o
            WHERE o.project_id = NEW.project_id
              AND o.object_identity_id = NEW.object_identity_id
              AND o.sharing_scope = 'map_local'
              AND o.owner_map_id <> NEW.map_id
        )
        BEGIN
            SELECT RAISE(ABORT, 'core_map_object_ref: map_local object may only be registered in its owner map.');
        END;
        """;

    /// <summary>
    /// core_map_initial_state + core_map_initial_object（P2-09-11 §4.1；
    /// CD-02-A §10）。每 Map 恰一个逻辑 Initial State（UNIQUE(project_id,map_id)）。
    /// 成员行复合 FK 指向 core_map_object_ref——数据库层直接强制"成员必须有注册行"
    /// 跨表不变量；内容层 typed schema 为 open decision（CD-02-A OD-1），本阶段仅
    /// 结构层（canonical JSON + fingerprint）。placement payload 与 schema version
    /// 成对可空；relationship payload 留待 Relationship 阶段。
    /// PRE-FOUNDATION-01B 形态：baseline 行的 mutable fingerprint/schema 版本列与
    /// 成员表由收敛迁移（CoreMapInitialStateConvergenceSql，FOUNDATION-01B §9）退役，
    /// 统一替换为 revision/cursor 存储；初始迁移保留本形态以保证已有开发库与新建库
    /// 走同一条迁移序列。
    /// </summary>
    public const string CoreMapInitialStateTablesSql = """
        CREATE TABLE core_map_initial_state (
            initial_state_id        TEXT    NOT NULL PRIMARY KEY,
            project_id              TEXT    NOT NULL,
            map_id                  TEXT    NOT NULL,
            baseline_schema_version INTEGER NOT NULL,
            content_fingerprint     TEXT    NOT NULL,
            created_at_utc_ms       INTEGER NOT NULL,
            updated_at_utc_ms       INTEGER NOT NULL,
            UNIQUE (project_id, map_id),
            UNIQUE (project_id, initial_state_id),
            UNIQUE (project_id, map_id, initial_state_id),
            FOREIGN KEY (project_id, map_id) REFERENCES core_map (project_id, map_id)
        );

        CREATE TABLE core_map_initial_object (
            project_id                       TEXT    NOT NULL,
            map_id                           TEXT    NOT NULL,
            initial_state_id                 TEXT    NOT NULL,
            object_identity_id               TEXT    NOT NULL,
            initial_membership_payload_json  TEXT    NOT NULL,
            initial_placement_payload_json   TEXT    NULL,
            initial_placement_schema_version INTEGER NULL,
            created_at_utc_ms                INTEGER NOT NULL,
            PRIMARY KEY (initial_state_id, object_identity_id),
            CHECK (
                (initial_placement_payload_json IS NULL AND initial_placement_schema_version IS NULL)
                OR (initial_placement_payload_json IS NOT NULL AND initial_placement_schema_version IS NOT NULL)
            ),
            FOREIGN KEY (project_id, map_id, initial_state_id)
                REFERENCES core_map_initial_state (project_id, map_id, initial_state_id),
            FOREIGN KEY (project_id, map_id, object_identity_id)
                REFERENCES core_map_object_ref (project_id, map_id, object_identity_id),
            FOREIGN KEY (project_id, object_identity_id)
                REFERENCES core_object (project_id, object_identity_id)
        );
        """;

    /// <summary>
    /// core_asset（P2-09-11 §4.6；CD-02-D Asset 权威元数据）。
    /// Asset 字节不可变且存于 SQLite 之外（FileAssetStore 内容寻址）；本表只承载
    /// 语义元数据与完整性状态，绝不覆写已发布文件。hash_algorithm V1 仅 sha-256
    /// （与 FileAssetStore 一致）；publication_state 仅 'published'——AI staging
    /// 永不进入 core_asset（P2-09-06 §5/§6：正式引用前必须已发布）。
    /// integrity_state 由完整性扫描服务管理（verified/unverified/corrupt/missing）。
    /// </summary>

    /// <summary>
    /// Immutable complete snapshots and the single mutable active cursor for time-zero authoring.
    /// Created by the initial migration, then dropped and recreated by the convergence
    /// migration, which also retires the mutable baseline/member storage (FOUNDATION-01B §9).
    /// </summary>
    public const string CoreMapInitialStateRevisionTablesSql = """
        CREATE TABLE core_map_initial_state_revision (
            revision_id TEXT NOT NULL PRIMARY KEY,
            project_id TEXT NOT NULL,
            map_id TEXT NOT NULL,
            initial_state_id TEXT NOT NULL,
            revision_number INTEGER NOT NULL CHECK (revision_number >= 0),
            parent_revision_id TEXT NULL,
            baseline_schema_version INTEGER NOT NULL,
            content_fingerprint TEXT NOT NULL CHECK (length(content_fingerprint) = 64 AND content_fingerprint NOT GLOB '*[^0-9a-f]*'),
            operation_kind TEXT NULL,
            operation_payload_json TEXT NULL,
            created_at_utc_ms INTEGER NOT NULL,
            CHECK ((operation_kind IS NULL AND operation_payload_json IS NULL) OR (operation_kind IS NOT NULL AND operation_payload_json IS NOT NULL)),
            CHECK ((revision_number = 0 AND parent_revision_id IS NULL AND operation_kind IS NULL) OR (revision_number > 0 AND parent_revision_id IS NOT NULL AND operation_kind IS NOT NULL)),
            UNIQUE (project_id, map_id, initial_state_id, revision_id),
            UNIQUE (project_id, map_id, initial_state_id, revision_number),
            FOREIGN KEY (project_id, map_id, initial_state_id) REFERENCES core_map_initial_state (project_id, map_id, initial_state_id),
            FOREIGN KEY (project_id, map_id, initial_state_id, parent_revision_id) REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id)
        );

        CREATE TABLE core_map_initial_state_revision_member (
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
            CHECK ((initial_placement_payload_json IS NULL AND initial_placement_schema_version IS NULL) OR (initial_placement_payload_json IS NOT NULL AND initial_placement_schema_version IS NOT NULL)),
            FOREIGN KEY (project_id, map_id, initial_state_id, revision_id) REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id),
            FOREIGN KEY (project_id, map_id, object_identity_id) REFERENCES core_map_object_ref (project_id, map_id, object_identity_id),
            FOREIGN KEY (project_id, object_identity_id) REFERENCES core_object (project_id, object_identity_id)
        );

        CREATE TABLE core_map_initial_state_cursor (
            project_id TEXT NOT NULL,
            map_id TEXT NOT NULL,
            initial_state_id TEXT NOT NULL,
            active_revision_id TEXT NOT NULL,
            selected_redo_child_revision_id TEXT NULL,
            cursor_version INTEGER NOT NULL CHECK (cursor_version >= 0),
            updated_at_utc_ms INTEGER NOT NULL,
            PRIMARY KEY (project_id, map_id),
            FOREIGN KEY (project_id, map_id, initial_state_id) REFERENCES core_map_initial_state (project_id, map_id, initial_state_id),
            FOREIGN KEY (project_id, map_id, initial_state_id, active_revision_id) REFERENCES core_map_initial_state_revision (project_id, map_id, initial_state_id, revision_id)
        );

        CREATE TRIGGER trg_core_map_initial_state_revision_no_update BEFORE UPDATE ON core_map_initial_state_revision
        BEGIN SELECT RAISE(ABORT, 'baseline revisions are immutable'); END;
        CREATE TRIGGER trg_core_map_initial_state_revision_no_delete BEFORE DELETE ON core_map_initial_state_revision
        BEGIN SELECT RAISE(ABORT, 'baseline revisions are immutable'); END;
        CREATE TRIGGER trg_core_map_initial_state_revision_member_no_update BEFORE UPDATE ON core_map_initial_state_revision_member
        BEGIN SELECT RAISE(ABORT, 'baseline revision members are immutable'); END;
        CREATE TRIGGER trg_core_map_initial_state_revision_member_no_delete BEFORE DELETE ON core_map_initial_state_revision_member
        BEGIN SELECT RAISE(ABORT, 'baseline revision members are immutable'); END;
        """;

    public const string CoreAssetTableSql = """
        CREATE TABLE core_asset (
            asset_id          TEXT    NOT NULL PRIMARY KEY,
            project_id        TEXT    NOT NULL,
            content_hash      TEXT    NOT NULL,
            hash_algorithm    TEXT    NOT NULL CHECK (hash_algorithm = 'sha-256'),
            byte_length       INTEGER NOT NULL CHECK (byte_length >= 0),
            media_type        TEXT    NOT NULL,
            format_version    TEXT    NOT NULL,
            relative_path     TEXT    NOT NULL,
            publication_state TEXT    NOT NULL CHECK (publication_state IN ('published')),
            integrity_state   TEXT    NOT NULL CHECK (integrity_state IN ('verified', 'unverified', 'corrupt', 'missing')),
            created_at_utc_ms INTEGER NOT NULL,
            UNIQUE (project_id, asset_id),
            UNIQUE (project_id, hash_algorithm, content_hash),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id)
        );
        """;

    /// <summary>
    /// core_object_definition_asset（P2-09-11 §4.6 typed asset link；P2-09-19 §9）。
    /// 行仅含 owner keys + asset_id + role——绝不塞入 content hash 或组件 ID
    /// （权威 hash 从 core_asset 解析比对，组件 ID 在 owner payload manifest 内，
    /// CD-02-A §12）。Definition geometry 引用随 Building/Cave adoption 启用
    /// （P2-09-13 §3.2 条件性规则；启用触发条件已由 CD-02-E adoption 满足）。
    /// core_map_initial_object_asset 不启用（当前 Initial State payload 无独立
    /// geometry Asset 引用）；core_command_asset 属 Command 阶段。
    /// </summary>
    public const string CoreObjectDefinitionAssetTableSql = """
        CREATE TABLE core_object_definition_asset (
            object_definition_asset_id TEXT    NOT NULL PRIMARY KEY,
            project_id                 TEXT    NOT NULL,
            object_identity_id         TEXT    NOT NULL,
            asset_id                   TEXT    NOT NULL,
            role                       TEXT    NOT NULL,
            created_at_utc_ms          INTEGER NOT NULL,
            UNIQUE (project_id, object_definition_asset_id),
            UNIQUE (project_id, object_identity_id, asset_id, role),
            FOREIGN KEY (project_id) REFERENCES core_project (project_id),
            FOREIGN KEY (project_id, object_identity_id) REFERENCES core_object (project_id, object_identity_id),
            FOREIGN KEY (project_id, asset_id) REFERENCES core_asset (project_id, asset_id)
        );
        """;

    /// <summary>
    /// FOUNDATION-01B §9 convergence: retires the mutable baseline/member storage and
    /// converts existing development data into the revision/cursor model.
    ///
    /// Conversion rules (runs uniformly on every database — the initial migration always
    /// created the pre-01B shapes and the interim revision migration always created empty
    /// revision storage):
    /// 1. Existing mutable member rows are preserved verbatim as revision zero members
    ///    (revision zero id = initial_state id, deterministic). The old baseline's
    ///    content_fingerprint is carried over unchanged: the fingerprint composition
    ///    (initial-state-fingerprint-v1) and the member payloads are identical, so the
    ///    carried value IS the Domain-recomputed fingerprint of the revision-zero member
    ///    set. No fingerprint is recomputed or invented inside SQL.
    /// 2. A Map without a mutable baseline receives an empty revision zero with the
    ///    Domain empty-snapshot fingerprint constant and a fresh baseline identity.
    /// 3. Every Map receives a cursor active on its revision zero (cursor_version 0,
    ///    no redo selection), so a Map without baseline/cursor after migration is
    ///    impossible (data-integrity error, not a lazy-creation state).
    /// 4. core_map_initial_object and the mutable baseline columns are dropped; the
    ///    revision/member/cursor storage is recreated under the final shapes. No
    ///    dual-write or compatibility layer remains.
    /// </summary>
    public const string CoreMapInitialStateConvergencePreparationSql = """
        CREATE TEMP TABLE t_initial_state_member_preserved AS
            SELECT project_id, map_id, initial_state_id, object_identity_id,
                   initial_membership_payload_json, initial_placement_payload_json,
                   initial_placement_schema_version, created_at_utc_ms
            FROM core_map_initial_object;

        CREATE TEMP TABLE t_baseline_fingerprint_preserved AS
            SELECT project_id, map_id, baseline_schema_version, content_fingerprint
            FROM core_map_initial_state;

        DROP TABLE core_map_initial_state_cursor;
        DROP TABLE core_map_initial_state_revision_member;
        DROP TABLE core_map_initial_state_revision;
        DROP TABLE core_map_initial_object;

        CREATE TABLE core_map_initial_state_converged (
            initial_state_id   TEXT    NOT NULL PRIMARY KEY,
            project_id         TEXT    NOT NULL,
            map_id             TEXT    NOT NULL,
            created_at_utc_ms  INTEGER NOT NULL,
            UNIQUE (project_id, map_id),
            UNIQUE (project_id, initial_state_id),
            UNIQUE (project_id, map_id, initial_state_id),
            FOREIGN KEY (project_id, map_id) REFERENCES core_map (project_id, map_id)
        );

        INSERT INTO core_map_initial_state_converged (initial_state_id, project_id, map_id, created_at_utc_ms)
            SELECT initial_state_id, project_id, map_id, created_at_utc_ms FROM core_map_initial_state;

        -- Maps without a baseline receive a fresh baseline identity + empty revision zero.
        -- Per-row identity generation: each Map's 16 random bytes are produced by a bare
        -- result-column expression (non-deterministic functions in a result list are evaluated
        -- once per output row — planner-independent), staged in a temp table, and the canonical
        -- UUID is then sliced from those SAME stored bytes. A non-correlated FROM-subquery
        -- expression is NOT safe here: the planner may materialize it once and hand every Map
        -- the same initial_state_id, failing the primary key on the second baseline-less Map.
        CREATE TEMP TABLE t_new_baseline_id (
            project_id        TEXT    NOT NULL,
            map_id            TEXT    NOT NULL,
            raw_random_hex    TEXT    NOT NULL,
            created_at_utc_ms INTEGER NOT NULL
        );

        INSERT INTO t_new_baseline_id (project_id, map_id, raw_random_hex, created_at_utc_ms)
            SELECT m.project_id, m.map_id, hex(randomblob(16)), m.created_at_utc_ms
            FROM core_map m
            WHERE NOT EXISTS (
                SELECT 1 FROM core_map_initial_state b
                WHERE b.project_id = m.project_id AND b.map_id = m.map_id);

        INSERT INTO core_map_initial_state_converged (initial_state_id, project_id, map_id, created_at_utc_ms)
            SELECT
                lower(
                    substr(t.raw_random_hex, 1, 8) || '-' || substr(t.raw_random_hex, 9, 4)
                    || '-' || substr(t.raw_random_hex, 13, 4) || '-' || substr(t.raw_random_hex, 17, 4)
                    || '-' || substr(t.raw_random_hex, 21, 12)),
                t.project_id,
                t.map_id,
                t.created_at_utc_ms
            FROM t_new_baseline_id t;

        DROP TABLE t_new_baseline_id;

        DROP TABLE core_map_initial_state;
        ALTER TABLE core_map_initial_state_converged RENAME TO core_map_initial_state;
        """;

    /// <summary>
    /// Convergence backfill: executed after <see cref="CoreMapInitialStateRevisionTablesSql"/>
    /// has recreated the revision/member/cursor storage over the converged baseline table.
    /// Existing baselines keep their carried fingerprint (identical composition, see the
    /// convergence summary above); baselines created for previously baseline-less Maps use
    /// the Domain empty-snapshot fingerprint ({0}). Revision zero id = initial state id,
    /// deterministic and unique per baseline. Every Map ends with a cursor active on its
    /// revision zero.
    /// </summary>
    public const string CoreMapInitialStateConvergenceBackfillSql = """
        INSERT INTO core_map_initial_state_revision
            (revision_id, project_id, map_id, initial_state_id, revision_number, parent_revision_id,
             baseline_schema_version, content_fingerprint, operation_kind, operation_payload_json, created_at_utc_ms)
            SELECT b.initial_state_id, b.project_id, b.map_id, b.initial_state_id, 0, NULL,
                   COALESCE(f.baseline_schema_version, 1),
                   COALESCE(f.content_fingerprint, '{0}'),
                   NULL, NULL, b.created_at_utc_ms
            FROM core_map_initial_state b
            LEFT JOIN t_baseline_fingerprint_preserved f
                ON f.project_id = b.project_id AND f.map_id = b.map_id;

        INSERT INTO core_map_initial_state_revision_member
            (project_id, map_id, initial_state_id, revision_id, object_identity_id,
             initial_membership_payload_json, initial_placement_payload_json,
             initial_placement_schema_version, created_at_utc_ms)
            SELECT project_id, map_id, initial_state_id, initial_state_id, object_identity_id,
                   initial_membership_payload_json, initial_placement_payload_json,
                   initial_placement_schema_version, created_at_utc_ms
            FROM t_initial_state_member_preserved;

        DROP TABLE t_initial_state_member_preserved;
        DROP TABLE t_baseline_fingerprint_preserved;

        INSERT INTO core_map_initial_state_cursor
            (project_id, map_id, initial_state_id, active_revision_id,
             selected_redo_child_revision_id, cursor_version, updated_at_utc_ms)
            SELECT b.project_id, b.map_id, b.initial_state_id, b.initial_state_id,
                   NULL, 0, b.created_at_utc_ms
            FROM core_map_initial_state b;
        """;

    /// <summary>
    /// Ordered DDL statements used by the single EF Core initial migration.
    /// This list is SQL organization only; it is not a migration history or ledger.
    /// Later migrations execute their own constants (revision storage, convergence).
    /// </summary>
    public static IReadOnlyList<string> Statements { get; } =
    [
        CoreProjectTableSql,
        CoreMapTableSql,
        CoreHistoryBranchTablesSql,
        CoreCalendarTablesSql,
        CoreObjectTypeTableSql,
        CoreObjectIdentityTablesSql,
        CoreMapObjectRefTableSql,
        CoreMapInitialStateTablesSql,
        CoreAssetTableSql,
        CoreObjectDefinitionAssetTableSql,
    ];
}
