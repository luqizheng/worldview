using Godot;
using WorldView.Ui.Dock;

namespace WorldView.Ui.Palette;

/// <summary>
/// 事件播放条（spec §3.3 / tasks.md T14）：BottomPalette 的 <c>row 0</c>。
///
/// <para><b>本轮只交付「常态空骨架」</b>：播放/暂停按钮（仅视觉）、时间文本占位、
/// 空进度轨 + 播放头、展开箭头。时间轴数据模型、刻度、挂载、回放、快照全部属 M6
/// （spec §9 N5），因此这里**不读取任何时间数据**，时间文本是固定占位字面量。</para>
///
/// <para>常态 28px；展开 56px（用 <c>T</c> 或在行内点箭头）→ 追加「事件刻度 / 快照标记 /
/// 时间粒度」占位区。展开高度由 <see cref="PaletteRow.SetExpanded"/> 驱动，本组件只负责
/// 自己的视觉切换（通过 <see cref="PaletteRow.ExpandedChanged"/> 同步）。</para>
/// </summary>
public partial class TimelineStrip : PaletteContent
{
    /// <summary>时间文本占位（editor context 无时间模型，spec §9 N5）。</summary>
    private const string TimePlaceholder = "创世历 1042-3-12 辰时";

    private HBoxContainer? _main;
    private Button? _playButton;
    private Label? _timeLabel;
    private Control? _track;
    private Panel? _trackBg;
    private ColorRect? _playhead;
    private Button? _expandButton;
    private Label? _zoomLabel;
    private Control? _extra;

    private bool _playing;
    private bool _expanded;
    private bool _rowBound;
    private bool _syncing;
    private float _progress = 0.32f;

    public override void _Ready()
    {
        base._Ready();

        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
        CustomMinimumSize = new Vector2(0f, 24f);

        BuildUi();
        SetExpandedLocal(false);
        SetProcess(true);
    }

    // ------------------------------------------------------------------ 构建

    private void BuildUi()
    {
        _main = new HBoxContainer { Name = "Main" };
        _main.AddThemeConstantOverride("separation", 6);
        AddChild(_main);
        _main.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _main.OffsetLeft = 4f;
        _main.OffsetTop = 2f;
        _main.OffsetRight = -6f;
        _main.OffsetBottom = -2f;

        _playButton = new Button
        {
            Name = "Play",
            Text = "▶",
            Flat = true,
            TooltipText = "播放 / 暂停（回放逻辑属 M6，本轮仅视觉）",
        };
        _playButton.AddThemeFontSizeOverride("font_size", 11);
        _playButton.CustomMinimumSize = new Vector2(22f, 22f);
        _playButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _playButton.Pressed += TogglePlay;
        _main.AddChild(_playButton);

        _timeLabel = new Label
        {
            Name = "Time",
            Text = TimePlaceholder,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _timeLabel.AddThemeFontSizeOverride("font_size", 11);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.90f, 0.90f));
        _main.AddChild(_timeLabel);

        BuildTrack();

        _zoomLabel = new Label
        {
            Name = "Zoom",
            Text = "年 ── 时",
            VerticalAlignment = VerticalAlignment.Center,
        };
        _zoomLabel.AddThemeFontSizeOverride("font_size", 10);
        _zoomLabel.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
        _main.AddChild(_zoomLabel);

        _expandButton = new Button
        {
            Name = "Expand",
            Text = "▾",
            Flat = true,
            TooltipText = "展开 / 收起播放条 (T)",
        };
        _expandButton.AddThemeFontSizeOverride("font_size", 11);
        _expandButton.CustomMinimumSize = new Vector2(20f, 20f);
        _expandButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _expandButton.Pressed += () => SetExpandedLocal(!_expanded, propagate: true);
        _main.AddChild(_expandButton);

        _extra = new Control { Name = "Extra", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_extra);
        _extra.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _extra.OffsetLeft = 4f;
        _extra.OffsetTop = 26f;
        _extra.OffsetRight = -6f;

        var extraHint = new Label
        {
            Text = "事件刻度 / 快照标记 / 时间粒度（M6）",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        extraHint.AddThemeFontSizeOverride("font_size", 10);
        extraHint.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
        _extra.AddChild(extraHint);
        extraHint.SetAnchorsPreset(LayoutPreset.BottomLeft);
        extraHint.OffsetTop = -16f;
        extraHint.OffsetBottom = -2f;
    }

