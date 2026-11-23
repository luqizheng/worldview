using System;
using System.Linq;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Tests.Testing;
using WorldView.Desktop.ViewModels;
using Xunit;

namespace WorldView.Desktop.Tests.ViewModels;

/// <summary>
/// WP-20a 项目工作区 ViewModel 测试（创建路径）：创建后经 LoadMapsAsync 刷新列表、
/// 失败后会话仍可用、创建用例为会话绑定实例。
/// </summary>
public class ProjectWorkspaceViewModelTests
{
    private static (ProjectWorkspaceViewModel Vm, FakeMapCreationService Creation, FakeMapQueryService Query, ProjectSession Session) Build()
    {
        var query = new FakeMapQueryService();
        var creation = new FakeMapCreationService(query);
        var session = TestSessions.CreateReadySession();
        var useCase = new CreateMapUseCase(session, creation);
        var vm = new ProjectWorkspaceViewModel(session, query, contextResolver: null, createMap: useCase, log: null);
        return (vm, creation, query, session);
    }

    [Fact]
    public void CreateMap_ExposesSessionBoundUseCaseInstance()
    {
        var (vm, _, _, session) = Build();
        Assert.Same(session, vm.CreateMap.Session);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task LoadMapsAsync_PopulatesListFromCoreDomainProjection()
    {
        var (vm, creation, query, _) = Build();
        query.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        query.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "南方边境" });

        await vm.LoadMapsAsync();

        Assert.Equal(2, vm.Maps.Count);
        Assert.Contains(vm.Maps, m => m.DisplayName == "主地图");
        _ = creation; // 查询路径不触发创建
    }

    [Fact]
    public async Task CreateThenLoadMaps_NewMapAppearsInList()
    {
        var (vm, creation, _, session) = Build();

        var result = await vm.CreateMap.ExecuteAsync("新地图");
        Assert.True(result.Success, result.Diagnostic);
        Assert.False(result.Success && result.Map is null); // 成功必带摘要

        // 窗口层在创建成功后调用 LoadMapsAsync（ProjectWindow 代码约定）。
        await vm.LoadMapsAsync();

        Assert.Single(vm.Maps);
        Assert.Equal("新地图", vm.Maps.Single().DisplayName);
        Assert.True(session.IsReady); // 创建失败/成功都不影响会话
        _ = creation;
    }

    [Fact]
    public async Task CreateFailure_SessionRemainsReady_AndRetryWorks()
    {
        var (vm, creation, query, session) = Build();
        query.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "已有地图" });
        creation.NextResult = MapCreationResult.Failed(MapCreationFailureKind.CreationFailed, "持久化失败。");

        var result = await vm.CreateMap.ExecuteAsync("失败地图");
        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.CreationFailed, result.FailureKind);

        // 会话仍然可用：列表加载正常、恢复成功路径后可再次创建。
        await vm.LoadMapsAsync();
        Assert.Single(vm.Maps);
        Assert.True(session.IsReady);

        creation.NextResult = null;
        var retry = await vm.CreateMap.ExecuteAsync("重试地图");
        Assert.True(retry.Success, retry.Diagnostic);
        await vm.LoadMapsAsync();
        Assert.Equal(2, vm.Maps.Count);
    }
}
