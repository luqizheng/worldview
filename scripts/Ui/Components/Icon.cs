using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用 Icon（§4.5.2 契约 CloseButton 内嵌 / 跨组件复用）
///
/// <para>不依赖 PNG / SVG,全部代码绘制。6 种形态 × 3 档尺寸:</para>
/// <list type="bullet">
///   <item><b>形态</b>：<see cref="Shape"/> 枚举:Close / Check / Cross / Info / Warning / Dot</item>
///   <item><b>尺寸</b>：<see cref="SizeTier"/> 枚举:Xs(16) / Sm(24) / Md(32),数值走
///         <c>Palette.Metric.IconSize*</c></item>
/// </list>
///
/// <para>颜色走 <c>Palette.Color.JrpgText</c>(默认),调用方可用 <see cref="SetColor"/> 覆盖
/// (CloseButton 把它设成 <c>JrpgTextPlaceholder</c>)。</para>
///
/// <para>命名说明：叫 <c>JrpgIcon</c> 而非 <c>Icon</c>,避免与 Godot <c>Button.Icon</c>
/// (Texture2D 属性) 类型解析冲突。</para>
///
/// <para>绘制策略：每个形态对应一个 <see cref="DrawShape"/> 分支,基于 Size 计算
/// 归一化坐标(0..1)再乘实际 px,保持缩放一致性。</para>
/// </summary>
public partial class JrpgIcon : Control
{
    /// <summary>Icon 形态(6 种,§4.5.2)。</summary>
    public enum Shape
    {
        Close,   // X 形关闭(对话框右上 / 浮窗关闭按钮)
        Check,   // 对勾(校验通过 / 确认)
        Cross,   // 叉号(危险 / 拒绝)
        Info,    // i 形(信息提示)
        Warning, // 三角感叹号(警告)
        Dot,     // 实心圆(占位 / 选中指示)
    }

    /// <summary>Icon 尺寸档(3 档,§4.5.2 量化层)。</summary>
    public enum SizeTier
    {
        Xs, // 16px
        Sm, // 24px
        Md, // 32px
    }

    private Shape _shape = Shape.Close;
    private SizeTier _tier = SizeTier.Sm;
    private Color _color = WorldView.Ui.Theme.Palette.Color.JrpgText;

    /// <summary>当前形态。</summary>
    public Shape CurrentShape
    {
        get => _shape;
        set { _shape = value; QueueRedraw(); }
    }

    /// <summary>当前尺寸档。</summary>
    public SizeTier CurrentSize
    {
        get => _tier;
        set
        {
            _tier = value;
            var px = ResolveSizePx(value);
            CustomMinimumSize = new Vector2(px, px);
            Size = new Vector2I(px, px);
            QueueRedraw();
        }
    }

    /// <summary>设置形态(语义化)。</summary>
    public JrpgIcon SetShape(Shape shape)
    {
        CurrentShape = shape;
        return this;
    }

    /// <summary>设置尺寸档(语义化)。</summary>
    public JrpgIcon SetSizeTier(SizeTier tier)
    {
        CurrentSize = tier;
        return this;
    }

    /// <summary>设置颜色(覆盖默认 JrpgText)。</summary>
    public JrpgIcon SetColor(Color color)
    {
        _color = color;
        QueueRedraw();
        return this;
    }

