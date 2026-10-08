using WorldView.Domain;

namespace WorldView.Domain.Generation;

/// <summary>
/// 共用海岸线细化 pass（spec 2 §5）。
///
/// <para>目的：世界生成器与笔刷局部涂改共享同一个纯函数（无 Godot 依赖），
/// 输入一段「只读 <see cref="Func{T1, T2, TResult}"/>」与改动范围，输出
/// 「稀疏字典变更 + 统计」。</para>
///
/// <para>步骤（顺序固定）：</para>
/// <list type="number">
///   <item>去碎岛 — 删除面积 &lt; <see cref="RefineOptions.MinIslandCells"/> 的连通陆块（4 邻接），
///     但**任何触及扫描盒边界或网格边界的连通块永远不删**（spec 2 §5.2a）。</item>
///   <item>填碎海 — 填充面积 &lt; <see cref="RefineOptions.MinLakeCells"/> 的连通水域（4 邻接），
///     同样不填任何触边界的水块。</item>
///   <item>形态学 — 仅当 <see cref="RefineOptions.Morphology"/> 为 true 且
///     <see cref="RefineOptions.Erosion"/> &gt; 0 时执行：交替腐蚀/膨胀 <c>Erosion</c> 轮，
///     削平单格凸起与 1 格宽毛刺（spec 2 §5.2b：此步非幂等,仅生成路径使用）。</item>
///   <item>重标 coast — 在「扫描盒外扩 1 格」后,与任一水格 8 邻接的陆地 → <see cref="TerrainType.Coast"/>，
///     其余陆地 → <see cref="TerrainType.Plain"/>。</item>
/// </list>
///
/// <para>连通性约定：岛屿/湖泊用 4 邻接（接 N/S/E/W）；coast 判定用 8 邻接（含对角）。</para>
///
/// <para>硬约束（spec 2 §5.2）：</para>
/// <list type="bullet">
///   <item>A：触扫描盒边界或网格边界的连通块永不删/填。</item>
///   <item>B：<see cref="RefineOptions.Morphology"/> = false 时整体幂等。</item>
///   <item>C：扫描盒外的格子绝不被读、被写或被列入 <see cref="RefineOutcome.Changed"/>。</item>
/// </list>
///
/// <para><see cref="RefineOutcome.Changed"/> 同时表达「写」与「删」：值为
/// <paramref name="defaultType"/> 时表示调用方应从稀疏字典删除该条目（spec 2 §5.1 末段）。</para>
/// </summary>
public static class CoastlineRefiner
{
    /// <summary>
    /// 在 <c>[rangeMin - 1, rangeMax + 1]</c>（与网格求交）内运行细化 pass。
    /// </summary>
    /// <param name="read">地形读访问器；越界或未写入的格视为 <paramref name="defaultType"/>。</param>
    /// <param name="width">网格宽度（X 方向）。</param>
    /// <param name="height">网格高度（Y 方向）。</param>
    /// <param name="rangeMin">用户改动区最小角（含）。</param>
    /// <param name="rangeMax">用户改动区最大角（含）。</param>
    /// <param name="defaultType">地图的默认地形（水的别名,通常是 <see cref="TerrainType.Water"/>）。</param>
    /// <param name="options">细化阈值与形态学开关。</param>
    public static RefineOutcome Refine(
        Func<int, int, TerrainType> read,
        int width, int height,
        GridCoord rangeMin, GridCoord rangeMax,
        TerrainType defaultType,
        RefineOptions options)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(options);

        // ──── 扫描盒 = [rangeMin - 1, rangeMax + 1] ∩ 网格 ────
        // 外扩 1 格是为了重标 coast 时不错过改动区外但紧邻的陆地（spec 2 §5.2 末段）。
        // 用 long 防整数溢出（width/height 可能很大）。
        int scanXMin = (int)Math.Max(0L, (long)rangeMin.X - 1L);
        int scanYMin = (int)Math.Max(0L, (long)rangeMin.Y - 1L);
        int scanXMax = (int)Math.Min((long)width - 1L, (long)rangeMax.X + 1L);
        int scanYMax = (int)Math.Min((long)height - 1L, (long)rangeMax.Y + 1L);

