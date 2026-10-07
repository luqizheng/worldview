// src/render/MapCanvas.tsx
// PixiJS 画布主组件（spec §11）：
//   - viewport (screen<->grid) 由 Viewport.ts 提供
//   - 视口裁剪（cull）：TerrainLayer / GridLayer 仅绘制可见视口覆盖的 cell
//   - 鼠标交互：pan（左/中/右键）+ 滚轮 zoom（围绕指针）+ Esc 复位 pan + 100ms 节流笔刷
//   - 快捷键：F（fit）、G（toggle grid）、C（toggle coordinates）、Ctrl+S / Ctrl+Z / Ctrl+Y / Esc
//
// **MVP 简化**：
//   - 地形数据来源：TerrainLayer 的 mock（与示例工程一致）；M1 后续会切到 LoadedMap.grid
//   - 笔刷命令：useEditingStore 持有 UndoStack；本组件调用 applyRoundBrush + runCommand
//
// 视口裁剪量化（spec §J2）：getDrawnCellCount() ≤ visibleCellCount × 1.2。

import { useEffect, useRef } from "react";
import { Application, Container } from "pixi.js";
import { useDomainStore } from "@/state/useDomainStore";
import { useUiStore } from "@/state/useUiStore";
import { useEditingStore } from "@/state/useEditingStore";
import { TerrainLayer } from "@/render/TerrainLayer";
import { GridLayer } from "@/render/GridLayer";
import { MarkerLayer } from "@/render/MarkerLayer";
import { clampScale, fitTo, visibleGridRange } from "@/render/Viewport";

const VIEWPORT_THROTTLE_MS = 100;

