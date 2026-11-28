using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// FOUNDATION-01B §9 收敛迁移行为验证：构造一个"已有开发数据库"（只应用收敛前的
/// 迁移、持有 mutable baseline + 成员行 + 无 baseline 的 Map），再前滚到最新 schema，
/// 验证旧数据无损转换为 revision zero / 成员 / cursor，且旧表彻底退役。
/// 指纹由 Domain 组成契约计算后以旧形态写入，迁移只允许原样携带（不允许发明）。
/// </summary>
public sealed class CoreMapInitialStateConvergenceMigrationTests : IDisposable
{
    private const long Now = 1_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly SqlitePackageDatabase _database;

    public CoreMapInitialStateConvergenceMigrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-convergence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private SqliteConnectionAccess Access() => ((ISqliteConnectionProvider)_database).GetAccess();

    private CoreDomainEfSession Session() => new(((ISqliteConnectionProvider)_database).GetAccess());

    private long CountRows(string table, string? where = null) =>
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table} {where ?? string.Empty};";
            return (long)command.ExecuteScalar()!;
        });

    private void ExecuteRaw(string sql) =>
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        });

    [Fact]
    public void Convergence_PreservesMutableBaselineAsRevisionZeroAndFillsBaselinelessMaps()
    {
        // 1) 只应用收敛前的迁移序列（FOUNDATIONS-01B 之前的开发库形态）。
        Session().Execute(db => db.Database.Migrate("20261128014741_CoreMapInitialStateRevisions"));
        Assert.True(CountRows("sqlite_master", "WHERE type = 'table' AND name = 'core_map_initial_object'") == 1);

        var projectId = Guid.NewGuid();
        var mapWithBaselineId = Guid.NewGuid();
        var mapWithoutBaselineId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var initialStateId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();

        // 2) 以旧形态种入开发数据（raw SQL；core_map 的 map_space/spatial_reference 列可空）。
        ExecuteRaw($"""
            INSERT INTO core_project (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{projectId:D}', 'Legacy Dev', 1, {Now}, {Now});
            INSERT INTO core_map (map_id, project_id, map_space_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{mapWithBaselineId:D}', '{projectId:D}', '{Guid.NewGuid():D}', 'With Baseline', 'active', {Now}, {Now});
            INSERT INTO core_map (map_id, project_id, map_space_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{mapWithoutBaselineId:D}', '{projectId:D}', '{Guid.NewGuid():D}', 'Without Baseline', 'active', {Now}, {Now});
            INSERT INTO core_object_type (object_type_id, project_id, type_key, display_name, allocatable, allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{typeId:D}', '{projectId:D}', 'river', 'River', 1, 'fmt', 1, {Now}, {Now});
            INSERT INTO core_object (object_identity_id, project_id, object_type_id, object_type_schema_version, object_type_semantics_id, sharing_scope, owner_map_id, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{objectId:D}', '{projectId:D}', '{typeId:D}', 2, 'world-view.object-type.river.v1', 'map_local', '{mapWithBaselineId:D}', 'active', {Now}, {Now});
            INSERT INTO core_map_object_ref (map_object_ref_id, project_id, map_id, object_identity_id, created_at_utc_ms)
            VALUES ('{registrationId:D}', '{projectId:D}', '{mapWithBaselineId:D}', '{objectId:D}', {Now});
            """);

        // 旧 baseline 行 + 一条成员行；指纹按 Domain 组成契约计算（迁移必须原样携带）。
        var membershipPayload = """{"side":"north"}""";
        var carriedFingerprint = InitialStateFingerprint.Compute(
            MapInitialStateService.StructuralBaselineSchemaVersion,
            new[] { MakeMemberRecord(projectId, mapWithBaselineId, initialStateId, objectId, membershipPayload) });
        ExecuteRaw($"""
            INSERT INTO core_map_initial_state (initial_state_id, project_id, map_id, baseline_schema_version, content_fingerprint, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{initialStateId:D}', '{projectId:D}', '{mapWithBaselineId:D}', 1, '{carriedFingerprint}', {Now}, {Now});
            INSERT INTO core_map_initial_object (project_id, map_id, initial_state_id, object_identity_id, initial_membership_payload_json, created_at_utc_ms)
            VALUES ('{projectId:D}', '{mapWithBaselineId:D}', '{initialStateId:D}', '{objectId:D}', '{membershipPayload}', {Now});
            """);

        // 3) 前滚到最新 schema（执行收敛迁移）。
        _ = new SqliteCoreDomainDatabase(_database);

        // 4) 旧存储彻底退役；revision/member/cursor 全部就位。
        Assert.True(CountRows("sqlite_master", "WHERE type = 'table' AND name = 'core_map_initial_object'") == 0);
        Assert.True(CountRows("sqlite_master",
            "WHERE type = 'table' AND name = 'core_map_initial_state_converged'") == 0);
        Assert.Equal(2, CountRows("core_map_initial_state"));
        Assert.Equal(2, CountRows("core_map_initial_state_revision"));
        Assert.Equal(2, CountRows("core_map_initial_state_cursor"));
        Assert.Equal(1, CountRows("core_map_initial_state_revision_member"));

        // 5) 有 baseline 的 Map：成员原样保留为 revision zero 成员；指纹原样携带（不是重算/占位）。
        var core = new SqliteCoreDomainDatabase(_database);
        var state = core.FindMapInitialStateRevisionState(projectId, mapWithBaselineId);
        Assert.NotNull(state);
        Assert.Equal(0, state!.Snapshot.Revision.RevisionNumber);
        Assert.Null(state.Snapshot.Revision.ParentRevisionId);
        Assert.Null(state.Snapshot.Revision.OperationKind);
        Assert.Equal(initialStateId, state.Snapshot.Revision.RevisionId);
        Assert.Equal(carriedFingerprint, state.Snapshot.Revision.ContentFingerprint);
        var member = Assert.Single(state.Snapshot.Members);
        Assert.Equal(objectId, member.ObjectIdentityId);
        Assert.Equal(membershipPayload, member.MembershipPayloadJson);
        Assert.Equal(0, state.Cursor.CursorVersion);
        Assert.Equal(state.Snapshot.Revision.RevisionId, state.Cursor.ActiveRevisionId);
        Assert.Null(state.Cursor.SelectedRedoChildRevisionId);

        // 6) 无 baseline 的 Map：获得空 revision zero（Domain 空快照指纹）+ cursor。
        var filled = core.FindMapInitialStateRevisionState(projectId, mapWithoutBaselineId);
        Assert.NotNull(filled);
        Assert.Equal(0, filled!.Snapshot.Revision.RevisionNumber);
        Assert.Empty(filled.Snapshot.Members);
        Assert.Equal(MapInitialStateService.EmptySnapshotFingerprint, filled.Snapshot.Revision.ContentFingerprint);
        Assert.Equal(0, filled.Cursor.CursorVersion);

        // 7) 收敛后的数据库可用完整 Domain 修订流（append → undo；成员 payload 更新型追加）。
        var appended = MapInitialStateService.AppendRevision(
            core, projectId, mapWithBaselineId,
            state.Cursor.ActiveRevisionId, state.Cursor.CursorVersion,
            new[] { new InitialObjectWrite { ObjectIdentityId = objectId, MembershipPayloadJson = """{"side":"south"}""" } },
            Now + 1);
        Assert.Equal(1, appended.Snapshot.Revision.RevisionNumber);
        Assert.Single(appended.Snapshot.Members);
        Assert.Equal(projectId, appended.Snapshot.Members[0].ProjectId);
        var undone = MapInitialStateService.Undo(
            core, projectId, mapWithBaselineId,
            appended.Cursor.ActiveRevisionId, appended.Cursor.CursorVersion, Now + 2);
        Assert.Equal(0, undone.Snapshot.Revision.RevisionNumber);
        Assert.Equal(appended.Cursor.ActiveRevisionId, undone.Cursor.SelectedRedoChildRevisionId);
    }

    /// <summary>
    /// 多 Map 回归（PR #54 修复）：同一次收敛 migration 处理多个无 baseline 的 Map，
    /// 每个 Map 必须获得独立唯一的 baseline identity（修复前非相关 FROM 子查询被
    /// SQLite 物化为单一随机值，第二个无 baseline Map 即触发主键冲突导致 migration 失败）。
    /// 真实执行 SQLite migration，不做 SQL 字符串断言。
    /// </summary>
    [Fact]
    public void Convergence_MultipleBaselinelessMaps_GetDistinctBaselineIdentities()
    {
        // 1) 停留在收敛前的迁移序列（旧形态 schema）。
        Session().Execute(db => db.Database.Migrate("20261128014741_CoreMapInitialStateRevisions"));

        var projectId = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_project (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{projectId:D}', 'Multi Map Legacy', 1, {Now}, {Now});
            """);

        // 2) 25 个无 baseline 的 Map（远超任务要求的 3 个；若随机表达式被物化为单值，
        //    第二个 Map 即主键冲突，migration 必败）。其中一个 Map 预置可注册对象，
        //    供迁移后走正式 append 流程。
        const int mapCount = 25;
        var mapIds = Enumerable.Range(0, mapCount).Select(_ => Guid.NewGuid()).ToArray();
        var typeId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var sb = new System.Text.StringBuilder();
        foreach (var mapId in mapIds)
        {
            sb.Append($"""
                INSERT INTO core_map (map_id, project_id, map_space_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
                VALUES ('{mapId:D}', '{projectId:D}', '{Guid.NewGuid():D}', 'Legacy {mapId:D}', 'active', {Now}, {Now});
                """);
        }

        sb.Append($"""
            INSERT INTO core_object_type (object_type_id, project_id, type_key, display_name, allocatable, allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{typeId:D}', '{projectId:D}', 'river', 'River', 1, 'fmt', 1, {Now}, {Now});
            INSERT INTO core_object (object_identity_id, project_id, object_type_id, object_type_schema_version, object_type_semantics_id, sharing_scope, owner_map_id, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{objectId:D}', '{projectId:D}', '{typeId:D}', 2, 'world-view.object-type.river.v1', 'map_local', '{mapIds[0]:D}', 'active', {Now}, {Now});
            INSERT INTO core_map_object_ref (map_object_ref_id, project_id, map_id, object_identity_id, created_at_utc_ms)
            VALUES ('{registrationId:D}', '{projectId:D}', '{mapIds[0]:D}', '{objectId:D}', {Now});
            """);
        ExecuteRaw(sb.ToString());

        // 3) 执行完整收敛 migration（真实 SQLite migration，含修复后的身份生成路径）。
        _ = new SqliteCoreDomainDatabase(_database);

        // 4) migration 成功完成：三个存储全部按 Map 数量就位。
        Assert.Equal(mapCount, CountRows("core_map_initial_state"));
        Assert.Equal(mapCount, CountRows("core_map_initial_state_revision"));
        Assert.Equal(mapCount, CountRows("core_map_initial_state_cursor"));

        // 5) 每个 Map 恰一条 baseline identity；全部非空且两两不同。
        var core = new SqliteCoreDomainDatabase(_database);
        var baselineIds = new List<Guid>();
        foreach (var mapId in mapIds)
        {
            var baseline = core.FindMapInitialStateBaseline(projectId, mapId);
            Assert.NotNull(baseline);
            Assert.NotEqual(Guid.Empty, baseline!.InitialStateId);
            baselineIds.Add(baseline.InitialStateId);
        }
        Assert.Equal(mapCount, baselineIds.Distinct().Count());

        // 6) 每个 Map 恰一条 revision zero（parent 空、ordinal 0、无 operation metadata、
        //    快照为空），且 cursor 正确指向自己的 revision zero（version 0、无 redo 选择）。
        foreach (var mapId in mapIds)
        {
            var state = core.FindMapInitialStateRevisionState(projectId, mapId);
            Assert.NotNull(state);
            Assert.Equal(0, state!.Snapshot.Revision.RevisionNumber);
            Assert.Null(state.Snapshot.Revision.ParentRevisionId);
            Assert.Null(state.Snapshot.Revision.OperationKind);
            Assert.Null(state.Snapshot.Revision.OperationPayloadJson);
            Assert.Empty(state.Snapshot.Members);
            Assert.Equal(0, state.Cursor.CursorVersion);
            Assert.Null(state.Cursor.SelectedRedoChildRevisionId);
            Assert.Equal(state.Snapshot.Revision.RevisionId, state.Cursor.ActiveRevisionId);
            Assert.Equal(state.Cursor.InitialStateId, state.Snapshot.Revision.InitialStateId);
            Assert.Equal(1, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapId:D}' AND revision_number = 0"));
            Assert.Equal(0, CountRows("core_map_initial_state_revision_member", $"WHERE map_id = '{mapId:D}'"));
        }

        // 7) 跨 Map 隔离：JOIN 仅按所属 (project_id, map_id) 配对，WHERE 检查
        // cursor/revision 的 initial_state_id 是否与其所属 Map 的 baseline identity
        // 不一致——不一致行数必须为 0。
        Assert.Equal(0, CountRows(
            "core_map_initial_state_cursor c JOIN core_map_initial_state b",
            "ON c.project_id = b.project_id AND c.map_id = b.map_id "
            + "WHERE c.initial_state_id <> b.initial_state_id"));
        Assert.Equal(0, CountRows(
            "core_map_initial_state_revision r JOIN core_map_initial_state b",
            "ON r.project_id = b.project_id AND r.map_id = b.map_id "
            + "WHERE r.initial_state_id <> b.initial_state_id"));

        // 8) 迁移结果可进入现有正式写入流程：对一个迁移出的 baseline 走完整 append。
        var zeroState = core.FindMapInitialStateRevisionState(projectId, mapIds[0])!;
        var appended = MapInitialStateService.AppendRevision(
            core, projectId, mapIds[0],
            zeroState.Cursor.ActiveRevisionId, zeroState.Cursor.CursorVersion,
            new[] { new InitialObjectWrite { ObjectIdentityId = objectId, MembershipPayloadJson = """{"placed":true}""" } },
            Now + 1);
        Assert.Equal(1, appended.Snapshot.Revision.RevisionNumber);
        Assert.Equal(zeroState.Cursor.ActiveRevisionId, appended.Snapshot.Revision.ParentRevisionId);
        Assert.Single(appended.Snapshot.Members);
        Assert.Equal(objectId, appended.Snapshot.Members[0].ObjectIdentityId);
        Assert.Equal(mapCount + 1, CountRows("core_map_initial_state_revision"));

        // 其余 Map 不受影响（仍各恰一条 revision zero、无成员）。
        Assert.Equal(0, CountRows("core_map_initial_state_revision_member", $"WHERE map_id = '{mapIds[1]:D}'"));
        Assert.Equal(1, CountRows("core_map_initial_state_revision", $"WHERE map_id = '{mapIds[1]:D}'"));
    }

    private static InitialObjectRecord MakeMemberRecord(
        Guid projectId, Guid mapId, Guid initialStateId, Guid objectId, string membershipPayload) => new()
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = initialStateId,
            RevisionId = initialStateId,
            ObjectIdentityId = objectId,
            MembershipPayloadJson = membershipPayload,
            PlacementPayloadJson = null,
            PlacementSchemaVersion = null,
            CreatedAtUtcMs = Now,
        };
}
