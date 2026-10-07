// src/render/GridLayer.ts
// 网格层（按 `map.grid.show` 与 UI store `showGrid` 共同决定是否绘制）。

import { Container, Graphics } from "pixi.js";

export class GridLayer {
  readonly container = new Container();
  private gridW: number;
  private gridH: number;

  constructor(gridW: number, gridH: number) {
    this.gridW = gridW;
    this.gridH = gridH;
    void this.gridW;
    void this.gridH;
  }

  update(visibleRange: { colMin: number; colMax: number; rowMin: number; rowMax: number }, scale: number): void {
    this.container.removeChildren();
    const { colMin, colMax, rowMin, rowMax } = visibleRange;
    const g = new Graphics();
    const lineWidth = 1 / scale;
    for (let x = colMin; x <= colMax + 1; x++) {
      g.moveTo(x, rowMin).lineTo(x, rowMax + 1);
    }
    for (let y = rowMin; y <= rowMax + 1; y++) {
      g.moveTo(colMin, y).lineTo(colMax + 1, y);
    }
    g.stroke({ width: lineWidth, color: 0x666666, alpha: 0.4 });
    this.container.addChild(g);
  }
}