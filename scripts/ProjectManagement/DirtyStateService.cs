using System;

namespace WorldView.ProjectManagement;

/// <summary>
/// 关闭前脏状态要求：<see cref="NoChanges"/> 表示可无障碍关闭；<see cref="NeedsSave"/> 表示需要先保存。
/// </summary>
public enum CloseRequirement
{
    NoChanges,
    NeedsSave,
}

/// <summary>
/// 工程脏状态服务。负责追踪是否有未保存修改，并在保存/回滚时协调 dirty 状态。
/// <para>关键设计：单调递增的 <see cref="DirtyVersion"/> 计数器——保存开始时记录版本号，
/// 保存结束后用记录的版本号调用 <see cref="MarkClean(int)"/>；仅当当前版本仍等于该快照时
/// 才清脏，否则视为"保存期间被新编辑覆盖"保留 dirty 状态。</para>
/// </summary>
public sealed class DirtyStateService
{
    private int _version;
    private bool _isDirty;

    /// <summary>当前脏状态版本号。保存前后用其判断新旧。</summary>
    public int DirtyVersion => _version;

    /// <summary>是否有未保存修改。</summary>
    public bool HasUnsavedChanges => _isDirty;

    /// <summary>脏状态切换事件。MarkDirty / MarkClean 都会触发。</summary>
    public event EventHandler? DirtyChanged;

    /// <summary>标记脏。每次调用都让 <see cref="DirtyVersion"/> 递增。</summary>
    public void MarkDirty()
    {
        _version++;
        var wasDirty = _isDirty;
        _isDirty = true;
        if (!wasDirty)
        {
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 按版本号尝试清脏。
    /// <list type="bullet">
    ///   <item><paramref name="version"/> 等于当前 <see cref="DirtyVersion"/> → 清脏。</item>
    ///   <item><paramref name="version"/> 落后于当前版本 → 视为"保存期间被新编辑覆盖"，保留 dirty。</item>
    /// </list>
    /// </summary>
    public void MarkClean(int version)
    {
        if (version != _version) return;
        if (!_isDirty) return;
        _isDirty = false;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>为关闭流程提供当前脏状态视图。</summary>
    public CloseRequirement PrepareClose()
        => _isDirty ? CloseRequirement.NeedsSave : CloseRequirement.NoChanges;
}
