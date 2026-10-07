// src/render/TerrainLayer.ts
// 地形层（MVP：实现「视口裁剪」+ 脏区重绘契约）
//
// 接口：
// - `terrainLayer.update(opts)` 全屏重绘（视口变化时调用）；
// - `terrainLayer.redrawRect({colMin,colMax,rowMin,rowMax})` 局部重绘（笔刷完成后调用）；
// - `terrainLayer.getDrawnCellCount()`：供 J2 量化断言。
//
// 数据来源：MVP mock — 与 examples/sample_world/maps/world/main.terrain.yaml 分布一致；
// 真实接入后会改用 useDomainStore 持有的 LoadedMap.grid。

import { Container, Graphics } from "pixi.js";
import { TERRAIN_COLORS } from "@/render/terrainPalette";
import type { TerrainCell, TerrainType } from "@/types";

export class TerrainLayer {
  readonly container = new Container();
  private gridW: number;
  private gridH: number;
  private mockCell: (col: number, row: number) => TerrainCell;
  /** 当前绘制的小 cell Graphics 列表（用于 cull / dirty rect 量化） */
  private drawnCells = new Map<string, Graphics>();

  constructor(gridW: number, gridH: number, mockCell?: (col: number, row: number) => TerrainCell) {
    this.gridW = gridW;
    this.gridH = gridH;
    this.mockCell = mockCell ?? defaultMockCell;
    // 保留字段：未来 cull / 全屏绘制时用于边界 clamp
    void this.gridW;
    void this.gridH;
  }

  /** 视口内绘制（cull：仅绘制 colMin..colMax × rowMin..rowMax 的单元 + 1 格边带）。 */
  update(visibleRange: { colMin: number; colMax: number; rowMin: number; rowMax: number }): void {
    this.container.removeChildren();
    this.drawnCells.clear();
    const { colMin, colMax, rowMin, rowMax } = visibleRange;
    for (let r = rowMin; r <= rowMax; r++) {
      for (let c = colMin; c <= colMax; c++) {
        const cell = this.mockCell(c, r);
        const g = this.makeCellGraphics(c, r, cell.type);
        this.container.addChild(g);
        this.drawnCells.set(`${c},${r}`, g);
      }
    }
  }

  /** 脏区重绘 — 清除该矩形内的旧 cell + 重绘。 */
  redrawRect(rect: { colMin: number; colMax: number; rowMin: number; rowMax: number }): void {
    const { colMin, colMax, rowMin, rowMax } = rect;
    for (let r = rowMin; r <= rowMax; r++) {
      for (let c = colMin; c <= colMax; c++) {
        const key = `${c},${r}`;
        const old = this.drawnCells.get(key);
        if (old) {
          this.container.removeChild(old);
          old.destroy();
          this.drawnCells.delete(key);
        }
        const cell = this.mockCell(c, r);
        const g = this.makeCellGraphics(c, r, cell.type);
        this.container.addChild(g);
        this.drawnCells.set(key, g);
      }
    }
  }

  /** 当前实际绘制的 cell 数量（用于 spec §J2 量化断言）。 */
  getDrawnCellCount(): number {
    return this.drawnCells.size;
  }

  private makeCellGraphics(col: number, row: number, type: TerrainType): Graphics {
    const g = new Graphics();
    const color = TERRAIN_COLORS[type] ?? 0x9ab87a;
    g.rect(col, row, 1, 1).fill({ color });
    return g;
  }
}

/** 默认 mock：与示例工程 world terrain 分布一致。 */
function defaultMockCell(c: number, r: number): TerrainCell {
  if (c >= 20 && c <= 30 && r >= 10 && r <= 20) {
    return { type: "mountain", move_cost: 4, buildable: false };
  }
  if (r === 40 && c >= 10 && c <= 54) {
    return { type: "river", move_cost: -1, buildable: false };
  }
  if (r === 39 && (c === 53 || c === 54)) {
    return { type: "river", move_cost: -1, buildable: false };
  }
  return { type: "plain", move_cost: 1, buildable: true };
}