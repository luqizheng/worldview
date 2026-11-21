using System;
using System.Collections.Generic;
using WorldView.Domain.Maps;
using WorldView.Domain.Projects;
using WorldView.Domain.Tests.TestDoubles;
using WorldView.Domain.Time;

namespace WorldView.Domain.Tests.Maps;

public class BranchLineageTests
{
    /// <summary>构建一棵标准结构：Map → B1（根历史 fork）→ B2（B1 fork），并放进替身库。</summary>
    private static (FakeCoreDomainDatabase Db, ProjectRecord Project, MapRecord Map, HistoryRecord Root, BranchRecord B1, BranchRecord B2) BuildTree()
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

        var plan = MapRules.CreateMap(project, "M", nowUtcMs: 10L);
        db.CreateMapStructure(plan);

        var b1Plan = BranchService.CreateBranch(db, project.ProjectId, plan.RootHistory.HistoryId, "B1", new HistoricalTime(100, 0), nowUtcMs: 20L);
        var b2Plan = BranchService.CreateBranch(db, project.ProjectId, b1Plan.OwnHistory.HistoryId, "B2", new HistoricalTime(200, 3600), nowUtcMs: 30L);
        return (db, project, plan.Map, plan.RootHistory, b1Plan.Branch, b2Plan.Branch);
    }

    [Fact]
    public void Resolve_SingleBranch_DepthOne()
    {
        var (db, project, map, root, b1, _) = BuildTree();
        var lineage = BranchLineageResolver.TryResolve(
            b1,
            expectedOwnerMapId: map.MapId,
            findHistory: id => db.FindHistory(project.ProjectId, id),
            findBranchByOwnHistory: id => db.FindBranchByOwnHistory(project.ProjectId, id),
            out var error);

        Assert.NotNull(lineage);
        Assert.Null(error);
        Assert.Equal(root.HistoryId, lineage.RootHistoryId);
        Assert.Equal(1, lineage.Depth);
        Assert.Equal(b1.BranchId, lineage.SelectedBranchId);
        var entry = Assert.Single(lineage.Entries);
        Assert.Equal(b1.BranchId, entry.BranchId);
        Assert.Equal(root.HistoryId, entry.ParentHistoryId);
        Assert.Equal(new HistoricalTime(100, 0), entry.ParentAt);
    }

    [Fact]
    public void Resolve_BranchOfBranch_RootToLeafOrder()
    {
        var (db, project, map, root, b1, b2) = BuildTree();
        var lineage = BranchLineageResolver.TryResolve(
            b2,
            expectedOwnerMapId: map.MapId,
            findHistory: id => db.FindHistory(project.ProjectId, id),
            findBranchByOwnHistory: id => db.FindBranchByOwnHistory(project.ProjectId, id),
            out var error);

        Assert.NotNull(lineage);
        Assert.Equal(2, lineage.Depth);
        Assert.Equal(b2.BranchId, lineage.SelectedBranchId);
        Assert.Equal(root.HistoryId, lineage.RootHistoryId);
        // 根→叶排序：B1 在前，B2 在后。
        Assert.Equal(b1.BranchId, lineage.Entries[0].BranchId);
        Assert.Equal(b2.BranchId, lineage.Entries[1].BranchId);
        Assert.Equal(b1.OwnHistoryId, lineage.Entries[1].ParentHistoryId);
    }

    [Fact]
    public void Resolve_Cycle_IsRejected()
    {
        // 手工构造环：H1(branch,owned by B)、H2(branch, owned by B) 互为 parent。
        var projectId = Guid.NewGuid();
        var b1 = new BranchRecord
        {
            BranchId = Guid.NewGuid(),
            ProjectId = projectId,
            ParentHistoryId = Guid.NewGuid(), // hB
            OwnHistoryId = Guid.NewGuid(),    // hA
            ParentAt = HistoricalTime.Epoch,
            BranchName = "B1",
            LifecycleStatus = MapLifecycle.ActiveStatus,
            CreatedAtUtcMs = 1L,
            UpdatedAtUtcMs = 1L,
        };
        var b2 = new BranchRecord
        {
            BranchId = Guid.NewGuid(),
            ProjectId = projectId,
            ParentHistoryId = b1.OwnHistoryId, // hA —— 与 hB 的 owner 一起构成环
            OwnHistoryId = b1.ParentHistoryId, // hB
            ParentAt = HistoricalTime.Epoch,
            BranchName = "B2",
            LifecycleStatus = MapLifecycle.ActiveStatus,
            CreatedAtUtcMs = 1L,
            UpdatedAtUtcMs = 1L,
        };
        var histories = new Dictionary<Guid, HistoryRecord>
        {
            [b1.ParentHistoryId] = new HistoryRecord { HistoryId = b1.ParentHistoryId, ProjectId = projectId, Kind = HistoryKind.BranchOwned, OwnerMapId = null, CreatedAtUtcMs = 1L },
            [b1.OwnHistoryId] = new HistoryRecord { HistoryId = b1.OwnHistoryId, ProjectId = projectId, Kind = HistoryKind.BranchOwned, OwnerMapId = null, CreatedAtUtcMs = 1L },
        };

        var lineage = BranchLineageResolver.TryResolve(
            b1,
            expectedOwnerMapId: Guid.NewGuid(),
            findHistory: id => histories.GetValueOrDefault(id),
            findBranchByOwnHistory: id => id == b1.ParentHistoryId ? b2 : (id == b2.ParentHistoryId ? b1 : null),
            out var error);

        Assert.Null(lineage);
        Assert.NotNull(error);
        Assert.Contains("环", error);
    }

    [Fact]
    public void Resolve_DanglingParentHistory_IsRejected()
    {
        var (db, project, map, _, b1, _) = BuildTree();
        db.Histories.RemoveAll(h => h.HistoryId == b1.ParentHistoryId);

        var lineage = BranchLineageResolver.TryResolve(
            b1,
            expectedOwnerMapId: map.MapId,
            findHistory: id => db.FindHistory(project.ProjectId, id),
            findBranchByOwnHistory: id => db.FindBranchByOwnHistory(project.ProjectId, id),
            out var error);

        Assert.Null(lineage);
        Assert.Contains("不存在", error);
    }

    [Fact]
    public void Resolve_BranchOwnedHistoryWithoutOwner_IsRejected()
    {
        var (db, project, map, _, b1, _) = BuildTree();
        // 伪造父历史为 branch_owned 但没有 owner branch。
        var orphan = new HistoryRecord
        {
            HistoryId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            Kind = HistoryKind.BranchOwned,
            OwnerMapId = null,
            CreatedAtUtcMs = 1L,
        };
        db.Histories.Add(orphan);
        db.Histories.RemoveAll(h => h.HistoryId == b1.ParentHistoryId);
        var orphanBranch = b1 with { ParentHistoryId = orphan.HistoryId };

        var lineage = BranchLineageResolver.TryResolve(
            orphanBranch,
            expectedOwnerMapId: map.MapId,
            findHistory: id => db.FindHistory(project.ProjectId, id),
            findBranchByOwnHistory: id => db.FindBranchByOwnHistory(project.ProjectId, id),
            out var error);

        Assert.Null(lineage);
        Assert.Contains("数据损坏", error);
    }

    [Fact]
    public void Resolve_RootOfDifferentMap_IsRejected()
    {
        var (db, project, _, _, b1, _) = BuildTree();
        // b1 的父历史是根历史且 owner 是本 Map——先移走根历史，再造一个属于其他 Map 的根历史。
        var otherMapRoot = new HistoryRecord
        {
            HistoryId = b1.ParentHistoryId,
            ProjectId = project.ProjectId,
            Kind = HistoryKind.MapRoot,
            OwnerMapId = Guid.NewGuid(), // 其他地图
            CreatedAtUtcMs = 1L,
        };
        db.Histories.RemoveAll(h => h.HistoryId == b1.ParentHistoryId);
        db.Histories.Add(otherMapRoot);

        var lineage = BranchLineageResolver.TryResolve(
            b1,
            expectedOwnerMapId: Guid.NewGuid(),
            findHistory: id => db.FindHistory(project.ProjectId, id),
            findBranchByOwnHistory: id => db.FindBranchByOwnHistory(project.ProjectId, id),
            out var error);

        Assert.Null(lineage);
        Assert.Contains("跨地图", error);
    }
}
