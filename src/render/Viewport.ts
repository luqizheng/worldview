// src/render/Viewport.ts
// 视口数学：screen→grid 与 grid→screen 互转（spec §5.3）。
// 坐标系：屏幕左上 = (0, 0)；网格 (col, row) → 屏幕 (x, y) = (col*scale + offsetX, row*scale + offsetY)。
// offsetX = viewport.x（屏幕原点对应的世界坐标）

import type { Viewport } from "@/state/useDomainStore";

export interface GridCoord {
  col: number;
  row: number;
}

export function screenToGrid(sx: number, sy: number, vp: Viewport): GridCoord {
  return {
    col: Math.floor((sx - vp.x) / vp.scale),
    row: Math.floor((sy - vp.y) / vp.scale),
  };
}

export function gridToScreen(col: number, row: number, vp: Viewport): { x: number; y: number } {
  return { x: col * vp.scale + vp.x, y: row * vp.scale + vp.y };
}

/** 计算「视口覆盖的网格范围（含 1 格边带冗余）」，供 TerrainLayer cull。 */
export function visibleGridRange(
  vp: Viewport,
  screenW: number,
  screenH: number,
  gridW: number,
  gridH: number,
): { colMin: number; colMax: number; rowMin: number; rowMax: number } {
  const colMin = Math.max(0, Math.floor((0 - vp.x) / vp.scale) - 1);
  const rowMin = Math.max(0, Math.floor((0 - vp.y) / vp.scale) - 1);
  const colMax = Math.min(
    gridW - 1,
    Math.ceil((screenW - vp.x) / vp.scale) + 1,
  );
  const rowMax = Math.min(
    gridH - 1,
    Math.ceil((screenH - vp.y) / vp.scale) + 1,
  );
  return { colMin, colMax, rowMin, rowMax };
}

export function clampScale(s: number): number {
  return Math.max(0.25, Math.min(8.0, s));
}

export function fitTo(gridW: number, gridH: number, screenW: number, screenH: number, padding = 0.08): Viewport {
  const padW = screenW * padding;
  const padH = screenH * padding;
  const scale = Math.min(
    (screenW - padW * 2) / Math.max(1, gridW),
    (screenH - padH * 2) / Math.max(1, gridH),
  );
  const clamped = clampScale(scale);
  const offsetX = (screenW - gridW * clamped) / 2;
  const offsetY = (screenH - gridH * clamped) / 2;
  return { x: offsetX, y: offsetY, scale: clamped };
}