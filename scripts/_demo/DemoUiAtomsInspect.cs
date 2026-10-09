using Godot;
using WorldView.Ui.Components;

namespace WorldView._demo;

/// <summary>
/// Inspect mode: 启动后 200ms 检查场景内所有 Button 的 Pressed signal connection 数量,
/// 输出到控制台验证 demo 的 Pressed += 是否生效。
/// </summary>
public partial class DemoUiAtomsInspect : Node
{
    private double _elapsed;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > 0.5)
        {
            var demoScene = ResourceLoader.Load<PackedScene>("res://scenes/_demo/DemoUiAtoms.tscn");
            GD.Print($"[Inspect] demoScene null? {demoScene == null}");
            if (demoScene != null)
            {
                var inst = demoScene.Instantiate();
                GD.Print($"[Inspect] inst type: {inst.GetType().Name}, is Control: {inst is Control}");
                if (inst is Control ctrl)
                {
                    AddChild(ctrl);
                    // Wait one frame for _Ready to complete
                    CallDeferred(MethodName.InspectAfterReady, ctrl);
                }
            }
        }
    }

    private void InspectAfterReady(Control ctrl)
    {
        GD.Print($"[Inspect] after-ready, child count: {ctrl.GetChildCount()}");
        InspectButtons(ctrl, 0);

        // Try simulating a click via EmitSignal on the openFloatingBtn / openDialogBtn
        var buttonRow = FindNodeByName(ctrl, "@HBoxContainer@66");
        if (buttonRow is Container container)
        {
            foreach (var child in container.GetChildren())
            {
                if (child is Button btn)
                {
                    GD.Print($"[Inspect] emitting Pressed on button: '{btn.Text}'");
                    btn.EmitSignal(BaseButton.SignalName.Pressed);
                }
            }
        }
        else
        {
            GD.Print($"[Inspect] buttonRow null? {buttonRow == null}");
        }

        // Test: manually create a FloatingWindow and inspect its state
        GD.Print("[Inspect] === Manual FloatingWindow test (after fix) ===");
        try
        {
            var win = new WorldView.Ui.Components.FloatingWindow()
                .SetTitle("Manual test")
                .SetContent(new Label { Text = "manual test content" });
            ctrl.AddChild(win);
            GD.Print($"[Inspect] after AddChild: Position={win.Position}, Size={win.Size}, Visible={win.Visible}");
            // Fixed API: PopupCentered(windowSize)
            win.PopupCentered(new Vector2(360, 200));
            GD.Print($"[Inspect] after PopupCentered(360,200): Position={win.Position}, Size={win.Size}, Visible={win.Visible}, Parent.Size={ctrl.Size}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Inspect] manual test exception: {ex.Message}\n{ex.StackTrace}");
        }
        GetTree().Quit(0);
    }

    private static Node? FindNodeByName(Node root, string name)
    {
        if (root.Name == name) return root;
        foreach (var child in root.GetChildren())
        {
            var found = FindNodeByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void InspectButtons(Node node, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (node is Button btn)
        {
            var connCount = btn.GetSignalConnectionList(BaseButton.SignalName.Pressed).Count;
            GD.Print($"{indent}[BTN] {node.Name} ({node.GetType().Name}) Text='{btn.Text}' PressedConnections={connCount}");
        }
        else
        {
            GD.Print($"{indent}[NODE] {node.Name} ({node.GetType().Name})");
        }
        foreach (var child in node.GetChildren())
        {
            InspectButtons(child, depth + 1);
        }
    }
}
