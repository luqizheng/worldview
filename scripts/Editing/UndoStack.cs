using System.Collections.Generic;

namespace WorldView.Editing;

/// <summary>
/// 撤销 / 重做栈（见 tasks.md T10 + R-UNDO-01/02/03）。
///
/// 语义：
/// <list type="bullet">
///   <item>容量固定 <see cref="Capacity"/> = 200，FIFO 淘汰最旧命令（R-UNDO-01）。</item>
///   <item><see cref="Push"/> 会自行调用 <c>cmd.Apply()</c>，调用方不要先 Apply。</item>
///   <item>新命令推入时清空 redo 分支（R-UNDO-02）。</item>
/// </list>
///
/// 纯 C#，不依赖 Godot，可直接单测。
/// </summary>
public sealed class UndoStack
{
    /// <summary>撤销栈上限（tasks.md T10：FIFO 上限 200）。</summary>
    public const int Capacity = 200;

    private readonly LinkedList<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>栈顶命令的描述（无则 null）。</summary>
    public string? UndoDescription => _undo.Last?.Value.Description;

    /// <summary>推入并立即执行一条命令；超过容量则丢弃最旧的一条。</summary>
    public void Push(IEditorCommand command)
    {
        command.Apply();
        _undo.AddLast(command);

        while (_undo.Count > Capacity)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
    }

    /// <summary>撤销最近一条命令。栈空返回 false。</summary>
    public bool Undo()
    {
        if (_undo.Last is null)
        {
            return false;
        }

        var command = _undo.Last.Value;
        _undo.RemoveLast();
        command.Revert();
        _redo.Push(command);
        return true;
    }

    /// <summary>重做最近被撤销的命令。redo 栈空返回 false。</summary>
    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var command = _redo.Pop();
        command.Apply();
        _undo.AddLast(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
