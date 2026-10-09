using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Render;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 小地图（spec §3.4 / §4.3 / tasks.md T13）：BottomPalette <c>build_row</c> 的最左 section。
///
/// <para>固定 <c>size_mode: fixed</c>，所在 section 的宽度由 <see cref="PaletteSection"/> 的
/// <c>SquareAspect</c> 跟随行高；本类再把整幅地图按<b>地图自身宽高比</b>等比放入该方形区域并居中
/// （spec §3.4 标注 104×104 为 <c>[待标定]</c>；示例世界是 64×48，拉伸会把地图压成竖长条）。</para>
///
/// <para><b>本类不改 WorldCanvas</b>：spec §6 line 555 规定 <c>scripts/Render/**</c> 本轮只允许
/// 新增 <c>TerrainPalette.cs</c>。相机一律通过 <c>GetViewport().GetCamera2D()</c> 获取
/// （WorldCanvas 在 <c>FitCameraToMap()</c> 里调过 <c>MakeCurrent()</c>），
/// 交互只改相机 transform，不写任何地图数据。</para>
///
/// <para>像素配色来自 <see cref="TerrainPalette"/>（其真源是
/// <c>Domain.TerrainDefaults.ColorHex</c>），**本类不另立调色板**。</para>
/// </summary>
public partial class MinimapSection : PaletteContent
{
    /// <summary>主视图一格的世界像素数（= <c>TerrainTileSetFactory.TilePixels</c>）。</summary>
    private const float CellPixels = 32f;

    /// <summary>上下留白合计（spec §3.4：104 = 152 − 48）。</summary>
    private const float VerticalPadding = 48f;

    private TextureRect? _view;
    private Panel? _viewportBox;
    private ImageTexture? _texture;
    private bool _dirty = true;
    private bool _bound;
    private bool _dragging;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        CustomMinimumSize = new Vector2(48f, 48f);

        _view = new TextureRect
        {
            Name = "View",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _view.SetAnchorsPreset(LayoutPreset.TopLeft);
        AddChild(_view);

        _viewportBox = new Panel { Name = "Viewport", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        // §4.9.7 契约: 视口框 #c8341a 2px (--jrpg-danger, 1px 描边 + 半透明填充)
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.784f, 0.204f, 0.102f, 0.16f),    // #c8341a @ 16% alpha
            BorderColor = new Color(0.784f, 0.204f, 0.102f, 1f),    // #c8341a full
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(2);
        _viewportBox.AddThemeStyleboxOverride("panel", style);
        _viewportBox.SetAnchorsPreset(LayoutPreset.TopLeft);
        AddChild(_viewportBox);

        Resized += OnResized;
        LayoutView();
        SetProcess(true);
        _dirty = true;
    }

    // ------------------------------------------------------------------ 布局

    private void OnResized()
    {
        LayoutView();
        UpdateViewportBox();
    }

    /// <summary>把地图按自身宽高比等比放进可用方形区域并居中。</summary>
    private void LayoutView()
    {
        if (_view is null)
        {
            return;
        }

        var availW = Mathf.Max(1f, Size.X);
        var availH = Mathf.Max(1f, Size.Y - VerticalPadding);
        var map = Context?.ActiveMap;

        float width;
        float height;
        if (map is null || map.Grid.Height <= 0)
        {
            width = Mathf.Min(availW, availH);
            height = width;
        }
        else
        {
            var aspect = map.Grid.Width / (float)map.Grid.Height;
            height = availH;
            width = height * aspect;
            if (width > availW)
            {
                width = availW;
                height = width / aspect;
            }
        }

        _view.Size = new Vector2(width, height);
        _view.Position = new Vector2((Size.X - width) * 0.5f, (Size.Y - height) * 0.5f);
    }

