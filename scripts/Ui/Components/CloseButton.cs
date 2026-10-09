using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 关闭按钮（§4.5.2 契约 / Dialog / FloatingWindow 右上角）
///
/// <para>继承 <see cref="Button"/>,内嵌 <see cref="Icon"/>(<c>Shape.Close</c>)。
/// 3 状态（normal / hover / pressed）+ 关闭时 <c>modulate.a</c> 淡出到 0 再 QueueFree,
/// 时长走 <c>Palette.Animation.FadeOutMs</c>。</para>
///
/// <para>默认 24x24(<c>IconSizeSm</c>),无 padding,只画 X 形。
/// 颜色走 <c>Palette.Color.JrpgTextPlaceholder</c>(灰棕,弱化关闭动作),hover 切到
/// <c>JrpgText</c>(深棕,视觉提升)。</para>
/// </summary>
public partial class CloseButton : Button
{
    private readonly JrpgIcon _icon;

    /// <inheritdoc/>
    public CloseButton()
    {
        _icon = new JrpgIcon()
            .SetShape(JrpgIcon.Shape.Close)
            .SetSizeTier(JrpgIcon.SizeTier.Sm)
            .SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder);

        var px = WorldView.Ui.Theme.Palette.Metric.IconSizeSm;
        CustomMinimumSize = new Vector2(px, px);
        FocusMode = FocusModeEnum.None;
        TooltipText = "关闭";

        // Button 内置 padding 影响视觉;清空让它只显示 Icon
        AddThemeConstantOverride("h_separation", 0);
        AddThemeConstantOverride("v_separation", 0);

        AddChild(_icon);
        _icon.AnchorRight = 1f;
        _icon.AnchorBottom = 1f;
        _icon.OffsetRight = 0;
        _icon.OffsetBottom = 0;

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        ButtonDown += OnButtonDown;
        ButtonUp += OnButtonUp;
        Pressed += OnPressed;
    }

    private void OnMouseEntered()
    {
        _icon.SetColor(WorldView.Ui.Theme.Palette.Color.JrpgText);
    }

    private void OnMouseExited()
    {
        _icon.SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder);
    }

    private void OnButtonDown()
    {
        _icon.SetColor(WorldView.Ui.Theme.Palette.Color.JrpgDanger);
    }

    private void OnButtonUp()
    {
        // 按下后释放:回到 hover(若仍 hover)或 normal
        if (IsHovered())
        {
            _icon.SetColor(WorldView.Ui.Theme.Palette.Color.JrpgText);
        }
        else
        {
            _icon.SetColor(WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder);
        }
    }

    /// <summary>语义化：触发关闭（淡出 + QueueFree）—— Dialog/FloatingWindow 父节点调。</summary>
    public void TriggerClose()
    {
        // fade-out modulate.a 从 1 → 0,再 QueueFree
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, WorldView.Ui.Theme.Palette.Animation.FadeOutMs / 1000f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void OnPressed()
    {
        // 默认按下即关闭。Dialog/FloatingWindow 派生子类可 override OnPressed 改写行为。
        TriggerClose();
    }
}
