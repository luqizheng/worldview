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
                    .SetText("这是 JRPG 模态对话框(PanelContainer + dim overlay)\n按 Esc 或点关闭按钮淡出"));
                dlg.BodyRoot.AddChild(new JrpgButton { Text = "确认" });
                // 模态弹出:overlay 拦截输入 + dialog 居中
                dlg.PopupModal(this);
                GD.Print($"[Demo] 对话框位置: {dlg.Position}, 大小: {dlg.Size}, 父容器: {Size}");
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

        // ---- A2 5 原子 ----
        root.AddChild(new JrpgLabel()
            .SetText("--- A2 5 原子 (ToolButton / Menu / Toast / Tooltip / Tab) ---")
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder));

        // ToolButton (active 切换)
        var toolBtn = new ToolButton()
            .SetIcon(JrpgIcon.Shape.Check)
            .SetText("Bold")
            .AsToggle();
        root.AddChild(toolBtn);

        // Menu (按钮触发 + 3 项)
        var menu = new Menu();
        menu.AddItem("打开", () => GD.Print("[Demo] Menu: 打开"));
        menu.AddItem("保存", () => GD.Print("[Demo] Menu: 保存"));
        menu.AddSeparator();
        menu.AddItem("退出", () => GD.Print("[Demo] Menu: 退出"));
        var openMenuBtn = new JrpgButton { Text = "打开菜单" };
        openMenuBtn.Pressed += () => menu.PopupCentered(this);
        root.AddChild(openMenuBtn);

        // Toast (按钮触发 + 2.5s 自动消失)
        var toast = new Toast();
        var showToastBtn = new JrpgButton { Text = "显示 Toast" };
        showToastBtn.Pressed += () => toast.ShowMessage(this, "已保存", Toast.Severity.Success, Toast.PositionEnum.BottomRight, 2.5f);
        root.AddChild(showToastBtn);

        // Tooltip (悬停 0.4s 延迟显示)
        var tipTarget = new JrpgLabel().SetText("悬停看我 0.4s 出 tooltip");
        var tip = new Tooltip { Text = "这是 Tooltip 提示文本" };
        tipTarget.MouseEntered += () => tip.ShowUnderCursor(tipTarget);
        tipTarget.MouseExited += () => tip.Hide();
        root.AddChild(tipTarget);

        // Tab (3 项 + 默认第二项 active)
        var tab = new Tab();
        tab.AddTab("Layer");
        tab.AddTab("Inspector");
        tab.AddTab("Settings");
        tab.ActiveIndex = 1;
        tab.TabChanged += idx => GD.Print($"[Demo] Tab 切换到 {idx}");
        root.AddChild(tab);

        // ---- A4 数值 3 Field ----
        root.AddChild(new JrpgLabel()
            .SetText("--- A4 数值 3 Field (Slider / Color / Date) ---")
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder));

        // SliderField (范围 0-200, 步长 10, 默认 50)
        var slider = new SliderField()
            .SetLabel("密度")
            .SetRange(0, 200, 10)
            .SetValue(50);
        slider.ValueChanged += v => GD.Print($"[Demo] Slider value: {v}");
        root.AddChild(slider);

        // ColorPickerField (默认 JRPG 暖色 jrpg_bg)
        var colorField = new ColorPickerField()
            .SetLabel("主色")
            .SetValue(WorldView.Ui.Theme.Palette.Color.JrpgBg);
        colorField.OnChange(c => GD.Print($"[Demo] Color: {ColorPickerField.ToHex(c)}"));
        root.AddChild(colorField);

        // DateField (今天 ± 30 天, 越界 prev/next 静默)
        var today = System.DateOnly.FromDateTime(System.DateTime.Today);
        var dateField = new DateField()
            .SetLabel("日期")
            .SetRange(today.AddDays(-30), today.AddDays(30))
            .SetValue(today);
        dateField.DateChanged += d => GD.Print($"[Demo] Date: {d}");
        root.AddChild(dateField);

        // GrabFocus 让 F5 进去直接键盘 / 手柄能走 Tab + Enter + Esc, 不必先点鼠标
        openFloatingBtn.GrabFocus();

        GD.Print("[DemoUiAtoms] ready — 验证 11 组件 + 4 Field + A2 5 原子 + A4 数值 3 Field 渲染");
    }
}
