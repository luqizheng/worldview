// src/types.ts
// MVP 阶段前端类型镜像。
// 数据契约以 `crates/world-view-core/src/model.rs` 为唯一真源；前端仅做最小镜像，
// 服务端返回什么形状，前端按相同字段读写（YAML 的 `serde(rename_all = "snake_case")` 已经统一了
// wire 字段名）。

export type MapType = "world" | "region" | "city" | "street" | "building" | "underground";

export type TerrainType =
  | "plain"
  | "mountain"
  | "forest"
  | "hill"
  | "water"
  | "desert"
  | "swamp"
  | "tundra"
  | "basin"
  | "coast"
  | "river";

export interface ProjectSettings {
  autosave?: boolean;
  backup_keep?: number;
}

export interface Project {
  project_version: string;
  name: string;
  author?: string;
  description?: string;
  era: Era;
  default_time: TimePoint;
  map_index: string[];
  character_index?: string[];
  timeline?: string;
  settings?: ProjectSettings;
}

export interface Era {
  name: string;
  start_year: number;
  months_per_year: number;
  days_per_month: number[];
  hours_per_day: number;
  minutes_per_hour: number;
  seconds_per_minute: number;
  leap?: { every: number; month: number; add_days: number };
}

export interface TimePoint {
  era?: string;
  year: number;
  month?: number;
  day?: number;
  hour?: number;
  minute?: number;
  second?: number;
}

export interface MapGrid {
  width: number;
  height: number;
  cell_distance: number;
  distance_unit: string;
  base_unit: string;
  height_unit?: string;
  origin: { x: number; y: number };
  show: boolean;
  snap: boolean;
  scale_label?: string;
}

export interface MapTime {
  default_granularity: string;
  min_granularity: string;
  display_format?: string;
  event_sort_unit?: string;
  inherit_calendar: boolean;
  allow_fuzzy: boolean;
}

export interface Layer {
  id: string;
  name: string;
  type: string;
  visible: boolean;
  locked: boolean;
  opacity: number;
  z_order: number;
  valid_from?: TimePoint;
  valid_to?: TimePoint;
  data?: unknown;
  params?: unknown;
}

export interface TerrainDataRef {
  encoding: string;
  cells_ref?: string;
  default?: unknown;
  cells?: unknown[];
}

export interface CityRef {
  id: string;
  name: string;
  x: number;
  y: number;
  entry?: { x: number; y: number };
  map_ref?: string;
  population?: number;
  faction?: string;
  notes?: string;
}

export interface BuildingGeometry {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface BuildingRef {
  id: string;
  name: string;
  type: string;
  geometry: BuildingGeometry;
  rotation?: number;
  floors?: number;
  entrance?: Array<{ x: number; y: number }>;
  interior_ref?: string;
  faction?: string;
  owner?: string;
  state?: string;
  custom_attributes?: unknown;
}

export interface MapPresentation {
  default_mode?: string;
  view_mode?: string;
  theme?: string;
  observer?: string;
  show_scale_bar?: boolean;
  show_coordinates?: boolean;
  show_minimap?: boolean;
}

export interface Map {
  id: string;
  name: string;
  type: MapType;
  parent_map?: string;
  child_maps?: string[];
  grid: MapGrid;
  time: MapTime;
  layers: Layer[];
  terrain_data?: TerrainDataRef;
  height_data?: TerrainDataRef;
  city_refs: CityRef[];
  building_refs: BuildingRef[];
  presentation?: MapPresentation;
  travel?: unknown;
}

export interface TerrainCell {
  type: TerrainType;
  move_cost: number;
  buildable: boolean;
  resources?: string[];
  height?: number;
}

export interface TerrainGrid {
  width: number;
  height: number;
  cells: TerrainCell[];
  default: TerrainCell;
}

export interface LoadedMap {
  path: string;
  map: Map;
  grid: TerrainGrid;
}

export interface LoadedProject {
  project: Project;
  maps: Record<string, LoadedMap>;
}

// ====== IPC DTOs (src-tauri 暴露给前端的形状) ======

export interface MapSummary {
  path: string;
  id: string;
  name: string;
  type: string;
}

export interface LoadedProjectDto {
  project: Project;
  maps: MapSummary[];
}

export interface CommandError {
  kind: string;
  path: string | null;
  cause: string;
}

export interface ValidationError {
  path: string;
  reason: string;
}