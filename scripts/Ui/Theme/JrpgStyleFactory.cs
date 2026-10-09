using Godot;

namespace WorldView.Ui.Theme;

/// <summary>
/// JRPG StyleBoxFlat 工厂(§4.5 配套基础设施)
///
/// <para>集中创建 <c>StyleBoxFlat</c>,所有色值严格走
/// <c>assets/palettes/parchment_palette.yaml:ui.*</c>(§4.5.1),由
/// <c>WorldView.Generator.PaletteGenerator</c> 编译期生成 <c>Palette.Color.*</c>
/// / <c>Palette.Metric.*</c> / <c>Palette.Shadow.*</c> 静态字段。
/// 本类不再持有任何硬编码 hex,所有色取自 <see cref="Palette"/>。</para>
///
/// <para>为何不全部走 .tres 主题?</para>
/// <list type="bullet">
///   <item>动态场景(信息卡势力色标题条、运行时 Panel 状态)需要按参数构造</item>
///   <item>静态控件(Button / List / TextBox)已走 parchment_theme.tres 完整定义</item>
///   <item>工厂类只覆盖需要动态色/尺寸的场景,避免重复</item>
/// </list>
/// </summary>
public static class JrpgStyleFactory
{
    // ===== 通用工厂方法 =====

    /// <summary>Panel 样式(§4.5.2 Panel 行):2px 焦褐描边 + 阴影偏移</summary>
    public static StyleBoxFlat PanelStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgCard,
            BorderColor = Palette.Color.JrpgOutline,
        };
        s.SetBorderWidthAll((int)Palette.Metric.BorderWidth);
        s.SetCornerRadiusAll((int)Palette.Metric.CornerRadius);
        var o = Palette.Color.JrpgOutline;
        s.ShadowColor = new Color(o.R, o.G, o.B, Palette.Shadow.Opacity);
        s.ShadowOffset = new Vector2(Palette.Shadow.OffsetX, Palette.Shadow.OffsetY);
        s.ShadowSize = (int)Palette.Shadow.Size; // ≤ 2px 满足 §4.5.4 + 拒绝清单 #2
        return s;
    }

    /// <summary>Panel 标题条样式(§4.5.2 + §4.9.6):24px 高 + 势力色 / fill_pressed</summary>
    public static StyleBoxFlat TitleBarStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgFillPressed,
            BorderColor = Palette.Color.JrpgOutline,
            CornerRadiusTopLeft = (int)Palette.Metric.CornerRadius,
            CornerRadiusTopRight = (int)Palette.Metric.CornerRadius,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
        s.BorderWidthBottom = (int)Palette.Metric.BorderWidth;
        return s;
    }

    /// <summary>按钮 normal 态(§4.5.2 Button normal)</summary>
    public static StyleBoxFlat ButtonNormalStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgFill,
            BorderColor = Palette.Color.JrpgOutline,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>按钮 hover 态(§4.5.2 Button hover)</summary>
    public static StyleBoxFlat ButtonHoverStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgFillHover,
            BorderColor = Palette.Color.JrpgOutline,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>按钮 pressed 态(§4.5.2 Button pressed,内移 1px 营造"陷入")</summary>
    public static StyleBoxFlat ButtonPressedStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgFillPressed,
            BorderColor = Palette.Color.JrpgOutline,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>按钮 focus 态(§4.5.2 + §4.5.5 #12:金黄描边**替换**焦褐,**不**叠加)</summary>
    public static StyleBoxFlat ButtonFocusStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgFill,
            BorderColor = Palette.Color.JrpgOutlineFocus,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>按钮 disabled 态(§4.5.2 Button disabled,shadow 透明度复用)</summary>
    public static StyleBoxFlat ButtonDisabledStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        var disabledAlpha = 0.6f;
        var bg = Palette.Color.JrpgFill;
        var border = Palette.Color.JrpgOutline;
        return new StyleBoxFlat
        {
            BgColor = new Color(bg.R, bg.G, bg.B, Palette.Shadow.Opacity),
            BorderColor = new Color(border.R, border.G, border.B, disabledAlpha),
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>输入框 normal 态(§4.5.2 TextBox)</summary>
    public static StyleBoxFlat TextBoxNormalStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgCard,
            BorderColor = Palette.Color.JrpgOutline,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>输入框 focus 态(金黄描边替换)</summary>
    public static StyleBoxFlat TextBoxFocusStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgCard,
            BorderColor = Palette.Color.JrpgOutlineFocus,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>列表项选中态(§4.5.2 List selected)</summary>
    public static StyleBoxFlat ListItemSelectedStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        var r = (int)Palette.Metric.CornerRadius;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgFillPressed,
            BorderColor = Palette.Color.JrpgOutlineFocus,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }

    /// <summary>复选框 unchecked(§4.5.2 CheckBox,圆角 0)</summary>
    public static StyleBoxFlat CheckBoxUncheckedStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgCard,
            BorderColor = Palette.Color.JrpgOutline,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
    }

    /// <summary>复选框 checked(§4.5.2 CheckBox,金黄底 + 焦褐描边)</summary>
    public static StyleBoxFlat CheckBoxCheckedStyle()
    {
        var w = (int)Palette.Metric.BorderWidth;
        return new StyleBoxFlat
        {
            BgColor = Palette.Color.JrpgOutlineFocus,
            BorderColor = Palette.Color.JrpgOutline,
            BorderWidthLeft = w,
            BorderWidthTop = w,
            BorderWidthRight = w,
            BorderWidthBottom = w,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
    }
}