    /// <inheritdoc/>
    public JrpgIcon()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CurrentSize = SizeTier.Sm;
    }

    private static int ResolveSizePx(SizeTier tier) => tier switch
    {
        SizeTier.Xs => WorldView.Ui.Theme.Palette.Metric.IconSizeXs,
        SizeTier.Sm => WorldView.Ui.Theme.Palette.Metric.IconSizeSm,
        SizeTier.Md => WorldView.Ui.Theme.Palette.Metric.IconSizeMd,
        _ => WorldView.Ui.Theme.Palette.Metric.IconSizeSm,
    };

    /// <inheritdoc/>
    public override void _Draw()
    {
        var px = (float)ResolveSizePx(_tier);
        if (px <= 0f) return;

        // 笔画粗细按尺寸缩放:Xs 1.5,Sm 2,Md 2.5(视觉权重平衡)
        var lineWidth = _tier switch
        {
            SizeTier.Xs => 1.5f,
            SizeTier.Sm => 2.0f,
            SizeTier.Md => 2.5f,
            _ => 2.0f,
        };

        var rect = new Rect2(0f, 0f, px, px);
        DrawShape(rect, _shape, _color, lineWidth);
    }

    private void DrawShape(Rect2 rect, Shape shape, Color color, float lineWidth)
    {
        // 内边距 12.5%,让图形不贴边(视觉密度更舒服)
        var pad = rect.Size.X * 0.125f;
        var inner = new Rect2(
            rect.Position.X + pad,
            rect.Position.Y + pad,
            rect.Size.X - 2 * pad,
            rect.Size.Y - 2 * pad);

        switch (shape)
        {
            case Shape.Close:
            {
                // X 形:左上→右下 + 右上→左下
                DrawLine(inner.Position, inner.Position + inner.Size, color, lineWidth);
                DrawLine(new Vector2(inner.Position.X + inner.Size.X, inner.Position.Y),
                         new Vector2(inner.Position.X, inner.Position.Y + inner.Size.Y),
                         color, lineWidth);
                break;
            }
            case Shape.Check:
            {
                // 对勾:左下点 → 拐点(底部 60% 处) → 右上角
                var p1 = new Vector2(inner.Position.X, inner.Position.Y + inner.Size.Y * 0.55f);
                var p2 = new Vector2(inner.Position.X + inner.Size.X * 0.4f, inner.Position.Y + inner.Size.Y);
                var p3 = new Vector2(inner.Position.X + inner.Size.X, inner.Position.Y);
                DrawLine(p1, p2, color, lineWidth);
                DrawLine(p2, p3, color, lineWidth);
                break;
            }
            case Shape.Cross:
            {
                // + 形:水平中线 + 垂直中线(长度 = inner 宽度)
                var cx = inner.Position.X + inner.Size.X * 0.5f;
                var cy = inner.Position.Y + inner.Size.Y * 0.5f;
                DrawLine(new Vector2(inner.Position.X, cy),
                         new Vector2(inner.Position.X + inner.Size.X, cy), color, lineWidth);
                DrawLine(new Vector2(cx, inner.Position.Y),
                         new Vector2(cx, inner.Position.Y + inner.Size.Y), color, lineWidth);
                break;
            }
            case Shape.Info:
            {
                // i 形:顶部小圆点 + 下方竖线(用矩形 + 圆模拟)
                var cx = inner.Position.X + inner.Size.X * 0.5f;
                var dotRadius = inner.Size.X * 0.12f;
                DrawCircle(new Vector2(cx, inner.Position.Y + inner.Size.Y * 0.22f), dotRadius, color);
                // 下方竖线用 lineWidth 加粗矩形近似(底部 4/5 高度)
                var stemY = inner.Position.Y + inner.Size.Y * 0.45f;
                var stemH = inner.Size.Y * 0.45f;
                var stemW = lineWidth;
                DrawRect(new Rect2(cx - stemW * 0.5f, stemY, stemW, stemH), color);
                break;
            }
            case Shape.Warning:
            {
                // 三角感叹号:顶角朝上的等边三角形 + 内部小圆点 + 下方短粗竖线
                var apex = new Vector2(inner.Position.X + inner.Size.X * 0.5f, inner.Position.Y);
                var bl = new Vector2(inner.Position.X, inner.Position.Y + inner.Size.Y);
                var br = new Vector2(inner.Position.X + inner.Size.X, inner.Position.Y + inner.Size.Y);
                DrawLine(apex, bl, color, lineWidth);
                DrawLine(bl, br, color, lineWidth);
                DrawLine(br, apex, color, lineWidth);

                // 中心小圆点(在三角形重心下方)
                var dotRadius = inner.Size.X * 0.10f;
                var centroid = new Vector2(
                    (apex.X + bl.X + br.X) / 3f,
                    (apex.Y + bl.Y + br.Y) / 3f);
                DrawCircle(new Vector2(centroid.X, centroid.Y + inner.Size.Y * 0.10f), dotRadius, color);
                break;
            }
            case Shape.Dot:
            {
                // 实心圆(占位 / 选中指示)
                var center = new Vector2(
                    inner.Position.X + inner.Size.X * 0.5f,
                    inner.Position.Y + inner.Size.Y * 0.5f);
                var radius = inner.Size.X * 0.5f;
                DrawCircle(center, radius, color);
                break;
            }
        }
    }
}
