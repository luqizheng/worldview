using System;
using WorldView.Domain.Maps;
using WorldView.Domain.Projects;
using WorldView.Domain.Tests.TestDoubles;
using WorldView.Domain.Time;
using WorldView.Domain.Workspace;

namespace WorldView.Domain.Tests.Workspace;

public class WorkspaceContextRulesTests
{
    private static (FakeCoreDomainDatabase Db, Guid ProjectId, Guid MapId, Guid RootHistoryId, Guid B1Id, Guid B2Id) Build()
    {
        var db = new FakeCoreDomainDatabase();
        var project = new ProjectRecord
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "P",
            ProjectFormatVersion = 1,
            CreatedAtUtcMs = 1L,
            UpdatedAtUtcMs = 1L,
        };
        db.Projects.Add(project);
        var plan = MapService.CreateMap(db, project.ProjectId, "Main Map", nowUtcMs: 10L);
        var b1 = BranchService.CreateBranch(db, project.ProjectId, plan.RootHistory.HistoryId, "B1", new HistoricalTime(100, 0), nowUtcMs: 20L);
        var b2 = BranchService.CreateBranch(db, project.ProjectId, b1.OwnHistory.HistoryId, "B2", new HistoricalTime(150, 600), nowUtcMs: 30L);
        return (db, project.ProjectId, plan.Map.MapId, plan.RootHistory.HistoryId, b1.Branch.BranchId, b2.Branch.BranchId);
    }

    [Fact]
    public void Resolve_RootContext_NullBranchAndTime_IsEpoch()
    {
        var (db, projectId, mapId, rootId, _, _) = Build();
        var ok = WorkspaceContextRules.TryResolve(db, projectId, mapId, branchId: null, historicalTimeInput: null, out var context, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(context);
        Assert.Null(context.BranchId);
        Assert.Equal(rootId, context.RootHistoryId);
        Assert.Equal(0, context.Lineage.Depth);
        Assert.Equal(HistoricalTime.Epoch, context.Time);
        Assert.Equal("Main Map", context.MapName);
    }

    [Fact]
    public void Resolve_BranchContext_LineageAndTimeResolved()
    {
        var (db, projectId, mapId, _, b1Id, b2Id) = Build();
        var ok = WorkspaceContextRules.TryResolve(db, projectId, mapId, b2Id, "120:30", out var context, out var error);

        Assert.True(ok);
        Assert.NotNull(context);
        Assert.Equal(b2Id, context.BranchId);
        Assert.Equal(2, context.Lineage.Depth);
        Assert.Equal(b1Id, context.Lineage.Entries[0].BranchId);
        Assert.Equal(new HistoricalTime(120, 30), context.Time);
        Assert.Equal("120:30", context.Time.Canonical());
    }

    [Fact]
    public void Resolve_MapNotFound_IsRejected()
    {
        var (db, projectId, mapId, _, _, _) = Build();
        var ok = WorkspaceContextRules.TryResolve(db, projectId, Guid.NewGuid(), null, null, out var context, out var error);

        Assert.False(ok);
        Assert.Null(context);
        Assert.Contains("不存在", error);
    }

    [Fact]
    public void Resolve_MapOfOtherProject_IsRejected()
    {
        // 同一替身库内另一项目的 Map：按项目范围查询应查不到（隔离由查询范围保证）。
        var (db, projectId, mapId, _, _, _) = Build();
        var otherProject = new ProjectRecord
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Other",
            ProjectFormatVersion = 1,
            CreatedAtUtcMs = 1L,
            UpdatedAtUtcMs = 1L,
        };
        db.Projects.Add(otherProject);
        var otherMap = MapService.CreateMap(db, otherProject.ProjectId, "Other Map", nowUtcMs: 10L);

        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, otherMap.Map.MapId, null, null, out _, out var error));
        Assert.Contains("不属于项目", error);
    }

    [Fact]
    public void Resolve_MissingRootHistory_IsRejected()
    {
        var (db, projectId, mapId, rootId, _, _) = Build();
        db.Histories.RemoveAll(h => h.HistoryId == rootId);

        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, mapId, null, null, out _, out var error));
        Assert.Contains("根历史", error);
    }

    [Fact]
    public void Resolve_UnknownBranch_IsRejected()
    {
        var (db, projectId, mapId, _, _, _) = Build();
        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, mapId, Guid.NewGuid(), null, out _, out var error));
        Assert.Contains("分支", error);
    }

    [Fact]
    public void Resolve_InvalidTimeInput_IsRejected()
    {
        var (db, projectId, mapId, _, _, _) = Build();
        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, mapId, null, "yesterday", out _, out var error));
        Assert.Contains("历史时间输入", error);
        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, mapId, null, "5:86400", out _, out _));
        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, mapId, null, "-1:0", out _, out _));
    }

    [Fact]
    public void Resolve_BranchOfDifferentMap_IsRejected()
    {
        // 项目内第二张地图及其分支：用于目标地图的上下文必须被拒绝。
        var (db, projectId, mapId, _, _, _) = Build();
        var secondMap = MapService.CreateMap(db, projectId, "Second Map", nowUtcMs: 10L);
        var foreignBranch = BranchService.CreateBranch(
            db, projectId, secondMap.RootHistory.HistoryId, "FB", new HistoricalTime(10, 0), nowUtcMs: 20L);

        Assert.False(WorkspaceContextRules.TryResolve(db, projectId, mapId, foreignBranch.Branch.BranchId, null, out _, out var error));
        Assert.Contains("跨地图", error);
    }
}
