using WorldView.Domain;
using WorldView.Domain.Generation;
using Xunit;

namespace WorldView.Tests.Generation;

/// <summary>
/// 共用海岸线细化 pass 测试（task-4-brief + spec 2 §5）。
///
/// 字段契约（与 brief 对应）：
/// <list type="bullet">
///   <item>陆地 = <c>read(...) != defaultType</c>；水 = 等于 <c>defaultType</c>。</item>
///   <item>扫描盒 = <c>[rangeMin - 1, rangeMax + 1]</c> 与网格求交（重标 coast 多扩 1 格）。</item>
///   <item>步骤顺序：去碎岛 → 填碎海 → 形态学（仅 Morphology &amp;&amp; Erosion &gt; 0）→ 重标 coast。</item>
///   <item>连通性：岛屿/湖泊 = 4 邻接；coast 判定 = 8 邻接。</item>
///   <item>硬约束 A：触及扫描盒边界或网格边界的连通块永不删除/填充。</item>
///   <item>硬约束 B：Morphology == false 时整个 pass 幂等。</item>
///   <item>硬约束 C：绝不重算扫描范围之外的内容。</item>
///   <item>Changed 同时表达「写」与「删」：值为 defaultType 表示调用方应删除该 sparse 条目。</item>
/// </list>
/// </summary>
public class CoastlineRefinerTests
{
    // ──── 测试用辅助 ──────────────────────────────────────────────────────────

    private const TerrainType DefaultType = TerrainType.Water;

    /// <summary>
    /// 构造一组「(x,y) → 类型」的 2D 网格（含越界默认），返回 <c>read</c> 闭包与实际写入字典。
    /// <paramref name="width"/> / <paramref name="height"/> 是网格范围；<paramref name="cells"/> 是非默认格。
    /// </summary>
    private static (Func<int, int, TerrainType> read, Dictionary<GridCoord, TerrainType> store)
        Cells(int width, int height, params (int x, int y, TerrainType type)[] cells)
    {
        var map = new Dictionary<GridCoord, TerrainType>();
        foreach (var (x, y, t) in cells)
        {
            Assert.True(x >= 0 && x < width, $"x={x} 越界 width={width}");
            Assert.True(y >= 0 && y < height, $"y={y} 越界 height={height}");
            map[new GridCoord(x, y)] = t;
        }

        Func<int, int, TerrainType> read = (x, y) =>
            map.TryGetValue(new GridCoord(x, y), out var v) ? v : DefaultType;

        return (read, map);
    }

    private static GridCoord At(int x, int y) => new(x, y);

