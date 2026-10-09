using Godot;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 比例尺 + 坐标显示（§4.9.7 契约 比例尺 / 坐标显示）
///
/// <para>画布右下角显示：坐标（x,y）/ 比例尺 60px 横线 + 两端竖线 + 中点刻度 + 下方文字。
/// 文字 12px 衬线 #3a2810（JrpgText），分隔距离 4px。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>容器：VBoxContainer，8px 距画布右下</item>
///   <item>坐标行：12px 衬线居右，格式 <c>x,y</c>，JrpgText</item>
///   <item>比例尺横线：60px 宽，2px 高，outline #5a3a1a 实色</item>
///   <item>比例尺两端竖线：4px 高，2px 宽</item>
///   <item>比例尺中点刻度：4px 高，1px 宽</item>
///   <item>比例尺下方文字：12px 衬线居中，<c>{距离}{单位}</c>，JrpgText</item>
/// </list>
///
/// <para>API：</para>
/// <list type="bullet">
///   <item><see cref="SetCell"/>：传当前鼠标格坐标 (x, y)</item>
///   <item><see cref="SetDistance"/>：传当前地图比例尺距离与单位（按 cellDistance 推导）</item>
/// </list>
/// </summary>
public partial class JrpgScaleBar : VBoxContainer
{
    /// <summary>JRPG 比例尺 / 坐标显示字号（与 §4.5 UI 区分：12px 用于辅助 UI，§4.5 是 14px+）。</summary>
    public const int DefaultFontSize = 12;

    /// <summary>横线宽（§4.9.7 契约）。</summary>
    public const int BarWidth = 60;

    /// <summary>横线高。2px 与 §4.5 描边一致。</summary>
    public const int BarHeight = 2;

    /// <summary>两端竖线高度。4px 让端点突出。</summary>
    public const int EndCapHeight = 4;

    /// <summary>中点刻度高度。3px 短于端点。</summary>
    public const int MidTickHeight = 3;

    private readonly Label _coordLabel = new();
    private readonly Control _barArea = new();
    private readonly Label _distanceLabel = new();

    private int _cellX;
    private int _cellY;
    private string _distanceText = "—";

    /// <summary>JRPG 风格：自动加载 theme 并构建坐标 + 比例尺 + 距离文字三段式布局。</summary>
    public JrpgScaleBar()
    {
        ApplyJrpgTheme();
        BuildLayout();
    }

    private void BuildLayout()
    {
        Alignment = BoxContainer.AlignmentMode.End;
        AddThemeConstantOverride("separation", 4);

        // 坐标行
        _coordLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _coordLabel.VerticalAlignment = VerticalAlignment.Center;
        _coordLabel.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        _coordLabel.AddThemeColorOverride("font_color", JrpgStyleFactory.JrpgText);
        _coordLabel.Text = "—,—";
        AddChild(_coordLabel);

        // 比例尺容器:固定 60x6 区域（横线 + 两端竖线 + 中点刻度），
        // 用 _Draw 直接绘制以保证像素对齐
        _barArea.CustomMinimumSize = new Vector2(BarWidth, EndCapHeight);
        _barArea.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _barArea.MouseFilter = MouseFilterEnum.Ignore;
        _barArea.Draw += DrawScaleBar;
        AddChild(_barArea);

        // 距离文字
        _distanceLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _distanceLabel.VerticalAlignment = VerticalAlignment.Center;
        _distanceLabel.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        _distanceLabel.AddThemeColorOverride("font_color", JrpgStyleFactory.JrpgText);
        _distanceLabel.Text = "—";
        AddChild(_distanceLabel);
    }

    /// <summary>语义化：设置当前鼠标格坐标（外部每帧更新）。</summary>
    public JrpgScaleBar SetCell(int x, int y)
    {
        _cellX = x;
        _cellY = y;
        _coordLabel.Text = $"{x},{y}";
        return this;
    }

    /// <summary>语义化：设置比例尺距离文字（按 cellDistance 推导,如 "10 km"）。</summary>
    public JrpgScaleBar SetDistance(string text)
    {
        _distanceText = text;
        _distanceLabel.Text = text;
        return this;
    }

    private void DrawScaleBar()
    {
        var outline = JrpgStyleFactory.JrpgOutline;
        // 1. 横线：60x2px 实心矩形
        var lineRect = new Rect2(0, EndCapHeight - BarHeight, BarWidth, BarHeight);
        _barArea.DrawRect(lineRect, outline, filled: true);

        // 2. 两端竖线：左 (0,0)-(2,4)  右 (58,0)-(2,4)
        _barArea.DrawRect(new Rect2(0, 0, BarHeight, EndCapHeight), outline, filled: true);
        _barArea.DrawRect(new Rect2(BarWidth - BarHeight, 0, BarHeight, EndCapHeight), outline, filled: true);

        // 3. 中点刻度：(29,1)-(2,3) 短
        _barArea.DrawRect(new Rect2(BarWidth / 2 - 1, EndCapHeight - MidTickHeight, 2, MidTickHeight), outline, filled: true);
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
    }
}
