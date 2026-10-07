// src/render/BuildingLayer.ts
// 建筑层（按 `map.building_refs[]` 渲染矩形 + 文字）。

import { Container, Graphics, Text } from "pixi.js";
import type { BuildingRef } from "@/types";

export class BuildingLayer {
  readonly container = new Container();
  private buildings: BuildingRef[] = [];

  setBuildings(buildings: BuildingRef[]) {
    this.buildings = buildings;
  }

  update(scale: number): void {
    this.container.removeChildren();
    for (const b of this.buildings) {
      const g = new Graphics();
      g.rect(b.geometry.x, b.geometry.y, b.geometry.w, b.geometry.h)
        .fill({ color: 0xb89070, alpha: 0.5 })
        .stroke({ width: 1 / scale, color: 0x553322, alpha: 0.9 });
      this.container.addChild(g);

      const label = new Text({
        text: b.name,
        style: {
          fontSize: 10 / scale,
          fill: 0x222222,
          fontFamily: "system-ui",
          fontWeight: "bold",
        },
      });
      label.position.set(b.geometry.x + 0.5 / scale, b.geometry.y + 0.5 / scale);
      this.container.addChild(label);
    }
  }
}