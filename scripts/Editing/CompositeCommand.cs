using System.Collections.Generic;

namespace WorldView.Editing;

/// <summary>
/// 把若干子命令作为一个原子单位（spec 2 §6.2）。
/// Apply 正序调用所有子命令；Revert 逆序回滚。
/// </summary>
public sealed class CompositeCommand : IEditorCommand
{
    private readonly List<IEditorCommand> _commands = new();

    public CompositeCommand(string description) { Description = description; }

    public string Description { get; }

    public int Count => _commands.Count;

    public void Add(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }

    public void Apply()
    {
        for (int i = 0; i < _commands.Count; i++) _commands[i].Apply();
    }

    public void Revert()
    {
        for (int i = _commands.Count - 1; i >= 0; i--) _commands[i].Revert();
    }
}
