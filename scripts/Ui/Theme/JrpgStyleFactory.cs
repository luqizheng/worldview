using Godot;

namespace WorldView.Ui.Theme;

/// <summary>
/// JRPG StyleBoxFlat 工厂（§4.5 配套基础设施）
///
/// <para>集中创建 <c>StyleBoxFlat</c>,所有色值严格走
/// <c>assets/palettes/parchment_palette.yaml:ui.*</c> 12 token（§4.5.1）,
/// 不在 C# 代码里硬编码 hex（§4.5.6 单一真源纪律）。</para>
///
/// <para>为何不全部走 .tres 主题？</para>
/// <list type="bullet">
///   <item>动态场景（信息卡势力色标题条、运行时 Panel 状态）需要按参数构造</item>
///   <item>静态控件（Button / List / TextBox）已走 parchment_theme.tres 完整定义</item>
///   <item>工厂类只覆盖需要动态色/尺寸的场景,避免重复</item>
/// </list>
/// </summary>
public static class JrpgStyleFactory
{
    // ===== 12 token（§4.5.1，唯一真源 parchment_palette.yaml:ui.*） =====
    // 用 .FromHtml 转 Color,注释标注 hex 便于回溯

    /// <summary>jrpg_bg = #f4f0e4（窗体 / 主背景）</summary>
    public static Color JrpgBg => Color.FromHtml("#f4f0e4ff");

    /// <summary>jrpg_card = #fff8e7（卡片 / 输入框底）</summary>
    public static Color JrpgCard => Color.FromHtml("#fff8e7ff");

    /// <summary>jrpg_fill = #f4e4c1（控件填充）</summary>
    public static Color JrpgFill => Color.FromHtml("#f4e4c1ff");

    /// <summary>jrpg_fill_pressed = #e8c89a（按下态 + 列表选中 + 标题条）</summary>
    public static Color JrpgFillPressed => Color.FromHtml("#e8c89aff");

    /// <summary>jrpg_outline = #5a3a1a（统一 2px 描边）</summary>
    public static Color JrpgOutline => Color.FromHtml("#5a3a1aff");

    /// <summary>jrpg_outline_focus = #e8c34a（焦点态金黄）</summary>
    public static Color JrpgOutlineFocus => Color.FromHtml("#e8c34aff");

    /// <summary>jrpg_text = #3a2810（正文深褐）</summary>
    public static Color JrpgText => Color.FromHtml("#3a2810ff");

    /// <summary>jrpg_text_placeholder = #a89878（占位符）</summary>
    public static Color JrpgTextPlaceholder => Color.FromHtml("#a89878ff");

    /// <summary>jrpg_text_disabled = #8a7a5a（disabled 文字）</summary>
    public static Color JrpgTextDisabled => Color.FromHtml("#8a7a5aff");

    /// <summary>jrpg_link = #3a5a8a（链接主色）</summary>
    public static Color JrpgLink => Color.FromHtml("#3a5a8aff");

    /// <summary>jrpg_danger = #c8341a（危险 / HP 红）</summary>
    public static Color JrpgDanger => Color.FromHtml("#c8341aff");

    /// <summary>jrpg_success = #5a8a3a（成功 / 任务完成）</summary>
    public static Color JrpgSuccess => Color.FromHtml("#5a8a3aff");

    // ===== 通用工厂方法 =====

    /// <summary>Panel 样式（§4.5.2 Panel 行）：2px 焦褐描边 + 阴影偏移</summary>
    public static StyleBoxFlat PanelStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = JrpgCard,
            BorderColor = JrpgOutline,
        };
        s.SetBorderWidthAll(2);
        s.SetCornerRadiusAll(2);
        s.ShadowColor = new Color(JrpgOutline.R, JrpgOutline.G, JrpgOutline.B, 0.4f);
        s.ShadowOffset = new Vector2(1, 1);
        s.ShadowSize = 2; // ≤ 2px 满足 §4.5.4 + 拒绝清单 #2
        return s;
    }

    /// <summary>Panel 标题条样式（§4.5.2 + §4.9.6）：24px 高 + 势力色 / fill_pressed</summary>
    public static StyleBoxFlat TitleBarStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = JrpgFillPressed,
            BorderColor = JrpgOutline,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
        s.BorderWidthBottom = 2;
        return s;
    }

    /// <summary>按钮 normal 态（§4.5.2 Button normal）</summary>
    public static StyleBoxFlat ButtonNormalStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgFill,
            BorderColor = JrpgOutline,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>按钮 hover 态（§4.5.2 Button hover,#f8ecd0 = jrpg_fill 略提亮）</summary>
    public static StyleBoxFlat ButtonHoverStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.973f, 0.925f, 0.816f, 1f), // #f8ecd0
            BorderColor = JrpgOutline,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>按钮 pressed 态（§4.5.2 Button pressed,内移 1px 营造"陷入"）</summary>
    public static StyleBoxFlat ButtonPressedStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgFillPressed,
            BorderColor = JrpgOutline,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>按钮 focus 态（§4.5.2 + §4.5.5 #12：金黄描边**替换**焦褐，**不**叠加）</summary>
    public static StyleBoxFlat ButtonFocusStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgFill,
            BorderColor = JrpgOutlineFocus,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>按钮 disabled 态（§4.5.2 Button disabled,50% 透明）</summary>
    public static StyleBoxFlat ButtonDisabledStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(JrpgFill.R, JrpgFill.G, JrpgFill.B, 0.4f),
            BorderColor = new Color(JrpgOutline.R, JrpgOutline.G, JrpgOutline.B, 0.6f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>输入框 normal 态（§4.5.2 TextBox）</summary>
    public static StyleBoxFlat TextBoxNormalStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgCard,
            BorderColor = JrpgOutline,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>输入框 focus 态（金黄描边替换）</summary>
    public static StyleBoxFlat TextBoxFocusStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgCard,
            BorderColor = JrpgOutlineFocus,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>列表项选中态（§4.5.2 List selected）</summary>
    public static StyleBoxFlat ListItemSelectedStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgFillPressed,
            BorderColor = JrpgOutlineFocus,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    /// <summary>复选框 unchecked（§4.5.2 CheckBox,圆角 0）</summary>
    public static StyleBoxFlat CheckBoxUncheckedStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgCard,
            BorderColor = JrpgOutline,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
    }

    /// <summary>复选框 checked（§4.5.2 CheckBox,#e8c34a + 1px 黑色对号画在 icon 上）</summary>
    public static StyleBoxFlat CheckBoxCheckedStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = JrpgOutlineFocus,
            BorderColor = JrpgOutline,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
    }
}
