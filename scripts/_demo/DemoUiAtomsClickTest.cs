using Godot;
using WorldView.Ui.Components;

namespace WorldView._demo;

/// <summary>
/// 临时的 click 模拟器,用于 headless 测试 DemoUiAtoms 的按钮是否触发 Pressed。
///
/// <para>启动后 200ms 触发"打开浮窗"按钮的 Pressed,再 200ms 触发"打开对话框"。
/// 通过看 GD.Print 是否输出 "button clicked" 来确认 Pressed 信号走通。</para>
/// </summary>
public partial class DemoUiAtomsClickTest : Control
{
    private JrpgButton? _openFloatingBtn;
    private JrpgButton? _openDialogBtn;
    private int _step;
    private double _elapsed;

    public override void _Ready()
    {
        var root = new VBoxContainer();
        AddChild(root);
        _openFloatingBtn = new JrpgButton { Text = "TEST: 模拟打开浮窗" };
        _openFloatingBtn.Pressed += () => GD.Print("[Test] 打开浮窗 Pressed fired");
        root.AddChild(_openFloatingBtn);
        _openDialogBtn = new JrpgButton { Text = "TEST: 模拟打开对话框" };
        _openDialogBtn.Pressed += () => GD.Print("[Test] 打开对话框 Pressed fired");
        root.AddChild(_openDialogBtn);

        GD.Print("[Test] ready, will simulate clicks via Pressed.EmitSignal");
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > 0.3 && _step == 0 && _openFloatingBtn != null)
        {
            GD.Print("[Test] emitting openFloatingBtn.Pressed signal");
            _openFloatingBtn.EmitSignal(BaseButton.SignalName.Pressed);
            _step = 1;
        }
        if (_elapsed > 0.6 && _step == 1 && _openDialogBtn != null)
        {
            GD.Print("[Test] emitting openDialogBtn.Pressed signal");
            _openDialogBtn.EmitSignal(BaseButton.SignalName.Pressed);
            _step = 2;
        }
        if (_elapsed > 1.0)
        {
            GetTree().Quit(0);
        }
    }
}
