using System;
using System.Collections.Generic;
using System.Linq;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;

namespace WorldView.Domain.Tests.TestDoubles;

/// <summary>
/// ICoreDomainDatabase 的内存测试替身（仅 Domain 单元测试使用；真实持久化证据
/// 由 Infrastructure.Tests 的真实 SQLite 测试提供，不以本类冒充集成）。
/// </summary>
public sealed class FakeCoreDomainDatabase : ICoreDomainDatabase
{
    public List<ProjectRecord> Projects { get; } = new();

    public List<MapRecord> Maps { get; } = new();

    public List<HistoryRecord> Histories { get; } = new();

    public List<BranchRecord> Branches { get; } = new();

    public List<CalendarRecord> Calendars { get; } = new();

    public List<CalendarConfigurationRecord> CalendarConfigurations { get; } = new();

    public ProjectRecord? FindProject(Guid projectId) => Projects.FirstOrDefault(p => p.ProjectId == projectId);

    public IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows() => Projects
        .Select(p => new RawProjectIdentityRow
        {
            ProjectIdText = p.ProjectId.ToString("D"),
            ProjectName = p.ProjectName,
            ProjectFormatVersion = p.ProjectFormatVersion,
            CreatedAtUtcMs = p.CreatedAtUtcMs,
            UpdatedAtUtcMs = p.UpdatedAtUtcMs,
        })
        .ToArray();

    public void InsertProject(ProjectRecord project)
    {
        if (Projects.Count > 0)
        {
            throw new CoreDomainDataException("已存在项目身份（一库一项目）。");
        }

        Projects.Add(project);
    }

    public MapRecord? FindMap(Guid projectId, Guid mapId) =>
        Maps.FirstOrDefault(m => m.ProjectId == projectId && m.MapId == mapId);

    public IReadOnlyList<MapRecord> ListMaps(Guid projectId) =>
        Maps.Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.CreatedAtUtcMs)
            .ThenBy(m => m.MapId)
            .ToArray();

    public void CreateMapStructure(MapCreationPlan plan)
    {
        Maps.Add(plan.Map);
        Histories.Add(plan.RootHistory);
        Calendars.Add(plan.DefaultCalendar);
        CalendarConfigurations.Add(plan.DayZeroConfiguration);
    }

    public HistoryRecord? FindHistory(Guid projectId, Guid historyId) =>
        Histories.FirstOrDefault(h => h.ProjectId == projectId && h.HistoryId == historyId);

    public HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId) =>
        Histories.FirstOrDefault(h =>
            h.ProjectId == projectId && h.Kind == HistoryKind.MapRoot && h.OwnerMapId == mapId);

    public BranchRecord? FindBranch(Guid projectId, Guid branchId) =>
        Branches.FirstOrDefault(b => b.ProjectId == projectId && b.BranchId == branchId);

    public BranchRecord? FindBranchByOwnHistory(Guid projectId, Guid ownHistoryId) =>
        Branches.FirstOrDefault(b => b.ProjectId == projectId && b.OwnHistoryId == ownHistoryId);

    public void CreateBranchStructure(BranchCreationPlan plan)
    {
        Histories.Add(plan.OwnHistory);
        Branches.Add(plan.Branch);
    }
}