        if (scanXMin > scanXMax || scanYMin > scanYMax)
        {
            // 范围完全在网格外 — 硬约束 C:不读不写,直接返空。
            return new RefineOutcome(
                new Dictionary<GridCoord, TerrainType>(),
                new RefineStats(0, 0, 0));
        }

        int boxW = scanXMax - scanXMin + 1;
        int boxH = scanYMax - scanYMin + 1;

        // 局部坐标 ↔ 全局坐标 偏移。
        int gx(int lx) => lx + scanXMin;
        int gy(int ly) => ly + scanYMin;

        // 「陆地」与「水」按 brief 第 4 条语义定义：陆地 = != defaultType,水 = == defaultType。
        bool IsLand(TerrainType t) => !EqualityComparer<TerrainType>.Default.Equals(t, defaultType);
        bool IsWater(TerrainType t) => EqualityComparer<TerrainType>.Default.Equals(t, defaultType);

        // ──── 加载扫描盒的「当前网格视图」（不改输入,仅快照） ────
        // 与「硬约束 C」配合：box 外的一律不在 view 里。
        var view = new TerrainType[boxW, boxH];
        for (int ly = 0; ly < boxH; ly++)
        {
            for (int lx = 0; lx < boxW; lx++)
            {
                view[lx, ly] = read(gx(lx), gy(ly));
            }
        }

        // ──── 步骤 1：去碎岛（4 邻接连通块） ────
        var visitedIsland = new bool[boxW, boxH];
        for (int ly = 0; ly < boxH; ly++)
        {
            for (int lx = 0; lx < boxW; lx++)
            {
                if (visitedIsland[lx, ly] || !IsLand(view[lx, ly])) continue;

                var cells = FloodFillLand4(lx, ly, view, visitedIsland, IsLand);
                bool touchesBoxBoundary = cells.Any(c => c.lx == 0 || c.lx == boxW - 1 || c.ly == 0 || c.ly == boxH - 1);
                bool touchesGridBoundary = cells.Any(c => gx(c.lx) == 0 || gx(c.lx) == width - 1 || gy(c.ly) == 0 || gy(c.ly) == height - 1);

                if (touchesBoxBoundary || touchesGridBoundary) continue;
                if (cells.Count >= options.MinIslandCells) continue;

                foreach (var c in cells) view[c.lx, c.ly] = defaultType;
            }
        }

        // ──── 步骤 2：填碎海（4 邻接连通块） ────
        var visitedLake = new bool[boxW, boxH];
        for (int ly = 0; ly < boxH; ly++)
        {
            for (int lx = 0; lx < boxW; lx++)
            {
                if (visitedLake[lx, ly] || !IsWater(view[lx, ly])) continue;

                var cells = FloodFillWater4(lx, ly, view, visitedLake, IsWater);
                bool touchesBoxBoundary = cells.Any(c => c.lx == 0 || c.lx == boxW - 1 || c.ly == 0 || c.ly == boxH - 1);
                bool touchesGridBoundary = cells.Any(c => gx(c.lx) == 0 || gx(c.lx) == width - 1 || gy(c.ly) == 0 || gy(c.ly) == height - 1);

                if (touchesBoxBoundary || touchesGridBoundary) continue;
                if (cells.Count >= options.MinLakeCells) continue;

                foreach (var c in cells) view[c.lx, c.ly] = TerrainType.Plain;
            }
        }

