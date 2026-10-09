using Godot;
using WorldView.Ui.Components;

namespace WorldView._demo;

/// <summary>
/// UI Atoms Demo 场景（Task 11 手测）
///
/// <para>展示 11 组件 + 4 Field 的渲染形态,验证 token 真源走通后视觉一致。
/// F5 在 Godot 4.7.2 mono 中打开 <c>scenes/_demo/DemoUiAtoms.tscn</c> 即可。</para>
///
/// <para>布局:左侧 6 形态 JrpgIcon 矩阵(3 档 × 6 形态) + 中部 4 Field 表单
/// + 右侧 2 浮窗(Dialog + FloatingWindow)。</para>
/// </summary>
public partial class DemoUiAtoms : Control
{
    public override void _Ready()
    {
        // 强制根 Control 铺满视口(anchors_preset=15 在 .tscn 里被设了但某些 Godot 4.7.2 路径
        // 不会重算,需要这里显式设 Size 兜底)
        var viewport = GetViewport().GetVisibleRect().Size;
        SetAnchorsPreset(LayoutPreset.FullRect, keepOffsets: false);
        Size = viewport;
        GD.Print($"[Demo] viewport size: {viewport}, root Size after set: {Size}");


        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(root);

        // ---- 标题 ----
        root.AddChild(new JrpgLabel()
            .SetText("JRPG UI Atoms Demo — Token 真源走通验证")
            .SetFontSize(WorldView.Ui.Theme.Palette.Metric.FontSizeTitle)
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgOutline));

        // ---- 6 形态 × 3 档 JrpgIcon 矩阵 ----
        root.AddChild(new JrpgLabel()
            .SetText("--- 6 Icon 形态 × 3 档 ---")
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder));

        var iconMatrix = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        iconMatrix.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        foreach (var shape in Enum.GetValues<JrpgIcon.Shape>())
        {
            var col = new VBoxContainer();
            col.AddChild(new JrpgLabel().SetText(shape.ToString()));
            col.AddChild(new JrpgIcon()
                .SetShape(shape)
                .SetSizeTier(JrpgIcon.SizeTier.Xs));
            col.AddChild(new JrpgIcon()
                .SetShape(shape)
                .SetSizeTier(JrpgIcon.SizeTier.Sm));
            col.AddChild(new JrpgIcon()
                .SetShape(shape)
                .SetSizeTier(JrpgIcon.SizeTier.Md));
            iconMatrix.AddChild(col);
        }
        root.AddChild(iconMatrix);

        // ---- 4 Field 表单 ----
        root.AddChild(new JrpgLabel()
            .SetText("--- 4 Field 表单 ---")
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder));

        var form = new JrpgForm();
        form.AddField(new TextField()
            .SetLabel("城市名 *")
            .Required()
            .SetValue(""))
            .AddField(new ToggleField()
                .SetLabel("已访问")
                .SetValue("false"))
            .AddField(new EnumField()
                .SetLabel("等级")
                .SetOptions(new[] { "小镇", "城市", "首都" })
                .SetSelectedIndex(0))
            .AddField(new ButtonField()
                .SetLabel("操作")
                .SetButtonText("保存")
                .OnClick(() => GD.Print("[Demo] 保存按钮被点击")));
        root.AddChild(form);

        var validateBtn = new JrpgButton { Text = "校验表单" };
        validateBtn.Pressed += () =>
        {
            var ok = form.Validate();
            GD.Print($"[Demo] 校验结果: {(ok ? "通过" : "失败")}");
        };
        root.AddChild(validateBtn);

        // ---- CloseButton + FloatingWindow + Dialog ----
        root.AddChild(new JrpgLabel()
            .SetText("--- CloseButton + FloatingWindow + Dialog ---")
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder));

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        var openFloatingBtn = new JrpgButton { Text = "打开浮窗" };
        openFloatingBtn.Pressed += () =>
        {
            GD.Print("[Demo] 打开浮窗 button clicked");
            try
            {
                var win = new FloatingWindow()
                    .SetTitle("JRPG 浮窗")
                    .SetContent(new JrpgLabel()
                        .SetText("可拖动标题条 / Esc 关闭\n走 Palette 量化层 + 描边 + 阴影"));
                // 加到本场景根(Control 全屏铺满视口),保证坐标空间一致
                AddChild(win);
                // 用目标窗口尺寸调 PopupCentered(FloatingWindow 内的算法会用父容器 Size 居中)
                win.PopupCentered(new Vector2(360, 200));
                GD.Print($"[Demo] 浮窗位置: {win.Position}, 大小: {win.Size}, 父容器: {Size}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[Demo] 打开浮窗异常: {ex.Message}");
            }
        };
        var openDialogBtn = new JrpgButton { Text = "打开对话框" };
        openDialogBtn.Pressed += () =>
        {
            GD.Print("[Demo] 打开对话框 button clicked");
            try
            {
                var dlg = new Dialog()
                    .SetTitle("确认操作");
                dlg.BodyRoot.AddChild(new JrpgLabel()
                    .SetText("这是 JRPG 模态对话框(继承 Godot.Window)\n按 Esc 或点关闭按钮淡出"));
                dlg.BodyRoot.AddChild(new JrpgButton { Text = "确认" });
                // Window 节点必须加到另一个 Window 下(GetTree().Root 就是主 Window)
                GetTree().Root.AddChild(dlg);
                // Godot 内建 PopupCentered(Vector2I size) = 想要的目标尺寸,自动居中
                dlg.PopupCentered(new Vector2I(360, 180));
                GD.Print($"[Demo] 对话框位置: {dlg.Position}, 大小: {dlg.Size}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[Demo] 打开对话框异常: {ex.Message}");
            }
        };
        buttonRow.AddChild(openFloatingBtn);
        buttonRow.AddChild(openDialogBtn);
        buttonRow.AddChild(new CloseButton());
        root.AddChild(buttonRow);

        GD.Print("[DemoUiAtoms] ready — 验证 11 组件 + 4 Field 渲染");
    }
}
