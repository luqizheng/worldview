using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Tests.Testing;
using WorldView.Desktop.ViewModels;
using Xunit;

namespace WorldView.Desktop.Tests.ViewModels;

/// <summary>
/// WP-UI-02：项目关闭成功判定。ViewModel 只在会话真正 Closed 时触发一次 ProjectClosed，
/// 失败时保留窗口可用状态并给出明确诊断，重试成功后才关闭。
/// </summary>
public class ProjectWorkspaceViewModelCloseTests
{
    private static (ProjectWorkspaceViewModel Vm, ProjectSession Session, StubAssetStore Assets, RecordingLog Log) Build()
    {
        var assets = new StubAssetStore();
        var session = TestSessions.CreateReadySession(assetStore: assets);
        var log = new RecordingLog();
        var vm = new ProjectWorkspaceViewModel(
            session,
            mapQuery: null,
            contextResolver: null,
            createMap: new CreateMapUseCase(session, new FakeMapCreationService()),
            log: log);
        return (vm, session, assets, log);
    }

    [Fact]
    public async Task CloseProject_Success_RaisesProjectClosedOnce()
    {
        var (vm, session, _, _) = Build();
        var raised = 0;
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, raised);
        Assert.Contains("已关闭", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseProject_SessionFailure_DoesNotRaiseProjectClosed_AndShowsDiagnostic()
    {
        var (vm, session, assets, log) = Build();
        assets.FailureHook = op => op == "AssetStore:Dispose"
            ? new IOException("注入：Asset Store 释放失败。")
            : null;
        var raised = 0;
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Equal(0, raised);
        // WP-30B 3A 统一编排（ADR-10 §7.2）：命令文案统一为"项目关闭失败"。
        Assert.Contains("项目关闭失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Contains("Asset Store 释放失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Single(log.Errors);
    }

    [Fact]
    public async Task CloseProject_FailureThenRetry_RaisesOnceOnlyAfterSuccess()
    {
        var (vm, session, assets, log) = Build();
        assets.FailureHook = op => op == "AssetStore:Dispose"
            ? new IOException("注入：Asset Store 释放失败。")
            : null;
        var raised = 0;
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(0, raised);

        assets.FailureHook = null;
        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, raised);
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Contains("已关闭", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Single(log.Errors); // 失败如实记录，重试成功不掩盖历史失败
    }

    [Fact]
    public async Task CloseProject_CommandInvokedTwiceAfterSuccess_RaisesOnlyOnce()
    {
        var (vm, session, assets, _) = Build();
        var raised = 0;
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);
        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, assets.DisposeAttempts);
        Assert.Equal(1, raised);
    }

    private sealed class RecordingLog : IAppLog
    {
        public List<string> Errors { get; } = new();

        public void Info(string category, string message) { }

        public void Warn(string category, string message, Exception? exception = null) { }

        public void Error(string category, string message, Exception? exception = null)
            => Errors.Add($"{category}: {message}: {exception?.Message}");
    }
}