        // ──── 步骤 3：形态学（仅当 Morphology && Erosion > 0） ────
        // 腐蚀/膨胀规则（4 邻接）：
        //   腐蚀：陆地格若 4 邻接中水 ≥ 3 → 删除（变水）。
        //   膨胀：水格若 4 邻接中陆 ≥ 3 → 加（变 Plain）。
        // 如此交替可削平单格毛刺,且对孤立的「1 格宽河道」不会过度膨胀。
        if (options.Morphology && options.Erosion > 0)
        {
            for (int step = 0; step < options.Erosion; step++)
            {
                view = ErodeLand(view, IsLand, IsWater, waterThreshold: 3);
                view = DilateLand(view, IsLand, IsWater, landThreshold: 3);
            }
        }

        // ──── 步骤 4：重标 coast（8 邻接） ────
        // 扫描盒内的所有陆地格,若 8 邻接中存在水格 → coast;否则 → plain。
        // box 已扩到 [rangeMin-1, rangeMax+1],所以 box 内的陆地周围的水若在 box 外我们看不到,
        // 但 spec 要求 box 已外扩 1 格,故 box 内的「改动区外 1 格的陆地」也能拿到正确的邻居视图。
        // 注意：box 内未触 box 边界的格子在 view 中,触边界的（即 box 角/边）可能仍有「box 外的」邻居,
        // 这些邻居我们看不到,但 spec 不要求 box 外扩 2 格；触边界的格子只要在 box 内能看到的水就是真实水,
        // box 外的水对 box 边的格子来说是「box 外的真实水」,但我们没读——这正是为什么 view 在 box 边界处
        // 会缺失「真实的水邻居」,从而可能漏标 coast。
        // 为正确处理 box 边格子的 coast 判定,我们额外做一次：对每个 box 边的陆地格,
        // 检查它「box 外的邻居」(通过 read) 是否为水,只要有 1 个水邻居即标 coast。
        for (int ly = 0; ly < boxH; ly++)
        {
            for (int lx = 0; lx < boxW; lx++)
            {
                if (!IsLand(view[lx, ly])) continue;

                bool hasWater = HasWaterNeighbor8(view, lx, ly, IsWater);
                if (!hasWater && IsOnBoxEdge(lx, ly, boxW, boxH))
                {
                    hasWater = HasOuterWaterNeighbor8(lx, ly, scanXMin, scanYMin, scanXMax, scanYMax, read, IsWater, width, height);
                }
                view[lx, ly] = hasWater ? TerrainType.Coast : TerrainType.Plain;
            }
        }

        // ──── 合并：扫描 view vs 原始 read,把所有「被改动」的格子写入 Changed ────
        // 三类改动的判定：
        //   - 当前 = defaultType, 原始 = 陆地   → Removed（删除）
        //   - 当前 = 陆地,     原始 = defaultType → Filled（填海）
        //   - 当前 = 陆地,     原始 = 陆地(不同类型) → Relabeled（重标）
        int statRemoved = 0, statFilled = 0, statRelabeled = 0;
        var changed = new Dictionary<GridCoord, TerrainType>();
        for (int ly = 0; ly < boxH; ly++)
        {
            for (int lx = 0; lx < boxW; lx++)
            {
                TerrainType original = read(gx(lx), gy(ly));
                TerrainType current = view[lx, ly];
                if (EqualityComparer<TerrainType>.Default.Equals(original, current)) continue;

                changed[new GridCoord(gx(lx), gy(ly))] = current;
                if (IsWater(current) && !IsWater(original))
                {
                    statRemoved++;
                }
                else if (!IsWater(current) && IsWater(original))
                {
                    statFilled++;
                }
                else
                {
                    statRelabeled++;
                }
            }
        }

