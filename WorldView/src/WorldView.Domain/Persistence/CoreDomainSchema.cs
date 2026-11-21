using System;
using System.Collections.Generic;

namespace WorldView.Domain.Persistence;

/// <summary>
/// Core Domain 模块（module_id="core-domain"）的迁移内容（CD-01 子集）。
/// 表结构取自 P2-09-11 V0.11 §4（提案；本文件是其最小可执行子集的正式落地），
/// 迁移槽位顺序遵循 P2-09-13 V0.6 §5 依赖图（ledger → core_project → core_map →
/// core_history/core_branch → core_calendar）。
/// 约定（P2-09-11 §2/§3）：canonical 小写连字符 UUID TEXT 主键；snake_case；
/// 运营时间戳 *_utc_ms INTEGER；历史时间为 (day_number, offset_seconds) 整数对；
/// 每连接 PRAGMA foreign_keys=ON；正式身份/历史表不用 ON DELETE CASCADE（默认 NO ACTION）；
/// 同 Project 归属以 UNIQUE(project_id, entity_id) + 复合外键防跨库误引用。
/// 刻意排除的表（open gate，未授权落地）：core_map_initial_state（payload 形状未定）、
/// core_object 系、core_command 系、core_asset/core_checkpoint、全部 AI Assistant 表。
/// </summary>
public static class CoreDomainSchema
{
    public const string ModuleId = "core-domain";

    /// <summary>
    /// 001：core_project（P2-09-11 §4.1）。V1 每库恰好一行——单行规则由 Core Domain
    /// 服务强制（读取时 0 行→NotEstablished、&gt;1 行→MultipleProjects；插入前校验）。
    /// </summary>
    public const string Migration001CoreProject = """
        CREATE TABLE core_project (
            project_id             TEXT    NOT NULL PRIMARY KEY,
            project_name           TEXT    NOT NULL,
            project_format_version INTEGER NOT NULL,
            created_at_utc_ms      INTEGER NOT NULL,
            updated_at_utc_ms      INTEGER NOT NULL
        );
        """;

    /// <summary>002：core_map（P2-09-11 §4.1）。lifecycle_status 词汇表未冻结（open gate），故不加 CHECK；CD-01 服务统一写 'active'。</summary>
    public const string Migration002CoreMapIdentity = """
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
    /// 003：core_history + core_branch（P2-09-11 §4.3，对应 P2-09-13 槽位 005 的 History/Branch 部分）。
    /// core_history 不回指 core_branch（Branch 行与自有 History 同事务创建，避免循环 FK）；
    /// 继承经 parent_history_id + parent_at 计算，祖先数据不复制。
    /// </summary>
    public const string Migration003CoreHistoryBranch = """
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
    /// 004：core_calendar + core_calendar_configuration（P2-09-11 §4.1/§4.4）。
    /// partial unique index 保证每 Map 恰好一个 default Calendar；
    /// 配置区间语义按 P2-09-14（半开区间；CD-01 仅写 day-zero 结构占位行）。
    /// </summary>
    public const string Migration004CoreCalendar = """
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

    /// <summary>迁移清单（顺序即依赖序：project → map → history/branch → calendar）。</summary>
    public static IReadOnlyList<(string MigrationId, string Sql)> Migrations { get; } =
    [
        ("001_core_project", Migration001CoreProject),
        ("002_core_map_identity", Migration002CoreMapIdentity),
        ("003_core_history_branch", Migration003CoreHistoryBranch),
        ("004_core_calendar", Migration004CoreCalendar),
    ];
}
