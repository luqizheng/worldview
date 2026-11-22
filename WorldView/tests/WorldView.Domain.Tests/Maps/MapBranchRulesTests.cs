using System;
using WorldView.Domain.Maps;
using WorldView.Domain.Projects;
using WorldView.Domain.Tests.TestDoubles;
using WorldView.Domain.Time;

namespace WorldView.Domain.Tests.Maps;

public class MapBranchRulesTests
{
    private static ProjectRecord NewProject() => new()
    {
        ProjectId = Guid.NewGuid(),
        ProjectName = "P",
        ProjectFormatVersion = 1,
        CreatedAtUtcMs = 1L,
        UpdatedAtUtcMs = 1L,
    };

    [Fact]
    public void CreateMap_PlanSatisfiesCreationInvariant()
    {
        var project = NewProject();
        var plan = MapRules.CreateMap(project, "  World  ", nowUtcMs: 42L);

        // Map 基本字段。
        Assert.Equal("World", plan.Map.MapName);
        Assert.Equal(project.ProjectId, plan.Map.ProjectId);
        Assert.Equal(MapLifecycle.ActiveStatus, plan.Map.LifecycleStatus);

        // 根 History 归属本 Map（map_root + owner 唯一）。
        Assert.Equal(HistoryKind.MapRoot, plan.RootHistory.Kind);
        Assert.Equal(plan.Map.MapId, plan.RootHistory.OwnerMapId);
        Assert.Equal(project.ProjectId, plan.RootHistory.ProjectId);

        // default Calendar（每 Map 恰一 default）。
        Assert.Equal(CalendarRole.Default, plan.DefaultCalendar.Role);
        Assert.Equal(plan.Map.MapId, plan.DefaultCalendar.MapId);
        Assert.Null(plan.DefaultCalendar.CalendarType); // 结构占位：类型词汇未冻结

        // day-zero Configuration：从纪元生效、user_created、无源 Command、占位规则内容。
        Assert.Equal(HistoricalTime.Epoch, plan.DayZeroConfiguration.EffectiveFrom);
        Assert.Null(plan.DayZeroConfiguration.EffectiveUntil);
        Assert.Equal(CalendarConfigurationOrigin.UserCreated, plan.DayZeroConfiguration.Origin);
        Assert.Null(plan.DayZeroConfiguration.SourceCommandId);
        Assert.Equal(0, plan.DayZeroConfiguration.RulesSchemaVersion);
        Assert.Equal("{}", plan.DayZeroConfiguration.RulesPayloadJson);
        Assert.Equal(plan.RootHistory.HistoryId, plan.DayZeroConfiguration.OwnerHistoryId);
        Assert.Equal(plan.DefaultCalendar.CalendarId, plan.DayZeroConfiguration.CalendarId);

        // 全部 ID 一次性预生成且互不相同。
        Assert.NotEqual(plan.Map.MapId, plan.RootHistory.HistoryId);
        Assert.NotEqual(plan.Map.MapId, plan.DefaultCalendar.CalendarId);
        Assert.NotEqual(plan.DefaultCalendar.CalendarId, plan.DayZeroConfiguration.CalendarConfigurationId);
    }

    [Fact]
    public void CreateMap_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => MapRules.CreateMap(NewProject(), "  ", nowUtcMs: 1L));
    }

    [Fact]
    public void CreateBranch_PlanLinksOwnHistoryAndParent()
    {
        var project = NewProject();
        var mapPlan = MapRules.CreateMap(project, "M", nowUtcMs: 1L);
        var parentAt = new HistoricalTime(1200, 43200);
        var plan = BranchRules.CreateBranch(project, mapPlan.RootHistory, "  Fork A  ", parentAt, nowUtcMs: 5L);

        Assert.Equal("Fork A", plan.Branch.BranchName);
        Assert.Equal(mapPlan.RootHistory.HistoryId, plan.Branch.ParentHistoryId);
        Assert.Equal(plan.OwnHistory.HistoryId, plan.Branch.OwnHistoryId);
        Assert.NotEqual(plan.Branch.ParentHistoryId, plan.Branch.OwnHistoryId);
        Assert.Equal(parentAt, plan.Branch.ParentAt);
        Assert.Equal(HistoryKind.BranchOwned, plan.OwnHistory.Kind);
        Assert.Null(plan.OwnHistory.OwnerMapId);
        Assert.Equal(MapLifecycle.ActiveStatus, plan.Branch.LifecycleStatus);
    }

    [Fact]
    public void CreateBranch_RejectsForeignProjectParent()
    {
        var project = NewProject();
        var foreign = NewProject();
        var mapPlan = MapRules.CreateMap(foreign, "M", nowUtcMs: 1L);

        Assert.Throws<ArgumentException>(() =>
            BranchRules.CreateBranch(project, mapPlan.RootHistory, "Fork", HistoricalTime.Epoch, nowUtcMs: 2L));
    }

    [Fact]
    public void MapService_UnknownProject_Throws()
    {
        var db = new FakeCoreDomainDatabase();
        Assert.Throws<InvalidOperationException>(() =>
            MapService.CreateMap(db, Guid.NewGuid(), "M", nowUtcMs: 1L));
    }

    [Fact]
    public void BranchService_UnknownParentHistory_Throws()
    {
        var db = new FakeCoreDomainDatabase();
        var project = NewProject();
        db.Projects.Add(project);
        MapService.CreateMap(db, project.ProjectId, "M", nowUtcMs: 1L);

        Assert.Throws<InvalidOperationException>(() =>
            BranchService.CreateBranch(db, project.ProjectId, Guid.NewGuid(), "B", HistoricalTime.Epoch, nowUtcMs: 2L));
    }

    [Fact]
    public void BranchService_DanglingAncestor_Throws()
    {
        var db = new FakeCoreDomainDatabase();
        var project = NewProject();
        db.Projects.Add(project);
        var mapPlan = MapService.CreateMap(db, project.ProjectId, "M", nowUtcMs: 1L);
        var b1 = BranchService.CreateBranch(db, project.ProjectId, mapPlan.RootHistory.HistoryId, "B1", new HistoricalTime(10, 0), nowUtcMs: 2L);

        // 抽走 B1 的父（根）历史 → 祖先链悬空。
        db.Histories.RemoveAll(h => h.HistoryId == mapPlan.RootHistory.HistoryId);
        Assert.Throws<InvalidOperationException>(() =>
            BranchService.CreateBranch(db, project.ProjectId, b1.OwnHistory.HistoryId, "B2", new HistoricalTime(20, 0), nowUtcMs: 3L));
    }

    [Fact]
    public void BranchService_HappyPath_PersistsStructure()
    {
        var db = new FakeCoreDomainDatabase();
        var project = NewProject();
        db.Projects.Add(project);
        var mapPlan = MapService.CreateMap(db, project.ProjectId, "M", nowUtcMs: 1L);

        var plan = BranchService.CreateBranch(db, project.ProjectId, mapPlan.RootHistory.HistoryId, "B1", new HistoricalTime(10, 0), nowUtcMs: 2L);

        Assert.Contains(plan.OwnHistory, db.Histories);
        Assert.Contains(plan.Branch, db.Branches);
    }
}