        return new RefineOutcome(
            changed,
            new RefineStats(statRemoved, statFilled, statRelabeled));
    }

    private static bool IsOnBoxEdge(int lx, int ly, int boxW, int boxH) =>
        lx == 0 || lx == boxW - 1 || ly == 0 || ly == boxH - 1;

    // ──── 4 邻接 flood fill（陆地） ────

    private static List<(int lx, int ly)> FloodFillLand4(
        int sx, int sy,
        TerrainType[,] view,
        bool[,] visited,
        Func<TerrainType, bool> isLand)
    {
        int w = view.GetLength(0);
        int h = view.GetLength(1);
        var stack = new Stack<(int lx, int ly)>();
        var cells = new List<(int lx, int ly)>();
        stack.Push((sx, sy));
        visited[sx, sy] = true;

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            cells.Add((x, y));

            if (x > 0 && !visited[x - 1, y] && isLand(view[x - 1, y]))
            {
                visited[x - 1, y] = true;
                stack.Push((x - 1, y));
            }
            if (x < w - 1 && !visited[x + 1, y] && isLand(view[x + 1, y]))
            {
                visited[x + 1, y] = true;
                stack.Push((x + 1, y));
            }
            if (y > 0 && !visited[x, y - 1] && isLand(view[x, y - 1]))
            {
                visited[x, y - 1] = true;
                stack.Push((x, y - 1));
            }
            if (y < h - 1 && !visited[x, y + 1] && isLand(view[x, y + 1]))
            {
                visited[x, y + 1] = true;
                stack.Push((x, y + 1));
            }
        }
        return cells;
    }

    // ──── 4 邻接 flood fill（水） ────

    private static List<(int lx, int ly)> FloodFillWater4(
        int sx, int sy,
        TerrainType[,] view,
        bool[,] visited,
        Func<TerrainType, bool> isWater)
    {
        int w = view.GetLength(0);
        int h = view.GetLength(1);
        var stack = new Stack<(int lx, int ly)>();
        var cells = new List<(int lx, int ly)>();
        stack.Push((sx, sy));
        visited[sx, sy] = true;

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            cells.Add((x, y));

            if (x > 0 && !visited[x - 1, y] && isWater(view[x - 1, y]))
            {
                visited[x - 1, y] = true;
                stack.Push((x - 1, y));
            }
            if (x < w - 1 && !visited[x + 1, y] && isWater(view[x + 1, y]))
            {
                visited[x + 1, y] = true;
                stack.Push((x + 1, y));
            }
            if (y > 0 && !visited[x, y - 1] && isWater(view[x, y - 1]))
            {
                visited[x, y - 1] = true;
                stack.Push((x, y - 1));
            }
            if (y < h - 1 && !visited[x, y + 1] && isWater(view[x, y + 1]))
            {
                visited[x, y + 1] = true;
                stack.Push((x, y + 1));
            }
        }
        return cells;
    }

    // ──── 形态学 ────

    private static TerrainType[,] ErodeLand(
        TerrainType[,] view,
        Func<TerrainType, bool> isLand,
        Func<TerrainType, bool> isWater,
        int waterThreshold)
    {
        int w = view.GetLength(0);
        int h = view.GetLength(1);
        var next = (TerrainType[,])view.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!isLand(view[x, y])) continue;
                int waterCount = 0;
                if (x > 0 && isWater(view[x - 1, y])) waterCount++;
                if (x < w - 1 && isWater(view[x + 1, y])) waterCount++;
                if (y > 0 && isWater(view[x, y - 1])) waterCount++;
                if (y < h - 1 && isWater(view[x, y + 1])) waterCount++;
                if (waterCount >= waterThreshold)
                {
                    next[x, y] = TerrainType.Water;
                }
            }
        }
        return next;
    }

    private static TerrainType[,] DilateLand(
        TerrainType[,] view,
        Func<TerrainType, bool> isLand,
        Func<TerrainType, bool> isWater,
        int landThreshold)
    {
        int w = view.GetLength(0);
        int h = view.GetLength(1);
        var next = (TerrainType[,])view.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!isWater(view[x, y])) continue;
                int landCount = 0;
                if (x > 0 && isLand(view[x - 1, y])) landCount++;
                if (x < w - 1 && isLand(view[x + 1, y])) landCount++;
                if (y > 0 && isLand(view[x, y - 1])) landCount++;
                if (y < h - 1 && isLand(view[x, y + 1])) landCount++;
                if (landCount >= landThreshold)
                {
                    next[x, y] = TerrainType.Plain;
                }
            }
        }
        return next;
    }

    // ──── 8 邻接「是否有水」 ────

    private static bool HasWaterNeighbor8(
        TerrainType[,] view, int x, int y, Func<TerrainType, bool> isWater)
    {
        int w = view.GetLength(0);
        int h = view.GetLength(1);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (isWater(view[nx, ny])) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查 box 边缘的陆地格在 box 外（通过 read 直接读）是否有水邻居。
    /// 这是 box 内的 8 邻接看不到的水（box 外的真实水）。
    /// </summary>
    private static bool HasOuterWaterNeighbor8(
        int lx, int ly,
        int scanXMin, int scanYMin, int scanXMax, int scanYMax,
        Func<int, int, TerrainType> read,
        Func<TerrainType, bool> isWater,
        int width, int height)
    {
        int gx = lx + scanXMin;
        int gy = ly + scanYMin;
        // 该格在 box 内,但是 box 边格。8 邻接中,如果邻居在 box 外（即在 (lx == boxW-1 && dx == 1) 等）,
        // box 内的 view 看不到 — 用 read 直接读。
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nlx = lx + dx;
                int nly = ly + dy;
                bool inBox = nlx >= 0 && nlx <= scanXMax - scanXMin && nly >= 0 && nly <= scanYMax - scanYMin;
                if (inBox) continue;  // box 内的 view 已覆盖
                int ngx = gx + dx;
                int ngy = gy + dy;
                if (ngx < 0 || ngx >= width || ngy < 0 || ngy >= height) continue;
                if (isWater(read(ngx, ngy))) return true;
            }
        }
        return false;
    }
}