    // ------------------------------------------------------------------ 交互

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton button || button.ButtonIndex != MouseButton.Left)
        {
            if (@event is InputEventMouseMotion motion && _dragging &&
                (motion.ButtonMask & MouseButtonMask.Left) != 0)
            {
                FocusAt(motion.Position);
                AcceptEvent();
            }

            return;
        }

        _dragging = button.Pressed;
        if (button.Pressed)
        {
            FocusAt(button.Position);
        }

        AcceptEvent();
    }

    /// <summary>把主视图中心移到小地图上的 <paramref name="local"/> 处（只改相机，不写数据）。</summary>
    private void FocusAt(Vector2 local)
    {
        var camera = GetViewport()?.GetCamera2D();
        var map = Context?.ActiveMap;
        if (camera is null || map is null || _view is null || _view.Size.X <= 1f || _view.Size.Y <= 1f)
        {
            return;
        }

        var inView = local - _view.Position;
        camera.Position = new Vector2(
            inView.X / _view.Size.X * map.Grid.Width * CellPixels,
            inView.Y / _view.Size.Y * map.Grid.Height * CellPixels);
        UpdateViewportBox();
    }

    // ------------------------------------------------------------------ 每帧

    public override void _Process(double delta)
    {
        var context = EditorContext.Instance;
        if (context is null)
        {
            return;
        }

        if (!_bound)
        {
            _bound = true;
            context.MapChanged += OnMapChanged;
            _dirty = true;
        }

        if (_dirty)
        {
            _dirty = false;
            RebuildTexture();
            LayoutView();
        }

        // 相机可能被键盘平移 / 滚轮缩放改动，视口框每帧跟随（开销极小）。
        UpdateViewportBox();
    }

    private void OnMapChanged()
    {
        // 标脏，下一帧重建（spec D20：编辑后 ≤200ms 刷新）。
        _dirty = true;
    }

    // ------------------------------------------------------------------ 纹理

    private void RebuildTexture()
    {
        var map = Context?.ActiveMap;
        if (_view is null)
        {
            return;
        }

        if (map is null)
        {
            _view.Texture = null;
            _texture?.Dispose();
            _texture = null;
            return;
        }

        var width = Mathf.Max(1, map.Grid.Width);
        var height = Mathf.Max(1, map.Grid.Height);

        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(TerrainPalette.ColorOf(map.TerrainData?.Default?.Type ?? TerrainType.Plain));

        if (map.TerrainData?.Cells is { } cells)
        {
            foreach (var cell in cells)
            {
                var x = cell.At.X;
                var y = cell.At.Y;
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    continue;
                }

                image.SetPixel(x, y, TerrainPalette.ColorOf(cell.Type));
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        _view.Texture = texture;
        _texture?.Dispose();
        _texture = texture;
    }

    // ------------------------------------------------------------------ 视口框

    private void UpdateViewportBox()
    {
        if (_viewportBox is null || _view is null)
        {
            return;
        }

        var camera = GetViewport()?.GetCamera2D();
        var map = Context?.ActiveMap;
        if (camera is null || map is null || _view.Size.X <= 1f || _view.Size.Y <= 1f)
        {
            _viewportBox.Visible = false;
            return;
        }

        var visibleWorld = GetViewportRect().Size / camera.Zoom;
        var visibleCells = visibleWorld / CellPixels;

        var boxWidth = Mathf.Clamp(visibleCells.X / map.Grid.Width * _view.Size.X, 2f, _view.Size.X);
        var boxHeight = Mathf.Clamp(visibleCells.Y / map.Grid.Height * _view.Size.Y, 2f, _view.Size.Y);

        var centerCells = camera.Position / CellPixels;
        var centerX = centerCells.X / map.Grid.Width * _view.Size.X;
        var centerY = centerCells.Y / map.Grid.Height * _view.Size.Y;

        _viewportBox.Visible = true;
        _viewportBox.Size = new Vector2(boxWidth, boxHeight);
        _viewportBox.Position = _view.Position + new Vector2(centerX - (boxWidth * 0.5f), centerY - (boxHeight * 0.5f));
    }
}