    private void BuildTrack()
    {
        _track = new Control
        {
            Name = "Track",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(80f, 20f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _main!.AddChild(_track);

        _trackBg = new Panel { Name = "TrackBg", MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = new Color(0.20f, 0.20f, 0.24f, 1f) };
        style.SetCornerRadiusAll(2);
        _trackBg.AddThemeStyleboxOverride("panel", style);
        _track.AddChild(_trackBg);
        _trackBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _trackBg.OffsetTop = 8f;
        _trackBg.OffsetBottom = -8f;

        _playhead = new ColorRect
        {
            Name = "Playhead",
            Color = new Color(0.902f, 0.706f, 0.314f, 1f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _track.AddChild(_playhead);

        _track.Resized += LayoutPlayhead;
    }

    private void LayoutPlayhead()
    {
        if (_track is null || _playhead is null)
        {
            return;
        }

        var width = _track.Size.X;
        var x = Mathf.Clamp(_progress * width - 1f, 0f, Mathf.Max(0f, width - 2f));
        _playhead.Position = new Vector2(x, 3f);
        _playhead.Size = new Vector2(2f, Mathf.Max(8f, _track.Size.Y - 6f));
    }

    private void TogglePlay()
    {
        _playing = !_playing;
        if (_playButton is not null)
        {
            _playButton.Text = _playing ? "⏸" : "▶";
        }
    }

    // ------------------------------------------------------------------ 展开态

    private void SetExpandedLocal(bool expanded, bool propagate = false)
    {
        _expanded = expanded;

        if (_extra is not null)
        {
            _extra.Visible = expanded;
        }

        if (_expandButton is not null)
        {
            _expandButton.Text = expanded ? "▴" : "▾";
        }

        if (propagate && Row is { } row && row.Expanded != expanded)
        {
            _syncing = true;
            row.SetExpanded(expanded);
            _syncing = false;
        }

        QueueRedraw();
    }

    private void OnRowExpandedChanged(bool expanded)
    {
        if (_syncing)
        {
            return;
        }

        SetExpandedLocal(expanded);
    }

    /// <summary>等待自己被挂进 PaletteRow 后再订阅展开态（父节点可能尚未建立）。</summary>
    public override void _Process(double delta)
    {
        if (_rowBound)
        {
            return;
        }

        if (Row is { } row)
        {
            _rowBound = true;
            row.ExpandedChanged += OnRowExpandedChanged;
            SetExpandedLocal(row.Expanded);
            SetProcess(false);
        }
    }

    // ------------------------------------------------------------------ 布局状态

    public override void ApplyState(PanelState? state)
    {
        SetExpandedLocal(state?.Expanded ?? Row?.Expanded ?? false);
    }

    // ------------------------------------------------------------------ 绘制（事件刻度占位）

    public override void _Draw()
    {
        if (!_expanded || Size.X <= 0f)
        {
            return;
        }

        // 展开态的「事件刻度」占位：等距刻线 + 两个快照标记三角。
        var top = 30f;
        var bottom = Mathf.Max(top + 4f, Size.Y - 22f);
        var color = new Color(0.30f, 0.30f, 0.36f, 1f);
        var accent = new Color(0.45f, 0.55f, 0.85f, 1f);

        const int ticks = 12;
        for (var i = 1; i < ticks; i++)
        {
            var x = Size.X * i / ticks;
            DrawLine(new Vector2(x, top), new Vector2(x, bottom), color, 1f);
        }

        DrawLine(new Vector2(0f, bottom), new Vector2(Size.X, bottom), color, 1f);

        var snapshotA = Size.X * 0.25f;
        var snapshotB = Size.X * 0.68f;
        DrawLine(new Vector2(snapshotA, top - 6f), new Vector2(snapshotA, bottom), accent, 1f);
        DrawLine(new Vector2(snapshotB, top - 6f), new Vector2(snapshotB, bottom), accent, 1f);
    }

    // ------------------------------------------------------------------ 快捷键 T

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.T && !key.CtrlPressed && !key.ShiftPressed && !key.AltPressed)
        {
            SetExpandedLocal(!_expanded, propagate: true);
            GetViewport().SetInputAsHandled();
        }
    }
}