export function MapCanvas() {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const appRef = useRef<Application | null>(null);
  const worldRef = useRef<Container | null>(null);
  const terrainRef = useRef<TerrainLayer | null>(null);
  const gridRef = useRef<GridLayer | null>(null);
  const markerRef = useRef<MarkerLayer | null>(null);
  const draggingRef = useRef<{ startX: number; startY: number; vpX: number; vpY: number } | null>(null);
  const sizeRef = useRef<{ w: number; h: number }>({ w: 800, h: 600 });
  const lastBrushAtRef = useRef<number>(0);

  const currentMapPath = useDomainStore((s) => s.currentMapPath);
  const currentMap = useDomainStore((s) => s.currentMap);
  const viewport = useUiStore((s) => s.viewport);
  const setViewport = useUiStore((s) => s.setViewport);
  const showGrid = useUiStore((s) => s.showGrid);
  const toggleGrid = useUiStore((s) => s.toggleGrid);
  const toggleCoordinates = useUiStore((s) => s.toggleCoordinates);
  const activeTool = useUiStore((s) => s.activeTool);
  const setTool = useUiStore((s) => s.setTool);
  const brushParams = useUiStore((s) => s.brushParams);
  const setBrushParams = useUiStore((s) => s.setBrushParams);

  // 拖拽 / 笔刷构建中的 stroke 缓冲
  const strokeBufRef = useRef<Array<{ col: number; row: number }>>([]);
  const strokeBeforeRef = useRef<Map<string, { col: number; row: number; cell: import("@/types").TerrainCell }> | null>(null);

  // PixiJS Application 初始化
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
      const terrainLayer = new TerrainLayer(64, 48);
      const gridLayer = new GridLayer(64, 48);
      const markerLayer = new MarkerLayer();
      app.stage.addChild(world);
      world.addChild(terrainLayer.container);
      world.addChild(gridLayer.container);
      world.addChild(markerLayer.container);

      worldRef.current = world;
      terrainRef.current = terrainLayer;
      gridRef.current = gridLayer;
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

  // 视口 / 当前地图 / showGrid 变化时重绘
  useEffect(() => {
    if (!appRef.current) return;
    redraw();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentMapPath, viewport.x, viewport.y, viewport.scale, showGrid]);

  // resize 时应用层跟随
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

    const { w, h } = sizeRef.current;
    const range = visibleGridRange(viewport, w, h, 64, 48);

    // 视口变换（world 容器整体位移 / 缩放）
    world.x = viewport.x;
    world.y = viewport.y;
    world.scale.set(viewport.scale);

    terrainLayer.update(range);
    if (showGrid) gridLayer.update(range, viewport.scale);
    else gridLayer.container.removeChildren();
    markerLayer.update(currentMap?.type ?? "world", viewport.scale);
  }

  // ====== 鼠标交互 ======
  function onPointerDown(e: React.PointerEvent<HTMLDivElement>) {
    if (!appRef.current) return;
    if (e.button !== 0 && e.button !== 1 && e.button !== 2) return;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const sx = e.clientX - rect.left;
    const sy = e.clientY - rect.top;

    // brush 工具：左键落点
    if (activeTool === "brush" && e.button === 0) {
      const grid = screenToGridInRect(sx, sy);
      if (!grid) return;
      // 初始化 stroke
      strokeBufRef.current = [{ col: grid.col, row: grid.row }];
      strokeBeforeRef.current = new Map();
      lastBrushAtRef.current = performance.now();
      applyBrushAt(grid.col, grid.row, false);
      (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
      return;
    }

    // 默认 / pan / placeBuilding：拖拽平移
    draggingRef.current = { startX: sx, startY: sy, vpX: viewport.x, vpY: viewport.y };
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  function onPointerMove(e: React.PointerEvent<HTMLDivElement>) {
    if (!appRef.current) return;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const sx = e.clientX - rect.left;
    const sy = e.clientY - rect.top;

    // brush 拖拽 + 100ms 节流
    if (activeTool === "brush" && strokeBufRef.current) {
      const now = performance.now();
      if (now - lastBrushAtRef.current >= VIEWPORT_THROTTLE_MS) {
        const grid = screenToGridInRect(sx, sy);
        if (grid) {
          applyBrushAt(grid.col, grid.row, true);
          lastBrushAtRef.current = now;
        }
      }
      return;
    }

    if (!draggingRef.current) return;
    const d = draggingRef.current;
    setViewport({ x: d.vpX + (sx - d.startX), y: d.vpY + (sy - d.startY) });
  }

  function onPointerUp(e: React.PointerEvent<HTMLDivElement>) {
    // brush 提交
    if (activeTool === "brush" && strokeBufRef.current) {
      const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
      const sx = e.clientX - rect.left;
      const sy = e.clientY - rect.top;
      const grid = screenToGridInRect(sx, sy);
      if (grid) {
        // mouseup 强制把当前位置并入一笔（spec §14.2）
        applyBrushAt(grid.col, grid.row, true);
      }
      commitStroke();
    }
    draggingRef.current = null;
    strokeBufRef.current = [];
    strokeBeforeRef.current = null;
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
    const gridX = (sx - viewport.x) / viewport.scale;
    const gridY = (sy - viewport.y) / viewport.scale;
    const newX = sx - gridX * nextScale;
    const newY = sy - gridY * nextScale;
    setViewport({ scale: nextScale, x: newX, y: newY });
  }

  function screenToGridInRect(sx: number, sy: number): { col: number; row: number } | null {
    const gridX = Math.floor((sx - viewport.x) / viewport.scale);
    const gridY = Math.floor((sy - viewport.y) / viewport.scale);
    if (gridX < 0 || gridX >= 64 || gridY < 0 || gridY >= 48) return null;
    return { col: gridX, row: gridY };
  }

  function applyBrushAt(col: number, row: number, record: boolean) {
    // 占位：MVP 阶段笔刷仅画在地形层上；真实实现会调用 EditingStore.runCommand(ApplyBrushCommand)
    // 当前 spec §E 阶段才进入；这里先放 mock：
    // - record=true 时记录到 strokeBufRef / strokeBeforeRef
    // - 不改 TerrainCell（mock 数据不修改）
    if (record && strokeBufRef.current) {
      strokeBufRef.current.push({ col, row });
    }
    // 视觉反馈：临时高亮（拖拽期间）
    const tl = terrainRef.current;
    if (!tl) return;
    // 简化：直接走 TerrainLayer.redrawRect 触发一次（实际应改 grid.cells）
    const r = Math.max(1, brushParams.size);
    const colMin = Math.max(0, col - r);
    const colMax = Math.min(63, col + r);
    const rowMin = Math.max(0, row - r);
    const rowMax = Math.min(47, row + r);
    tl.redrawRect({ colMin, colMax, rowMin, rowMax });
    void setBrushParams;
  }

  function commitStroke() {
    // 提交一条 ApplyBrushCommand 到 useEditingStore（spec §T13 / §T14）
    // MVP 阶段：mock 写一个空命令
    useEditingStore.getState().runCommand({
      kind: "apply_brush",
      apply: () => {},
      revert: () => {},
      dirtyRect: null,
    });
    // 触发 dirty 标记 + 重绘
    if (currentMapPath) useDomainStore.getState().markDirty(currentMapPath);
    redraw();
  }

  // 键盘快捷键
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const ae = document.activeElement as HTMLElement | null;
      if (ae && (ae.tagName === "INPUT" || ae.tagName === "TEXTAREA" || ae.isContentEditable)) return;
      const meta = e.ctrlKey || e.metaKey;
      if (meta && (e.key === "s" || e.key === "S")) {
        e.preventDefault();
        useDomainStore.getState().saveProject();
        return;
      }
      if (meta && (e.key === "z" || e.key === "Z")) {
        e.preventDefault();
        useEditingStore.getState().undoOne();
        return;
      }
      if (meta && (e.key === "y" || e.key === "Y")) {
        e.preventDefault();
        useEditingStore.getState().redoOne();
        return;
      }
      if (e.key === "Escape") {
        // 复位到 pan 工具
        setTool("pan");
        strokeBufRef.current = [];
        strokeBeforeRef.current = null;
        draggingRef.current = null;
        return;
      }
      if (e.key === "f" || e.key === "F") {
        const w = currentMap?.type === "city" ? 64 : 64;
        const h = currentMap?.type === "city" ? 64 : 48;
        const fit = fitTo(w, h, sizeRef.current.w, sizeRef.current.h);
        setViewport(fit);
      } else if (e.key === "g" || e.key === "G") {
        toggleGrid();
      } else if (e.key === "c" || e.key === "C") {
        toggleCoordinates();
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [currentMap, setTool, setViewport, toggleGrid, toggleCoordinates]);

  return (
    <div
      ref={containerRef}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
      onWheel={onWheel}
      onContextMenu={(e) => e.preventDefault()}
      style={{ width: "100%", height: "100%", cursor: activeTool === "brush" ? "crosshair" : "grab", userSelect: "none" }}
    />
  );
}