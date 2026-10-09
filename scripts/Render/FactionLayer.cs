// scripts/Render/FactionLayer.cs
//
// §4.9.4 势力视觉（v0.1 MVP）。
//
// 完整 5 元素（疆界 / 热力图 / 争议区 / 停战线 / 据点）见
// docs/美术资源规范_v0.1.md §4.9.4 + docs/图层系统规格_v0.1.md「势力图层」；
// Faction 实体登记 / 时序生效 / 跨图引用 走 M5 路线。
// v0.1 仅实装"势力色圆点"——把 faction_red/blue 两色钉在指定 cell 上画 8px 实心点，
// 给主流程一个可走的渲染入口；势力数据从硬编码常量读，后续接 MapDoc.FactionRefs。
//
// 渲染实现：自管 _Draw()；CoordTransform 是静态工具类,直接用 CellToWorld(cell, cellDistance)。
// 接入：WorldCanvas.ConfigureAll 调 Configure(cellDistance) 注入距离；ModeChanged 切显隐。

using Godot;
using WorldView.Editor;

namespace WorldView.Render;

public partial class FactionLayer : Node2D
{
    // 6 势力色 — 与 jrpg_palette.yaml:24-25 字段 (faction_red/blue) 平行；
    // 4 个扩展色从 palettable JRPG 暗色扩域派生（30% 亮度、20% 饱和度），M5 接 factions.yaml 再校。
    private static readonly Color[] FactionColors =
    {
        new("c8341aff"), // red     #c8341a (jrpg_danger)
        new("3a5a8aff"), // blue    #3a5a8a (jrpg_link)
        new("5a4a1aff"), // amber   #5a4a1a
        new("3a5a3aff"), // green   #3a5a3a
        new("6a3a5aff"), // purple  #6a3a5a
        new("5a3a5aff"), // violet  #5a3a5a
    };

    private float _cellDistance = 32f;
    private bool _visibleByMode = true;
    private bool _contextBound;

    public override void _Ready()
    {
        ZIndex = 6; // 在 MarkerLayer(5) 之上，UI 之下
        TryBindContext();
    }

    public void Configure(float cellDistance)
    {
        _cellDistance = cellDistance;
        QueueRedraw();
    }

    private bool TryBindContext()
    {
        if (_contextBound) return true;
        var context = EditorContext.Instance;
        if (context is null) return false;
        context.ModeChanged += OnModeChanged;
        ApplyMode(context.Mode);
        _contextBound = true;
        return true;
    }

    private void OnModeChanged(string mode) => ApplyMode(mode);

    private void ApplyMode(string mode)
    {
        // 势力图只保留在 edit / preview 模式（演示/回放不显）
        _visibleByMode = mode is "edit" or "preview";
        Visible = _visibleByMode;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_visibleByMode) return;
        // v0.1 demo 数据：2 个固定势力圆点（艾瑟兰大陆 64×48 中心区）。
        // M5 接 factions.yaml 后改 _activeFactions 列表驱动。
        DrawFactionDot(0, new Vector2I(28, 20));
        DrawFactionDot(1, new Vector2I(34, 24));
    }

    private void DrawFactionDot(int factionIndex, Vector2I cell)
    {
        var color = FactionColors[factionIndex % FactionColors.Length];
        var world = CoordTransform.CellToWorld(cell, _cellDistance);
        // 8px 实心点 + 1px 深褐描边（与 marker 一致层级）
        DrawCircle(world, 4f, color);
        DrawArc(world, 4f, 0f, Mathf.Tau, 12, new Color("3a2810ff"), 1f);
    }

    public override void _ExitTree()
    {
        var context = EditorContext.Instance;
        if (context is not null)
        {
            context.ModeChanged -= OnModeChanged;
            _contextBound = false;
        }
    }
}
