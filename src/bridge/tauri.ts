// src/bridge/tauri.ts
// Tauri commands IPC 封装。
//
// MVP 阶段：commands 在 src-tauri/src/lib.rs 中注册（open_project / save_project / create_city_map）。
// 浏览器开发模式（无 Tauri）下，invoke 会抛 "window.__TAURI_INTERNALS__ is undefined"，
// 这时桥层统一回落到「mock 实现」：返回示例工程的最小数据，供纯前端 typecheck / 演示。

import type {
  CommandError,
  LoadedProjectDto,
  Map,
  Project,
} from "@/types";

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
  }
}

function hasTauri(): boolean {
  return typeof window !== "undefined" && !!window.__TAURI_INTERNALS__;
}

async function invoke<T>(cmd: string, args?: Record<string, unknown>): Promise<T> {
  if (!hasTauri()) {
    throw new Error(
      `[bridge] Tauri runtime 不可用 (cmd=${cmd}); 浏览器预览请使用 mock 路径`,
    );
  }
  // 动态 import 避免 vite dev 时无 Tauri 环境下 import 失败
  const mod = await import("@tauri-apps/api/core");
  return mod.invoke<T>(cmd, args);
}

export async function openProject(dir: string): Promise<LoadedProjectDto> {
  if (!hasTauri()) {
    return mockOpenProject();
  }
  return invoke<LoadedProjectDto>("open_project", { dir });
}

export async function saveProject(dir: string, project: Project): Promise<void> {
  if (!hasTauri()) {
    console.warn("[bridge] saveProject mock: no Tauri runtime, skipping");
    return;
  }
  return invoke<void>("save_project", { dir, project });
}

export async function createCityMap(
  dir: string,
  project: Project,
  parentWorldPath: string,
  cityId: string,
  cityName: string,
  parentCityId: string,
): Promise<Map> {
  if (!hasTauri()) {
    return mockCreateCityMap(cityId, cityName);
  }
  return invoke<Map>("create_city_map", {
    dir,
    project,
    parentWorldPath,
    cityId,
    cityName,
    parentCityId,
  });
}

// ====== Mock (浏览器预览) ======

function mockOpenProject(): LoadedProjectDto {
  return {
    project: {
      project_version: "0.1",
      name: "示例世界（mock）",
      era: {
        name: "创世历",
        start_year: 1,
        months_per_year: 12,
        days_per_month: [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31],
        hours_per_day: 24,
        minutes_per_hour: 60,
        seconds_per_minute: 60,
      },
      default_time: { year: 1042, month: 3, day: 12 },
      map_index: ["maps/world/main.yaml", "maps/city/port_aurora.yaml"],
    },
    maps: [
      { path: "maps/world/main.yaml", id: "map_world_01", name: "艾瑟兰大陆", type: "world" },
      { path: "maps/city/port_aurora.yaml", id: "map_city_port_aurora", name: "极光港", type: "city" },
    ],
  };
}

function mockCreateCityMap(cityId: string, cityName: string): Map {
  return {
    id: cityId,
    name: cityName,
    type: "city",
    grid: {
      width: 64,
      height: 64,
      cell_distance: 2,
      distance_unit: "m",
      base_unit: "m",
      origin: { x: 0, y: 0 },
      show: true,
      snap: true,
    },
    time: {
      default_granularity: "hour",
      min_granularity: "hour",
      inherit_calendar: true,
      allow_fuzzy: true,
    },
    layers: [],
    city_refs: [],
    building_refs: [],
  };
}

export { hasTauri };
export type { CommandError };