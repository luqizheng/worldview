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

    // ---------------------------------------------------------------------
    // P2-02：ProjectClosed 逐订阅者异常隔离（通知是完成信号，不是关闭阶段；
    // 订阅者异常不得阻断其余订阅者、不得把已完成的关闭判定为失败、
    // 重试不得造成第二次通知；日志自身失败也不得影响关闭收尾）。
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CloseProject_FirstSubscriberThrows_SubsequentSubscriberStillNotified()
    {
        var (vm, session, _, log) = Build();
        var raised = 0;
        vm.ProjectClosed += () => throw new InvalidOperationException("注入：第一个订阅者通知失败。");
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, raised);
        Assert.True(vm.IsProjectCloseLifetimeFinished);
    }

    [Fact]
    public async Task CloseProject_MixedSubscribers_PartThrowOthersStillExecute()
    {
        var (vm, session, _, _) = Build();
        var raisedA = 0;
        var raisedB = 0;
        vm.ProjectClosed += () => raisedA++;
        vm.ProjectClosed += () => throw new InvalidOperationException("注入：订阅者 A 通知失败。");
        vm.ProjectClosed += () => raisedB++;
        vm.ProjectClosed += () => throw new InvalidOperationException("注入：订阅者 B 通知失败。");

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, raisedA);
        Assert.Equal(1, raisedB);
    }

    [Fact]
    public async Task CloseProject_SubscriberThrows_C2StillSucceedsAndReportsClosed()
    {
        var (vm, session, _, log) = Build();
        vm.ProjectClosed += () => throw new InvalidOperationException("注入：订阅者通知失败。");

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        // 通知异常不外泄：C2 成功完成，命令路径不进入失败分支（无 Error 日志、
        // 状态文案为成功而非"项目关闭失败"、失败重试入口未开启）。
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.True(vm.IsProjectCloseLifetimeFinished);
        Assert.Contains("已关闭", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("项目关闭失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task CloseProject_RetryAfterThrowingSubscriber_DoesNotNotifyTwice()
    {
        var (vm, session, _, _) = Build();
        var raised = 0;
        vm.ProjectClosed += () => throw new InvalidOperationException("注入：订阅者通知失败。");
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);
        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        // 订阅者异常不把 C2 判定为失败 → 无重试机会 → 第二次命令只是等待
        // 同一终态任务，通知严格一次。
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, raised);

        // 成功为终态：再次编排入口直接返回已成功完成的同一任务（无新一轮编排）。
        var terminal = vm.CloseProjectLifetimeAsync();
        Assert.True(terminal.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task CloseProject_LoggingFailureDuringNotification_C2StillCompletes()
    {
        var assets = new StubAssetStore();
        var session = TestSessions.CreateReadySession(assetStore: assets);
        var log = new ThrowingWarnLog();
        var vm = new ProjectWorkspaceViewModel(
            session,
            mapQuery: null,
            contextResolver: null,
            createMap: new CreateMapUseCase(session, new FakeMapCreationService()),
            log: log);
        var raised = 0;
        vm.ProjectClosed += () => throw new InvalidOperationException("注入：订阅者通知失败。");
        vm.ProjectClosed += () => raised++;

        await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);

        // 订阅者异常触发 WarnBestEffort，日志实现自身再抛异常——两级失败都不
        // 能影响通知循环与 C2 收尾。
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, raised);
        Assert.True(vm.IsProjectCloseLifetimeFinished);
        Assert.Contains("已关闭", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Equal(1, log.WarnAttempts);
    }

    private sealed class RecordingLog : IAppLog
    {
        public List<string> Errors { get; } = new();

        public void Info(string category, string message) { }

        public void Warn(string category, string message, Exception? exception = null) { }

        public void Error(string category, string message, Exception? exception = null)
            => Errors.Add($"{category}: {message}: {exception?.Message}");
    }

    /// <summary>Warn 自身抛异常的日志实现（P2-02：验证 best-effort 日志纪律）。</summary>
    private sealed class ThrowingWarnLog : IAppLog
    {
        public int WarnAttempts { get; private set; }

        public void Info(string category, string message) { }

        public void Warn(string category, string message, Exception? exception = null)
        {
            WarnAttempts++;
            throw new InvalidOperationException("注入：日志实现自身故障。");
        }

        public void Error(string category, string message, Exception? exception = null) { }
    }
}
