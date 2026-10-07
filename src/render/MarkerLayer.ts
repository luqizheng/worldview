// src/render/MarkerLayer.ts
// 城市 / 标记层（MVP mock：极光港）

import { Container, Graphics, Text } from "pixi.js";

export class MarkerLayer {
  readonly container = new Container();

  update(mapType: string, scale: number): void {
    this.container.removeChildren();
    if (mapType === "world") {
      const dot = new Graphics();
      dot.circle(0, 0, 0.5).fill({ color: 0xffd166 });
      dot.stroke({ width: 1 / scale, color: 0x000000, alpha: 0.6 });
      dot.position.set(32, 24);
      this.container.addChild(dot);

      const label = new Text({
        text: "极光港",
        style: { fontSize: 12 / scale, fill: 0xffffff, fontFamily: "system-ui" },
      });
      label.position.set(33, 23);
      this.container.addChild(label);
    }
  }
}