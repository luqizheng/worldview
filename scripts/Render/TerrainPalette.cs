using System;
using System.Collections.Generic;
using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 地形类型 → RGB 的**只读常量表**（spec §6 / tasks.md T13）。
///
/// <para>这是本轮唯一允许触碰 <c>scripts/Render/</c> 的新增文件，理由是避免主视图与小地图
/// 各维护一份地形调色板。本类：不含节点（不继承 Node）、不含渲染逻辑、不写任何状态。</para>
///
/// <para>颜色**不另立**：直接复用 <see cref="TerrainDefaults.ColorHex"/>（Domain 层的唯一真源，
/// 其注释已声明「Render/TerrainPalette.cs 按此映射取帧」），只做一次 hex → <see cref="Color"/>
/// 的预解析并缓存，避免小地图逐格解析字符串。</para>
/// </summary>
public static class TerrainPalette
{
    private static readonly TerrainType[] Known = (TerrainType[])Enum.GetValues(typeof(TerrainType));
    private static readonly Color[] Colors = BuildColors();

    /// <summary>全部地形类型（枚举声明顺序）。</summary>
    public static IReadOnlyList<TerrainType> Types => Known;

    /// <summary>取地形颜色；未知值回退到平原色。</summary>
    public static Color ColorOf(TerrainType type)
    {
        var index = (int)type;
        return index >= 0 && index < Colors.Length ? Colors[index] : Colors[(int)TerrainType.Plain];
    }

    private static Color[] BuildColors()
    {
        var max = 0;
        foreach (var type in Known)
        {
            max = Math.Max(max, (int)type);
        }

        var table = new Color[Math.Max(1, max + 1)];
        foreach (var type in Known)
        {
            table[(int)type] = Color.FromHtml(TerrainDefaults.ColorHex(type));
        }

        return table;
    }
}
