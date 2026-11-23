using System;
using System.Linq;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Tests.Testing;
using WorldView.Desktop.ViewModels;
using Xunit;

namespace WorldView.Desktop.Tests.ViewModels;

/// <summary>
/// WP-20a 创建地图对话框 ViewModel 测试：成功关闭、失败留框显示错误、
/// 取消无持久化变化、提交期间防重复提交。
/// </summary>
public class CreateMapDialogViewModelTests
{
    private static CreateMapUseCase BuildUseCase(FakeMapCreationService service)
        => new(TestSessions.CreateReadySession(), service);

    [Fact]
    public async Task Create_Success_RaisesRequestCloseTrue_NoError()
    {
        var service = new FakeMapCreationService();
        var vm = new CreateMapDialogViewModel(BuildUseCase(service))
        {
            MapName = "主地图",
        };
        bool? closedWith = null;
        vm.RequestClose += created => closedWith = created;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.True(closedWith == true);
        Assert.Null(vm.ErrorText);
        Assert.Equal("主地图", Assert.Single(service.Requests).MapName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_BlankName_ShowsError_NoClose_NoServiceCall(string mapName)
    {
        var service = new FakeMapCreationService();
        var vm = new CreateMapDialogViewModel(BuildUseCase(service)) { MapName = mapName };
        var closeCount = 0;
        vm.RequestClose += _ => closeCount++;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal(0, closeCount);
        Assert.NotNull(vm.ErrorText);
        Assert.Contains("空白", vm.ErrorText, StringComparison.Ordinal);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Create_DuplicateName_ShowsUnderstandableError_NoClose()
    {
        var service = new FakeMapCreationService
        {
            NextResult = MapCreationResult.Failed(
                MapCreationFailureKind.DuplicateName,
                "同一项目内已存在同名地图「主地图」（名称唯一，不区分大小写）。"),
        };
        var vm = new CreateMapDialogViewModel(BuildUseCase(service)) { MapName = "主地图" };
        var closeCount = 0;
        vm.RequestClose += _ => closeCount++;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal(0, closeCount);
        Assert.NotNull(vm.ErrorText);
        Assert.Contains("同名", vm.ErrorText, StringComparison.Ordinal);
        _ = Assert.Single(service.Requests);
    }

    [Fact]
    public async Task Cancel_RaisesRequestCloseFalse_NeverCallsService()
    {
        var service = new FakeMapCreationService();
        var vm = new CreateMapDialogViewModel(BuildUseCase(service)) { MapName = "主地图" };
        bool? closedWith = null;
        vm.RequestClose += created => closedWith = created;

        vm.CancelCommand.Execute(null);

        Assert.True(closedWith == false);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Create_WhileInFlight_SubmitGateClosedUntilCompletion()
    {
        // 防重复提交机制：提交期间 IsSubmitting=true → CanExecute=false（UI 按钮被禁用，
        // 重复点击无法触发命令）→ 完成后恢复。注：由 CancellationToken 方法生成的
        // AsyncRelayCommand 在程序化重复调用时是"取消前一执行并替换"语义（toolkit 行为），
        // 真实 UI 中按钮禁用使二次触发不可达；本测试只走单次提交并验证门的全过程。
        var service = new FakeMapCreationService { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = new CreateMapDialogViewModel(BuildUseCase(service)) { MapName = "主地图" };
        bool? closedWith = null;
        vm.RequestClose += created => closedWith = created;

        var cmd = (CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.CreateCommand;
        cmd.ExecuteAsync(null);
        Assert.Equal(1, service.Requests.Count);
        Assert.True(vm.IsSubmitting);
        Assert.False(vm.CreateCommand.CanExecute(null));

        service.Gate!.TrySetResult();
        await cmd.ExecutionTask!;

        Assert.True(closedWith == true);
        Assert.Single(service.Requests); // 只发生一次提交
        Assert.False(vm.IsSubmitting);
        Assert.True(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Create_Failure_KeepsDialogUsableForRetry()
    {
        var service = new FakeMapCreationService
        {
            NextResult = MapCreationResult.Failed(MapCreationFailureKind.CreationFailed, "持久化失败。"),
        };
        var vm = new CreateMapDialogViewModel(BuildUseCase(service)) { MapName = "主地图" };

        await vm.CreateCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorText);
        Assert.False(vm.IsSubmitting);

        // 失败后可重试：注入成功结果再次提交。
        service.NextResult = MapCreationResult.Ok(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorText);
        Assert.Equal(2, service.Requests.Count);
        Assert.False(vm.IsSubmitting);
    }
}
