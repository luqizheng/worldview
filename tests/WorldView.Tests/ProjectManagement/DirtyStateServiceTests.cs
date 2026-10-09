using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class DirtyStateServiceTests
{
    [Fact]
    public void Initially_Not_Dirty()
    {
        var sut = new DirtyStateService();
        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void MarkDirty_Fires_Event_And_Sets_Flag()
    {
        var sut = new DirtyStateService();
        var fired = 0;
        sut.DirtyChanged += (_, _) => fired++;

        sut.MarkDirty();

        Assert.True(sut.HasUnsavedChanges);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void MarkDirty_Is_Idempotent_But_Advances_Version()
    {
        // 即便状态已经 dirty，每次 MarkDirty 仍应让 _version 递增，
        // 这样后来的 MarkClean(staleVersion) 才会被识别为过期。
        var sut = new DirtyStateService();
        sut.MarkDirty();
        var v1 = sut.DirtyVersion;

        sut.MarkDirty();
        var v2 = sut.DirtyVersion;

        Assert.True(v2 > v1);
    }

    [Fact]
    public void MarkClean_Current_Version_Clears_Flag()
    {
        var sut = new DirtyStateService();
        sut.MarkDirty();
        var version = sut.DirtyVersion;

        sut.MarkClean(version);

        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void MarkClean_Stale_Version_Keeps_Dirty()
    {
        // 关键回归：保存期间被新编辑覆盖。
        // 场景：保存前 MarkDirty() → version=N → 启动保存（拿到 version=N）→
        //       保存完成前用户又改了一笔 MarkDirty() → version=N+1 →
        //       旧保存回调用 version=N 来 MarkClean → 应保持 dirty。
        var sut = new DirtyStateService();
        sut.MarkDirty();
        var snapshot = sut.DirtyVersion;

        // 模拟"保存进行中被新编辑覆盖"
        sut.MarkDirty();

        // 旧保存完成，调用 MarkClean 用旧版本
        sut.MarkClean(snapshot);

        Assert.True(sut.HasUnsavedChanges);
    }

    [Fact]
    public void PrepareClose_Returns_NoChanges_When_Clean()
    {
        var sut = new DirtyStateService();
        Assert.Equal(CloseRequirement.NoChanges, sut.PrepareClose());
    }

    [Fact]
    public void PrepareClose_Returns_NeedsSave_When_Dirty()
    {
        var sut = new DirtyStateService();
        sut.MarkDirty();
        Assert.Equal(CloseRequirement.NeedsSave, sut.PrepareClose());
    }
}
