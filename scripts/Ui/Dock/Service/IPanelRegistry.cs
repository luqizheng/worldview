using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Dock.Service;

/// <summary>
/// 面板向布局系统登记自己。替代 <c>DockLayout.Instance?.RegisterPanel(this)</c>
/// 的反环访问 (C 轮 Task 2)。由 <see cref="DockPanel"/> 在 <c>_Ready</c> 里调用。
/// </summary>
public interface IPanelRegistry
{
    /// <summary>登记面板，重复登记覆盖旧的（稳定 ID 跨会话复用）。</summary>
    void Register(DockPanel panel);

    /// <summary>取消登记（目前 <see cref="DockLayout"/> 无对应动作，预留）。</summary>
    void Unregister(DockPanel panel);

    /// <summary>按 ID 取面板（不存在返回 null）。</summary>
    DockPanel? Find(string panelId);
}
