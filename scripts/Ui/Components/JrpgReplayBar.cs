// scripts/Ui/Components/JrpgReplayBar.cs
//
// §4.9.5 4 模式视觉差异 — 回放模式顶条（v0.1 简化实装）。
//
// 契约：24px 半透明条（80% #2a1f10 深褐底）+ 标题(地图名) + 时刻度进度条 (#e8c34a 金黄)。
// 进度值由 EditorController 模拟（v0.1 简化：M3+ 时间轴实装后接真值）。
//
// 接入：Main.tscn 挂载，EditorController.MouseMoved 推送 SetProgress；ModeChanged 切显隐。
// 纪律：唯一破例半透明深褐底（§4.5 UI 全用羊皮底），§4.9.5 显式允许。

using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

public partial class JrpgReplayBar : PanelContainer
{
    public const int BarHeight = 24;

    private static readonly Color OverlayBg     = new("2a1f10cc");  // #2a1f10 80% alpha
    private static readonly Color OverlayTitle  = new("fff8e7ff");  // #fff8e7
    private static readonly Color ProgressFill  = new("e8c34aff");  // #e8c34a
    private static readonly Color ProgressBg    = new("1a1108ff");  // 暗一档的深褐

    private Label? _titleLabel;
    private ColorRect? _progressFill;
    private string _title = "";
    private float _progress = 0f;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, BarHeight);
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        // 自管 StyleBox（半透明深褐底 — §4.5 羊皮底之外唯一破例）
        var sb = new StyleBoxFlat
        {
            BgColor = OverlayBg,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusBottomLeft = 0,
        };
        AddThemeStyleboxOverride("panel", sb);

        // HBox 容器
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(hbox);

        // 左: 标题 Label
        _titleLabel = new Label { Text = _title };
        _titleLabel.AddThemeColorOverride("font_color", OverlayTitle);
        _titleLabel.AddThemeFontSizeOverride("font_size", 13);
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        hbox.AddChild(_titleLabel);

        // 右: 进度条 PanelContainer (固定 200×4, 内部 ColorRect 填充宽度)
        var progressContainer = new PanelContainer
        {
            CustomMinimumSize = new Vector2(200, 4),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var progressSb = new StyleBoxFlat
        {
            BgColor = ProgressBg,
            BorderWidthLeft = 0, BorderWidthTop = 0, BorderWidthRight = 0, BorderWidthBottom = 0,
            CornerRadiusTopLeft = 0, CornerRadiusTopRight = 0, CornerRadiusBottomRight = 0, CornerRadiusBottomLeft = 0,
        };
        progressContainer.AddThemeStyleboxOverride("panel", progressSb);
        hbox.AddChild(progressContainer);

        _progressFill = new ColorRect
        {
            Color = ProgressFill,
            CustomMinimumSize = new Vector2(0, 4),
            SizeFlagsHorizontal = SizeFlags.Fill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        progressContainer.AddChild(_progressFill);

        // 右: 进度数字 Label
        var percentLabel = new Label { Text = "0%" };
        percentLabel.Name = "PercentLabel";
        percentLabel.AddThemeColorOverride("font_color", OverlayTitle);
        percentLabel.AddThemeFontSizeOverride("font_size", 13);
        percentLabel.MouseFilter = MouseFilterEnum.Ignore;
        hbox.AddChild(percentLabel);
        _percentLabel = percentLabel;

        // 默认隐藏（仅 replay 模式显示）
        Visible = false;

        // 触发首次绘制
        ApplyTitle();
        ApplyProgress();
    }

    private Label? _percentLabel;

    /// <summary>设置地图标题（左侧文字）。</summary>
    public void SetTitle(string title)
    {
        _title = title;
        ApplyTitle();
    }

    /// <summary>设置进度 0.0~1.0（M3+ 接时间轴真实值；v0.1 由 EditorController 模拟）。</summary>
    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp(progress, 0f, 1f);
        ApplyProgress();
    }

    private void ApplyTitle()
    {
        if (_titleLabel is not null)
        {
            _titleLabel.Text = _title;
        }
    }

    private void ApplyProgress()
    {
        if (_progressFill is not null)
        {
            // 进度条宽度按百分比 0~200 px（progressContainer 宽 200）
            var width = Mathf.Round(200f * _progress);
            _progressFill.CustomMinimumSize = new Vector2(width, 4);
            _progressFill.SizeFlagsHorizontal = width <= 0 ? SizeFlags.ShrinkCenter : SizeFlags.Fill;
        }
        if (_percentLabel is not null)
        {
            _percentLabel.Text = $"{Mathf.RoundToInt(_progress * 100f)}%";
        }
    }
}