    /// <summary>
    /// 把 <paramref name="initial"/>（一个 sparse 字典）+ <paramref name="changes"/>（RefineOutcome.Changed）
    /// 应用到一起，返回「应用后」的 sparse 字典（值为 defaultType 表示删除）。
    /// </summary>
    private static Dictionary<GridCoord, TerrainType> Apply(
        Dictionary<GridCoord, TerrainType> initial,
        IReadOnlyDictionary<GridCoord, TerrainType> changes)
    {
        var result = new Dictionary<GridCoord, TerrainType>(initial);
        foreach (var kvp in changes)
        {
            if (EqualityComparer<TerrainType>.Default.Equals(kvp.Value, DefaultType))
            {
                result.Remove(kvp.Key);
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// 把字典打包成 <c>read</c> 闭包,越界/缺失 → <see cref="DefaultType"/>。
    /// </summary>
    private static Func<int, int, TerrainType> ReadFrom(Dictionary<GridCoord, TerrainType> map) =>
        (x, y) => map.TryGetValue(new GridCoord(x, y), out var v) ? v : DefaultType;

    // ──── 测试 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 3 格孤岛（<c>min_island_cells = 8</c>）→ 三格全部被改成 defaultType。
    /// 覆盖「去碎岛」步骤（spec 2 §5.1 第 1 步）。
    /// </summary>
    [Fact]
    public void ThreeCellIsland_IsRemoved_AtThreshold8()
    {
        // 三格孤岛，位置：(2,1)(3,1)(2,2)（以连通 4 邻接切分属同一块）。
        var (read, _) = Cells(
            width: 10, height: 10,
            (2, 1, TerrainType.Plain),
            (3, 1, TerrainType.Plain),
            (2, 2, TerrainType.Plain));

        var outcome = CoastlineRefiner.Refine(
            read,
            width: 10, height: 10,
            rangeMin: At(0, 0),
            rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // 三格孤岛 → 全部「删」回 defaultType。
        Assert.Equal(DefaultType, outcome.Changed[At(2, 1)]);
        Assert.Equal(DefaultType, outcome.Changed[At(3, 1)]);
        Assert.Equal(DefaultType, outcome.Changed[At(2, 2)]);
        Assert.Equal(3, outcome.Stats.RemovedCells);
        Assert.Equal(0, outcome.Stats.FilledCells);
    }

    /// <summary>
    /// 硬约束 A：3 格陆地**贴着扫描盒边界**（被 [0,9]×[0,9] 的 box 切中）→ 永不删除。
    /// 覆盖 spec 2 §5.2a 的核心要求：横跨范围的大陆不能被切角。
    /// </summary>
    [Fact]
    public void BoundaryTouching_Component_Is_Preserved()
    {
        // 3 格陆地贴在扫描盒顶部边（y=9）+ 左侧边（x=0），与 box 切中 → 视为「外部大陆的一部分」，
        // 永不删除。位置：(0,9)(1,9)(2,9) 通过 4 邻接构成同一块，全部触边界。
        var (read, _) = Cells(
            width: 10, height: 10,
            (0, 9, TerrainType.Plain),
            (1, 9, TerrainType.Plain),
            (2, 9, TerrainType.Plain));

        var outcome = CoastlineRefiner.Refine(
            read,
            width: 10, height: 10,
            rangeMin: At(0, 0),
            rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // 硬约束 A：触边界的陆地永远不会被「删除」(变成 Water)。
        // 它们可以被 coast 重标（这是合法的保留 — 格子仍在,只是类型变了）。
        Assert.Equal(0, outcome.Stats.RemovedCells);
        if (outcome.Changed.ContainsKey(At(0, 9)))
        {
            Assert.NotEqual(TerrainType.Water, outcome.Changed[At(0, 9)]);
        }
        if (outcome.Changed.ContainsKey(At(1, 9)))
        {
            Assert.NotEqual(TerrainType.Water, outcome.Changed[At(1, 9)]);
        }
        if (outcome.Changed.ContainsKey(At(2, 9)))
        {
            Assert.NotEqual(TerrainType.Water, outcome.Changed[At(2, 9)]);
        }
    }

    /// <summary>
    /// 4 格封闭水域（<c>min_lake_cells = 6</c>）→ 全部填为 plain。
    /// 覆盖「填碎海」步骤（spec 2 §5.1 第 2 步）。
    /// </summary>
    [Fact]
    public void FourCellLake_IsFilled_AtThreshold6()
    {
        // 一个 4 格封闭水域：环 (1,1)(2,1)(1,2)(2,2) 是水；
        // 外圈 (0..3, 0..3) 中其余格是 plain,才能让这片水真的「封闭」。
        var (read, initial) = Cells(
            width: 10, height: 10,
            // 外圈陆地
            (0, 0, TerrainType.Plain), (1, 0, TerrainType.Plain), (2, 0, TerrainType.Plain), (3, 0, TerrainType.Plain),
            (0, 1, TerrainType.Plain),                                       (3, 1, TerrainType.Plain),
            (0, 2, TerrainType.Plain),                                       (3, 2, TerrainType.Plain),
            (0, 3, TerrainType.Plain), (1, 3, TerrainType.Plain), (2, 3, TerrainType.Plain), (3, 3, TerrainType.Plain));

        var outcome = CoastlineRefiner.Refine(
            read,
            width: 10, height: 10,
            rangeMin: At(0, 0),
            rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // 4 格封闭水域 → 填为 plain。
        Assert.Equal(TerrainType.Plain, outcome.Changed[At(1, 1)]);
        Assert.Equal(TerrainType.Plain, outcome.Changed[At(2, 1)]);
        Assert.Equal(TerrainType.Plain, outcome.Changed[At(1, 2)]);
        Assert.Equal(TerrainType.Plain, outcome.Changed[At(2, 2)]);
        Assert.Equal(4, outcome.Stats.FilledCells);

        // 验证 Changed.Count == Removed + Filled + Relabeled。
        Assert.Equal(
            outcome.Stats.RemovedCells + outcome.Stats.FilledCells + outcome.Stats.RelabeledCells,
            outcome.Changed.Count);
    }

    /// <summary>
    /// 硬约束 A + 「填碎海」组合：1 格宽、贯穿扫描盒的水道（触左右边界）→ 永不填充。
    /// 即便面积 (10 格) &lt; 默认阈值 6 时,水道仍按「边界连通豁免」保留。
    /// 覆盖 spec 2 §5.2a 与运河保留（spec 2 §9.4「运河保护」）。
    /// </summary>
    [Fact]
    public void FirstCellWideCanal_IsNotFilled()
    {
        // 上半 (y=0..4) 全陆,下半 (y=5..9) 全陆,中间 y=5 整行是水（即「陆地夹水道」）;
        // 但更直观的「运河」布局是: 全部都是陆地,只有 y=5 这一行是水,横跨左右边界。
        // 这样水道触左右边界 → 永不填充。
        var cellsList = new (int x, int y, TerrainType type)[20];
        int idx = 0;
        for (int x = 0; x < 10; x++)
        {
            cellsList[idx++] = (x, 4, TerrainType.Plain);  // 上岸
            cellsList[idx++] = (x, 5, TerrainType.Plain);  // ←「水道」其实是 plain,不是 water
        }

        var (read, _) = Cells(width: 10, height: 10, cellsList);

        // 真正测试：水道是 water,两岸是 plain。
        // 构造一组:水道 = water(y=5 整行,触左右边界),其余=plain。
        var read2 = new Dictionary<GridCoord, TerrainType>();
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                if (y == 5) continue;  // water
                read2[new GridCoord(x, y)] = TerrainType.Plain;
            }
        }
        Func<int, int, TerrainType> r2 = (x, y) =>
            read2.TryGetValue(new GridCoord(x, y), out var v) ? v : DefaultType;

        var outcome = CoastlineRefiner.Refine(
            r2,
            width: 10, height: 10,
            rangeMin: At(0, 0),
            rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // 整行水道（water, 触左右边界）→ 永不填充。
        Assert.Equal(0, outcome.Stats.FilledCells);
        // 水道格不在 Changed 中（水道是 defaultType,而「填海」会把 defaultType 改回陆地 →
        // Changed 应包含这些水道格变成 Plain 的条目）。水道触边界,所以不会有填海。
        // 关键:没有任何水道格被填。
        // 验证：Changed 中不应该有 y=5 的任何格子（水道被保留为 water,默认语义下 read 视 water 为 defaultType,
        // 不需写到 sparse 字典中）。
        foreach (var kvp in outcome.Changed)
        {
            Assert.NotEqual(5, kvp.Key.Y);  // y=5 这一行不应被改
        }
    }

    /// <summary>
    /// 重标 coast 环必须包括「改动区外 1 格」内的陆地（spec 2 §5.2 末段）。
    /// 即：扫描盒 [rangeMin - 1, rangeMax + 1] 在网格求交后,box 边界外的紧邻陆地也要被扫到。
    /// </summary>
    [Fact]
    public void CoastRing_IsRelabeled_IncludingExpandedRing()
    {
        // 布局：用户「改动区」是 (5..14, 5..14) （宽 10）。细化扫描盒 = (4..15, 4..15)。
        // 让 (5,5) 是 plain,且其 8 邻接中存在一个 water 格(在扫描盒外 — 但 box 应扩 1 格)：
        //   (5,4) 是 plain,扫描盒吃到 → 应被重标。
        //   我们让 (5,3) 是 water — 这格在 box 外,box 内 (5,4) 的 8 邻接看不到。
        //   但是 (5,4) 的 8 邻接里有 (5,5) 是 plain → 不算 water;
        //   真实的水是 (5,3),扫描盒外。
        //   为让 (5,4) 真的是 coast,我们把 (4,4) 和 (4,5) 设为 water（在 box 内）。
        // (4,4)=water, (4,5)=water —— 这两格在 box 内,(5,4) 的 8 邻接里看到 water → 应被 coast。
        // (7,7) 是 plain,周围都是 plain → 保持 plain(不挨水)。
        var (read, _) = Cells(
            width: 20, height: 20,
            // 改动区内（且扫描盒内）的陆地
            (5, 5, TerrainType.Plain),
            (6, 5, TerrainType.Plain),
            (5, 6, TerrainType.Plain),
            // 紧邻 (5,5) 上方（y=4）的格,落在扫描盒内、改动区外 — 关键:它要在 box 内
            (5, 4, TerrainType.Plain),
            // 紧邻 (5,5) 左侧（x=4）的格,扫描盒内、改动区外
            (4, 5, TerrainType.Plain));
        // (4,4) 落在 box 内,默认是 water（defaultType）→ 让 (5,4) 的 8 邻接有水。

        var outcome = CoastlineRefiner.Refine(
            read,
            width: 20, height: 20,
            rangeMin: At(5, 5),
            rangeMax: At(14, 14),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // (5,4) 紧邻的 (4,4) 是 water（默认）,→ 8 邻接含水 → 应被标 coast。
        Assert.Equal(TerrainType.Coast, outcome.Changed[At(5, 4)]);
        // (4,5) 紧邻的 (3,5) 是 water（默认）,→ 应被标 coast。
        Assert.Equal(TerrainType.Coast, outcome.Changed[At(4, 5)]);
        // 至少有一格在改动区外被重标（这就是「扫描盒扩 1 格」的意义 — 紧邻改动区外的格子也在扫描盒内）。
        Assert.True(outcome.Stats.RelabeledCells > 0);
    }

    /// <summary>
    /// 硬约束 B（幂等）：Morphology = false 时,同一个输入跑两次 → 第二次 Changed 为空。
    /// </summary>
    [Fact]
    public void SecondPass_WithMorphologyOff_ChangesNothing()
    {
        var initial = new Dictionary<GridCoord, TerrainType>
        {
            // 三格孤岛
            [At(2, 1)] = TerrainType.Plain,
            [At(3, 1)] = TerrainType.Plain,
            [At(2, 2)] = TerrainType.Plain,
            // 4 格封闭水域的「外圈陆地」
            [At(0, 0)] = TerrainType.Plain, [At(1, 0)] = TerrainType.Plain, [At(2, 0)] = TerrainType.Plain, [At(3, 0)] = TerrainType.Plain,
            [At(0, 1)] = TerrainType.Plain,                                                                [At(3, 1)] = TerrainType.Plain,
            [At(0, 2)] = TerrainType.Plain,                                                                [At(3, 2)] = TerrainType.Plain,
            [At(0, 3)] = TerrainType.Plain, [At(1, 3)] = TerrainType.Plain, [At(2, 3)] = TerrainType.Plain, [At(3, 3)] = TerrainType.Plain,
        };

        Func<int, int, TerrainType> read = ReadFrom(initial);

        var options = new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false);
        var first = CoastlineRefiner.Refine(
            read, 10, 10,
            rangeMin: At(0, 0), rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: options);

        // 第一次有改动（删岛 + 填湖 + 可能的 coast 重标）。
        Assert.NotEmpty(first.Changed);

        // 应用 first.Changed 后,第二次跑应幂等。
        var afterFirst = Apply(initial, first.Changed);
        Func<int, int, TerrainType> readAfterFirst = ReadFrom(afterFirst);

        var second = CoastlineRefiner.Refine(
            readAfterFirst, 10, 10,
            rangeMin: At(0, 0), rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: options);

        // 第二次不应再有改动（幂等）。
        Assert.Empty(second.Changed);
    }

    /// <summary>
    /// 硬约束 C（局部性）：扫描盒之外的格子**一个都不变**。
    /// </summary>
    [Fact]
    public void NothingOutsideRange_IsChanged()
    {
        // 三格孤岛,位置 (2,1)(3,1)(2,2),全部都在扫描盒 (5..14, 5..14) 内 → 应被删除。
        // 改动区外 (0,0) 处放一个 plain,扫描盒外 → 不应出现在 Changed 里。
        var (read, _) = Cells(
            width: 20, height: 20,
            (2, 1, TerrainType.Plain),
            (3, 1, TerrainType.Plain),
            (2, 2, TerrainType.Plain),
            // 扫描盒外
            (0, 0, TerrainType.Plain),
            (19, 19, TerrainType.Plain),
            (0, 19, TerrainType.Plain),
            (19, 0, TerrainType.Plain));

        var outcome = CoastlineRefiner.Refine(
            read,
            width: 20, height: 20,
            rangeMin: At(5, 5),
            rangeMax: At(14, 14),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // 扫描盒外的格子绝不被列入 Changed。
        Assert.False(outcome.Changed.ContainsKey(At(0, 0)));
        Assert.False(outcome.Changed.ContainsKey(At(19, 19)));
        Assert.False(outcome.Changed.ContainsKey(At(0, 19)));
        Assert.False(outcome.Changed.ContainsKey(At(19, 0)));
    }

    /// <summary>
    /// Erosion = 0 时,即便 Morphology = true,形态学步骤也不跑。
    /// 间接验证:Changed 的删除总数严格 == 孤岛面积,不引入任何「形态学吃掉」的多余删除。
    /// </summary>
    [Fact]
    public void ErosionZero_DoesNotRunMorphology()
    {
        // 三格孤岛 — 在默认阈值下会被去碎岛（Removed = 3）。
        // 若 morphology 跑了,腐蚀会把边缘的 plain 也「吃掉」产生更多 RemovedCells;
        // 但那些格本就是 defaultType（不在 sparse 字典中）,无需写到 Changed 中。
        // 反向证明:Changed.Count 的 Removed 部分应恰好 = 3。
        var (read, _) = Cells(
            width: 10, height: 10,
            (2, 1, TerrainType.Plain),
            (3, 1, TerrainType.Plain),
            (2, 2, TerrainType.Plain));

        var options = new RefineOptions(
            MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: true);

        var first = CoastlineRefiner.Refine(
            read, 10, 10,
            rangeMin: At(0, 0), rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: options);

        // 预期:RemovedCells = 3（孤岛面积）。
        Assert.Equal(3, first.Stats.RemovedCells);

        // 应用 first.Changed 后,第二次跑应幂等 — 形态学关掉后,前 3 步 + coast 重标都是幂等的。
        var afterFirst = Apply(
            new Dictionary<GridCoord, TerrainType>
            {
                [At(2, 1)] = TerrainType.Plain, [At(3, 1)] = TerrainType.Plain, [At(2, 2)] = TerrainType.Plain,
            },
            first.Changed);

        var second = CoastlineRefiner.Refine(
            ReadFrom(afterFirst), 10, 10,
            rangeMin: At(0, 0), rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: options);

        Assert.Empty(second.Changed);
    }

    /// <summary>
    /// 「删除」与「写」语义：Changed 中当 NewValue == defaultType 时,
    /// 调用方应「删除 sparse 条目」而非「写入 defaultType」。
    /// </summary>
    [Fact]
    public void ReturnedDeletion_IsReportedAsDefaultType()
    {
        // 三格孤岛被去碎岛 → 三格应被报告为「删除」即 NewValue = defaultType。
        var (read, _) = Cells(
            width: 10, height: 10,
            (2, 1, TerrainType.Plain),
            (3, 1, TerrainType.Plain),
            (2, 2, TerrainType.Plain));

        var outcome = CoastlineRefiner.Refine(
            read, 10, 10,
            rangeMin: At(0, 0), rangeMax: At(9, 9),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        // 三格都应被报告为「删除」（NewValue == defaultType）。
        Assert.Equal(DefaultType, outcome.Changed[At(2, 1)]);
        Assert.Equal(DefaultType, outcome.Changed[At(3, 1)]);
        Assert.Equal(DefaultType, outcome.Changed[At(2, 2)]);

        // 验证调用方可以「应用 Changed」得到正确的字典状态。
        var initial = new Dictionary<GridCoord, TerrainType>
        {
            [At(2, 1)] = TerrainType.Plain,
            [At(3, 1)] = TerrainType.Plain,
            [At(2, 2)] = TerrainType.Plain,
        };
        var after = Apply(initial, outcome.Changed);
        Assert.False(after.ContainsKey(At(2, 1)));
        Assert.False(after.ContainsKey(At(3, 1)));
        Assert.False(after.ContainsKey(At(2, 2)));
    }

    /// <summary>
    /// 不变量：RemovedCells + FilledCells + RelabeledCells == Changed.Count。
    /// 即 Changed 同时承载了「删除」「填海」「重标」三类,三者加总 = Changed 大小。
    /// </summary>
    [Fact]
    public void StatsCounts_MatchChangedSize()
    {
        // 布局:三格孤岛 + 一个 4 格封闭水域 + 一些杂海岸陆地 → 覆盖三类改动。
        var (read, _) = Cells(
            width: 20, height: 20,
            // 三格孤岛 → 去碎岛（Removed = 3）
            (2, 1, TerrainType.Plain),
            (3, 1, TerrainType.Plain),
            (2, 2, TerrainType.Plain),
            // 4 格封闭水域 → 填碎海（Filled = 4）
            (10, 10, TerrainType.Plain), (11, 10, TerrainType.Plain), (12, 10, TerrainType.Plain), (13, 10, TerrainType.Plain),
            (10, 11, TerrainType.Plain),                                                            (13, 11, TerrainType.Plain),
            (10, 12, TerrainType.Plain),                                                            (13, 12, TerrainType.Plain),
            (10, 13, TerrainType.Plain), (11, 13, TerrainType.Plain), (12, 13, TerrainType.Plain), (13, 13, TerrainType.Plain),
            // 「内圈」陆地,挨水域 → 应被重标为 coast（Relabeled > 0）
            (10, 9, TerrainType.Plain),
            (9, 10, TerrainType.Plain));

        var outcome = CoastlineRefiner.Refine(
            read, 20, 20,
            rangeMin: At(0, 0), rangeMax: At(19, 19),
            defaultType: DefaultType,
            options: new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false));

        int sum = outcome.Stats.RemovedCells + outcome.Stats.FilledCells + outcome.Stats.RelabeledCells;
        Assert.Equal(sum, outcome.Changed.Count);
    }
}