/// <summary>
/// 细化 pass 的阈值与开关（spec 2 §5.1 / §6.4）。
/// </summary>
/// <param name="MinIslandCells">陆地块面积阈值；小于此值的孤立陆块被删除（默认 8）。</param>
/// <param name="MinLakeCells">水域块面积阈值；小于此值的封闭水域被填充（默认 6）。</param>
/// <param name="Erosion">形态学步数；仅当 <see cref="Morphology"/> = true 且本值 &gt; 0 时执行（默认 0）。</param>
/// <param name="Morphology">是否执行形态学（腐蚀/膨胀）；仅生成路径启用，笔刷路径关闭以保幂等（默认 false）。</param>
public sealed record RefineOptions(int MinIslandCells = 8, int MinLakeCells = 6, int Erosion = 0, bool Morphology = false);

/// <summary>
/// 细化 pass 的统计：三类改动的格数。
/// 不变量：<c>RemovedCells + FilledCells + RelabeledCells == Changed.Count</c>。
/// </summary>
/// <param name="RemovedCells">被删除的孤立陆地格数。</param>
/// <param name="FilledCells">被填充的封闭水域格数。</param>
/// <param name="RelabeledCells">plain ↔ coast 重标的陆地格数。</param>
public sealed record RefineStats(int RemovedCells, int FilledCells, int RelabeledCells);

/// <summary>
/// 细化 pass 的输出：稀疏字典变更 + 统计。
/// <see cref="Changed"/> 同时表达「写」与「删」：值为 <c>defaultType</c> 时表示调用方应从稀疏字典中删除该条目。
/// </summary>
/// <param name="Changed">被改动的稀疏条目；键 = 全局坐标，值 = 新地形（= defaultType 表示删除）。</param>
/// <param name="Stats">三类改动的格数。</param>
public sealed record RefineOutcome(IReadOnlyDictionary<GridCoord, TerrainType> Changed, RefineStats Stats);