// src/state/useUiStore.ts
// UI store：当前工具 / 笔刷参数 / 视口 / 选中 / 网格显示 / 图层面板覆盖。

import { create } from "zustand";
import type { Viewport, ToolId, BrushParams } from "./useDomainStore";

export interface UiState {
  activeTool: ToolId;
  brushParams: BrushParams;
  viewport: Viewport;
  showGrid: boolean;
  showCoordinates: boolean;
  selectedObjectId: string | null;
  layerPanelOpen: boolean;
  isDirty: boolean;

  // actions
  setTool(tool: ToolId): void;
  setBrushParams(p: Partial<BrushParams>): void;
  setViewport(vp: Partial<Viewport>): void;
  toggleGrid(): void;
  toggleCoordinates(): void;
  setSelectedObjectId(id: string | null): void;
  toggleLayerPanel(): void;
}

export const useUiStore = create<UiState>((set, get) => ({
  activeTool: "pan",
  brushParams: { size: 3, terrainType: "plain", strength: 1.0 },
  viewport: { x: 0, y: 0, scale: 1 },
  showGrid: true,
  showCoordinates: true,
  selectedObjectId: null,
  layerPanelOpen: true,
  isDirty: false,

  setTool(tool) {
    set({ activeTool: tool });
  },
  setBrushParams(p) {
    set({ brushParams: { ...get().brushParams, ...p } });
  },
  setViewport(vp) {
    set({ viewport: { ...get().viewport, ...vp } });
  },
  toggleGrid() {
    set({ showGrid: !get().showGrid });
  },
  toggleCoordinates() {
    set({ showCoordinates: !get().showCoordinates });
  },
  setSelectedObjectId(id) {
    set({ selectedObjectId: id });
  },
  toggleLayerPanel() {
    set({ layerPanelOpen: !get().layerPanelOpen });
  },
}));