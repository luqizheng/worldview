// src/editing/brush.ts
// 圆形笔刷算法（spec §T14）。
//
// 公开函数：
// - `applyRoundBrush(centerCol, centerRow, size, terrainType, strength)` ——
//   返回 `ApplyBrushCommand`，不立即 apply（由 UndoStack.push 调用 cmd.apply()）。
//
// 「圆形笔刷」定义：中心 (col, row)、半径 `size`（格）；落点范围 = 与中心 Chebyshev 距离 ≤ size 的单元。
// MVP 阶段 strength 不参与计算（保留字段，未来 M2+ 用）。

import { ApplyBrushCommand } from "./commands";
import type { TerrainCell } from "@/types";

/** TerrainCell 默认值（与 `world-view-core::terrain::TerrainCell::plain_default()` 镜像）。 */
export function plainDefaultCell(): TerrainCell {
  return { type: "plain", move_cost: 1, buildable: true, resources: [], height: 0 };
}

/** 默认地形类型表（与数据模型规格 §3.1 对齐）。 */
export const TERRAIN_DEFAULT_CELLS: Record<string, TerrainCell> = {
  plain:    { type: "plain",    move_cost: 1,  buildable: true,  resources: [] },
  mountain: { type: "mountain", move_cost: 4,  buildable: false, resources: [] },
  forest:   { type: "forest",   move_cost: 2,  buildable: true,  resources: ["timber"] },
  hill:     { type: "hill",     move_cost: 2,  buildable: true,  resources: [] },
  water:    { type: "water",    move_cost: 3,  buildable: false, resources: ["fish"] },
  desert:   { type: "desert",   move_cost: 3,  buildable: false, resources: [] },
  swamp:    { type: "swamp",    move_cost: 3,  buildable: false, resources: [] },
  tundra:   { type: "tundra",   move_cost: 2,  buildable: false, resources: [] },
  basin:    { type: "basin",    move_cost: 2,  buildable: true,  resources: [] },
  coast:    { type: "coast",    move_cost: 1,  buildable: true,  resources: [] },
  river:    { type: "river",    move_cost: -1, buildable: false, resources: ["freshwater"] },
};

/** 计算一个圆形笔刷在 (col, row)、半径 size 下覆盖的格子列表。 */
export function circleFootprint(col: number, row: number, size: number): Array<{ col: number; row: number }> {
  const out: Array<{ col: number; row: number }> = [];
  for (let dr = -size; dr <= size; dr++) {
    for (let dc = -size; dc <= size; dc++) {
      // Chebyshev 距离 ≤ size → 圆形（轴对齐外接圆）
      if (Math.abs(dr) + Math.abs(dc) <= size * 2 || Math.max(Math.abs(dr), Math.abs(dc)) <= size) {
        // 我们用 Chebyshev（轴对齐正方形），更贴合「圆形笔刷」视觉；
        // 也可用 Manhattan 模拟，但视觉上太「菱形」。
        // 改用 Chebyshev：max(|dr|, |dc|) ≤ size → 外接正方形中的「圆」。
        if (Math.max(Math.abs(dr), Math.abs(dc)) <= size) {
          out.push({ col: col + dc, row: row + dr });
        }
      }
    }
  }
  return out;
}

/** 构造一个 ApplyBrushCommand（不 apply）。 */
export function applyRoundBrush(
  centerCol: number,
  centerRow: number,
  size: number,
  terrainType: string,
  _strength: number,
  gridW: number,
  gridH: number,
): ApplyBrushCommand {
  const fp = circleFootprint(centerCol, centerRow, size);
  // 计算脏区（外接矩形）
  let colMin = Number.POSITIVE_INFINITY;
  let colMax = Number.NEGATIVE_INFINITY;
  let rowMin = Number.POSITIVE_INFINITY;
  let rowMax = Number.NEGATIVE_INFINITY;
  const before = new Map<string, TerrainCell>();
  for (const p of fp) {
    if (p.col < 0 || p.col >= gridW || p.row < 0 || p.row >= gridH) continue;
    colMin = Math.min(colMin, p.col);
    colMax = Math.max(colMax, p.col);
    rowMin = Math.min(rowMin, p.row);
    rowMax = Math.max(rowMax, p.row);
    // before：caller 注入（apply 时从 LoadedMap.grid 读取）
    // 这里只占位，让 cmd 知道有变化；真实 before/after 由 editing layer 维护。
  }
  // 占位：MVP 阶段 before/after 都填 plainDefaultCell（实际由 caller 注入）
  const fallback = plainDefaultCell();
  for (const p of fp) {
    if (p.col < 0 || p.col >= gridW || p.row < 0 || p.row >= gridH) continue;
    before.set(`${p.col},${p.row}`, fallback);
  }
  const newCell = TERRAIN_DEFAULT_CELLS[terrainType] ?? fallback;
  const after = new Map<string, TerrainCell>();
  for (const p of fp) {
    if (p.col < 0 || p.col >= gridW || p.row < 0 || p.row >= gridH) continue;
    after.set(`${p.col},${p.row}`, newCell);
  }
  const dirtyRect = colMin !== Number.POSITIVE_INFINITY
    ? { colMin, colMax, rowMin, rowMax }
    : null;
  return new ApplyBrushCommand(
    fp,
    before,
    after,
    dirtyRect,
  );
}