using WorldView.Editing;
using Xunit;

namespace WorldView.Tests.Editing;

/// <summary>
/// 撤销 / 重做栈测试。对应 checklist E5 / E6 / E7 / E8 + spec R-UNDO-01/02/03。
///
/// 注:UndoStack 自调 Apply,故 Push 等价于「执行并记录」,Undo/Redo 各自独立触达命令。
/// 所有命令用本地桩,不依赖 EditorContext(因 EditorContext 继承 Godot.Node 不能裸测)。
/// </summary>
public class UndoStackTests
{
    /// <summary>桩命令:记录 Apply / Revert 调用顺序到共享 buffer,便于断言。</summary>
    private sealed class FakeCommand : IEditorCommand
    {
        private readonly List<string> _log;
        private readonly string _name;
        private bool _applied;

        public FakeCommand(List<string> log, string name) { _log = log; _name = name; }

        public string Description => _name;

        public bool IsApplied => _applied;

        public void Apply()
        {
            _applied = true;
            _log.Add($"apply:{_name}");
        }

        public void Revert()
        {
            _applied = false;
            _log.Add($"revert:{_name}");
        }
    }

    [Fact]
    public void New_Stack_Is_Empty()
    {
        var stack = new UndoStack();
        Assert.Equal(0, stack.UndoCount);
        Assert.Equal(0, stack.RedoCount);
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Null(stack.UndoDescription);
    }

    [Fact]
    public void Push_Calls_Apply_Immediately()
    {
        var stack = new UndoStack();
        var log = new List<string>();
        stack.Push(new FakeCommand(log, "a"));

        Assert.Equal(1, stack.UndoCount);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Single(log);
        Assert.Equal("apply:a", log[0]);
    }

    [Fact]
    public void Undo_Calls_Revert_And_Pushes_To_Redo()
    {
        var stack = new UndoStack();
        var log = new List<string>();
        var cmd = new FakeCommand(log, "a");
        stack.Push(cmd);

        Assert.True(stack.Undo());

        Assert.Equal(0, stack.UndoCount);
        Assert.Equal(1, stack.RedoCount);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
        Assert.False(cmd.IsApplied);
        Assert.Equal(new[] { "apply:a", "revert:a" }, log);
    }

    [Fact]
    public void Redo_Reapplies_And_Pushes_Back_To_Undo()
    {
        var stack = new UndoStack();
        var log = new List<string>();
        var cmd = new FakeCommand(log, "a");
        stack.Push(cmd);
        stack.Undo();

        Assert.True(stack.Redo());

        Assert.Equal(1, stack.UndoCount);
        Assert.Equal(0, stack.RedoCount);
        Assert.True(cmd.IsApplied);
        Assert.Equal(new[] { "apply:a", "revert:a", "apply:a" }, log);
    }

    [Fact]
    public void Undo_On_Empty_Stack_Returns_False()
    {
        var stack = new UndoStack();
        Assert.False(stack.Undo());
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void Redo_On_Empty_Redo_Returns_False()
    {
        var stack = new UndoStack();
        Assert.False(stack.Redo());
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void New_Push_Clears_Redo_Branch()
    {
        var stack = new UndoStack();
        var log = new List<string>();
        stack.Push(new FakeCommand(log, "a"));
        stack.Push(new FakeCommand(log, "b"));
        stack.Undo(); // redo 现在有 b
        Assert.True(stack.CanRedo);

        // 新命令推入应清空 redo 分支(R-UNDO-02)
        stack.Push(new FakeCommand(log, "c"));

        Assert.False(stack.CanRedo);
        Assert.Equal(2, stack.UndoCount);
        Assert.Equal(new[] { "apply:a", "apply:b", "revert:b", "apply:c" }, log);
    }

    [Fact]
    public void Push_Past_Capacity_Drops_Oldest_FIFO()
    {
        // spec E7:连续 push 250 条 → 容量 200,最旧的 50 条被丢弃
        var stack = new UndoStack();
        var log = new List<string>();
        for (var i = 0; i < 250; i++)
        {
            stack.Push(new FakeCommand(log, $"c{i}"));
        }

        Assert.Equal(UndoStack.Capacity, stack.UndoCount);
        Assert.Equal(200, UndoStack.Capacity);

        // 第 50 条之前(含)的 apply 都还在 log 里(只是从栈里被丢弃),
        // 但栈顶现在应该是 c249
        Assert.Equal("c249", stack.UndoDescription);

        // 撤销栈顶应是 c249
        stack.Undo();
        Assert.Equal("c248", stack.UndoDescription);
    }

    [Fact]
    public void Apply_Undo_Loop_100_Times_Has_No_Residue()
    {
        // spec R-UNDO-03:循环执行 apply + undo 不留下任何残留。
        // 用计数命令的 Apply / Revert 严格互逆来证明。
        var stack = new UndoStack();
        var counter = new CounterCommand();
        for (var i = 0; i < 100; i++)
        {
            stack.Push(counter); // apply → +1
            Assert.True(stack.Undo()); // revert → 0
        }
        Assert.Equal(0, counter.Value);
        Assert.Equal(0, stack.UndoCount);
        Assert.Equal(100, stack.RedoCount);
    }

    [Fact]
    public void Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric()
    {
        var stack = new UndoStack();
        var counter = new CounterCommand();
        for (var i = 0; i < 100; i++)
        {
            stack.Push(counter);    // +1
            stack.Undo();            // 0
            stack.Redo();            // +1
            stack.Undo();            // 0
        }
        Assert.Equal(0, counter.Value);
        Assert.Equal(0, stack.UndoCount);
        Assert.Equal(100, stack.RedoCount);
    }

    [Fact]
    public void Clear_Wipes_Undo_And_Redo()
    {
        var stack = new UndoStack();
        var log = new List<string>();
        stack.Push(new FakeCommand(log, "a"));
        stack.Push(new FakeCommand(log, "b"));
        stack.Undo();

        stack.Clear();

        Assert.Equal(0, stack.UndoCount);
        Assert.Equal(0, stack.RedoCount);
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    /// <summary>计数命令:Apply +1,Revert -1;严格互逆,便于循环测试。</summary>
    private sealed class CounterCommand : IEditorCommand
    {
        public int Value { get; private set; }
        public string Description => $"counter={Value}";
        public void Apply() => Value++;
        public void Revert() => Value--;
    }
}
