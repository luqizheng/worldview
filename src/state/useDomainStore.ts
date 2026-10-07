// src/state/useDomainStore.ts
// 领域 store：持有 Project / 所有 LoadedMap / 当前地图 / dirtyMapIds / breadcrumb。
//
// MVP 阶段 LoadedMap 的 grid 内存数据**不**通过 store 持有（pixijs 渲染层内部缓存）。
// store 只持有最小元数据，避免大网格在 React 渲染时触发不必要的复制。
// 详情：spec §6.2「领域 store」。

import { create } from "zustand";
import type { LoadedProjectDto, MapSummary, Project, ValidationError } from "@/types";
import * as bridge from "@/bridge/tauri";
import { useErrorStore } from "./useErrorStore";

export type ToolId = "pan" | "zoom" | "brush" | "placeBuilding";

export interface BrushParams {
  size: number;          // 笔刷半径（格），1-16
  terrainType: string;   // 地形类型 key
  strength: number;      // 0.1-1.0（MVP 不区分强弱，保留字段）
}

export interface Viewport {
  x: number;            // 屏幕坐标下的画布平移
  y: number;
  scale: number;        // 缩放，1 = 1 像素/格
}

export interface BreadcrumbItem {
  label: string;
  mapPath: string;
}

export interface DomainState {
  // === 工程 ===
  projectDir: string | null;
  project: Project | null;
  mapSummaries: MapSummary[];

  // === 当前地图 ===
  currentMapPath: string | null;
  currentMap: MapSummary | null;

  // === dirty 状态 ===
  dirtyMapPaths: Set<string>;

  // === 导航 ===
  breadcrumb: BreadcrumbItem[];

  // === 视口历史（key = mapPath） ===
  viewportHistory: Map<string, Viewport>;

  // === 选中对象（UI 状态，按 spec 应放 UI store；这里保留最小 ID 用于显示） ===
  selectedObjectId: string | null;

  // === actions ===
  openProject(dir: string): Promise<void>;
  saveProject(): Promise<void>;
  createCityMap(parentWorldPath: string, cityId: string, cityName: string, parentCityId: string): Promise<boolean>;
  switchMap(mapPath: string): void;
  switchToBreadcrumbIndex(i: number): void;
  markDirty(mapPath: string): void;
  clearDirty(mapPath?: string): void;
  setSelectedObject(id: string | null): void;
  saveViewport(mapPath: string, vp: Viewport): void;
}

export const useDomainStore = create<DomainState>((set, get) => ({
  projectDir: null,
  project: null,
  mapSummaries: [],
  currentMapPath: null,
  currentMap: null,
  dirtyMapPaths: new Set(),
  breadcrumb: [],
  viewportHistory: new Map(),
  selectedObjectId: null,

  async openProject(dir) {
    let dto: LoadedProjectDto;
    try {
      dto = await bridge.openProject(dir);
    } catch (e) {
      useErrorStore.getState().show({
        title: "打开工程失败",
        errors: [
          {
            path: dir,
            reason: String((e as Error)?.message ?? e),
          },
        ],
      });
      return;
    }
    const first = dto.maps[0] ?? null;
    set({
      projectDir: dir,
      project: dto.project,
      mapSummaries: dto.maps,
      currentMapPath: first?.path ?? null,
      currentMap: first,
      dirtyMapPaths: new Set(),
      breadcrumb: first
        ? [{ label: dto.project.name, mapPath: "__project__" }, { label: first.name, mapPath: first.path }]
        : [],
      viewportHistory: new Map(),
    });
  },

  async saveProject() {
    const { projectDir, project, dirtyMapPaths } = get();
    if (!projectDir || !project) return;
    if (dirtyMapPaths.size === 0) return; // 没变更不写盘
    try {
      await bridge.saveProject(projectDir, project);
      set({ dirtyMapPaths: new Set() });
    } catch (e) {
      // 后端返回 Vec<ValidationError> 时 Tauri 会序列化为 [{path, reason}] 列表
      const errs = parseValidationErrors(e);
      useErrorStore.getState().show({
        title: "保存校验未通过",
        errors: errs,
      });
    }
  },

  async createCityMap(parentWorldPath, cityId, cityName, parentCityId) {
    const { projectDir, project } = get();
    if (!projectDir || !project) return false;
    try {
      await bridge.createCityMap(projectDir, project, parentWorldPath, cityId, cityName, parentCityId);
      // 重新打开工程以拉取最新 map_index
      await get().openProject(projectDir);
      // 切到新建的 city_map
      get().switchMap(`maps/city/${cityId}.yaml`);
      return true;
    } catch (e) {
      useErrorStore.getState().show({
        title: "创建城市地图失败",
        errors: [
          {
            path: parentWorldPath,
            reason: String((e as Error)?.message ?? e),
          },
        ],
      });
      return false;
    }
  },

  switchMap(mapPath) {
    const { mapSummaries, breadcrumb } = get();
    const summary = mapSummaries.find((m) => m.path === mapPath);
    if (!summary) return;

    // 视口恢复由 UI store 在 useEffect 中检测 mapPath 变化后执行；
    // 视口保存由 MapCanvas 在 currentMapPath 变化时调用 saveViewport。

    // 更新面包屑：截断到当前路径再追加
    const idx = breadcrumb.findIndex((b) => b.mapPath === mapPath);
    const nextBreadcrumb: BreadcrumbItem[] =
      idx >= 0
        ? breadcrumb.slice(0, idx + 1)
        : [...breadcrumb, { label: summary.name, mapPath }];

    set({
      currentMapPath: mapPath,
      currentMap: summary,
      breadcrumb: nextBreadcrumb,
    });
  },

  switchToBreadcrumbIndex(i) {
    const item = get().breadcrumb[i];
    if (!item) return;
    if (item.mapPath === "__project__") {
      // 跳到第一张地图
      const first = get().mapSummaries[0];
      if (first) get().switchMap(first.path);
      return;
    }
    get().switchMap(item.mapPath);
  },

  markDirty(mapPath) {
    const next = new Set(get().dirtyMapPaths);
    next.add(mapPath);
    set({ dirtyMapPaths: next });
  },

  clearDirty(mapPath) {
    const next = new Set(get().dirtyMapPaths);
    if (mapPath) next.delete(mapPath);
    else next.clear();
    set({ dirtyMapPaths: next });
  },

  setSelectedObject(id) {
    set({ selectedObjectId: id });
  },

  saveViewport(mapPath, vp) {
    const next = new Map(get().viewportHistory);
    next.set(mapPath, vp);
    set({ viewportHistory: next });
  },
}));

function parseValidationErrors(e: unknown): ValidationError[] {
  // Tauri 把 Vec<ValidationError> 序列化为 Array<{path, reason}>
  if (Array.isArray(e)) {
    return (e as Array<{ path?: string; reason?: string }>).map((x) => ({
      path: String(x.path ?? ""),
      reason: String(x.reason ?? ""),
    }));
  }
  return [{ path: "", reason: String((e as Error)?.message ?? e) }];
}