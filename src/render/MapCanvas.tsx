// src/render/MapCanvas.tsx
// PixiJS 画布（MVP 阶段：地形 + 网格 + 城市标记 + 视口交互）。
//
// **MVP 简化**：本组件用 PixiJS 8 直接渲染，不走 React 渲染循环（避免 React StrictMode 双初始化）。
// `useEffect` cleanup 中必须调用 `app.destroy(true, { children: true, texture: true })`。
//
// 视口：左键拖拽 pan / 中键 / 右键拖拽 pan / 滚轮 zoom（围绕指针）。

import { useEffect, useRef } from "react";
import { Application, Container, Graphics, Text } from "pixi.js";
import { useDomainStore } from "@/state/useDomainStore";
import { useUiStore } from "@/state/useUiStore";
import { TERRAIN_COLORS } from "@/render/terrainPalette";
import type { TerrainCell } from "@/types";
import { clampScale, fitTo } from "@/render/Viewport";

export function MapCanvas() {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const appRef = useRef<Application | null>(null);
  const worldRef = useRef<Container | null>(null);
  const gridRef = useRef<Container | null>(null);
  const terrainRef = useRef<Container | null>(null);
  const markerRef = useRef<Container | null>(null);
  const draggingRef = useRef<{ startX: number; startY: number; vpX: number; vpY: number } | null>(null);
  const sizeRef = useRef<{ w: number; h: number }>({ w: 800, h: 600 });

  const currentMapPath = useDomainStore((s) => s.currentMapPath);
  const mapSummaries = useDomainStore((s) => s.mapSummaries);
  const viewport = useUiStore((s) => s.viewport);
  const setViewport = useUiStore((s) => s.setViewport);
  const showGrid = useUiStore((s) => s.showGrid);
  const toggleGrid = useUiStore((s) => s.toggleGrid);
  const toggleCoordinates = useUiStore((s) => s.toggleCoordinates);
  void viewport; void showGrid; void toggleGrid; void toggleCoordinates;
  void setViewport;

  // 初始化 Application（StrictMode 兼容：cleanup 调用 app.destroy）
  useEffect(() => {
    if (!containerRef.current) return;
    const app = new Application();
    appRef.current = app;
    let mounted = true;

    app.init({
      width: containerRef.current.clientWidth || 800,
      height: containerRef.current.clientHeight || 600,
      backgroundColor: 0x0d0d0d,
      antialias: true,
    }).then(() => {
      if (!mounted) {
        app.destroy(true, { children: true, texture: true });
        return;
      }
      const root = containerRef.current;
      if (!root) return;
      root.appendChild(app.canvas);
      sizeRef.current = { w: app.screen.width, h: app.screen.height };

      const world = new Container();
      const gridLayer = new Container();
      const terrainLayer = new Container();
      const markerLayer = new Container();
      app.stage.addChild(world);
      world.addChild(terrainLayer);
      world.addChild(gridLayer);
      world.addChild(markerLayer);

      worldRef.current = world;
      gridRef.current = gridLayer;
      terrainRef.current = terrainLayer;
      markerRef.current = markerLayer;

      redraw();
    });

    return () => {
      mounted = false;
      const a = appRef.current;
      if (a) {
        a.destroy(true, { children: true, texture: true });
        appRef.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // currentMapPath / viewport / showGrid 变化时重绘
  useEffect(() => {
    if (!appRef.current) return;
    redraw();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentMapPath, viewport.x, viewport.y, viewport.scale, showGrid]);

  // 窗口尺寸变化时 resize PixiJS
  useEffect(() => {
    const handler = () => {
      const root = containerRef.current;
      const app = appRef.current;
      if (!root || !app) return;
      const w = root.clientWidth;
      const h = root.clientHeight;
      if (w > 0 && h > 0) {
        app.renderer.resize(w, h);
        sizeRef.current = { w, h };
        redraw();
      }
    };
    window.addEventListener("resize", handler);
    return () => window.removeEventListener("resize", handler);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function redraw() {
    const app = appRef.current;
    const world = worldRef.current;
    const terrainLayer = terrainRef.current;
    const gridLayer = gridRef.current;
    const markerLayer = markerRef.current;
    if (!app || !world || !terrainLayer || !gridLayer || !markerLayer) return;

    const summary = mapSummaries.find((m) => m.path === currentMapPath);
    if (!summary) {
      terrainLayer.removeChildren();
      gridLayer.removeChildren();
      markerLayer.removeChildren();
      return;
    }

    // MVP 阶段：地形数据用 mock（不依赖 IPC）
    const gridW = 64;
    const gridH = 48;

    // 应用视口（用 world.x/y/scale 直接做 transform）
    world.x = viewport.x;
    world.y = viewport.y;
    world.scale.set(viewport.scale);

    // === Terrain ===
    terrainLayer.removeChildren();
    for (let r = 0; r < gridH; r++) {
      for (let c = 0; c < gridW; c++) {
        // MVP mock：固定 mountain 矩形 + 河流直线（与示例工程对应）
        const cell = mockCell(c, r);
        const color = TERRAIN_COLORS[cell.type] ?? 0x9ab87a;
        const g = new Graphics();
        g.rect(c, r, 1, 1).fill({ color });
        terrainLayer.addChild(g);
      }
    }

    // === Grid ===
    gridLayer.removeChildren();
    if (showGrid) {
      const g = new Graphics();
      g.moveTo(0, 0).lineTo(gridW, 0).stroke({ width: 1 / viewport.scale, color: 0x666666, alpha: 0.4 });
      for (let x = 0; x <= gridW; x++) {
        g.moveTo(x, 0).lineTo(x, gridH);
      }
      for (let y = 0; y <= gridH; y++) {
        g.moveTo(0, y).lineTo(gridW, y);
      }
      g.stroke({ width: 1 / viewport.scale, color: 0x666666, alpha: 0.4 });
      gridLayer.addChild(g);
    }

    // === Markers (MVP mock: 极光港) ===
    markerLayer.removeChildren();
    if (summary.type === "world") {
      const dot = new Graphics();
      dot.circle(0, 0, 0.5).fill({ color: 0xffd166 });
      dot.stroke({ width: 1 / viewport.scale, color: 0x000000, alpha: 0.6 });
      dot.position.set(32, 24);
      markerLayer.addChild(dot);
      const label = new Text({
        text: "极光港",
        style: { fontSize: 12 / viewport.scale, fill: 0xffffff, fontFamily: "system-ui" },
      });
      label.position.set(33, 24);
      markerLayer.addChild(label);
    }
  }

  // ====== 鼠标交互 ======
  function onPointerDown(e: React.PointerEvent<HTMLDivElement>) {
    if (!appRef.current) return;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const sx = e.clientX - rect.left;
    const sy = e.clientY - rect.top;
    // 左键 + pan / 任何键：拖拽平移
    if (e.button === 0 || e.button === 1 || e.button === 2) {
      draggingRef.current = { startX: sx, startY: sy, vpX: viewport.x, vpY: viewport.y };
      (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    }
  }
  function onPointerMove(e: React.PointerEvent<HTMLDivElement>) {
    if (!draggingRef.current) return;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const sx = e.clientX - rect.left;
    const sy = e.clientY - rect.top;
    const d = draggingRef.current;
    setViewport({ x: d.vpX + (sx - d.startX), y: d.vpY + (sy - d.startY) });
  }
  function onPointerUp(e: React.PointerEvent<HTMLDivElement>) {
    draggingRef.current = null;
    (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
  }
  function onWheel(e: React.WheelEvent<HTMLDivElement>) {
    if (!appRef.current) return;
    e.preventDefault();
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const sx = e.clientX - rect.left;
    const sy = e.clientY - rect.top;
    const factor = Math.exp(-e.deltaY * 0.001);
    const nextScale = clampScale(viewport.scale * factor);
    // 围绕 (sx, sy) 缩放
    const gridX = (sx - viewport.x) / viewport.scale;
    const gridY = (sy - viewport.y) / viewport.scale;
    const newX = sx - gridX * nextScale;
    const newY = sy - gridY * nextScale;
    setViewport({ scale: nextScale, x: newX, y: newY });
  }

  // 键盘快捷键
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      // 仅在画布聚焦时生效（避免抢掉 input）
      if (document.activeElement && (document.activeElement as HTMLElement).tagName === "INPUT") return;
      if (e.key === "f" || e.key === "F") {
        const summary = mapSummaries.find((m) => m.path === currentMapPath);
        if (!summary) return;
        const fit = fitTo(64, summary.type === "world" ? 48 : 64, sizeRef.current.w, sizeRef.current.h);
        setViewport(fit);
      } else if (e.key === "g" || e.key === "G") {
        toggleGrid();
      } else if (e.key === "c" || e.key === "C") {
        toggleCoordinates();
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [mapSummaries, currentMapPath, setViewport, toggleGrid, toggleCoordinates]);

  function mockCell(c: number, r: number): TerrainCell {
    // 与 examples/sample_world/maps/world/main.terrain.yaml 分布一致
    if (c >= 20 && c <= 30 && r >= 10 && r <= 20) {
      return { type: "mountain", move_cost: 4, buildable: false };
    }
    if (r === 40 && c >= 10 && c <= 54) {
      return { type: "river", move_cost: -1, buildable: false };
    }
    if ((r === 39) && (c === 53 || c === 54)) {
      return { type: "river", move_cost: -1, buildable: false };
    }
    // forest 略
    return { type: "plain", move_cost: 1, buildable: true };
  }

  return (
    <div
      ref={containerRef}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
      onWheel={onWheel}
      onContextMenu={(e) => e.preventDefault()}
      style={{ width: "100%", height: "100%", cursor: "grab", userSelect: "none" }}
    />
  );
}