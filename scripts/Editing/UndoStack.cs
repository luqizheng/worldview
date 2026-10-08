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
///   <item>批处理：<see cref="BeginBatch"/> + <see cref="EndBatch"/> 把一组命令作为
///         <b>一个单位</b>登记到撤销表。批内 <c>Push</c> 仍立即 <c>Apply</c>，
///         但不直接进撤销表，只追加到当前打开的 <see cref="CompositeCommand"/>；
///         <c>EndBatch</c> 时把整个批作为一个单位登记（不再重放 Apply），
///         空批则什么也不登记（避免「按 Ctrl+Z 没反应」的空步）。
///         嵌套 BeginBatch = 沿用外层（忽略内层）。
///         批处理单位与单命令共享同一容量与 FIFO 淘汰。</item>
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
    private CompositeCommand? _openBatch;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>栈顶命令的描述（无则 null）。</summary>
    public string? UndoDescription => _undo.Last?.Value.Description;

    /// <summary>当前是否处于批处理中。</summary>
    public bool IsBatching => _openBatch is not null;

    /// <summary>当前批描述（无则 null）；嵌套时返回外层描述。</summary>
    public string? BatchDescription => _openBatch?.Description;

    /// <summary>
    /// 推入并立即执行一条命令；超过容量则丢弃最旧的一条。
    /// 处于批处理中时：仍立即 <c>Apply</c>，但仅追加到当前打开的
    /// <see cref="CompositeCommand"/>，不直接进入撤销表。
    /// </summary>
    public void Push(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_openBatch is not null)
        {
            command.Apply();
            _openBatch.Add(command);
            // 批内 Push 仍需清空 redo 分支，与 R-UNDO-02 一致。
            _redo.Clear();
            return;
        }

        command.Apply();
        EnqueueUnit(command);
    }

    /// <summary>
    /// 打开一个批。<c>EndBatch</c> 时把当前批内所有 <c>Push</c> 进来的命令作为
    /// 一个单位登记到撤销表。嵌套调用时保留外层（忽略内层描述）。
    /// </summary>
    public void BeginBatch(string description)
    {
        if (_openBatch is not null)
        {
            // 嵌套：忽略内层 BeginBatch,沿用外层描述。
            return;
        }
        _openBatch = new CompositeCommand(description);
    }

    /// <summary>
    /// 关闭当前批。批内 ≥1 条命令 → 作为 <b>一个单位</b>登记到撤销表；
    /// 批内 0 条 → 不登记（避免空步）；未打开批则空操作。
    /// </summary>
    public void EndBatch()
    {
        if (_openBatch is null)
        {
            return;
        }

        var batch = _openBatch;
        _openBatch = null;

        if (batch.Count == 0)
        {
            // 空批：什么都不压,避免「Ctrl+Z 没反应」。
            return;
        }

        // 批内命令已经全部 Apply 过了,直接作为一个单位登记。
        EnqueueUnit(batch);
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
        _openBatch = null;
    }

    /// <summary>
    /// 把一个撤销单位（单命令或 CompositeCommand）登记到 undo 表；
    /// 超过容量则 FIFO 淘汰最旧；同时清空 redo 分支（R-UNDO-02）。
    /// </summary>
    private void EnqueueUnit(IEditorCommand unit)
    {
        _undo.AddLast(unit);

        while (_undo.Count > Capacity)
        {
            _undo.RemoveFirst();
        }

        _redo.Clear();
    }
}
