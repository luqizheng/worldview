using WorldView.Editing;
using Xunit;

namespace WorldView.Tests.Editing;

public class UndoStackBatchTests
{
    private sealed class FakeCommand : IEditorCommand
    {
        private readonly List<string> _log;
        private readonly string _name;
        public FakeCommand(List<string> log, string name) { _log = log; _name = name; }
        public string Description => _name;
        public void Apply() => _log.Add($"apply:{_name}");
        public void Revert() => _log.Add($"revert:{_name}");
    }

    [Fact]
    public void Batch_Of_Three_Pushes_Is_OneUndoUnit()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.BeginBatch("stroke-1");
        stack.Push(new FakeCommand(log, "c1"));
        stack.Push(new FakeCommand(log, "c2"));
        stack.Push(new FakeCommand(log, "c3"));
        Assert.Equal(3, log.Count);
        Assert.All(log, s => Assert.StartsWith("apply:", s));
        stack.EndBatch();
        Assert.Equal(1, stack.UndoCount);
        Assert.Equal("stroke-1", stack.UndoDescription);
    }

    [Fact]
    public void Undo_RevertsBatch_InReverseOrder()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.BeginBatch("stroke");
        stack.Push(new FakeCommand(log, "c1"));
        stack.Push(new FakeCommand(log, "c2"));
        stack.Push(new FakeCommand(log, "c3"));
        stack.EndBatch();
        stack.Undo();
        // Expect 3 apply + 3 revert in reverse
        Assert.Equal(new[] {
            "apply:c1", "apply:c2", "apply:c3",
            "revert:c3", "revert:c2", "revert:c1"
        }, log);
    }

    [Fact]
    public void Redo_AppliesBatch_InForwardOrder()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.BeginBatch("stroke");
        stack.Push(new FakeCommand(log, "c1"));
        stack.Push(new FakeCommand(log, "c2"));
        stack.Push(new FakeCommand(log, "c3"));
        stack.EndBatch();
        stack.Undo();
        log.Clear();
        stack.Redo();
        Assert.Equal(new[] { "apply:c1", "apply:c2", "apply:c3" }, log);
    }

    [Fact]
    public void EmptyBatch_PushesNothing_AndUndoReturnsFalse()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.BeginBatch("empty");
        stack.EndBatch();
        Assert.Equal(0, stack.UndoCount);
        Assert.False(stack.Undo());
    }

    [Fact]
    public void EndBatch_WithoutBegin_IsNoOp()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.EndBatch();   // 没有 BeginBatch: no throw, no state change
        Assert.Equal(0, stack.UndoCount);
        Assert.False(stack.IsBatching);
    }

    [Fact]
    public void NestedBegin_KeepsOuterDescription()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.BeginBatch("outer");
        stack.BeginBatch("inner");   // ignored
        Assert.Equal("outer", stack.BatchDescription);
        stack.Push(new FakeCommand(log, "x"));
        stack.EndBatch();             // ends the outer (inner was no-op)
        Assert.Equal(1, stack.UndoCount);
        Assert.Equal("outer", stack.UndoDescription);
    }

    [Fact]
    public void Batch_CountsAsOneAgainstCapacity()
    {
        // push 250 single commands (no batch) → UndoCount = Capacity = 200 (FIFO drops oldest 50)
        var stack = new UndoStack();
        for (int i = 0; i < 250; i++)
            stack.Push(new FakeCommand(new List<string>(), $"c{i}"));
        Assert.Equal(UndoStack.Capacity, stack.UndoCount);   // 200

        // now: 1 batch with 100 commands → still Capacity, batch is one unit
        var log = new List<string>();
        stack.BeginBatch("big");
        for (int i = 0; i < 100; i++)
            stack.Push(new FakeCommand(log, $"b{i}"));
        stack.EndBatch();
        // Before batch EndBatch: UndoCount was 200 (FIFO of single commands).
        // After EndBatch with 1 unit: still 200 (the batch is the 200th, oldest single dropped).
        Assert.Equal(UndoStack.Capacity, stack.UndoCount);
        // Undo once → entire batch reverts (100 reverts in log)
        stack.Undo();
        Assert.Equal(100, log.Count(c => c.StartsWith("revert:")));
    }

    [Fact]
    public void Push_OutsideBatch_StillWorks()
    {
        // Regression: existing Push semantics unchanged when not batching.
        var log = new List<string>();
        var stack = new UndoStack();
        stack.Push(new FakeCommand(log, "solo"));
        Assert.Equal(1, stack.UndoCount);
        Assert.False(stack.IsBatching);
        Assert.Equal("solo", stack.UndoDescription);
        stack.Undo();
        Assert.Equal(new[] { "apply:solo", "revert:solo" }, log);
    }
}
