namespace WorldView.Editing;

/// <summary>
/// 编辑命令（见 tasks.md T10 + spec.md §3.5）。
///
/// 纪律：<b>任何文档修改都必须经由命令对象</b>，禁止直接改 <c>MapDoc</c>。
/// 这是撤销栈与将来时间轴差异序列（N02，M5+）共用的唯一写入通道。
/// </summary>
public interface IEditorCommand
{
    /// <summary>人类可读描述，用于状态栏 / 撤销提示。</summary>
    string Description { get; }

    /// <summary>执行（由 <c>UndoStack.Push</c> 自动调用，勿手动调用）。</summary>
    void Apply();

    /// <summary>回滚到执行前状态。</summary>
    void Revert();
}
