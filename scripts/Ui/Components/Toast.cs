using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 短消息提示（§4.3 spec 契约 A2 — Toast）
///
/// <para>屏幕角弹出短消息,2.5s 自动消失。继承 <see cref="PanelContainer"/>(不走 OS Window 主题)。</para>
///
/// <para>用法: <c>toast.ShowMessage(this, "已保存", Toast.Severity.Success);</c>
/// 内部:加到 host.GetTree().Root,Position 按 Severity/Position 枚举算,
/// 淡出走 <c>FadeOutMs</c>。</para>
///
/// <para>token 接入:字号 14 / 字号 cap 12 / 字号 title 16 / 内边距 8 / 严重色 / 文本色 / 边框 / 阴影 / 淡出 / 圆角。</para>
/// </summary>
public partial class Toast : PanelContainer
{
    [Signal] public delegate void TimeoutEventHandler();

    /// <summary>严重级别(决定左边色条色)。</summary>
    public enum Severity { Info, Success, Warning, Error }

    /// <summary>弹出位置(屏幕角)。</summary>
    public enum PositionEnum { TopRight, BottomRight, TopCenter, BottomCenter }

    private readonly ColorRect _bar = new();
    private readonly JrpgLabel _label = new();
    private Tween? _fadeTween;
    private SceneTreeTimer? _hideTimer;

    /// <inheritdoc/>
    public Toast()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _bar.MouseFilter = MouseFilterEnum.Ignore;
        _bar.CustomMinimumSize = new Vector2(8, 0);
        _bar.AnchorBottom = 1.0f;
        _bar.Color = WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder;

        var hbox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
        };
        hbox.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        hbox.AddChild(_bar);
        hbox.AddChild(_label);
        AddChild(hbox);
    }

    /// <summary>在 host 控件附近弹出消息。Position 决定屏幕角。</summary>
    public void ShowMessage(Control host, string text, Severity sev = Severity.Info, PositionEnum pos = PositionEnum.BottomRight, float autoHideSec = 2.5f)
    {
        var root = host.GetTree().Root;
        if (GetParent() == null) root.AddChild(this);

        // 严重色映射
        _bar.Color = sev switch
        {
            Severity.Error => WorldView.Ui.Theme.Palette.Color.JrpgDanger,
            Severity.Success => WorldView.Ui.Theme.Palette.Color.JrpgSuccess,
            Severity.Warning => WorldView.Ui.Theme.Palette.Color.JrpgOutlineFocus,
            _ => WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder,
        };
        _label.Text = text;
        _label.SetFontSize(WorldView.Ui.Theme.Palette.Metric.FontSizeBody);

        // 按 Position 算屏幕坐标(viewport)
        var vp = root.GetViewport().GetVisibleRect().Size;
        var size = new Vector2(280, WorldView.Ui.Theme.Palette.Metric.ListRowHeight + 8);
        const float margin = 16f;
        Position = pos switch
        {
            PositionEnum.TopRight => new Vector2(vp.X - size.X - margin, margin),
            PositionEnum.BottomRight => new Vector2(vp.X - size.X - margin, vp.Y - size.Y - margin),
            PositionEnum.TopCenter => new Vector2((vp.X - size.X) * 0.5f, margin),
            PositionEnum.BottomCenter => new Vector2((vp.X - size.X) * 0.5f, vp.Y - size.Y - margin),
            _ => new Vector2(vp.X - size.X - margin, vp.Y - size.Y - margin),
        };
        Size = size;
        Modulate = Colors.White;
        Show();

        // 自动消失:延迟 + 淡出
        _hideTimer = host.GetTree().CreateTimer(autoHideSec);
        _hideTimer.Timeout += FadeOutAndFree;
    }

    private void FadeOutAndFree()
    {
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 0.0f, WorldView.Ui.Theme.Palette.Animation.FadeOutMs / 1000.0f);
        _fadeTween.TweenCallback(Callable.From(() =>
        {
            EmitSignal(SignalName.Timeout);
            QueueFree();
        }));
    }

    /// <summary>语义化:立即触发淡出 + 销毁。</summary>
    public void Dismiss()
    {
        FadeOutAndFree();
    }
}
