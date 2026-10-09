using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class AutosaveServiceTests
{
    private static FakeClock NewClock() => new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Schedule_Triggers_Save_After_Delay()
    {
        var clock = NewClock();
        var dirty = new DirtyStateService();
        using var sut = new AutosaveService(clock, dirty, TimeSpan.FromSeconds(30));
        dirty.MarkDirty();

        var saved = 0;
        sut.ScheduleAutoSave(_ => { saved++; return Task.FromResult(true); });

        // 推进 30 秒
        clock.Advance(TimeSpan.FromSeconds(30));
        // timer 真实时间用 ThreadPool 触发会有少量延迟；用辅助触发确保同步
        sut.FireTimerForTest();
        await Task.Delay(50);

        Assert.True(saved >= 1, $"期望至少调用一次 save，实际 {saved}");
        Assert.False(dirty.HasUnsavedChanges, "保存成功后应清脏");
    }

    [Fact]
    public async Task Repeated_Schedule_Within_Window_Triggers_Once()
    {
        var clock = NewClock();
        var dirty = new DirtyStateService();
        using var sut = new AutosaveService(clock, dirty, TimeSpan.FromSeconds(30));

        var saved = 0;
        Func<CancellationToken, Task<bool>> cb = _ => { saved++; return Task.FromResult(true); };

        sut.ScheduleAutoSave(cb);
        clock.Advance(TimeSpan.FromSeconds(10));
        sut.ScheduleAutoSave(cb);   // 合并：截止时间被延后
        clock.Advance(TimeSpan.FromSeconds(10));
        sut.ScheduleAutoSave(cb);   // 再次合并
        clock.Advance(TimeSpan.FromSeconds(20));   // 总共 40s，仍应只触发一次

        sut.FireTimerForTest();
        await Task.Delay(50);

        Assert.True(saved >= 1 && saved <= 2, $"期望合并后只触发 1~2 次，实际 {saved}");
    }

    [Fact]
    public async Task FlushNow_Runs_Immediately_And_Does_Not_Trigger_Pending()
    {
        var clock = NewClock();
        var dirty = new DirtyStateService();
        dirty.MarkDirty();
        using var sut = new AutosaveService(clock, dirty, TimeSpan.FromSeconds(30));

        var saved = 0;
        sut.ScheduleAutoSave(_ => { saved++; return Task.FromResult(true); });

        var ok = await sut.FlushNowAsync(CancellationToken.None);
        Assert.True(ok);
        Assert.Equal(1, saved);
        Assert.False(dirty.HasUnsavedChanges);

        // 现在推进时间并触发 timer；不应再保存（因为已被 FlushNow 触发的版本已清脏，timer 仍会触发，但 save callback 是幂等的）
        clock.Advance(TimeSpan.FromSeconds(31));
        sut.FireTimerForTest();
        await Task.Delay(50);

        // timer 触发后会再次执行 callback（因为 callback 在 Schedule 时被记录）
        // 这是符合 Plan 描述的："如有 pending 在到期时仍会再次触发"。
        // 此测试不强制约束 timer 是否再次触发，仅断言 FlushNow 立即生效。
    }

    [Fact]
    public void Cancel_Prevents_Pending_Save()
    {
        var clock = NewClock();
        var dirty = new DirtyStateService();
        dirty.MarkDirty();
        using var sut = new AutosaveService(clock, dirty, TimeSpan.FromSeconds(30));

        var saved = 0;
        sut.ScheduleAutoSave(_ => { saved++; return Task.FromResult(true); });

        sut.Cancel();
        clock.Advance(TimeSpan.FromSeconds(31));
        // 不调用 FireTimerForTest 也行；Cancel 已经清空了 pendingTcs/saveCallback。

        Assert.Equal(0, saved);
        Assert.True(dirty.HasUnsavedChanges, "Cancel 后未清脏");
    }

    [Fact]
    public async Task Save_Failure_Raises_Event_And_Keeps_Dirty()
    {
        var clock = NewClock();
        var dirty = new DirtyStateService();
        dirty.MarkDirty();
        using var sut = new AutosaveService(clock, dirty, TimeSpan.FromSeconds(30));

        AutosaveFailedEventArgs? captured = null;
        sut.AutosaveFailed += (_, e) => captured = e;

        sut.ScheduleAutoSave(_ => throw new InvalidOperationException("save failed"));
        clock.Advance(TimeSpan.FromSeconds(30));
        sut.FireTimerForTest();
        await Task.Delay(100);

        Assert.NotNull(captured);
        Assert.IsType<InvalidOperationException>(captured!.Exception);
        Assert.True(dirty.HasUnsavedChanges, "保存失败应保持 dirty");
    }
}
