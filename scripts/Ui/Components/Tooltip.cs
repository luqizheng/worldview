using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 悬停提示（§4.4 spec 契约 A2 — Tooltip）
///
/// <para>鼠标悬停 <see cref="ShowUnderCursor"/> 后延迟 <see cref="DelaySec"/> 秒显示。
/// 走 <see cref="PanelContainer"/>(不走 OS 主题)。</para>
///
/// <para>用法: 任意 Control 拿到 Tooltip 实例后调 <c>tooltip.ShowUnderCursor(targetControl)</c>,
/// 或调 <c>tooltip.Hide()</c> 立即消失。</para>
///
/// <para>token 接入:字号 cap 12 / 内边距 8 / 文本色 / 边框 / 背景 / 阴影 / 淡入。</para>
/// </summary>
public partial class Tooltip : PanelContainer
{
    private readonly JrpgIcon? _icon = null; // 可选 icon (先预留)
    private readonly JrpgLabel _label = new();
    private SceneTreeTimer? _showTimer;

    /// <summary>悬停多久显示(秒,默认 0.4)。</summary>
    public float DelaySec { get; set; } = 0.4f;

    /// <summary>提示文本。</summary>
    public string Text
    {
        get => _label.Text;
        set
        {
            _label.Text = value;
            _label.SetFontSize(WorldView.Ui.Theme.Palette.Metric.FontSizeCaption);
        }
    }

    /// <inheritdoc/>
    public Tooltip()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore; // Tooltip 自身不接收 input
        var hbox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        hbox.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        hbox.AddChild(_label);
        AddChild(hbox);
        CustomMinimumSize = new Vector2(120, 24);
    }

    /// <summary>在 target 控件下方显示(默认位置 target 下方偏移 8)。</summary>
    public void ShowUnderCursor(Control target)
    {
        var root = target.GetTree().Root;
        if (GetParent() == null) root.AddChild(this);

        // 计算位置:target 下方偏移 8
        Position = target.GlobalPosition + new Vector2(0, target.Size.Y + 8);

        // 延迟显示
        _showTimer = target.GetTree().CreateTimer(DelaySec);
        _showTimer.Timeout += () => ShowNow();
    }

    private void ShowNow()
    {
        Modulate = Colors.White;
        Show();
    }

    /// <summary>立即消失 + 销毁。</summary>
    public new void Hide()
    {
        _showTimer = null;
        base.Hide();
        QueueFree();
    }
}
