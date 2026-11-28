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
