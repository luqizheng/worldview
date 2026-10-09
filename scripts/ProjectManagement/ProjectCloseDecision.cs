namespace WorldView.ProjectManagement;

/// <summary>
/// 关闭工程时的可选决策（规格 §5.4）。
/// UI 层在本轮之前不会用到这些值；ProjectService.CloseProject(decision) 已经按这个三态处理。
/// </summary>
public enum ProjectCloseDecision
{
    /// <summary>保存当前修改后关闭。</summary>
    SaveAndClose,

    /// <summary>丢弃当前修改直接关闭。</summary>
    CloseWithoutSaving,

    /// <summary>放弃关闭动作，回到当前会话。</summary>
    Cancel,
}
