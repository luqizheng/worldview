// 生成 examples/sample_world/maps/world/main.terrain.yaml
// 分布：
//   - mountain: 矩形 (x:20..30, y:10..20) = 11×11 = 121 格连片
//   - river:    横向折线 (x:10..54, y=40) + 弯折 y=40..38 = 47 格
//   - forest:   散点 (x:5..15, y:30..38) ∪ (x:45..55, y:5..12) ≈ 60 格
//   - 其余 plain

import { writeFileSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";

const W = 64;
const H = 48;

const TERRAIN = {
  plain:    { type: "plain",    move_cost: 1,  buildable: true },
  mountain: { type: "mountain", move_cost: 4,  buildable: false },
  forest:   { type: "forest",   move_cost: 2,  buildable: true,  resources: ["timber"] },
  river:    { type: "river",    move_cost: -1, buildable: false, resources: ["freshwater"] },
};

function makeCells() {
  const cells = new Array(W * H);
  for (let r = 0; r < H; r++) {
    for (let c = 0; c < W; c++) {
      cells[r * W + c] = TERRAIN.plain;
    }
  }

  // mountain: 矩形 (20..30, 10..20) = 11*11 = 121
  for (let r = 10; r <= 20; r++) {
    for (let c = 20; c <= 30; c++) {
      cells[r * W + c] = TERRAIN.mountain;
    }
  }

  // river: 横向 y=40 x=10..54 = 45 格 + 弯到 y=38 x=52..54 = 2 格 = 47
  for (let c = 10; c <= 54; c++) {
    cells[40 * W + c] = TERRAIN.river;
  }
  cells[39 * W + 53] = TERRAIN.river;
  cells[39 * W + 54] = TERRAIN.river;

  // forest: 散点 (5..15, 30..38) 11*9=99 个随机散点取 32 个 + (45..55, 5..12) 11*8=88 个随机取 32 个
  const forestPositions = [];
  const rng = mulberry32(20251007);
  for (let i = 0; i < 32; i++) {
    const c = 5 + Math.floor(rng() * 11);
    const r = 30 + Math.floor(rng() * 9);
    forestPositions.push([c, r]);
  }
  for (let i = 0; i < 32; i++) {
    const c = 45 + Math.floor(rng() * 11);
    const r = 5 + Math.floor(rng() * 8);
    forestPositions.push([c, r]);
  }
  for (const [c, r] of forestPositions) {
    cells[r * W + c] = TERRAIN.forest;
  }

  return cells;
}

function mulberry32(a) {
  return function () {
    a |= 0;
    a = (a + 0x6D2B79F5) | 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const cells = makeCells();

// 统计
const counts = { plain: 0, mountain: 0, forest: 0, river: 0 };
for (const c of cells) counts[c.type]++;

console.error("counts:", counts);

const yamlLines = [
  "# 艾瑟兰大陆世界地形（grid 编码，行优先 (row, col)）",
  "# 由 _tools/gen_world_terrain.ts 自动生成；分布：",
  `#   mountain: 矩形 (x:20..30, y:10..20) 共 ${counts.mountain} 格`,
  `#   river:    横向折线 (x:10..54, y=40..39) 共 ${counts.river} 格`,
  `#   forest:   散点 (x:5..15, y:30..38) ∪ (x:45..55, y:5..12) 共 ${counts.forest} 格`,
  `#   其余 plain 共 ${counts.plain} 格`,
  "encoding: grid",
  "default:",
  "  type: plain",
  "  move_cost: 1",
  "  buildable: true",
  "cells:",
];

// 行优先：每个 cell 单独一段缩进
for (let r = 0; r < H; r++) {
  yamlLines.push(`  # row ${r}`);
  for (let c = 0; c < W; c++) {
    const cell = cells[r * W + c];
    let entry = `  - type: ${cell.type}\n    move_cost: ${cell.move_cost}\n    buildable: ${cell.buildable}`;
    if (cell.resources) {
      entry += `\n    resources: [${cell.resources.join(", ")}]`;
    }
    yamlLines.push(entry);
  }
}

const outPath = process.argv[2] || "examples/sample_world/maps/world/main.terrain.yaml";
mkdirSync(dirname(outPath), { recursive: true });
writeFileSync(outPath, yamlLines.join("\n") + "\n", "utf8");
console.error("wrote", outPath);