// src/render/terrainPalette.ts
// 地形色板（spec §11.4）

import type { TerrainType } from "@/types";

export const TERRAIN_COLORS: Record<TerrainType, number> = {
  plain: 0x9ab87a,
  mountain: 0x7a6f64,
  forest: 0x3f6b3a,
  hill: 0xa89866,
  water: 0x3a6a9a,
  desert: 0xd8c890,
  swamp: 0x5e6e44,
  tundra: 0xcfd9d4,
  basin: 0x8a8a8a,
  coast: 0x9bb4d6,
  river: 0x3a6a9a,
};